-- 扩展表迁移 020：D_Repair_Ticket / D_Repair_Attachment / D_Late_Entry /
--   D_Hygiene_Record / D_Room 主键序列与触发器。
-- 在已有环境上执行于 ddl/extensions/019_shared_item_idempotency.sql 之后；
-- 全量重建时执行顺序为 foundation/001 -> extensions/010 -> extensions/011
--   -> extensions/012 -> extensions/013 -> extensions/014 -> extensions/015
--   -> extensions/016 -> extensions/017 -> extensions/018 -> extensions/019
--   -> extensions/020。
-- 本脚本不得修改或重建任何基线表。
--
-- 为什么补这 5 张表的序列（C-031 / C-032 裁决）？
--   基线 DDL（foundation 20 张表）主键均为裸 NUMBER(10)，初始设计意图是应用层
--   生成主键；但既有已实测走通的插入路径均为"序列+触发器"（012/014/015 家族，
--   EF ValueGeneratedOnAdd + ODP.NET RETURNING 回填）。PR #44 的 8 个 501 接口
--   （STU-08/10/11/12、DORM-31/32/34、REP-01/02）首次向 D_Repair_Ticket /
--   D_Repair_Attachment / D_Late_Entry / D_Hygiene_Record 写入，缺少序列会导致
--   EF 插入主键为 NULL 报 ORA-01400；D_Room 的 EF 模型已配置 ValueGeneratedOnAdd
--   但库中无生成器（RoomService.CreateAsync 不赋值），同样存在 ORA-01400 地雷。
--   为并发安全统一走序列：NEXTVAL 原子、无 MAX+1 竞态；WHEN (NEW.xxx IS NULL)
--   对既有显式赋值的写方保持兼容。
--
-- 执行方式（DBeaver，JDBC 连接）：
--   整段复制后执行（匿名块，幂等写法：已存在的 sequence / trigger 被跳过，
--   重复执行无害）。
--
-- 验证：重跑 database/verify/extension_schema_checks.sql，确认第 18 部分
--   每段 SELECT 均返回 5 行。


-- =====================================================================
-- 主键序列与触发器：D_Repair_Ticket / D_Repair_Attachment / D_Late_Entry
--   / D_Hygiene_Record / D_Room
-- 以下匿名块均为幂等写法：已存在的 sequence / trigger 会被跳过，可重复执行；
-- 每张表均含 NUMBER(10) 上限防护（同 014 迁移先例）。
-- =====================================================================

-- ===== D_Repair_Ticket =====
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

-- ===== D_Repair_Attachment =====
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

-- ===== D_Late_Entry =====
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

-- ===== D_Hygiene_Record =====
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

-- ===== D_Room =====
-- C-032：EF 模型已配置 ValueGeneratedOnAdd，但库中无生成器，插入会 ORA-01400。
DECLARE
    v_exists NUMBER;
    v_max_id NUMBER;
    v_start_with NUMBER;
BEGIN
    SELECT NVL(MAX(Room_ID), 0)
      INTO v_max_id
      FROM D_Room;

    IF v_max_id >= 9999999999 THEN
        RAISE_APPLICATION_ERROR(-20020, 'D_Room.Room_ID 已触达 NUMBER(10) 上限');
    END IF;

    SELECT COUNT(*)
      INTO v_exists
      FROM USER_SEQUENCES
     WHERE SEQUENCE_NAME = 'SEQ_D_ROOM_ID';

    IF v_exists = 0 THEN
        v_start_with := v_max_id + 1;
        EXECUTE IMMEDIATE
            'CREATE SEQUENCE SEQ_D_ROOM_ID START WITH ' || v_start_with ||
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
