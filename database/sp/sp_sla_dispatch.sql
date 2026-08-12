-- SLA派单存储过程（难点⑤）
-- 依赖：D_Repair_Ticket, D_Repair_Log, D_Admin, D_Room
-- 基线：database/ddl/foundation/001_create_tables.sql
-- 流程：学生报修 → SP_Assign_Ticket(初始派单) → SP_Claim_Ticket(管理员接单) → SP_Complete_Repair(完工)
-- 巡检：SP_Escalate_SLA(普通超时→紧急+转派楼长)

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
-- 返回：0=成功, 1=工单不存在, 2=已指派(幂等), 3=无可用管理员
-- ============================================================
CREATE OR REPLACE PROCEDURE SP_Assign_Ticket(
    p_Ticket_ID   IN  NUMBER,
    p_Result_Code OUT NUMBER
) AS
    v_Room_ID     NUMBER;
    v_Building_ID NUMBER;
    v_Admin_ID    VARCHAR2(20);
    v_Assigned    VARCHAR2(20);
    v_Submit_Time DATE;
BEGIN
    p_Result_Code := 0;

    -- 1. 取工单信息
    BEGIN
        SELECT Room_ID, Submit_Time, Assigned_To
        INTO v_Room_ID, v_Submit_Time, v_Assigned
        FROM D_Repair_Ticket WHERE Ticket_ID = p_Ticket_ID;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN p_Result_Code := 1; RETURN;
    END;

    -- 幂等：已指派则跳过
    IF v_Assigned IS NOT NULL THEN p_Result_Code := 2; RETURN; END IF;

    -- 2. Room_ID → Building_ID
    BEGIN
        SELECT Building_ID INTO v_Building_ID
        FROM D_Room WHERE Room_ID = v_Room_ID;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN p_Result_Code := 3; RETURN;
    END;

    -- 3. 优先找本楼维修员
    BEGIN
        SELECT Admin_ID INTO v_Admin_ID
        FROM D_Admin
        WHERE Role_Level = '维修员' AND Building_ID = v_Building_ID
          AND ROWNUM = 1;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN v_Admin_ID := NULL;
    END;

    -- 4. 派单
    IF v_Admin_ID IS NOT NULL THEN
        UPDATE D_Repair_Ticket
        SET Assigned_To = v_Admin_ID,
            SLA_Level   = '普通',
            Deadline    = v_Submit_Time + INTERVAL '24' HOUR
        WHERE Ticket_ID = p_Ticket_ID;
    ELSE
        -- 无维修员 → 直接派楼长，紧急级别
        BEGIN
            SELECT Admin_ID INTO v_Admin_ID
            FROM D_Admin
            WHERE Role_Level = '楼长' AND Building_ID = v_Building_ID
              AND ROWNUM = 1;
        EXCEPTION
            WHEN NO_DATA_FOUND THEN p_Result_Code := 3; RETURN;
        END;

        UPDATE D_Repair_Ticket
        SET Assigned_To = v_Admin_ID,
            SLA_Level   = '紧急',
            Deadline    = v_Submit_Time + INTERVAL '12' HOUR
        WHERE Ticket_ID = p_Ticket_ID;
    END IF;

    COMMIT;
END SP_Assign_Ticket;
/

-- ============================================================
-- SP_Claim_Ticket：管理员接单（并发唯一）—— 原子 UPDATE 守门
-- DORM-27
-- 返回：0=成功, 1=工单不存在/状态不对/已被抢/非本人
-- ============================================================
CREATE OR REPLACE PROCEDURE SP_Claim_Ticket(
    p_Ticket_ID   IN  NUMBER,
    p_Admin_ID    IN  VARCHAR2,
    p_Result_Code OUT NUMBER
) AS
BEGIN
    p_Result_Code := 0;

    -- 原子抢单：只有 Status='待处理' 且指派给当前管理员（或未指派）的工单才能接
    UPDATE D_Repair_Ticket
    SET Status = '处理中'
    WHERE Ticket_ID = p_Ticket_ID
      AND Status = '待处理'
      AND (Assigned_To = p_Admin_ID OR Assigned_To IS NULL);

    IF SQL%ROWCOUNT = 0 THEN
        p_Result_Code := 1;
        RETURN;
    END IF;

    COMMIT;
END SP_Claim_Ticket;
/

-- ============================================================
-- SP_Complete_Repair：管理员完成维修 + 写入维修日志
-- DORM-28
-- 返回：0=成功, 1=工单不存在, 2=工单已完成(幂等), 3=日志写入冲突
-- ============================================================
CREATE OR REPLACE PROCEDURE SP_Complete_Repair(
    p_Ticket_ID    IN  NUMBER,
    p_Admin_ID     IN  VARCHAR2,
    p_Process_Desc IN  VARCHAR2,
    p_Result_Code  OUT NUMBER
) AS
    v_Status VARCHAR2(20);
BEGIN
    p_Result_Code := 0;

    -- 1. 检查工单状态
    BEGIN
        SELECT Status INTO v_Status
        FROM D_Repair_Ticket WHERE Ticket_ID = p_Ticket_ID;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN p_Result_Code := 1; RETURN;
    END;

    IF v_Status = '已完成' THEN p_Result_Code := 2; RETURN; END IF;

    -- 2. 写维修日志（先写日志，UK 唯一约束确保一单一日志）
    BEGIN
        INSERT INTO D_Repair_Log (
            Log_ID, Ticket_ID, Admin_ID, Process_Desc, Resolve_Time
        ) VALUES (
            SEQ_SLA_LOG.NEXTVAL, p_Ticket_ID, p_Admin_ID, p_Process_Desc, SYSDATE
        );
    EXCEPTION
        WHEN DUP_VAL_ON_INDEX THEN p_Result_Code := 3; RETURN;
    END;

    -- 3. 标记完成
    UPDATE D_Repair_Ticket
    SET Status = '已完成'
    WHERE Ticket_ID = p_Ticket_ID;

    COMMIT;
END SP_Complete_Repair;
/

-- ============================================================
-- SP_Escalate_SLA：SLA升级巡检——普通超时工单 → 紧急 + 转派楼长
-- 每15分钟由 Quartz 调用
-- 只升级"普通"超时工单，"紧急"已是最高的不再升级
-- ============================================================
CREATE OR REPLACE PROCEDURE SP_Escalate_SLA AS
    v_Building_ID NUMBER;
    v_Admin_ID    VARCHAR2(20);
BEGIN
    FOR ticket_rec IN (
        SELECT t.Ticket_ID, r.Building_ID
        FROM D_Repair_Ticket t
        JOIN D_Room r ON t.Room_ID = r.Room_ID
        WHERE t.Status IN ('待处理', '处理中')
          AND t.SLA_Level = '普通'
          AND t.Deadline < SYSDATE
    ) LOOP
        -- 找本楼楼长
        BEGIN
            SELECT Admin_ID INTO v_Admin_ID
            FROM D_Admin
            WHERE Role_Level = '楼长' AND Building_ID = ticket_rec.Building_ID
              AND ROWNUM = 1;
        EXCEPTION
            WHEN NO_DATA_FOUND THEN CONTINUE;  -- 无楼长则跳过该条
        END;

        -- 升级：转派楼长 + 紧急 + Deadline 重置 12h
        UPDATE D_Repair_Ticket
        SET SLA_Level   = '紧急',
            Assigned_To = v_Admin_ID,
            Deadline    = SYSDATE + INTERVAL '12' HOUR
        WHERE Ticket_ID = ticket_rec.Ticket_ID;
    END LOOP;

    COMMIT;
END SP_Escalate_SLA;
/
