-- 033_credit_appeal.sql — 信用分申诉表（APPEAL-01/02/03）
-- 依赖：foundation/001（D_Admin）、extensions/010（D_Credit_Log、D_Credit_Account）
-- 执行顺序：在 extensions/032_admin_role_include_counselor.sql 之后执行。
-- 本脚本新建独立扩展表，不得修改或重建任何基线表。
-- 主键生成：SEQ_D_CREDIT_APPEAL_ID 序列 + TRG_D_CREDIT_APPEAL_ID_BI 触发器（同 014 模式）。

CREATE TABLE D_Credit_Appeal (
    Appeal_ID NUMBER(10) CONSTRAINT PK_D_CREDIT_APPEAL PRIMARY KEY,
    Credit_Log_ID NUMBER(10) NOT NULL,
    Student_ID VARCHAR2(20) NOT NULL,
    Reason VARCHAR2(200 CHAR) NOT NULL,
    Status VARCHAR2(20) DEFAULT '待复核' NOT NULL,
    Result_Desc VARCHAR2(200 CHAR),
    Reviewed_By VARCHAR2(20),
    Review_Time DATE,
    Create_Time DATE DEFAULT SYSDATE NOT NULL,
    CONSTRAINT CK_D_CREDIT_APPEAL_STATUS CHECK (Status IN ('待复核', '已通过', '已驳回')),
    CONSTRAINT UK_D_CREDIT_APPEAL_LOG UNIQUE (Credit_Log_ID),
    CONSTRAINT FK_D_CREDIT_APPEAL_LOG FOREIGN KEY (Credit_Log_ID) REFERENCES D_Credit_Log (Log_ID),
    CONSTRAINT FK_D_CREDIT_APPEAL_STUDENT FOREIGN KEY (Student_ID) REFERENCES D_Credit_Account (Student_ID),
    CONSTRAINT FK_D_CREDIT_APPEAL_REVIEWER FOREIGN KEY (Reviewed_By) REFERENCES D_Admin (Admin_ID)
);

-- 主键序列（幂等，从现有 max+1 起，避免与种子 9xxxxx 段撞号）
DECLARE
    v_exists NUMBER;
    v_max    NUMBER;
    v_start  NUMBER;
BEGIN
    SELECT NVL(MAX(Appeal_ID), 0) INTO v_max FROM D_Credit_Appeal;
    SELECT COUNT(*) INTO v_exists FROM USER_SEQUENCES
     WHERE SEQUENCE_NAME = 'SEQ_D_CREDIT_APPEAL_ID';
    IF v_exists = 0 THEN
        v_start := v_max + 1;
        EXECUTE IMMEDIATE 'CREATE SEQUENCE SEQ_D_CREDIT_APPEAL_ID START WITH ' || v_start ||
                          ' INCREMENT BY 1 MAXVALUE 9999999999 NOCACHE';
    END IF;
END;
/

-- 主键触发器（幂等）
DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_exists FROM USER_TRIGGERS
     WHERE TRIGGER_NAME = 'TRG_D_CREDIT_APPEAL_ID_BI';
    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE 'CREATE TRIGGER TRG_D_CREDIT_APPEAL_ID_BI ' ||
                          'BEFORE INSERT ON D_Credit_Appeal ' ||
                          'FOR EACH ROW WHEN (NEW.Appeal_ID IS NULL) ' ||
                          'BEGIN SELECT SEQ_D_CREDIT_APPEAL_ID.NEXTVAL INTO :NEW.Appeal_ID FROM dual; END;';
    END IF;
END;
/

-- 学生申诉列表查询索引（幂等）
DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_exists FROM USER_INDEXES
     WHERE INDEX_NAME = 'IDX_D_CREDIT_APPEAL_STUDENT_TIME';
    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE 'CREATE INDEX IDX_D_CREDIT_APPEAL_STUDENT_TIME ' ||
                          'ON D_Credit_Appeal(Student_ID, Create_Time, Appeal_ID)';
    END IF;
END;
/
