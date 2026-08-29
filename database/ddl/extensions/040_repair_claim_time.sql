-- 扩展表迁移 040：D_Repair_Ticket 补 Claim_Time 列（接单时间）
-- 2026-08-29 定稿。
-- 用途：维修员「接受工单」持久化接单时间。SP_Claim_Ticket 原为并发守门
--       （自赋值 UPDATE，不改任何数据），导致维修员/学生端均无"已接收"反馈。
--       四审 PR #44 裁决接单不改状态机（CK_D_REPAIR_TICKET_STATUS 4 值），
--       故以可空 Claim_Time 记录接单时刻，UI 派生显示"已接收"。
-- 字段：Claim_Time DATE 可空——NULL=未接单；NOT NULL=已由指派维修员接单
--        （保留首次接单时间，SP 内 COALESCE）。
-- 关系：D_Repair_Ticket 单表列，不新增约束/索引，不改变既有字段语义。
-- 受影响接口：POST /repair-tickets/{ticketId}/claim（写 Claim_Time）；
--   GET /students/{studentId}/repair-tickets、GET /admins/{adminId}/repair-tickets
--   （响应体新增可空 claimTime 字段）。
-- 可重建：全量重建时位于 extensions/039 之后；幂等，重复执行安全。
-- 执行：DBeaver 整段执行，或由 init-db.sh DDL 清单调用。

DECLARE
    v_cnt NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_cnt FROM user_tab_columns
     WHERE table_name = 'D_REPAIR_TICKET' AND column_name = 'CLAIM_TIME';
    IF v_cnt = 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE D_Repair_Ticket ADD (Claim_Time DATE)';
        EXECUTE IMMEDIATE 'COMMENT ON COLUMN D_Repair_Ticket.Claim_Time IS ''接单时间（NULL=未接单；NOT NULL=已由指派维修员接单，接单不改状态机，UI 派生显示"已接收"）''';
    END IF;
END;
/
COMMIT;
