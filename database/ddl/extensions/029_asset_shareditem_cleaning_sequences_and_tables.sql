-- 扩展表迁移 029：资产管理 / 共享物品主数据 / 保洁任务 的数据库前置。
-- 包含五部分：
--   1) D_Asset / D_Shared_Item / D_Cleaning_Task 主键序列与触发器
--      （三表基线均无主键生成器，直接 INSERT 会 ORA-01400，参照迁移 020 模式补齐）；
--   2) 新建扩展表 D_Asset_Repair（资产↔报修工单关联，不触碰冻结的 D_Repair_Ticket）；
--   3) 新建扩展表 D_Asset_Warning（损耗预警与处理痕迹，不触碰冻结的 D_Asset）；
--   4) D_Shared_Item 新增 DESCRIPTION 列（契约 DORM-48/49 的 description 字段落库）；
--   5) 上述两张新表的主键序列与触发器。
--
-- 编号说明：028 已被其他成员占用（尚未上传），本次使用 029。
-- 在已有环境上执行于 ddl/extensions/027_add_admin_post.sql 之后；
-- 全量重建时执行顺序为 foundation/001 -> extensions/010~027 -> extensions/029。
-- 本脚本不修改 / 不重建任何基线表（foundation 20 张冻结）。
--
-- 执行方式（DBeaver，JDBC 连接）：整段复制后执行。
--   - 序列 / 触发器均为幂等写法（已存在的对象被跳过，重复执行无害）；
--   - CREATE TABLE / ALTER TABLE 为 DDL，重复执行会报 ORA-00955 / ORA-01430，属预期，
--     首次执行成功后无需重跑。
--
-- 验证：重跑 database/verify/extension_schema_checks.sql，
--   确认第 22 部分每段 SELECT 均返回对应行数。


-- =====================================================================
-- 1) 主键序列与触发器：D_Asset / D_Shared_Item / D_Cleaning_Task
--    幂等匿名块 + NUMBER(10) 上限防护 + MAX(Id)+1 起点（同迁移 020 先例）。
-- =====================================================================

-- ===== D_Asset =====
DECLARE
    v_exists NUMBER;
    v_max_id NUMBER;
    v_start_with NUMBER;
BEGIN
    SELECT NVL(MAX(Asset_ID), 0)
      INTO v_max_id
      FROM D_Asset;

    IF v_max_id >= 9999999999 THEN
        RAISE_APPLICATION_ERROR(-20020, 'D_Asset.Asset_ID 已触达 NUMBER(10) 上限');
    END IF;

    SELECT COUNT(*)
      INTO v_exists
      FROM USER_SEQUENCES
     WHERE SEQUENCE_NAME = 'SEQ_D_ASSET_ID';

    IF v_exists = 0 THEN
        v_start_with := v_max_id + 1;
        EXECUTE IMMEDIATE
            'CREATE SEQUENCE SEQ_D_ASSET_ID START WITH ' || v_start_with ||
            ' INCREMENT BY 1 MAXVALUE 9999999999 NOCACHE';
    END IF;
END;

DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*)
      INTO v_exists
      FROM USER_TRIGGERS
     WHERE TRIGGER_NAME = 'TRG_D_ASSET_ID_BI';

    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE
            'CREATE TRIGGER TRG_D_ASSET_ID_BI ' ||
            'BEFORE INSERT ON D_Asset ' ||
            'FOR EACH ROW ' ||
            'WHEN (NEW.Asset_ID IS NULL) ' ||
            'BEGIN ' ||
            '    SELECT SEQ_D_ASSET_ID.NEXTVAL ' ||
            '      INTO :NEW.Asset_ID ' ||
            '      FROM dual; ' ||
            'END;';
    END IF;
END;

-- ===== D_Shared_Item =====
DECLARE
    v_exists NUMBER;
    v_max_id NUMBER;
    v_start_with NUMBER;
BEGIN
    SELECT NVL(MAX(Item_ID), 0)
      INTO v_max_id
      FROM D_Shared_Item;

    IF v_max_id >= 9999999999 THEN
        RAISE_APPLICATION_ERROR(-20020, 'D_Shared_Item.Item_ID 已触达 NUMBER(10) 上限');
    END IF;

    SELECT COUNT(*)
      INTO v_exists
      FROM USER_SEQUENCES
     WHERE SEQUENCE_NAME = 'SEQ_D_SHARED_ITEM_ID';

    IF v_exists = 0 THEN
        v_start_with := v_max_id + 1;
        EXECUTE IMMEDIATE
            'CREATE SEQUENCE SEQ_D_SHARED_ITEM_ID START WITH ' || v_start_with ||
            ' INCREMENT BY 1 MAXVALUE 9999999999 NOCACHE';
    END IF;
END;

DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*)
      INTO v_exists
      FROM USER_TRIGGERS
     WHERE TRIGGER_NAME = 'TRG_D_SHARED_ITEM_ID_BI';

    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE
            'CREATE TRIGGER TRG_D_SHARED_ITEM_ID_BI ' ||
            'BEFORE INSERT ON D_Shared_Item ' ||
            'FOR EACH ROW ' ||
            'WHEN (NEW.Item_ID IS NULL) ' ||
            'BEGIN ' ||
            '    SELECT SEQ_D_SHARED_ITEM_ID.NEXTVAL ' ||
            '      INTO :NEW.Item_ID ' ||
            '      FROM dual; ' ||
            'END;';
    END IF;
END;

-- ===== D_Cleaning_Task =====
DECLARE
    v_exists NUMBER;
    v_max_id NUMBER;
    v_start_with NUMBER;
BEGIN
    SELECT NVL(MAX(Task_ID), 0)
      INTO v_max_id
      FROM D_Cleaning_Task;

    IF v_max_id >= 9999999999 THEN
        RAISE_APPLICATION_ERROR(-20020, 'D_Cleaning_Task.Task_ID 已触达 NUMBER(10) 上限');
    END IF;

    SELECT COUNT(*)
      INTO v_exists
      FROM USER_SEQUENCES
     WHERE SEQUENCE_NAME = 'SEQ_D_CLEANING_TASK_ID';

    IF v_exists = 0 THEN
        v_start_with := v_max_id + 1;
        EXECUTE IMMEDIATE
            'CREATE SEQUENCE SEQ_D_CLEANING_TASK_ID START WITH ' || v_start_with ||
            ' INCREMENT BY 1 MAXVALUE 9999999999 NOCACHE';
    END IF;
END;

DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*)
      INTO v_exists
      FROM USER_TRIGGERS
     WHERE TRIGGER_NAME = 'TRG_D_CLEANING_TASK_ID_BI';

    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE
            'CREATE TRIGGER TRG_D_CLEANING_TASK_ID_BI ' ||
            'BEFORE INSERT ON D_Cleaning_Task ' ||
            'FOR EACH ROW ' ||
            'WHEN (NEW.Task_ID IS NULL) ' ||
            'BEGIN ' ||
            '    SELECT SEQ_D_CLEANING_TASK_ID.NEXTVAL ' ||
            '      INTO :NEW.Task_ID ' ||
            '      FROM dual; ' ||
            'END;';
    END IF;
END;


-- =====================================================================
-- 2) 新建扩展表 D_Asset_Repair（资产 ↔ 报修工单关联）
--    D_Repair_Ticket 属 foundation 冻结表、无 Asset_ID 列，故用扩展表记录
--    资产与工单的关联；应用层据此实现 DORM-16「防重复工单」与
--    DORM-14-delete「已关联报修的资产禁止删除」。
-- =====================================================================

CREATE TABLE D_Asset_Repair (
    Link_ID NUMBER(10) CONSTRAINT PK_D_ASSET_REPAIR PRIMARY KEY,
    Asset_ID NUMBER(10) NOT NULL,
    Ticket_ID NUMBER(10) NOT NULL,
    Create_Time DATE DEFAULT SYSDATE NOT NULL,
    CONSTRAINT FK_D_ASSET_REPAIR_ASSET
        FOREIGN KEY (Asset_ID) REFERENCES D_Asset (Asset_ID),
    CONSTRAINT FK_D_ASSET_REPAIR_TICKET
        FOREIGN KEY (Ticket_ID) REFERENCES D_Repair_Ticket (Ticket_ID)
);

CREATE INDEX IDX_D_ASSET_REPAIR_ASSET
    ON D_Asset_Repair (Asset_ID, Ticket_ID);

-- ===== D_Asset_Repair 主键序列与触发器 =====
DECLARE
    v_exists NUMBER;
    v_max_id NUMBER;
    v_start_with NUMBER;
BEGIN
    SELECT NVL(MAX(Link_ID), 0)
      INTO v_max_id
      FROM D_Asset_Repair;

    IF v_max_id >= 9999999999 THEN
        RAISE_APPLICATION_ERROR(-20020, 'D_Asset_Repair.Link_ID 已触达 NUMBER(10) 上限');
    END IF;

    SELECT COUNT(*)
      INTO v_exists
      FROM USER_SEQUENCES
     WHERE SEQUENCE_NAME = 'SEQ_D_ASSET_REPAIR_ID';

    IF v_exists = 0 THEN
        v_start_with := v_max_id + 1;
        EXECUTE IMMEDIATE
            'CREATE SEQUENCE SEQ_D_ASSET_REPAIR_ID START WITH ' || v_start_with ||
            ' INCREMENT BY 1 MAXVALUE 9999999999 NOCACHE';
    END IF;
END;

DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*)
      INTO v_exists
      FROM USER_TRIGGERS
     WHERE TRIGGER_NAME = 'TRG_D_ASSET_REPAIR_ID_BI';

    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE
            'CREATE TRIGGER TRG_D_ASSET_REPAIR_ID_BI ' ||
            'BEFORE INSERT ON D_Asset_Repair ' ||
            'FOR EACH ROW ' ||
            'WHEN (NEW.Link_ID IS NULL) ' ||
            'BEGIN ' ||
            '    SELECT SEQ_D_ASSET_REPAIR_ID.NEXTVAL ' ||
            '      INTO :NEW.Link_ID ' ||
            '      FROM dual; ' ||
            'END;';
    END IF;
END;


-- =====================================================================
-- 3) 新建扩展表 D_Asset_Warning（损耗预警与处理痕迹）
--    D_Asset 属 foundation 冻结表、无预警列，故用扩展表记录预警的
--    产生（DORM-16 损坏转报修时落一条 Handled='否'）与处理（DORM-18）：
--    处理后将 Handled 置为 '是' 退出预警列表；再次损坏产生新预警行重新进入。
-- =====================================================================

CREATE TABLE D_Asset_Warning (
    Warning_ID NUMBER(10) CONSTRAINT PK_D_ASSET_WARNING PRIMARY KEY,
    Asset_ID NUMBER(10) NOT NULL,
    Create_Time DATE DEFAULT SYSDATE NOT NULL,
    Note VARCHAR2(500),
    Handle_Action VARCHAR2(20),
    Handle_Time DATE,
    Handled VARCHAR2(10) DEFAULT '否' NOT NULL,
    CONSTRAINT CK_D_ASSET_WARNING_STATUS
        CHECK (Handled IN ('是', '否')),
    CONSTRAINT CK_D_ASSET_WARNING_ACTION
        CHECK (Handle_Action IS NULL OR Handle_Action IN ('处理', '标记重点')),
    CONSTRAINT CK_D_ASSET_WARNING_DONE
        CHECK (Handled = '否' OR (Handle_Action IS NOT NULL AND Handle_Time IS NOT NULL)),
    CONSTRAINT FK_D_ASSET_WARNING_ASSET
        FOREIGN KEY (Asset_ID) REFERENCES D_Asset (Asset_ID)
);

CREATE INDEX IDX_D_ASSET_WARNING_ACTIVE
    ON D_Asset_Warning (Asset_ID, Handled);

-- ===== D_Asset_Warning 主键序列与触发器 =====
DECLARE
    v_exists NUMBER;
    v_max_id NUMBER;
    v_start_with NUMBER;
BEGIN
    SELECT NVL(MAX(Warning_ID), 0)
      INTO v_max_id
      FROM D_Asset_Warning;

    IF v_max_id >= 9999999999 THEN
        RAISE_APPLICATION_ERROR(-20020, 'D_Asset_Warning.Warning_ID 已触达 NUMBER(10) 上限');
    END IF;

    SELECT COUNT(*)
      INTO v_exists
      FROM USER_SEQUENCES
     WHERE SEQUENCE_NAME = 'SEQ_D_ASSET_WARNING_ID';

    IF v_exists = 0 THEN
        v_start_with := v_max_id + 1;
        EXECUTE IMMEDIATE
            'CREATE SEQUENCE SEQ_D_ASSET_WARNING_ID START WITH ' || v_start_with ||
            ' INCREMENT BY 1 MAXVALUE 9999999999 NOCACHE';
    END IF;
END;

DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*)
      INTO v_exists
      FROM USER_TRIGGERS
     WHERE TRIGGER_NAME = 'TRG_D_ASSET_WARNING_ID_BI';

    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE
            'CREATE TRIGGER TRG_D_ASSET_WARNING_ID_BI ' ||
            'BEFORE INSERT ON D_Asset_Warning ' ||
            'FOR EACH ROW ' ||
            'WHEN (NEW.Warning_ID IS NULL) ' ||
            'BEGIN ' ||
            '    SELECT SEQ_D_ASSET_WARNING_ID.NEXTVAL ' ||
            '      INTO :NEW.Warning_ID ' ||
            '      FROM dual; ' ||
            'END;';
    END IF;
END;


-- =====================================================================
-- 4) D_Shared_Item 新增 DESCRIPTION 列（契约 DORM-48/49 description 字段）
--    D_Shared_Item 属扩展表，可新增列；不改既有字段语义。
--    实例为 BYTE 语义，须显式 CHAR 长度（同 013/017/018/019 迁移先例）。
-- =====================================================================

ALTER TABLE D_Shared_Item
    ADD DESCRIPTION VARCHAR2(200 CHAR);
