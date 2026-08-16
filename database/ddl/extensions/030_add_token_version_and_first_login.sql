-- ============================================================
-- 扩展表迁移 030：添加 TOKEN_VERSION 和 IS_FIRST_LOGIN 列
-- 说明：支持停用后 5 分钟会话失效（Token 版本号）和首次登录强制修改密码
-- 影响表：D_ADMIN, D_USER_ACCOUNT
-- ============================================================

-- 1. D_ADMIN 增加 TOKEN_VERSION 列
DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*)
      INTO v_exists
      FROM USER_TAB_COLUMNS
     WHERE TABLE_NAME = 'D_ADMIN'
       AND COLUMN_NAME = 'TOKEN_VERSION';

    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE D_ADMIN ADD TOKEN_VERSION NUMBER(10) DEFAULT 0 NOT NULL';
        EXECUTE IMMEDIATE 'COMMENT ON COLUMN D_ADMIN.TOKEN_VERSION IS ''Token 版本号，停用时自增，用于使旧 Token 失效''';
    END IF;
END;
/

-- 2. D_USER_ACCOUNT 增加 IS_FIRST_LOGIN 列
DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*)
      INTO v_exists
      FROM USER_TAB_COLUMNS
     WHERE TABLE_NAME = 'D_USER_ACCOUNT'
       AND COLUMN_NAME = 'IS_FIRST_LOGIN';

    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE D_USER_ACCOUNT ADD IS_FIRST_LOGIN VARCHAR2(1) DEFAULT ''Y'' NOT NULL';
        EXECUTE IMMEDIATE 'COMMENT ON COLUMN D_USER_ACCOUNT.IS_FIRST_LOGIN IS ''首次登录标志，Y-首次登录需改密，N-已改密''';
    END IF;
END;
/

-- 3. 存量账号设为非首登
UPDATE D_USER_ACCOUNT SET IS_FIRST_LOGIN = 'N';
COMMIT;

-- ============================================================
-- 回退脚本（如需回滚，请单独执行）
-- ============================================================
-- ALTER TABLE D_ADMIN DROP COLUMN TOKEN_VERSION;
-- ALTER TABLE D_USER_ACCOUNT DROP COLUMN IS_FIRST_LOGIN;
-- COMMIT;