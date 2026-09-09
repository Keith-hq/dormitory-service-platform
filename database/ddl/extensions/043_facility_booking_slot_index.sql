-- 043 重建 UK_D_FACILITY_BOOK_ACTIVE（设施预约改为“按(设施, 时段)占用”）
--
-- 原索引：同一设施同时最多 1 条活跃(已预约/使用中)预约（整体占用，不分时段）。
--   造成：一个设施被某学生预约后，任何时段其它学生都不可约（“一直被占用”），
--   无法表达“选某天某时段预约、同段互斥、异段可约”。
--
-- 新索引：同一(设施, 开始时段) 最多 1 条活跃预约。
--   已预约/使用中 → 键 (Facility_ID, Start_Time)：同设施同 Start_Time 唯一（同段互斥）；
--   已完成/已失效   → 表达式取 NULL：不占键，时段释放后可再约。
--   前提：Start_Time 在预约时即写入所选时段（否则为 NULL，退化为整体互斥）。
--   前置校验：当前无 (Facility_ID, Start_Time) 重复的活跃预约（无则安全）。
DROP INDEX UK_D_FACILITY_BOOK_ACTIVE;
CREATE UNIQUE INDEX UK_D_FACILITY_BOOK_ACTIVE
    ON D_Facility_Booking (Facility_ID,
        CASE WHEN Status IN ('已预约', '使用中') AND Start_Time IS NOT NULL
             THEN ROUND((Start_Time - DATE '1970-01-01') * 86400)
             ELSE Booking_ID END);
