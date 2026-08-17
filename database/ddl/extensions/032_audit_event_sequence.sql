-- 扩展表迁移 032：D_Audit_Event 主键序列与触发器（补主键生成器）
--
-- 背景：D_Audit_Event 由 010 建表，AUDIT_ID NUMBER(10) 主键无序列/触发器。
--   后端全局 AuditEventFilter 通过 AuditService.LogEventAsync 每次请求写审计
--   记录，INSERT 不携带 AUDIT_ID，触发 ORA-01400（无法将 NULL 插入 AUDIT_ID），
--   导致所有经过审计过滤器的请求（含登录）返回 500。
--   本迁移沿用 020/024/028/029 的既有模式，为缺主键生成器的扩展表补
--   序列 + BEFORE INSERT 触发器（WHEN NEW.Audit_ID IS NULL 时取 NEXTVAL）。
--   不改动任何原有 DDL 文件（010/025 均不重写）。
--
-- 受影响表：D_Audit_Event.Audit_ID
-- 受影响接口：全局（AuditEventFilter / 审计日志 SUPER-07）
-- 验证：重跑 database/verify/extension_schema_checks.sql §25
--   （DETAILS 列检查不受影响）；下方返回序列/触发器各 1 行。

DECLARE
    v_exists NUMBER;
    v_max_id NUMBER;
    v_start_with NUMBER;
BEGIN
    SELECT NVL(MAX(Audit_ID), 0)
      INTO v_max_id
      FROM D_Audit_Event;

    IF v_max_id >= 9999999999 THEN
        RAISE_APPLICATION_ERROR(-20032, 'D_Audit_Event.Audit_ID 已触达 NUMBER(10) 上限');
    END IF;

    SELECT COUNT(*)
      INTO v_exists
      FROM USER_SEQUENCES
     WHERE SEQUENCE_NAME = 'SEQ_D_AUDIT_EVENT_ID';

    IF v_exists = 0 THEN
        v_start_with := v_max_id + 1;
        EXECUTE IMMEDIATE
            'CREATE SEQUENCE SEQ_D_AUDIT_EVENT_ID START WITH ' || v_start_with ||
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
     WHERE TRIGGER_NAME = 'TRG_D_AUDIT_EVENT_ID_BI';

    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE
            'CREATE TRIGGER TRG_D_AUDIT_EVENT_ID_BI ' ||
            'BEFORE INSERT ON D_Audit_Event ' ||
            'FOR EACH ROW ' ||
            'WHEN (NEW.Audit_ID IS NULL) ' ||
            'BEGIN ' ||
            '    SELECT SEQ_D_AUDIT_EVENT_ID.NEXTVAL ' ||
            '      INTO :NEW.Audit_ID ' ||
            '      FROM dual; ' ||
            'END;';
    END IF;
END;
/

-- 验证：
-- SELECT sequence_name FROM user_sequences WHERE sequence_name = 'SEQ_D_AUDIT_EVENT_ID';
-- SELECT trigger_name, status FROM user_triggers WHERE trigger_name = 'TRG_D_AUDIT_EVENT_ID_BI';
