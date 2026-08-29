-- SLA派单存储过程（难点⑤ 四审修复版）
-- 依赖：D_Repair_Ticket, D_Repair_Log, D_Admin, D_Room
-- 基线：database/ddl/foundation/001_create_tables.sql + database/ddl/extensions/010_extension_tables.sql
-- DDL 由编号迁移 database/ddl/extensions/021_sla_dispatch.sql 提供（Escalation_Time /
--   Repair_Result VARCHAR2(200 CHAR) / SEQ_SLA_LOG / CK_D_REPAIR_TICKET_STATUS /
--   CK_D_ADMIN_ROLE），本文件不再内联任何 DDL（四审 R2）。
-- 状态机（PRD §2.3.4，四审裁决）：提交→待处理 → 派单→已派单 → 完工→已完成；可撤销→已撤销；
--   接单（SP_Claim_Ticket）不改状态，仅做并发守门——开工与否以维修日志是否存在判定（PR #44 裁决 §四-3）。
-- 流程：学生报修 → SP_Assign_Ticket(初始派单, 待处理→已派单) → SP_Claim_Ticket(被指派的维修员接单)
--   → SP_Complete_Repair(完工写日志, 已派单→已完成)
-- 巡检：SP_Escalate_SLA(普通超时→原子标记升级并返回清单，通知由应用层公共服务投递，不转派)
-- 注意：本 SP 不直接写 D_Notification / D_Audit_Event（数据拥有者边界，且 MAX+1 并发撞主键）

-- ============================================================
-- SP_Assign_Ticket：初始派单——根据楼栋自动指派维修员（待处理→已派单）
-- P2-1 修复：原子 UPDATE 守门（WHERE Assigned_To IS NULL AND Status='待处理'）
-- P1-4 配套：无维修员 → rc=3（配置异常），不再擅自回退楼长
-- 四审 R1：派单成功即置 Status='已派单'（与迁移 021 CK_D_REPAIR_TICKET_STATUS 4 值状态机一致）
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
        Deadline    = v_Submit_Time + INTERVAL '24' HOUR,
        Status      = '已派单'
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
-- SP_Claim_Ticket：被指派的维修员接单（并发守门，不改状态）
-- P1-3 修复：移除 Assigned_To IS NULL 路径，仅允许被指派的维修员接单
-- 四审 R1：接单不再写 Status（PRD §2.3.4 无'处理中'；开工与否以维修日志
--   是否存在判定，PR #44 裁决 §四-3）。此处以自赋值 UPDATE 作原子守门——
--   仅 Status='已派单' 且 Assigned_To=p_Admin_ID 的工单命中（SQL%ROWCOUNT 判定）。
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

    -- 原子守门（自赋值 UPDATE + 写接单时间，行计数判定是否命中）
    -- 迁移 040：Claim_Time 持久化接单时刻（COALESCE 保留首次接单时间），
    -- 状态机仍保持 4 值不变（四审 PR #44），UI 派生显示"已接收"。
    UPDATE D_Repair_Ticket
    SET Assigned_To = Assigned_To,
        Claim_Time = COALESCE(Claim_Time, SYSDATE)
    WHERE Ticket_ID = p_Ticket_ID
      AND Status = '已派单'
      AND Assigned_To = p_Admin_ID;

    IF SQL%ROWCOUNT = 0 THEN
        p_Result_Code := 1;
        RETURN;
    END IF;

    COMMIT;
END SP_Claim_Ticket;
/

-- ============================================================
-- SP_Complete_Repair：管理员完成维修 + 写入维修日志（已派单→已完成）
-- P1-2 修复：原子 UPDATE 守门——Status='已派单' AND Assigned_To=p_Admin_ID
-- P2-3 修复：SAVEPOINT + ROLLBACK 确保 INSERT 失败时不残留状态变更
-- 新增 p_Repair_Result 和 p_Solve_Time 参数对齐契约
-- 四审 R1：守门状态由'处理中'改为'已派单'（4 值状态机；开工与否以维修日志是否存在判定）
-- DORM-28
-- 返回：0=成功, 1=工单不存在, 2=状态不是已派单或非本人, 3=日志写入冲突(UK)
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

    -- 2. 原子 UPDATE 守门：仅 Status='已派单' 且指派给当前管理员的工单可完工
    UPDATE D_Repair_Ticket
    SET Status = '已完成'
    WHERE Ticket_ID = p_Ticket_ID
      AND Status = '已派单'
      AND Assigned_To = p_Admin_ID;

    IF SQL%ROWCOUNT = 0 THEN
        -- 区分不存在 vs 状态/权限不对
        DECLARE v_Dummy NUMBER;
        BEGIN
            SELECT 1 INTO v_Dummy FROM D_Repair_Ticket WHERE Ticket_ID = p_Ticket_ID;
            p_Result_Code := 2;  -- 状态不是已派单或非本人
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
-- SP_Escalate_SLA：SLA 升级巡检——普通超时工单原子标记升级并返回本次升级清单
-- P1-4 修复：只提醒不转派，不覆盖 Assigned_To，不改变 SLA_Level
-- 三审修复（并发安全 + 数据拥有者边界）：
--   1) FOR UPDATE SKIP LOCKED + 条件 UPDATE + SQL%ROWCOUNT 检查：
--      多实例并发巡检同一工单时，行锁保证每个工单只会被一个会话赢得
--      （另一会话 SKIP 或提交后重读已提交数据，条件不再成立 → 跳过），杜绝双升级；
--      ORDER BY Ticket_ID 固定加锁顺序，避免两实例交叉死锁。
--   2) 不再在 SP 内写 D_Notification / D_Audit_Event：
--      MAX(Audit_ID)+1、MAX(Notification_ID)+1 并发会撞主键；
--      通知改由应用层公共服务 INotificationService 投递（数据拥有者边界），
--      SP 通过 SYS_REFCURSOR 返回本次新升级的工单清单（含楼长信息）。
-- 每 15 分钟由 Quartz 调用。
-- ============================================================
CREATE OR REPLACE PROCEDURE SP_Escalate_SLA(
    p_Cursor OUT SYS_REFCURSOR
) AS
    v_Id_List VARCHAR2(4000);
BEGIN
    v_Id_List := NULL;

    -- 仅游标扫描 D_Repair_Ticket 单表（FOR UPDATE 要求可更新单表查询）；
    -- D_Room 的关联（楼长查询）在下游结果游标中完成。
    -- 四审 R1：状态集随 4 值状态机更新（'处理中'已剔除）；
    -- 未派单（待处理）工单 Deadline 为空，Deadline < SYSDATE 恒不成立，天然不命中。
    FOR ticket_rec IN (
        SELECT Ticket_ID
        FROM D_Repair_Ticket
        WHERE Status IN ('待处理', '已派单')
          AND SLA_Level = '普通'
          AND Deadline < SYSDATE
          AND Escalation_Time IS NULL   -- 仅首次升级
        ORDER BY Ticket_ID
        FOR UPDATE SKIP LOCKED
    ) LOOP
        -- 条件 UPDATE 守门：即使锁已持有，仍以 Escalation_Time IS NULL 为准，
        -- 与 Escalation_Time IS NOT NULL = 已升级 的语义保持一致
        UPDATE D_Repair_Ticket
        SET Escalation_Time = SYSDATE
        WHERE Ticket_ID = ticket_rec.Ticket_ID
          AND Escalation_Time IS NULL;

        IF SQL%ROWCOUNT = 1 THEN
            -- 本会话赢得该工单的升级权，记录 ID（数字列拼接，无注入面）
            v_Id_List := v_Id_List || ',' || ticket_rec.Ticket_ID;
        END IF;
    END LOOP;

    COMMIT;

    -- 返回本次赢得升级的工单清单（Ticket_ID, Room_ID, Assigned_To, Mgr_Admin_ID）
    -- Mgr_Admin_ID：本楼楼长（无楼长 → NULL，应用层跳过投递）
    IF v_Id_List IS NOT NULL THEN
        OPEN p_Cursor FOR
            'SELECT t.Ticket_ID, t.Room_ID, t.Assigned_To,
                    (SELECT Admin_ID FROM D_Admin
                      WHERE Role_Level = ''楼长''
                        AND Building_ID = r.Building_ID
                        AND ROWNUM = 1) AS Mgr_Admin_ID
               FROM D_Repair_Ticket t
               JOIN D_Room r ON t.Room_ID = r.Room_ID
              WHERE t.Ticket_ID IN (' || SUBSTR(v_Id_List, 2) || ')';
    ELSE
        -- 本次无可升级工单：返回空结果集（列结构与上一致，供应用层空循环）
        OPEN p_Cursor FOR
            SELECT NULL AS Ticket_ID, NULL AS Room_ID, NULL AS Assigned_To,
                   NULL AS Mgr_Admin_ID
            FROM DUAL WHERE 1 = 0;
    END IF;
END SP_Escalate_SLA;
/
