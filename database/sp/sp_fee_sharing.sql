-- 水电分摊存储过程（难点①）v1.3
-- 依赖：D_Utility_Fee, D_Bed_Allocation, D_Fee_Detail, D_Room
-- 基线：database/ddl/extensions/010_extension_tables.sql（UK_D_FEE_DETAIL）
-- v1.3（难点⑥ 一审 R2，2026-08-14）：唯一索引 UK_D_FEE_DETAIL 收紧为
--   (Fee_ID, Student_ID)（迁移 database/ddl/extensions/022_fee_detail_dedup_uk.sql）。
--   两入口互斥（月度/退宿只取其一）改由 DB 唯一性兜底，本文件两个 SP 的
--   DUP_VAL_ON_INDEX 静默跳过同时成为并发竞态（IT-C10-004）的安全网。

-- ============================================================
-- 创建主键序列——替代 MAX+1，避免并发撞 PK
-- 迁移注意：若 D_Fee_Detail 已有数据，起点自动取 MAX(Detail_ID)+1
-- ============================================================
DECLARE
    v_StartVal NUMBER;
BEGIN
    SELECT NVL(MAX(Detail_ID), 0) + 1 INTO v_StartVal FROM D_Fee_Detail;
    EXECUTE IMMEDIATE 'CREATE SEQUENCE SEQ_FEE_DETAIL START WITH ' || v_StartVal || ' INCREMENT BY 1';
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE = -955 THEN NULL;   -- 序列已存在，跳过
        ELSE RAISE;                     -- 其他异常（权限不足等）必须暴露
        END IF;
END;
/

-- ============================================================
-- SP_Calc_Monthly_Fee：每月1日调用，批量生成全部个人分摊
-- 参数：p_YearMonth 格式 'YYYY-MM'（如 '2026-08'）
-- v1.2（难点⑥ 分工对齐）：两入口防重——该笔费用若已有 '退宿' 明细
--     （退宿 SP 已结算），月度不再重复生成（IT-C10-004）
-- v1.3（一审 R2）：UK 收紧为 (Fee_ID, Student_ID) 后，COUNT 预检查是快路径，
--     预检查与 INSERT 之间的并发窗口由 UK 唯一性兜底（DUP_VAL_ON_INDEX 跳过）
-- ============================================================
CREATE OR REPLACE PROCEDURE SP_Calc_Monthly_Fee(
    p_YearMonth IN VARCHAR2
) AS
    v_TotalDays NUMBER := 0;
    v_MonthStart DATE;
    v_MonthEnd   DATE;
    v_DupCount   NUMBER := 0;   -- 重复跳过计数
    v_CheckoutCnt NUMBER := 0;
BEGIN
    v_MonthStart := TO_DATE(p_YearMonth || '-01', 'YYYY-MM-DD');
    v_MonthEnd   := LAST_DAY(v_MonthStart);

    FOR fee_rec IN (
        SELECT * FROM D_Utility_Fee
        WHERE Year_Month = p_YearMonth AND Publish_Status = '已发布'
    ) LOOP

        SELECT SUM(
            TRUNC(
                LEAST(v_MonthEnd, NVL(CheckOut_Date, v_MonthEnd))
                -
                GREATEST(v_MonthStart, CheckIn_Date)
            ) + 1
        ) INTO v_TotalDays
        FROM D_Bed_Allocation
        WHERE Room_ID = fee_rec.Room_ID
          AND CheckIn_Date <= v_MonthEnd
          AND (CheckOut_Date IS NULL OR CheckOut_Date >= v_MonthStart);

        IF v_TotalDays IS NULL OR v_TotalDays = 0 THEN
            CONTINUE;
        END IF;

        FOR student_rec IN (
            SELECT Student_ID,
                   TRUNC(
                       LEAST(v_MonthEnd, NVL(CheckOut_Date, v_MonthEnd))
                       -
                       GREATEST(v_MonthStart, CheckIn_Date)
                   ) + 1 AS Stay_Days
            FROM D_Bed_Allocation
            WHERE Room_ID = fee_rec.Room_ID
              AND CheckIn_Date <= v_MonthEnd
              AND (CheckOut_Date IS NULL OR CheckOut_Date >= v_MonthStart)
        ) LOOP

            -- 两入口防重：该笔费用若已由退宿 SP 结算，月度不再重复生成
            SELECT COUNT(*) INTO v_CheckoutCnt
            FROM D_Fee_Detail
            WHERE Fee_ID = fee_rec.Fee_ID
              AND Student_ID = student_rec.Student_ID
              AND Bill_Type = '退宿';

            IF v_CheckoutCnt = 0 THEN
                -- 捕获 UK 冲突：重复执行时跳过已存在的分摊记录；
                -- v1.3 UK(Fee_ID, Student_ID) 收紧后，此处同时兜住
                -- COUNT 与 INSERT 之间被退宿 SP 抢先结算的并发窗口
                BEGIN
                    INSERT INTO D_Fee_Detail (
                        Detail_ID, Fee_ID, Student_ID, Room_ID,
                        Water_Share, Power_Share, Stay_Days, Total_Days,
                        Bill_Type, Is_Paid, Create_Time
                    ) VALUES (
                        SEQ_FEE_DETAIL.NEXTVAL,
                        fee_rec.Fee_ID,
                        student_rec.Student_ID,
                        fee_rec.Room_ID,
                        ROUND(fee_rec.Water_Fee * student_rec.Stay_Days / v_TotalDays, 2),
                        ROUND(fee_rec.Power_Fee * student_rec.Stay_Days / v_TotalDays, 2),
                        student_rec.Stay_Days,
                        v_TotalDays,
                        '月度',
                        '否',
                        SYSDATE
                    );
                EXCEPTION
                    WHEN DUP_VAL_ON_INDEX THEN
                        v_DupCount := v_DupCount + 1;  -- 已有记录，静默跳过
                END;
            END IF;

        END LOOP;
    END LOOP;

    COMMIT;
END SP_Calc_Monthly_Fee;
/

-- ============================================================
-- SP_Calc_Checkout_Fee：退宿时调用，为退宿学生结算当月分摊
-- 参数：p_Student_ID   退宿学生学号
--       p_Allocation_ID 对应的住宿分配记录ID
-- v1.1：v_MyDays 改从 CheckOut_Date 计算，不再用 SYSDATE
-- v1.2（难点⑥ 分工对齐方案，组长+刘润东已确认）：
--   1) 调用约定：settle 必须先写 CheckOut_Date 再调用本过程（SYSDATE 仅作防御兜底）
--   2) 事务约定：过程内不 COMMIT，由调用方事务统一提交（床位/房间/费用原子性）
--   3) 口径对齐：只结算 Publish_Status='已发布' 的费用（与月度 SP 一致）
--   4) 两入口防重：该笔费用若已有 '月度' 明细，退宿不再重复生成（IT-C10-004）
-- v1.3（一审 R2）：UK 收紧为 (Fee_ID, Student_ID) 后，COUNT 预检查是快路径，
--     预检查与 INSERT 之间的并发窗口由 UK 唯一性兜底（DUP_VAL_ON_INDEX 跳过）
-- ============================================================
CREATE OR REPLACE PROCEDURE SP_Calc_Checkout_Fee(
    p_Student_ID    IN VARCHAR2,
    p_Allocation_ID IN NUMBER
) AS
    v_TotalDays    NUMBER := 0;
    v_MyDays       NUMBER := 0;
    v_MonthStart   DATE;
    v_MonthEnd     DATE;
    v_RoomID       NUMBER;
    v_YearMonth    VARCHAR2(10);
    v_CheckoutDate DATE;
    v_MonthlyCnt   NUMBER := 0;
BEGIN
    -- 从退宿记录中读取实际退宿日期（而非 SYSDATE）
    SELECT Room_ID, CheckOut_Date INTO v_RoomID, v_CheckoutDate
    FROM D_Bed_Allocation
    WHERE Allocation_ID = p_Allocation_ID
      AND Student_ID = p_Student_ID;

    -- 分工对齐约定：settle 先写 CheckOut_Date 再调用；
    -- 未写入则回退 SYSDATE（防御分支，正常流程不触发）
    IF v_CheckoutDate IS NULL THEN
        v_CheckoutDate := SYSDATE;
    END IF;

    v_YearMonth := TO_CHAR(v_CheckoutDate, 'YYYY-MM');
    v_MonthStart := TO_DATE(v_YearMonth || '-01', 'YYYY-MM-DD');
    v_MonthEnd   := LAST_DAY(v_MonthStart);

    -- 用实际退宿日期计算当月入住天数
    v_MyDays := TRUNC(v_CheckoutDate) - v_MonthStart + 1;

    -- 重新计算该房间当月总人天数（含退宿学生）
    SELECT SUM(
        TRUNC(
            LEAST(v_MonthEnd, NVL(CheckOut_Date, v_MonthEnd))
            -
            GREATEST(v_MonthStart, CheckIn_Date)
        ) + 1
    ) INTO v_TotalDays
    FROM D_Bed_Allocation
    WHERE Room_ID = v_RoomID
      AND CheckIn_Date <= v_MonthEnd
      AND (CheckOut_Date IS NULL OR CheckOut_Date >= v_MonthStart);

    FOR fee_rec IN (
        SELECT * FROM D_Utility_Fee
        WHERE Room_ID = v_RoomID
          AND Year_Month = v_YearMonth
          AND Publish_Status = '已发布'
    ) LOOP
        -- 两入口防重：该笔费用若已有 '月度' 明细（月度 SP 已结算），退宿不再重复生成
        SELECT COUNT(*) INTO v_MonthlyCnt
        FROM D_Fee_Detail
        WHERE Fee_ID = fee_rec.Fee_ID
          AND Student_ID = p_Student_ID
          AND Bill_Type = '月度';

        IF v_MonthlyCnt = 0 THEN
            -- 捕获 UK 冲突：同一退宿流程重复执行时跳过（与月度 SP 一致的幂等策略）；
            -- v1.3 UK(Fee_ID, Student_ID) 收紧后，此处同时兜住
            -- COUNT 与 INSERT 之间被月度 SP 抢先结算的并发窗口
            BEGIN
                INSERT INTO D_Fee_Detail (
                    Detail_ID, Fee_ID, Student_ID, Room_ID,
                    Water_Share, Power_Share, Stay_Days, Total_Days,
                    Bill_Type, Is_Paid, Create_Time
                ) VALUES (
                    SEQ_FEE_DETAIL.NEXTVAL,
                    fee_rec.Fee_ID,
                    p_Student_ID,
                    v_RoomID,
                    ROUND(fee_rec.Water_Fee * v_MyDays / v_TotalDays, 2),
                    ROUND(fee_rec.Power_Fee * v_MyDays / v_TotalDays, 2),
                    v_MyDays,
                    v_TotalDays,
                    '退宿',
                    '否',
                    SYSDATE
                );
            EXCEPTION
                WHEN DUP_VAL_ON_INDEX THEN
                    NULL;  -- 已存在退宿分摊，静默跳过
            END;
        END IF;
    END LOOP;

    -- 不 COMMIT：事务由调用方统一提交（分工对齐方案第 2 条）
END SP_Calc_Checkout_Fee;
/
