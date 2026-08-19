-- 扩展表迁移 035：修复字节语义列导致的中文超长 ORA-12899。
-- 2026-08-18 公共服务集成测试现场发现（D1/D2）：
--   D1：SP_Auto_Deduct 写 '余额不足'（4 汉字 = 12 字节）到
--       D_Fee_Deduction_Attempt.RESULT（VARCHAR2(10 BYTE)）→ ORA-12899，
--       导致 C3-003 定时自动划扣接口 500。
--   D2：AuditEventFilter 写 {METHOD} {PATH} 到 D_Audit_Event.EVENT_TYPE，
--       以及文件 ref 路径到 TARGET_ID；二者原为 VARCHAR2(50 BYTE)，
--       DELETE /api/internal/files/<yyyy>/<MM>/<guid>.png 路径超长 → ORA-12899，
--       导致 C7-002 删除接口 500（文件实际已删除）。
-- 处理：改为显式 CHAR 语义并加长（与 013/017/018/019/027c 先例一致）。
-- 执行方式：匿名块整体执行（幂等），重复执行无害。

DECLARE
    v_cnt NUMBER;
BEGIN
    -- D1：扣款尝试结果列
    SELECT COUNT(*) INTO v_cnt
      FROM USER_TAB_COLUMNS
     WHERE TABLE_NAME = 'D_FEE_DEDUCTION_ATTEMPT'
       AND COLUMN_NAME = 'RESULT'
       AND (CHAR_USED = 'B' OR CHAR_LENGTH < 20);
    IF v_cnt > 0 THEN
        EXECUTE IMMEDIATE
            'ALTER TABLE D_Fee_Deduction_Attempt MODIFY (Result VARCHAR2(20 CHAR))';
    END IF;

    -- D2a：审计事件类型列（含 METHOD + PATH）
    SELECT COUNT(*) INTO v_cnt
      FROM USER_TAB_COLUMNS
     WHERE TABLE_NAME = 'D_AUDIT_EVENT'
       AND COLUMN_NAME = 'EVENT_TYPE'
       AND (CHAR_USED = 'B' OR CHAR_LENGTH < 200);
    IF v_cnt > 0 THEN
        EXECUTE IMMEDIATE
            'ALTER TABLE D_Audit_Event MODIFY (Event_Type VARCHAR2(200 CHAR))';
    END IF;

    -- D2b：审计目标 ID 列（文件 ref 等长路径）
    SELECT COUNT(*) INTO v_cnt
      FROM USER_TAB_COLUMNS
     WHERE TABLE_NAME = 'D_AUDIT_EVENT'
       AND COLUMN_NAME = 'TARGET_ID'
       AND (CHAR_USED = 'B' OR CHAR_LENGTH < 200);
    IF v_cnt > 0 THEN
        EXECUTE IMMEDIATE
            'ALTER TABLE D_Audit_Event MODIFY (Target_ID VARCHAR2(200 CHAR))';
    END IF;
END;
/
