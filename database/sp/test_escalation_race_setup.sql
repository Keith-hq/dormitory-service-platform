-- ============================================================
-- 难点⑤ SLA升级 双会话竞态测试 — 准备阶段
-- 创建 3 张超时未升级的普通工单（Student_ID=NULL + Issue_Desc 前缀 RACE: 标记自有数据）
-- 运行方式（bash 编排）：
--   1) sqlplus @test_escalation_race_setup.sql   → 输出 RACE_TICKET_i=<id>
--   2) 两个 sqlplus 会话并行执行 @test_escalation_race_run.sql
--   3) sqlplus @test_escalation_race_verify.sql  → 断言每张只升级一次并清理
-- ============================================================
SET SERVEROUTPUT ON SIZE UNLIMITED
DECLARE
    v_Room_ID NUMBER;
    v_Id      NUMBER;
BEGIN
    SELECT MIN(Room_ID) INTO v_Room_ID FROM D_Room;

    FOR i IN 1..3 LOOP
        SELECT NVL(MAX(Ticket_ID), 0) + 1 INTO v_Id FROM D_Repair_Ticket;
        INSERT INTO D_Repair_Ticket (
            Ticket_ID, Student_ID, Room_ID, Issue_Desc, Submit_Time,
            Status, SLA_Level, Deadline, Assigned_To, Escalation_Time
        ) VALUES (
            v_Id, NULL, v_Room_ID,
            'RACE: overdue ticket ' || i,
            SYSDATE - 2, '待处理', '普通', SYSDATE - 1, NULL, NULL
        );
        COMMIT;
        DBMS_OUTPUT.PUT_LINE('RACE_TICKET_' || i || '=' || v_Id);
    END LOOP;
END;
/
EXIT;
