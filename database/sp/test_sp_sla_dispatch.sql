-- ============================================================
-- 难点⑤ SLA派单 回归测试（一审修复版）
-- 测试场景：
--   1. 基本流程：创建工单→派单(维修员)→被指派维修员接单→完工
--   2. 完工越权：Admin B 尝试完成 Admin A 的工单 → rc=2
--   3. 未接单不能完工：待处理工单直接完工 → rc=2
--   4. 幂等：重复派单→rc=2，重复接单→rc=1，重复完工→rc=2
--   5. 无维修员→rc=3（配置异常，不擅自派楼长）
--   6. 公共抢单拦截：未指派工单不能被随意 Claim → rc=1
--   7. SLA 升级一次：Escalation_Time 非空，二次巡检不重复升级
--   8. 升级不覆盖负责人：Assigned_To 保持原维修员
--   9. 边界：不存在工单的 Assign/Complete → rc=1
--   10. 完工日志 UK 冲突 → rc=3（SAVEPOINT 回滚）
--   11. 清理只删本脚本创建的数据
-- ============================================================
SET SERVEROUTPUT ON SIZE UNLIMITED

DECLARE
    v_Room_ID      NUMBER;
    v_Building_ID  NUMBER;
    v_Maint_Admin  VARCHAR2(20);
    v_Maint_Admin2 VARCHAR2(20);
    v_Mgr_Admin    VARCHAR2(20);
    v_Ts           VARCHAR2(20);
    v_Ticket1_ID   NUMBER;
    v_Ticket2_ID   NUMBER;
    v_Ticket3_ID   NUMBER;
    v_Ticket4_ID   NUMBER;
    v_Ticket5_ID   NUMBER;
    v_Rc           NUMBER;
    v_Status       VARCHAR2(20);
    v_Assigned_To  VARCHAR2(20);
    v_SLA_Level    VARCHAR2(10);
    v_Esc_Time     DATE;
    v_Esc_Count    NUMBER;
    v_Log_Count    NUMBER;
    v_Ticket_Cnt   NUMBER;
    v_Admin_Cnt    NUMBER;

    PROCEDURE P( msg IN VARCHAR2 ) IS
    BEGIN DBMS_OUTPUT.PUT_LINE(msg); END;
    PROCEDURE PL( msg IN VARCHAR2 ) IS
    BEGIN DBMS_OUTPUT.PUT_LINE('  ' || msg); END;

    -- 取一个新 Ticket_ID（本脚本专用）
    FUNCTION NewTicketId RETURN NUMBER IS
        v_Id NUMBER;
    BEGIN
        SELECT NVL(MAX(Ticket_ID), 0) + 1 INTO v_Id FROM D_Repair_Ticket;
        RETURN v_Id;
    END;
BEGIN
    P('========== 难点⑤ SLA派单 回归测试（一审修复版）==========');
    v_Ts := TO_CHAR(SYSDATE, 'MMDDHH24MI');
    PL('Timestamp: ' || v_Ts);

    -- ===== STEP 0: Setup =====
    P('--- Step 0: Setup test data ---');

    -- 找一个有效房间
    SELECT r.Room_ID, r.Building_ID
    INTO v_Room_ID, v_Building_ID
    FROM D_Room r
    WHERE ROWNUM = 1;
    PL('Room_ID=' || v_Room_ID || ', Building_ID=' || v_Building_ID);

    -- 建测试维修员 A
    v_Maint_Admin := 'T5A_' || v_Ts;
    BEGIN
        INSERT INTO D_Admin (Admin_ID, Admin_Name, Role_Level, Building_ID)
        VALUES (v_Maint_Admin, 'T5维修员A', '维修员', v_Building_ID);
        PL('Created maintenance admin A: ' || v_Maint_Admin);
    EXCEPTION WHEN DUP_VAL_ON_INDEX THEN PL('Admin A already exists'); END;

    -- 建测试维修员 B（同楼栋，用于越权测试）
    v_Maint_Admin2 := 'T5B_' || v_Ts;
    BEGIN
        INSERT INTO D_Admin (Admin_ID, Admin_Name, Role_Level, Building_ID)
        VALUES (v_Maint_Admin2, 'T5维修员B', '维修员', v_Building_ID);
        PL('Created maintenance admin B: ' || v_Maint_Admin2);
    EXCEPTION WHEN DUP_VAL_ON_INDEX THEN PL('Admin B already exists'); END;

    -- 建测试楼长
    v_Mgr_Admin := 'T5M_' || v_Ts;
    BEGIN
        INSERT INTO D_Admin (Admin_ID, Admin_Name, Role_Level, Building_ID)
        VALUES (v_Mgr_Admin, 'T5楼长', '楼长', v_Building_ID);
        PL('Created building manager: ' || v_Mgr_Admin);
    EXCEPTION WHEN DUP_VAL_ON_INDEX THEN PL('Manager already exists'); END;

    -- 建楼长对应的 D_User_Account（用于 SLA 升级通知）
    DECLARE v_Acct NUMBER;
    BEGIN
        SELECT NVL(MAX(Account_ID), 0) + 1 INTO v_Acct FROM D_User_Account;
        INSERT INTO D_User_Account (Account_ID, Login_Name, Password_Hash, Account_Status, Admin_ID)
        VALUES (v_Acct, 't5_mgr_' || v_Ts, 'TEST_HASH', '正常', v_Mgr_Admin);
        PL('Created user account for manager');
    EXCEPTION WHEN DUP_VAL_ON_INDEX THEN PL('Manager account already exists'); END;

    COMMIT;

    -- ================================================================
    -- TEST 1: 基本派单流程——维修员
    -- ================================================================
    P('--- Test 1: SP_Assign_Ticket → repairer (expect rc=0) ---');
    v_Ticket1_ID := NewTicketId;
    INSERT INTO D_Repair_Ticket (
        Ticket_ID, Student_ID, Room_ID, Issue_Desc, Submit_Time, Status, SLA_Level, Assigned_To
    ) VALUES (
        v_Ticket1_ID, 'S001', v_Room_ID, 'T1: 水龙头漏水',
        SYSDATE, '待处理', '普通', NULL
    );
    COMMIT;
    PL('Created Ticket_ID=' || v_Ticket1_ID);

    SP_Assign_Ticket(v_Ticket1_ID, v_Rc);
    PL('Assign rc=' || v_Rc || ' (expect 0)');
    IF v_Rc != 0 THEN RAISE_APPLICATION_ERROR(-20001, 'Test 1 FAIL: Assign rc=' || v_Rc); END IF;

    SELECT Assigned_To, SLA_Level, Status
    INTO v_Assigned_To, v_SLA_Level, v_Status
    FROM D_Repair_Ticket WHERE Ticket_ID = v_Ticket1_ID;
    PL('Assigned_To=' || v_Assigned_To || ', SLA=' || v_SLA_Level || ', Status=' || v_Status);
    IF v_Assigned_To != v_Maint_Admin THEN
        RAISE_APPLICATION_ERROR(-20002, 'Test 1 FAIL: Assigned to ' || v_Assigned_To || ', expected ' || v_Maint_Admin);
    END IF;
    IF v_SLA_Level != '普通' THEN
        RAISE_APPLICATION_ERROR(-20003, 'Test 1 FAIL: SLA_Level=' || v_SLA_Level);
    END IF;

    -- ================================================================
    -- TEST 2: 幂等——重复派单 → rc=2
    -- ================================================================
    P('--- Test 2: SP_Assign_Ticket again (expect rc=2, idempotent) ---');
    SP_Assign_Ticket(v_Ticket1_ID, v_Rc);
    PL('Assign again rc=' || v_Rc || ' (expect 2)');
    IF v_Rc != 2 THEN RAISE_APPLICATION_ERROR(-20004, 'Test 2 FAIL: expected rc=2'); END IF;

    -- ================================================================
    -- TEST 3: 被指派的维修员接单 → rc=0
    -- ================================================================
    P('--- Test 3: SP_Claim_Ticket by assigned repairer (expect rc=0) ---');
    SP_Claim_Ticket(v_Ticket1_ID, v_Maint_Admin, v_Rc);
    PL('Claim rc=' || v_Rc || ' (expect 0)');
    IF v_Rc != 0 THEN RAISE_APPLICATION_ERROR(-20005, 'Test 3 FAIL: Claim rc=' || v_Rc); END IF;

    SELECT Status INTO v_Status FROM D_Repair_Ticket WHERE Ticket_ID = v_Ticket1_ID;
    PL('Status=' || v_Status);
    IF v_Status != '处理中' THEN
        RAISE_APPLICATION_ERROR(-20006, 'Test 3 FAIL: Status=' || v_Status);
    END IF;

    -- ================================================================
    -- TEST 4: 重复接单 → rc=1
    -- ================================================================
    P('--- Test 4: SP_Claim_Ticket again (expect rc=1, already claimed) ---');
    SP_Claim_Ticket(v_Ticket1_ID, v_Maint_Admin, v_Rc);
    PL('Claim again rc=' || v_Rc || ' (expect 1)');
    IF v_Rc != 1 THEN RAISE_APPLICATION_ERROR(-20007, 'Test 4 FAIL: expected rc=1'); END IF;

    -- ================================================================
    -- TEST 5: 完工越权——Admin B 尝试完成 Admin A 的工单 → rc=2
    -- ================================================================
    P('--- Test 5: Complete by other admin (expect rc=2, cross-admin blocked) ---');
    SP_Complete_Repair(v_Ticket1_ID, v_Maint_Admin2, '越权完工', '没修好', NULL, v_Rc);
    PL('Cross-admin complete rc=' || v_Rc || ' (expect 2)');
    IF v_Rc != 2 THEN RAISE_APPLICATION_ERROR(-20008, 'Test 5 FAIL: cross-admin not blocked, rc=' || v_Rc); END IF;

    -- 验证状态未被修改
    SELECT Status INTO v_Status FROM D_Repair_Ticket WHERE Ticket_ID = v_Ticket1_ID;
    PL('Status still=' || v_Status || ' (expect 处理中)');
    IF v_Status != '处理中' THEN
        RAISE_APPLICATION_ERROR(-20009, 'Test 5 FAIL: Status changed to ' || v_Status);
    END IF;

    -- ================================================================
    -- TEST 6: 本人正常完工（带 result 和 solveTime）→ rc=0
    -- ================================================================
    P('--- Test 6: Complete by assigned admin (expect rc=0) ---');
    SP_Complete_Repair(v_Ticket1_ID, v_Maint_Admin, '已更换水龙头密封圈',
                       '水龙头恢复正常使用', SYSDATE, v_Rc);
    PL('Complete rc=' || v_Rc || ' (expect 0)');
    IF v_Rc != 0 THEN RAISE_APPLICATION_ERROR(-20010, 'Test 6 FAIL: Complete rc=' || v_Rc); END IF;

    SELECT Status INTO v_Status FROM D_Repair_Ticket WHERE Ticket_ID = v_Ticket1_ID;
    PL('Status=' || v_Status);
    IF v_Status != '已完成' THEN
        RAISE_APPLICATION_ERROR(-20011, 'Test 6 FAIL: Status=' || v_Status);
    END IF;

    -- 验证 Repair_Result
    DECLARE v_Result VARCHAR2(200);
    BEGIN
        SELECT Repair_Result INTO v_Result FROM D_Repair_Log WHERE Ticket_ID = v_Ticket1_ID;
        PL('Repair_Result=' || v_Result);
        IF v_Result != '水龙头恢复正常使用' THEN
            RAISE_APPLICATION_ERROR(-20012, 'Test 6 FAIL: Repair_Result mismatch');
        END IF;
    EXCEPTION WHEN NO_DATA_FOUND THEN
        RAISE_APPLICATION_ERROR(-20013, 'Test 6 FAIL: No repair log');
    END;

    -- ================================================================
    -- TEST 7: 重复完工 → rc=2（Status 已不是处理中）
    -- ================================================================
    P('--- Test 7: Complete again (expect rc=2, already done) ---');
    SP_Complete_Repair(v_Ticket1_ID, v_Maint_Admin, '再次完工', '应该失败', NULL, v_Rc);
    PL('Complete again rc=' || v_Rc || ' (expect 2)');
    IF v_Rc != 2 THEN RAISE_APPLICATION_ERROR(-20014, 'Test 7 FAIL: expected rc=2'); END IF;

    -- ================================================================
    -- TEST 8: 公共抢单被拦截——未指派工单不能被随意 Claim → rc=1
    -- ================================================================
    P('--- Test 8: Claim unassigned ticket (expect rc=1, public claim blocked) ---');
    v_Ticket2_ID := NewTicketId;
    INSERT INTO D_Repair_Ticket (
        Ticket_ID, Student_ID, Room_ID, Issue_Desc, Submit_Time, Status, SLA_Level, Assigned_To
    ) VALUES (
        v_Ticket2_ID, 'S002', v_Room_ID, 'T8: 门锁损坏',
        SYSDATE, '待处理', '普通', NULL
    );
    COMMIT;
    PL('Created unassigned Ticket_ID=' || v_Ticket2_ID);

    -- 未指派的工单，任何人 Claim 都应失败
    SP_Claim_Ticket(v_Ticket2_ID, v_Maint_Admin, v_Rc);
    PL('Claim unassigned rc=' || v_Rc || ' (expect 1, public claim blocked)');
    IF v_Rc != 1 THEN RAISE_APPLICATION_ERROR(-20015, 'Test 8 FAIL: public claim not blocked, rc=' || v_Rc); END IF;

    -- 验证状态未变
    SELECT Status INTO v_Status FROM D_Repair_Ticket WHERE Ticket_ID = v_Ticket2_ID;
    PL('Status=' || v_Status || ' (expect 待处理)');
    IF v_Status != '待处理' THEN
        RAISE_APPLICATION_ERROR(-20016, 'Test 8 FAIL: Status changed to ' || v_Status);
    END IF;

    -- ================================================================
    -- TEST 9: 未接单不能完工——待处理工单直接完工 → rc=2
    -- ================================================================
    P('--- Test 9: Complete pending ticket (expect rc=2, not in-progress) ---');
    -- 先派单给维修员A
    SP_Assign_Ticket(v_Ticket2_ID, v_Rc);
    PL('Assign rc=' || v_Rc);
    IF v_Rc != 0 THEN RAISE_APPLICATION_ERROR(-20017, 'Test 9 FAIL: Assign rc=' || v_Rc); END IF;

    -- 未接单就尝试完工 → 应失败
    SP_Complete_Repair(v_Ticket2_ID, v_Maint_Admin, '未接单完工', NULL, NULL, v_Rc);
    PL('Complete before claim rc=' || v_Rc || ' (expect 2)');
    IF v_Rc != 2 THEN RAISE_APPLICATION_ERROR(-20018, 'Test 9 FAIL: expected rc=2, got ' || v_Rc); END IF;

    -- ================================================================
    -- TEST 10: 无维修员 → rc=3（配置异常，不擅自派楼长）
    -- ================================================================
    P('--- Test 10: No repairer → rc=3 (config error, no fallback to manager) ---');
    -- 暂时删除测试楼栋的维修员
    DELETE FROM D_Repair_Log WHERE Admin_ID IN (v_Maint_Admin, v_Maint_Admin2);
    DELETE FROM D_Repair_Ticket WHERE Assigned_To IN (v_Maint_Admin, v_Maint_Admin2);
    DELETE FROM D_Admin WHERE Admin_ID IN (v_Maint_Admin, v_Maint_Admin2);
    COMMIT;

    v_Ticket3_ID := NewTicketId;
    INSERT INTO D_Repair_Ticket (
        Ticket_ID, Student_ID, Room_ID, Issue_Desc, Submit_Time, Status, SLA_Level, Assigned_To
    ) VALUES (
        v_Ticket3_ID, 'S003', v_Room_ID, 'T10: 空调故障',
        SYSDATE, '待处理', '普通', NULL
    );
    COMMIT;
    PL('Created Ticket_ID=' || v_Ticket3_ID);

    SP_Assign_Ticket(v_Ticket3_ID, v_Rc);
    PL('Assign with no repairer rc=' || v_Rc || ' (expect 3)');
    IF v_Rc != 3 THEN RAISE_APPLICATION_ERROR(-20019, 'Test 10 FAIL: expected rc=3, got ' || v_Rc); END IF;

    -- 验证工单未被擅自派给楼长
    SELECT Assigned_To INTO v_Assigned_To FROM D_Repair_Ticket WHERE Ticket_ID = v_Ticket3_ID;
    PL('Assigned_To=' || NVL(v_Assigned_To, 'NULL') || ' (expect NULL)');
    IF v_Assigned_To IS NOT NULL THEN
        RAISE_APPLICATION_ERROR(-20020, 'Test 10 FAIL: Assigned_To should be NULL');
    END IF;

    -- 恢复维修员
    INSERT INTO D_Admin (Admin_ID, Admin_Name, Role_Level, Building_ID)
    VALUES (v_Maint_Admin, 'T5维修员A', '维修员', v_Building_ID);
    INSERT INTO D_Admin (Admin_ID, Admin_Name, Role_Level, Building_ID)
    VALUES (v_Maint_Admin2, 'T5维修员B', '维修员', v_Building_ID);
    COMMIT;

    -- ================================================================
    -- TEST 11: 不存在工单 → rc=1
    -- ================================================================
    P('--- Test 11: Nonexistent ticket (expect rc=1) ---');
    SP_Assign_Ticket(99999999, v_Rc);
    PL('Assign rc=' || v_Rc || ' (expect 1)');
    IF v_Rc != 1 THEN RAISE_APPLICATION_ERROR(-20021, 'Test 11 FAIL: expected rc=1'); END IF;

    SP_Complete_Repair(99999999, 'ADM001', 'test', NULL, NULL, v_Rc);
    PL('Complete rc=' || v_Rc || ' (expect 1)');
    IF v_Rc != 1 THEN RAISE_APPLICATION_ERROR(-20022, 'Test 11 FAIL: expected rc=1'); END IF;

    -- ================================================================
    -- TEST 12: SLA 升级——只提醒不转派 + Escalation_Time 防二次升级
    -- ================================================================
    P('--- Test 12: SLA Escalation (notify only, no reassign, once only) ---');

    -- 创建过期普通工单（Deadline 在过去，未升级）
    v_Ticket4_ID := NewTicketId;
    INSERT INTO D_Repair_Ticket (
        Ticket_ID, Student_ID, Room_ID, Issue_Desc, Submit_Time, Status,
        SLA_Level, Deadline, Assigned_To, Escalation_Time
    ) VALUES (
        v_Ticket4_ID, 'S004', v_Room_ID, 'T12: 窗户破损',
        SYSDATE - 2, '待处理', '普通', SYSDATE - 1, v_Maint_Admin, NULL
    );
    COMMIT;
    PL('Created expired 普通 ticket: ' || v_Ticket4_ID
       || ' (Deadline=' || TO_CHAR(SYSDATE-1, 'YYYY-MM-DD HH24:MI') || ')');

    -- --- 12a: 首次升级 ---
    SP_Escalate_SLA;

    -- 验证 Escalation_Time 已设置
    SELECT Escalation_Time INTO v_Esc_Time
    FROM D_Repair_Ticket WHERE Ticket_ID = v_Ticket4_ID;
    PL('Escalation_Time after 1st scan: ' || TO_CHAR(v_Esc_Time, 'YYYY-MM-DD HH24:MI:SS'));
    IF v_Esc_Time IS NULL THEN
        RAISE_APPLICATION_ERROR(-20023, 'Test 12a FAIL: Escalation_Time still NULL');
    END IF;

    -- 验证 Assigned_To 未被覆盖（仍为原维修员）
    SELECT Assigned_To, SLA_Level INTO v_Assigned_To, v_SLA_Level
    FROM D_Repair_Ticket WHERE Ticket_ID = v_Ticket4_ID;
    PL('Assigned_To after escalation: ' || v_Assigned_To || ' (expect ' || v_Maint_Admin || ')');
    IF v_Assigned_To != v_Maint_Admin THEN
        RAISE_APPLICATION_ERROR(-20024, 'Test 12a FAIL: Assigned_To changed to ' || v_Assigned_To);
    END IF;
    PL('SLA_Level after escalation: ' || v_SLA_Level || ' (expect 普通, unchanged)');
    IF v_SLA_Level != '普通' THEN
        RAISE_APPLICATION_ERROR(-20025, 'Test 12a FAIL: SLA_Level changed to ' || v_SLA_Level);
    END IF;

    -- 验证审计事件
    SELECT COUNT(*) INTO v_Esc_Count FROM D_Audit_Event
    WHERE Event_Type = 'SLA_ESCALATION'
      AND Target_Type = 'REPAIR_TICKET'
      AND Target_ID = TO_CHAR(v_Ticket4_ID);
    PL('Audit events: ' || v_Esc_Count || ' (expect >=1)');
    IF v_Esc_Count < 1 THEN
        RAISE_APPLICATION_ERROR(-20026, 'Test 12a FAIL: no audit event');
    END IF;

    -- --- 12b: 二次巡检——Escalation_Time 已非空，不重复升级 ---
    SELECT Escalation_Time INTO v_Esc_Time
    FROM D_Repair_Ticket WHERE Ticket_ID = v_Ticket4_ID;

    SP_Escalate_SLA;

    -- Escalation_Time 不变
    DECLARE v_Esc_Time2 DATE;
    BEGIN
        SELECT Escalation_Time INTO v_Esc_Time2
        FROM D_Repair_Ticket WHERE Ticket_ID = v_Ticket4_ID;
        PL('Escalation_Time after 2nd scan: ' || TO_CHAR(v_Esc_Time2, 'YYYY-MM-DD HH24:MI:SS'));
        IF v_Esc_Time2 != v_Esc_Time THEN
            RAISE_APPLICATION_ERROR(-20027, 'Test 12b FAIL: Escalation_Time changed on 2nd scan');
        END IF;
    END;
    PL('PASS: 二次巡检未重复升级');

    -- ================================================================
    -- TEST 13: 完工日志 UK 冲突 → rc=3（SAVEPOINT 回滚）
    -- ================================================================
    P('--- Test 13: Repair log UK conflict → rc=3 (SAVEPOINT rollback) ---');

    v_Ticket5_ID := NewTicketId;
    INSERT INTO D_Repair_Ticket (
        Ticket_ID, Student_ID, Room_ID, Issue_Desc, Submit_Time, Status,
        SLA_Level, Deadline, Assigned_To
    ) VALUES (
        v_Ticket5_ID, 'S001', v_Room_ID, 'T13: 电源故障',
        SYSDATE, '处理中', '普通', SYSDATE + 1, v_Maint_Admin
    );
    COMMIT;
    PL('Created Ticket_ID=' || v_Ticket5_ID);

    -- 手动插入一条日志（模拟并发），占用 UK_D_REPAIR_LOG_TICKET
    DECLARE v_MaxLogId NUMBER;
    BEGIN
        SELECT NVL(MAX(Log_ID), 0) + 1 INTO v_MaxLogId FROM D_Repair_Log;
        INSERT INTO D_Repair_Log (Log_ID, Ticket_ID, Admin_ID, Process_Desc, Resolve_Time)
        VALUES (v_MaxLogId, v_Ticket5_ID, v_Maint_Admin, 'Race condition log', SYSDATE);
        COMMIT;
        PL('Inserted race-condition log for ticket ' || v_Ticket5_ID);
    END;

    -- 通过 SP 完工 → 应因 UK 冲突返回 rc=3，且状态回滚
    SP_Complete_Repair(v_Ticket5_ID, v_Maint_Admin, '正常完工', '已修复', NULL, v_Rc);
    PL('Complete (UK conflict) rc=' || v_Rc || ' (expect 3)');
    IF v_Rc != 3 THEN RAISE_APPLICATION_ERROR(-20028, 'Test 13 FAIL: expected rc=3, got ' || v_Rc); END IF;

    -- 验证状态未变（SAVEPOINT 回滚成功）
    SELECT Status INTO v_Status FROM D_Repair_Ticket WHERE Ticket_ID = v_Ticket5_ID;
    PL('Status after UK conflict: ' || v_Status || ' (expect 处理中, rollback worked)');
    IF v_Status != '处理中' THEN
        RAISE_APPLICATION_ERROR(-20029, 'Test 13 FAIL: Status changed to ' || v_Status || ' (SAVEPOINT not working)');
    END IF;

    -- ================================================================
    -- CLEANUP
    -- ================================================================
    P('--- Cleanup (only test-created data) ---');
    DELETE FROM D_Audit_Event WHERE Target_Type = 'REPAIR_TICKET'
        AND Target_ID IN (TO_CHAR(v_Ticket1_ID), TO_CHAR(v_Ticket2_ID), TO_CHAR(v_Ticket3_ID),
                          TO_CHAR(v_Ticket4_ID), TO_CHAR(v_Ticket5_ID));

    DELETE FROM D_Notification WHERE Title = '报修工单 SLA 超时提醒'
        AND Create_Time >= SYSDATE - INTERVAL '1' HOUR;

    DELETE FROM D_Repair_Log WHERE Ticket_ID IN
        (v_Ticket1_ID, v_Ticket2_ID, v_Ticket3_ID, v_Ticket4_ID, v_Ticket5_ID);

    DELETE FROM D_Repair_Ticket WHERE Ticket_ID IN
        (v_Ticket1_ID, v_Ticket2_ID, v_Ticket3_ID, v_Ticket4_ID, v_Ticket5_ID);

    -- 删除测试创建的 User_Account
    DELETE FROM D_User_Account WHERE Login_Name = 't5_mgr_' || v_Ts;

    -- 删除测试 Admin
    DELETE FROM D_Admin WHERE Admin_ID IN (v_Maint_Admin, v_Maint_Admin2, v_Mgr_Admin);

    COMMIT;
    PL('All test data cleaned up');

    P('');
    P('========== 所有 13 个测试全部通过! ==========');

EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        P('');
        P('========== 测试失败! ==========');
        P('Error: ' || SQLERRM || ' (code=' || SQLCODE || ')');
        -- Attempt cleanup
        BEGIN
            DELETE FROM D_Audit_Event WHERE Target_Type = 'REPAIR_TICKET'
                AND Target_ID IN (TO_CHAR(v_Ticket1_ID), TO_CHAR(v_Ticket2_ID), TO_CHAR(v_Ticket3_ID),
                                  TO_CHAR(v_Ticket4_ID), TO_CHAR(v_Ticket5_ID));
            DELETE FROM D_Notification WHERE Title = '报修工单 SLA 超时提醒'
                AND Create_Time >= SYSDATE - INTERVAL '1' HOUR;
            DELETE FROM D_Repair_Log WHERE Ticket_ID IN
                (v_Ticket1_ID, v_Ticket2_ID, v_Ticket3_ID, v_Ticket4_ID, v_Ticket5_ID);
            DELETE FROM D_Repair_Ticket WHERE Ticket_ID IN
                (v_Ticket1_ID, v_Ticket2_ID, v_Ticket3_ID, v_Ticket4_ID, v_Ticket5_ID);
            DELETE FROM D_User_Account WHERE Login_Name = 't5_mgr_' || v_Ts;
            DELETE FROM D_Admin WHERE Admin_ID IN (v_Maint_Admin, v_Maint_Admin2, v_Mgr_Admin);
            COMMIT;
        EXCEPTION WHEN OTHERS THEN NULL;
        END;
        RAISE;
END;
/
