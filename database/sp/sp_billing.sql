-- 账单划扣与断电判定存储过程（难点②）v1.3
-- 依赖：D_Fee_Detail, D_Wallet_Account, D_Wallet_Log, D_Fee_Deduction_Attempt, D_Room
-- 基线：database/ddl/extensions/010_extension_tables.sql
-- 执行顺序：本脚本必须在 sp_fee_sharing.sql（创建 SEQ_FEE_DETAIL）之后执行；
--   sp_wallet.sql 在本脚本之后执行（复用 SEQ_WALLET_LOG）
-- v1.2（缴费/钱包落地，2026-08-15）：修复 SP_Auto_Deduct 与人工缴费的竞态——
--   游标读到 Is_Paid='否' 后若人工缴费先完成（扣款+标记'是'+提交），原子 UPDATE
--   仍会命中同一明细 → 双重扣款（AUTO Key 与人工 Key 不同，不撞 UK）。
--   修复：原子 UPDATE 的 WHERE 追加 Is_Paid='否' 子查询复查，与人工缴费在
--   D_Fee_Detail 行锁上线性化——人工缴费先提交者胜，自动扣款跳过该明细。
-- v1.3（PR #58 评审整改，2026-08-15）：双重扣款防线升级为"原子认领"——
--   先认领明细（UPDATE Is_Paid='否'→'是'，SQL%ROWCOUNT 裁决）再扣钱包，
--   与 SP_Manual_Pay v1.1 同锁序（明细→钱包），两 SP 并发无交叉锁死锁；
--   余额不足时回滚认领保持明细未缴；并发他人已缴则静默跳过（不再误记
--   '余额不足'尝试，v1.2 该分支语义不准）。

-- ============================================================
-- 创建专用序列——替代 SEQ_FEE_DETAIL，语义独立
-- ============================================================
DECLARE
    v_StartVal NUMBER;
BEGIN
    SELECT NVL(MAX(Log_ID), 0) + 1 INTO v_StartVal FROM D_Wallet_Log;
    EXECUTE IMMEDIATE 'CREATE SEQUENCE SEQ_WALLET_LOG START WITH ' || v_StartVal || ' INCREMENT BY 1';
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE = -955 THEN NULL; ELSE RAISE; END IF;
END;
/

DECLARE
    v_StartVal NUMBER;
BEGIN
    SELECT NVL(MAX(Attempt_ID), 0) + 1 INTO v_StartVal FROM D_Fee_Deduction_Attempt;
    EXECUTE IMMEDIATE 'CREATE SEQUENCE SEQ_FEE_DED_ATT START WITH ' || v_StartVal || ' INCREMENT BY 1';
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE = -955 THEN NULL; ELSE RAISE; END IF;
END;
/

-- ============================================================
-- SP_Auto_Deduct：自动扣款——每月1/2/3日定时调用
-- 参数：p_AttemptNo  扣款尝试次数（1、2、3）
--       p_YearMonth  账单月份（如 '2026-08'）
-- v1.1：原子扣款（UPDATE WHERE Balance>=due）、过滤0元账单、
--       过滤 Publish_Status='已发布'、重跑时更新旧 Attempt 状态
-- v1.2：原子 UPDATE 追加 Is_Paid='否' 子查询复查，修复与人工缴费的
--       双重扣款竞态（详见文件头 v1.2 修订说明）
-- v1.3：改为"原子认领"模式（先认领明细再扣钱包，SQL%ROWCOUNT 裁决，
--       与 SP_Manual_Pay v1.1 同锁序，详见文件头 v1.3 修订说明）
-- ============================================================
CREATE OR REPLACE PROCEDURE SP_Auto_Deduct(
    p_AttemptNo IN NUMBER,
    p_YearMonth IN VARCHAR2
) AS
    v_TotalDue  NUMBER(10,2);
    v_RowsUpdated NUMBER;
    v_KeyPrefix VARCHAR2(50);
BEGIN
    v_KeyPrefix := 'AUTO-' || p_YearMonth || '-';

    FOR fee_rec IN (
        SELECT fd.Detail_ID, fd.Student_ID, fd.Room_ID,
               fd.Water_Share, fd.Power_Share,
               (fd.Water_Share + fd.Power_Share) AS Total_Share
        FROM D_Fee_Detail fd
        JOIN D_Utility_Fee uf ON fd.Fee_ID = uf.Fee_ID
        WHERE uf.Year_Month = p_YearMonth
          AND uf.Publish_Status = '已发布'
          AND fd.Is_Paid = '否'
          AND (fd.Water_Share + fd.Power_Share) > 0     -- 排除0元账单
    ) LOOP
        v_TotalDue := fee_rec.Total_Share;

        SAVEPOINT sp_det;

        -- 1) 原子认领（v1.3）：与 SP_Manual_Pay v1.1 在 D_Fee_Detail 行锁上
        --    线性化——游标读取后若人工缴费/另一批 AUTO 抢先完成，本 UPDATE
        --    ROWCOUNT=0，静默跳过（明细已缴，无需扣款、不记尝试；v1.2 的
        --    '余额不足'误记分支随之移除）
        UPDATE D_Fee_Detail
           SET Is_Paid = '是'
         WHERE Detail_ID = fee_rec.Detail_ID
           AND Is_Paid = '否';

        v_RowsUpdated := SQL%ROWCOUNT;
        IF v_RowsUpdated = 0 THEN
            CONTINUE;                     -- 已被他人缴，跳过
        END IF;

        -- 2) 原子扣款：WHERE Balance >= due 保证不会扣成负数；
        --    余额不足/钱包行不存在 → 回滚认领（明细保持未缴，下轮重试），
        --    只记'余额不足'尝试
        UPDATE D_Wallet_Account
        SET Balance = Balance - v_TotalDue
        WHERE Student_ID = fee_rec.Student_ID
          AND Balance >= v_TotalDue;

        v_RowsUpdated := SQL%ROWCOUNT;

        IF v_RowsUpdated = 0 THEN
            ROLLBACK TO sp_det;           -- 撤销认领

            BEGIN
                INSERT INTO D_Fee_Deduction_Attempt (
                    Attempt_ID, Detail_ID, Attempt_No, Attempt_Time, Result
                ) VALUES (
                    SEQ_FEE_DED_ATT.NEXTVAL,
                    fee_rec.Detail_ID,
                    p_AttemptNo,
                    SYSDATE,
                    '余额不足'
                );
            EXCEPTION
                WHEN DUP_VAL_ON_INDEX THEN
                    UPDATE D_Fee_Deduction_Attempt
                    SET Result = '余额不足', Attempt_Time = SYSDATE
                    WHERE Detail_ID = fee_rec.Detail_ID
                      AND Attempt_No = p_AttemptNo;
            END;

            CONTINUE;
        END IF;

        -- 3) 扣款成功：写流水
        BEGIN
            INSERT INTO D_Wallet_Log (
                Log_ID, Student_ID, Amount, Transaction_Type,
                Before_Balance, After_Balance, Detail_ID,
                Idempotency_Key, Create_Time
            ) VALUES (
                SEQ_WALLET_LOG.NEXTVAL,
                fee_rec.Student_ID,
                v_TotalDue,
                '自动扣款',
                (SELECT Balance + v_TotalDue FROM D_Wallet_Account WHERE Student_ID = fee_rec.Student_ID),
                (SELECT Balance FROM D_Wallet_Account WHERE Student_ID = fee_rec.Student_ID),
                fee_rec.Detail_ID,
                v_KeyPrefix || fee_rec.Detail_ID,
                SYSDATE
            );
        EXCEPTION
            WHEN DUP_VAL_ON_INDEX THEN
                NULL;  -- 已扣过（重跑幂等），跳过流水
        END;

        -- 4) 记录/更新 Attempt（重跑时覆盖旧状态）
        BEGIN
            INSERT INTO D_Fee_Deduction_Attempt (
                Attempt_ID, Detail_ID, Attempt_No, Attempt_Time, Result
            ) VALUES (
                SEQ_FEE_DED_ATT.NEXTVAL,
                fee_rec.Detail_ID,
                p_AttemptNo,
                SYSDATE,
                '成功'
            );
        EXCEPTION
            WHEN DUP_VAL_ON_INDEX THEN
                UPDATE D_Fee_Deduction_Attempt
                SET Result = '成功', Attempt_Time = SYSDATE
                WHERE Detail_ID = fee_rec.Detail_ID
                  AND Attempt_No = p_AttemptNo;
        END;
    END LOOP;

    COMMIT;
END SP_Auto_Deduct;
/

-- ============================================================
-- SP_Check_Power_Cut：断电判定——每月3日第三次扣款后调用
-- v1.1：增加 Publish_Status = '已发布' 过滤
-- ============================================================
CREATE OR REPLACE PROCEDURE SP_Check_Power_Cut(
    p_YearMonth IN VARCHAR2
) AS
BEGIN
    FOR room_rec IN (
        SELECT DISTINCT fd.Room_ID
        FROM D_Fee_Detail fd
        JOIN D_Utility_Fee uf ON fd.Fee_ID = uf.Fee_ID
        WHERE uf.Year_Month = p_YearMonth
          AND uf.Publish_Status = '已发布'
          AND fd.Is_Paid = '否'
          AND fd.Power_Share > 0
          AND EXISTS (
              SELECT 1 FROM D_Fee_Deduction_Attempt da
              WHERE da.Detail_ID = fd.Detail_ID
                AND da.Attempt_No = 3
                AND da.Result = '余额不足'
          )
    ) LOOP
        UPDATE D_Room
        SET Power_Status = '断电'
        WHERE Room_ID = room_rec.Room_ID;
    END LOOP;

    COMMIT;
END SP_Check_Power_Cut;
/

-- ============================================================
-- SP_Restore_Power：恢复供电巡检——每分钟执行
-- v1.1：NOT EXISTS 子查询增加 Publish_Status = '已发布'
-- ============================================================
CREATE OR REPLACE PROCEDURE SP_Restore_Power AS
    v_Month VARCHAR2(10);
BEGIN
    v_Month := TO_CHAR(SYSDATE, 'YYYY-MM');

    FOR room_rec IN (
        SELECT DISTINCT r.Room_ID
        FROM D_Room r
        WHERE r.Power_Status = '断电'
          AND NOT EXISTS (
              SELECT 1
              FROM D_Fee_Detail fd
              JOIN D_Utility_Fee uf ON fd.Fee_ID = uf.Fee_ID
              WHERE fd.Room_ID = r.Room_ID
                AND uf.Year_Month = v_Month
                AND uf.Publish_Status = '已发布'
                AND fd.Is_Paid = '否'
                AND fd.Power_Share > 0
          )
    ) LOOP
        UPDATE D_Room
        SET Power_Status = '正常'
        WHERE Room_ID = room_rec.Room_ID;
    END LOOP;

    COMMIT;
END SP_Restore_Power;
/
