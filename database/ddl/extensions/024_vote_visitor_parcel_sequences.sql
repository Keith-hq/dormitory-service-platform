-- 扩展表迁移 024：D_Room_Vote / D_Visitor_Authorization / D_Parcel_Record 主键序列与触发器。
-- 022/023 编号已被占用，本迁移排在 024，按编号顺序执行。
-- 全量重建时执行顺序为 foundation/001 -> extensions/010 -> ... -> extensions/024。
-- 本脚本不得修改或重建任何基线表。
--
-- 为什么补这些序列？
--   安全社区三张表（D_Room_Vote、D_Visitor_Authorization、D_Parcel_Record）的
--   主键均为 NUMBER(10) 无自增，EF Core 插入时主键为 NULL 会报 ORA-01400。
--   参照 015 迁移为缺少序列的表补 sequence + BEFORE INSERT trigger。
--   D_Room_Vote_Response 为复合主键（Vote_ID + Student_ID），无需序列。
--
-- 执行方式（DBeaver，JDBC 连接）：
--   整段复制后执行（匿名块，幂等写法：已存在的 sequence / trigger 被跳过，
--   重复执行无害）。
--
-- 验证：重跑 database/verify/extension_schema_checks.sql 确认相关表存在；
--   插入一条投票/访客授权/快递记录验证主键自动填充。


-- ===== D_Room_Vote =====
DECLARE
    v_exists NUMBER;
    v_start_with NUMBER;
BEGIN
    SELECT COUNT(*)
      INTO v_exists
      FROM USER_SEQUENCES
     WHERE SEQUENCE_NAME = 'SEQ_D_ROOM_VOTE_ID';

    IF v_exists = 0 THEN
        SELECT NVL(MAX(Vote_ID), 0) + 1
          INTO v_start_with
          FROM D_Room_Vote;

        EXECUTE IMMEDIATE
            'CREATE SEQUENCE SEQ_D_ROOM_VOTE_ID START WITH ' || v_start_with ||
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
     WHERE TRIGGER_NAME = 'TRG_D_ROOM_VOTE_ID_BI';

    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE
            'CREATE TRIGGER TRG_D_ROOM_VOTE_ID_BI ' ||
            'BEFORE INSERT ON D_Room_Vote ' ||
            'FOR EACH ROW ' ||
            'WHEN (NEW.Vote_ID IS NULL) ' ||
            'BEGIN ' ||
            '    SELECT SEQ_D_ROOM_VOTE_ID.NEXTVAL ' ||
            '      INTO :NEW.Vote_ID ' ||
            '      FROM dual; ' ||
            'END;';
    END IF;
END;
/

-- ===== D_Visitor_Authorization =====
DECLARE
    v_exists NUMBER;
    v_start_with NUMBER;
BEGIN
    SELECT COUNT(*)
      INTO v_exists
      FROM USER_SEQUENCES
     WHERE SEQUENCE_NAME = 'SEQ_D_VISITOR_AUTH_ID';

    IF v_exists = 0 THEN
        SELECT NVL(MAX(Authorization_ID), 0) + 1
          INTO v_start_with
          FROM D_Visitor_Authorization;

        EXECUTE IMMEDIATE
            'CREATE SEQUENCE SEQ_D_VISITOR_AUTH_ID START WITH ' || v_start_with ||
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
     WHERE TRIGGER_NAME = 'TRG_D_VISITOR_AUTH_ID_BI';

    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE
            'CREATE TRIGGER TRG_D_VISITOR_AUTH_ID_BI ' ||
            'BEFORE INSERT ON D_Visitor_Authorization ' ||
            'FOR EACH ROW ' ||
            'WHEN (NEW.Authorization_ID IS NULL) ' ||
            'BEGIN ' ||
            '    SELECT SEQ_D_VISITOR_AUTH_ID.NEXTVAL ' ||
            '      INTO :NEW.Authorization_ID ' ||
            '      FROM dual; ' ||
            'END;';
    END IF;
END;
/

-- ===== D_Parcel_Record =====
DECLARE
    v_exists NUMBER;
    v_start_with NUMBER;
BEGIN
    SELECT COUNT(*)
      INTO v_exists
      FROM USER_SEQUENCES
     WHERE SEQUENCE_NAME = 'SEQ_D_PARCEL_RECORD_ID';

    IF v_exists = 0 THEN
        SELECT NVL(MAX(Parcel_ID), 0) + 1
          INTO v_start_with
          FROM D_Parcel_Record;

        EXECUTE IMMEDIATE
            'CREATE SEQUENCE SEQ_D_PARCEL_RECORD_ID START WITH ' || v_start_with ||
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
     WHERE TRIGGER_NAME = 'TRG_D_PARCEL_RECORD_ID_BI';

    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE
            'CREATE TRIGGER TRG_D_PARCEL_RECORD_ID_BI ' ||
            'BEFORE INSERT ON D_Parcel_Record ' ||
            'FOR EACH ROW ' ||
            'WHEN (NEW.Parcel_ID IS NULL) ' ||
            'BEGIN ' ||
            '    SELECT SEQ_D_PARCEL_RECORD_ID.NEXTVAL ' ||
            '      INTO :NEW.Parcel_ID ' ||
            '      FROM dual; ' ||
            'END;';
    END IF;
END;
/
