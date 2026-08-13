-- 扩展表迁移 019：D_Item_Loan / D_Repair_Material_Usage 增加 Idempotency_Key（幂等键）列与唯一索引。
-- 在已有环境上执行于 ddl/extensions/018_d_student_email.sql 之后；
-- 全量重建时执行顺序为 foundation/001 -> extensions/010 -> extensions/011
--   -> extensions/012 -> extensions/013 -> extensions/014 -> extensions/015
--   -> extensions/016 -> extensions/017 -> extensions/018 -> extensions/019。
-- 本脚本只 ALTER 扩展表加列与索引，不重建、不改写既有列。
--
-- 为什么走编号迁移脚本（C-030，2026-08-13 确认）？
--   难点④ 借出（STU-25）与耗材出库（DORM-29）的幂等设计依赖
--   "持久化幂等键 + 唯一索引兜底"：并发重放时 INSERT 撞唯一索引 → 回滚
--   库存扣减（SAVEPOINT）→ 读取胜者记录比对内容。原实现把 ALTER / 索引
--   内联在 database/sp/sp_shared_item.sql 中（PR #36 三审/五审指出）：
--   ① 不在编号迁移家族内，全量重建顺序中无落点；
--   ② extension_schema_checks.sql 无对应结构检查，可重建与验证证据不完整。
--   确认：加列与索引走编号迁移；sp_shared_item.sql 删除内联 DDL 补丁段，
--   只保留 SP 与专用序列（依赖本迁移保证列与索引存在）。
--
-- 语义说明：
--   Idempotency_Key 可空：SP 层幂等检查以"键非空"为前提（客户端未提供键
--   时不做幂等去重）；唯一索引对全 NULL 列天然允许多行，不会误伤无键记录。
--   VARCHAR2(100 CHAR) 显式字符长度：本实例 NLS_LENGTH_SEMANTICS=BYTE
--   （C-018），若写 VARCHAR2(100) 实际只有 100 字节（中文约 33 字符），与
--   "幂等键 100 字符"契约语义不符；同 013/014/017/018 迁移先例。
--   校验口径随 CHAR 语义统一为字符数：SP_Borrow_Item / SP_Consume_Material
--   的 LENGTHB 校验改为 LENGTH；应用层 InventoryTxnController 的 UTF-8
--   字节数校验改为字符数（key.Length > 100）。原字节校验在 CHAR 语义下
--   偏严（行为安全但口径不一致），一并随本迁移对齐（由 PR #36 整改实施）。
--
-- 执行方式（DBeaver，JDBC 连接）：
--   逐条语句选中执行（Ctrl+Enter）即可，不带 "/"；按 ALTER → ALTER →
--   CREATE INDEX → CREATE INDEX 顺序执行。DDL 隐式提交，无法回滚；若仅
--   部分成功，按原顺序重跑，已存在的列/索引会报 ORA-01430 / ORA-00955，
--   确认对象已存在后跳过即可。
--
-- 重复执行说明：
--   ALTER TABLE ADD COLUMN 与 CREATE UNIQUE INDEX 非幂等：
--   - 已执行过本迁移的环境跳过本脚本；
--   - 已执行过 sp_shared_item.sql 内联 DDL 补丁的环境（列与索引同名，
--     但列是 VARCHAR2(100) 字节语义），执行本脚本前需先执行文末的
--     "兼容对齐"段（ALTER ... MODIFY 为 CHAR 语义，列已存在不重加）。
--
-- 验证：重跑 database/verify/extension_schema_checks.sql，确认新增的第 17
--   部分输出 D_ITEM_LOAN / D_REPAIR_MATERIAL_USAGE 的 IDEMPOTENCY_KEY 列
--   各 1 行（NULLABLE='Y'、CHAR_USED='C'、CHAR_LENGTH=100），以及
--   UK_D_ITEM_LOAN_IDEM / UK_D_REPAIR_MAT_USE_IDEM 唯一索引各 1 行。
ALTER TABLE D_Item_Loan
    ADD (Idempotency_Key VARCHAR2(100 CHAR));

ALTER TABLE D_Repair_Material_Usage
    ADD (Idempotency_Key VARCHAR2(100 CHAR));

CREATE UNIQUE INDEX UK_D_ITEM_LOAN_IDEM
    ON D_Item_Loan (Idempotency_Key);

CREATE UNIQUE INDEX UK_D_REPAIR_MAT_USE_IDEM
    ON D_Repair_Material_Usage (Idempotency_Key);

-- ============ 兼容对齐（仅"已执行过内联 DDL 补丁"的环境需要） ============
-- sp_shared_item.sql 旧内联补丁创建的列是 VARCHAR2(100) 字节语义，
-- 与 019 的 CHAR 语义不一致；对齐后校验口径（LENGTH 字符数）才匹配。
-- 全新重建库（010→…→019）不需要执行本段。
-- ALTER TABLE D_Item_Loan MODIFY (Idempotency_Key VARCHAR2(100 CHAR));
-- ALTER TABLE D_Repair_Material_Usage MODIFY (Idempotency_Key VARCHAR2(100 CHAR));
