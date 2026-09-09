-- 042 重建 UK_D_BED_ALLOC_ACTIVE 唯一索引（修复函数索引 NULL 语义缺陷）
--
-- 背景 / 缺陷：
--   原表达式 CASE WHEN CheckOut_Date IS NULL THEN Bed_No END 对“已结束”行取 NULL，
--   配合非空的 Room_ID 组成索引键 (Room_ID, NULL) —— Oracle 对“部分列为 NULL”的行仍会
--   索引，导致同房间多条历史都聚成同一键 → 每房只能存在 1 条历史(已结束)记录；
--   一旦该房已有一条历史，再退宿/调寝即违反唯一约束(ORA-00001)。
--
-- 修复（复合索引，利用“全部列为 NULL 的行不入索引”）：
--   在住行   → 两列均为非空 (Room_ID, Bed_No)  → 同房同床位至多一条在住；
--   已结束行 → 两列均为 NULL → 完全不进索引    → 任意多条历史，无碰撞。
--   （不再用 Bed_No 与 Allocation_ID 混用同一列，避免历史键与新入住 1 号床冲突。）
-- 影响范围：入住分配/调寝/退宿共用此兜底；仅放宽“每房仅 1 条历史”的错误限制。
DROP INDEX UK_D_BED_ALLOC_ACTIVE;
CREATE UNIQUE INDEX UK_D_BED_ALLOC_ACTIVE
    ON D_Bed_Allocation (
        CASE WHEN CheckOut_Date IS NULL THEN Room_ID END,
        CASE WHEN CheckOut_Date IS NULL THEN Bed_No END
    );
