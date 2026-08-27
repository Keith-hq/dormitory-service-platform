-- 扩展表迁移 039：D_Violation_Record 补 Status 列（有效/已撤销）
-- 2026-08-27 定稿。
-- 用途：信用申诉复核通过后，把对应违规记录自动标记为「已撤销」，
--       超管治理页可见状态，避免"申诉通过但违规仍显有效"的数据不一致。
-- 幂等：列存在性判断后 ADD，重复执行安全。
-- 执行：DBeaver 整段执行；全量重建时位于 extensions/038 之后。

DECLARE
    v_cnt NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_cnt FROM user_tab_columns
     WHERE table_name = 'D_VIOLATION_RECORD' AND column_name = 'STATUS';
    IF v_cnt = 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE D_Violation_Record ADD (Status VARCHAR2(20))';
    END IF;
END;
/

-- 已有记录回填为「有效」
UPDATE D_Violation_Record SET Status = '有效' WHERE Status IS NULL;
COMMIT;
