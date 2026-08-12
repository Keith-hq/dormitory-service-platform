-- SLA派单存储过程（难点⑤ 一审修复版）
-- 依赖：D_Repair_Ticket, D_Repair_Log, D_Admin, D_Room, D_User_Account, D_Notification, D_Audit_Event
-- 基线：database/ddl/foundation/001_create_tables.sql + database/ddl/extensions/010_extension_tables.sql
-- 流程：学生报修 → SP_Assign_Ticket(初始派单) → SP_Claim_Ticket(被指派的维修员接单) → SP_Complete_Repair(完工写日志)
-- 巡检：SP_Escalate_SLA(普通超时→通知楼长，不转派)

-- ============================================================
-- DDL 补丁：增列 + 约束（幂等执行）
-- ============================================================

-- 补丁 1：D_Repair_Ticket 加 Escalation_Time（NULL=未升级，NOT NULL=已升级，防二次升级）
BEGIN
    EXECUTE IMMEDIATE 'ALTER TABLE D_Repair_Ticket ADD (Escalation_Time DATE)';
EXCEPTION WHEN OTHERS THEN
    IF SQLCODE = -1430 THEN NULL; ELSE RAISE; END IF;
END;
/

-- 补丁 2：D_Repair_Log 加 Repair_Result（对齐契约 result 字段）
BEGIN
    EXECUTE IMMEDIATE 'ALTER TABLE D_Repair_Log ADD (Repair_Result VARCHAR2(200))';
EXCEPTION WHEN OTHERS THEN
    IF SQLCODE = -1430 THEN NULL; ELSE RAISE; END IF;
END;
/

-- 补丁 3：D_Repair_Ticket.Status CHECK 约束
BEGIN
    EXECUTE IMMEDIATE 'ALTER TABLE D_Repair_Ticket ADD CONSTRAINT CK_D_REPAIR_TICKET_STATUS CHECK (Status IN (''待处理'',''处理中'',''已完成''))';
EXCEPTION WHEN OTHERS THEN
    IF SQLCODE = -2260 THEN NULL; ELSE RAISE; END IF;
END;
/

-- 补丁 4：D_Admin.Role_Level CHECK 约束
BEGIN
    EXECUTE IMMEDIATE 'ALTER TABLE D_Admin ADD CONSTRAINT CK_D_ADMIN_ROLE CHECK (Role_Level IN (''楼长'',''维修员'',''超级管理员''))';
EXCEPTION WHEN OTHERS THEN
    IF SQLCODE = -2260 THEN NULL; ELSE RAISE; END IF;
END;
/

-- ============================================================
-- 创建专用序列
-- ============================================================
DECLARE v_StartVal NUMBER;
BEGIN
    SELECT NVL(MAX(Log_ID), 0) + 1 INTO v_StartVal FROM D_Repair_Log;
    EXECUTE IMMEDIATE 'CREATE SEQUENCE SEQ_SLA_LOG START WITH ' || v_StartVal || ' INCREMENT BY 1';
EXCEPTION WHEN OTHERS THEN IF SQLCODE = -955 THEN NULL; ELSE RAISE; END IF;
END;
/

-- ============================================================
-- SP_Assign_Ticket：初始派单——根据楼栋自动指派维修员
-- P2-1 修复：原子 UPDATE 守门（WHERE Assigned_To IS NULL AND Status='待处理'）
-- P1-4 配套：无维修员 → rc=3（配置异常），不再擅自回退楼长
-- 返回：0=成功, 1=工单不存在, 2=已指派或状态不对(并发被抢), 3=无可用维修员
-- ============================================================
CREATE OR REPLACE PROCEDURE SP_Assign_Ticket(
    p_Ticket_ID   IN  NUMBER,
    p_Result_Code OUT NUMBER
) AS
    v_Room_ID     NUMBER;
    v_Building_ID NUMBER;
    v_Admin_ID    VARCHAR2(20);
    v_Submit_Time DATE;
    v_Row_Count   NUMBER;
BEGIN
    p_Result_Code := 0;

    -- 1. 取工单信息（Room_ID + Submit_Time）
    BEGIN
        SELECT Room_ID, Submit_Time
        INTO v_Room_ID, v_Submit_Time
        FROM D_Repair_Ticket WHERE Ticket_ID = p_Ticket_ID;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN p_Result_Code := 1; RETURN;
    END;

    -- 2. Room_ID → Building_ID
    BEGIN
        SELECT Building_ID INTO v_Building_ID
        FROM D_Room WHERE Room_ID = v_Room_ID;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN p_Result_Code := 3; RETURN;
    END;

    -- 3. 找本楼维修员
    BEGIN
        SELECT Admin_ID INTO v_Admin_ID
        FROM D_Admin
        WHERE Role_Level = '维修员' AND Building_ID = v_Building_ID
          AND ROWNUM = 1;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN v_Admin_ID := NULL;
    END;

    -- 无维修员 → rc=3（配置异常），不擅自回退楼长
    IF v_Admin_ID IS NULL THEN
        p_Result_Code := 3;
        RETURN;
    END IF;

    -- 4. 原子派单：WHERE 条件确保只有 Assigned_To IS NULL + Status='待处理' 才更新
    UPDATE D_Repair_Ticket
    SET Assigned_To = v_Admin_ID,
        SLA_Level   = '普通',
        Deadline    = v_Submit_Time + INTERVAL '24' HOUR
    WHERE Ticket_ID = p_Ticket_ID
      AND Assigned_To IS NULL
      AND Status = '待处理';

    IF SQL%ROWCOUNT = 0 THEN
        p_Result_Code := 2;  -- 已被他人指派/状态已变更
        RETURN;
    END IF;

    COMMIT;
END SP_Assign_Ticket;
/

-- ============================================================
-- SP_Claim_Ticket：被指派的维修员接单（并发唯一）
-- P1-3 修复：移除 Assigned_To IS NULL 路径，仅允许被指派的维修员接单
-- 原子 UPDATE 守门：Status='待处理' AND Assigned_To=p_Admin_ID
-- DORM-27
-- 返回：0=成功, 1=工单不存在/状态不对/非本人指派
-- ============================================================
CREATE OR REPLACE PROCEDURE SP_Claim_Ticket(
    p_Ticket_ID   IN  NUMBER,
    p_Admin_ID    IN  VARCHAR2,
    p_Result_Code OUT NUMBER
) AS
BEGIN
    p_Result_Code := 0;

    -- 原子接单：仅允许被指派的管理员将待处理工单转为处理中
    UPDATE D_Repair_Ticket
    SET Status = '处理中'
    WHERE Ticket_ID = p_Ticket_ID
      AND Status = '待处理'
      AND Assigned_To = p_Admin_ID;

    IF SQL%ROWCOUNT = 0 THEN
        p_Result_Code := 1;
        RETURN;
    END IF;

    COMMIT;
END SP_Claim_Ticket;
/

-- ============================================================
-- SP_Complete_Repair：管理员完成维修 + 写入维修日志
-- P1-2 修复：原子 UPDATE 守门——Status='处理中' AND Assigned_To=p_Admin_ID
-- P2-3 修复：SAVEPOINT + ROLLBACK 确保 INSERT 失败时不残留状态变更
-- 新增 p_Repair_Result 和 p_Solve_Time 参数对齐契约
-- DORM-28
-- 返回：0=成功, 1=工单不存在, 2=状态不是处理中或非本人, 3=日志写入冲突(UK)
-- ============================================================
CREATE OR REPLACE PROCEDURE SP_Complete_Repair(
    p_Ticket_ID     IN  NUMBER,
    p_Admin_ID      IN  VARCHAR2,
    p_Process_Desc  IN  VARCHAR2,
    p_Repair_Result IN  VARCHAR2 DEFAULT NULL,
    p_Solve_Time    IN  DATE DEFAULT NULL,
    p_Result_Code   OUT NUMBER
) AS
    v_Solve_Time DATE;
BEGIN
    p_Result_Code := 0;
    v_Solve_Time := NVL(p_Solve_Time, SYSDATE);

    -- 1. SAVEPOINT 守门——确保后续 INSERT 失败时可回滚状态变更
    SAVEPOINT sp_complete;

    -- 2. 原子 UPDATE 守门：仅 Status='处理中' 且指派给当前管理员的工单可完工
    UPDATE D_Repair_Ticket
    SET Status = '已完成'
    WHERE Ticket_ID = p_Ticket_ID
      AND Status = '处理中'
      AND Assigned_To = p_Admin_ID;

    IF SQL%ROWCOUNT = 0 THEN
        -- 区分不存在 vs 状态/权限不对
        DECLARE v_Dummy NUMBER;
        BEGIN
            SELECT 1 INTO v_Dummy FROM D_Repair_Ticket WHERE Ticket_ID = p_Ticket_ID;
            p_Result_Code := 2;  -- 状态不是处理中或非本人
        EXCEPTION
            WHEN NO_DATA_FOUND THEN p_Result_Code := 1;  -- 工单不存在
        END;
        ROLLBACK TO sp_complete;
        RETURN;
    END IF;

    -- 3. 写维修日志（UK_D_REPAIR_LOG_TICKET 唯一约束确保一单一日志）
    BEGIN
        INSERT INTO D_Repair_Log (
            Log_ID, Ticket_ID, Admin_ID, Process_Desc, Repair_Result, Resolve_Time
        ) VALUES (
            SEQ_SLA_LOG.NEXTVAL, p_Ticket_ID, p_Admin_ID, p_Process_Desc,
            p_Repair_Result, v_Solve_Time
        );
    EXCEPTION
        WHEN DUP_VAL_ON_INDEX THEN
            ROLLBACK TO sp_complete;
            p_Result_Code := 3;  -- 日志写入冲突（UK_D_REPAIR_LOG_TICKET）
            RETURN;
    END;

    COMMIT;
END SP_Complete_Repair;
/

-- ============================================================
-- SP_Escalate_SLA：SLA 升级巡检——普通超时工单通知楼长，不转派
-- P1-4 修复：只提醒不转派，不覆盖 Assigned_To，不改变 SLA_Level
-- 每 15 分钟由 Quartz 调用
-- Escalation_Time IS NULL → 首次升级，写入通知+审计
-- Escalation_Time IS NOT NULL → 已升级过，跳过（防二次升级）
-- ============================================================
CREATE OR REPLACE PROCEDURE SP_Escalate_SLA AS
    v_Building_ID    NUMBER;
    v_Mgr_Admin_ID   VARCHAR2(20);
    v_Mgr_Account_ID NUMBER;
    v_Audit_ID       NUMBER;
    v_Notif_ID       NUMBER;
BEGIN
    FOR ticket_rec IN (
        SELECT t.Ticket_ID, t.Assigned_To, t.SLA_Level, r.Building_ID,
               t.Room_ID
        FROM D_Repair_Ticket t
        JOIN D_Room r ON t.Room_ID = r.Room_ID
        WHERE t.Status IN ('待处理', '处理中')
          AND t.SLA_Level = '普通'
          AND t.Deadline < SYSDATE
          AND t.Escalation_Time IS NULL   -- 仅首次升级
    ) LOOP
        -- 找本楼楼长
        BEGIN
            SELECT Admin_ID INTO v_Mgr_Admin_ID
            FROM D_Admin
            WHERE Role_Level = '楼长' AND Building_ID = ticket_rec.Building_ID
              AND ROWNUM = 1;
        EXCEPTION
            WHEN NO_DATA_FOUND THEN CONTINUE;  -- 无楼长则跳过该条
        END;

        -- 通过 D_User_Account 解析楼长的 Account_ID
        BEGIN
            SELECT Account_ID INTO v_Mgr_Account_ID
            FROM D_User_Account
            WHERE Admin_ID = v_Mgr_Admin_ID AND Account_Status = '正常'
              AND ROWNUM = 1;
        EXCEPTION
            WHEN NO_DATA_FOUND THEN CONTINUE;  -- 楼长无账户则跳过
        END;

        -- 标记首次升级时间（防二次升级）
        UPDATE D_Repair_Ticket
        SET Escalation_Time = SYSDATE
        WHERE Ticket_ID = ticket_rec.Ticket_ID
          AND Escalation_Time IS NULL;

        -- 写审计事件
        SELECT NVL(MAX(Audit_ID), 0) + 1 INTO v_Audit_ID FROM D_Audit_Event;
        INSERT INTO D_Audit_Event (
            Audit_ID, Actor_Account_ID, Event_Type, Target_Type, Target_ID, Event_Time
        ) VALUES (
            v_Audit_ID, NULL, 'SLA_ESCALATION', 'REPAIR_TICKET',
            TO_CHAR(ticket_rec.Ticket_ID), SYSDATE
        );

        -- 通知楼长（Notification_Type='报修'，对齐已冻结枚举）
        SELECT NVL(MAX(Notification_ID), 0) + 1 INTO v_Notif_ID FROM D_Notification;
        INSERT INTO D_Notification (
            Notification_ID, Recipient_Account_ID, Title, Content,
            Notification_Type, Create_Time
        ) VALUES (
            v_Notif_ID, v_Mgr_Account_ID,
            '报修工单 SLA 超时提醒',
            '工单#' || ticket_rec.Ticket_ID
            || '（Room_ID=' || ticket_rec.Room_ID
            || '）已超过处理时限（' || ticket_rec.SLA_Level || '/'
            || TO_CHAR(SYSDATE, 'YYYY-MM-DD HH24:MI') || '），请跟进处理。'
            || '当前负责人：' || NVL(ticket_rec.Assigned_To, '未指派'),
            '报修',
            SYSDATE
        );
    END LOOP;

    COMMIT;
END SP_Escalate_SLA;
/
