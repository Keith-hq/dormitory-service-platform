-- 公共设施预约存储过程（难点③）
-- 依赖：D_Facility, D_Facility_Booking, D_Credit_Account
-- 基线：database/ddl/extensions/010_extension_tables.sql
-- 并发兜底：UK_D_FACILITY_BOOK_ACTIVE 函数索引（数据库级）

-- ============================================================
-- SP_Book_Facility：即时预约——锁定设施，检查无冲突后创建预约
-- 参数：p_Facility_ID  设施编号
--       p_Student_ID   预约学生学号
-- 返回：0=成功, 1=设施不可用, 2=信用分不足, 3=已有活跃预约
-- ============================================================
CREATE OR REPLACE PROCEDURE SP_Book_Facility(
    p_Facility_ID IN NUMBER,
    p_Student_ID  IN VARCHAR2,
    p_Result_Code OUT NUMBER
) AS
    v_Facility_Status VARCHAR2(10);
    v_Credit_Score    NUMBER;
    v_Active_Count    NUMBER;
BEGIN
    p_Result_Code := 0;

    -- 1. 检查设施状态
    SELECT Status INTO v_Facility_Status
    FROM D_Facility
    WHERE Facility_ID = p_Facility_ID;

    IF v_Facility_Status != '正常' THEN
        p_Result_Code := 1;  -- 设施维修/停用
        RETURN;
    END IF;

    -- 2. 检查信用分（低于60冻结）
    SELECT Current_Score INTO v_Credit_Score
    FROM D_Credit_Account
    WHERE Student_ID = p_Student_ID;

    IF v_Credit_Score < 60 THEN
        p_Result_Code := 2;  -- 信用分不足
        RETURN;
    END IF;

    -- 3. 检查是否已有活跃预约（同一学生不能重复预约）
    SELECT COUNT(*) INTO v_Active_Count
    FROM D_Facility_Booking
    WHERE Student_ID = p_Student_ID
      AND Status IN ('已预约', '使用中');

    IF v_Active_Count > 0 THEN
        p_Result_Code := 3;  -- 已有活跃预约
        RETURN;
    END IF;

    -- 4. 创建预约（UK_D_FACILITY_BOOK_ACTIVE 兜底：同一设施最多一条活跃）
    INSERT INTO D_Facility_Booking (
        Booking_ID, Facility_ID, Student_ID,
        Create_Time, Start_Time, End_Time, Status
    ) VALUES (
        SEQ_FEE_DETAIL.NEXTVAL,  -- 复用已有序列
        p_Facility_ID,
        p_Student_ID,
        SYSDATE,
        NULL,      -- 尚未开始
        NULL,      -- 尚未结束
        '已预约'
    );

    COMMIT;
END SP_Book_Facility;
/

-- ============================================================
-- SP_Start_Use：点击"开始使用"——Status 从"已预约"→"使用中"
-- 参数：p_Booking_ID  预约记录编号
--       p_Student_ID  操作学生（校验身份）
-- 返回：0=成功, 1=预约不存在/状态不对, 2=身份不匹配
-- ============================================================
CREATE OR REPLACE PROCEDURE SP_Start_Use(
    p_Booking_ID IN NUMBER,
    p_Student_ID IN VARCHAR2,
    p_Result_Code OUT NUMBER
) AS
    v_Status VARCHAR2(10);
    v_Owner  VARCHAR2(20);
BEGIN
    p_Result_Code := 0;

    SELECT Status, Student_ID INTO v_Status, v_Owner
    FROM D_Facility_Booking
    WHERE Booking_ID = p_Booking_ID;

    IF v_Owner != p_Student_ID THEN
        p_Result_Code := 2;  -- 身份不匹配
        RETURN;
    END IF;

    IF v_Status != '已预约' THEN
        p_Result_Code := 1;  -- 状态不对
        RETURN;
    END IF;

    -- 预填 End_Time = 60 分钟后，满足 CK_D_FACILITY_BOOK_TIME（必须同时非NULL）
    UPDATE D_Facility_Booking
    SET Status = '使用中',
        Start_Time = SYSDATE,
        End_Time = SYSDATE + INTERVAL '60' MINUTE
    WHERE Booking_ID = p_Booking_ID;

    COMMIT;
END SP_Start_Use;
/

-- ============================================================
-- SP_End_Use：结束使用——Status 从"使用中"→"已完成"，释放设施
-- 参数：p_Booking_ID  预约记录编号
--       p_Student_ID  操作学生
-- 返回：0=成功, 1=状态不对, 2=身份不匹配
-- ============================================================
CREATE OR REPLACE PROCEDURE SP_End_Use(
    p_Booking_ID IN NUMBER,
    p_Student_ID IN VARCHAR2,
    p_Result_Code OUT NUMBER
) AS
    v_Status VARCHAR2(10);
    v_Owner  VARCHAR2(20);
BEGIN
    p_Result_Code := 0;

    SELECT Status, Student_ID INTO v_Status, v_Owner
    FROM D_Facility_Booking
    WHERE Booking_ID = p_Booking_ID;

    IF v_Owner != p_Student_ID THEN
        p_Result_Code := 2;
        RETURN;
    END IF;

    IF v_Status != '使用中' THEN
        p_Result_Code := 1;
        RETURN;
    END IF;

    -- 提前结束：End_Time 更新为实际时间，安全兜底保证 > Start_Time
    UPDATE D_Facility_Booking
    SET Status = '已完成',
        End_Time = CASE WHEN SYSDATE > Start_Time THEN SYSDATE
                        ELSE Start_Time + INTERVAL '1' SECOND END
    WHERE Booking_ID = p_Booking_ID;

    COMMIT;
END SP_End_Use;
/

-- ============================================================
-- SP_Expire_Booking：过期巡检——"已预约"超过15分钟未开始→失效
-- Quartz 每15秒调用一次
-- ============================================================
CREATE OR REPLACE PROCEDURE SP_Expire_Booking AS
BEGIN
    UPDATE D_Facility_Booking
    SET Status = '已失效'
    WHERE Status = '已预约'
      AND (SYSDATE - Create_Time) * 24 * 60 > 15;  -- 超过15分钟

    COMMIT;
END SP_Expire_Booking;
/

-- ============================================================
-- SP_Auto_Complete：超时自动完成——"使用中"且 NOW>End_Time → 已完成
-- End_Time 在 SP_Start_Use 时预填（60分钟后），此处只改状态
-- Quartz 每15秒调用一次
-- ============================================================
CREATE OR REPLACE PROCEDURE SP_Auto_Complete AS
BEGIN
    UPDATE D_Facility_Booking
    SET Status = '已完成'
    WHERE Status = '使用中'
      AND End_Time IS NOT NULL
      AND SYSDATE > End_Time;

    COMMIT;
END SP_Auto_Complete;
/
