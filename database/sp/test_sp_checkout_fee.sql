-- 难点⑥ 退宿结算测试（SP_Calc_Checkout_Fee v1.3 / SP_Calc_Monthly_Fee v1.3）
-- 覆盖分工对齐方案的五项验收 + 一审 R2 唯一索引互斥兜底：
--   T1 口径对齐：未发布费用不结算（与月度 SP 一致）                        （1 断言）
--   T2 正常结算：按 CheckOut_Date 结算当月已发布费用，金额=全额（单人房间） （4 断言）
--   T3 幂等重放：同流程重复调用只产生一行                                  （1 断言）
--   T5 两入口防重(正向)：退宿已结算 → 月度 SP 不再重复生成                （1 断言）
--   T6 两入口防重(反向)：月度已结算 → 退宿 SP 不再重复生成                （2 断言）
--   T4/T7 事务边界：SP 无内部 COMMIT（外层可回滚）+ CheckOut_Date 未写时
--         SYSDATE 防御兜底                                                 （1+3 断言）
--   T8 参数不匹配：Allocation 不存在时抛 NO_DATA_FOUND                     （1 断言）
--   T9 一审 R2：UK(Fee_ID, Student_ID) 互斥兜底——月度/退宿并存直插被拒    （2 断言）
-- 合计 16 条断言。
--
-- 一审 R4 修订（v1.3）：
--   ① 断言计数修正：14 → 16（新增 T9 两断言）。
--   ② 月份无关化：T4/T7 使用的 990003 费用账期 = 当月（TO_CHAR(SYSDATE)），
--      期望天数按 TRUNC(SYSDATE,'MM') / LAST_DAY(SYSDATE) 动态计算，
--      不再依赖 SYSDATE 落在 2026-08。
--      注意：T1/T2/T5/T6 使用固定账期 2026-05/2026-06（历史账期，与当月
--      不重叠）；若脚本恰在 2026-05 或 2026-06 月运行，需先改这两组 fixture。
--   ③ 失败不中止：断言 FAIL 只置 :g_fail=1，不再 RAISE_APPLICATION_ERROR，
--      会话不会中途中断，结尾清理段必定执行（清理段自身也带异常兜底），
--      无 S-CHK-001 残留。退出码 = :g_fail（0=全过，1=有失败），供 CI 判定。
--
-- 测试数据全部使用 99xxxx ID 段，与真实数据隔离，结尾自清理。
SET SERVEROUTPUT ON SIZE UNLIMITED

VARIABLE g_fail NUMBER
BEGIN :g_fail := 0; END;
/

-- ============ 0. 环境准备（清历史 + 建测试数据） ============
BEGIN
    DELETE FROM D_Fee_Detail WHERE Student_ID = 'S-CHK-001';
    DELETE FROM D_Utility_Fee WHERE Fee_ID IN (990001, 990002, 990003);
    DELETE FROM D_Bed_Allocation WHERE Allocation_ID = 990001;
    DELETE FROM D_Student WHERE Student_ID = 'S-CHK-001';
    DELETE FROM D_Room WHERE Room_ID = 9901;
    COMMIT;

    INSERT INTO D_Room (Room_ID, Room_Number, Capacity, Occupancy, Power_Status)
        VALUES (9901, 'TEST-CHK', 4, 1, '正常');
    INSERT INTO D_Student (Student_ID, Name) VALUES ('S-CHK-001', '测试退宿生');
    INSERT INTO D_Bed_Allocation (Allocation_ID, Student_ID, Room_ID, Bed_No, CheckIn_Date)
        VALUES (990001, 'S-CHK-001', 9901, 1, TO_DATE('2026-06-01', 'YYYY-MM-DD'));
    -- 2026-06 已发布（T2/T3/T5/T6/T9 用）、2026-05 未发布（T1 用）、当月已发布（T4/T7 用）
    INSERT INTO D_Utility_Fee (Fee_ID, Room_ID, Year_Month, Water_Fee, Power_Fee, Publish_Status)
        VALUES (990001, 9901, '2026-06', 300, 100, '已发布');
    INSERT INTO D_Utility_Fee (Fee_ID, Room_ID, Year_Month, Water_Fee, Power_Fee, Publish_Status)
        VALUES (990002, 9901, '2026-05', 50, 50, '未发布');
    INSERT INTO D_Utility_Fee (Fee_ID, Room_ID, Year_Month, Water_Fee, Power_Fee, Publish_Status)
        VALUES (990003, 9901, TO_CHAR(SYSDATE, 'YYYY-MM'), 310, 90, '已发布');
    COMMIT;
    DBMS_OUTPUT.PUT_LINE('SETUP OK');
EXCEPTION
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('SETUP FAILED: ' || SQLERRM);
        :g_fail := 1;
END;
/

-- ============ T1 口径对齐：未发布费用不结算 ============
BEGIN
    UPDATE D_Bed_Allocation SET CheckOut_Date = TO_DATE('2026-05-15', 'YYYY-MM-DD')
        WHERE Allocation_ID = 990001;
    COMMIT;
    SP_Calc_Checkout_Fee('S-CHK-001', 990001);
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
    SELECT COUNT(*) INTO v_cnt FROM D_Fee_Detail WHERE Student_ID = 'S-CHK-001';
    Assert(v_cnt = 0, 'T1 未发布费用不生成明细 (got ' || v_cnt || ')');
    IF v_ok = 0 THEN :g_fail := 1; END IF;
END;
/

-- ============ T2 正常结算：按 CheckOut_Date 结算当月已发布费用 ============
BEGIN
    UPDATE D_Bed_Allocation SET CheckOut_Date = TO_DATE('2026-06-15', 'YYYY-MM-DD')
        WHERE Allocation_ID = 990001;
    COMMIT;
    SP_Calc_Checkout_Fee('S-CHK-001', 990001);
    COMMIT;
END;
/
DECLARE
    v_cnt        NUMBER;
    v_water      NUMBER;
    v_power      NUMBER;
    v_stay       NUMBER;
    v_ok         NUMBER := 1;
    PROCEDURE Assert(cond IN BOOLEAN, msg IN VARCHAR2) IS
    BEGIN
        IF cond THEN DBMS_OUTPUT.PUT_LINE('  PASS: ' || msg);
        ELSE v_ok := 0; DBMS_OUTPUT.PUT_LINE('  FAIL: ' || msg);
        END IF;
    END;
BEGIN
    SELECT COUNT(*) INTO v_cnt FROM D_Fee_Detail
        WHERE Student_ID = 'S-CHK-001' AND Bill_Type = '退宿';
    Assert(v_cnt = 1, 'T2 生成一条退宿明细 (got ' || v_cnt || ')');
    SELECT Water_Share, Power_Share, Stay_Days
        INTO v_water, v_power, v_stay
        FROM D_Fee_Detail WHERE Student_ID = 'S-CHK-001' AND Bill_Type = '退宿';
    Assert(v_water = 300, 'T2 水费分摊=300 (got ' || v_water || ')');
    Assert(v_power = 100, 'T2 电费分摊=100 (got ' || v_power || ')');
    Assert(v_stay = 15, 'T2 入住天数 15 (got ' || v_stay || ')');
    IF v_ok = 0 THEN :g_fail := 1; END IF;
END;
/

-- ============ T3 幂等重放：重复调用只产生一行 ============
BEGIN
    SP_Calc_Checkout_Fee('S-CHK-001', 990001);
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
    SELECT COUNT(*) INTO v_cnt FROM D_Fee_Detail
        WHERE Student_ID = 'S-CHK-001' AND Bill_Type = '退宿';
    Assert(v_cnt = 1, 'T3 重放后仍只有一条退宿明细 (got ' || v_cnt || ')');
    IF v_ok = 0 THEN :g_fail := 1; END IF;
END;
/

-- ============ T5 两入口防重(正向)：退宿已结算 → 月度跳过 ============
BEGIN
    SP_Calc_Monthly_Fee('2026-06');
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
    SELECT COUNT(*) INTO v_cnt FROM D_Fee_Detail
        WHERE Student_ID = 'S-CHK-001' AND Bill_Type = '月度';
    Assert(v_cnt = 0, 'T5 退宿已结算后月度 SP 不再生成 (got ' || v_cnt || ')');
    IF v_ok = 0 THEN :g_fail := 1; END IF;
END;
/

-- ============ T6 两入口防重(反向)：月度已结算 → 退宿跳过 ============
BEGIN
    DELETE FROM D_Fee_Detail WHERE Student_ID = 'S-CHK-001' AND Bill_Type = '退宿';
    COMMIT;
    SP_Calc_Monthly_Fee('2026-06');
    COMMIT;
    SP_Calc_Checkout_Fee('S-CHK-001', 990001);
    COMMIT;
END;
/
DECLARE
    v_monthly NUMBER;
    v_checkout NUMBER;
    v_ok      NUMBER := 1;
    PROCEDURE Assert(cond IN BOOLEAN, msg IN VARCHAR2) IS
    BEGIN
        IF cond THEN DBMS_OUTPUT.PUT_LINE('  PASS: ' || msg);
        ELSE v_ok := 0; DBMS_OUTPUT.PUT_LINE('  FAIL: ' || msg);
        END IF;
    END;
BEGIN
    SELECT COUNT(*) INTO v_monthly FROM D_Fee_Detail
        WHERE Student_ID = 'S-CHK-001' AND Bill_Type = '月度';
    Assert(v_monthly = 1, 'T6 月度 SP 生成一条月度明细 (got ' || v_monthly || ')');
    SELECT COUNT(*) INTO v_checkout FROM D_Fee_Detail
        WHERE Student_ID = 'S-CHK-001' AND Bill_Type = '退宿';
    Assert(v_checkout = 0, 'T6 月度已结算后退宿 SP 不再生成 (got ' || v_checkout || ')');
    IF v_ok = 0 THEN :g_fail := 1; END IF;
END;
/

-- ============ T4 事务边界：SP 无内部 COMMIT，外层可回滚 ============
BEGIN
    UPDATE D_Bed_Allocation SET CheckOut_Date = NULL WHERE Allocation_ID = 990001;
    COMMIT;
END;
/
-- 关键：SP 调用后不 COMMIT，紧跟 ROLLBACK；若 SP 内部提交，此行会残留
BEGIN
    SP_Calc_Checkout_Fee('S-CHK-001', 990001);
END;
/
ROLLBACK;
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
    SELECT COUNT(*) INTO v_cnt FROM D_Fee_Detail
        WHERE Student_ID = 'S-CHK-001' AND Fee_ID = 990003;
    Assert(v_cnt = 0, 'T4 外层 ROLLBACK 后明细不残留，证明 SP 无内部 COMMIT (got ' || v_cnt || ')');
    IF v_ok = 0 THEN :g_fail := 1; END IF;
END;
/

-- ============ T7 防御兜底：CheckOut_Date 未写 → SYSDATE 结算（月份无关） ============
BEGIN
    SP_Calc_Checkout_Fee('S-CHK-001', 990001);
    COMMIT;
END;
/
DECLARE
    v_cnt    NUMBER;
    v_stay   NUMBER;
    v_exp    NUMBER;
    v_ok     NUMBER := 1;
    PROCEDURE Assert(cond IN BOOLEAN, msg IN VARCHAR2) IS
    BEGIN
        IF cond THEN DBMS_OUTPUT.PUT_LINE('  PASS: ' || msg);
        ELSE v_ok := 0; DBMS_OUTPUT.PUT_LINE('  FAIL: ' || msg);
        END IF;
    END;
BEGIN
    SELECT COUNT(*) INTO v_cnt FROM D_Fee_Detail
        WHERE Student_ID = 'S-CHK-001' AND Fee_ID = 990003 AND Bill_Type = '退宿';
    Assert(v_cnt = 1, 'T7 SYSDATE 兜底生成当月退宿明细 (got ' || v_cnt || ')');
    v_exp := TRUNC(SYSDATE) - TRUNC(SYSDATE, 'MM') + 1;
    SELECT Stay_Days INTO v_stay FROM D_Fee_Detail
        WHERE Student_ID = 'S-CHK-001' AND Fee_ID = 990003 AND Bill_Type = '退宿';
    Assert(v_stay = v_exp, 'T7 兜底按 SYSDATE 计算入住天数 (期望 ' || v_exp || ', got ' || v_stay || ')');
    IF v_ok = 0 THEN :g_fail := 1; END IF;
END;
/

-- ============ T8 参数不匹配：Allocation 不存在抛 NO_DATA_FOUND ============
DECLARE
    v_caught NUMBER := 0;
    v_ok     NUMBER := 1;
    PROCEDURE Assert(cond IN BOOLEAN, msg IN VARCHAR2) IS
    BEGIN
        IF cond THEN DBMS_OUTPUT.PUT_LINE('  PASS: ' || msg);
        ELSE v_ok := 0; DBMS_OUTPUT.PUT_LINE('  FAIL: ' || msg);
        END IF;
    END;
BEGIN
    BEGIN
        SP_Calc_Checkout_Fee('S-CHK-001', 999999);
    EXCEPTION
        WHEN NO_DATA_FOUND THEN v_caught := 1;
    END;
    Assert(v_caught = 1, 'T8 不存在的 Allocation 抛 NO_DATA_FOUND');
    IF v_ok = 0 THEN :g_fail := 1; END IF;
END;
/

-- ============ T9 一审 R2：UK(Fee_ID, Student_ID) 互斥兜底 ============
-- v1.3 唯一索引收紧后，同一 (Fee_ID, Student_ID) 不允许月度/退宿并存：
-- 990001 已有 '月度' 明细（T6 生成），直插 '退宿' 应被 ORA-00001 拒绝，
-- 且原月度明细不受影响（COUNT 预检查与 INSERT 之间的并发窗口由本约束兜底）。
DECLARE
    v_caught  NUMBER := 0;
    v_monthly NUMBER;
    v_checkout NUMBER;
    v_ok      NUMBER := 1;
    PROCEDURE Assert(cond IN BOOLEAN, msg IN VARCHAR2) IS
    BEGIN
        IF cond THEN DBMS_OUTPUT.PUT_LINE('  PASS: ' || msg);
        ELSE v_ok := 0; DBMS_OUTPUT.PUT_LINE('  FAIL: ' || msg);
        END IF;
    END;
BEGIN
    BEGIN
        INSERT INTO D_Fee_Detail (
            Detail_ID, Fee_ID, Student_ID,
            Water_Share, Power_Share, Stay_Days,
            Bill_Type, Is_Paid, Create_Time
        ) VALUES (
            SEQ_FEE_DETAIL.NEXTVAL, 990001, 'S-CHK-001',
            1, 1, 1, '退宿', '否', SYSDATE
        );
    EXCEPTION
        WHEN DUP_VAL_ON_INDEX THEN v_caught := 1;
    END;
    Assert(v_caught = 1, 'T9 月度已存在时直插退宿被 UK 拒绝 (ORA-00001)');
    SELECT COUNT(*) INTO v_monthly FROM D_Fee_Detail
        WHERE Student_ID = 'S-CHK-001' AND Bill_Type = '月度' AND Fee_ID = 990001;
    Assert(v_monthly = 1, 'T9 原月度明细不受影响 (got ' || v_monthly || ')');
    IF v_ok = 0 THEN :g_fail := 1; END IF;
END;
/

-- ============ 清理（带异常兜底，必定执行；成功即提交） ============
BEGIN
    DELETE FROM D_Fee_Detail WHERE Student_ID = 'S-CHK-001';
    DELETE FROM D_Utility_Fee WHERE Fee_ID IN (990001, 990002, 990003);
    DELETE FROM D_Bed_Allocation WHERE Allocation_ID = 990001;
    DELETE FROM D_Student WHERE Student_ID = 'S-CHK-001';
    DELETE FROM D_Room WHERE Room_ID = 9901;
    COMMIT;
    DBMS_OUTPUT.PUT_LINE('CLEANUP OK');
EXCEPTION
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('CLEANUP FAILED: ' || SQLERRM);
        ROLLBACK;
        :g_fail := 1;
END;
/

-- 退出码 = 失败断言数标记（0=全过，1=有失败），供 CI/人工判定
EXIT :g_fail
