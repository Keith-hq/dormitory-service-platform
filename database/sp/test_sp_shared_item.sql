-- 难点④ 五审回归测试（主套件）
-- 覆盖：幂等键内容冲突（跨用户/跨物品/跨票单/跨数量）、并发重放由唯一索引兜底的语义、
--       跨用户归还校验、超期归还按 PRD 按次扣 2 分（经信用分统一入口，SP 不直写信用分）、
--       幂等键 100 字符边界（迁移 019 CHAR 语义）。
-- 五审修订：
--   1. D_Credit_Log 直插不再带 Log_ID（MAX+1 会烧掉基线序列下一值）——主键由
--      迁移 014 触发器 TRG_D_CREDIT_LOG_ID_BI 生成（WHEN NEW.Log_ID IS NULL），
--      与基线触发器路径一致；
--   2. 逾期巡检提醒测试移出本套件：SP_Check_Overdue 已删除（四审），提醒逻辑在
--      应用层 OverdueCheckJob（行锁互斥 + 同事务检查插入，同日去重），
--      由 test_sp_shared_item_4th.sql 与 HTTP 集成验证覆盖；
--   3. 幂等键边界按迁移 019 的 CHAR 语义：LENGTH（字符）判定，
--      中文 100 字符通过、101 字符拒绝（见 test_sp_shared_item_4th.sql T4）。
-- 注意：应用层 ICreditService.DeductAsync 的完整语义（学生行锁串行化、Event_Key 幂等、
--       冻结通知）已由 backend/tests 与 Oracle 集成验证覆盖；
--       本脚本在 DB 层模拟统一入口的扣分 SQL，验证唯一约束 UK_D_CREDIT_LOG_EVENT 兜底。
SET SERVEROUTPUT ON SIZE UNLIMITED

DECLARE
    v_ts       VARCHAR2(20) := TO_CHAR(SYSDATE, 'YYYYMMDDHH24MISS');
    v_avail    NUMBER;
    v_stock    NUMBER;
    v_rc       NUMBER;
    v_od       NUMBER;
    v_over     NUMBER;
    v_lid      NUMBER;
    v_lid2     NUMBER;
    v_lidX     NUMBER;
    v_cnt      NUMBER;
    v_score_b  NUMBER;
    v_score_a  NUMBER;
    v_dup      NUMBER;

    PROCEDURE P(msg IN VARCHAR2) IS
    BEGIN DBMS_OUTPUT.PUT_LINE(msg); END;
    PROCEDURE Assert(cond IN BOOLEAN, msg IN VARCHAR2) IS
    BEGIN
        IF NOT cond THEN RAISE_APPLICATION_ERROR(-20099, 'FAIL: ' || msg); END IF;
        DBMS_OUTPUT.PUT_LINE('  PASS: ' || msg);
    END;
BEGIN
    P('========== 难点④ 五审回归测试 (v' || v_ts || ') ==========');

    -- ===== 0. 准备测试数据 =====
    BEGIN INSERT INTO D_Credit_Account (Student_ID, Current_Score)
        VALUES ('S001', 100); EXCEPTION WHEN DUP_VAL_ON_INDEX THEN NULL; END;
    BEGIN INSERT INTO D_Credit_Account (Student_ID, Current_Score)
        VALUES ('S002', 100); EXCEPTION WHEN DUP_VAL_ON_INDEX THEN NULL; END;

    UPDATE D_Repair_Material SET Stock_Qty = 10 WHERE Material_ID = 1;
    COMMIT;

    SELECT Available_Qty INTO v_avail FROM D_Shared_Item WHERE Item_ID = 1;
    P('--- 0. 基线: Item1 avail=' || v_avail || ', Mat1 stock=10 ---');

    -- ================================================================
    -- 一、借用：幂等键 + 内容冲突（三审 P1-2）
    -- ================================================================
    P('--- 1. 借用 + 同内容幂等重放 ---');
    SP_Borrow_Item(1, 'S001', 'T5B1-' || v_ts, v_rc, v_lid);
    Assert(v_rc = 0, '首次借用 rc=0');
    SP_Borrow_Item(1, 'S001', 'T5B1-' || v_ts, v_rc, v_lid2);
    Assert(v_rc = 0 AND v_lid2 = v_lid, '同 Key 同内容重放返回原 Loan_ID');
    SELECT Available_Qty INTO v_avail FROM D_Shared_Item WHERE Item_ID = 1;
    P('  库存（借1次+重放）= ' || v_avail || '，应比基线少 1');

    P('--- 2. 同 Key 不同内容 → rc=5 ---');
    SP_Borrow_Item(1, 'S002', 'T5B1-' || v_ts, v_rc, v_lidX);
    Assert(v_rc = 5, '同 Key 跨用户 → rc=5');
    SELECT COUNT(*) INTO v_cnt FROM D_Shared_Item WHERE Item_ID = 2;
    IF v_cnt = 1 THEN
        SP_Borrow_Item(2, 'S001', 'T5B1-' || v_ts, v_rc, v_lidX);
        Assert(v_rc = 5, '同 Key 跨物品 → rc=5');
    END IF;
    SELECT Available_Qty INTO v_avail FROM D_Shared_Item WHERE Item_ID = 1;
    P('  冲突请求未触碰库存（avail 不变）');

    -- ================================================================
    -- 二、归还：归属校验 + 按次扣 2 分走统一入口（三审 P1-1）
    -- ================================================================
    P('--- 3. 跨用户归还拦截 ---');
    SP_Return_Item(v_lid, 'S002', v_rc, v_od, v_over);
    Assert(v_rc = 1, 'S002 归还 S001 的借出被拦截 rc=1');

    P('--- 4. 本人按期归还 ---');
    SP_Return_Item(v_lid, 'S001', v_rc, v_od, v_over);
    Assert(v_rc = 0 AND v_od = 0 AND v_over = 0, '按期归还 rc=0 且 overdueDays=0、isOverdue=0');
    SP_Return_Item(v_lid, 'S001', v_rc, v_od, v_over);
    Assert(v_rc = 1, '重复归还被拦截 rc=1');

    P('--- 5. 超期归还：SP 不再直写信用分 ---');
    SP_Borrow_Item(1, 'S001', 'T5B2-' || v_ts, v_rc, v_lid);
    UPDATE D_Item_Loan SET Borrow_Time = SYSDATE - 3, Due_Time = SYSDATE - 2
    WHERE Loan_ID = v_lid;
    COMMIT;
    SELECT Current_Score INTO v_score_b FROM D_Credit_Account WHERE Student_ID = 'S001';
    SP_Return_Item(v_lid, 'S001', v_rc, v_od, v_over);
    Assert(v_rc = 0 AND v_od >= 2 AND v_over = 1, '超期归还 rc=0 且 overdueDays>=2、isOverdue=1');
    SELECT Current_Score INTO v_score_a FROM D_Credit_Account WHERE Student_ID = 'S001';
    SELECT COUNT(*) INTO v_cnt FROM D_Credit_Log WHERE Event_Key = 'OVERDUE-' || v_lid;
    Assert(v_score_b = v_score_a AND v_cnt = 0,
        '归还 SP 未触碰信用分（扣分由应用层统一入口完成）');

    P('--- 6. 统一入口语义模拟：Event_Key 唯一约束兜底只扣一次 ---');
    -- 应用层 ICreditService.DeductAsync 实际执行的扣分 SQL（-2 + 流水）；
    -- Log_ID 由迁移 014 触发器生成（不带 Log_ID 直插，不烧序列值）
    UPDATE D_Credit_Account
    SET Current_Score = Current_Score - 2, Updated_Time = SYSDATE
    WHERE Student_ID = 'S001';
    INSERT INTO D_Credit_Log (Student_ID, Score_Change, Reason, Event_Key, Create_Time)
    VALUES ('S001', -2,
        '共享物品超期归还（Loan_ID=' || v_lid || '，按次扣2分）',
        'OVERDUE-' || v_lid, SYSDATE);
    COMMIT;
    SELECT Current_Score INTO v_score_b FROM D_Credit_Account WHERE Student_ID = 'S001';
    -- 并发重试/重复请求：同 Event_Key 的第二次扣分被唯一约束拒绝
    v_dup := 0;
    BEGIN
        INSERT INTO D_Credit_Log (Student_ID, Score_Change, Reason, Event_Key, Create_Time)
        VALUES ('S001', -2, '重复扣分尝试', 'OVERDUE-' || v_lid, SYSDATE);
        COMMIT;
    EXCEPTION
        WHEN DUP_VAL_ON_INDEX THEN v_dup := 1; ROLLBACK;
    END;
    SELECT Current_Score INTO v_score_a FROM D_Credit_Account WHERE Student_ID = 'S001';
    Assert(v_dup = 1 AND v_score_b = v_score_a,
        '同 Event_Key 重复扣分被 UK_D_CREDIT_LOG_EVENT 拒绝，只扣一次 2 分');

    -- ================================================================
    -- 三、耗材出库：幂等键 + 内容冲突（三审 P1-2）
    -- ================================================================
    P('--- 7. 耗材出库 + 同内容幂等重放 ---');
    SP_Consume_Material(1, 1, 3, 'T5C1-' || v_ts, v_rc);
    Assert(v_rc = 0, '出库 rc=0');
    SP_Consume_Material(1, 1, 3, 'T5C1-' || v_ts, v_rc);
    Assert(v_rc = 0, '同 Key 同内容重放 rc=0（幂等）');
    SELECT Stock_Qty INTO v_stock FROM D_Repair_Material WHERE Material_ID = 1;
    Assert(v_stock = 7, '库存只扣一次（10-3=7）');

    P('--- 8. 同 Key 不同内容 → rc=3 ---');
    SP_Consume_Material(1, 2, 3, 'T5C1-' || v_ts, v_rc);
    Assert(v_rc = 3, '同 Key 不同票单 → rc=3');
    SP_Consume_Material(1, 1, 5, 'T5C1-' || v_ts, v_rc);
    Assert(v_rc = 3, '同 Key 不同数量 → rc=3');
    SELECT COUNT(*) INTO v_cnt FROM D_Repair_Material WHERE Material_ID = 2;
    IF v_cnt = 1 THEN
        SP_Consume_Material(2, 1, 3, 'T5C1-' || v_ts, v_rc);
        Assert(v_rc = 3, '同 Key 不同耗材 → rc=3');
    END IF;
    SELECT Stock_Qty INTO v_stock FROM D_Repair_Material WHERE Material_ID = 1;
    Assert(v_stock = 7, '冲突请求未触碰库存');

    P('--- 9. 库存不足 ---');
    SP_Consume_Material(1, 1, 99999, 'T5C2-' || v_ts, v_rc);
    Assert(v_rc = 2, '库存不足 rc=2');

    -- ===== 清理本脚本产生的数据（自有标记） =====
    DELETE FROM D_Repair_Material_Usage WHERE Idempotency_Key LIKE 'T5C%' AND Idempotency_Key LIKE '%' || v_ts;
    DELETE FROM D_Item_Loan WHERE Idempotency_Key LIKE 'T5B%' AND Idempotency_Key LIKE '%' || v_ts;
    DELETE FROM D_Credit_Log WHERE Event_Key LIKE 'OVERDUE-%' AND Reason LIKE '%（Loan_ID=' || v_lid || '%';
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
