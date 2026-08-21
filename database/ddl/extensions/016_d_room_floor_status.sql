-- 扩展表迁移 016：D_Room 增加 Floor（楼层）与 Status（房间状态）列。
-- 在已有环境上执行于 ddl/extensions/015_facility_notice_sequences.sql 之后；
-- 全量重建时执行顺序为 foundation/001 -> extensions/010 -> extensions/011
--   -> extensions/012 -> extensions/013 -> extensions/014 -> extensions/015
--   -> extensions/016。
-- 本脚本只 ALTER 基线表 D_Room 加列，不重建、不改写既有列。
--
-- 为什么走编号迁移脚本（C-022，2026-08-10 裁决）？
--   Apifox 契约（api-contract-8.10.yaml，已锁定路径与名称）中：
--     DORM-05 新增房间 required 含 floor；
--     DORM-06 批量初始化 required 含 floor；
--     DORM-07 修改房间 body 含 status（枚举 正常/停用）。
--   而 foundation D_Room 只有 Power_Status（供电状态：正常/断电），无
--   Floor / Status 列。契约声明的楼层与房间状态语义无落库载体，实现方
--   只能做假实现或抛 ORA 约束错误。裁决：加列走迁移，契约不动。
--
-- 语义说明：
--   Status 与 Power_Status 是两回事：Status 管房间可用状态（正常/停用，
--   停用后不再分配入住），Power_Status 管供电状态（正常/断电，欠费自动
--   断电判定用）。二者并存，不冲突。
--   Floor 为楼层号（如 3），NUMBER(3) 覆盖常规宿舍楼。
--
-- 执行方式（DBeaver，JDBC 连接）：
--   单条 ALTER 语句，选中执行（Ctrl+Enter）即可，不带 "/"。
--
-- 重复执行说明：
--   ALTER TABLE ADD COLUMN 非幂等，重复执行报 ORA-01430（column already
--   exists）；已执行环境跳过本脚本即可。
--
-- 验证：重跑 database/verify/extension_schema_checks.sql，确认新增的第 14
--   部分输出 D_Room 的 FLOOR 与 STATUS 两列各 1 行。
ALTER TABLE D_Room
    ADD (Floor NUMBER(3),
         Status VARCHAR2(10) DEFAULT '正常' NOT NULL,
         CONSTRAINT CK_D_ROOM_STATUS CHECK (Status IN ('正常', '停用')));
