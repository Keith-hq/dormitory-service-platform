-- ============================================================
-- C8 安全社区黑盒测试种子数据（王浩宇）
-- 覆盖 IT-C8-001 访客二维码 / IT-C8-002 房间投票一人一票。
-- 快递（IT-C8-003）已暂缓，不造快递数据。
--
-- 前置：以 sysdba 连接后执行（脚本内含 CONTAINER/CURRENT_SCHEMA 切换）。
-- 幂等：先按测试 ID 删除本脚本已有数据，再插入；可重复执行。
--
-- 测试账号（登录名 = 学号，密码统一 Test1234）：
--   IT_STU_001 / IT_STU_002  -> 房间 900101（用于 C8-002 同房间投票）
--   IT_STU_003              -> 房间 900102（用于 C8-002 非本房间负例）
--
-- 预留 ID 与《集成测试用例-认领-王浩宇.md》§0 一致：
--   studentId IT_STU_001/002/003，roomId 900101/900102。
-- ============================================================

ALTER SESSION SET CONTAINER = DORMDB;
ALTER SESSION SET CURRENT_SCHEMA = SYSTEM;

-- ---------- 幂等清理（子表 -> 父表） ----------
DELETE FROM D_ROOM_VOTE_RESPONSE
 WHERE VOTE_ID IN (SELECT VOTE_ID FROM D_ROOM_VOTE WHERE ROOM_ID IN (900101, 900102));
DELETE FROM D_ROOM_VOTE WHERE ROOM_ID IN (900101, 900102);
DELETE FROM D_VISITOR_AUTHORIZATION WHERE STUDENT_ID IN ('IT_STU_001', 'IT_STU_002', 'IT_STU_003');
DELETE FROM D_USER_ACCOUNT WHERE LOGIN_NAME IN ('IT_STU_001', 'IT_STU_002', 'IT_STU_003');
DELETE FROM D_BED_ALLOCATION WHERE STUDENT_ID IN ('IT_STU_001', 'IT_STU_002', 'IT_STU_003');
DELETE FROM D_STUDENT WHERE STUDENT_ID IN ('IT_STU_001', 'IT_STU_002', 'IT_STU_003');
DELETE FROM D_ROOM WHERE ROOM_ID IN (900101, 900102);
DELETE FROM D_MAJOR WHERE MAJOR_ID = 1;
DELETE FROM D_BUILDING WHERE BUILDING_ID = 9001;
DELETE FROM D_COLLEGE WHERE COLLEGE_ID = 1;
COMMIT;

-- ---------- 基础数据 ----------
INSERT INTO D_College (College_ID, College_Name, Counselor_Name, Contact_Phone)
VALUES (1, '信息技术学院', '测试辅导员', '13800000001');

INSERT INTO D_Major (Major_ID, College_ID, Major_Name)
VALUES (1, 1, '软件工程');

INSERT INTO D_Building (Building_ID, Building_Name, Building_Type, Total_Floors)
VALUES (9001, '9号楼', '男生楼', 6);

INSERT INTO D_Room (Room_ID, Building_ID, Room_Number, Capacity, Occupancy, Power_Status, Floor, Status)
VALUES (900101, 9001, '900101', 4, 2, '正常', 1, '正常');

INSERT INTO D_Room (Room_ID, Building_ID, Room_Number, Capacity, Occupancy, Power_Status, Floor, Status)
VALUES (900102, 9001, '900102', 4, 1, '正常', 1, '正常');

INSERT INTO D_Student (Student_ID, Name, Gender, Major_ID, Phone, Email)
VALUES ('IT_STU_001', '王浩宇', '男', 1, '13900000001', 'stu001@test.com');

INSERT INTO D_Student (Student_ID, Name, Gender, Major_ID, Phone, Email)
VALUES ('IT_STU_002', '李泽远', '男', 1, '13900000002', 'stu002@test.com');

INSERT INTO D_Student (Student_ID, Name, Gender, Major_ID, Phone, Email)
VALUES ('IT_STU_003', '刘润东', '男', 1, '13900000003', 'stu003@test.com');

-- ---------- 床位分配（访客申请需有在住房间） ----------
-- Allocation_ID 由 SEQ_D_BED_ALLOCATION_ID + 触发器生成。
INSERT INTO D_Bed_Allocation (Student_ID, Room_ID, Bed_No, CheckIn_Date, CheckOut_Date)
VALUES ('IT_STU_001', 900101, 1, SYSDATE - 100, NULL);

INSERT INTO D_Bed_Allocation (Student_ID, Room_ID, Bed_No, CheckIn_Date, CheckOut_Date)
VALUES ('IT_STU_002', 900101, 2, SYSDATE - 100, NULL);

INSERT INTO D_Bed_Allocation (Student_ID, Room_ID, Bed_No, CheckIn_Date, CheckOut_Date)
VALUES ('IT_STU_003', 900102, 1, SYSDATE - 100, NULL);

-- ---------- 登录账号（密码统一 Test1234，BCrypt） ----------
-- Account_ID 由 SEQ_D_USER_ACCOUNT_ID + 触发器生成。
-- IS_FIRST_LOGIN = 'N'：跳过首登改密中间件，直接进入业务接口。
INSERT INTO D_User_Account (Login_Name, Password_Hash, Account_Status, Student_ID, Admin_ID, Create_Time, IS_FIRST_LOGIN)
VALUES ('IT_STU_001', '$2b$10$2qz3O8qspCRD9ZoMo.f1vesIhrv8I6t4B2Dm4CmCpKOHXYyAWqPT6', '正常', 'IT_STU_001', NULL, SYSDATE, 'N');

INSERT INTO D_User_Account (Login_Name, Password_Hash, Account_Status, Student_ID, Admin_ID, Create_Time, IS_FIRST_LOGIN)
VALUES ('IT_STU_002', '$2b$10$2qz3O8qspCRD9ZoMo.f1vesIhrv8I6t4B2Dm4CmCpKOHXYyAWqPT6', '正常', 'IT_STU_002', NULL, SYSDATE, 'N');

INSERT INTO D_User_Account (Login_Name, Password_Hash, Account_Status, Student_ID, Admin_ID, Create_Time, IS_FIRST_LOGIN)
VALUES ('IT_STU_003', '$2b$10$2qz3O8qspCRD9ZoMo.f1vesIhrv8I6t4B2Dm4CmCpKOHXYyAWqPT6', '正常', 'IT_STU_003', NULL, SYSDATE, 'N');

COMMIT;

-- ---------- 验证 ----------
SELECT 'D_College' AS T, COUNT(*) AS N FROM D_College WHERE College_ID = 1
UNION ALL SELECT 'D_Major', COUNT(*) FROM D_Major WHERE Major_ID = 1
UNION ALL SELECT 'D_Building', COUNT(*) FROM D_Building WHERE Building_ID = 9001
UNION ALL SELECT 'D_Room', COUNT(*) FROM D_Room WHERE Room_ID IN (900101, 900102)
UNION ALL SELECT 'D_Student', COUNT(*) FROM D_Student WHERE STUDENT_ID IN ('IT_STU_001','IT_STU_002','IT_STU_003')
UNION ALL SELECT 'D_Bed_Allocation', COUNT(*) FROM D_Bed_Allocation WHERE STUDENT_ID IN ('IT_STU_001','IT_STU_002','IT_STU_003')
UNION ALL SELECT 'D_User_Account', COUNT(*) FROM D_User_Account WHERE LOGIN_NAME IN ('IT_STU_001','IT_STU_002','IT_STU_003');

EXIT;
