-- 扩展表迁移 028：D_USER_ACCOUNT 主键序列与触发器。
--
-- 账号由超级管理员通过 AUTH-04 / AUTH-05 创建。ACCOUNT_ID 是裸 NUMBER(10)，
-- 若由应用层 MAX+1 或默认 0 生成，会产生并发冲突。统一改为 Oracle 序列 +
-- BEFORE INSERT 触发器；显式提供 ACCOUNT_ID 的既有测试数据保持兼容。
--
-- 受影响接口：POST /api/auth/accounts/students、POST /api/auth/accounts/admins。

DECLARE
    v_exists NUMBER;
    v_max_id NUMBER;
    v_start_with NUMBER;
    v_next_value NUMBER;
    v_advance_by NUMBER;
    v_consumed_value NUMBER;
BEGIN
    SELECT NVL(MAX(Account_ID), 0) INTO v_max_id FROM D_User_Account;

    IF v_max_id >= 9999999999 THEN
        RAISE_APPLICATION_ERROR(-20028, 'D_User_Account.Account_ID 已触达 NUMBER(10) 上限');
    END IF;

    SELECT COUNT(*) INTO v_exists
      FROM USER_SEQUENCES
     WHERE SEQUENCE_NAME = 'SEQ_D_USER_ACCOUNT_ID';

    IF v_exists = 0 THEN
        v_start_with := v_max_id + 1;
        EXECUTE IMMEDIATE
            'CREATE SEQUENCE SEQ_D_USER_ACCOUNT_ID START WITH ' || v_start_with ||
            ' INCREMENT BY 1 MAXVALUE 9999999999 NOCACHE';
    ELSE
        SELECT LAST_NUMBER INTO v_next_value
          FROM USER_SEQUENCES
         WHERE SEQUENCE_NAME = 'SEQ_D_USER_ACCOUNT_ID';

        -- 兼容旧环境中序列已创建、但尚未追上存量主键的情况。
        IF v_next_value <= v_max_id THEN
            v_advance_by := v_max_id - v_next_value + 1;
            EXECUTE IMMEDIATE
                'ALTER SEQUENCE SEQ_D_USER_ACCOUNT_ID INCREMENT BY ' || v_advance_by;
            EXECUTE IMMEDIATE
                'SELECT SEQ_D_USER_ACCOUNT_ID.NEXTVAL FROM dual' INTO v_consumed_value;
            EXECUTE IMMEDIATE
                'ALTER SEQUENCE SEQ_D_USER_ACCOUNT_ID INCREMENT BY 1';
        END IF;
    END IF;

    SELECT COUNT(*) INTO v_exists
      FROM USER_TRIGGERS
     WHERE TRIGGER_NAME = 'TRG_D_USER_ACCOUNT_ID_BI';

    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE
            'CREATE TRIGGER TRG_D_USER_ACCOUNT_ID_BI ' ||
            'BEFORE INSERT ON D_User_Account ' ||
            'FOR EACH ROW ' ||
            'WHEN (NEW.Account_ID IS NULL) ' ||
            'BEGIN ' ||
            '    SELECT SEQ_D_USER_ACCOUNT_ID.NEXTVAL INTO :NEW.Account_ID FROM dual; ' ||
            'END;';
    END IF;
END;
/

-- 验证：以下查询应分别返回 1。
-- SELECT COUNT(*) FROM USER_SEQUENCES WHERE SEQUENCE_NAME = 'SEQ_D_USER_ACCOUNT_ID';
-- SELECT COUNT(*) FROM USER_TRIGGERS WHERE TRIGGER_NAME = 'TRG_D_USER_ACCOUNT_ID_BI' AND STATUS = 'ENABLED';
