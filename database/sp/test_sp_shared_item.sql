-- 难点④ 回归测试脚本（含逾期场景）
SET SERVEROUTPUT ON

-- ===== 1. 验证新序列 SEQ_CREDIT_LOG 存在 =====
BEGIN
    DBMS_OUTPUT.PUT_LINE('--- 1. SEQ_CREDIT_LOG 验证 ---');
    DBMS_OUTPUT.PUT_LINE('SEQ_CREDIT_LOG.NEXTVAL = ' || SEQ_CREDIT_LOG.NEXTVAL);
    DBMS_OUTPUT.PUT_LINE('SEQ_ITEM_LOAN.NEXTVAL = ' || SEQ_ITEM_LOAN.NEXTVAL);
    DBMS_OUTPUT.PUT_LINE('SEQ_MATERIAL_USAGE.NEXTVAL = ' || SEQ_MATERIAL_USAGE.NEXTVAL);
END;
/

-- ===== 2. 查看当前数据基线 =====
DECLARE
    v_avail NUMBER;
    v_stock NUMBER;
    v_score NUMBER;
BEGIN
    SELECT Available_Qty INTO v_avail FROM D_Shared_Item WHERE Item_ID = 1;
    SELECT Stock_Qty INTO v_stock FROM D_Repair_Material WHERE Material_ID = 1;
    BEGIN
        SELECT Current_Score INTO v_score FROM D_Credit_Account WHERE Student_ID = 'S001';
    EXCEPTION WHEN NO_DATA_FOUND THEN v_score := -1;
    END;
    DBMS_OUTPUT.PUT_LINE('--- 2. 数据基线 ---');
    DBMS_OUTPUT.PUT_LINE('Item_ID=1 Available_Qty = ' || v_avail);
    DBMS_OUTPUT.PUT_LINE('Material_ID=1 Stock_Qty = ' || v_stock);
    DBMS_OUTPUT.PUT_LINE('S001 Credit_Score = ' || v_score);
END;
/

-- ===== 3. SP_Borrow_Item 正常借出 =====
DECLARE
    rc NUMBER;
    lid NUMBER;
BEGIN
    DBMS_OUTPUT.PUT_LINE('--- 3. SP_Borrow_Item (正常借出) ---');
    SP_Borrow_Item(1, 'S001', rc, lid);
    DBMS_OUTPUT.PUT_LINE('Borrow rc=' || rc || ' loanId=' || lid);
END;
/

-- ===== 4. 验证库存扣减 =====
DECLARE
    v_avail NUMBER;
BEGIN
    SELECT Available_Qty INTO v_avail FROM D_Shared_Item WHERE Item_ID = 1;
    DBMS_OUTPUT.PUT_LINE('--- 4. 库存扣减验证 ---');
    DBMS_OUTPUT.PUT_LINE('After borrow: Available_Qty = ' || v_avail);
END;
/

-- ===== 5. SP_Return_Item 正常归还（无超期） =====
DECLARE
    rc NUMBER;
    v_lid NUMBER;
BEGIN
    SELECT MAX(Loan_ID) INTO v_lid FROM D_Item_Loan
    WHERE Student_ID = 'S001' AND Return_Time IS NULL;
    DBMS_OUTPUT.PUT_LINE('--- 5. SP_Return_Item (正常归还) ---');
    SP_Return_Item(v_lid, rc);
    DBMS_OUTPUT.PUT_LINE('Return rc=' || rc || ' (expect 0)');
END;
/

-- ===== 6. 验证库存回加 =====
DECLARE
    v_avail NUMBER;
BEGIN
    SELECT Available_Qty INTO v_avail FROM D_Shared_Item WHERE Item_ID = 1;
    DBMS_OUTPUT.PUT_LINE('--- 6. 库存回加验证 ---');
    DBMS_OUTPUT.PUT_LINE('After return: Available_Qty = ' || v_avail || ' (expect original)');
END;
/

-- ===== 7. 重复归还（并发竞态模拟） =====
DECLARE
    rc NUMBER;
    v_lid NUMBER;
BEGIN
    SELECT MAX(Loan_ID) INTO v_lid FROM D_Item_Loan WHERE Student_ID = 'S001';
    DBMS_OUTPUT.PUT_LINE('--- 7. 重复归还（并发竞态） ---');
    SP_Return_Item(v_lid, rc);
    DBMS_OUTPUT.PUT_LINE('Duplicate return rc=' || rc || ' (expect 1)');
END;
/

-- ===== 8. SP_Consume_Material 正常出库 =====
DECLARE
    rc NUMBER;
BEGIN
    DBMS_OUTPUT.PUT_LINE('--- 8. SP_Consume_Material ---');
    SP_Consume_Material(1, 1, 3, rc);
    DBMS_OUTPUT.PUT_LINE('Consume 3 units rc=' || rc || ' (expect 0)');
END;
/

-- ===== 9. 耗材库存不足 =====
DECLARE
    rc NUMBER;
BEGIN
    DBMS_OUTPUT.PUT_LINE('--- 9. 库存不足 ---');
    SP_Consume_Material(1, 1, 99999, rc);
    DBMS_OUTPUT.PUT_LINE('Consume 99999 rc=' || rc || ' (expect 2)');
END;
/

-- ============================================================
-- 逾期场景：借出 → 伪造逾期 3 天 → 巡检扣分 → 巡检幂等 → 归还补扣
-- ============================================================

-- ===== 10. 借出一个物品（用于逾期测试） =====
DECLARE
    rc NUMBER;
    lid NUMBER;
    v_score NUMBER;
BEGIN
    SP_Borrow_Item(1, 'S001', rc, lid);
    DBMS_OUTPUT.PUT_LINE('--- 10. 逾期测试：借出 Loan_ID=' || lid || ' ---');

    -- 伪造逾期 3 天：把 Borrow_Time 和 Due_Time 一起回退
    -- CK_D_ITEM_LOAN_DATES 要求 Due_Time > Borrow_Time，需同步调整
    UPDATE D_Item_Loan
    SET Borrow_Time = SYSDATE - 4,
        Due_Time    = SYSDATE - 3
    WHERE Loan_ID = lid;
    COMMIT;

    SELECT Current_Score INTO v_score FROM D_Credit_Account WHERE Student_ID = 'S001';
    DBMS_OUTPUT.PUT_LINE('Before overdue scan: Credit_Score = ' || v_score);
END;
/

-- ===== 11. 首次逾期巡检 —— 应扣 15 分（3天×5） =====
DECLARE
    v_score NUMBER;
    v_cnt  NUMBER;
BEGIN
    DBMS_OUTPUT.PUT_LINE('--- 11. 首次逾期巡检 ---');
    SP_Check_Overdue;

    SELECT Current_Score INTO v_score FROM D_Credit_Account WHERE Student_ID = 'S001';
    DBMS_OUTPUT.PUT_LINE('After 1st scan: Credit_Score = ' || v_score || ' (expect -15)');

    SELECT COUNT(*) INTO v_cnt FROM D_Credit_Log
    WHERE Student_ID = 'S001' AND Event_Key LIKE 'OVERDUE-SCAN-%';
    DBMS_OUTPUT.PUT_LINE('OVERDUE-SCAN log entries: ' || v_cnt || ' (expect 1)');
END;
/

-- ===== 12. 同一天再次巡检 —— Event_Key 幂等，不应重复扣分 =====
DECLARE
    v_score_before NUMBER;
    v_score_after  NUMBER;
BEGIN
    SELECT Current_Score INTO v_score_before FROM D_Credit_Account WHERE Student_ID = 'S001';

    DBMS_OUTPUT.PUT_LINE('--- 12. 同日二次巡检（幂等验证） ---');
    SP_Check_Overdue;

    SELECT Current_Score INTO v_score_after FROM D_Credit_Account WHERE Student_ID = 'S001';
    DBMS_OUTPUT.PUT_LINE('Score before: ' || v_score_before || ', after: ' || v_score_after);
    IF v_score_before = v_score_after THEN
        DBMS_OUTPUT.PUT_LINE('PASS: 同日无重复扣分');
    ELSE
        DBMS_OUTPUT.PUT_LINE('FAIL: 同日被重复扣分!');
    END IF;
END;
/

-- ===== 13. 归还逾期物品 —— 巡检已扣 15 分，归还应补扣 0 =====
DECLARE
    rc NUMBER;
    v_lid NUMBER;
    v_score_before NUMBER;
    v_score_after  NUMBER;
    v_penalty_log  NUMBER;
BEGIN
    SELECT MAX(Loan_ID) INTO v_lid FROM D_Item_Loan
    WHERE Student_ID = 'S001' AND Return_Time IS NULL;

    SELECT Current_Score INTO v_score_before FROM D_Credit_Account WHERE Student_ID = 'S001';

    DBMS_OUTPUT.PUT_LINE('--- 13. 归还逾期物品 (Loan_ID=' || v_lid || ') ---');
    SP_Return_Item(v_lid, rc);
    DBMS_OUTPUT.PUT_LINE('Return rc=' || rc || ' (expect 0)');

    SELECT Current_Score INTO v_score_after FROM D_Credit_Account WHERE Student_ID = 'S001';

    -- 查询归还事件扣分
    BEGIN
        SELECT ABS(Score_Change) INTO v_penalty_log FROM D_Credit_Log
        WHERE Event_Key = 'OVERDUE-' || v_lid;
    EXCEPTION WHEN NO_DATA_FOUND THEN v_penalty_log := 0;
    END;

    DBMS_OUTPUT.PUT_LINE('Score before return: ' || v_score_before || ', after: ' || v_score_after);
    DBMS_OUTPUT.PUT_LINE('Return penalty (expect 0, scan already deducted 15): ' || v_penalty_log);

    IF v_score_before = v_score_after THEN
        DBMS_OUTPUT.PUT_LINE('PASS: 归还未重复扣分（巡检已扣）');
    ELSE
        DBMS_OUTPUT.PUT_LINE('FAIL: 归还时被叠加扣分! Diff=' || (v_score_before - v_score_after));
    END IF;
END;
/

-- ===== 14. 总结 =====
DECLARE
    v_score NUMBER;
    v_total_deducted NUMBER;
BEGIN
    SELECT Current_Score INTO v_score FROM D_Credit_Account WHERE Student_ID = 'S001';

    SELECT NVL(SUM(ABS(Score_Change)), 0) INTO v_total_deducted FROM D_Credit_Log
    WHERE Student_ID = 'S001' AND Event_Key LIKE 'OVERDUE%';

    DBMS_OUTPUT.PUT_LINE('========================================');
    DBMS_OUTPUT.PUT_LINE('  逾期 3 天总结');
    DBMS_OUTPUT.PUT_LINE('  S001 最终信用分 = ' || v_score);
    DBMS_OUTPUT.PUT_LINE('  逾期总扣分 = ' || v_total_deducted || ' (expect 15)');
    DBMS_OUTPUT.PUT_LINE('========================================');
END;
/
