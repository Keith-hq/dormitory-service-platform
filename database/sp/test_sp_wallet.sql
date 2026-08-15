-- 难点② 钱包充值/人工缴费测试（SP_Recharge v1.0 / SP_Manual_Pay v1.0）
-- 覆盖缴费/钱包端点落地验收（IT-C3 链路的库层基础）：
--   T1  充值成功：余额 1000→1100，生成 1 条充值流水                    （3 断言）
--   T2  充值同 Key 重放：rc=0，余额与流水数不变（幂等重放先于业务校验） （3 断言）
--   T3  金额非法：负数与 NULL 均 rc=1，不产生流水                     （3 断言）
--   T4  充值自动开户：无钱包行的学生首充建行，Before=0/After=50       （3 断言）
--   T5  缴费成功：余额 1100→950，明细标记'是'，流水挂 Detail_ID       （4 断言）
--   T6  缴费同 Key 重放：rc=0，余额与流水数不变                       （3 断言）
--   T7  明细不存在：rc=1                                              （1 断言）
--   T8  明细非本人：rc=2                                              （1 断言）
--   T9  余额不足：rc=3，不扣款、无流水、明细状态不变                  （4 断言）
--   T10 已缴换新 Key：rc=4                                            （1 断言）
--   T11 0 元明细：rc=4（CK Amount>0 不允许 0 金额流水）               （1 断言）
--   T12 跨类型 Key 冲突：充值用缴费 Key → rc=2；缴费用充值 Key → rc=5 （2 断言）
--   T13 幂等兜底模拟：预插同 Key 流水后缴费 → rc=0 且余额不变         （3 断言）
--   T14 流水 CK 直插验证：Amount=0 抛 ORA-02290                       （1 断言）
--   T15 竞态修复验证（sp_billing v1.2）：人工缴费后跑 SP_Auto_Deduct，
--       已缴明细不再扣款、无 AUTO 流水、余额不变；未缴且余额不足的明细
--       只记'余额不足'尝试                                            （5 断言）
-- 合计 38 条断言。
--
-- 前置条件：SP_Recharge / SP_Manual_Pay（sp_wallet.sql）、SP_Auto_Deduct
--   （sp_billing.sql v1.2）、SEQ_WALLET_LOG / SEQ_FEE_DETAIL / SEQ_FEE_DED_ATT
--   均已部署；迁移 023（D_Utility_Fee 序列）与本脚本无关（脚本显式赋 Fee_ID）。
--
-- T15 说明：自动扣款按整月遍历。为避免影响库中同月的真实数据，T15 先做
--   前置守卫——2026-06 存在本测试以外的已发布未缴明细时跳过 T15 并打印 SKIP
--   （不算失败）。顺带说明：T15 顺序执行只能验证"人工缴费提交在前"的终态
--   一致性（游标过滤 Is_Paid='否'）；v1.2 新增的 UPDATE 内 Is_Paid 子查询
--   复查是交错并发窗口的兜底（AUTO 与人工 Key 不同不撞 UK），需要双会话
--   并发场景才能精确复现，两重防线保证的是同一个用户可见终态：不重复扣款。
--
-- 失败不中止：断言 FAIL 只置 :g_fail=1，会话不会中途中断，结尾清理段必定
--   执行（清理段自身也带异常兜底），无 S-PAY 残留。退出码 = :g_fail。
--
-- 测试数据全部使用 99xxxx ID 段与 S-PAY 学号段，与真实数据隔离，结尾自清理。
SET SERVEROUTPUT ON SIZE UNLIMITED

VARIABLE g_fail NUMBER
BEGIN :g_fail := 0; END;
/

-- ============ 0. 环境准备（清历史 + 建测试数据） ============
BEGIN
    -- 清历史残留（按 FK 顺序）
    DELETE FROM D_Fee_Deduction_Attempt
        WHERE Detail_ID IN (SELECT Detail_ID FROM D_Fee_Detail
                             WHERE Fee_ID IN (991001, 991002)
                                OR Student_ID IN ('S-PAY-001', 'S-PAY-002'));
    DELETE FROM D_Wallet_Log WHERE Student_ID IN ('S-PAY-001', 'S-PAY-002');
    DELETE FROM D_Fee_Detail
        WHERE Fee_ID IN (991001, 991002) OR Student_ID IN ('S-PAY-001', 'S-PAY-002');
    DELETE FROM D_Utility_Fee WHERE Fee_ID IN (991001, 991002);
    DELETE FROM D_Wallet_Account WHERE Student_ID IN ('S-PAY-001', 'S-PAY-002');
    DELETE FROM D_Student WHERE Student_ID IN ('S-PAY-001', 'S-PAY-002');
    DELETE FROM D_Room WHERE Room_ID = 9910;
    COMMIT;

    -- 建测试数据：房间 9910，两笔费用（991001 有金额 2026-06 / 991002 零金额 2026-07），
    -- S-PAY-001 钱包 1000；S-PAY-002 无钱包行（T4 测开户）
    INSERT INTO D_Room (Room_ID, Room_Number, Capacity, Occupancy, Power_Status)
        VALUES (9910, 'TEST-PAY', 4, 1, '正常');
    INSERT INTO D_Student (Student_ID, Name) VALUES ('S-PAY-001', '测试缴费生1');
    INSERT INTO D_Student (Student_ID, Name) VALUES ('S-PAY-002', '测试缴费生2');
    INSERT INTO D_Wallet_Account (Student_ID, Balance) VALUES ('S-PAY-001', 1000);
    INSERT INTO D_Utility_Fee (Fee_ID, Room_ID, Year_Month, Water_Fee, Power_Fee, Is_Paid, Publish_Status)
        VALUES (991001, 9910, '2026-06', 100, 50, '否', '已发布');
    INSERT INTO D_Utility_Fee (Fee_ID, Room_ID, Year_Month, Water_Fee, Power_Fee, Is_Paid, Publish_Status)
        VALUES (991002, 9910, '2026-07', 0, 0, '否', '已发布');
    -- 明细：d1=(991001,S-PAY-001,应缴150) d2=(991001,S-PAY-002,应缴150) d3=(991002,S-PAY-001,0/0)
    INSERT INTO D_Fee_Detail (Detail_ID, Fee_ID, Student_ID, Room_ID,
                              Water_Share, Power_Share, Stay_Days, Total_Days,
                              Bill_Type, Is_Paid, Create_Time)
        VALUES (SEQ_FEE_DETAIL.NEXTVAL, 991001, 'S-PAY-001', 9910, 100, 50, 30, 30, '月度', '否', SYSDATE);
    INSERT INTO D_Fee_Detail (Detail_ID, Fee_ID, Student_ID, Room_ID,
                              Water_Share, Power_Share, Stay_Days, Total_Days,
                              Bill_Type, Is_Paid, Create_Time)
        VALUES (SEQ_FEE_DETAIL.NEXTVAL, 991001, 'S-PAY-002', 9910, 100, 50, 30, 30, '月度', '否', SYSDATE);
    INSERT INTO D_Fee_Detail (Detail_ID, Fee_ID, Student_ID, Room_ID,
                              Water_Share, Power_Share, Stay_Days, Total_Days,
                              Bill_Type, Is_Paid, Create_Time)
        VALUES (SEQ_FEE_DETAIL.NEXTVAL, 991002, 'S-PAY-001', 9910, 0, 0, 30, 30, '月度', '否', SYSDATE);
    COMMIT;
    DBMS_OUTPUT.PUT_LINE('SETUP OK');
EXCEPTION
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('SETUP FAILED: ' || SQLERRM);
        :g_fail := 1;
END;
/

-- ============ T1 充值成功 ============
DECLARE
    v_rc      NUMBER;
    v_balance NUMBER;
    v_cnt     NUMBER;
    v_ok      NUMBER := 1;
    PROCEDURE Assert(cond IN BOOLEAN, msg IN VARCHAR2) IS
    BEGIN
        IF cond THEN DBMS_OUTPUT.PUT_LINE('  PASS: ' || msg);
        ELSE v_ok := 0; DBMS_OUTPUT.PUT_LINE('  FAIL: ' || msg);
        END IF;
    END;
BEGIN
    SP_Recharge('S-PAY-001', 100, 'SPW-RC-T1', v_rc);
    Assert(v_rc = 0, 'T1 充值返回 rc=0 (got ' || v_rc || ')');
    SELECT Balance INTO v_balance FROM D_Wallet_Account WHERE Student_ID = 'S-PAY-001';
    Assert(v_balance = 1100, 'T1 余额 1000→1100 (got ' || v_balance || ')');
    SELECT COUNT(*) INTO v_cnt FROM D_Wallet_Log WHERE Idempotency_Key = 'SPW-RC-T1';
    Assert(v_cnt = 1, 'T1 生成 1 条充值流水 (got ' || v_cnt || ')');
    IF v_ok = 0 THEN :g_fail := 1; END IF;
END;
/

-- ============ T2 充值同 Key 重放 ============
DECLARE
    v_rc      NUMBER;
    v_balance NUMBER;
    v_cnt     NUMBER;
    v_ok      NUMBER := 1;
    PROCEDURE Assert(cond IN BOOLEAN, msg IN VARCHAR2) IS
    BEGIN
        IF cond THEN DBMS_OUTPUT.PUT_LINE('  PASS: ' || msg);
        ELSE v_ok := 0; DBMS_OUTPUT.PUT_LINE('  FAIL: ' || msg);
        END IF;
    END;
BEGIN
    SP_Recharge('S-PAY-001', 100, 'SPW-RC-T1', v_rc);
    Assert(v_rc = 0, 'T2 同 Key 重放返回 rc=0 (got ' || v_rc || ')');
    SELECT Balance INTO v_balance FROM D_Wallet_Account WHERE Student_ID = 'S-PAY-001';
    Assert(v_balance = 1100, 'T2 重放后余额不变 (got ' || v_balance || ')');
    SELECT COUNT(*) INTO v_cnt FROM D_Wallet_Log WHERE Idempotency_Key = 'SPW-RC-T1';
    Assert(v_cnt = 1, 'T2 重放后流水数不变 (got ' || v_cnt || ')');
    IF v_ok = 0 THEN :g_fail := 1; END IF;
END;
/

-- ============ T3 金额非法（负数 / NULL） ============
DECLARE
    v_rc      NUMBER;
    v_cnt     NUMBER;
    v_ok      NUMBER := 1;
    PROCEDURE Assert(cond IN BOOLEAN, msg IN VARCHAR2) IS
    BEGIN
        IF cond THEN DBMS_OUTPUT.PUT_LINE('  PASS: ' || msg);
        ELSE v_ok := 0; DBMS_OUTPUT.PUT_LINE('  FAIL: ' || msg);
        END IF;
    END;
BEGIN
    SP_Recharge('S-PAY-001', -100, 'SPW-RC-T3N', v_rc);
    Assert(v_rc = 1, 'T3 负数金额返回 rc=1 (got ' || v_rc || ')');
    SP_Recharge('S-PAY-001', NULL, 'SPW-RC-T3X', v_rc);
    Assert(v_rc = 1, 'T3 NULL 金额返回 rc=1 (got ' || v_rc || ')');
    SELECT COUNT(*) INTO v_cnt FROM D_Wallet_Log WHERE Idempotency_Key LIKE 'SPW-RC-T3%';
    Assert(v_cnt = 0, 'T3 非法金额不产生流水 (got ' || v_cnt || ')');
    IF v_ok = 0 THEN :g_fail := 1; END IF;
END;
/

-- ============ T4 充值自动开户 ============
DECLARE
    v_rc      NUMBER;
    v_balance NUMBER;
    v_before  NUMBER;
    v_after   NUMBER;
    v_ok      NUMBER := 1;
    PROCEDURE Assert(cond IN BOOLEAN, msg IN VARCHAR2) IS
    BEGIN
        IF cond THEN DBMS_OUTPUT.PUT_LINE('  PASS: ' || msg);
        ELSE v_ok := 0; DBMS_OUTPUT.PUT_LINE('  FAIL: ' || msg);
        END IF;
    END;
BEGIN
    SP_Recharge('S-PAY-002', 50, 'SPW-RC-T4', v_rc);
    Assert(v_rc = 0, 'T4 无钱包行学生充值返回 rc=0 (got ' || v_rc || ')');
    SELECT Balance INTO v_balance FROM D_Wallet_Account WHERE Student_ID = 'S-PAY-002';
    Assert(v_balance = 50, 'T4 自动开户后余额=50 (got ' || v_balance || ')');
    SELECT Before_Balance, After_Balance INTO v_before, v_after
        FROM D_Wallet_Log WHERE Idempotency_Key = 'SPW-RC-T4';
    Assert(v_before = 0 AND v_after = 50, 'T4 流水 Before=0/After=50 (got ' || v_before || '/' || v_after || ')');
    IF v_ok = 0 THEN :g_fail := 1; END IF;
END;
/

-- ============ T5 缴费成功 ============
DECLARE
    v_rc      NUMBER;
    v_balance NUMBER;
    v_d1      NUMBER;
    v_cnt     NUMBER;
    v_ispaid  VARCHAR2(10);
    v_ok      NUMBER := 1;
    PROCEDURE Assert(cond IN BOOLEAN, msg IN VARCHAR2) IS
    BEGIN
        IF cond THEN DBMS_OUTPUT.PUT_LINE('  PASS: ' || msg);
        ELSE v_ok := 0; DBMS_OUTPUT.PUT_LINE('  FAIL: ' || msg);
        END IF;
    END;
BEGIN
    SELECT Detail_ID INTO v_d1 FROM D_Fee_Detail WHERE Fee_ID = 991001 AND Student_ID = 'S-PAY-001';
    SP_Manual_Pay(v_d1, 'S-PAY-001', 'SPW-PAY-T5', v_rc);
    Assert(v_rc = 0, 'T5 缴费返回 rc=0 (got ' || v_rc || ')');
    SELECT Balance INTO v_balance FROM D_Wallet_Account WHERE Student_ID = 'S-PAY-001';
    Assert(v_balance = 950, 'T5 余额 1100→950 (got ' || v_balance || ')');
    SELECT Is_Paid INTO v_ispaid FROM D_Fee_Detail WHERE Detail_ID = v_d1;
    Assert(v_ispaid = '是', 'T5 明细标记已缴 (got ' || v_ispaid || ')');
    SELECT COUNT(*) INTO v_cnt FROM D_Wallet_Log
        WHERE Idempotency_Key = 'SPW-PAY-T5' AND Detail_ID = v_d1;
    Assert(v_cnt = 1, 'T5 缴费流水挂明细 d1 (got ' || v_cnt || ')');
    IF v_ok = 0 THEN :g_fail := 1; END IF;
END;
/

-- ============ T6 缴费同 Key 重放 ============
DECLARE
    v_rc      NUMBER;
    v_balance NUMBER;
    v_d1      NUMBER;
    v_cnt     NUMBER;
    v_ok      NUMBER := 1;
    PROCEDURE Assert(cond IN BOOLEAN, msg IN VARCHAR2) IS
    BEGIN
        IF cond THEN DBMS_OUTPUT.PUT_LINE('  PASS: ' || msg);
        ELSE v_ok := 0; DBMS_OUTPUT.PUT_LINE('  FAIL: ' || msg);
        END IF;
    END;
BEGIN
    SELECT Detail_ID INTO v_d1 FROM D_Fee_Detail WHERE Fee_ID = 991001 AND Student_ID = 'S-PAY-001';
    SP_Manual_Pay(v_d1, 'S-PAY-001', 'SPW-PAY-T5', v_rc);
    Assert(v_rc = 0, 'T6 同 Key 重放返回 rc=0（先于已缴检查，不误报 rc=4）(got ' || v_rc || ')');
    SELECT Balance INTO v_balance FROM D_Wallet_Account WHERE Student_ID = 'S-PAY-001';
    Assert(v_balance = 950, 'T6 重放后余额不变 (got ' || v_balance || ')');
    SELECT COUNT(*) INTO v_cnt FROM D_Wallet_Log WHERE Idempotency_Key = 'SPW-PAY-T5';
    Assert(v_cnt = 1, 'T6 重放后流水数不变 (got ' || v_cnt || ')');
    IF v_ok = 0 THEN :g_fail := 1; END IF;
END;
/

-- ============ T7 明细不存在 ============
DECLARE
    v_rc NUMBER;
    v_ok NUMBER := 1;
    PROCEDURE Assert(cond IN BOOLEAN, msg IN VARCHAR2) IS
    BEGIN
        IF cond THEN DBMS_OUTPUT.PUT_LINE('  PASS: ' || msg);
        ELSE v_ok := 0; DBMS_OUTPUT.PUT_LINE('  FAIL: ' || msg);
        END IF;
    END;
BEGIN
    SP_Manual_Pay(999999, 'S-PAY-001', 'SPW-PAY-T7', v_rc);
    Assert(v_rc = 1, 'T7 明细不存在返回 rc=1 (got ' || v_rc || ')');
    IF v_ok = 0 THEN :g_fail := 1; END IF;
END;
/

-- ============ T8 明细非本人 ============
DECLARE
    v_rc NUMBER;
    v_d2 NUMBER;
    v_ok NUMBER := 1;
    PROCEDURE Assert(cond IN BOOLEAN, msg IN VARCHAR2) IS
    BEGIN
        IF cond THEN DBMS_OUTPUT.PUT_LINE('  PASS: ' || msg);
        ELSE v_ok := 0; DBMS_OUTPUT.PUT_LINE('  FAIL: ' || msg);
        END IF;
    END;
BEGIN
    SELECT Detail_ID INTO v_d2 FROM D_Fee_Detail WHERE Fee_ID = 991001 AND Student_ID = 'S-PAY-002';
    SP_Manual_Pay(v_d2, 'S-PAY-001', 'SPW-PAY-T8', v_rc);
    Assert(v_rc = 2, 'T8 他人明细返回 rc=2 (got ' || v_rc || ')');
    IF v_ok = 0 THEN :g_fail := 1; END IF;
END;
/

-- ============ T9 余额不足 ============
DECLARE
    v_rc      NUMBER;
    v_balance NUMBER;
    v_d2      NUMBER;
    v_cnt     NUMBER;
    v_ispaid  VARCHAR2(10);
    v_ok      NUMBER := 1;
    PROCEDURE Assert(cond IN BOOLEAN, msg IN VARCHAR2) IS
    BEGIN
        IF cond THEN DBMS_OUTPUT.PUT_LINE('  PASS: ' || msg);
        ELSE v_ok := 0; DBMS_OUTPUT.PUT_LINE('  FAIL: ' || msg);
        END IF;
    END;
BEGIN
    SELECT Detail_ID INTO v_d2 FROM D_Fee_Detail WHERE Fee_ID = 991001 AND Student_ID = 'S-PAY-002';
    SP_Manual_Pay(v_d2, 'S-PAY-002', 'SPW-PAY-T9', v_rc);
    Assert(v_rc = 3, 'T9 余额不足返回 rc=3 (got ' || v_rc || ')');
    SELECT Balance INTO v_balance FROM D_Wallet_Account WHERE Student_ID = 'S-PAY-002';
    Assert(v_balance = 50, 'T9 不扣款余额仍为 50 (got ' || v_balance || ')');
    SELECT COUNT(*) INTO v_cnt FROM D_Wallet_Log WHERE Idempotency_Key = 'SPW-PAY-T9';
    Assert(v_cnt = 0, 'T9 失败不产生流水 (got ' || v_cnt || ')');
    SELECT Is_Paid INTO v_ispaid FROM D_Fee_Detail WHERE Detail_ID = v_d2;
    Assert(v_ispaid = '否', 'T9 明细状态不变 (got ' || v_ispaid || ')');
    IF v_ok = 0 THEN :g_fail := 1; END IF;
END;
/

-- ============ T10 已缴换新 Key ============
DECLARE
    v_rc NUMBER;
    v_d1 NUMBER;
    v_ok NUMBER := 1;
    PROCEDURE Assert(cond IN BOOLEAN, msg IN VARCHAR2) IS
    BEGIN
        IF cond THEN DBMS_OUTPUT.PUT_LINE('  PASS: ' || msg);
        ELSE v_ok := 0; DBMS_OUTPUT.PUT_LINE('  FAIL: ' || msg);
        END IF;
    END;
BEGIN
    SELECT Detail_ID INTO v_d1 FROM D_Fee_Detail WHERE Fee_ID = 991001 AND Student_ID = 'S-PAY-001';
    SP_Manual_Pay(v_d1, 'S-PAY-001', 'SPW-PAY-T10', v_rc);
    Assert(v_rc = 4, 'T10 已缴明细换新 Key 返回 rc=4 (got ' || v_rc || ')');
    IF v_ok = 0 THEN :g_fail := 1; END IF;
END;
/

-- ============ T11 0 元明细 ============
DECLARE
    v_rc NUMBER;
    v_d3 NUMBER;
    v_ok NUMBER := 1;
    PROCEDURE Assert(cond IN BOOLEAN, msg IN VARCHAR2) IS
    BEGIN
        IF cond THEN DBMS_OUTPUT.PUT_LINE('  PASS: ' || msg);
        ELSE v_ok := 0; DBMS_OUTPUT.PUT_LINE('  FAIL: ' || msg);
        END IF;
    END;
BEGIN
    SELECT Detail_ID INTO v_d3 FROM D_Fee_Detail WHERE Fee_ID = 991002 AND Student_ID = 'S-PAY-001';
    SP_Manual_Pay(v_d3, 'S-PAY-001', 'SPW-PAY-T11', v_rc);
    Assert(v_rc = 4, 'T11 0 元明细返回 rc=4（无需缴）(got ' || v_rc || ')');
    IF v_ok = 0 THEN :g_fail := 1; END IF;
END;
/

-- ============ T12 跨类型 Key 冲突 ============
DECLARE
    v_rc NUMBER;
    v_d2 NUMBER;
    v_ok NUMBER := 1;
    PROCEDURE Assert(cond IN BOOLEAN, msg IN VARCHAR2) IS
    BEGIN
        IF cond THEN DBMS_OUTPUT.PUT_LINE('  PASS: ' || msg);
        ELSE v_ok := 0; DBMS_OUTPUT.PUT_LINE('  FAIL: ' || msg);
        END IF;
    END;
BEGIN
    SP_Recharge('S-PAY-001', 10, 'SPW-PAY-T5', v_rc);
    Assert(v_rc = 2, 'T12 充值使用缴费 Key 返回 rc=2 (got ' || v_rc || ')');
    SELECT Detail_ID INTO v_d2 FROM D_Fee_Detail WHERE Fee_ID = 991001 AND Student_ID = 'S-PAY-002';
    SP_Manual_Pay(v_d2, 'S-PAY-002', 'SPW-RC-T1', v_rc);
    Assert(v_rc = 5, 'T12 缴费使用充值 Key 返回 rc=5 (got ' || v_rc || ')');
    IF v_ok = 0 THEN :g_fail := 1; END IF;
END;
/

-- ============ T13 幂等兜底模拟：预插同 Key 流水后缴费 ============
-- 模拟并发双提交的后提交会话视角：同 Key 流水已存在（对方先行提交），
-- 本会话 SP 调用应直接 rc=0，且余额不变、流水数不变。
DECLARE
    v_rc      NUMBER;
    v_balance NUMBER;
    v_d1      NUMBER;
    v_cnt     NUMBER;
    v_ok      NUMBER := 1;
    PROCEDURE Assert(cond IN BOOLEAN, msg IN VARCHAR2) IS
    BEGIN
        IF cond THEN DBMS_OUTPUT.PUT_LINE('  PASS: ' || msg);
        ELSE v_ok := 0; DBMS_OUTPUT.PUT_LINE('  FAIL: ' || msg);
        END IF;
    END;
BEGIN
    INSERT INTO D_Wallet_Log (Log_ID, Student_ID, Amount, Transaction_Type,
                              Before_Balance, After_Balance, Detail_ID,
                              Idempotency_Key, Create_Time)
        VALUES (SEQ_WALLET_LOG.NEXTVAL, 'S-PAY-001', 1, '人工缴费', 950, 950, NULL, 'SPW-PAY-T13', SYSDATE);
    COMMIT;
    SELECT Detail_ID INTO v_d1 FROM D_Fee_Detail WHERE Fee_ID = 991001 AND Student_ID = 'S-PAY-001';
    SP_Manual_Pay(v_d1, 'S-PAY-001', 'SPW-PAY-T13', v_rc);
    Assert(v_rc = 0, 'T13 同 Key 流水已存在时缴费返回 rc=0 (got ' || v_rc || ')');
    SELECT Balance INTO v_balance FROM D_Wallet_Account WHERE Student_ID = 'S-PAY-001';
    Assert(v_balance = 950, 'T13 余额不变 (got ' || v_balance || ')');
    SELECT COUNT(*) INTO v_cnt FROM D_Wallet_Log WHERE Idempotency_Key = 'SPW-PAY-T13';
    Assert(v_cnt = 1, 'T13 流水数不变 (got ' || v_cnt || ')');
    IF v_ok = 0 THEN :g_fail := 1; END IF;
END;
/

-- ============ T14 流水 CK 直插验证：Amount=0 被拒绝 ============
DECLARE
    v_code NUMBER;
    v_ok   NUMBER := 1;
    PROCEDURE Assert(cond IN BOOLEAN, msg IN VARCHAR2) IS
    BEGIN
        IF cond THEN DBMS_OUTPUT.PUT_LINE('  PASS: ' || msg);
        ELSE v_ok := 0; DBMS_OUTPUT.PUT_LINE('  FAIL: ' || msg);
        END IF;
    END;
BEGIN
    BEGIN
        INSERT INTO D_Wallet_Log (Log_ID, Student_ID, Amount, Transaction_Type,
                                  Before_Balance, After_Balance, Idempotency_Key)
            VALUES (SEQ_WALLET_LOG.NEXTVAL, 'S-PAY-001', 0, '充值', 950, 950, 'SPW-CK-T14');
    EXCEPTION
        WHEN OTHERS THEN v_code := SQLCODE;
    END;
    Assert(v_code = -2290, 'T14 Amount=0 直插被 CK 拒绝 (got ' || v_code || ')');
    IF v_ok = 0 THEN :g_fail := 1; END IF;
END;
/

-- ============ T15 竞态修复验证：人工缴费后自动扣款不再重复扣 ============
-- 前置守卫：2026-06 除本测试 Fee_ID=991001 外无其它"已发布未缴且金额>0"明细，
-- 否则跳过 T15（避免自动扣款影响库中真实数据）。若库中该月恰好有真实数据，
-- 本测试仍可安全运行（SKIP 不算失败）。
DECLARE
    v_other      NUMBER;
    v_rc_guard   NUMBER;
    v_balance1   NUMBER;
    v_balance2   NUMBER;
    v_d1         NUMBER;
    v_d2         NUMBER;
    v_auto_cnt   NUMBER;
    v_att_cnt    NUMBER;
    v_ispaid     VARCHAR2(10);
    v_ok         NUMBER := 1;
    PROCEDURE Assert(cond IN BOOLEAN, msg IN VARCHAR2) IS
    BEGIN
        IF cond THEN DBMS_OUTPUT.PUT_LINE('  PASS: ' || msg);
        ELSE v_ok := 0; DBMS_OUTPUT.PUT_LINE('  FAIL: ' || msg);
        END IF;
    END;
BEGIN
    SELECT COUNT(*) INTO v_other
      FROM D_Fee_Detail fd
      JOIN D_Utility_Fee uf ON fd.Fee_ID = uf.Fee_ID
     WHERE uf.Year_Month = '2026-06'
       AND uf.Publish_Status = '已发布'
       AND fd.Is_Paid = '否'
       AND (fd.Water_Share + fd.Power_Share) > 0
       AND fd.Fee_ID <> 991001;

    IF v_other > 0 THEN
        DBMS_OUTPUT.PUT_LINE('  SKIP: T15 前置守卫未通过（2026-06 存在 ' || v_other ||
                             ' 条本测试以外的未缴明细，自动扣款会波及真实数据）');
    ELSE
        SP_Auto_Deduct(1, '2026-06');

        SELECT Detail_ID INTO v_d1 FROM D_Fee_Detail WHERE Fee_ID = 991001 AND Student_ID = 'S-PAY-001';
        SELECT Detail_ID INTO v_d2 FROM D_Fee_Detail WHERE Fee_ID = 991001 AND Student_ID = 'S-PAY-002';

        SELECT Balance INTO v_balance1 FROM D_Wallet_Account WHERE Student_ID = 'S-PAY-001';
        Assert(v_balance1 = 950, 'T15 已缴明细未被再次扣款，余额仍为 950 (got ' || v_balance1 || ')');

        SELECT COUNT(*) INTO v_auto_cnt FROM D_Wallet_Log
            WHERE Detail_ID = v_d1 AND Transaction_Type = '自动扣款';
        Assert(v_auto_cnt = 0, 'T15 已缴明细无 AUTO 流水 (got ' || v_auto_cnt || ')');

        SELECT Is_Paid INTO v_ispaid FROM D_Fee_Detail WHERE Detail_ID = v_d2;
        Assert(v_ispaid = '否', 'T15 余额不足明细状态不变 (got ' || v_ispaid || ')');
        SELECT Balance INTO v_balance2 FROM D_Wallet_Account WHERE Student_ID = 'S-PAY-002';
        Assert(v_balance2 = 50, 'T15 余额不足明细不扣款，余额仍为 50 (got ' || v_balance2 || ')');

        SELECT COUNT(*) INTO v_att_cnt FROM D_Fee_Deduction_Attempt
            WHERE Detail_ID = v_d2 AND Attempt_No = 1 AND Result = '余额不足';
        Assert(v_att_cnt = 1, 'T15 余额不足明细记 1 条余额不足尝试 (got ' || v_att_cnt || ')');
    END IF;

    IF v_ok = 0 THEN :g_fail := 1; END IF;
END;
/

-- ============ 清理（带异常兜底，必定执行；成功即提交） ============
BEGIN
    DELETE FROM D_Fee_Deduction_Attempt
        WHERE Detail_ID IN (SELECT Detail_ID FROM D_Fee_Detail
                             WHERE Fee_ID IN (991001, 991002)
                                OR Student_ID IN ('S-PAY-001', 'S-PAY-002'));
    DELETE FROM D_Wallet_Log WHERE Student_ID IN ('S-PAY-001', 'S-PAY-002');
    DELETE FROM D_Fee_Detail
        WHERE Fee_ID IN (991001, 991002) OR Student_ID IN ('S-PAY-001', 'S-PAY-002');
    DELETE FROM D_Utility_Fee WHERE Fee_ID IN (991001, 991002);
    DELETE FROM D_Wallet_Account WHERE Student_ID IN ('S-PAY-001', 'S-PAY-002');
    DELETE FROM D_Student WHERE Student_ID IN ('S-PAY-001', 'S-PAY-002');
    DELETE FROM D_Room WHERE Room_ID = 9910;
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
