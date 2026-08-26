-- 扩展表迁移 038：D_Building / D_Room / D_Asset 主键序列/触发器兜底
-- （修复"新增楼栋 500：无 ID 生成机制"，2026-08-26）。
--
-- 根因：SEQ_D_BUILDING / TRG_D_BUILDING_ID_BI 等只在 init-db.sh [5/7]
--       （全量初始化路径）创建，不在迁移文件里 → 云端增量部署（migrate-db.sh）
--       只跑差集，缺失对象永不补建 → EF 直插（POST /buildings）省略主键列
--       → 插 NULL → ORA-01400 → 500。与 036 事故同源（036 已覆盖通知/报修/
--       晚归等，但未覆盖楼栋/房间/资产三组）。
--
-- 本脚本幂等：对象已存在即跳过，重复执行安全；任何一段失败会让
-- migrate-db.sh 中止（与 036 同款最小防御写法）。
--
-- 验证：重跑 database/verify/extension_schema_checks.sql（如涉及）。

-- =====================================================================
-- Part A：D_Building（SEQ_D_BUILDING + TRG_D_BUILDING_ID_BI）
-- =====================================================================
DECLARE
    v_exists NUMBER;
    v_start  NUMBER;
BEGIN
    SELECT NVL(MAX(Building_ID), 0) + 1 INTO v_start FROM D_Building;

    SELECT COUNT(*) INTO v_exists FROM user_sequences WHERE sequence_name = 'SEQ_D_BUILDING';
    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE 'CREATE SEQUENCE SEQ_D_BUILDING START WITH ' || v_start || ' INCREMENT BY 1 NOCACHE';
    END IF;

    SELECT COUNT(*) INTO v_exists FROM user_triggers WHERE trigger_name = 'TRG_D_BUILDING_ID_BI';
    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE 'CREATE TRIGGER TRG_D_BUILDING_ID_BI BEFORE INSERT ON D_Building ' ||
                          'FOR EACH ROW WHEN (NEW.Building_ID IS NULL) ' ||
                          'BEGIN SELECT SEQ_D_BUILDING.NEXTVAL INTO :NEW.Building_ID FROM dual; END;';
    END IF;
END;
/

-- =====================================================================
-- Part B：D_Room（SEQ_D_ROOM + TRG_D_ROOM_ID_BI）
-- =====================================================================
DECLARE
    v_exists NUMBER;
    v_start  NUMBER;
BEGIN
    SELECT NVL(MAX(Room_ID), 0) + 1 INTO v_start FROM D_Room;

    SELECT COUNT(*) INTO v_exists FROM user_sequences WHERE sequence_name = 'SEQ_D_ROOM';
    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE 'CREATE SEQUENCE SEQ_D_ROOM START WITH ' || v_start || ' INCREMENT BY 1 NOCACHE';
    END IF;

    SELECT COUNT(*) INTO v_exists FROM user_triggers WHERE trigger_name = 'TRG_D_ROOM_ID_BI';
    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE 'CREATE TRIGGER TRG_D_ROOM_ID_BI BEFORE INSERT ON D_Room ' ||
                          'FOR EACH ROW WHEN (NEW.Room_ID IS NULL) ' ||
                          'BEGIN SELECT SEQ_D_ROOM.NEXTVAL INTO :NEW.Room_ID FROM dual; END;';
    END IF;
END;
/

-- =====================================================================
-- Part C：D_Asset（SEQ_D_ASSET + TRG_D_ASSET_ID_BI）
-- =====================================================================
DECLARE
    v_exists NUMBER;
    v_start  NUMBER;
BEGIN
    SELECT NVL(MAX(Asset_ID), 0) + 1 INTO v_start FROM D_Asset;

    SELECT COUNT(*) INTO v_exists FROM user_sequences WHERE sequence_name = 'SEQ_D_ASSET';
    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE 'CREATE SEQUENCE SEQ_D_ASSET START WITH ' || v_start || ' INCREMENT BY 1 NOCACHE';
    END IF;

    SELECT COUNT(*) INTO v_exists FROM user_triggers WHERE trigger_name = 'TRG_D_ASSET_ID_BI';
    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE 'CREATE TRIGGER TRG_D_ASSET_ID_BI BEFORE INSERT ON D_Asset ' ||
                          'FOR EACH ROW WHEN (NEW.Asset_ID IS NULL) ' ||
                          'BEGIN SELECT SEQ_D_ASSET.NEXTVAL INTO :NEW.Asset_ID FROM dual; END;';
    END IF;
END;
/
