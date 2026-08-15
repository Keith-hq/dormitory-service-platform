-- 扩展表迁移 023：PR #49 一审整改（C-040 裁定）
--   D_Room 楼栋内房间号唯一索引 + D_Bed_Allocation / D_Checkout_Log /
--   D_Leave_Application 主键序列与触发器。
--
-- 在已有环境上执行于 ddl/extensions/022_fee_detail_dedup_uk.sql 之后；
-- 全量重建时执行顺序为 foundation/001 -> extensions/010 -> ... -> extensions/023。
-- 编号避让：022 已占用（fee_detail_dedup_uk），024 预留给 PR #52。
--
-- 为什么补（C-040 / C-032 延伸）？
--   1. DORM-06 批量初始化幂等：D_Room 加 (Building_ID, Room_Number) 唯一约束，
--      作为 DB 级幂等兜底；应用层并发同键插入命中 ORA-00001 后应转“已存在房间跳过”。
--   2. D_Bed_Allocation / D_Checkout_Log / D_Leave_Application 首次写入方为
--      PR #49（住宿分配/退宿清算/离校报备），当前主键无序列，应用层使用
--      MAX+1 + DbSaveRetry 存在并发撞号窗口。统一补“序列 + 触发器”
--      （020 模式：MAXVALUE 上限 + WHEN NEW IS NULL 兼容），后续应用层可平滑
--      切换为 EF ValueGeneratedOnAdd / 直接使用 NEXTVAL。
--
-- 执行方式（DBeaver，JDBC 连接）：整段选中执行（匿名块，幂等写法）。
-- 验证：由数据库负责人按需补充 verify 段（当前 PR 暂不添加）。

-- =====================================================================
-- Part A：D_Room 唯一约束 UK_D_ROOM_BUILDING_NO (Building_ID, Room_Number)
-- =====================================================================
-- 先做存量冲突检查：存在重复房间号则终止，避免 ADD CONSTRAINT 直接 ORA-02299。
DECLARE
    v_dup NUMBER;
BEGIN
    SELECT COUNT(*)
      INTO v_dup
      FROM (
          SELECT Building_ID, Room_Number
            FROM D_Room
           GROUP BY Building_ID, Room_Number
          HAVING COUNT(*) > 1
      );

    IF v_dup > 0 THEN
        RAISE_APPLICATION_ERROR(-20023,
            'D_Room 存在重复 (Building_ID, Room_Number) 共 ' || v_dup
            || ' 组，请先人工归并后再执行迁移 023');
    END IF;
END;
/

DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*)
      INTO v_exists
      FROM USER_CONSTRAINTS
     WHERE Constraint_Name = 'UK_D_ROOM_BUILDING_NO'
       AND Table_Name = 'D_ROOM';

    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE
            'ALTER TABLE D_Room ADD CONSTRAINT UK_D_ROOM_BUILDING_NO ' ||
            'UNIQUE (Building_ID, Room_Number)';
    END IF;
END;
/

-- =====================================================================
-- Part B：D_Bed_Allocation 主键序列与触发器
-- =====================================================================
DECLARE
    v_exists NUMBER;
    v_max_id NUMBER;
    v_start_with NUMBER;
BEGIN
    SELECT NVL(MAX(Allocation_ID), 0)
      INTO v_max_id
      FROM D_Bed_Allocation;

    IF v_max_id >= 9999999999 THEN
        RAISE_APPLICATION_ERROR(-20020, 'D_Bed_Allocation.Allocation_ID 已触达 NUMBER(10) 上限');
    END IF;

    SELECT COUNT(*)
      INTO v_exists
      FROM USER_SEQUENCES
     WHERE SEQUENCE_NAME = 'SEQ_D_BED_ALLOCATION_ID';

    IF v_exists = 0 THEN
        v_start_with := v_max_id + 1;
        EXECUTE IMMEDIATE
            'CREATE SEQUENCE SEQ_D_BED_ALLOCATION_ID START WITH ' || v_start_with ||
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
     WHERE TRIGGER_NAME = 'TRG_D_BED_ALLOCATION_ID_BI';

    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE
            'CREATE TRIGGER TRG_D_BED_ALLOCATION_ID_BI ' ||
            'BEFORE INSERT ON D_Bed_Allocation ' ||
            'FOR EACH ROW ' ||
            'WHEN (NEW.Allocation_ID IS NULL) ' ||
            'BEGIN ' ||
            '    SELECT SEQ_D_BED_ALLOCATION_ID.NEXTVAL ' ||
            '      INTO :NEW.Allocation_ID ' ||
            '      FROM dual; ' ||
            'END;';
    END IF;
END;
/

-- =====================================================================
-- Part C：D_Checkout_Log 主键序列与触发器
-- =====================================================================
DECLARE
    v_exists NUMBER;
    v_max_id NUMBER;
    v_start_with NUMBER;
BEGIN
    SELECT NVL(MAX(Log_ID), 0)
      INTO v_max_id
      FROM D_Checkout_Log;

    IF v_max_id >= 9999999999 THEN
        RAISE_APPLICATION_ERROR(-20020, 'D_Checkout_Log.Log_ID 已触达 NUMBER(10) 上限');
    END IF;

    SELECT COUNT(*)
      INTO v_exists
      FROM USER_SEQUENCES
     WHERE SEQUENCE_NAME = 'SEQ_D_CHECKOUT_LOG_ID';

    IF v_exists = 0 THEN
        v_start_with := v_max_id + 1;
        EXECUTE IMMEDIATE
            'CREATE SEQUENCE SEQ_D_CHECKOUT_LOG_ID START WITH ' || v_start_with ||
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
     WHERE TRIGGER_NAME = 'TRG_D_CHECKOUT_LOG_ID_BI';

    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE
            'CREATE TRIGGER TRG_D_CHECKOUT_LOG_ID_BI ' ||
            'BEFORE INSERT ON D_Checkout_Log ' ||
            'FOR EACH ROW ' ||
            'WHEN (NEW.Log_ID IS NULL) ' ||
            'BEGIN ' ||
            '    SELECT SEQ_D_CHECKOUT_LOG_ID.NEXTVAL ' ||
            '      INTO :NEW.Log_ID ' ||
            '      FROM dual; ' ||
            'END;';
    END IF;
END;
/

-- =====================================================================
-- Part D：D_Leave_Application 主键序列与触发器
-- =====================================================================
DECLARE
    v_exists NUMBER;
    v_max_id NUMBER;
    v_start_with NUMBER;
BEGIN
    SELECT NVL(MAX(Apply_ID), 0)
      INTO v_max_id
      FROM D_Leave_Application;

    IF v_max_id >= 9999999999 THEN
        RAISE_APPLICATION_ERROR(-20020, 'D_Leave_Application.Apply_ID 已触达 NUMBER(10) 上限');
    END IF;

    SELECT COUNT(*)
      INTO v_exists
      FROM USER_SEQUENCES
     WHERE SEQUENCE_NAME = 'SEQ_D_LEAVE_APPLICATION_ID';

    IF v_exists = 0 THEN
        v_start_with := v_max_id + 1;
        EXECUTE IMMEDIATE
            'CREATE SEQUENCE SEQ_D_LEAVE_APPLICATION_ID START WITH ' || v_start_with ||
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
     WHERE TRIGGER_NAME = 'TRG_D_LEAVE_APPLICATION_ID_BI';

    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE
            'CREATE TRIGGER TRG_D_LEAVE_APPLICATION_ID_BI ' ||
            'BEFORE INSERT ON D_Leave_Application ' ||
            'FOR EACH ROW ' ||
            'WHEN (NEW.Apply_ID IS NULL) ' ||
            'BEGIN ' ||
            '    SELECT SEQ_D_LEAVE_APPLICATION_ID.NEXTVAL ' ||
            '      INTO :NEW.Apply_ID ' ||
            '      FROM dual; ' ||
            'END;';
    END IF;
END;
/
