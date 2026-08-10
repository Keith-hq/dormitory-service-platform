-- 公共设施预约存储过程（难点③）v1.1
-- 依赖：D_Facility, D_Facility_Booking, D_Credit_Account, SEQ_FACILITY_BOOKING
-- 并发兜底：UK_D_FACILITY_BOOK_ACTIVE 函数索引

-- ============================================================
-- 创建专用序列
-- ============================================================
DECLARE v_StartVal NUMBER;
BEGIN
    SELECT NVL(MAX(Booking_ID), 0) + 1 INTO v_StartVal FROM D_Facility_Booking;
    EXECUTE IMMEDIATE 'CREATE SEQUENCE SEQ_FACILITY_BOOKING START WITH ' || v_StartVal || ' INCREMENT BY 1';
EXCEPTION WHEN OTHERS THEN IF SQLCODE = -955 THEN NULL; ELSE RAISE; END IF;
END;
/

-- ============================================================
-- SP_Book_Facility：即时预约
-- 参数：p_Facility_ID, p_Student_ID
-- 返回：0=成功(OUT Booking_ID), 1=设施不存在/不可用, 2=信用分不足,
--       3=已有活跃预约, 4=设施已被他人占用
-- ============================================================
CREATE OR REPLACE PROCEDURE SP_Book_Facility(
    p_Facility_ID  IN  NUMBER,
    p_Student_ID   IN  VARCHAR2,
    p_Result_Code  OUT NUMBER,
    p_Booking_ID   OUT NUMBER
) AS
    v_Facility_Status VARCHAR2(10);
    v_Credit_Score    NUMBER;
    v_Active_Count    NUMBER;
BEGIN
    p_Result_Code := 0;

    -- 1. 检查设施状态
    BEGIN
        SELECT Status INTO v_Facility_Status
        FROM D_Facility WHERE Facility_ID = p_Facility_ID;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN p_Result_Code := 1; RETURN;
    END;

    IF v_Facility_Status != '正常' THEN p_Result_Code := 1; RETURN; END IF;

    -- 2. 检查信用分
    BEGIN
        SELECT Current_Score INTO v_Credit_Score
        FROM D_Credit_Account WHERE Student_ID = p_Student_ID;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN p_Result_Code := 2; RETURN;
    END;

    IF v_Credit_Score < 60 THEN p_Result_Code := 2; RETURN; END IF;

    -- 3. 检查是否已有活跃预约（学生维度）
    SELECT COUNT(*) INTO v_Active_Count
    FROM D_Facility_Booking
    WHERE Student_ID = p_Student_ID AND Status IN ('已预约', '使用中');

    IF v_Active_Count > 0 THEN p_Result_Code := 3; RETURN; END IF;

    -- 4. 插入预约（设施维度由 UK_D_FACILITY_BOOK_ACTIVE 兜底）
    BEGIN
        INSERT INTO D_Facility_Booking (
            Booking_ID, Facility_ID, Student_ID, Create_Time, Status
        ) VALUES (
            SEQ_FACILITY_BOOKING.NEXTVAL, p_Facility_ID, p_Student_ID, SYSDATE, '已预约'
        ) RETURNING Booking_ID INTO p_Booking_ID;
    EXCEPTION
        WHEN DUP_VAL_ON_INDEX THEN p_Result_Code := 4; RETURN;
    END;

    COMMIT;
END SP_Book_Facility;
/

-- ============================================================
-- SP_Start_Use：开始使用（原子 UPDATE + SQL%ROWCOUNT）
-- ============================================================
CREATE OR REPLACE PROCEDURE SP_Start_Use(
    p_Booking_ID IN NUMBER,
    p_Student_ID IN VARCHAR2,
    p_Result_Code OUT NUMBER
) AS
    v_Rows NUMBER;
BEGIN
    p_Result_Code := 0;

    UPDATE D_Facility_Booking
    SET Status = '使用中',
        Start_Time = SYSDATE,
        End_Time = SYSDATE + INTERVAL '60' MINUTE
    WHERE Booking_ID = p_Booking_ID
      AND Student_ID = p_Student_ID
      AND Status = '已预约';

    v_Rows := SQL%ROWCOUNT;

    IF v_Rows = 0 THEN
        -- 判断是预约不存在还是状态不对还是身份不匹配
        SELECT Status INTO p_Result_Code FROM D_Facility_Booking WHERE Booking_ID = p_Booking_ID;
        -- 能查到说明不是预约不存在，状态不对或身份不对
        p_Result_Code := 1;
        RETURN;
    END IF;

    COMMIT;
EXCEPTION
    WHEN NO_DATA_FOUND THEN p_Result_Code := 1; RETURN;
END SP_Start_Use;
/

-- ============================================================
-- SP_Finish_Use：结束使用
-- ============================================================
CREATE OR REPLACE PROCEDURE SP_Finish_Use(
    p_Booking_ID IN NUMBER,
    p_Student_ID IN VARCHAR2,
    p_Result_Code OUT NUMBER
) AS
    v_Rows NUMBER;
BEGIN
    p_Result_Code := 0;

    UPDATE D_Facility_Booking
    SET Status = '已完成',
        End_Time = CASE WHEN SYSDATE > Start_Time THEN SYSDATE
                        ELSE Start_Time + INTERVAL '1' SECOND END
    WHERE Booking_ID = p_Booking_ID
      AND Student_ID = p_Student_ID
      AND Status = '使用中';

    v_Rows := SQL%ROWCOUNT;

    IF v_Rows = 0 THEN
        p_Result_Code := 1;
        RETURN;
    END IF;

    COMMIT;
END SP_Finish_Use;
/

-- ============================================================
-- SP_Expire_Booking：过期巡检
-- ============================================================
CREATE OR REPLACE PROCEDURE SP_Expire_Booking AS
BEGIN
    UPDATE D_Facility_Booking
    SET Status = '已失效'
    WHERE Status = '已预约'
      AND (SYSDATE - Create_Time) * 24 * 60 > 15;
    COMMIT;
END SP_Expire_Booking;
/

-- ============================================================
-- SP_Auto_Complete：超时自动完成
-- ============================================================
CREATE OR REPLACE PROCEDURE SP_Auto_Complete AS
BEGIN
    UPDATE D_Facility_Booking
    SET Status = '已完成'
    WHERE Status = '使用中' AND End_Time IS NOT NULL AND SYSDATE > End_Time;
    COMMIT;
END SP_Auto_Complete;
/
