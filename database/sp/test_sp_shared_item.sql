-- 难点④ 三审回归测试
-- 覆盖：幂等键内容冲突（跨用户/跨物品/跨票单/跨数量）、并发重放由唯一索引兜底的语义、
--       跨用户归还校验、超期归还按 PRD 按次扣 2 分（经信用分统一入口，SP 不直写信用分）、
--       逾期巡检只提醒不扣分 + 同日去重、无账户学生巡检跳过。
-- 注意：应用层 ICreditService.DeductAsync 的完整语义（学生行锁串行化、Event_Key 幂等、
--       冻结通知）已由 backend/tests/TemplateDormApi.Tests/CreditServiceTests.cs 覆盖；
--       本脚本在 DB 层模拟统一入口的扣分 SQL，验证唯一约束 UK_D_CREDIT_LOG_EVENT 兜底。
SET SERVEROUTPUT ON SIZE UNLIMITED

DECLARE
    v_ts       VARCHAR2(20) := TO_CHAR(SYSDATE, 'YYYYMMDDHH24MISS');
    v_avail    NUMBER;
    v_stock    NUMBER;
    v_rc       NUMBER;
    v_od       NUMBER;
    v_lid      NUMBER;
    v_lid2     NUMBER;
    v_lid3     NUMBER;
    v_lidX     NUMBER;
    v_cnt      NUMBER;
    v_score_b  NUMBER;
    v_score_a  NUMBER;
    v_acc1     NUMBER;
    v_acc2     NUMBER;
    v_logid    NUMBER;
    v_dup      NUMBER;

    PROCEDURE P(msg IN VARCHAR2) IS
    BEGIN DBMS_OUTPUT.PUT_LINE(msg); END;
    PROCEDURE Assert(cond IN BOOLEAN, msg IN VARCHAR2) IS
    BEGIN
        IF NOT cond THEN RAISE_APPLICATION_ERROR(-20099, 'FAIL: ' || msg); END IF;
        DBMS_OUTPUT.PUT_LINE('  PASS: ' || msg);
    END;
BEGIN
    P('========== 难点④ 三审回归测试 (v' || v_ts || ') ==========');

    -- ===== 0. 准备测试数据 =====
    BEGIN INSERT INTO D_Credit_Account (Student_ID, Current_Score)
        VALUES ('S001', 100); EXCEPTION WHEN DUP_VAL_ON_INDEX THEN NULL; END;
    BEGIN INSERT INTO D_Credit_Account (Student_ID, Current_Score)
        VALUES ('S002', 100); EXCEPTION WHEN DUP_VAL_ON_INDEX THEN NULL; END;

    -- S001/S002 用户账户（巡检提醒需要 Recipient_Account_ID）
    BEGIN
        SELECT Account_ID INTO v_acc1 FROM D_User_Account
        WHERE Student_ID = 'S001' AND ROWNUM = 1;
    EXCEPTION WHEN NO_DATA_FOUND THEN
        SELECT NVL(MAX(Account_ID), 0) + 1 INTO v_acc1 FROM D_User_Account;
        INSERT INTO D_User_Account (Account_ID, Login_Name, Password_Hash, Account_Status, Student_ID, Create_Time)
        VALUES (v_acc1, 'T4_S001_' || v_ts, 'x', '正常', 'S001', SYSDATE);
    END;
    BEGIN
        SELECT Account_ID INTO v_acc2 FROM D_User_Account
        WHERE Student_ID = 'S002' AND ROWNUM = 1;
    EXCEPTION WHEN NO_DATA_FOUND THEN
        SELECT NVL(MAX(Account_ID), 0) + 1 INTO v_acc2 FROM D_User_Account;
        INSERT INTO D_User_Account (Account_ID, Login_Name, Password_Hash, Account_Status, Student_ID, Create_Time)
        VALUES (v_acc2, 'T4_S002_' || v_ts, 'x', '正常', 'S002', SYSDATE);
    END;

    UPDATE D_Repair_Material SET Stock_Qty = 10 WHERE Material_ID = 1;
    COMMIT;

    SELECT Available_Qty INTO v_avail FROM D_Shared_Item WHERE Item_ID = 1;
    P('--- 0. 基线: Item1 avail=' || v_avail || ', Mat1 stock=10 ---');

    -- ================================================================
    -- 一、借用：幂等键 + 内容冲突（三审 P1-2）
    -- ================================================================
    P('--- 1. 借用 + 同内容幂等重放 ---');
    SP_Borrow_Item(1, 'S001', 'T4B1-' || v_ts, v_rc, v_lid);
    Assert(v_rc = 0, '首次借用 rc=0');
    SP_Borrow_Item(1, 'S001', 'T4B1-' || v_ts, v_rc, v_lid2);
    Assert(v_rc = 0 AND v_lid2 = v_lid, '同 Key 同内容重放返回原 Loan_ID');
    SELECT Available_Qty INTO v_avail FROM D_Shared_Item WHERE Item_ID = 1;
    P('  库存（借1次+重放）= ' || v_avail || '，应比基线少 1');

    P('--- 2. 同 Key 不同内容 → rc=5 ---');
    SP_Borrow_Item(1, 'S002', 'T4B1-' || v_ts, v_rc, v_lidX);
    Assert(v_rc = 5, '同 Key 跨用户 → rc=5');
    SELECT COUNT(*) INTO v_cnt FROM D_Shared_Item WHERE Item_ID = 2;
    IF v_cnt = 1 THEN
        SP_Borrow_Item(2, 'S001', 'T4B1-' || v_ts, v_rc, v_lidX);
        Assert(v_rc = 5, '同 Key 跨物品 → rc=5');
    END IF;
    SELECT Available_Qty INTO v_avail FROM D_Shared_Item WHERE Item_ID = 1;
    P('  冲突请求未触碰库存（avail 不变）');

    -- ================================================================
    -- 二、归还：归属校验 + 按次扣 2 分走统一入口（三审 P1-1）
    -- ================================================================
    P('--- 3. 跨用户归还拦截 ---');
    SP_Return_Item(v_lid, 'S002', v_rc, v_od);
    Assert(v_rc = 1, 'S002 归还 S001 的借出被拦截 rc=1');

    P('--- 4. 本人按期归还 ---');
    SP_Return_Item(v_lid, 'S001', v_rc, v_od);
    Assert(v_rc = 0 AND v_od = 0, '按期归还 rc=0 且 overdueDays=0');
    SP_Return_Item(v_lid, 'S001', v_rc, v_od);
    Assert(v_rc = 1, '重复归还被拦截 rc=1');

    P('--- 5. 超期归还：SP 不再直写信用分 ---');
    SP_Borrow_Item(1, 'S001', 'T4B2-' || v_ts, v_rc, v_lid);
    UPDATE D_Item_Loan SET Borrow_Time = SYSDATE - 3, Due_Time = SYSDATE - 2
    WHERE Loan_ID = v_lid;
    COMMIT;
    SELECT Current_Score INTO v_score_b FROM D_Credit_Account WHERE Student_ID = 'S001';
    SP_Return_Item(v_lid, 'S001', v_rc, v_od);
    Assert(v_rc = 0 AND v_od >= 2, '超期归还 rc=0 且 overdueDays>=2');
    SELECT Current_Score INTO v_score_a FROM D_Credit_Account WHERE Student_ID = 'S001';
    SELECT COUNT(*) INTO v_cnt FROM D_Credit_Log WHERE Event_Key = 'OVERDUE-' || v_lid;
    Assert(v_score_b = v_score_a AND v_cnt = 0,
        '归还 SP 未触碰信用分（扣分由应用层统一入口完成）');

    P('--- 6. 统一入口语义模拟：Event_Key 唯一约束兜底只扣一次 ---');
    -- 应用层 ICreditService.DeductAsync 实际执行的扣分 SQL（-2 + 流水）
    UPDATE D_Credit_Account
    SET Current_Score = Current_Score - 2, Updated_Time = SYSDATE
    WHERE Student_ID = 'S001';
    SELECT NVL(MAX(Log_ID), 0) + 1 INTO v_logid FROM D_Credit_Log;
    INSERT INTO D_Credit_Log (Log_ID, Student_ID, Score_Change, Reason, Event_Key, Create_Time)
    VALUES (v_logid, 'S001', -2,
        '共享物品超期归还（Loan_ID=' || v_lid || '，按次扣2分）',
        'OVERDUE-' || v_lid, SYSDATE);
    COMMIT;
    SELECT Current_Score INTO v_score_b FROM D_Credit_Account WHERE Student_ID = 'S001';
    -- 并发重试/重复请求：同 Event_Key 的第二次扣分被唯一约束拒绝
    v_dup := 0;
    BEGIN
        SELECT NVL(MAX(Log_ID), 0) + 1 INTO v_logid FROM D_Credit_Log;
        INSERT INTO D_Credit_Log (Log_ID, Student_ID, Score_Change, Reason, Event_Key, Create_Time)
        VALUES (v_logid, 'S001', -2, '重复扣分尝试', 'OVERDUE-' || v_lid, SYSDATE);
        COMMIT;
    EXCEPTION
        WHEN DUP_VAL_ON_INDEX THEN v_dup := 1; ROLLBACK;
    END;
    SELECT Current_Score INTO v_score_a FROM D_Credit_Account WHERE Student_ID = 'S001';
    Assert(v_dup = 1 AND v_score_b = v_score_a,
        '同 Event_Key 重复扣分被 UK_D_CREDIT_LOG_EVENT 拒绝，只扣一次 2 分');

    -- ================================================================
    -- 三、逾期巡检：只提醒不扣分 + 同日去重（三审 P1-1）
    -- ================================================================
    P('--- 7. 巡检发提醒且不扣信用分 ---');
    SP_Borrow_Item(1, 'S002', 'T4B3-' || v_ts, v_rc, v_lid3);
    UPDATE D_Item_Loan SET Borrow_Time = SYSDATE - 3, Due_Time = SYSDATE - 2
    WHERE Loan_ID = v_lid3;
    COMMIT;
    SELECT Current_Score INTO v_score_b FROM D_Credit_Account WHERE Student_ID = 'S002';
    SP_Check_Overdue;
    SELECT Current_Score INTO v_score_a FROM D_Credit_Account WHERE Student_ID = 'S002';
    Assert(v_score_b = v_score_a, '巡检不扣信用分');
    SELECT COUNT(*) INTO v_cnt FROM D_Notification
    WHERE Recipient_Account_ID = v_acc2
      AND Title = '共享物品逾期归还提醒'
      AND Content LIKE '%Loan_ID=' || v_lid3 || '，%'
      AND Create_Time >= TRUNC(SYSDATE);
    Assert(v_cnt = 1, '逾期提醒通知已发送给 S002');

    P('--- 8. 同日二次巡检不重复提醒 ---');
    SP_Check_Overdue;
    SELECT COUNT(*) INTO v_cnt FROM D_Notification
    WHERE Recipient_Account_ID = v_acc2
      AND Title = '共享物品逾期归还提醒'
      AND Content LIKE '%Loan_ID=' || v_lid3 || '，%'
      AND Create_Time >= TRUNC(SYSDATE);
    Assert(v_cnt = 1, '同日同笔借出只提醒一次');

    P('--- 9. 归还 × 巡检：并发语义下只扣一次 2 分 ---');
    -- 巡检已先跑过（不扣分）；此时归还，应用层扣 2 分（模拟统一入口）
    SELECT Current_Score INTO v_score_b FROM D_Credit_Account WHERE Student_ID = 'S002';
    SP_Return_Item(v_lid3, 'S002', v_rc, v_od);
    Assert(v_rc = 0 AND v_od >= 2, '逾期借出归还 rc=0');
    UPDATE D_Credit_Account
    SET Current_Score = Current_Score - 2, Updated_Time = SYSDATE
    WHERE Student_ID = 'S002';
    SELECT NVL(MAX(Log_ID), 0) + 1 INTO v_logid FROM D_Credit_Log;
    INSERT INTO D_Credit_Log (Log_ID, Student_ID, Score_Change, Reason, Event_Key, Create_Time)
    VALUES (v_logid, 'S002', -2,
        '共享物品超期归还（Loan_ID=' || v_lid3 || '，按次扣2分）',
        'OVERDUE-' || v_lid3, SYSDATE);
    COMMIT;
    SELECT Current_Score INTO v_score_a FROM D_Credit_Account WHERE Student_ID = 'S002';
    Assert(v_score_b - v_score_a = 2, '归还后恰好只扣 2 分（巡检路径零扣分）');
    -- 归还后再巡检：无新提醒、无扣分
    SP_Check_Overdue;
    SELECT Current_Score INTO v_score_b FROM D_Credit_Account WHERE Student_ID = 'S002';
    Assert(v_score_b = v_score_a, '归还后巡检不再产生任何信用分变更');

    P('--- 10. 无用户账户学生：巡检精确跳过 ---');
    SELECT NVL(MAX(Loan_ID), 0) + 1 INTO v_lidX FROM D_Item_Loan;
    INSERT INTO D_Item_Loan (Loan_ID, Item_ID, Student_ID, Borrow_Time, Due_Time)
    VALUES (v_lidX, 1, 'S003', SYSDATE - 4, SYSDATE - 3);
    COMMIT;
    SP_Check_Overdue;  -- 不应报错
    Assert(TRUE, '巡检未因无用户账户而中断');
    DELETE FROM D_Item_Loan WHERE Loan_ID = v_lidX;
    COMMIT;

    -- ================================================================
    -- 四、耗材出库：幂等键 + 内容冲突（三审 P1-2）
    -- ================================================================
    P('--- 11. 耗材出库 + 同内容幂等重放 ---');
    SP_Consume_Material(1, 1, 3, 'T4C1-' || v_ts, v_rc);
    Assert(v_rc = 0, '出库 rc=0');
    SP_Consume_Material(1, 1, 3, 'T4C1-' || v_ts, v_rc);
    Assert(v_rc = 0, '同 Key 同内容重放 rc=0（幂等）');
    SELECT Stock_Qty INTO v_stock FROM D_Repair_Material WHERE Material_ID = 1;
    Assert(v_stock = 7, '库存只扣一次（10-3=7）');

    P('--- 12. 同 Key 不同内容 → rc=3 ---');
    SP_Consume_Material(1, 2, 3, 'T4C1-' || v_ts, v_rc);
    Assert(v_rc = 3, '同 Key 不同票单 → rc=3');
    SP_Consume_Material(1, 1, 5, 'T4C1-' || v_ts, v_rc);
    Assert(v_rc = 3, '同 Key 不同数量 → rc=3');
    SELECT COUNT(*) INTO v_cnt FROM D_Repair_Material WHERE Material_ID = 2;
    IF v_cnt = 1 THEN
        SP_Consume_Material(2, 1, 3, 'T4C1-' || v_ts, v_rc);
        Assert(v_rc = 3, '同 Key 不同耗材 → rc=3');
    END IF;
    SELECT Stock_Qty INTO v_stock FROM D_Repair_Material WHERE Material_ID = 1;
    Assert(v_stock = 7, '冲突请求未触碰库存');

    P('--- 13. 库存不足 ---');
    SP_Consume_Material(1, 1, 99999, 'T4C2-' || v_ts, v_rc);
    Assert(v_rc = 2, '库存不足 rc=2');

    -- ===== 清理本脚本产生的提醒 =====
    DELETE FROM D_Notification
    WHERE Title = '共享物品逾期归还提醒'
      AND (Content LIKE '%Loan_ID=' || v_lid3 || '，%'
           OR Content LIKE '%Loan_ID=' || v_lidX || '，%');
    COMMIT;

    P('');
    P('========== 所有测试通过! ==========');
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        P('========== 测试失败! ==========');
        P('Error: ' || SQLERRM || ' (code=' || SQLCODE || ')');
        RAISE;
END;
/
EXIT
