-- 042 重建 UK_D_BED_ALLOC_ACTIVE 唯一索引（修复函数索引 NULL 语义缺陷）
--
-- 背景 / 缺陷：
--   原表达式 CASE WHEN CheckOut_Date IS NULL THEN Bed_No END 对“已结束”行取 NULL。
--   Oracle 函数唯一索引会把这些行压缩成同房间的 (Room_ID, NULL) 键，
--   导致【一个房间最多只能存在 1 条历史(已结束)记录】；
--   一旦该房已有一条历史，再产生任何退宿/调寝的旧记录即违反唯一约束(ORA-00001)，
--   表现为 调寝/退宿 服务器内部错误(500/409)。
--
-- 修复：
--   “在住”行    → 键 (Room_ID, Bed_No)        ：同房同床至多一条在住（约束保留）；
--   “已结束”行  → 键 (Room_ID, Allocation_ID) ：每条历史各自唯一，允许任意多条。
--
-- 影响范围：入住分配/调寝/退宿共用此兜底索引；仅放宽“每房仅 1 条历史”的错误限制，
--           在住唯一语义不变。需先确认全表在住 (Room_ID, Bed_No) 无重复。
DROP INDEX UK_D_BED_ALLOC_ACTIVE;
CREATE UNIQUE INDEX UK_D_BED_ALLOC_ACTIVE
    ON D_Bed_Allocation (Room_ID,
        CASE WHEN CheckOut_Date IS NULL THEN Bed_No ELSE Allocation_ID END);
