-- 扩展表迁移 011：冻结 D_Notification.Notification_Type 枚举值。
-- 在已有环境上执行于 ddl/extensions/010_extension_tables.sql 之后；
-- 全量重建时执行顺序为 foundation/001 -> extensions/010 -> extensions/011。
-- 本脚本不得修改或重建任何基线表。
--
-- 为什么用编号迁移脚本而不是修改 010_extension_tables.sql？
--   23 张扩展表已经建表并验证（证据已归档）。回写基线会让脚本与已建环境
--   悄悄失同步；按项目规则，结构变化只能走编号迁移脚本。
--
-- 目的：
--   通知类型枚举已冻结（2026-08-07 裁决）：契约枚举
--   [预约, 报修, 账单, 信用, 访客, 系统] 必须在数据库层同步强制。
--   基线列 Notification_Type VARCHAR2(20) NOT NULL 没有任何约束，
--   任意字符串都可能写入。本 CHECK 把允许值钉死在数据库层，
--   使契约、代码与存储数据保持一致。
--
-- 约束：CK_D_NOTIFICATION_TYPE
--   CHECK (Notification_Type IN ('预约', '报修', '账单', '信用', '访客', '系统'))
--
-- 受影响接口（通知中心 5 个接口）：
--   GET  /api/notifications               （通知列表）
--   PUT  /api/notifications/{id}/read     （标记已读）
--   POST /api/notifications/read-batch    （批量已读）
--   GET  /api/notifications/unread-count  （未读数）
--   POST /api/internal/notifications      （SVC-NOTIFY-01，内部投递）
--
-- 验证：重跑 database/verify/extension_schema_checks.sql，第 9 部分
--   已包含 CK_D_NOTIFICATION_TYPE。
--
-- 重复执行说明：同一 schema 上执行两次会报 ORA-02260（约束名重复）；
--   重建流程中本脚本只执行一次。

ALTER TABLE D_Notification
    ADD CONSTRAINT CK_D_NOTIFICATION_TYPE
    CHECK (Notification_Type IN ('预约', '报修', '账单', '信用', '访客', '系统'));
