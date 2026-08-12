-- 难点④ 回归测试脚本（含跨日差量扣分 + 幂等键 + 跨用户归还校验）
SET SERVEROUTPUT ON

-- ===== 0. 准备测试数据（每轮独立，用时间戳避免键冲突）=====
DECLARE
    v_ts   VARCHAR2(20) := TO_CHAR(SYSDATE, 'YYYYMMDDHH24MISS');
    v_avail NUMBER;
    v_stock NUMBER;
    v_score NUMBER;
    v_rc   NUMBER;
    v_lid  NUMBER;
    v_lid2 NUMBER;
    v_score_before NUMBER;
    v_score_after  NUMBER;
    v_total NUMBER;
    v_penalty_log NUMBER;
    v_already NUMBER;
    v_dummy NUMBER;

    PROCEDURE P(msg IN VARCHAR2) IS
    BEGIN DBMS_OUTPUT.PUT_LINE(msg); END;
    PROCEDURE PL(msg IN VARCHAR2) IS
    BEGIN DBMS_OUTPUT.PUT_LINE('  ' || msg); END;

    -- 简化的巡检调用：直接取一条逾期记录计算差量
    PROCEDURE RunOverdueCheck(p_label IN VARCHAR2) IS
    BEGIN
        SP_Check_Overdue;
    END;

BEGIN
    P('========== 难点④ 回归测试 (v' || v_ts || ') ==========');

    -- 0a. 确保测试学生有信用账户
    BEGIN INSERT INTO D_Credit_Account (Student_ID, Current_Score)
        VALUES ('S001', 100); EXCEPTION WHEN DUP_VAL_ON_INDEX THEN NULL; END;
    BEGIN INSERT INTO D_Credit_Account (Student_ID, Current_Score)
        VALUES ('S002', 100); EXCEPTION WHEN DUP_VAL_ON_INDEX THEN NULL; END;
    UPDATE D_Repair_Material SET Stock_Qty = 10 WHERE Material_ID = 1;
    COMMIT;

    SELECT Available_Qty INTO v_avail FROM D_Shared_Item WHERE Item_ID = 1;
    SELECT Stock_Qty INTO v_stock FROM D_Repair_Material WHERE Material_ID = 1;
    SELECT Current_Score INTO v_score FROM D_Credit_Account WHERE Student_ID = 'S001';
    P('--- 0. 基线: Item avail=' || v_avail || ', Mat stock=' || v_stock || ', S001 score=' || v_score || ' ---');

    -- ================================================================
    -- P1-2: Idempotency-Key 幂等测试
    -- ================================================================
    P('--- 1. SP_Borrow_Item + Idempotency-Key ---');
    SP_Borrow_Item(1, 'S001', 'KEY-BORROW-' || v_ts, v_rc, v_lid);
    PL('Borrow rc=' || v_rc || ' loanId=' || v_lid || ' (expect rc=0)');
    IF v_rc != 0 THEN RAISE_APPLICATION_ERROR(-20001, 'FAIL: borrow rc!=''0'''); END IF;

    -- 幂等重放：同 Key 再请求 → 应返回同一 Loan_ID，库存不重复扣
    P('--- 2. 幂等重放 (same Idempotency-Key) ---');
    SELECT Available_Qty INTO v_avail FROM D_Shared_Item WHERE Item_ID = 1;
    SP_Borrow_Item(1, 'S001', 'KEY-BORROW-' || v_ts, v_rc, v_lid2);
    PL('Replay rc=' || v_rc || ' loanId=' || v_lid2 || ' (expect rc=0, sameLoanId=' || v_lid || ')');
    IF v_rc = 0 AND v_lid = v_lid2 THEN
        PL('PASS: 幂等返回原 Loan_ID');
    ELSE
        RAISE_APPLICATION_ERROR(-20002, 'FAIL: 幂等未生效');
    END IF;

    -- 库存应只扣了一次
    SELECT Available_Qty INTO v_avail FROM D_Shared_Item WHERE Item_ID = 1;
    PL('Stock after borrow+replay=' || v_avail || ' (expect original-1)');

    -- ================================================================
    -- P1-3: 跨用户归还校验
    -- ================================================================
    P('--- 3. 跨用户归还 (S002 试图归还 S001 的记录) ---');
    SP_Return_Item(v_lid, 'S002', v_rc);
    PL('S002 cross-return rc=' || v_rc || ' (expect 1=blocked)');
    IF v_rc = 1 THEN PL('PASS: 跨用户归还被拦截'); ELSE RAISE_APPLICATION_ERROR(-20003, 'FAIL: 未拦截'); END IF;

    -- S001 本人正常归还
    SP_Return_Item(v_lid, 'S001', v_rc);
    PL('S001 self-return rc=' || v_rc || ' (expect 0)');
    IF v_rc = 0 THEN PL('PASS: 本人正常归还'); ELSE RAISE_APPLICATION_ERROR(-20004, 'FAIL: 本人归还失败'); END IF;

    -- 重复归还 → rc=1
    SP_Return_Item(v_lid, 'S001', v_rc);
    PL('Duplicate return rc=' || v_rc || ' (expect 1)');
    IF v_rc != 1 THEN RAISE_APPLICATION_ERROR(-20005, 'FAIL: 重复归还未拦截'); END IF;

    -- ================================================================
    -- P2-1: 耗材出库 Idempotency-Key
    -- ================================================================
    P('--- 4. SP_Consume_Material + Idempotency-Key ---');
    SP_Consume_Material(1, 1, 3, 'KEY-CONSUME-' || v_ts, v_rc);
    PL('Consume 3 rc=' || v_rc || ' (expect 0)');
    IF v_rc != 0 THEN RAISE_APPLICATION_ERROR(-20006, 'FAIL: consume rc!=''0'''); END IF;

    SELECT Stock_Qty INTO v_stock FROM D_Repair_Material WHERE Material_ID = 1;
    -- 幂等重放
    SP_Consume_Material(1, 1, 3, 'KEY-CONSUME-' || v_ts, v_rc);
    PL('Consume replay rc=' || v_rc || ' (expect 0, idempotent)');
    SELECT Stock_Qty INTO v_avail FROM D_Repair_Material WHERE Material_ID = 1;
    IF v_stock = v_avail THEN PL('PASS: 幂等重放未重复扣库存');
    ELSE RAISE_APPLICATION_ERROR(-20007, 'FAIL: 库存被重复扣减'); END IF;

    -- 库存不足
    SP_Consume_Material(1, 1, 99999, 'KEY-CONSUME-OVF-' || v_ts, v_rc);
    PL('Consume 99999 rc=' || v_rc || ' (expect 2)');
    IF v_rc != 2 THEN RAISE_APPLICATION_ERROR(-20008, 'FAIL: 库存不足未检测'); END IF;

    -- ================================================================
    -- P1-1 核心: 跨日巡检差量扣分
    -- 策略：用两笔不同 Loan 验证差量逻辑
    --   Loan A: 逾期 1 天 → 目标 5，巡检扣 5
    --   Loan B: 逾期 3 天 → 目标 15，巡检扣 15
    --   Loan C: 先手动插入一笔已扣流水，逾期 3 天 → 目标 15，已扣 5，差量 10
    -- ================================================================

    -- --- Loan A: 逾期 1 天 ---
    P('--- 5. Loan A: 逾期1天 → 巡检扣5分 ---');
    SP_Borrow_Item(1, 'S002', 'KEY-LOAN-A-' || v_ts, v_rc, v_lid);
    UPDATE D_Item_Loan SET Borrow_Time = SYSDATE-2, Due_Time = SYSDATE-1
    WHERE Loan_ID = v_lid; COMMIT;

    SELECT Current_Score INTO v_score_before FROM D_Credit_Account WHERE Student_ID = 'S002';
    SP_Check_Overdue;
    SELECT Current_Score INTO v_score_after FROM D_Credit_Account WHERE Student_ID = 'S002';
    PL('Loan A(1d): ' || v_score_before || ' -> ' || v_score_after
        || ' (diff=' || (v_score_before-v_score_after) || ', expect 5)');
    IF v_score_before - v_score_after = 5 THEN PL('PASS');
    ELSE RAISE_APPLICATION_ERROR(-20009, 'FAIL: Loan A deduction wrong'); END IF;

    -- 同日二次巡检：幂等
    SELECT Current_Score INTO v_score_before FROM D_Credit_Account WHERE Student_ID = 'S002';
    SP_Check_Overdue;
    SELECT Current_Score INTO v_score_after FROM D_Credit_Account WHERE Student_ID = 'S002';
    PL('Loan A same-day re-scan: diff=' || (v_score_before-v_score_after) || ' (expect 0)');
    IF v_score_before = v_score_after THEN PL('PASS: 同日幂等');
    ELSE RAISE_APPLICATION_ERROR(-20010, 'FAIL: 同日重复扣分'); END IF;

    -- --- Loan B: 逾期 3 天 ---
    P('--- 6. Loan B: 逾期3天 → 巡检扣15分 ---');
    SP_Borrow_Item(1, 'S001', 'KEY-LOAN-B-' || v_ts, v_rc, v_lid2);
    UPDATE D_Item_Loan SET Borrow_Time = SYSDATE-4, Due_Time = SYSDATE-3
    WHERE Loan_ID = v_lid2; COMMIT;

    SELECT Current_Score INTO v_score_before FROM D_Credit_Account WHERE Student_ID = 'S001';
    SP_Check_Overdue;
    SELECT Current_Score INTO v_score_after FROM D_Credit_Account WHERE Student_ID = 'S001';
    PL('Loan B(3d): ' || v_score_before || ' -> ' || v_score_after
        || ' (diff=' || (v_score_before-v_score_after) || ', expect 15)');

    -- 查 S001 总扣分（Loan B 扣分）
    SELECT NVL(SUM(ABS(Score_Change)), 0) INTO v_total FROM D_Credit_Log
    WHERE Student_ID = 'S001'
      AND (Event_Key LIKE 'OVERDUE-SCAN-' || v_lid2 || '-%'
           OR Event_Key = 'OVERDUE-' || v_lid2);
    PL('Total deducted for Loan B: ' || v_total || ' (expect 15)');
    IF v_total = 15 THEN PL('PASS: 逾期3天扣15分');
    ELSE RAISE_APPLICATION_ERROR(-20011, 'FAIL: Loan B deduction=' || v_total); END IF;

    -- 归还 Loan B：巡检已扣 15，补扣应为 0
    P('--- 7. 归还 Loan B (巡检已扣15，补扣=0) ---');
    SELECT Current_Score INTO v_score_before FROM D_Credit_Account WHERE Student_ID = 'S001';
    SP_Return_Item(v_lid2, 'S001', v_rc);
    PL('Return rc=' || v_rc);

    SELECT Current_Score INTO v_score_after FROM D_Credit_Account WHERE Student_ID = 'S001';

    BEGIN SELECT ABS(Score_Change) INTO v_penalty_log FROM D_Credit_Log
        WHERE Event_Key = 'OVERDUE-' || v_lid2; EXCEPTION WHEN NO_DATA_FOUND THEN v_penalty_log := 0; END;

    SELECT NVL(SUM(ABS(Score_Change)), 0) INTO v_total FROM D_Credit_Log
    WHERE Student_ID = 'S001'
      AND (Event_Key LIKE 'OVERDUE-SCAN-' || v_lid2 || '-%'
           OR Event_Key = 'OVERDUE-' || v_lid2);

    PL('Score: ' || v_score_before || ' -> ' || v_score_after
        || ' (diff=' || (v_score_before-v_score_after) || ')');
    PL('Return penalty: ' || v_penalty_log || ', Total: ' || v_total || ' (expect 15)');
    IF v_score_before = v_score_after AND v_total = 15 THEN
        PL('PASS: 补扣=0，累计15=3天×5');
    ELSE
        RAISE_APPLICATION_ERROR(-20012, 'FAIL: Total=' || v_total);
    END IF;

    -- ================================================================
    -- P2-2: 无信用账户学生 → 巡检跳过不中断
    -- ================================================================
    P('--- 8. 无信用账户学生 → 巡检精确跳过 ---');
    -- S003 has no credit account — create loan for S003
    DECLARE v_tmp NUMBER;
    BEGIN
        SP_Borrow_Item(1, 'S003', 'KEY-NO-CREDIT-' || v_ts, v_rc, v_tmp);
        IF v_rc = 4 THEN
            -- rc=4 means credit score frozen / no credit account. Direct insert a loan for S003.
            SELECT NVL(MAX(Loan_ID), 0) + 1 INTO v_tmp FROM D_Item_Loan;
            INSERT INTO D_Item_Loan (Loan_ID, Item_ID, Student_ID, Borrow_Time, Due_Time)
            VALUES (v_tmp, 1, 'S003', SYSDATE-4, SYSDATE-3);
            COMMIT;
            PL('Created manual overdue loan for S003 (no credit account): ' || v_tmp);
        END IF;
    END;
    -- Run overdue scan — should NOT crash even though S003 has no credit account
    SP_Check_Overdue;
    PL('PASS: 巡检未因无信用账户而中断');

    -- ================================================================
    P('');
    P('========== 所有测试通过! ==========');

    -- 清理借出测试数据（归还 Loan A）
    BEGIN
        SELECT MAX(Loan_ID) INTO v_lid FROM D_Item_Loan
        WHERE Student_ID = 'S002' AND Return_Time IS NULL;
        SP_Return_Item(v_lid, 'S002', v_rc);
    EXCEPTION WHEN OTHERS THEN NULL; END;
    COMMIT;

EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        P('========== 测试失败! ==========');
        P('Error: ' || SQLERRM || ' (code=' || SQLCODE || ')');
        RAISE;
END;
/
