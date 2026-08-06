-- Extension tables for the frozen foundation schema.
-- Run after ddl/foundation/001_create_tables.sql.
-- This script must not alter or recreate any foundation table.

-- Fee allocation and wallet
CREATE TABLE D_Fee_Detail (
    Detail_ID NUMBER(10) CONSTRAINT PK_D_FEE_DETAIL PRIMARY KEY,
    Fee_ID NUMBER(10) NOT NULL,
    Student_ID VARCHAR2(20) NOT NULL,
    Room_ID NUMBER(10) NOT NULL,
    Water_Share NUMBER(8, 2) DEFAULT 0 NOT NULL,
    Power_Share NUMBER(8, 2) DEFAULT 0 NOT NULL,
    Stay_Days NUMBER(4) NOT NULL,
    Total_Days NUMBER(4) NOT NULL,
    Bill_Type VARCHAR2(10) NOT NULL,
    Is_Paid VARCHAR2(10) DEFAULT '否' NOT NULL,
    Create_Time DATE DEFAULT SYSDATE NOT NULL,
    CONSTRAINT CK_D_FEE_DETAIL_TYPE
        CHECK (Bill_Type IN ('月度', '退宿')),
    CONSTRAINT UK_D_FEE_DETAIL
        UNIQUE (Fee_ID, Student_ID, Bill_Type),
    CONSTRAINT CK_D_FEE_DETAIL_AMOUNT
        CHECK (Water_Share >= 0 AND Power_Share >= 0),
    CONSTRAINT CK_D_FEE_DETAIL_DAYS
        CHECK (Stay_Days > 0 AND Total_Days > 0 AND Stay_Days <= Total_Days),
    CONSTRAINT CK_D_FEE_DETAIL_STATUS
        CHECK (Is_Paid IN ('是', '否')),
    CONSTRAINT FK_D_FEE_DETAIL_FEE
        FOREIGN KEY (Fee_ID) REFERENCES D_Utility_Fee (Fee_ID),
    CONSTRAINT FK_D_FEE_DETAIL_STUDENT
        FOREIGN KEY (Student_ID) REFERENCES D_Student (Student_ID),
    CONSTRAINT FK_D_FEE_DETAIL_ROOM
        FOREIGN KEY (Room_ID) REFERENCES D_Room (Room_ID)
);

CREATE TABLE D_Wallet_Account (
    Student_ID VARCHAR2(20) CONSTRAINT PK_D_WALLET_ACCOUNT PRIMARY KEY,
    Balance NUMBER(8, 2) DEFAULT 0 NOT NULL,
    CONSTRAINT CK_D_WALLET_BALANCE
        CHECK (Balance >= 0),
    CONSTRAINT FK_D_WALLET_STUDENT
        FOREIGN KEY (Student_ID) REFERENCES D_Student (Student_ID)
);

CREATE TABLE D_Wallet_Log (
    Log_ID NUMBER(10) CONSTRAINT PK_D_WALLET_LOG PRIMARY KEY,
    Student_ID VARCHAR2(20) NOT NULL,
    Amount NUMBER(10, 2) NOT NULL,
    Transaction_Type VARCHAR2(20) NOT NULL,
    Before_Balance NUMBER(8, 2) NOT NULL,
    After_Balance NUMBER(8, 2) NOT NULL,
    Detail_ID NUMBER(10),
    Idempotency_Key VARCHAR2(100) NOT NULL,
    Create_Time DATE DEFAULT SYSDATE NOT NULL,
    CONSTRAINT CK_D_WALLET_LOG_AMOUNT
        CHECK (Amount > 0),
    CONSTRAINT CK_D_WALLET_LOG_TYPE
        CHECK (Transaction_Type IN ('充值', '自动扣款', '人工缴费')),
    CONSTRAINT CK_D_WALLET_LOG_BALANCE
        CHECK (Before_Balance >= 0 AND After_Balance >= 0),
    CONSTRAINT UK_D_WALLET_LOG_KEY
        UNIQUE (Idempotency_Key),
    CONSTRAINT FK_D_WALLET_LOG_ACCOUNT
        FOREIGN KEY (Student_ID) REFERENCES D_Wallet_Account (Student_ID),
    CONSTRAINT FK_D_WALLET_LOG_DETAIL
        FOREIGN KEY (Detail_ID) REFERENCES D_Fee_Detail (Detail_ID)
);

CREATE TABLE D_Fee_Deduction_Attempt (
    Attempt_ID NUMBER(10) CONSTRAINT PK_D_FEE_DED_ATT PRIMARY KEY,
    Detail_ID NUMBER(10) NOT NULL,
    Attempt_No NUMBER(1) NOT NULL,
    Attempt_Time DATE DEFAULT SYSDATE NOT NULL,
    Result VARCHAR2(10) NOT NULL,
    CONSTRAINT CK_D_FEE_DED_NO
        CHECK (Attempt_No BETWEEN 1 AND 3),
    CONSTRAINT CK_D_FEE_DED_STATUS
        CHECK (Result IN ('成功', '余额不足')),
    CONSTRAINT UK_D_FEE_DED_ATT
        UNIQUE (Detail_ID, Attempt_No),
    CONSTRAINT FK_D_FEE_DED_DETAIL
        FOREIGN KEY (Detail_ID) REFERENCES D_Fee_Detail (Detail_ID)
);

-- Credit and shared items
CREATE TABLE D_Credit_Account (
    Student_ID VARCHAR2(20) CONSTRAINT PK_D_CREDIT_ACCOUNT PRIMARY KEY,
    Current_Score NUMBER(3) DEFAULT 100 NOT NULL,
    Updated_Time DATE DEFAULT SYSDATE NOT NULL,
    CONSTRAINT CK_D_CREDIT_SCORE
        CHECK (Current_Score BETWEEN 0 AND 100),
    CONSTRAINT FK_D_CREDIT_STUDENT
        FOREIGN KEY (Student_ID) REFERENCES D_Student (Student_ID)
);

CREATE TABLE D_Credit_Log (
    Log_ID NUMBER(10) CONSTRAINT PK_D_CREDIT_LOG PRIMARY KEY,
    Student_ID VARCHAR2(20) NOT NULL,
    Score_Change NUMBER(3) NOT NULL,
    Reason VARCHAR2(200) NOT NULL,
    Event_Key VARCHAR2(100) NOT NULL,
    Create_Time DATE DEFAULT SYSDATE NOT NULL,
    CONSTRAINT CK_D_CREDIT_LOG_CHANGE
        CHECK (Score_Change <> 0),
    CONSTRAINT UK_D_CREDIT_LOG_EVENT
        UNIQUE (Event_Key),
    CONSTRAINT FK_D_CREDIT_LOG_ACCOUNT
        FOREIGN KEY (Student_ID) REFERENCES D_Credit_Account (Student_ID)
);

CREATE TABLE D_Shared_Item (
    Item_ID NUMBER(10) CONSTRAINT PK_D_SHARED_ITEM PRIMARY KEY,
    Item_Name VARCHAR2(50) NOT NULL,
    Building_ID NUMBER(10) NOT NULL,
    Total_Qty NUMBER(3) DEFAULT 1 NOT NULL,
    Available_Qty NUMBER(3) NOT NULL,
    Status VARCHAR2(10) DEFAULT '正常' NOT NULL,
    CONSTRAINT CK_D_SHARED_ITEM_QTY
        CHECK (Total_Qty >= 0 AND Available_Qty BETWEEN 0 AND Total_Qty),
    CONSTRAINT CK_D_SHARED_ITEM_STATUS
        CHECK (Status IN ('正常', '停用')),
    CONSTRAINT FK_D_SHARED_ITEM_BUILDING
        FOREIGN KEY (Building_ID) REFERENCES D_Building (Building_ID)
);

CREATE TABLE D_Item_Loan (
    Loan_ID NUMBER(10) CONSTRAINT PK_D_ITEM_LOAN PRIMARY KEY,
    Item_ID NUMBER(10) NOT NULL,
    Student_ID VARCHAR2(20) NOT NULL,
    Borrow_Time DATE NOT NULL,
    Due_Time DATE NOT NULL,
    Return_Time DATE,
    CONSTRAINT CK_D_ITEM_LOAN_DATES
        CHECK (Due_Time > Borrow_Time AND
               (Return_Time IS NULL OR Return_Time >= Borrow_Time)),
    CONSTRAINT FK_D_ITEM_LOAN_ITEM
        FOREIGN KEY (Item_ID) REFERENCES D_Shared_Item (Item_ID),
    CONSTRAINT FK_D_ITEM_LOAN_STUDENT
        FOREIGN KEY (Student_ID) REFERENCES D_Student (Student_ID)
);

-- Facilities and cleaning tasks
CREATE TABLE D_Facility (
    Facility_ID NUMBER(10) CONSTRAINT PK_D_FACILITY PRIMARY KEY,
    Building_ID NUMBER(10) NOT NULL,
    Facility_Code VARCHAR2(30) NOT NULL,
    Facility_Type VARCHAR2(30) NOT NULL,
    Status VARCHAR2(10) DEFAULT '正常' NOT NULL,
    CONSTRAINT UK_D_FACILITY_CODE
        UNIQUE (Facility_Code),
    CONSTRAINT CK_D_FACILITY_STATUS
        CHECK (Status IN ('正常', '维修', '停用')),
    CONSTRAINT FK_D_FACILITY_BUILDING
        FOREIGN KEY (Building_ID) REFERENCES D_Building (Building_ID)
);

CREATE TABLE D_Facility_Booking (
    Booking_ID NUMBER(10) CONSTRAINT PK_D_FACILITY_BOOK PRIMARY KEY,
    Facility_ID NUMBER(10) NOT NULL,
    Student_ID VARCHAR2(20) NOT NULL,
    Create_Time DATE DEFAULT SYSDATE NOT NULL,
    Start_Time DATE,
    End_Time DATE,
    Status VARCHAR2(10) DEFAULT '已预约' NOT NULL,
    CONSTRAINT CK_D_FACILITY_BOOK_TIME
        CHECK ((Start_Time IS NULL AND End_Time IS NULL) OR
               (Start_Time IS NOT NULL AND End_Time IS NOT NULL AND
                End_Time > Start_Time)),
    CONSTRAINT CK_D_FACILITY_BOOK_STATUS
        CHECK (Status IN ('已预约', '使用中', '已完成', '已失效')),
    CONSTRAINT FK_D_FACILITY_BOOK_FAC
        FOREIGN KEY (Facility_ID) REFERENCES D_Facility (Facility_ID),
    CONSTRAINT FK_D_FACILITY_BOOK_STU
        FOREIGN KEY (Student_ID) REFERENCES D_Student (Student_ID)
);

CREATE TABLE D_Cleaning_Task (
    Task_ID NUMBER(10) CONSTRAINT PK_D_CLEANING_TASK PRIMARY KEY,
    Facility_ID NUMBER(10) NOT NULL,
    Trigger_Count NUMBER(10) NOT NULL,
    Status VARCHAR2(10) DEFAULT '待处理' NOT NULL,
    Create_Time DATE DEFAULT SYSDATE NOT NULL,
    Complete_Time DATE,
    CONSTRAINT CK_D_CLEANING_COUNT
        CHECK (Trigger_Count > 0),
    CONSTRAINT CK_D_CLEANING_STATUS
        CHECK (Status IN ('待处理', '已完成')),
    CONSTRAINT CK_D_CLEANING_TIME
        CHECK (Complete_Time IS NULL OR Complete_Time >= Create_Time),
    CONSTRAINT UK_D_CLEANING_TRIGGER
        UNIQUE (Facility_ID, Trigger_Count),
    CONSTRAINT FK_D_CLEANING_FACILITY
        FOREIGN KEY (Facility_ID) REFERENCES D_Facility (Facility_ID)
);

-- 同一设施最多一条活动预约（已预约/使用中），并发防冲突的数据库兜底。
-- Oracle 不支持带 WHERE 的部分索引，改用函数索引：非活动预约取 NULL，NULL 不参与唯一判定。
CREATE UNIQUE INDEX UK_D_FACILITY_BOOK_ACTIVE
    ON D_Facility_Booking (CASE WHEN Status IN ('已预约', '使用中') THEN Facility_ID END);

-- Repair materials and attachments
CREATE TABLE D_Repair_Material (
    Material_ID NUMBER(10) CONSTRAINT PK_D_REPAIR_MATERIAL PRIMARY KEY,
    Material_Name VARCHAR2(50) NOT NULL,
    Unit VARCHAR2(20) NOT NULL,
    Stock_Qty NUMBER(8) DEFAULT 0 NOT NULL,
    CONSTRAINT CK_D_REPAIR_MATERIAL_STOCK
        CHECK (Stock_Qty >= 0)
);

CREATE TABLE D_Repair_Material_Usage (
    Usage_ID NUMBER(10) CONSTRAINT PK_D_REPAIR_MAT_USE PRIMARY KEY,
    Ticket_ID NUMBER(10) NOT NULL,
    Material_ID NUMBER(10) NOT NULL,
    Quantity NUMBER(8) NOT NULL,
    Use_Time DATE DEFAULT SYSDATE NOT NULL,
    CONSTRAINT CK_D_REPAIR_MAT_USE_QTY
        CHECK (Quantity > 0),
    CONSTRAINT FK_D_REPAIR_MAT_USE_TICKET
        FOREIGN KEY (Ticket_ID) REFERENCES D_Repair_Ticket (Ticket_ID),
    CONSTRAINT FK_D_REPAIR_MAT_USE_MAT
        FOREIGN KEY (Material_ID) REFERENCES D_Repair_Material (Material_ID)
);

CREATE TABLE D_Repair_Attachment (
    Attachment_ID NUMBER(10) CONSTRAINT PK_D_REPAIR_ATTACH PRIMARY KEY,
    Ticket_ID NUMBER(10) NOT NULL,
    Storage_Ref VARCHAR2(500) NOT NULL,
    Original_Name VARCHAR2(255),
    Content_Type VARCHAR2(100),
    File_Size NUMBER(10),
    Create_Time DATE DEFAULT SYSDATE NOT NULL,
    CONSTRAINT CK_D_REPAIR_ATTACH_SIZE
        CHECK (File_Size IS NULL OR File_Size > 0),
    CONSTRAINT UK_D_REPAIR_ATTACH_REF
        UNIQUE (Storage_Ref),
    CONSTRAINT FK_D_REPAIR_ATTACH_TICKET
        FOREIGN KEY (Ticket_ID) REFERENCES D_Repair_Ticket (Ticket_ID)
);

-- Accounts, notifications, visitor authorization and audit events
CREATE TABLE D_User_Account (
    Account_ID NUMBER(10) CONSTRAINT PK_D_USER_ACCOUNT PRIMARY KEY,
    Login_Name VARCHAR2(50) NOT NULL,
    Password_Hash VARCHAR2(255) NOT NULL,
    Account_Status VARCHAR2(10) DEFAULT '正常' NOT NULL,
    Student_ID VARCHAR2(20),
    Admin_ID VARCHAR2(20),
    Create_Time DATE DEFAULT SYSDATE NOT NULL,
    CONSTRAINT UK_D_USER_LOGIN UNIQUE (Login_Name),
    CONSTRAINT UK_D_USER_STUDENT UNIQUE (Student_ID),
    CONSTRAINT UK_D_USER_ADMIN UNIQUE (Admin_ID),
    CONSTRAINT CK_D_USER_STATUS
        CHECK (Account_Status IN ('正常', '停用')),
    CONSTRAINT CK_D_USER_IDENTITY
        CHECK ((Student_ID IS NOT NULL AND Admin_ID IS NULL) OR
               (Student_ID IS NULL AND Admin_ID IS NOT NULL)),
    CONSTRAINT FK_D_USER_STUDENT
        FOREIGN KEY (Student_ID) REFERENCES D_Student (Student_ID),
    CONSTRAINT FK_D_USER_ADMIN
        FOREIGN KEY (Admin_ID) REFERENCES D_Admin (Admin_ID)
);

CREATE TABLE D_Notification (
    Notification_ID NUMBER(10) CONSTRAINT PK_D_NOTIFICATION PRIMARY KEY,
    Recipient_Account_ID NUMBER(10) NOT NULL,
    Title VARCHAR2(100) NOT NULL,
    Content VARCHAR2(1000) NOT NULL,
    Notification_Type VARCHAR2(20) NOT NULL,
    Read_Time DATE,
    Create_Time DATE DEFAULT SYSDATE NOT NULL,
    CONSTRAINT FK_D_NOTIFICATION_ACCOUNT
        FOREIGN KEY (Recipient_Account_ID) REFERENCES D_User_Account (Account_ID)
);

CREATE TABLE D_Visitor_Authorization (
    Authorization_ID NUMBER(10) CONSTRAINT PK_D_VISITOR_AUTH PRIMARY KEY,
    Student_ID VARCHAR2(20) NOT NULL,
    Room_ID NUMBER(10) NOT NULL,
    Visitor_Name VARCHAR2(50) NOT NULL,
    Visit_Reason VARCHAR2(200),
    Authorization_Token VARCHAR2(100) NOT NULL,
    Expires_Time DATE NOT NULL,
    Status VARCHAR2(10) DEFAULT '有效' NOT NULL,
    Create_Time DATE DEFAULT SYSDATE NOT NULL,
    CONSTRAINT UK_D_VISITOR_AUTH_TOKEN
        UNIQUE (Authorization_Token),
    CONSTRAINT CK_D_VISITOR_AUTH_TIME
        CHECK (Expires_Time > Create_Time),
    CONSTRAINT CK_D_VISITOR_AUTH_STATUS
        CHECK (Status IN ('有效', '已过期', '已撤销')),
    CONSTRAINT FK_D_VISITOR_AUTH_STU
        FOREIGN KEY (Student_ID) REFERENCES D_Student (Student_ID),
    CONSTRAINT FK_D_VISITOR_AUTH_ROOM
        FOREIGN KEY (Room_ID) REFERENCES D_Room (Room_ID)
);

CREATE TABLE D_Audit_Event (
    Audit_ID NUMBER(10) CONSTRAINT PK_D_AUDIT_EVENT PRIMARY KEY,
    Actor_Account_ID NUMBER(10),
    Event_Type VARCHAR2(50) NOT NULL,
    Target_Type VARCHAR2(50),
    Target_ID VARCHAR2(50),
    Event_Time DATE DEFAULT SYSDATE NOT NULL,
    CONSTRAINT FK_D_AUDIT_ACCOUNT
        FOREIGN KEY (Actor_Account_ID) REFERENCES D_User_Account (Account_ID)
);

-- Room governance and checkout
CREATE TABLE D_Room_Vote (
    Vote_ID NUMBER(10) CONSTRAINT PK_D_ROOM_VOTE PRIMARY KEY,
    Room_ID NUMBER(10) NOT NULL,
    Initiator_Student_ID VARCHAR2(20) NOT NULL,
    Topic VARCHAR2(200) NOT NULL,
    Create_Time DATE DEFAULT SYSDATE NOT NULL,
    Deadline DATE NOT NULL,
    Eligible_Count NUMBER(2) NOT NULL,
    Status VARCHAR2(10) NOT NULL,
    CONSTRAINT CK_D_ROOM_VOTE_COUNT
        CHECK (Eligible_Count > 0),
    CONSTRAINT CK_D_ROOM_VOTE_STATUS
        CHECK (Status IN ('进行中', '已通过', '未通过', '已结束')),
    CONSTRAINT FK_D_ROOM_VOTE_ROOM
        FOREIGN KEY (Room_ID) REFERENCES D_Room (Room_ID),
    CONSTRAINT FK_D_ROOM_VOTE_INIT
        FOREIGN KEY (Initiator_Student_ID) REFERENCES D_Student (Student_ID)
);

CREATE TABLE D_Room_Vote_Response (
    Vote_ID NUMBER(10) NOT NULL,
    Student_ID VARCHAR2(20) NOT NULL,
    Choice VARCHAR2(10) NOT NULL,
    Vote_Time DATE DEFAULT SYSDATE NOT NULL,
    CONSTRAINT PK_D_ROOM_VOTE_RESP PRIMARY KEY (Vote_ID, Student_ID),
    CONSTRAINT CK_D_ROOM_VOTE_CHOICE
        CHECK (Choice IN ('同意', '不同意')),
    CONSTRAINT FK_D_ROOM_VOTE_RESP_VOTE
        FOREIGN KEY (Vote_ID) REFERENCES D_Room_Vote (Vote_ID),
    CONSTRAINT FK_D_ROOM_VOTE_RESP_STU
        FOREIGN KEY (Student_ID) REFERENCES D_Student (Student_ID)
);

CREATE TABLE D_Checkout_Log (
    Log_ID NUMBER(10) CONSTRAINT PK_D_CHECKOUT_LOG PRIMARY KEY,
    Allocation_ID NUMBER(10) NOT NULL,
    Request_Time DATE DEFAULT SYSDATE NOT NULL,
    Result_Time DATE,
    Fee_Check VARCHAR2(10),
    Item_Check VARCHAR2(10),
    Status VARCHAR2(10) NOT NULL,
    Reject_Reason VARCHAR2(500),
    -- Fee_Check / Item_Check 为 NULL 表示尚未执行对应检查。
    CONSTRAINT CK_D_CHECKOUT_WATER
        CHECK (Fee_Check IS NULL OR Fee_Check IN ('通过', '未通过')),
    CONSTRAINT CK_D_CHECKOUT_ITEM
        CHECK (Item_Check IS NULL OR Item_Check IN ('通过', '未通过')),
    CONSTRAINT CK_D_CHECKOUT_STATUS
        CHECK (Status IN ('待清算', '已通过', '已拒绝', '已取消')),
    CONSTRAINT CK_D_CHECKOUT_RESULT_TIME
        CHECK (Result_Time IS NULL OR Result_Time >= Request_Time),
    CONSTRAINT FK_D_CHECKOUT_ALLOC
        FOREIGN KEY (Allocation_ID) REFERENCES D_Bed_Allocation (Allocation_ID)
);

-- 同一住宿分配最多一条进行中的清算，防止重复提交退宿申请。
-- Oracle 不支持带 WHERE 的部分索引，改用函数索引：非待清算取 NULL，NULL 不参与唯一判定。
CREATE UNIQUE INDEX UK_D_CHECKOUT_ACTIVE
    ON D_Checkout_Log (CASE WHEN Status = '待清算' THEN Allocation_ID END);

CREATE TABLE D_Notice_Display (
    Notice_ID NUMBER(10) CONSTRAINT PK_D_NOTICE_DISPLAY PRIMARY KEY,
    Is_Pinned VARCHAR2(10) DEFAULT '否' NOT NULL,
    Pin_Time DATE,
    CONSTRAINT CK_D_NOTICE_DISPLAY_PIN
        CHECK (Is_Pinned IN ('是', '否')),
    CONSTRAINT FK_D_NOTICE_DISPLAY_NOTICE
        FOREIGN KEY (Notice_ID) REFERENCES D_Notice (Notice_ID)
);

CREATE TABLE D_Hygiene_Comment (
    Record_ID NUMBER(10) CONSTRAINT PK_D_HYGIENE_COMMENT PRIMARY KEY,
    -- COMMENT is an Oracle keyword; retain the approved logical name as a quoted identifier.
    "COMMENT" VARCHAR2(500),
    CONSTRAINT FK_D_HYGIENE_COMMENT_RECORD
        FOREIGN KEY (Record_ID) REFERENCES D_Hygiene_Record (Record_ID)
);
