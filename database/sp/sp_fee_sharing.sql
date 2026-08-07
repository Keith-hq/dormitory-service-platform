-- 水电分摊存储过程（难点①）v1.1
-- 依赖：D_Utility_Fee, D_Bed_Allocation, D_Fee_Detail, D_Room
-- 基线：database/ddl/extensions/010_extension_tables.sql（UK_D_FEE_DETAIL）

-- ============================================================
-- 创建主键序列——替代 MAX+1，避免并发撞 PK
-- ============================================================
BEGIN
    EXECUTE IMMEDIATE 'CREATE SEQUENCE SEQ_FEE_DETAIL START WITH 1 INCREMENT BY 1';
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE = -955 THEN NULL; END IF;  -- 序列已存在则跳过
END;
/

-- ============================================================
-- SP_Calc_Monthly_Fee：每月1日调用，批量生成全部个人分摊
-- 参数：p_YearMonth 格式 'YYYY-MM'（如 '2026-08'）
-- ============================================================
CREATE OR REPLACE PROCEDURE SP_Calc_Monthly_Fee(
    p_YearMonth IN VARCHAR2
) AS
    v_TotalDays NUMBER := 0;
    v_MonthStart DATE;
    v_MonthEnd   DATE;
    v_DupCount   NUMBER := 0;   -- 重复跳过计数
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

            -- 捕获 UK 冲突：重复执行时跳过已存在的分摊记录
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
BEGIN
    -- 从退宿记录中读取实际退宿日期（而非 SYSDATE）
    SELECT Room_ID, CheckOut_Date INTO v_RoomID, v_CheckoutDate
    FROM D_Bed_Allocation
    WHERE Allocation_ID = p_Allocation_ID
      AND Student_ID = p_Student_ID;

    -- 如果 CheckOut_Date 尚未写入（清算在退宿写入之前执行），
    -- 回退使用 SYSDATE 并记录风险
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
        WHERE Room_ID = v_RoomID AND Year_Month = v_YearMonth
    ) LOOP
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
    END LOOP;

    COMMIT;
END SP_Calc_Checkout_Fee;
/
