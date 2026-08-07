-- 账单划扣与断电判定存储过程（难点②）
-- 依赖：D_Fee_Detail, D_Wallet_Account, D_Wallet_Log, D_Fee_Deduction_Attempt, D_Room
-- 基线：database/ddl/extensions/010_extension_tables.sql

-- ============================================================
-- SP_Auto_Deduct：自动扣款——每月1/2/3日定时调用
-- 参数：p_AttemptNo  扣款尝试次数（1、2、3）
--       p_YearMonth  账单月份（如 '2026-08'）
-- 逻辑：
--   遍历当月所有 Is_Paid='否' 的个人分摊
--   余额够 → 扣款、Is_Paid='是'、写 D_Wallet_Log（Transaction_Type='自动扣款'）
--   余额不足 → 不扣款、写 D_Fee_Deduction_Attempt（Result='余额不足'）
--   同一账单同一尝试次数幂等：UK_D_FEE_DED_ATT(Detail_ID, Attempt_No) 兜底
-- ============================================================
CREATE OR REPLACE PROCEDURE SP_Auto_Deduct(
    p_AttemptNo IN NUMBER,
    p_YearMonth IN VARCHAR2
) AS
    v_Balance NUMBER(8,2);
    v_TotalDue NUMBER(8,2);
    v_SkipCount NUMBER := 0;
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
          AND fd.Is_Paid = '否'
    ) LOOP
        v_TotalDue := fee_rec.Total_Share;

        -- 读取当前余额
        SELECT Balance INTO v_Balance
        FROM D_Wallet_Account
        WHERE Student_ID = fee_rec.Student_ID;

        IF v_Balance >= v_TotalDue THEN
            -- 余额充足：扣款
            UPDATE D_Wallet_Account
            SET Balance = Balance - v_TotalDue
            WHERE Student_ID = fee_rec.Student_ID;

            -- 写钱包流水（Idempotency_Key 保证同一账单+月份不重复扣）
            BEGIN
                INSERT INTO D_Wallet_Log (
                    Log_ID, Student_ID, Amount, Transaction_Type,
                    Before_Balance, After_Balance, Detail_ID,
                    Idempotency_Key, Create_Time
                ) VALUES (
                    SEQ_FEE_DETAIL.NEXTVAL,
                    fee_rec.Student_ID,
                    v_TotalDue,
                    '自动扣款',
                    v_Balance,
                    v_Balance - v_TotalDue,
                    fee_rec.Detail_ID,
                    v_KeyPrefix || fee_rec.Detail_ID,
                    SYSDATE
                );
            EXCEPTION
                WHEN DUP_VAL_ON_INDEX THEN
                    v_SkipCount := v_SkipCount + 1;
                    CONTINUE;
            END;

            -- 标记账单已缴
            UPDATE D_Fee_Detail
            SET Is_Paid = '是'
            WHERE Detail_ID = fee_rec.Detail_ID;

            -- 记录扣款尝试（成功）
            BEGIN
                INSERT INTO D_Fee_Deduction_Attempt (
                    Attempt_ID, Detail_ID, Attempt_No, Attempt_Time, Result
                ) VALUES (
                    SEQ_FEE_DETAIL.NEXTVAL,
                    fee_rec.Detail_ID,
                    p_AttemptNo,
                    SYSDATE,
                    '成功'
                );
            EXCEPTION
                WHEN DUP_VAL_ON_INDEX THEN NULL;
            END;

        ELSE
            -- 余额不足：不扣款，只记录尝试
            BEGIN
                INSERT INTO D_Fee_Deduction_Attempt (
                    Attempt_ID, Detail_ID, Attempt_No, Attempt_Time, Result
                ) VALUES (
                    SEQ_FEE_DETAIL.NEXTVAL,
                    fee_rec.Detail_ID,
                    p_AttemptNo,
                    SYSDATE,
                    '余额不足'
                );
            EXCEPTION
                WHEN DUP_VAL_ON_INDEX THEN NULL;
            END;

        END IF;
    END LOOP;

    COMMIT;
END SP_Auto_Deduct;
/

-- ============================================================
-- SP_Check_Power_Cut：断电判定——每月3日第三次扣款后调用
-- 逻辑：
--   扫描当月存在 Power_Share 未缴账单的房间
--   若该房间本月第3次尝试仍有"余额不足" → 断电
--   只看电费（Power_Share），水费欠缴不触发断电
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
-- 逻辑：
--   扫描所有 Power_Status='断电' 的房间
--   若该房间本月全部电费账单 Is_Paid='是' → 恢复供电
--   仅看电费：水费不影响供电状态
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
