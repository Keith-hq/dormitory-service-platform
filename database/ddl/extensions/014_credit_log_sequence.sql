-- 信用分流水主键序列、触发器、查询索引及字符长度语义迁移。
-- 在 ddl/extensions/013_notification_char_length.sql 之后执行；
-- 全量重建时执行顺序为 foundation/001 -> extensions/010 -> extensions/011
--   -> extensions/012 -> extensions/013 -> extensions/014。
-- 本脚本不得修改或重建任何基线表。
--
-- 执行方式（DBeaver，JDBC 连接）：
--   前三个 PL/SQL 匿名块以 END; 结束，不带 SQL*Plus 的 "/"；将光标放到
--   块内（或选中单个块）按 Ctrl+Enter 逐块执行。最后一条 ALTER TABLE
--   单独选中执行。
--
-- 重复执行说明：
--   已存在的 sequence、trigger、index 会被跳过；ALTER TABLE MODIFY 可重复执行。
--
-- 非原子提示：
--   Oracle DDL 会隐式提交。若脚本仅部分成功，请按原顺序重跑；已存在对象会
--   自动跳过，但已经成功的 DDL 不能随事务回滚。

DECLARE
    v_exists NUMBER;
    v_max_log_id NUMBER;
    v_start_with NUMBER;
BEGIN
    SELECT NVL(MAX(Log_ID), 0)
      INTO v_max_log_id
      FROM D_Credit_Log;

    IF v_max_log_id >= 9999999999 THEN
        RAISE_APPLICATION_ERROR(-20014, 'D_Credit_Log.Log_ID 已触达 NUMBER(10) 上限');
    END IF;

    SELECT COUNT(*)
      INTO v_exists
      FROM USER_SEQUENCES
     WHERE SEQUENCE_NAME = 'SEQ_D_CREDIT_LOG_ID';

    IF v_exists = 0 THEN
        v_start_with := v_max_log_id + 1;
        EXECUTE IMMEDIATE
            'CREATE SEQUENCE SEQ_D_CREDIT_LOG_ID START WITH ' || v_start_with ||
            ' INCREMENT BY 1 MAXVALUE 9999999999 NOCACHE';
    END IF;
END;
/

DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*)
      INTO v_exists
      FROM USER_TRIGGERS
     WHERE TRIGGER_NAME = 'TRG_D_CREDIT_LOG_ID_BI';

    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE
            'CREATE TRIGGER TRG_D_CREDIT_LOG_ID_BI ' ||
            'BEFORE INSERT ON D_Credit_Log ' ||
            'FOR EACH ROW ' ||
            'WHEN (NEW.Log_ID IS NULL) ' ||
            'BEGIN ' ||
            '    SELECT SEQ_D_CREDIT_LOG_ID.NEXTVAL ' ||
            '      INTO :NEW.Log_ID ' ||
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
     WHERE INDEX_NAME = 'IDX_D_CREDIT_LOG_STUDENT_TIME';

    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE
            'CREATE INDEX IDX_D_CREDIT_LOG_STUDENT_TIME ' ||
            'ON D_Credit_Log(Student_ID, Create_Time, Log_ID)';
    END IF;
END;
/

ALTER TABLE D_Credit_Log
    MODIFY (Reason VARCHAR2(200 CHAR),
            Event_Key VARCHAR2(100 CHAR));
