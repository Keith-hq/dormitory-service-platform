-- 扩展表迁移 015：D_Facility / D_Notice / D_Notice_Display 主键序列与触发器。
-- 在已有环境上执行于 ddl/extensions/014_credit_log_sequence.sql 之后；
-- 全量重建时执行顺序为 foundation/001 -> extensions/010 -> extensions/011
--   -> extensions/012 -> extensions/013 -> extensions/014 -> extensions/015。
-- 本脚本不得修改或重建任何基线表。
--
-- 为什么用编号迁移脚本而不是修改 010_extension_tables.sql（C-024）？
--   这三张表的主键序列原由 PR #28 直接追加在 010 末尾。010 是已建表并
--   验证过的基线脚本（23 张扩展表 + 证据已归档），回写基线会让脚本与已建
--   环境悄悄失同步；且新环境按 010->011->...->015 顺序重建时永远拿不到
--   追加段（追加只在有人手动重跑整个 010 时生效）。按项目规则，结构变化
--   只能走编号迁移脚本，本迁移把追加段原样拆出（C-024 裁决：拆 015）。
--
-- 内容来源：PR #28（feature/facility-notice）提交 ea755cd + 50e7e2a 的
--   010 追加段，原样迁移，无逻辑改动。
--
-- 执行方式（DBeaver，JDBC 连接）：
--   整段复制后执行（匿名块，幂等写法：已存在的 sequence / trigger 被跳过，
--   重复执行无害）。负责人环境已执行过等价内容，本脚本可直接执行验证。
--
-- 验证：重跑 database/verify/extension_schema_checks.sql，确认第 13 部分
--   每段 SELECT 均返回 1 行。


-- =====================================================================
-- 主键序列与触发器：D_Facility / D_Notice / D_Notice_Display
-- 基线 DDL 中这三个表缺少主键序列，EF Core 插入时主键为 NULL 会报 ORA-01400。
-- 以下匿名块均为幂等写法：已存在的 sequence / trigger 会被跳过，可重复执行。
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

-- ===== D_Notice_Display =====
-- 主键列名为 NOTICE_ID（与 D_Notice 共享同一主键域），序列按表主键语义命名，
-- 避免与 D_Notice 的 SEQ_D_NOTICE_ID 混淆（S3）。
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
