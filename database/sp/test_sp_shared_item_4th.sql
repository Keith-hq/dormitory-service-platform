-- 难点④ 四审修复验证测试（五审修订版）
--   T1/T2/T3 逾期边界：刚超 1 秒 / 1 小时 → Is_Overdue=1 且展示天数=1；
--              未到期 → Is_Overdue=0、天数=0
--   T4       幂等键 100 字符边界（迁移 019 CHAR 语义，SP 按 LENGTH 判定）：
--              ASCII 100 字符通过，101/102 字符 rc=6（借出）/rc=4（耗材）；
--              中文 100 字符通过（字节语义下会误拒，CHAR 语义对齐后通过），
--              101 字符 rc=6
--   T5       归还 SP 不触碰通知表（D_Notification 行数不变）
--   T6       自愈补扣候选谓词：已归还逾期且无 OVERDUE-{Loan_ID} 流水 → 命中；
--              已有流水 → 不命中；已有流水经迁移 014 触发器路径写入（不带 Log_ID）
-- 测试数据使用 4TH-% 幂等键与 Loan_ID 990001~990006，结尾自清理。
SET SERVEROUTPUT ON SIZE UNLIMITED
-- 会话切到 CHAR 语义：保证 VARCHAR2(200 CHAR) 声明按字符计长，
-- 中文键（300 字节）不会因 BYTE 语义的变量声明溢出。
-- 注意：RPAD 对全角字符按显示宽度减半填充（实测 RPAD('中',97,'中') 的
-- LENGTH=49），中文边界键因此用循环拼接构造（见 T4）。
-- SP 的 LENGTH 守卫按字符判定，与迁移 019 列宽 VARCHAR2(100 CHAR) 一致，
-- 不受会话长度语义影响。
ALTER SESSION SET NLS_LENGTH_SEMANTICS = CHAR;

-- ============ 0. 准备 ============
BEGIN
    DELETE FROM D_Item_Loan WHERE Idempotency_Key LIKE '4TH-%';
    DELETE FROM D_Item_Loan WHERE Loan_ID BETWEEN 990001 AND 990006;
    DELETE FROM D_Credit_Log WHERE Event_Key IN ('OVERDUE-990005', 'OVERDUE-990006');
    UPDATE D_Shared_Item SET Available_Qty = Total_Qty WHERE Item_ID = 1;
    UPDATE D_Repair_Material SET Stock_Qty = 10 WHERE Material_ID = 1;
    BEGIN INSERT INTO D_Credit_Account (Student_ID, Current_Score)
        VALUES ('S001', 100); EXCEPTION WHEN DUP_VAL_ON_INDEX THEN
        UPDATE D_Credit_Account SET Current_Score = 100 WHERE Student_ID = 'S001';
    END;
    COMMIT;
    DBMS_OUTPUT.PUT_LINE('SETUP OK');
END;
/

-- ============ T4 幂等键 100 字符边界（借出） ============
-- 迁移 019 列宽为 VARCHAR2(100 CHAR)，SP 按 LENGTH（字符）判定：
-- ASCII 100 字符通过、101/102 字符拒绝；中文 100 字符（300 字节）通过，
-- 101 字符拒绝——证明校验口径与列宽 CHAR 语义一致（字节口径会误拒中文）。
-- 中文键不用 RPAD 构造：本环境 RPAD 对全角字符按显示宽度减半填充
-- （RPAD('中',97,'中') 实测 LENGTH=49，48 个中+1 空格），改用循环逐字符
-- 拼接，长度精确可控。
-- 守卫位于 SP 最前，先于库存检查：每次可成功的调用前补回库存，
-- 确保拒绝超长键的 rc=6 不是库存不足（rc=3）的假象。
DECLARE
    v_rc     NUMBER;
    v_loan   NUMBER;
    v_key    VARCHAR2(200 CHAR);
    v_i      NUMBER;
    v_ok     NUMBER := 1;
    PROCEDURE Assert(cond IN BOOLEAN, msg IN VARCHAR2) IS
    BEGIN
        IF cond THEN DBMS_OUTPUT.PUT_LINE('  PASS: ' || msg);
        ELSE v_ok := 0; DBMS_OUTPUT.PUT_LINE('  FAIL: ' || msg);
        END IF;
    END;
BEGIN
    UPDATE D_Shared_Item SET Available_Qty = Total_Qty WHERE Item_ID = 1;
    COMMIT;
    SP_Borrow_Item(1, 'S001', '4TH-' || RPAD('X', 96, 'X'), v_rc, v_loan);  -- 4+96=100 字符
    Assert(v_rc = 0 AND v_loan IS NOT NULL, 'T4 100 字符键（列宽边界）借出成功 (rc=' || v_rc || ')');

    SP_Borrow_Item(1, 'S001', '4TH-' || RPAD('X', 97, 'X'), v_rc, v_loan);  -- 4+97=101 字符
    Assert(v_rc = 6, 'T4 101 字符键被拒绝 rc=6 (got ' || v_rc || ')');

    SP_Borrow_Item(1, 'S001', '4TH-' || RPAD('X', 98, 'X'), v_rc, v_loan);  -- 4+98=102 字符
    Assert(v_rc = 6, 'T4 102 字符键被拒绝 rc=6 (got ' || v_rc || ')');

    -- 中文 100 字符（300 字节）：CHAR 语义下与列宽一致，借出成功
    v_key := '4TH-';
    FOR v_i IN 1..96 LOOP v_key := v_key || '中'; END LOOP;
    SP_Borrow_Item(1, 'S001', v_key, v_rc, v_loan);
    Assert(v_rc = 0 AND v_loan IS NOT NULL,
        'T4 中文 100 字符键（300 字节）借出成功——CHAR 语义口径 (rc=' || v_rc || ')');

    -- 中文 101 字符：LENGTH 判定超 100，拒绝
    v_key := '4TH-';
    FOR v_i IN 1..97 LOOP v_key := v_key || '中'; END LOOP;
    SP_Borrow_Item(1, 'S001', v_key, v_rc, v_loan);
    Assert(v_rc = 6, 'T4 中文 101 字符键被拒绝 rc=6 (got ' || v_rc || ')');

    IF v_ok = 0 THEN RAISE_APPLICATION_ERROR(-20099, 'T4 FAIL'); END IF;
END;
/

-- ============ T4b 幂等键 100 字符边界（耗材出库） ============
DECLARE
    v_rc     NUMBER;
    v_ok     NUMBER := 1;
    PROCEDURE Assert(cond IN BOOLEAN, msg IN VARCHAR2) IS
    BEGIN
        IF cond THEN DBMS_OUTPUT.PUT_LINE('  PASS: ' || msg);
        ELSE v_ok := 0; DBMS_OUTPUT.PUT_LINE('  FAIL: ' || msg);
        END IF;
    END;
BEGIN
    UPDATE D_Repair_Material SET Stock_Qty = 10 WHERE Material_ID = 1;
    COMMIT;
    SP_Consume_Material(1, 1, 1, '4TH-' || RPAD('X', 96, 'X'), v_rc);  -- 100 字符，工单 1 存在
    Assert(v_rc = 0, 'T4b 100 字符键出库成功 (rc=' || v_rc || ')');

    SP_Consume_Material(1, 1, 1, '4TH-' || RPAD('X', 97, 'X'), v_rc);  -- 101 字符
    Assert(v_rc = 4, 'T4b 101 字符键被拒绝 rc=4 (got ' || v_rc || ')');

    SP_Consume_Material(1, 1, 1, '4TH-' || RPAD('X', 98, 'X'), v_rc);  -- 102 字符
    Assert(v_rc = 4, 'T4b 102 字符键被拒绝 rc=4 (got ' || v_rc || ')');

    IF v_ok = 0 THEN RAISE_APPLICATION_ERROR(-20099, 'T4b FAIL'); END IF;
END;
/

-- ============ T1 刚超时 1 秒 ============
BEGIN
    UPDATE D_Shared_Item SET Available_Qty = Total_Qty - 1 WHERE Item_ID = 1;
    INSERT INTO D_Item_Loan (Loan_ID, Item_ID, Student_ID, Borrow_Time, Due_Time)
        VALUES (990001, 1, 'S001', SYSDATE - 2, SYSDATE - 1/86400);
    COMMIT;
END;
/
DECLARE
    v_rc   NUMBER; v_days NUMBER; v_over NUMBER;
    v_ok   NUMBER := 1;
    PROCEDURE Assert(cond IN BOOLEAN, msg IN VARCHAR2) IS
    BEGIN
        IF cond THEN DBMS_OUTPUT.PUT_LINE('  PASS: ' || msg);
        ELSE v_ok := 0; DBMS_OUTPUT.PUT_LINE('  FAIL: ' || msg);
        END IF;
    END;
BEGIN
    SP_Return_Item(990001, 'S001', v_rc, v_days, v_over);
    Assert(v_rc = 0, 'T1 归还成功 (rc=' || v_rc || ')');
    Assert(v_over = 1, 'T1 刚超时 1 秒判定为逾期 (got ' || v_over || ')');
    Assert(v_days = 1, 'T1 展示天数至少为 1 (got ' || v_days || ')');
    IF v_ok = 0 THEN RAISE_APPLICATION_ERROR(-20099, 'T1 FAIL'); END IF;
END;
/

-- ============ T2 刚超时 1 小时 ============
BEGIN
    UPDATE D_Shared_Item SET Available_Qty = Total_Qty - 1 WHERE Item_ID = 1;
    INSERT INTO D_Item_Loan (Loan_ID, Item_ID, Student_ID, Borrow_Time, Due_Time)
        VALUES (990002, 1, 'S001', SYSDATE - 2, SYSDATE - 1/24);
    COMMIT;
END;
/
DECLARE
    v_rc   NUMBER; v_days NUMBER; v_over NUMBER;
    v_ok   NUMBER := 1;
    PROCEDURE Assert(cond IN BOOLEAN, msg IN VARCHAR2) IS
    BEGIN
        IF cond THEN DBMS_OUTPUT.PUT_LINE('  PASS: ' || msg);
        ELSE v_ok := 0; DBMS_OUTPUT.PUT_LINE('  FAIL: ' || msg);
        END IF;
    END;
BEGIN
    SP_Return_Item(990002, 'S001', v_rc, v_days, v_over);
    Assert(v_rc = 0, 'T2 归还成功 (rc=' || v_rc || ')');
    Assert(v_over = 1, 'T2 刚超时 1 小时判定为逾期 (got ' || v_over || ')');
    Assert(v_days = 1, 'T2 展示天数至少为 1 (got ' || v_days || ')');
    IF v_ok = 0 THEN RAISE_APPLICATION_ERROR(-20099, 'T2 FAIL'); END IF;
END;
/

-- ============ T3 未到期（Due_Time 在未来）+ T5 归还不碰通知表 ============
BEGIN
    UPDATE D_Shared_Item SET Available_Qty = Total_Qty - 1 WHERE Item_ID = 1;
    INSERT INTO D_Item_Loan (Loan_ID, Item_ID, Student_ID, Borrow_Time, Due_Time)
        VALUES (990003, 1, 'S001', SYSDATE - 1, SYSDATE + 1/24);
    COMMIT;
END;
/
DECLARE
    v_rc    NUMBER; v_days NUMBER; v_over NUMBER;
    v_before NUMBER; v_after NUMBER;
    v_ok    NUMBER := 1;
    PROCEDURE Assert(cond IN BOOLEAN, msg IN VARCHAR2) IS
    BEGIN
        IF cond THEN DBMS_OUTPUT.PUT_LINE('  PASS: ' || msg);
        ELSE v_ok := 0; DBMS_OUTPUT.PUT_LINE('  FAIL: ' || msg);
        END IF;
    END;
BEGIN
    SELECT COUNT(*) INTO v_before FROM D_Notification;
    SP_Return_Item(990003, 'S001', v_rc, v_days, v_over);
    SELECT COUNT(*) INTO v_after FROM D_Notification;
    Assert(v_rc = 0, 'T3 归还成功 (rc=' || v_rc || ')');
    Assert(v_over = 0, 'T3 未到期不判逾期 (got ' || v_over || ')');
    Assert(v_days = 0, 'T3 展示天数为 0 (got ' || v_days || ')');
    Assert(v_after = v_before, 'T5 归还 SP 不写通知表 (' || v_before || ' -> ' || v_after || ')');
    IF v_ok = 0 THEN RAISE_APPLICATION_ERROR(-20099, 'T3/T5 FAIL'); END IF;
END;
/

-- ============ T6 自愈补扣候选谓词（与应用层一致的 SQL 语义） ============
BEGIN
    INSERT INTO D_Item_Loan (Loan_ID, Item_ID, Student_ID, Borrow_Time, Due_Time, Return_Time)
        VALUES (990005, 1, 'S001', SYSDATE - 4, SYSDATE - 3, SYSDATE - 2);
    INSERT INTO D_Item_Loan (Loan_ID, Item_ID, Student_ID, Borrow_Time, Due_Time, Return_Time)
        VALUES (990006, 1, 'S001', SYSDATE - 4, SYSDATE - 3, SYSDATE - 2);
    -- 990006 已有扣分流水（模拟已扣分），990005 没有（模拟扣分失败待补偿）
    -- 五审修订：不带 Log_ID 直插——主键由迁移 014 触发器 TRG_D_CREDIT_LOG_ID_BI
    -- （WHEN NEW.Log_ID IS NULL）生成。MAX(Log_ID)+1 直插会烧掉基线序列下一值，
    -- 应用层下次插入即撞主键，与基线触发器路径不符。
    INSERT INTO D_Credit_Log (Student_ID, Score_Change, Reason, Event_Key, Create_Time)
        VALUES ('S001', -2, '测试已有扣分', 'OVERDUE-990006', SYSDATE);
    COMMIT;
END;
/
DECLARE
    v_cnt NUMBER;
    v_ok  NUMBER := 1;
    PROCEDURE Assert(cond IN BOOLEAN, msg IN VARCHAR2) IS
    BEGIN
        IF cond THEN DBMS_OUTPUT.PUT_LINE('  PASS: ' || msg);
        ELSE v_ok := 0; DBMS_OUTPUT.PUT_LINE('  FAIL: ' || msg);
        END IF;
    END;
BEGIN
    -- 与应用层 CompensatePendingDeductions 相同的候选语义：
    -- 已归还且逾期、且无 OVERDUE-{Loan_ID} 信用流水
    SELECT COUNT(*) INTO v_cnt
    FROM D_Item_Loan l
    WHERE l.Loan_ID IN (990005, 990006)
      AND l.Return_Time IS NOT NULL
      AND l.Return_Time > l.Due_Time
      AND NOT EXISTS (SELECT 1 FROM D_Credit_Log c
                      WHERE c.Event_Key = 'OVERDUE-' || l.Loan_ID);
    Assert(v_cnt = 1, 'T6 待补偿候选仅命中无流水的借出 (got ' || v_cnt || ')');
    IF v_ok = 0 THEN RAISE_APPLICATION_ERROR(-20099, 'T6 FAIL'); END IF;
END;
/

-- ============ 清理 ============
BEGIN
    DELETE FROM D_Item_Loan WHERE Idempotency_Key LIKE '4TH-%';
    DELETE FROM D_Item_Loan WHERE Loan_ID BETWEEN 990001 AND 990006;
    DELETE FROM D_Repair_Material_Usage WHERE Idempotency_Key LIKE '4TH-%';
    DELETE FROM D_Credit_Log WHERE Event_Key IN ('OVERDUE-990005', 'OVERDUE-990006');
    UPDATE D_Shared_Item SET Available_Qty = Total_Qty WHERE Item_ID = 1;
    UPDATE D_Repair_Material SET Stock_Qty = 10 WHERE Material_ID = 1;
    COMMIT;
    DBMS_OUTPUT.PUT_LINE('CLEANUP OK');
END;
/

EXIT
