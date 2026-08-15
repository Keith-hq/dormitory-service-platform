-- 钱包充值与人工缴费存储过程（难点②）v1.0
-- 依赖：D_Fee_Detail, D_Wallet_Account, D_Wallet_Log（基线 database/ddl/extensions/010_extension_tables.sql）；
--       SEQ_WALLET_LOG（由 sp_billing.sql 创建）
-- 执行顺序：本脚本必须在 sp_billing.sql 之后执行（复用其 SEQ_WALLET_LOG，不内联 DDL，
--   遵守数据拥有者边界）。
--
-- 事务约定：两个 SP 均【自 COMMIT】——单域原子（同 sp_billing.sql 的 SP_Auto_Deduct），
--   C# 调用层不包事务；业务失败一律返回 p_Result_Code，不 RAISE
--   （sla_dispatch 惯例：失败不是系统异常，是业务结果）。
--
-- 幂等与并发语义（写入难点②文档与 PR 描述）：
--   幂等键 = D_Wallet_Log.Idempotency_Key（UK_D_WALLET_LOG_KEY 唯一约束）。
--   1) 同 Key 顺序重试：幂等重放检查在任何业务校验之前，直接返回 rc=0。
--   2) 同 Key 并发双提交（线性化兜底）：两个会话同时通过重放检查后，
--      后提交者的流水 INSERT 撞 UK_D_WALLET_LOG_KEY（DUP_VAL_ON_INDEX），
--      ROLLBACK TO SAVEPOINT 撤销本会话的扣款/加款后返回 rc=0；
--      若后提交者扣款前余额已不足则返回 rc=3。
--      因此同 Key 重试 rc=0 或 rc=3 均为交易已终态，余额以钱包查询（STU-06）为准。

-- ============================================================
-- SP_Manual_Pay：人工缴费——学生主动缴单条账单明细
-- 参数：p_Detail_ID       明细 ID（D_Fee_Detail.Detail_ID）
--       p_Student_ID      缴费学生学号（C# 层已校验为当前登录学生）
--       p_Idempotency_Key 幂等键（与 C# Idempotency-Key 请求头同源，≤100 字符）
--       p_Result_Code OUT 结果码：
--         0 = 成功或幂等重放（同 Key 已成功过 / 并发同 Key 由对方先行提交）
--         1 = 明细不存在
--         2 = 明细非本人（Detail 的 Student_ID 与 p_Student_ID 不符）
--         3 = 余额不足（钱包行不存在同此——缴费不自动开户；并发同 Key 兜底也可能返回）
--         4 = 已缴或明细金额为 0（不同 Key 重复缴 / 无需缴；CK Amount>0 不允许 0 元流水）
--         5 = Idempotency-Key 已被其它交易类型占用
-- ============================================================
CREATE OR REPLACE PROCEDURE SP_Manual_Pay(
    p_Detail_ID       IN NUMBER,
    p_Student_ID      IN VARCHAR2,
    p_Idempotency_Key IN VARCHAR2,
    p_Result_Code     OUT NUMBER
) AS
    v_Type          VARCHAR2(20);
    v_DetailStudent VARCHAR2(20);
    v_IsPaid        VARCHAR2(10);
    v_Due           NUMBER(10,2);
    v_RowsUpdated   NUMBER;
BEGIN
    p_Result_Code := 0;

    -- 1) 幂等重放检查：必须先于 Is_Paid 检查——同 Key 重试直接返回成功，
    --    否则"已缴"状态下重试会误报 rc=4（重试语义必须是终态一致，不能变成新错误）
    BEGIN
        SELECT Transaction_Type INTO v_Type
          FROM D_Wallet_Log
         WHERE Idempotency_Key = p_Idempotency_Key;

        IF v_Type = '人工缴费' THEN
            RETURN;                       -- rc=0 幂等重放
        ELSE
            p_Result_Code := 5;           -- Key 被其它类型占用
            RETURN;
        END IF;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN NULL;     -- 无记录，继续正常流程
    END;

    -- 2) 明细校验：不存在 → rc=1；非本人 → rc=2
    BEGIN
        SELECT Student_ID, Is_Paid, Water_Share + Power_Share
          INTO v_DetailStudent, v_IsPaid, v_Due
          FROM D_Fee_Detail
         WHERE Detail_ID = p_Detail_ID;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            p_Result_Code := 1;
            RETURN;
    END;

    IF v_DetailStudent <> p_Student_ID THEN
        p_Result_Code := 2;
        RETURN;
    END IF;

    -- 3) 已缴（不同 Key 重复缴）或 0 元明细（无需缴）→ rc=4
    IF v_IsPaid = '是' OR v_Due <= 0 THEN
        p_Result_Code := 4;
        RETURN;
    END IF;

    -- 4) 原子扣款：WHERE Balance >= due 保证不会扣成负数；
    --    ROWCOUNT=0（余额不足 / 钱包行不存在 / 并发已扣）→ rc=3
    SAVEPOINT sp_pay;

    UPDATE D_Wallet_Account
       SET Balance = Balance - v_Due
     WHERE Student_ID = p_Student_ID
       AND Balance >= v_Due;

    v_RowsUpdated := SQL%ROWCOUNT;
    IF v_RowsUpdated = 0 THEN
        ROLLBACK TO sp_pay;
        p_Result_Code := 3;
        RETURN;
    END IF;

    -- 5) 写流水；并发同 Key 双提交撞 UK → 回滚本次扣款，按幂等重放返回 rc=0
    BEGIN
        INSERT INTO D_Wallet_Log (
            Log_ID, Student_ID, Amount, Transaction_Type,
            Before_Balance, After_Balance, Detail_ID, Idempotency_Key, Create_Time
        ) VALUES (
            SEQ_WALLET_LOG.NEXTVAL,
            p_Student_ID,
            v_Due,
            '人工缴费',
            (SELECT Balance + v_Due FROM D_Wallet_Account WHERE Student_ID = p_Student_ID),
            (SELECT Balance FROM D_Wallet_Account WHERE Student_ID = p_Student_ID),
            p_Detail_ID,
            p_Idempotency_Key,
            SYSDATE
        );
    EXCEPTION
        WHEN DUP_VAL_ON_INDEX THEN
            ROLLBACK TO sp_pay;           -- 撤销本会话扣款，对方会话已提交同 Key 交易
            RETURN;
    END;

    -- 6) 标记已缴
    UPDATE D_Fee_Detail
       SET Is_Paid = '是'
     WHERE Detail_ID = p_Detail_ID;

    COMMIT;
END SP_Manual_Pay;
/

-- ============================================================
-- SP_Recharge：钱包充值——余额不足时钱包行自动开户
-- 参数：p_Student_ID      充值学生学号（C# 层已校验为当前登录学生）
--       p_Amount          充值金额（>0）
--       p_Idempotency_Key 幂等键（同 SP_Manual_Pay）
--       p_Result_Code OUT 结果码：
--         0 = 成功或幂等重放（同 Key 已成功过 / 并发同 Key 由对方先行提交）
--         1 = 金额为空或 ≤0，或加款后超出余额上限 NUMBER(8,2)
--         2 = 幂等键已被其它交易类型占用
--         3 = 学生不存在（D_Student 无该学号，FK -2291；正常流程 C# 已拦截）
-- ============================================================
CREATE OR REPLACE PROCEDURE SP_Recharge(
    p_Student_ID      IN VARCHAR2,
    p_Amount          IN NUMBER,
    p_Idempotency_Key IN VARCHAR2,
    p_Result_Code     OUT NUMBER
) AS
    v_Type        VARCHAR2(20);
    v_RowsUpdated NUMBER;
BEGIN
    p_Result_Code := 0;

    -- 1) 幂等重放检查（顺序同 SP_Manual_Pay，必须在业务校验之前）
    BEGIN
        SELECT Transaction_Type INTO v_Type
          FROM D_Wallet_Log
         WHERE Idempotency_Key = p_Idempotency_Key;

        IF v_Type = '充值' THEN
            RETURN;
        ELSE
            p_Result_Code := 2;
            RETURN;
        END IF;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN NULL;
    END;

    -- 2) 金额校验
    IF p_Amount IS NULL OR p_Amount <= 0 THEN
        p_Result_Code := 1;
        RETURN;
    END IF;

    -- 3) 钱包自动开户；学生不存在 → FK -2291 → rc=3
    BEGIN
        MERGE INTO D_Wallet_Account t
        USING dual
           ON (t.Student_ID = p_Student_ID)
         WHEN NOT MATCHED THEN
              INSERT (Student_ID, Balance) VALUES (p_Student_ID, 0);
    EXCEPTION
        WHEN DUP_VAL_ON_INDEX THEN
            NULL;                         -- 并发首充：另一会话已开户，继续
        WHEN OTHERS THEN
            IF SQLCODE = -2291 THEN
                p_Result_Code := 3;       -- D_Student 无该学号
                RETURN;
            ELSE
                RAISE;
            END IF;
    END;

    SAVEPOINT sp_rc;

    -- 4) 加余额；超出 NUMBER(8,2) 上限（ORA-01438）与金额非法同码 rc=1
    BEGIN
        UPDATE D_Wallet_Account
           SET Balance = Balance + p_Amount
         WHERE Student_ID = p_Student_ID;
    EXCEPTION
        WHEN OTHERS THEN
            IF SQLCODE = -1438 THEN
                ROLLBACK TO sp_rc;
                p_Result_Code := 1;
                RETURN;
            ELSE
                RAISE;
            END IF;
    END;

    v_RowsUpdated := SQL%ROWCOUNT;
    IF v_RowsUpdated = 0 THEN
        ROLLBACK TO sp_rc;
        p_Result_Code := 3;
        RETURN;
    END IF;

    -- 5) 写流水；并发同 Key 双提交撞 UK → 回滚本次加款，按幂等重放返回 rc=0
    BEGIN
        INSERT INTO D_Wallet_Log (
            Log_ID, Student_ID, Amount, Transaction_Type,
            Before_Balance, After_Balance, Detail_ID, Idempotency_Key, Create_Time
        ) VALUES (
            SEQ_WALLET_LOG.NEXTVAL,
            p_Student_ID,
            p_Amount,
            '充值',
            (SELECT Balance - p_Amount FROM D_Wallet_Account WHERE Student_ID = p_Student_ID),
            (SELECT Balance FROM D_Wallet_Account WHERE Student_ID = p_Student_ID),
            NULL,
            p_Idempotency_Key,
            SYSDATE
        );
    EXCEPTION
        WHEN DUP_VAL_ON_INDEX THEN
            ROLLBACK TO sp_rc;            -- 撤销本会话加款，对方会话已提交同 Key 交易
            RETURN;
    END;

    COMMIT;
END SP_Recharge;
/
