-- 共享物品借还与维修耗材出库存储过程（难点④）
-- 依赖：D_Shared_Item, D_Item_Loan, D_Repair_Material, D_Repair_Material_Usage, D_Credit_Account, D_Credit_Log
-- 基线：database/ddl/extensions/010_extension_tables.sql
-- 裁决红线：D_Shared_Item 不复用为桶装水库存；三种库存不混用

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

DECLARE v_StartVal NUMBER;
BEGIN
    SELECT NVL(MAX(Log_ID), 0) + 1 INTO v_StartVal FROM D_Credit_Log;
    EXECUTE IMMEDIATE 'CREATE SEQUENCE SEQ_CREDIT_LOG START WITH ' || v_StartVal || ' INCREMENT BY 1';
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
-- SP_Borrow_Item：借出共享物品（P1-2: 支持 Idempotency-Key 幂等）
-- 返回：0=成功, 1=物品不存在, 2=物品停用, 3=库存不足, 4=信用分冻结
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
BEGIN
    p_Result_Code := 0;

    -- 0. 幂等检查：同一个 Idempotency-Key 已处理过 → 直接返回已有 Loan_ID
    IF p_Idempotency_Key IS NOT NULL THEN
        BEGIN
            SELECT Loan_ID INTO p_Loan_ID
            FROM D_Item_Loan
            WHERE Idempotency_Key = p_Idempotency_Key;
            p_Result_Code := 0;
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

    -- 2. 信用分检查
    BEGIN
        SELECT Current_Score INTO v_Credit_Score
        FROM D_Credit_Account WHERE Student_ID = p_Student_ID;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN p_Result_Code := 4; RETURN;
    END;

    IF v_Credit_Score < 60 THEN p_Result_Code := 4; RETURN; END IF;
    -- 阈值 60 为信用分冻结线，与 D_Credit_Account 业务规则对齐；修改时两处需同步

    -- 3. 原子扣减库存 + 写借出记录
    UPDATE D_Shared_Item
    SET Available_Qty = Available_Qty - 1
    WHERE Item_ID = p_Item_ID AND Available_Qty > 0;

    IF SQL%ROWCOUNT = 0 THEN p_Result_Code := 3; RETURN; END IF;

    INSERT INTO D_Item_Loan (
        Loan_ID, Item_ID, Student_ID, Borrow_Time, Due_Time, Idempotency_Key
    ) VALUES (
        SEQ_ITEM_LOAN.NEXTVAL, p_Item_ID, p_Student_ID, SYSDATE, SYSDATE + 1, p_Idempotency_Key
    ) RETURNING Loan_ID INTO p_Loan_ID;

    COMMIT;
END SP_Borrow_Item;
/

-- ============================================================
-- SP_Return_Item：归还共享物品（原子 + 超期扣分 + P1-3: 校验 Student_ID）
-- 返回：0=成功, 1=借出记录不存在/状态不对/非本人
-- ============================================================
CREATE OR REPLACE PROCEDURE SP_Return_Item(
    p_Loan_ID     IN  NUMBER,
    p_Student_ID  IN  VARCHAR2,
    p_Result_Code OUT NUMBER
) AS
    v_Item_ID          NUMBER;
    v_Due_Time         DATE;
    v_Overdue_Days     NUMBER;
    v_Penalty          NUMBER;
    v_Score            NUMBER;
    v_Already_Deducted NUMBER;
BEGIN
    p_Result_Code := 0;

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

    -- 4. 超期判定与信用分扣分
    IF SYSDATE > v_Due_Time THEN
        v_Overdue_Days := TRUNC(SYSDATE - v_Due_Time);

        BEGIN
            SELECT Current_Score INTO v_Score
            FROM D_Credit_Account WHERE Student_ID = p_Student_ID;

            -- 目标总罚分 = 逾期天数 × 5
            v_Penalty := v_Overdue_Days * 5;

            -- 查询巡检(SP_Check_Overdue)已扣分数，只补扣差额，防止双路径叠加
            -- 同时算入本次归还可能已写入的流水（Event_Key = 'OVERDUE-' || Loan_ID）
            SELECT NVL(SUM(ABS(Score_Change)), 0) INTO v_Already_Deducted
            FROM D_Credit_Log
            WHERE Student_ID = p_Student_ID
              AND (Event_Key LIKE 'OVERDUE-SCAN-' || p_Loan_ID || '-%'
                   OR Event_Key = 'OVERDUE-' || p_Loan_ID);

            v_Penalty := GREATEST(0, v_Penalty - v_Already_Deducted);
            v_Penalty := LEAST(v_Score, v_Penalty);  -- 不能扣超过当前分数

            IF v_Penalty > 0 THEN
                UPDATE D_Credit_Account
                SET Current_Score = GREATEST(0, Current_Score - v_Penalty)
                WHERE Student_ID = p_Student_ID;

                INSERT INTO D_Credit_Log (
                    Log_ID, Student_ID, Score_Change, Reason, Event_Key, Create_Time
                ) VALUES (
                    SEQ_CREDIT_LOG.NEXTVAL, p_Student_ID, -v_Penalty,
                    '共享物品超期归还（Loan_ID=' || p_Loan_ID || ',逾期' || v_Overdue_Days || '天'
                    || ',巡检已扣' || v_Already_Deducted || ',补扣' || v_Penalty || '分）',
                    'OVERDUE-' || p_Loan_ID,
                    SYSDATE
                );
            END IF;
        EXCEPTION
            WHEN NO_DATA_FOUND THEN NULL;     -- 无信用账户则跳过
            WHEN DUP_VAL_ON_INDEX THEN NULL;  -- 幂等：同一次归还不重复写日志
        END;
    END IF;

    COMMIT;
END SP_Return_Item;
/

-- ============================================================
-- SP_Consume_Material：维修耗材出库（P2-1: 支持 Idempotency-Key 幂等）
-- 返回：0=成功, 1=耗材不存在, 2=库存不足
-- ============================================================
CREATE OR REPLACE PROCEDURE SP_Consume_Material(
    p_Material_ID     IN  NUMBER,
    p_Ticket_ID       IN  NUMBER,
    p_Quantity        IN  NUMBER,
    p_Idempotency_Key IN  VARCHAR2,
    p_Result_Code     OUT NUMBER
) AS
    v_Stock  NUMBER;
    v_Dummy  NUMBER;
BEGIN
    p_Result_Code := 0;

    -- 0. 幂等检查
    IF p_Idempotency_Key IS NOT NULL THEN
        BEGIN
            SELECT 1 INTO v_Dummy
            FROM D_Repair_Material_Usage
            WHERE Idempotency_Key = p_Idempotency_Key;
            p_Result_Code := 0;
            RETURN;
        EXCEPTION
            WHEN NO_DATA_FOUND THEN NULL;
        END;
    END IF;

    -- 1. 检查耗材库存（原子扣减）
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

    -- 2. 记录消耗
    INSERT INTO D_Repair_Material_Usage (
        Usage_ID, Ticket_ID, Material_ID, Quantity, Use_Time, Idempotency_Key
    ) VALUES (
        SEQ_MATERIAL_USAGE.NEXTVAL, p_Ticket_ID, p_Material_ID, p_Quantity, SYSDATE, p_Idempotency_Key
    );

    COMMIT;
END SP_Consume_Material;
/

-- ============================================================
-- SP_Check_Overdue：逾期巡检——每15分钟扫未归还+超期→差量扣分
-- P1-1: 跨日巡检只扣差额（目标总罚分 - 已扣），消除累计重复
-- P2-2: 精确异常处理，不再吞掉未知系统错误
-- ============================================================
CREATE OR REPLACE PROCEDURE SP_Check_Overdue AS
    v_Target           NUMBER;
    v_Already_Deducted NUMBER;
    v_Penalty          NUMBER;
    v_Actual_Deduct    NUMBER;
    v_Score            NUMBER;
    v_Log_Id           NUMBER;
BEGIN
    FOR loan_rec IN (
        SELECT Loan_ID, Student_ID, Item_ID,
               TRUNC(SYSDATE - Due_Time) AS Overdue_Days
        FROM D_Item_Loan
        WHERE Return_Time IS NULL AND Due_Time < SYSDATE
    ) LOOP
        -- 目标总罚分 = 逾期天数 × 5，单次上限 100
        v_Target := LEAST(5 * loan_rec.Overdue_Days, 100);

        -- 查询巡检路径已扣分数（含所有历史扫描 + 归还补扣）
        SELECT NVL(SUM(ABS(Score_Change)), 0) INTO v_Already_Deducted
        FROM D_Credit_Log
        WHERE Student_ID = loan_rec.Student_ID
          AND (Event_Key LIKE 'OVERDUE-SCAN-' || loan_rec.Loan_ID || '-%'
               OR Event_Key = 'OVERDUE-' || loan_rec.Loan_ID);

        -- 差额 = 目标 - 已扣，只扣新增部分
        v_Penalty := GREATEST(0, v_Target - v_Already_Deducted);

        IF v_Penalty <= 0 THEN CONTINUE; END IF;

        -- 查询当前信用分，确保日志与实扣一致
        BEGIN
            SELECT Current_Score INTO v_Score
            FROM D_Credit_Account
            WHERE Student_ID = loan_rec.Student_ID;
        EXCEPTION
            WHEN NO_DATA_FOUND THEN CONTINUE;  -- 无信用账户，跳过
        END;

        IF v_Score <= 0 THEN CONTINUE; END IF;

        v_Actual_Deduct := LEAST(v_Penalty, v_Score);

        -- INSERT 在前作为幂等守门员：Event_Key 按 Loan_ID + 日期去重，同日不重复扣
        BEGIN
            SELECT SEQ_CREDIT_LOG.NEXTVAL INTO v_Log_Id FROM DUAL;

            INSERT INTO D_Credit_Log (
                Log_ID, Student_ID, Score_Change, Reason, Event_Key, Create_Time
            ) VALUES (
                v_Log_Id, loan_rec.Student_ID, -v_Actual_Deduct,
                '共享物品逾期未还（Loan_ID=' || loan_rec.Loan_ID
                || ',超' || loan_rec.Overdue_Days || '天'
                || ',目标' || v_Target || ',已扣' || v_Already_Deducted
                || ',本次扣' || v_Actual_Deduct || '分）',
                'OVERDUE-SCAN-' || loan_rec.Loan_ID || '-' || TO_CHAR(SYSDATE, 'YYYYMMDD'),
                SYSDATE
            );

            UPDATE D_Credit_Account
            SET Current_Score = GREATEST(0, Current_Score - v_Actual_Deduct)
            WHERE Student_ID = loan_rec.Student_ID;
        EXCEPTION
            WHEN DUP_VAL_ON_INDEX THEN NULL;  -- 今天已处理，跳过
        END;
    END LOOP;

    COMMIT;
END SP_Check_Overdue;
/
