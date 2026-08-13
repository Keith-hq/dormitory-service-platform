-- ============================================================
-- 难点④ 五审：测试库补齐基线迁移 012/013/014/018 + 019 兼容对齐
-- （sqlplus 版：为 DBeaver 风格匿名块补 "/"）
-- 执行顺序：012 → 013 → 014 → 018 → 019 兼容对齐（列已由旧内联补丁创建）
-- ============================================================
SET SERVEROUTPUT ON SIZE UNLIMITED
WHENEVER SQLERROR CONTINUE

PROMPT ===== [012] SEQ_D_NOTIFICATION_ID =====
DECLARE
    v_exists NUMBER;
    v_start_with NUMBER;
BEGIN
    SELECT COUNT(*)
      INTO v_exists
      FROM USER_SEQUENCES
     WHERE SEQUENCE_NAME = 'SEQ_D_NOTIFICATION_ID';

    IF v_exists = 0 THEN
        SELECT NVL(MAX(Notification_ID), 0) + 1
          INTO v_start_with
          FROM D_Notification;

        EXECUTE IMMEDIATE
            'CREATE SEQUENCE SEQ_D_NOTIFICATION_ID START WITH ' || v_start_with ||
            ' INCREMENT BY 1 NOCACHE';
        DBMS_OUTPUT.PUT_LINE('012 SEQ CREATED start=' || v_start_with);
    ELSE
        DBMS_OUTPUT.PUT_LINE('012 SEQ SKIPPED (exists)');
    END IF;
END;
/

PROMPT ===== [012] TRG_D_NOTIFICATION_ID_BI =====
DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*)
      INTO v_exists
      FROM USER_TRIGGERS
     WHERE TRIGGER_NAME = 'TRG_D_NOTIFICATION_ID_BI';

    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE
            'CREATE TRIGGER TRG_D_NOTIFICATION_ID_BI ' ||
            'BEFORE INSERT ON D_Notification ' ||
            'FOR EACH ROW ' ||
            'WHEN (NEW.Notification_ID IS NULL) ' ||
            'BEGIN ' ||
            '    SELECT SEQ_D_NOTIFICATION_ID.NEXTVAL ' ||
            '      INTO :NEW.Notification_ID ' ||
            '      FROM dual; ' ||
            'END;';
        DBMS_OUTPUT.PUT_LINE('012 TRIGGER CREATED');
    ELSE
        DBMS_OUTPUT.PUT_LINE('012 TRIGGER SKIPPED (exists)');
    END IF;
END;
/

PROMPT ===== [012] IDX_D_NOTIFICATION_RECIPIENT =====
DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*)
      INTO v_exists
      FROM USER_INDEXES
     WHERE INDEX_NAME = 'IDX_D_NOTIFICATION_RECIPIENT';

    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE
            'CREATE INDEX IDX_D_NOTIFICATION_RECIPIENT ' ||
            'ON D_Notification(Recipient_Account_ID)';
        DBMS_OUTPUT.PUT_LINE('012 INDEX CREATED');
    ELSE
        DBMS_OUTPUT.PUT_LINE('012 INDEX SKIPPED (exists)');
    END IF;
END;
/

PROMPT ===== [013] D_Notification CHAR 语义 =====
ALTER TABLE D_Notification
    MODIFY (Title VARCHAR2(100 CHAR),
            Content VARCHAR2(1000 CHAR));

PROMPT ===== [014] SEQ_D_CREDIT_LOG_ID =====
DECLARE
    v_exists NUMBER;
    v_max_log_id NUMBER;
    v_start_with NUMBER;
BEGIN
    SELECT NVL(MAX(Log_ID), 0)
      INTO v_max_log_id
      FROM D_Credit_Log;

    IF v_max_log_id >= 9999999999 THEN
        RAISE_APPLICATION_ERROR(-20014, 'D_Credit_Log.Log_ID 已触达 NUMBER(10) 上限');
    END IF;

    SELECT COUNT(*)
      INTO v_exists
      FROM USER_SEQUENCES
     WHERE SEQUENCE_NAME = 'SEQ_D_CREDIT_LOG_ID';

    IF v_exists = 0 THEN
        v_start_with := v_max_log_id + 1;
        EXECUTE IMMEDIATE
            'CREATE SEQUENCE SEQ_D_CREDIT_LOG_ID START WITH ' || v_start_with ||
            ' INCREMENT BY 1 MAXVALUE 9999999999 NOCACHE';
        DBMS_OUTPUT.PUT_LINE('014 SEQ CREATED start=' || v_start_with);
    ELSE
        DBMS_OUTPUT.PUT_LINE('014 SEQ SKIPPED (exists)');
    END IF;
END;
/

PROMPT ===== [014] TRG_D_CREDIT_LOG_ID_BI =====
DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*)
      INTO v_exists
      FROM USER_TRIGGERS
     WHERE TRIGGER_NAME = 'TRG_D_CREDIT_LOG_ID_BI';

    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE
            'CREATE TRIGGER TRG_D_CREDIT_LOG_ID_BI ' ||
            'BEFORE INSERT ON D_Credit_Log ' ||
            'FOR EACH ROW ' ||
            'WHEN (NEW.Log_ID IS NULL) ' ||
            'BEGIN ' ||
            '    SELECT SEQ_D_CREDIT_LOG_ID.NEXTVAL ' ||
            '      INTO :NEW.Log_ID ' ||
            '      FROM dual; ' ||
            'END;';
        DBMS_OUTPUT.PUT_LINE('014 TRIGGER CREATED');
    ELSE
        DBMS_OUTPUT.PUT_LINE('014 TRIGGER SKIPPED (exists)');
    END IF;
END;
/

PROMPT ===== [014] IDX_D_CREDIT_LOG_STUDENT_TIME =====
DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*)
      INTO v_exists
      FROM USER_INDEXES
     WHERE INDEX_NAME = 'IDX_D_CREDIT_LOG_STUDENT_TIME';

    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE
            'CREATE INDEX IDX_D_CREDIT_LOG_STUDENT_TIME ' ||
            'ON D_Credit_Log(Student_ID, Create_Time, Log_ID)';
        DBMS_OUTPUT.PUT_LINE('014 INDEX CREATED');
    ELSE
        DBMS_OUTPUT.PUT_LINE('014 INDEX SKIPPED (exists)');
    END IF;
END;
/

PROMPT ===== [014] D_Credit_Log CHAR 语义 =====
ALTER TABLE D_Credit_Log
    MODIFY (Reason VARCHAR2(200 CHAR),
            Event_Key VARCHAR2(100 CHAR));

PROMPT ===== [018] D_Student.Email =====
ALTER TABLE D_Student
    ADD (Email VARCHAR2(200 CHAR));

PROMPT ===== [019 兼容对齐] 幂等键列 CHAR 语义 =====
ALTER TABLE D_Item_Loan MODIFY (Idempotency_Key VARCHAR2(100 CHAR));
ALTER TABLE D_Repair_Material_Usage MODIFY (Idempotency_Key VARCHAR2(100 CHAR));

PROMPT ===== MIGRATIONS DONE =====
EXIT
