-- 通知主键序列、触发器与收件人索引迁移。
-- 在 ddl/extensions/011_notification_type_check.sql 之后执行；
-- 全量重建时执行顺序为 foundation/001 -> extensions/010 -> extensions/011 -> extensions/012。
-- 本脚本不得修改或重建任何基线表。
--
-- 执行方式（DBeaver，JDBC 连接）：
--   本脚本是三个独立的 PL/SQL 匿名块，以 END; 结束，不带 SQL*Plus 的 "/"。
--   将光标放到块内（或选中单个块）按 Ctrl+Enter 逐块执行；不要用 Alt+X 脚本模式。
--
-- 目的：
--   为 D_Notification.Notification_ID 提供数据库端主键生成机制，并为按收件人查询通知建立索引。
--
-- 执行顺序：
--   先创建序列，再创建 BEFORE INSERT 触发器，最后创建收件人索引。
--
-- 重复执行说明：
--   同一 schema 上重复执行时，已存在的 sequence、trigger、index 会被跳过；
--   已存在对象不会被替换或重建，避免影响已运行环境。

DECLARE
    v_exists NUMBER;
    v_start_with NUMBER;
BEGIN
    SELECT COUNT(*)
      INTO v_exists
      FROM USER_SEQUENCES
     WHERE SEQUENCE_NAME = 'SEQ_D_NOTIFICATION_ID';

    IF v_exists = 0 THEN
        SELECT NVL(MAX(Notification_ID), 0) + 1
          INTO v_start_with
          FROM D_Notification;

        EXECUTE IMMEDIATE
            'CREATE SEQUENCE SEQ_D_NOTIFICATION_ID START WITH ' || v_start_with ||
            ' INCREMENT BY 1 NOCACHE';
    END IF;
END;
/

DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*)
      INTO v_exists
      FROM USER_TRIGGERS
     WHERE TRIGGER_NAME = 'TRG_D_NOTIFICATION_ID_BI';

    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE
            'CREATE TRIGGER TRG_D_NOTIFICATION_ID_BI ' ||
            'BEFORE INSERT ON D_Notification ' ||
            'FOR EACH ROW ' ||
            'WHEN (NEW.Notification_ID IS NULL) ' ||
            'BEGIN ' ||
            '    SELECT SEQ_D_NOTIFICATION_ID.NEXTVAL ' ||
            '      INTO :NEW.Notification_ID ' ||
            '      FROM dual; ' ||
            'END;';
    END IF;
END;
/

DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*)
      INTO v_exists
      FROM USER_INDEXES
     WHERE INDEX_NAME = 'IDX_D_NOTIFICATION_RECIPIENT';

    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE
            'CREATE INDEX IDX_D_NOTIFICATION_RECIPIENT ' ||
            'ON D_Notification(Recipient_Account_ID)';
    END IF;
END;
/
