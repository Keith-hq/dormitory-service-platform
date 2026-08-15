-- 扩展表迁移 025：D_Audit_Event 增加 DETAILS 列 + D_College / D_Major 主键序列与触发器。
-- 2026-08-15 起草（PR #55 三审，数据库负责人确认后执行）。
-- 在已有环境上执行于 ddl/extensions/024_xxx.sql（其他 PR）之后；
-- 全量重建时执行顺序为 foundation/001 -> extensions/010 -> extensions/011
--   -> ... -> extensions/021 -> extensions/022/023/024（其他 PR）-> extensions/025。
-- 本脚本不修改、不重建任何基线表的既有列/约束；只对扩展表 D_Audit_Event 加列，
--   并为 D_College / D_Major 补主键生成器（加列先例 C-027/018，序列先例
--   C-031/C-032/020：基础表主键为裸 NUMBER(10)，初始设计意图是应用层生成主键；
--   既有已实测走通的插入路径均为"序列+触发器"家族，EF ValueGeneratedOnAdd +
--   ODP.NET RETURNING 回填）。
--
-- 变更依据（PR #55 三审，2026-08-15）：
--   Part 1  D_Audit_Event.DETAILS：锁定契约 SUPER-07 / AUTH-05 / PWD-02 / VIOL-03
--           均要求"写入审计日志（含详情）"，PR #55 审计实现写入 details 参数；
--           基线 010 的 D_Audit_Event 只有 6 列（Audit_ID/Actor_Account_ID/
--           Event_Type/Target_Type/Target_ID/Event_Time），无详情列。代码侧先以
--           [NotMapped] 兼容（数据暂不持久化），本迁移落地后由作者移除 [NotMapped]。
--           受影响接口：SUPER-07、AUTH-05、PWD-02、VIOL-03。
--   Part 2/3 D_College / D_Major 序列+触发器：PR #55 实现 SUPER-01 POST /colleges
--           与 SUPER-02 POST /majors（EF ValueGeneratedOnAdd，插入主键为 NULL），
--           基线 foundation 该两表主键为裸 NUMBER(10) 无生成器，缺少序列会报
--           ORA-01400（同 020 先例）。为并发安全统一走序列：NEXTVAL 原子、
--           无 MAX+1 竞态；WHEN (NEW.xxx IS NULL) 对既有显式赋值的写方保持兼容。
--
-- 语义说明：
--   DETAILS 可空：既存审计事件与新事件均可为空。
--   VARCHAR2(2000 CHAR) 显式字符长度：本实例 NLS_LENGTH_SEMANTICS=BYTE（C-018），
--   使用 CHAR 语义与 013/017/018/019/021 迁移保持一致。
--
-- 执行方式（DBeaver，JDBC 连接）：
--   Part 1 为单条 ALTER TABLE，选中执行（Ctrl+Enter）即可，不带 "/"；
--     重复执行报 ORA-01430（column already exists），已执行环境跳过该段即可。
--   Part 2 / Part 3 为匿名块，整段复制执行（幂等写法：已存在的 sequence /
--     trigger 被跳过，重复执行无害）。
--
-- 验证：重跑 database/verify/extension_schema_checks.sql，确认新增第 20 部分
--   三段 SELECT 分别返回 1 行（DETAILS 列）、2 行（序列）、2 行（触发器）。


-- =====================================================================
-- Part 1：D_Audit_Event 增加 DETAILS 列
-- =====================================================================
ALTER TABLE D_Audit_Event ADD DETAILS VARCHAR2(2000 CHAR);
COMMENT ON COLUMN D_Audit_Event.DETAILS IS '审计操作的详细描述（如创建/修改/删除时的上下文信息）';


-- =====================================================================
-- Part 2：D_College 主键序列与触发器
-- =====================================================================
DECLARE
    v_exists NUMBER;
    v_max_id NUMBER;
    v_start_with NUMBER;
BEGIN
    SELECT NVL(MAX(College_ID), 0)
      INTO v_max_id
      FROM D_College;

    IF v_max_id >= 9999999999 THEN
        RAISE_APPLICATION_ERROR(-20022, 'D_College.College_ID 已触达 NUMBER(10) 上限');
    END IF;

    SELECT COUNT(*)
      INTO v_exists
      FROM USER_SEQUENCES
     WHERE SEQUENCE_NAME = 'SEQ_D_COLLEGE_ID';

    IF v_exists = 0 THEN
        v_start_with := v_max_id + 1;
        EXECUTE IMMEDIATE
            'CREATE SEQUENCE SEQ_D_COLLEGE_ID START WITH ' || v_start_with ||
            ' INCREMENT BY 1 MAXVALUE 9999999999 NOCACHE';
    END IF;

    SELECT COUNT(*)
      INTO v_exists
      FROM USER_TRIGGERS
     WHERE TRIGGER_NAME = 'TRG_D_COLLEGE_ID_BI';

    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE
            'CREATE TRIGGER TRG_D_COLLEGE_ID_BI ' ||
            'BEFORE INSERT ON D_College ' ||
            'FOR EACH ROW ' ||
            'WHEN (NEW.College_ID IS NULL) ' ||
            'BEGIN ' ||
            '    SELECT SEQ_D_COLLEGE_ID.NEXTVAL ' ||
            '      INTO :NEW.College_ID ' ||
            '      FROM dual; ' ||
            'END;';
    END IF;
END;


-- =====================================================================
-- Part 3：D_Major 主键序列与触发器
-- =====================================================================
DECLARE
    v_exists NUMBER;
    v_max_id NUMBER;
    v_start_with NUMBER;
BEGIN
    SELECT NVL(MAX(Major_ID), 0)
      INTO v_max_id
      FROM D_Major;

    IF v_max_id >= 9999999999 THEN
        RAISE_APPLICATION_ERROR(-20023, 'D_Major.Major_ID 已触达 NUMBER(10) 上限');
    END IF;

    SELECT COUNT(*)
      INTO v_exists
      FROM USER_SEQUENCES
     WHERE SEQUENCE_NAME = 'SEQ_D_MAJOR_ID';

    IF v_exists = 0 THEN
        v_start_with := v_max_id + 1;
        EXECUTE IMMEDIATE
            'CREATE SEQUENCE SEQ_D_MAJOR_ID START WITH ' || v_start_with ||
            ' INCREMENT BY 1 MAXVALUE 9999999999 NOCACHE';
    END IF;

    SELECT COUNT(*)
      INTO v_exists
      FROM USER_TRIGGERS
     WHERE TRIGGER_NAME = 'TRG_D_MAJOR_ID_BI';

    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE
            'CREATE TRIGGER TRG_D_MAJOR_ID_BI ' ||
            'BEFORE INSERT ON D_Major ' ||
            'FOR EACH ROW ' ||
            'WHEN (NEW.Major_ID IS NULL) ' ||
            'BEGIN ' ||
            '    SELECT SEQ_D_MAJOR_ID.NEXTVAL ' ||
            '      INTO :NEW.Major_ID ' ||
            '      FROM dual; ' ||
            'END;';
    END IF;
END;
