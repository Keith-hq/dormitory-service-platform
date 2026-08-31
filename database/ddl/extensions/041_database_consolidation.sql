-- 扩展表迁移 041：数据库表整理（废表删除、范式治理、1:1 卫星表合并）。
-- 依据 ADR-002（2026-08-31 裁决）：表基线 47 → 42（foundation 20→17，extension 27→25）。
-- 在已有环境上执行于 ddl/extensions/040_repair_claim_time.sql 之后；
-- 全量重建时顺序为 foundation/001 -> extensions/010 -> … -> 040 -> 041 -> sp/*。
--
-- 内容（五段）：
--   ① 卫星表合并：D_Notice 加 Is_Pinned/Pin_Time（枚举 CHECK 随迁自
--      CK_D_NOTICE_DISPLAY_PIN）；D_Hygiene_Record 加 "COMMENT"；数据搬移后
--      DROP D_Notice_Display / D_Hygiene_Comment（连带 015 的
--      SEQ_D_NOTICE_DISPLAY_PK / TRG_D_NOTICE_DISPLAY_PK_BI）。
--   ② D_Fee_Detail 范式刀：删 Total_Days / Room_ID（C-012 快照裁决被本轮
--      覆盖：房间归属唯一载体为 D_Utility_Fee.Room_ID）。CK_D_FEE_DETAIL_DAYS
--      先按谓词文本判定（含 TOTAL_DAYS 的旧谓词先 DROP），删列后重建为仅
--      Stay_Days > 0。连带删除 FK_D_FEE_DETAIL_ROOM。
--   ③ 散列删除：D_Credit_Appeal.Student_ID（先显式 DROP 复合索引
--      IDX_D_CREDIT_APPEAL_STUDENT_TIME，再删 FK_D_CREDIT_APPEAL_STUDENT；
--      学生归属经 Credit_Log_ID→D_Credit_Log.Student_ID 联查）；
--      D_Utility_Fee.Is_Paid（建单后无人维护的烂值死列，缴费状态唯一事实
--      来源为 D_Fee_Detail.Is_Paid）；D_Visitor_Registry.Create_Time（与
--      Enter_Time 同为 SYSDATE 默认，冗余）。
--   ④ 废表删除（C-048"技术基线保留"口径作废）：D_Parcel_Record（连带 024
--      的 SEQ_D_PARCEL_RECORD_ID / TRG_D_PARCEL_RECORD_ID_BI）、
--      D_Visitor_Log、D_Water_Order。三者均无入向外键、无业务代码引用。
--   ⑤ 重编译失效对象 + 硬断言自检（verify 两脚本为软报表，不拦假绿，
--      终态断言必须落在本迁移内）。
--
-- 幂等与重跑：每段均为"查字典→不存在才做"守卫（含 ② 按
-- USER_CONSTRAINTS.SEARCH_CONDITION_VC 谓词文本区分旧/新 CHECK 的中间态），
-- 半途失败可从文件头整体重跑，重复执行无害。migrate-db.sh 记账粒度为
-- 整文件：041 永久失败会卡住其后编号迁移与部署，修复后重跑本文件即可，
-- 不要手工往 D_APP_MIGRATION 插记录。
--
-- 与 SP 的顺序：migrate-db.sh [5/6] 在迁移之后恒重跑 sp/*.sql；仓库内
-- sp_fee_sharing / sp_billing 已同步为不引用被删列的版本，二者必须同 PR。
-- DDL 生效到 SP 重跑之间的窗口内不要触发划扣/分摊任务（部署停调度覆盖）。
--
-- 执行方式：DBeaver（JDBC）整文件执行，或 sqlplus 按 @ 调用；匿名块以 "/" 结束。
-- 验证：重跑 database/verify/foundation_schema_checks.sql（17 表）与
--       extension_schema_checks.sql（25 表），并跑 database/seed/99_validate.sql。

-- =====================================================================
-- ① 卫星表合并：D_Notice / D_Hygiene_Record 加列
-- =====================================================================

DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_exists FROM USER_TAB_COLS
     WHERE TABLE_NAME = 'D_NOTICE' AND COLUMN_NAME = 'IS_PINNED';
    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE
            'ALTER TABLE D_Notice ADD (Is_Pinned VARCHAR2(10) DEFAULT ''否'' NOT NULL)';
    END IF;

    SELECT COUNT(*) INTO v_exists FROM USER_TAB_COLS
     WHERE TABLE_NAME = 'D_NOTICE' AND COLUMN_NAME = 'PIN_TIME';
    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE D_Notice ADD (Pin_Time DATE)';
    END IF;

    SELECT COUNT(*) INTO v_exists FROM USER_CONSTRAINTS
     WHERE CONSTRAINT_NAME = 'CK_D_NOTICE_PIN';
    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE
            'ALTER TABLE D_Notice ADD CONSTRAINT CK_D_NOTICE_PIN ' ||
            'CHECK (Is_Pinned IN (''是'', ''否''))';
    END IF;
END;
/

DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_exists FROM USER_TAB_COLS
     WHERE TABLE_NAME = 'D_HYGIENE_RECORD' AND COLUMN_NAME = 'COMMENT';
    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE D_Hygiene_Record ADD ("COMMENT" VARCHAR2(500))';
    END IF;
END;
/

-- ①b 数据搬移（仅当卫星表仍在时执行；搬完即删表，重跑时自然跳过）

DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_exists FROM USER_TABLES
     WHERE TABLE_NAME = 'D_NOTICE_DISPLAY';
    IF v_exists > 0 THEN
        EXECUTE IMMEDIATE
            'UPDATE D_Notice n ' ||
            '   SET (n.Is_Pinned, n.Pin_Time) = ' ||
            '       (SELECT d.Is_Pinned, d.Pin_Time FROM D_Notice_Display d ' ||
            '         WHERE d.Notice_ID = n.Notice_ID) ' ||
            ' WHERE EXISTS (SELECT 1 FROM D_Notice_Display d ' ||
            '                WHERE d.Notice_ID = n.Notice_ID)';
        COMMIT;
    END IF;

    SELECT COUNT(*) INTO v_exists FROM USER_TABLES
     WHERE TABLE_NAME = 'D_HYGIENE_COMMENT';
    IF v_exists > 0 THEN
        EXECUTE IMMEDIATE
            'UPDATE D_Hygiene_Record r ' ||
            '   SET r."COMMENT" = ' ||
            '       (SELECT c."COMMENT" FROM D_Hygiene_Comment c ' ||
            '         WHERE c.Record_ID = r.Record_ID) ' ||
            ' WHERE EXISTS (SELECT 1 FROM D_Hygiene_Comment c ' ||
            '                WHERE c.Record_ID = r.Record_ID)';
        COMMIT;
    END IF;
END;
/

-- ①c 删除卫星表及其 015 对象（trigger → 表 → sequence）

DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_exists FROM USER_TRIGGERS
     WHERE TRIGGER_NAME = 'TRG_D_NOTICE_DISPLAY_PK_BI';
    IF v_exists > 0 THEN
        EXECUTE IMMEDIATE 'DROP TRIGGER TRG_D_NOTICE_DISPLAY_PK_BI';
    END IF;

    SELECT COUNT(*) INTO v_exists FROM USER_TABLES
     WHERE TABLE_NAME = 'D_NOTICE_DISPLAY';
    IF v_exists > 0 THEN
        EXECUTE IMMEDIATE 'DROP TABLE D_Notice_Display';
    END IF;

    SELECT COUNT(*) INTO v_exists FROM USER_SEQUENCES
     WHERE SEQUENCE_NAME = 'SEQ_D_NOTICE_DISPLAY_PK';
    IF v_exists > 0 THEN
        EXECUTE IMMEDIATE 'DROP SEQUENCE SEQ_D_NOTICE_DISPLAY_PK';
    END IF;

    SELECT COUNT(*) INTO v_exists FROM USER_TABLES
     WHERE TABLE_NAME = 'D_HYGIENE_COMMENT';
    IF v_exists > 0 THEN
        EXECUTE IMMEDIATE 'DROP TABLE D_Hygiene_Comment';
    END IF;
END;
/

-- =====================================================================
-- ② D_Fee_Detail：CHECK 谓词守卫 → 删约束/列 → 重建缩水 CHECK
-- =====================================================================

DECLARE
    v_exists NUMBER;
    v_pred   USER_CONSTRAINTS.SEARCH_CONDITION_VC%TYPE;
BEGIN
    SELECT COUNT(*) INTO v_exists FROM USER_CONSTRAINTS
     WHERE CONSTRAINT_NAME = 'CK_D_FEE_DETAIL_DAYS';
    IF v_exists > 0 THEN
        SELECT SEARCH_CONDITION_VC INTO v_pred FROM USER_CONSTRAINTS
         WHERE CONSTRAINT_NAME = 'CK_D_FEE_DETAIL_DAYS';
        IF INSTR(UPPER(v_pred), 'TOTAL_DAYS') > 0 THEN
            EXECUTE IMMEDIATE
                'ALTER TABLE D_Fee_Detail DROP CONSTRAINT CK_D_FEE_DETAIL_DAYS';
        END IF;
    END IF;

    SELECT COUNT(*) INTO v_exists FROM USER_CONSTRAINTS
     WHERE CONSTRAINT_NAME = 'FK_D_FEE_DETAIL_ROOM';
    IF v_exists > 0 THEN
        EXECUTE IMMEDIATE
            'ALTER TABLE D_Fee_Detail DROP CONSTRAINT FK_D_FEE_DETAIL_ROOM';
    END IF;
END;
/

DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_exists FROM USER_TAB_COLS
     WHERE TABLE_NAME = 'D_FEE_DETAIL' AND COLUMN_NAME = 'ROOM_ID';
    IF v_exists > 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE D_Fee_Detail DROP (Room_ID)';
    END IF;

    SELECT COUNT(*) INTO v_exists FROM USER_TAB_COLS
     WHERE TABLE_NAME = 'D_FEE_DETAIL' AND COLUMN_NAME = 'TOTAL_DAYS';
    IF v_exists > 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE D_Fee_Detail DROP (Total_Days)';
    END IF;
END;
/

DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_exists FROM USER_CONSTRAINTS
     WHERE CONSTRAINT_NAME = 'CK_D_FEE_DETAIL_DAYS';
    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE
            'ALTER TABLE D_Fee_Detail ADD CONSTRAINT CK_D_FEE_DETAIL_DAYS ' ||
            'CHECK (Stay_Days > 0)';
    END IF;
END;
/

-- =====================================================================
-- ③ 散列删除：Credit_Appeal.Student_ID / Utility_Fee.Is_Paid /
--              Visitor_Registry.Create_Time
-- =====================================================================

DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_exists FROM USER_INDEXES
     WHERE INDEX_NAME = 'IDX_D_CREDIT_APPEAL_STUDENT_TIME';
    IF v_exists > 0 THEN
        EXECUTE IMMEDIATE 'DROP INDEX IDX_D_CREDIT_APPEAL_STUDENT_TIME';
    END IF;

    SELECT COUNT(*) INTO v_exists FROM USER_CONSTRAINTS
     WHERE CONSTRAINT_NAME = 'FK_D_CREDIT_APPEAL_STUDENT';
    IF v_exists > 0 THEN
        EXECUTE IMMEDIATE
            'ALTER TABLE D_Credit_Appeal DROP CONSTRAINT FK_D_CREDIT_APPEAL_STUDENT';
    END IF;

    SELECT COUNT(*) INTO v_exists FROM USER_TAB_COLS
     WHERE TABLE_NAME = 'D_CREDIT_APPEAL' AND COLUMN_NAME = 'STUDENT_ID';
    IF v_exists > 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE D_Credit_Appeal DROP (Student_ID)';
    END IF;
END;
/

DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_exists FROM USER_TAB_COLS
     WHERE TABLE_NAME = 'D_UTILITY_FEE' AND COLUMN_NAME = 'IS_PAID';
    IF v_exists > 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE D_Utility_Fee DROP (Is_Paid)';
    END IF;

    SELECT COUNT(*) INTO v_exists FROM USER_TAB_COLS
     WHERE TABLE_NAME = 'D_VISITOR_REGISTRY' AND COLUMN_NAME = 'CREATE_TIME';
    IF v_exists > 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE D_Visitor_Registry DROP (Create_Time)';
    END IF;
END;
/

-- =====================================================================
-- ④ 废表删除：D_Parcel_Record / D_Visitor_Log / D_Water_Order
-- =====================================================================

DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_exists FROM USER_TRIGGERS
     WHERE TRIGGER_NAME = 'TRG_D_PARCEL_RECORD_ID_BI';
    IF v_exists > 0 THEN
        EXECUTE IMMEDIATE 'DROP TRIGGER TRG_D_PARCEL_RECORD_ID_BI';
    END IF;

    SELECT COUNT(*) INTO v_exists FROM USER_TABLES
     WHERE TABLE_NAME = 'D_PARCEL_RECORD';
    IF v_exists > 0 THEN
        EXECUTE IMMEDIATE 'DROP TABLE D_Parcel_Record';
    END IF;

    SELECT COUNT(*) INTO v_exists FROM USER_SEQUENCES
     WHERE SEQUENCE_NAME = 'SEQ_D_PARCEL_RECORD_ID';
    IF v_exists > 0 THEN
        EXECUTE IMMEDIATE 'DROP SEQUENCE SEQ_D_PARCEL_RECORD_ID';
    END IF;

    SELECT COUNT(*) INTO v_exists FROM USER_TABLES
     WHERE TABLE_NAME = 'D_VISITOR_LOG';
    IF v_exists > 0 THEN
        EXECUTE IMMEDIATE 'DROP TABLE D_Visitor_Log';
    END IF;

    SELECT COUNT(*) INTO v_exists FROM USER_TABLES
     WHERE TABLE_NAME = 'D_WATER_ORDER';
    IF v_exists > 0 THEN
        EXECUTE IMMEDIATE 'DROP TABLE D_Water_Order';
    END IF;
END;
/

-- =====================================================================
-- ⑤ 重编译失效对象 + 终态硬断言
-- =====================================================================

DECLARE
    v_stmt VARCHAR2(200);
BEGIN
    FOR r IN (SELECT object_name, object_type FROM USER_OBJECTS
               WHERE status = 'INVALID'
                 AND object_type IN ('PROCEDURE', 'FUNCTION', 'TRIGGER',
                                     'PACKAGE', 'PACKAGE BODY')) LOOP
        IF r.object_type = 'PACKAGE BODY' THEN
            v_stmt := 'ALTER PACKAGE ' || r.object_name || ' COMPILE BODY';
        ELSE
            v_stmt := 'ALTER ' || r.object_type || ' ' || r.object_name || ' COMPILE';
        END IF;
        BEGIN
            EXECUTE IMMEDIATE v_stmt;
        EXCEPTION
            WHEN OTHERS THEN
                -- 041 后 deploy/migrate 会恒重跑 sp/*.sql（CREATE OR REPLACE），
                -- 旧版 SP_* 因引用被删列而失效属升级期预期，不应阻断 041；
                -- 这里仅警告，留待 SP 重跑覆盖。非 SP_* 对象仍按硬错误处理。
                IF r.object_type = 'PROCEDURE'
                   AND r.object_name LIKE 'SP\_%' ESCAPE '\' THEN
                    DBMS_OUTPUT.PUT_LINE('041 WARN: 等待 SP 重跑覆盖 ' ||
                        r.object_type || ' ' || r.object_name);
                ELSE
                    RAISE_APPLICATION_ERROR(-20041,
                        '041 重编译失败: ' || r.object_type || ' ' || r.object_name);
                END IF;
        END;
    END LOOP;
END;
/

DECLARE
    v_cnt NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_cnt FROM USER_TABLES
     WHERE TABLE_NAME LIKE 'D\_%' ESCAPE '\' AND TABLE_NAME <> 'D_APP_MIGRATION';
    IF v_cnt <> 42 THEN
        RAISE_APPLICATION_ERROR(-20041,
            '041 自检失败：业务表应为 42 张，实际 ' || v_cnt || ' 张');
    END IF;

    SELECT COUNT(*) INTO v_cnt FROM USER_TABLES
     WHERE TABLE_NAME IN ('D_VISITOR_LOG', 'D_WATER_ORDER', 'D_PARCEL_RECORD',
                          'D_NOTICE_DISPLAY', 'D_HYGIENE_COMMENT');
    IF v_cnt <> 0 THEN
        RAISE_APPLICATION_ERROR(-20041, '041 自检失败：仍有废表/卫星表残留');
    END IF;

    SELECT COUNT(*) INTO v_cnt FROM USER_TAB_COLS
     WHERE (TABLE_NAME = 'D_FEE_DETAIL'      AND COLUMN_NAME IN ('ROOM_ID', 'TOTAL_DAYS'))
        OR (TABLE_NAME = 'D_CREDIT_APPEAL'   AND COLUMN_NAME = 'STUDENT_ID')
        OR (TABLE_NAME = 'D_UTILITY_FEE'     AND COLUMN_NAME = 'IS_PAID')
        OR (TABLE_NAME = 'D_VISITOR_REGISTRY' AND COLUMN_NAME = 'CREATE_TIME');
    IF v_cnt <> 0 THEN
        RAISE_APPLICATION_ERROR(-20041, '041 自检失败：范式违规列未清除');
    END IF;

    SELECT COUNT(*) INTO v_cnt FROM USER_TAB_COLS
     WHERE TABLE_NAME = 'D_NOTICE' AND COLUMN_NAME IN ('IS_PINNED', 'PIN_TIME');
    IF v_cnt <> 2 THEN
        RAISE_APPLICATION_ERROR(-20041, '041 自检失败：D_Notice 置顶列未落位');
    END IF;

    SELECT COUNT(*) INTO v_cnt FROM USER_TAB_COLS
     WHERE TABLE_NAME = 'D_HYGIENE_RECORD' AND COLUMN_NAME = 'COMMENT';
    IF v_cnt <> 1 THEN
        RAISE_APPLICATION_ERROR(-20041, '041 自检失败：D_Hygiene_Record 评语列未落位');
    END IF;

    SELECT COUNT(*) INTO v_cnt FROM USER_SEQUENCES
     WHERE SEQUENCE_NAME IN ('SEQ_D_NOTICE_DISPLAY_PK', 'SEQ_D_PARCEL_RECORD_ID');
    IF v_cnt <> 0 THEN
        RAISE_APPLICATION_ERROR(-20041, '041 自检失败：孤儿序列残留');
    END IF;

    SELECT COUNT(*) INTO v_cnt FROM USER_CONSTRAINTS
     WHERE CONSTRAINT_NAME IN ('CK_D_FEE_DETAIL_TYPE', 'UK_D_FEE_DETAIL',
                               'FK_D_FEE_DETAIL_FEE', 'FK_D_FEE_DETAIL_STUDENT',
                               'CK_D_FEE_DETAIL_AMOUNT', 'CK_D_FEE_DETAIL_DAYS',
                               'CK_D_FEE_DETAIL_STATUS', 'CK_D_NOTICE_PIN');
    IF v_cnt <> 8 THEN
        RAISE_APPLICATION_ERROR(-20041,
            '041 自检失败：D_Fee_Detail/D_Notice 约束族应为 8 个，实际 ' || v_cnt);
    END IF;

    DBMS_OUTPUT.PUT_LINE('041_database_consolidation: 42-table final state OK');
END;
/
