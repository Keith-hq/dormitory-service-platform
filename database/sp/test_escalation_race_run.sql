-- ============================================================
-- 难点⑤ SLA升级 双会话竞态测试 — 单会话巡检脚本
-- 调用 SP_Escalate_SLA 并打印本会话赢得的升级清单。
-- 两个此脚本的并行会话竞争同一批工单；每个工单只应出现在
-- 其中一个会话的 ESCALATED_TICKET 输出中（FOR UPDATE SKIP LOCKED
-- + 条件 UPDATE + SQL%ROWCOUNT 守门）。
-- ============================================================
SET SERVEROUTPUT ON SIZE UNLIMITED
DECLARE
    v_Cur SYS_REFCURSOR;
    v_Tid NUMBER;
    v_Rid NUMBER;
    v_Ato VARCHAR2(20);
    v_Mgr VARCHAR2(20);
    v_Cnt NUMBER := 0;
BEGIN
    SP_Escalate_SLA(v_Cur);
    LOOP
        FETCH v_Cur INTO v_Tid, v_Rid, v_Ato, v_Mgr;
        EXIT WHEN v_Cur%NOTFOUND;
        v_Cnt := v_Cnt + 1;
        DBMS_OUTPUT.PUT_LINE('ESCALATED_TICKET=' || v_Tid);
    END LOOP;
    CLOSE v_Cur;
    DBMS_OUTPUT.PUT_LINE('TOTAL_ESCALATED=' || v_Cnt);
END;
/
EXIT;
