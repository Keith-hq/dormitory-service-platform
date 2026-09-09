-- 043 重建 UK_D_FACILITY_BOOK_ACTIVE（设施预约按“同设施同段互斥”，并保留即时预约互斥）
--
-- 原索引：同一设施同时最多 1 条活跃(已预约/使用中)预约（整体占用，不分时段）
--   → 一个设施被约后任何时段其它学生都不可约（“一直被占用”）。
--
-- 新索引（复合，利用“全部列为 NULL 的行不入索引”）：
--   活跃行(已预约/使用中)：
--     第 1 列 = Facility_ID
--     第 2 列 = NVL(Start_Time, DATE '1970-01-01')
--       · 时段预约(Start 非空) → (设施, 起始时间) 唯一 → 同设施同段互斥、异段可约；
--       · 即时预约(Start 为空) → (设施, epoch)    → 同设施同刻最多一条(即时互斥)。
--   历史行(已完成/已失效)：两列均 NULL → 完全不进索引 → 任意多条，且不干扰新预约。
-- 说明：Start_Time 由 SP_Book_Facility 在预约时写入所选时段；SP_Start_Use 不再覆写已有时段。
DROP INDEX UK_D_FACILITY_BOOK_ACTIVE;
CREATE UNIQUE INDEX UK_D_FACILITY_BOOK_ACTIVE
    ON D_Facility_Booking (
        CASE WHEN Status IN ('已预约', '使用中') THEN Facility_ID END,
        CASE WHEN Status IN ('已预约', '使用中') THEN NVL(Start_Time, DATE '1970-01-01') END
    );
