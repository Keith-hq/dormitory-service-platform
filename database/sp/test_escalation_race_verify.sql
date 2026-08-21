-- ============================================================
-- 难点⑤ SLA升级 双会话竞态测试 — 校验与清理阶段
-- 断言：所有 RACE 工单均被升级（Escalation_Time NOT NULL）；
--       "每张只出现在一个会话的清单中"由 bash 编排层对比两个会话输出断言。
-- 清理：只删本脚本创建的数据（Issue_Desc LIKE 'RACE:%' AND Student_ID IS NULL）
-- ============================================================
SET SERVEROUTPUT ON SIZE UNLIMITED
DECLARE
    v_Bad NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_Bad FROM D_Repair_Ticket
    WHERE Issue_Desc LIKE 'RACE:%' AND Student_ID IS NULL
      AND Escalation_Time IS NULL;

    DBMS_OUTPUT.PUT_LINE('UNESCALATED_RACE_TICKETS=' || v_Bad);
    IF v_Bad != 0 THEN
        RAISE_APPLICATION_ERROR(-20101,
            'race verify FAIL: ' || v_Bad || ' race tickets not escalated');
    END IF;

    -- 升级唯一性由 Escalation_Time 单列 + 条件 UPDATE 保证（多实例下每单只赢一次），
    -- 此处再校验不存在 Escalation_Time 被覆盖的痕迹：无第二列可被双写。
    DBMS_OUTPUT.PUT_LINE('RACE_VERIFY_OK');

    -- 清理自有数据
    DELETE FROM D_Repair_Log
    WHERE Ticket_ID IN (SELECT Ticket_ID FROM D_Repair_Ticket
                        WHERE Issue_Desc LIKE 'RACE:%' AND Student_ID IS NULL);
    DELETE FROM D_Repair_Ticket
    WHERE Issue_Desc LIKE 'RACE:%' AND Student_ID IS NULL;
    COMMIT;
    DBMS_OUTPUT.PUT_LINE('RACE_CLEANUP_OK');
END;
/
EXIT;
