-- 扩展表迁移 037：C6 违规登记实现（VIOL-01 解除 501 占位）
-- 2026-08-26 定稿。
-- 用途：D_Violation_Record 补 Detail / Record_By 列 + 主键序列 SEQ_D_VIOLATION，
--       使 POST /api/violations 能真实落库（原 ViolationRepository 为 501 占位）。
-- 幂等：列与序列均先查存在性再 DDL，重复执行安全。
-- 执行：DBeaver 整段执行；全量重建时位于 extensions/036 之后。

-- =====================================================================
-- Part A：补列（幂等）
-- =====================================================================
DECLARE
    v_cnt NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_cnt FROM user_tab_columns
     WHERE table_name = 'D_VIOLATION_RECORD' AND column_name = 'DETAIL';
    IF v_cnt = 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE D_Violation_Record ADD (Detail VARCHAR2(500))';
    END IF;

    SELECT COUNT(*) INTO v_cnt FROM user_tab_columns
     WHERE table_name = 'D_VIOLATION_RECORD' AND column_name = 'RECORD_BY';
    IF v_cnt = 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE D_Violation_Record ADD (Record_By VARCHAR2(20))';
    END IF;
END;
/

-- =====================================================================
-- Part B：主键序列（起点取 MAX(Record_ID)+1，避免与种子 9xxxxx 撞号）
-- =====================================================================
DECLARE
    v_StartVal NUMBER;
BEGIN
    SELECT NVL(MAX(Record_ID), 0) + 1 INTO v_StartVal FROM D_Violation_Record;
    EXECUTE IMMEDIATE 'CREATE SEQUENCE SEQ_D_VIOLATION START WITH ' || v_StartVal || ' INCREMENT BY 1 NOCACHE';
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE = -955 THEN NULL;   -- 序列已存在，跳过
        ELSE RAISE;
        END IF;
END;
/
