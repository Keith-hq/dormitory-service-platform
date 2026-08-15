-- 扩展表迁移 027：D_ADMIN 增加 POST 列（宿管岗位）。
-- 在已有环境上执行于 ddl/extensions/026（#58 待合入）之后；
-- 全量重建时执行顺序为 foundation/001 -> extensions/010 -> ... -> extensions/025
--   -> extensions/026 -> extensions/027。
-- 编号说明：023 已分配给 #49（D_Checkout 序列+房间唯一索引）、024 分配给 #52、
--   025 分配给 #55（工作台 C-040 裁定）、026 分配给 #58（D_Utility_Fee 序列），
--   本迁移编号为 027，用于 AUTH-05 契约补充（宿管岗位字段）。
-- 本脚本不得修改或重建任何基线表。
--
-- 为什么补 D_ADMIN.POST？
--   契约 POST /accounts/admins 要求支持岗位（post）字段，但基线表 D_ADMIN
--   仅有 ROLE_LEVEL（角色），无单独岗位列。为存储宿管的具体岗位（如“楼长”），
--   新增 VARCHAR2(50) 列 POST，不强制非空。
--   当前实现中，POST 字段与 ROLE_LEVEL 可能内容重叠，但契约要求独立字段，
--   因此按需添加，不影响现有业务。
--
-- 执行方式（DBeaver，JDBC 连接）：
--   整段复制后执行（匿名块，幂等写法：已存在的列会被跳过，重复执行无害）。

-- =====================================================================
-- 列添加：D_ADMIN.POST
-- 幂等写法：检查列是否存在，若不存在则添加。
-- =====================================================================

DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*)
      INTO v_exists
      FROM USER_TAB_COLUMNS
     WHERE TABLE_NAME = 'D_ADMIN'
       AND COLUMN_NAME = 'POST';

    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE D_ADMIN ADD POST VARCHAR2(50)';
        EXECUTE IMMEDIATE 'COMMENT ON COLUMN D_ADMIN.POST IS ''宿管岗位（如：楼长、维修员等）''';
    END IF;
END;
/

-- =====================================================================
-- 回退脚本（如需回滚，请单独执行）
-- =====================================================================
-- ALTER TABLE D_ADMIN DROP COLUMN POST;
-- COMMIT;