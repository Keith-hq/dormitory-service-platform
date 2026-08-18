-- 扩展表迁移 032：CK_D_ADMIN_ROLE 纳入"辅导员"角色（ADR-0007）
--
-- 背景：代码层（AuthController.cs / AdminsController.cs）支持 4 类角色
--   （超级管理员 / 楼长 / 维修员 / 辅导员），登录时映射 JWT role
--   （super_admin / admin / repairman / counselor）。但迁移 021 创建的
--   CK_D_ADMIN_ROLE 仅含 3 值（楼长/维修员/超级管理员），辅导员在
--   D_Admin 落库时被 ORA-02290 拦截，辅导员账号无法创建、无法登录。
-- 处置：登记矛盾清单 → 团队 ADR-0007 拍板 → 本迁移重定义约束，纳入辅导员。
--
-- 影响表：D_Admin.Role_Level
-- 受影响接口：AUTH-01/02（各角色登录）、AUTH-05（建管理员账号）、COUN-01~04
-- 验证：重跑 database/verify/extension_schema_checks.sql §19d（存在性校验，
--   应仍返回 1 行）与 §30（值集检查，SEARCH_CONDITION 应含"辅导员"）。

DECLARE
    v_has_ck NUMBER;
    v_has_counselor NUMBER;
BEGIN
    SELECT COUNT(*)
      INTO v_has_ck
      FROM USER_CONSTRAINTS
     WHERE CONSTRAINT_NAME = 'CK_D_ADMIN_ROLE';

    IF v_has_ck = 0 THEN
        -- 约束不存在：直接建四值约束
        EXECUTE IMMEDIATE
            'ALTER TABLE D_Admin ADD CONSTRAINT CK_D_ADMIN_ROLE ' ||
            'CHECK (Role_Level IN (''超级管理员'', ''楼长'', ''维修员'', ''辅导员''))';
    ELSE
        -- 约束存在：检查值集是否已含辅导员
        SELECT COUNT(*)
          INTO v_has_counselor
          FROM USER_CONSTRAINTS
         WHERE CONSTRAINT_NAME = 'CK_D_ADMIN_ROLE'
           AND INSTR(SEARCH_CONDITION_VC, '辅导员') > 0;

        IF v_has_counselor = 0 THEN
            EXECUTE IMMEDIATE 'ALTER TABLE D_Admin DROP CONSTRAINT CK_D_ADMIN_ROLE';
            EXECUTE IMMEDIATE
                'ALTER TABLE D_Admin ADD CONSTRAINT CK_D_ADMIN_ROLE ' ||
                'CHECK (Role_Level IN (''超级管理员'', ''楼长'', ''维修员'', ''辅导员''))';
        END IF;
    END IF;
END;
/

-- 验证：应返回 1 行，SEARCH_CONDITION_VC 含"辅导员"
-- SELECT constraint_name, search_condition_vc FROM user_constraints WHERE constraint_name = 'CK_D_ADMIN_ROLE';
