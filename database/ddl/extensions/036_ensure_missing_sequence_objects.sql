-- 扩展表迁移 036：兜底重建生产缺失的主键序列/触发器/索引与列语义补齐
--   （PR #90 上线事故修复，2026-08-21）。
-- 在已有环境上执行于 ddl/extensions/035_fix_byte_columns_fee_attempt_audit.sql 之后。
--
-- 事故与根因（2026-08-21 生产复盘）：
--   PR #90 部署后生产环境「提交报修」报 500；生产日志同期出现
--   D_Notification.Notification_ID 的 ORA-01400（OverdueCheckJob 每 15 分钟抛
--   一次）。生产库盘点（USER_SEQUENCES / USER_TRIGGERS / USER_TABLES 实测）确认
--   编号迁移 012 / 013 / 014 / 015 / 017 / 018 / 020 的对象在生产库缺失或未生效，
--   而 023~034 全部在位。可能成因：
--   1) migrate-db.sh 引导模式回填竞态：D_APP_MIGRATION 为空但 schema 已有表时，
--      010~035 全部标记"已应用"而从不执行，此后增量模式只跑差集，缺失对象
--      永不补建（migrate-db.sh [4/6]）；
--   2) 早期编号迁移为 DBeaver 风格（匿名块缺 "/"），流水线 sqlplus 下会静默
--      吞块但记录"已应用"（2026-08-18 实测 029 事故同源）。
--   EF 直插路径依赖这些序列+触发器回填主键（模型 ValueGeneratedOnAdd 约定，
--   INSERT 不带主键），缺失即 ORA-01400；sp/*.sql 每次部署恒重跑，故预约/
--   借用等 SP 路径不受影响——与"只有 EF 直插路径 500"的现象一致。
--
-- 生产实测证据（2026-08-21，DORM_OPER@DORMPDB）：
--   - 报修 POST：ORA-01400 D_REPAIR_TICKET.TICKET_ID（020 缺失）
--   - OverdueCheckJob：ORA-01400 D_NOTIFICATION.NOTIFICATION_ID（012 缺失）
--   - D_CREDIT_LOG.REASON / EVENT_KEY 仍为 BYTE 语义（014 未生效）
--   - TRG_D_ROOM_ID_BI 存在且 VALID，引用旧式 SEQ_D_ROOM（房间插入路径
--     实际可用）→ 本迁移保留该触发器不重建，仅在无触发器时才建 020 规范件。
--
-- 本脚本全部为幂等写法（已存在的对象被跳过；MODIFY/守卫式 ADD 可重复执行），
-- 重复执行无害。任何一段失败会让 migrate-db.sh 中止部署——每段均为
-- "对象已存在即跳过"的最小防御写法。
--
-- 验证：重跑 database/verify/extension_schema_checks.sql 第 34 部分。

-- =====================================================================
-- Part A：通知（012 序列/触发器/索引 + 013 CHAR 语义）
-- =====================================================================

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

-- 013：通知标题/内容 CHAR 语义（MODIFY 幂等；不写 NULL/NOT NULL 以保留空值属性）
ALTER TABLE D_Notification
    MODIFY (Title VARCHAR2(100 CHAR),
            Content VARCHAR2(1000 CHAR));

-- =====================================================================
-- Part B：报修链（020 五组序列/触发器 + 021 SEQ_SLA_LOG）
-- =====================================================================

-- ===== D_Repair_Ticket（报修单主键，报修 500 实测根因） =====
DECLARE
    v_exists NUMBER;
    v_max_id NUMBER;
    v_start_with NUMBER;
BEGIN
    SELECT NVL(MAX(Ticket_ID), 0)
      INTO v_max_id
      FROM D_Repair_Ticket;

    IF v_max_id >= 9999999999 THEN
        RAISE_APPLICATION_ERROR(-20020, 'D_Repair_Ticket.Ticket_ID 已触达 NUMBER(10) 上限');
    END IF;

    SELECT COUNT(*)
      INTO v_exists
      FROM USER_SEQUENCES
     WHERE SEQUENCE_NAME = 'SEQ_D_REPAIR_TICKET_ID';

    IF v_exists = 0 THEN
        v_start_with := v_max_id + 1;
        EXECUTE IMMEDIATE
            'CREATE SEQUENCE SEQ_D_REPAIR_TICKET_ID START WITH ' || v_start_with ||
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
     WHERE TRIGGER_NAME = 'TRG_D_REPAIR_TICKET_ID_BI';

    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE
            'CREATE TRIGGER TRG_D_REPAIR_TICKET_ID_BI ' ||
            'BEFORE INSERT ON D_Repair_Ticket ' ||
            'FOR EACH ROW ' ||
            'WHEN (NEW.Ticket_ID IS NULL) ' ||
            'BEGIN ' ||
            '    SELECT SEQ_D_REPAIR_TICKET_ID.NEXTVAL ' ||
            '      INTO :NEW.Ticket_ID ' ||
            '      FROM dual; ' ||
            'END;';
    END IF;
END;
/

-- ===== D_Repair_Attachment（报修附件，与报修单同链写入） =====
DECLARE
    v_exists NUMBER;
    v_max_id NUMBER;
    v_start_with NUMBER;
BEGIN
    SELECT NVL(MAX(Attachment_ID), 0)
      INTO v_max_id
      FROM D_Repair_Attachment;

    IF v_max_id >= 9999999999 THEN
        RAISE_APPLICATION_ERROR(-20020, 'D_Repair_Attachment.Attachment_ID 已触达 NUMBER(10) 上限');
    END IF;

    SELECT COUNT(*)
      INTO v_exists
      FROM USER_SEQUENCES
     WHERE SEQUENCE_NAME = 'SEQ_D_REPAIR_ATTACHMENT_ID';

    IF v_exists = 0 THEN
        v_start_with := v_max_id + 1;
        EXECUTE IMMEDIATE
            'CREATE SEQUENCE SEQ_D_REPAIR_ATTACHMENT_ID START WITH ' || v_start_with ||
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
     WHERE TRIGGER_NAME = 'TRG_D_REPAIR_ATTACHMENT_ID_BI';

    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE
            'CREATE TRIGGER TRG_D_REPAIR_ATTACHMENT_ID_BI ' ||
            'BEFORE INSERT ON D_Repair_Attachment ' ||
            'FOR EACH ROW ' ||
            'WHEN (NEW.Attachment_ID IS NULL) ' ||
            'BEGIN ' ||
            '    SELECT SEQ_D_REPAIR_ATTACHMENT_ID.NEXTVAL ' ||
            '      INTO :NEW.Attachment_ID ' ||
            '      FROM dual; ' ||
            'END;';
    END IF;
END;
/

-- ===== SEQ_SLA_LOG（D_Repair_Log 主键序列，SP_Complete_Repair 显式 NEXTVAL） =====
DECLARE
    v_exists NUMBER;
    v_max_id NUMBER;
    v_start_with NUMBER;
BEGIN
    SELECT NVL(MAX(Log_ID), 0)
      INTO v_max_id
      FROM D_Repair_Log;

    IF v_max_id >= 9999999999 THEN
        RAISE_APPLICATION_ERROR(-20020, 'D_Repair_Log.Log_ID 已触达 NUMBER(10) 上限');
    END IF;

    SELECT COUNT(*)
      INTO v_exists
      FROM USER_SEQUENCES
     WHERE SEQUENCE_NAME = 'SEQ_SLA_LOG';

    IF v_exists = 0 THEN
        v_start_with := v_max_id + 1;
        EXECUTE IMMEDIATE
            'CREATE SEQUENCE SEQ_SLA_LOG START WITH ' || v_start_with ||
            ' INCREMENT BY 1 MAXVALUE 9999999999 NOCACHE';
    END IF;
END;
/

-- ===== D_Late_Entry（晚归登记） =====
DECLARE
    v_exists NUMBER;
    v_max_id NUMBER;
    v_start_with NUMBER;
BEGIN
    SELECT NVL(MAX(Record_ID), 0)
      INTO v_max_id
      FROM D_Late_Entry;

    IF v_max_id >= 9999999999 THEN
        RAISE_APPLICATION_ERROR(-20020, 'D_Late_Entry.Record_ID 已触达 NUMBER(10) 上限');
    END IF;

    SELECT COUNT(*)
      INTO v_exists
      FROM USER_SEQUENCES
     WHERE SEQUENCE_NAME = 'SEQ_D_LATE_ENTRY_ID';

    IF v_exists = 0 THEN
        v_start_with := v_max_id + 1;
        EXECUTE IMMEDIATE
            'CREATE SEQUENCE SEQ_D_LATE_ENTRY_ID START WITH ' || v_start_with ||
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
     WHERE TRIGGER_NAME = 'TRG_D_LATE_ENTRY_ID_BI';

    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE
            'CREATE TRIGGER TRG_D_LATE_ENTRY_ID_BI ' ||
            'BEFORE INSERT ON D_Late_Entry ' ||
            'FOR EACH ROW ' ||
            'WHEN (NEW.Record_ID IS NULL) ' ||
            'BEGIN ' ||
            '    SELECT SEQ_D_LATE_ENTRY_ID.NEXTVAL ' ||
            '      INTO :NEW.Record_ID ' ||
            '      FROM dual; ' ||
            'END;';
    END IF;
END;
/

-- ===== D_Hygiene_Record（卫生检查记录） =====
DECLARE
    v_exists NUMBER;
    v_max_id NUMBER;
    v_start_with NUMBER;
BEGIN
    SELECT NVL(MAX(Record_ID), 0)
      INTO v_max_id
      FROM D_Hygiene_Record;

    IF v_max_id >= 9999999999 THEN
        RAISE_APPLICATION_ERROR(-20020, 'D_Hygiene_Record.Record_ID 已触达 NUMBER(10) 上限');
    END IF;

    SELECT COUNT(*)
      INTO v_exists
      FROM USER_SEQUENCES
     WHERE SEQUENCE_NAME = 'SEQ_D_HYGIENE_RECORD_ID';

    IF v_exists = 0 THEN
        v_start_with := v_max_id + 1;
        EXECUTE IMMEDIATE
            'CREATE SEQUENCE SEQ_D_HYGIENE_RECORD_ID START WITH ' || v_start_with ||
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
     WHERE TRIGGER_NAME = 'TRG_D_HYGIENE_RECORD_ID_BI';

    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE
            'CREATE TRIGGER TRG_D_HYGIENE_RECORD_ID_BI ' ||
            'BEFORE INSERT ON D_Hygiene_Record ' ||
            'FOR EACH ROW ' ||
            'WHEN (NEW.Record_ID IS NULL) ' ||
            'BEGIN ' ||
            '    SELECT SEQ_D_HYGIENE_RECORD_ID.NEXTVAL ' ||
            '      INTO :NEW.Record_ID ' ||
            '      FROM dual; ' ||
            'END;';
    END IF;
END;
/

-- ===== D_Room（房间主键；生产既有 TRG_D_ROOM_ID_BI 引用旧式 SEQ_D_ROOM 且
--   VALID，予以保留——仅当触发器不存在时才补建 020 规范件；序列同理，
--   已有可用生成路径时不另建 SEQ_D_ROOM_ID 以避免双序列并发取值碰撞） =====
DECLARE
    v_seq_exists NUMBER;
    v_trg_exists NUMBER;
    v_max_id NUMBER;
    v_start_with NUMBER;
BEGIN
    SELECT COUNT(*)
      INTO v_trg_exists
      FROM USER_TRIGGERS
     WHERE TRIGGER_NAME = 'TRG_D_ROOM_ID_BI';

    IF v_trg_exists = 0 THEN
        SELECT NVL(MAX(Room_ID), 0)
          INTO v_max_id
          FROM D_Room;

        IF v_max_id >= 9999999999 THEN
            RAISE_APPLICATION_ERROR(-20020, 'D_Room.Room_ID 已触达 NUMBER(10) 上限');
        END IF;

        SELECT COUNT(*)
          INTO v_seq_exists
          FROM USER_SEQUENCES
         WHERE SEQUENCE_NAME = 'SEQ_D_ROOM_ID';

        IF v_seq_exists = 0 THEN
            v_start_with := v_max_id + 1;
            EXECUTE IMMEDIATE
                'CREATE SEQUENCE SEQ_D_ROOM_ID START WITH ' || v_start_with ||
                ' INCREMENT BY 1 MAXVALUE 9999999999 NOCACHE';
        END IF;
    END IF;
END;
/

DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*)
      INTO v_exists
      FROM USER_TRIGGERS
     WHERE TRIGGER_NAME = 'TRG_D_ROOM_ID_BI';

    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE
            'CREATE TRIGGER TRG_D_ROOM_ID_BI ' ||
            'BEFORE INSERT ON D_Room ' ||
            'FOR EACH ROW ' ||
            'WHEN (NEW.Room_ID IS NULL) ' ||
            'BEGIN ' ||
            '    SELECT SEQ_D_ROOM_ID.NEXTVAL ' ||
            '      INTO :NEW.Room_ID ' ||
            '      FROM dual; ' ||
            'END;';
    END IF;
END;
/

-- =====================================================================
-- Part C：信用流水（014 序列/触发器/索引 + CHAR 语义）
-- =====================================================================

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

-- 014：信用流水 CHAR 语义（MODIFY 幂等；生产实测两列仍为 BYTE 语义）
ALTER TABLE D_Credit_Log
    MODIFY (Reason VARCHAR2(200 CHAR),
            Event_Key VARCHAR2(100 CHAR));

-- =====================================================================
-- Part D：设施/公告（015 三组序列/触发器）
-- =====================================================================

-- ===== D_Facility =====
DECLARE
    v_exists NUMBER;
    v_start_with NUMBER;
BEGIN
    SELECT COUNT(*)
      INTO v_exists
      FROM USER_SEQUENCES
     WHERE SEQUENCE_NAME = 'SEQ_D_FACILITY_ID';

    IF v_exists = 0 THEN
        SELECT NVL(MAX(Facility_ID), 0) + 1
          INTO v_start_with
          FROM D_Facility;

        EXECUTE IMMEDIATE
            'CREATE SEQUENCE SEQ_D_FACILITY_ID START WITH ' || v_start_with ||
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
     WHERE TRIGGER_NAME = 'TRG_D_FACILITY_ID_BI';

    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE
            'CREATE TRIGGER TRG_D_FACILITY_ID_BI ' ||
            'BEFORE INSERT ON D_Facility ' ||
            'FOR EACH ROW ' ||
            'WHEN (NEW.Facility_ID IS NULL) ' ||
            'BEGIN ' ||
            '    SELECT SEQ_D_FACILITY_ID.NEXTVAL ' ||
            '      INTO :NEW.Facility_ID ' ||
            '      FROM dual; ' ||
            'END;';
    END IF;
END;
/

-- ===== D_Notice =====
DECLARE
    v_exists NUMBER;
    v_start_with NUMBER;
BEGIN
    SELECT COUNT(*)
      INTO v_exists
      FROM USER_SEQUENCES
     WHERE SEQUENCE_NAME = 'SEQ_D_NOTICE_ID';

    IF v_exists = 0 THEN
        SELECT NVL(MAX(Notice_ID), 0) + 1
          INTO v_start_with
          FROM D_Notice;

        EXECUTE IMMEDIATE
            'CREATE SEQUENCE SEQ_D_NOTICE_ID START WITH ' || v_start_with ||
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
     WHERE TRIGGER_NAME = 'TRG_D_NOTICE_ID_BI';

    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE
            'CREATE TRIGGER TRG_D_NOTICE_ID_BI ' ||
            'BEFORE INSERT ON D_Notice ' ||
            'FOR EACH ROW ' ||
            'WHEN (NEW.Notice_ID IS NULL) ' ||
            'BEGIN ' ||
            '    SELECT SEQ_D_NOTICE_ID.NEXTVAL ' ||
            '      INTO :NEW.Notice_ID ' ||
            '      FROM dual; ' ||
            'END;';
    END IF;
END;
/

-- ===== D_Notice_Display（主键列同为 NOTICE_ID，序列独立命名避免混淆） =====
DECLARE
    v_exists NUMBER;
    v_start_with NUMBER;
BEGIN
    SELECT COUNT(*)
      INTO v_exists
      FROM USER_SEQUENCES
     WHERE SEQUENCE_NAME = 'SEQ_D_NOTICE_DISPLAY_PK';

    IF v_exists = 0 THEN
        SELECT NVL(MAX(Notice_ID), 0) + 1
          INTO v_start_with
          FROM D_Notice_Display;

        EXECUTE IMMEDIATE
            'CREATE SEQUENCE SEQ_D_NOTICE_DISPLAY_PK START WITH ' || v_start_with ||
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
     WHERE TRIGGER_NAME = 'TRG_D_NOTICE_DISPLAY_PK_BI';

    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE
            'CREATE TRIGGER TRG_D_NOTICE_DISPLAY_PK_BI ' ||
            'BEFORE INSERT ON D_Notice_Display ' ||
            'FOR EACH ROW ' ||
            'WHEN (NEW.Notice_ID IS NULL) ' ||
            'BEGIN ' ||
            '    SELECT SEQ_D_NOTICE_DISPLAY_PK.NEXTVAL ' ||
            '      INTO :NEW.Notice_ID ' ||
            '      FROM dual; ' ||
            'END;';
    END IF;
END;
/

-- =====================================================================
-- Part E：离校/学生列补齐（017 / 018 守卫式 ADD COLUMN）
--   ADD COLUMN 非幂等（ORA-01430），故先查 USER_TAB_COLUMNS 再动态 ALTER；
--   表在生产盘点中确认存在。
-- =====================================================================

-- 017：D_Leave_Application.Reason（离校报备原因）
DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*)
      INTO v_exists
      FROM USER_TAB_COLUMNS
     WHERE TABLE_NAME = 'D_LEAVE_APPLICATION' AND COLUMN_NAME = 'REASON';

    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE
            'ALTER TABLE D_Leave_Application ADD (Reason VARCHAR2(200 CHAR))';
    END IF;
END;
/

-- 018：D_Student.Email（学生邮箱）
DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*)
      INTO v_exists
      FROM USER_TAB_COLUMNS
     WHERE TABLE_NAME = 'D_STUDENT' AND COLUMN_NAME = 'EMAIL';

    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE
            'ALTER TABLE D_Student ADD (Email VARCHAR2(200 CHAR))';
    END IF;
END;
/
