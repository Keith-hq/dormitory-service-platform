-- 共享物品借还与维修耗材出库存储过程（难点④）
-- 依赖：D_Shared_Item, D_Item_Loan, D_Repair_Material, D_Repair_Material_Usage, D_Notification, D_User_Account
-- 基线：database/ddl/extensions/010_extension_tables.sql
-- 裁决红线：D_Shared_Item 不复用为桶装水库存；三种库存不混用
--
-- 三审修订（业务规则以组长确认为准）：
--   1. 信用分扣分统一走应用层信用分公共服务 ICreditService.DeductAsync
--      （Event_Key 幂等 + 学生行锁串行化 + 冻结通知），本文件所有 SP 不再直写
--      D_Credit_Account / D_Credit_Log；
--   2. 超期归还按 PRD 只在归还时按次扣 2 分（Event_Key = OVERDUE-{Loan_ID}），
--      逾期巡检（SP_Check_Overdue）只发提醒通知，不扣分；
--   3. Idempotency-Key 增加内容比对：同 Key 不同请求内容返回明确的业务错误码；
--      并发重放由唯一索引兜底——INSERT 撞 UK 时回滚库存扣减、读取胜者记录、
--      全量比对内容后返回原结果或冲突码。

-- ============================================================
-- 创建专用序列
-- ============================================================
DECLARE v_StartVal NUMBER;
BEGIN
    SELECT NVL(MAX(Loan_ID), 0) + 1 INTO v_StartVal FROM D_Item_Loan;
    EXECUTE IMMEDIATE 'CREATE SEQUENCE SEQ_ITEM_LOAN START WITH ' || v_StartVal || ' INCREMENT BY 1';
EXCEPTION WHEN OTHERS THEN IF SQLCODE = -955 THEN NULL; ELSE RAISE; END IF;
END;
/

DECLARE v_StartVal NUMBER;
BEGIN
    SELECT NVL(MAX(Usage_ID), 0) + 1 INTO v_StartVal FROM D_Repair_Material_Usage;
    EXECUTE IMMEDIATE 'CREATE SEQUENCE SEQ_MATERIAL_USAGE START WITH ' || v_StartVal || ' INCREMENT BY 1';
EXCEPTION WHEN OTHERS THEN IF SQLCODE = -955 THEN NULL; ELSE RAISE; END IF;
END;
/

-- ============================================================
-- DDL 补丁：为幂等键添加列与唯一索引
-- ============================================================
BEGIN
    EXECUTE IMMEDIATE 'ALTER TABLE D_Item_Loan ADD (Idempotency_Key VARCHAR2(100))';
EXCEPTION WHEN OTHERS THEN
    IF SQLCODE = -1430 THEN NULL; ELSE RAISE; END IF;
END;
/

BEGIN
    EXECUTE IMMEDIATE 'CREATE UNIQUE INDEX UK_D_ITEM_LOAN_IDEM ON D_Item_Loan (Idempotency_Key)';
EXCEPTION WHEN OTHERS THEN
    IF SQLCODE = -955 THEN NULL; ELSE RAISE; END IF;
END;
/

BEGIN
    EXECUTE IMMEDIATE 'ALTER TABLE D_Repair_Material_Usage ADD (Idempotency_Key VARCHAR2(100))';
EXCEPTION WHEN OTHERS THEN
    IF SQLCODE = -1430 THEN NULL; ELSE RAISE; END IF;
END;
/

BEGIN
    EXECUTE IMMEDIATE 'CREATE UNIQUE INDEX UK_D_REPAIR_MAT_USE_IDEM ON D_Repair_Material_Usage (Idempotency_Key)';
EXCEPTION WHEN OTHERS THEN
    IF SQLCODE = -955 THEN NULL; ELSE RAISE; END IF;
END;
/

-- ============================================================
-- SP_Borrow_Item：借出共享物品
-- 返回：0=成功, 1=物品不存在, 2=物品停用, 3=库存不足,
--       4=信用分不足（低于60）, 5=幂等键已使用且请求内容不一致
-- 并发语义（三审 P1-2）：
--   同 Key 并发：两个会话都通过幂等快路径后，先扣库存者先 INSERT 成功；
--   后到者的 INSERT 撞唯一索引 UK_D_ITEM_LOAN_IDEM → 回滚库存扣减（SAVEPOINT）
--   → 重读胜者记录（Oracle 保证唯一键冲突可见时胜者已提交）→ 比对内容。
--   内容一致（Student_ID + Item_ID）返回原 Loan_ID，不一致返回 5。
-- ============================================================
CREATE OR REPLACE PROCEDURE SP_Borrow_Item(
    p_Item_ID         IN  NUMBER,
    p_Student_ID      IN  VARCHAR2,
    p_Idempotency_Key IN  VARCHAR2,
    p_Result_Code     OUT NUMBER,
    p_Loan_ID         OUT NUMBER
) AS
    v_Status       VARCHAR2(10);
    v_Available    NUMBER;
    v_Credit_Score NUMBER;
    v_Ex_Student   VARCHAR2(20);
    v_Ex_Item      NUMBER;
BEGIN
    p_Result_Code := 0;
    p_Loan_ID := NULL;

    -- 0. 幂等快路径：同 Key 已处理过 → 比对完整请求内容
    IF p_Idempotency_Key IS NOT NULL THEN
        BEGIN
            SELECT Loan_ID, Student_ID, Item_ID
            INTO p_Loan_ID, v_Ex_Student, v_Ex_Item
            FROM D_Item_Loan
            WHERE Idempotency_Key = p_Idempotency_Key;

            IF v_Ex_Student = p_Student_ID AND v_Ex_Item = p_Item_ID THEN
                p_Result_Code := 0;  -- 内容一致，返回原 Loan_ID
            ELSE
                p_Result_Code := 5;  -- 同 Key 不同请求内容
            END IF;
            RETURN;
        EXCEPTION
            WHEN NO_DATA_FOUND THEN NULL;  -- 未使用过，继续处理
        END;
    END IF;

    -- 1. 检查物品
    BEGIN
        SELECT Status, Available_Qty INTO v_Status, v_Available
        FROM D_Shared_Item WHERE Item_ID = p_Item_ID;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN p_Result_Code := 1; RETURN;
    END;

    IF v_Status != '正常' THEN p_Result_Code := 2; RETURN; END IF;
    IF v_Available <= 0 THEN p_Result_Code := 3; RETURN; END IF;

    -- 2. 信用分检查（只读闸门，扣分不在这里发生）
    BEGIN
        SELECT Current_Score INTO v_Credit_Score
        FROM D_Credit_Account WHERE Student_ID = p_Student_ID;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN p_Result_Code := 4; RETURN;
    END;

    IF v_Credit_Score < 60 THEN p_Result_Code := 4; RETURN; END IF;
    -- 阈值 60 为信用分冻结线，与 D_Credit_Account 业务规则对齐；修改时两处需同步

    -- 3. 原子扣减库存 + 写借出记录（SAVEPOINT 保护：INSERT 撞唯一键时回滚库存）
    SAVEPOINT sp_borrow;

    UPDATE D_Shared_Item
    SET Available_Qty = Available_Qty - 1
    WHERE Item_ID = p_Item_ID AND Available_Qty > 0;

    IF SQL%ROWCOUNT = 0 THEN p_Result_Code := 3; RETURN; END IF;

    BEGIN
        INSERT INTO D_Item_Loan (
            Loan_ID, Item_ID, Student_ID, Borrow_Time, Due_Time, Idempotency_Key
        ) VALUES (
            SEQ_ITEM_LOAN.NEXTVAL, p_Item_ID, p_Student_ID, SYSDATE, SYSDATE + 1, p_Idempotency_Key
        ) RETURNING Loan_ID INTO p_Loan_ID;
    EXCEPTION
        WHEN DUP_VAL_ON_INDEX THEN
            IF p_Idempotency_Key IS NULL THEN RAISE; END IF;

            -- 并发重放：撤销本次库存扣减，读取胜者记录做内容比对
            ROLLBACK TO sp_borrow;

            SELECT Loan_ID, Student_ID, Item_ID
            INTO p_Loan_ID, v_Ex_Student, v_Ex_Item
            FROM D_Item_Loan
            WHERE Idempotency_Key = p_Idempotency_Key;

            IF v_Ex_Student = p_Student_ID AND v_Ex_Item = p_Item_ID THEN
                p_Result_Code := 0;  -- 内容一致：并发兄弟请求已成功，返回其 Loan_ID
            ELSE
                p_Result_Code := 5;  -- 同 Key 不同请求内容
            END IF;
            COMMIT;
            RETURN;
    END;

    COMMIT;
END SP_Borrow_Item;
/

-- ============================================================
-- SP_Return_Item：归还共享物品（原子 + 校验 Student_ID 归属）
-- 返回：0=成功, 1=借出记录不存在/已归还/非本人
-- p_Overdue_Days：逾期天数（0=未逾期），供应用层判定是否扣信用分
-- 三审 P1-1：本 SP 不再直写信用分表。超期归还按 PRD 按次扣 2 分，
--   由应用层调用信用分公共服务 ICreditService.DeductAsync 完成
--   （Event_Key = OVERDUE-{Loan_ID}：同一笔借出只扣一次，串行化+幂等+冻结通知）。
-- ============================================================
CREATE OR REPLACE PROCEDURE SP_Return_Item(
    p_Loan_ID      IN  NUMBER,
    p_Student_ID   IN  VARCHAR2,
    p_Result_Code  OUT NUMBER,
    p_Overdue_Days OUT NUMBER
) AS
    v_Item_ID  NUMBER;
    v_Due_Time DATE;
BEGIN
    p_Result_Code := 0;
    p_Overdue_Days := 0;

    -- 1. 原子写归还时间——校验 Student_ID 防止跨用户枚举 Loan_ID
    UPDATE D_Item_Loan
    SET Return_Time = SYSDATE
    WHERE Loan_ID = p_Loan_ID
      AND Return_Time IS NULL
      AND Student_ID = p_Student_ID;

    IF SQL%ROWCOUNT = 0 THEN
        p_Result_Code := 1;  -- 不存在/已归还/非本人
        RETURN;
    END IF;

    -- 2. 取借出详情（UPDATE 已确保本会话独占该行，无需 FOR UPDATE）
    SELECT Item_ID, Due_Time INTO v_Item_ID, v_Due_Time
    FROM D_Item_Loan WHERE Loan_ID = p_Loan_ID;

    -- 3. 归还：库存 +1
    UPDATE D_Shared_Item
    SET Available_Qty = Available_Qty + 1
    WHERE Item_ID = v_Item_ID;

    -- 4. 超期天数交给应用层判定（信用分扣分在统一入口完成，本 SP 不触碰信用分表）
    IF SYSDATE > v_Due_Time THEN
        p_Overdue_Days := TRUNC(SYSDATE - v_Due_Time);
    END IF;

    COMMIT;
END SP_Return_Item;
/

-- ============================================================
-- SP_Consume_Material：维修耗材出库
-- 返回：0=成功, 1=耗材不存在, 2=库存不足, 3=幂等键已使用且请求内容不一致
-- 并发语义与 SP_Borrow_Item 相同（SAVEPOINT + 唯一索引兜底 + 内容比对），
-- 比对内容为 Ticket_ID + Material_ID + Quantity。
-- ============================================================
CREATE OR REPLACE PROCEDURE SP_Consume_Material(
    p_Material_ID     IN  NUMBER,
    p_Ticket_ID       IN  NUMBER,
    p_Quantity        IN  NUMBER,
    p_Idempotency_Key IN  VARCHAR2,
    p_Result_Code     OUT NUMBER
) AS
    v_Stock       NUMBER;
    v_Ex_Ticket   NUMBER;
    v_Ex_Material NUMBER;
    v_Ex_Quantity NUMBER;
BEGIN
    p_Result_Code := 0;

    -- 0. 幂等快路径：同 Key 已处理过 → 比对完整请求内容
    IF p_Idempotency_Key IS NOT NULL THEN
        BEGIN
            SELECT Ticket_ID, Material_ID, Quantity
            INTO v_Ex_Ticket, v_Ex_Material, v_Ex_Quantity
            FROM D_Repair_Material_Usage
            WHERE Idempotency_Key = p_Idempotency_Key;

            IF v_Ex_Ticket = p_Ticket_ID
               AND v_Ex_Material = p_Material_ID
               AND v_Ex_Quantity = p_Quantity THEN
                p_Result_Code := 0;  -- 内容一致
            ELSE
                p_Result_Code := 3;  -- 同 Key 不同请求内容
            END IF;
            RETURN;
        EXCEPTION
            WHEN NO_DATA_FOUND THEN NULL;
        END;
    END IF;

    -- 1. 检查耗材库存（原子扣减）
    SAVEPOINT sp_consume;

    UPDATE D_Repair_Material
    SET Stock_Qty = Stock_Qty - p_Quantity
    WHERE Material_ID = p_Material_ID AND Stock_Qty >= p_Quantity;

    IF SQL%ROWCOUNT = 0 THEN
        -- 判断是不存在还是不够
        BEGIN
            SELECT Stock_Qty INTO v_Stock FROM D_Repair_Material WHERE Material_ID = p_Material_ID;
            p_Result_Code := 2;  -- 库存不足
        EXCEPTION
            WHEN NO_DATA_FOUND THEN p_Result_Code := 1;  -- 耗材不存在
        END;
        RETURN;
    END IF;

    -- 2. 记录消耗（唯一索引兜底并发重放）
    BEGIN
        INSERT INTO D_Repair_Material_Usage (
            Usage_ID, Ticket_ID, Material_ID, Quantity, Use_Time, Idempotency_Key
        ) VALUES (
            SEQ_MATERIAL_USAGE.NEXTVAL, p_Ticket_ID, p_Material_ID, p_Quantity, SYSDATE, p_Idempotency_Key
        );
    EXCEPTION
        WHEN DUP_VAL_ON_INDEX THEN
            IF p_Idempotency_Key IS NULL THEN RAISE; END IF;

            ROLLBACK TO sp_consume;

            SELECT Ticket_ID, Material_ID, Quantity
            INTO v_Ex_Ticket, v_Ex_Material, v_Ex_Quantity
            FROM D_Repair_Material_Usage
            WHERE Idempotency_Key = p_Idempotency_Key;

            IF v_Ex_Ticket = p_Ticket_ID
               AND v_Ex_Material = p_Material_ID
               AND v_Ex_Quantity = p_Quantity THEN
                p_Result_Code := 0;  -- 并发兄弟请求已成功
            ELSE
                p_Result_Code := 3;  -- 同 Key 不同请求内容
            END IF;
            COMMIT;
            RETURN;
    END;

    COMMIT;
END SP_Consume_Material;
/

-- ============================================================
-- SP_Check_Overdue：逾期巡检（提醒模式）
-- 三审 P1-1：巡检不再扣信用分。超期归还按 PRD 按次扣 2 分，
--   只在归还路径经信用分统一入口（ICreditService.DeductAsync）扣一次。
-- 本 SP 只做提醒：对每笔逾期未归还的借出，给借款人发系统通知，
--   同一笔借出同一天只提醒一次（按 Recipient + Title + Content 内嵌 Loan_ID 去重）。
-- 由此"归还 × 巡检并发"的扣分写方只有一个（归还路径），
--   加上 Event_Key 唯一约束，天然满足"只产生一次扣分且冻结通知不丢失"。
-- ============================================================
CREATE OR REPLACE PROCEDURE SP_Check_Overdue AS
    v_Account_ID NUMBER;
    v_Notif_ID   NUMBER;
    v_Cnt        NUMBER;
BEGIN
    FOR loan_rec IN (
        SELECT Loan_ID, Student_ID, Item_ID,
               TRUNC(SYSDATE - Due_Time) AS Overdue_Days
        FROM D_Item_Loan
        WHERE Return_Time IS NULL AND Due_Time < SYSDATE
    ) LOOP
        -- 1. 找借款人正常账户（无账户则跳过）
        BEGIN
            SELECT Account_ID INTO v_Account_ID
            FROM D_User_Account
            WHERE Student_ID = loan_rec.Student_ID
              AND Account_Status = '正常'
              AND ROWNUM = 1;
        EXCEPTION
            WHEN NO_DATA_FOUND THEN CONTINUE;
        END;

        -- 2. 当日该笔借出已提醒过 → 跳过（同日不重复打扰）
        SELECT COUNT(*) INTO v_Cnt
        FROM D_Notification
        WHERE Recipient_Account_ID = v_Account_ID
          AND Title = '共享物品逾期归还提醒'
          AND Content LIKE '%Loan_ID=' || loan_rec.Loan_ID || '，%'
          AND Create_Time >= TRUNC(SYSDATE);

        IF v_Cnt > 0 THEN CONTINUE; END IF;

        -- 3. 写提醒（D_Notification 无序列/触发器，ID 用 MAX+1）
        SELECT NVL(MAX(Notification_ID), 0) + 1 INTO v_Notif_ID FROM D_Notification;

        INSERT INTO D_Notification (
            Notification_ID, Recipient_Account_ID, Title, Content,
            Notification_Type, Create_Time
        ) VALUES (
            v_Notif_ID, v_Account_ID, '共享物品逾期归还提醒',
            '您借用的共享物品（Loan_ID=' || loan_rec.Loan_ID
            || '，Item_ID=' || loan_rec.Item_ID
            || '）已逾期 ' || loan_rec.Overdue_Days
            || ' 天未归还。请尽快归还；超期归还将按次扣除 2 分信用分。',
            '系统', SYSDATE
        );
    END LOOP;

    COMMIT;
END SP_Check_Overdue;
/
