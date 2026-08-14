-- 扩展表迁移 022：难点⑥ 一审 R2 —— D_Fee_Detail 两入口防重唯一索引收紧。
-- 2026-08-14 定稿（随 SP_Calc_Checkout_Fee / SP_Calc_Monthly_Fee v1.3 发布）。
-- 在已有环境上执行于 ddl/extensions/021_sla_dispatch.sql 之后；
-- 全量重建时执行顺序为 foundation/001 -> extensions/010 -> ... -> extensions/022。
--
-- 为什么改（一审 R2，IT-C10-004 并发竞态）？
--   基线 010 的 UK_D_FEE_DETAIL 是 UNIQUE (Fee_ID, Student_ID, Bill_Type)：
--   同一 (Fee_ID, Student_ID) 允许同时存在 '月度' 与 '退宿' 两行，与 SP v1.2
--   的"两入口互斥"语义矛盾。SP 的 COUNT 预检查存在并发窗口（退宿结算与
--   月度批量任务同时读、同时插），竞态下同一学生同一笔费用会被扣两次。
--   裁决：唯一索引收紧为 (Fee_ID, Student_ID)——月度/退宿互斥由 DB 唯一性
--   兜底，SP 内 DUP_VAL_ON_INDEX 静默跳过成为并发安全网。
--
-- 执行前提（重要）：
--   本脚本 DROP 旧约束前会先做"存量冲突检查"（同一 Fee_ID+Student_ID 同时
--   存在 '月度' 与 '退宿' 明细）。若存量数据存在冲突（v1.2 之前的历史
--   重复扣款数据），脚本会 RAISE 终止，需人工归并后再执行：
--     SELECT Fee_ID, Student_ID
--       FROM D_Fee_Detail
--      WHERE Bill_Type IN ('月度', '退宿')
--      GROUP BY Fee_ID, Student_ID
--     HAVING COUNT(DISTINCT Bill_Type) > 1;
--   归并口径由数据库负责人裁定（保留一条、删除重复并补记流水）。
--
-- 执行方式（DBeaver，JDBC 连接）：整段选中执行（三段式匿名块）。
-- 幂等说明：约束的 DROP/ADD 均先查 USER_CONSTRAINTS，已按新口径建好则
--   跳过；重复执行安全。
--
-- 验证：执行后查询
--   SELECT Constraint_Name, Index_Name FROM USER_CONSTRAINTS
--    WHERE Table_Name = 'D_FEE_DETAIL' AND Constraint_Type = 'U';
--   应只剩 1 行，Index_Name 为 UK_D_FEE_DETAIL（列序 Fee_ID, Student_ID）。

-- =====================================================================
-- Part A：存量冲突检查（有冲突即终止，不执行后续 DDL）
-- =====================================================================
DECLARE
    v_conflict NUMBER;
BEGIN
    SELECT COUNT(*)
      INTO v_conflict
      FROM (
          SELECT 1
            FROM D_Fee_Detail
           WHERE Bill_Type IN ('月度', '退宿')
           GROUP BY Fee_ID, Student_ID
          HAVING COUNT(DISTINCT Bill_Type) > 1
      );

    IF v_conflict > 0 THEN
        RAISE_APPLICATION_ERROR(-20021,
            'D_Fee_Detail 存在同一 (Fee_ID, Student_ID) 的月度/退宿并存数据（'
            || v_conflict || ' 组），请按文件头说明人工归并后再执行本迁移');
    END IF;
END;
/

-- =====================================================================
-- Part B：删除旧 UK (Fee_ID, Student_ID, Bill_Type)
-- =====================================================================
DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*)
      INTO v_exists
      FROM USER_CONSTRAINTS
     WHERE Constraint_Name = 'UK_D_FEE_DETAIL'
       AND Table_Name = 'D_FEE_DETAIL';

    IF v_exists > 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE D_Fee_Detail DROP CONSTRAINT UK_D_FEE_DETAIL';
    END IF;
END;
/

-- =====================================================================
-- Part C：建立新 UK (Fee_ID, Student_ID)——两入口互斥的唯一性兜底
-- =====================================================================
DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*)
      INTO v_exists
      FROM USER_CONSTRAINTS
     WHERE Constraint_Name = 'UK_D_FEE_DETAIL'
       AND Table_Name = 'D_FEE_DETAIL';

    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE
            'ALTER TABLE D_Fee_Detail ADD CONSTRAINT UK_D_FEE_DETAIL ' ||
            'UNIQUE (Fee_ID, Student_ID)';
    END IF;
END;
/
