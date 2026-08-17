-- 扩展表迁移 031：D_Audit_Event 主键序列与触发器。
-- 2026-08-16 起草（集成测试 C4 执行现场发现，李昂）。
-- 注：原拟编号 029，与资产域迁移 029（asset/shareditem/cleaning）冲突，
-- 按评审意见改用 031（030 已被 TokenVersion/IsFirstLogin 占用）。
--
-- 现场问题：IT-C4-001 报修接口返回 500，日志为
--   ORA-01400: 无法将 NULL 插入 ("DORM_OPER"."D_AUDIT_EVENT"."AUDIT_ID")
-- D_Audit_Event 基表（010）主键为裸 NUMBER(10) 无生成器，EF 侧
-- ValueGeneratedOnAdd 插入 NULL 依赖数据库生成（同 020/023/028 先例）。
-- 025 已为 D_College/D_Major 补生成器，但审计表自身缺失；且 025 的
-- DETAILS 列在共享库未执行前会先报 ORA-00904（DETAILS 标识符无效），
-- 两层问题叠加导致审计落库从未走通。
-- 本迁移补 D_Audit_Event 生成器；显式提供 AUDIT_ID 的既有写方保持兼容
-- （WHEN (NEW.Audit_ID IS NULL)）。
--
-- 受影响接口：所有经 AuditEventFilter 落审计日志的写操作
-- （SUPER-07 / AUTH-05 / PWD-02 / VIOL-03 等）。
--
-- 执行方式：匿名块整体执行（幂等），重复执行无害。

DECLARE
    v_exists NUMBER;
    v_max_id NUMBER;
    v_start_with NUMBER;
    v_next_value NUMBER;
    v_advance_by NUMBER;
    v_consumed_value NUMBER;
BEGIN
    SELECT NVL(MAX(Audit_ID), 0) INTO v_max_id FROM D_Audit_Event;

    IF v_max_id >= 9999999999 THEN
        RAISE_APPLICATION_ERROR(-20031, 'D_Audit_Event.Audit_ID 已触达 NUMBER(10) 上限');
    END IF;

    SELECT COUNT(*) INTO v_exists
      FROM USER_SEQUENCES
     WHERE SEQUENCE_NAME = 'SEQ_D_AUDIT_EVENT_ID';

    IF v_exists = 0 THEN
        v_start_with := v_max_id + 1;
        EXECUTE IMMEDIATE
            'CREATE SEQUENCE SEQ_D_AUDIT_EVENT_ID START WITH ' || v_start_with ||
            ' INCREMENT BY 1 MAXVALUE 9999999999 NOCACHE';
    ELSE
        SELECT LAST_NUMBER INTO v_next_value
          FROM USER_SEQUENCES
         WHERE SEQUENCE_NAME = 'SEQ_D_AUDIT_EVENT_ID';

        -- 兼容旧环境中序列已创建、但尚未追上存量主键的情况。
        IF v_next_value <= v_max_id THEN
            v_advance_by := v_max_id - v_next_value + 1;
            EXECUTE IMMEDIATE
                'ALTER SEQUENCE SEQ_D_AUDIT_EVENT_ID INCREMENT BY ' || v_advance_by;
            EXECUTE IMMEDIATE
                'SELECT SEQ_D_AUDIT_EVENT_ID.NEXTVAL FROM dual' INTO v_consumed_value;
            EXECUTE IMMEDIATE
                'ALTER SEQUENCE SEQ_D_AUDIT_EVENT_ID INCREMENT BY 1';
        END IF;
    END IF;

    SELECT COUNT(*) INTO v_exists
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
