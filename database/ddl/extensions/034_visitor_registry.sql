-- 034_visitor_registry.sql — 门岗登记表（VST-01/02/03）
-- 依赖：foundation/001（D_Student）、extensions/010（D_Visitor_Authorization）
-- 执行顺序：在 extensions/033_credit_appeal.sql 之后执行。
-- 本脚本新建独立扩展表，不得修改或重建任何基线表。
-- 主键生成：SEQ_D_VISITOR_REGISTRY_ID 序列 + TRG_D_VISITOR_REGISTRY_ID_BI 触发器（同 014 模式）。

CREATE TABLE D_Visitor_Registry (
    Registry_ID NUMBER(19) CONSTRAINT PK_D_VISITOR_REGISTRY PRIMARY KEY,
    Qr_Token VARCHAR2(100),
    Visitor_Name VARCHAR2(50) NOT NULL,
    Phone VARCHAR2(20),
    Student_ID VARCHAR2(20),
    Enter_Time DATE DEFAULT SYSDATE NOT NULL,
    Exit_Time DATE,
    Status VARCHAR2(20) DEFAULT '待核验' NOT NULL,
    Create_Time DATE DEFAULT SYSDATE NOT NULL,
    CONSTRAINT UK_D_VISITOR_REGISTRY_QR UNIQUE (Qr_Token),
    CONSTRAINT CK_D_VISITOR_REGISTRY_STATUS CHECK (Status IN ('待核验', '已核验', '已离开')),
    CONSTRAINT FK_D_VISITOR_REGISTRY_STU FOREIGN KEY (Student_ID) REFERENCES D_Student (Student_ID)
);

-- 主键序列（幂等，从现有 max+1 起）
DECLARE
    v_exists NUMBER;
    v_max    NUMBER;
    v_start  NUMBER;
BEGIN
    SELECT NVL(MAX(Registry_ID), 0) INTO v_max FROM D_Visitor_Registry;
    SELECT COUNT(*) INTO v_exists FROM USER_SEQUENCES
     WHERE SEQUENCE_NAME = 'SEQ_D_VISITOR_REGISTRY_ID';
    IF v_exists = 0 THEN
        v_start := v_max + 1;
        EXECUTE IMMEDIATE 'CREATE SEQUENCE SEQ_D_VISITOR_REGISTRY_ID START WITH ' || v_start ||
                          ' INCREMENT BY 1 MAXVALUE 9999999999 NOCACHE';
    END IF;
END;
/

-- 主键触发器（幂等）
DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_exists FROM USER_TRIGGERS
     WHERE TRIGGER_NAME = 'TRG_D_VISITOR_REGISTRY_ID_BI';
    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE 'CREATE TRIGGER TRG_D_VISITOR_REGISTRY_ID_BI ' ||
                          'BEFORE INSERT ON D_Visitor_Registry ' ||
                          'FOR EACH ROW WHEN (NEW.Registry_ID IS NULL) ' ||
                          'BEGIN SELECT SEQ_D_VISITOR_REGISTRY_ID.NEXTVAL INTO :NEW.Registry_ID FROM dual; END;';
    END IF;
END;
/

-- 核验查询索引（幂等）
DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_exists FROM USER_INDEXES
     WHERE INDEX_NAME = 'IDX_D_VISITOR_REGISTRY_STATUS_NAME';
    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE 'CREATE INDEX IDX_D_VISITOR_REGISTRY_STATUS_NAME ' ||
                          'ON D_Visitor_Registry(Status, Visitor_Name, Enter_Time)';
    END IF;
END;
/
