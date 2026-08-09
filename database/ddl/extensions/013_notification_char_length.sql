-- 扩展表迁移 013：D_Notification 标题与内容改为显式字符长度语义。
-- 在已有环境上执行于 ddl/extensions/012_notification_id_sequence.sql 之后；
-- 全量重建时执行顺序为 foundation/001 -> extensions/010 -> extensions/011
--   -> extensions/012 -> extensions/013。
-- 本脚本不得修改或重建任何基线表。
--
-- 为什么用编号迁移脚本而不是修改 010_extension_tables.sql？
--   与 011 同理：23 张扩展表已经建表并验证（证据已归档）。回写基线会
--   让脚本与已建环境悄悄失同步；按项目规则，结构变化只能走编号迁移脚本。
--
-- 目的（C-018）：
--   已确认实例 NLS_LENGTH_SEMANTICS = BYTE（2026-08-08 用户 DBeaver 验证）。
--   裸 VARCHAR2(n) 按字节计长：AL32UTF8 下 1 个汉字 3 字节，VARCHAR2(100)
--   实际只能存 33 个汉字，VARCHAR2(1000) 只能存 333 个汉字；而 OpenAPI 契约
--   maxLength 按字符计（100/1000），前端也按字符校验，中文标题/内容会
--   在校验通过后于入库时报 ORA-12899。
--   本迁移将两列显式改为 VARCHAR2(... CHAR)，使契约、校验与存储口径一致，
--   契约 maxLength 无需调整。
--
-- 执行方式（DBeaver，JDBC 连接）：
--   单条 DDL，选中执行（Ctrl+Enter）即可，与 011 相同，不带 "/"。
--
-- 重复执行说明：
--   ALTER TABLE MODIFY 幂等，同一 schema 上重复执行不报错。
--
-- 验证：重跑 database/verify/extension_schema_checks.sql，确认第 4 部分
--   输出正常（该部分仅展示 DATA_LENGTH，无长度断言，结果不受影响）。

-- 说明：两列已是 NOT NULL，MODIFY 时不写 NULL/NOT NULL 以保留现有空值属性；
--   若重复声明 NOT NULL，JDBC 连接下会报 ORA-01442（SQL*Plus 仅为警告）。
ALTER TABLE D_Notification
    MODIFY (Title VARCHAR2(100 CHAR),
            Content VARCHAR2(1000 CHAR));
