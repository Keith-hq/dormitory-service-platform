-- 水电分摊存储过程（难点①）
-- 依赖：D_Utility_Fee, D_Bed_Allocation, D_Fee_Detail, D_Room
-- 执行前确认已创建扩展表 D_Fee_Detail

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
    v_NextDetailID NUMBER;
BEGIN
    v_MonthStart := TO_DATE(p_YearMonth || '-01', 'YYYY-MM-DD');
    v_MonthEnd   := LAST_DAY(v_MonthStart);

    -- 获取当前最大 Detail_ID
    SELECT NVL(MAX(Detail_ID), 0) INTO v_NextDetailID FROM D_Fee_Detail;
    v_NextDetailID := v_NextDetailID + 1;

    -- 遍历每个有账单的房间
    FOR fee_rec IN (
        SELECT * FROM D_Utility_Fee WHERE Year_Month = p_YearMonth
    ) LOOP

        -- 计算该房间本月总人天数
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

        -- 跳过本月无人入住的房间
        IF v_TotalDays IS NULL OR v_TotalDays = 0 THEN
            CONTINUE;
        END IF;

        -- 为每个在住学生生成分摊记录
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

            INSERT INTO D_Fee_Detail (
                Detail_ID, Fee_ID, Student_ID, Room_ID,
                Water_Share, Power_Share, Stay_Days, Total_Days,
                Bill_Type, Is_Paid, Create_Time
            ) VALUES (
                v_NextDetailID,
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

            v_NextDetailID := v_NextDetailID + 1;
        END LOOP;
    END LOOP;

    COMMIT;
END SP_Calc_Monthly_Fee;
/

-- ============================================================
-- SP_Calc_Checkout_Fee：退宿时调用，为退宿学生结算当月分摊
-- 参数：p_Student_ID  退宿学生学号
--       p_Allocation_ID  对应的住宿分配记录ID
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
    v_NextDetailID NUMBER;
BEGIN
    v_YearMonth := TO_CHAR(SYSDATE, 'YYYY-MM');
    v_MonthStart := TO_DATE(v_YearMonth || '-01', 'YYYY-MM-DD');
    v_MonthEnd   := LAST_DAY(v_MonthStart);

    -- 获取退宿房间
    SELECT Room_ID INTO v_RoomID
    FROM D_Bed_Allocation
    WHERE Allocation_ID = p_Allocation_ID
      AND Student_ID = p_Student_ID;

    -- 计算该学生在当月住了多少天
    SELECT TRUNC(SYSDATE) - v_MonthStart + 1 INTO v_MyDays FROM DUAL;

    -- 重新计算该房间当月总人天数
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

    -- 获取下一条 Detail_ID
    SELECT NVL(MAX(Detail_ID), 0) + 1 INTO v_NextDetailID FROM D_Fee_Detail;

    -- 为退宿学生写入当月分摊
    FOR fee_rec IN (
        SELECT * FROM D_Utility_Fee
        WHERE Room_ID = v_RoomID AND Year_Month = v_YearMonth
    ) LOOP
        INSERT INTO D_Fee_Detail (
            Detail_ID, Fee_ID, Student_ID, Room_ID,
            Water_Share, Power_Share, Stay_Days, Total_Days,
            Bill_Type, Is_Paid, Create_Time
        ) VALUES (
            v_NextDetailID,
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
