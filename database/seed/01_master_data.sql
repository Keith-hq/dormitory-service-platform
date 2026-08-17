SET DEFINE OFF;
SET AUTOCOMMIT ON;
-- 01 主数据：学院/专业/楼栋/房间/学生/管理员/账号/住宿分配/资产/资产预警
-- 依赖：00_cleanup.sql 已执行；foundation/001 与 extensions/010~032 已建库。
-- 主键全部落在 9xxxxx 段 / IT_ 前缀，与应用序列（1 起）互不撞号。

-- ===== 1. 学院（D_College） =====
INSERT INTO D_College (College_ID, College_Name, Counselor_Name, Contact_Phone) VALUES (9001, '信息技术学院', '陈慧', '13900001001');
INSERT INTO D_College (College_ID, College_Name, Counselor_Name, Contact_Phone) VALUES (9002, '机电工程学院', '周建国', '13900001002');
INSERT INTO D_College (College_ID, College_Name, Counselor_Name, Contact_Phone) VALUES (9003, '外国语学院', '刘敏华', '13900001003');

-- ===== 2. 专业（D_Major） =====
INSERT INTO D_Major (Major_ID, College_ID, Major_Name) VALUES (9001, 9001, '计算机科学与技术');
INSERT INTO D_Major (Major_ID, College_ID, Major_Name) VALUES (9002, 9001, '软件工程');
INSERT INTO D_Major (Major_ID, College_ID, Major_Name) VALUES (9003, 9002, '电气工程及其自动化');
INSERT INTO D_Major (Major_ID, College_ID, Major_Name) VALUES (9004, 9002, '机械设计制造及其自动化');
INSERT INTO D_Major (Major_ID, College_ID, Major_Name) VALUES (9005, 9003, '英语');
INSERT INTO D_Major (Major_ID, College_ID, Major_Name) VALUES (9006, 9003, '日语');

-- ===== 3. 楼栋（D_Building） =====
INSERT INTO D_Building (Building_ID, Building_Name, Building_Type, Total_Floors) VALUES (9001, '男生宿舍楼', '男生宿舍', 6);
INSERT INTO D_Building (Building_ID, Building_Name, Building_Type, Total_Floors) VALUES (9002, '女生宿舍楼', '女生宿舍', 6);

-- ===== 4. 房间（D_Room）48 间，Room_ID=楼栋4位+房号2位 =====
-- 900102 为欠费断电样本（Power_Status='断电'）。
INSERT INTO D_Room (Room_ID, Building_ID, Room_Number, Capacity, Occupancy, Power_Status, Floor, Status)
SELECT 900100 + n, 9001, LPAD(n, 2, '0'), 4, 0,
       CASE WHEN n = 2 THEN '断电' ELSE '正常' END,
       1 + TRUNC((n - 1) / 4), '正常'
FROM (SELECT LEVEL n FROM DUAL CONNECT BY LEVEL <= 24);

INSERT INTO D_Room (Room_ID, Building_ID, Room_Number, Capacity, Occupancy, Power_Status, Floor, Status)
SELECT 900200 + n, 9002, LPAD(n, 2, '0'), 4, 0, '正常', 1 + TRUNC((n - 1) / 4), '正常'
FROM (SELECT LEVEL n FROM DUAL CONNECT BY LEVEL <= 24);

-- ===== 5. 管理员（D_Admin） =====
-- 注意：IT_COUN_001（辅导员）按代码口径写入。需目标库已应用迁移 031
-- （CK_D_ADMIN_ROLE 纳入"辅导员"，ADR-0007）；未应用则 ORA-02290。
INSERT INTO D_Admin (Admin_ID, Admin_Name, Phone, Role_Level, Building_ID, POST) VALUES ('IT_ADMIN_001', '王建国', '13900000001', '楼长',       9001, '楼长');
INSERT INTO D_Admin (Admin_ID, Admin_Name, Phone, Role_Level, Building_ID, POST) VALUES ('IT_REPAIR_001', '张建军', '13900000002', '维修员',     9001, '维修员');
INSERT INTO D_Admin (Admin_ID, Admin_Name, Phone, Role_Level, Building_ID, POST) VALUES ('IT_REPAIR_002', '李铁柱', '13900000003', '维修员',     9002, '维修员');
INSERT INTO D_Admin (Admin_ID, Admin_Name, Phone, Role_Level, Building_ID, POST) VALUES ('IT_COUN_001',   '陈慧',   '13900000004', '辅导员',     NULL, '辅导员');
INSERT INTO D_Admin (Admin_ID, Admin_Name, Phone, Role_Level, Building_ID, POST) VALUES ('IT_SUPER_001',  '赵志远', '13900000005', '超级管理员', NULL, '超级管理员');

-- ===== 6. 学生（D_Student）30 名在册 + 1 名已退宿 =====
-- IT_STU_002 / IT_STU_005 无床位：002 供"并发抢床位"演示，005 供"首登改密"演示。
INSERT INTO D_Student (Student_ID, Name, Gender, Major_ID, Phone, Email) VALUES ('IT_STU_001',  '张伟', 'M', 9001, '13800000001', 'zhangwei@stu.example.edu.cn');
INSERT INTO D_Student (Student_ID, Name, Gender, Major_ID, Phone, Email) VALUES ('IT_STU_002',  '王强', 'M', 9002, '13800000002', 'wangqiang@stu.example.edu.cn');
INSERT INTO D_Student (Student_ID, Name, Gender, Major_ID, Phone, Email) VALUES ('IT_STU_003',  '李磊', 'M', 9001, '13800000003', 'lilei@stu.example.edu.cn');
INSERT INTO D_Student (Student_ID, Name, Gender, Major_ID, Phone, Email) VALUES ('IT_STU_004',  '刘洋', 'M', 9002, '13800000004', 'liuyang@stu.example.edu.cn');
INSERT INTO D_Student (Student_ID, Name, Gender, Major_ID, Phone, Email) VALUES ('IT_STU_005',  '陈俊', 'M', 9003, '13800000005', 'chenjun@stu.example.edu.cn');
INSERT INTO D_Student (Student_ID, Name, Gender, Major_ID, Phone, Email) VALUES ('IT_STU_006',  '杨帆', 'M', 9003, '13800000006', 'yangfan@stu.example.edu.cn');
INSERT INTO D_Student (Student_ID, Name, Gender, Major_ID, Phone, Email) VALUES ('IT_STU_007',  '赵鹏', 'M', 9005, '13800000007', 'zhaopeng@stu.example.edu.cn');
INSERT INTO D_Student (Student_ID, Name, Gender, Major_ID, Phone, Email) VALUES ('IT_STU_008',  '黄明', 'M', 9001, '13800000008', 'huangming@stu.example.edu.cn');
INSERT INTO D_Student (Student_ID, Name, Gender, Major_ID, Phone, Email) VALUES ('IT_STU_009',  '周涛', 'M', 9001, '13800000009', 'zhoutao@stu.example.edu.cn');
INSERT INTO D_Student (Student_ID, Name, Gender, Major_ID, Phone, Email) VALUES ('IT_STU_010',  '吴杰', 'M', 9002, '13800000010', 'wujie@stu.example.edu.cn');
INSERT INTO D_Student (Student_ID, Name, Gender, Major_ID, Phone, Email) VALUES ('IT_STU_011',  '徐凯', 'M', 9002, '13800000011', 'xukai@stu.example.edu.cn');
INSERT INTO D_Student (Student_ID, Name, Gender, Major_ID, Phone, Email) VALUES ('IT_STU_012',  '孙浩', 'M', 9003, '13800000012', 'sunhao@stu.example.edu.cn');
INSERT INTO D_Student (Student_ID, Name, Gender, Major_ID, Phone, Email) VALUES ('IT_STU_013',  '马超', 'M', 9003, '13800000013', 'machao@stu.example.edu.cn');
INSERT INTO D_Student (Student_ID, Name, Gender, Major_ID, Phone, Email) VALUES ('IT_STU_014',  '朱峰', 'M', 9004, '13800000014', 'zhufeng@stu.example.edu.cn');
INSERT INTO D_Student (Student_ID, Name, Gender, Major_ID, Phone, Email) VALUES ('IT_STU_015',  '胡鑫', 'M', 9004, '13800000015', 'huxin@stu.example.edu.cn');
INSERT INTO D_Student (Student_ID, Name, Gender, Major_ID, Phone, Email) VALUES ('IT_STU_016',  '李静', 'F', 9005, '13800000016', 'lijing@stu.example.edu.cn');
INSERT INTO D_Student (Student_ID, Name, Gender, Major_ID, Phone, Email) VALUES ('IT_STU_017',  '王芳', 'F', 9005, '13800000017', 'wangfang@stu.example.edu.cn');
INSERT INTO D_Student (Student_ID, Name, Gender, Major_ID, Phone, Email) VALUES ('IT_STU_018',  '张丽', 'F', 9006, '13800000018', 'zhangli@stu.example.edu.cn');
INSERT INTO D_Student (Student_ID, Name, Gender, Major_ID, Phone, Email) VALUES ('IT_STU_019',  '刘敏', 'F', 9005, '13800000019', 'liumin@stu.example.edu.cn');
INSERT INTO D_Student (Student_ID, Name, Gender, Major_ID, Phone, Email) VALUES ('IT_STU_020',  '陈燕', 'F', 9006, '13800000020', 'chenyan@stu.example.edu.cn');
INSERT INTO D_Student (Student_ID, Name, Gender, Major_ID, Phone, Email) VALUES ('IT_STU_021',  '杨雪', 'F', 9005, '13800000021', 'yangxue@stu.example.edu.cn');
INSERT INTO D_Student (Student_ID, Name, Gender, Major_ID, Phone, Email) VALUES ('IT_STU_022',  '赵蕾', 'F', 9006, '13800000022', 'zhaolei@stu.example.edu.cn');
INSERT INTO D_Student (Student_ID, Name, Gender, Major_ID, Phone, Email) VALUES ('IT_STU_023',  '黄娟', 'F', 9006, '13800000023', 'huangjuan@stu.example.edu.cn');
INSERT INTO D_Student (Student_ID, Name, Gender, Major_ID, Phone, Email) VALUES ('IT_STU_024',  '周悦', 'F', 9005, '13800000024', 'zhouyue@stu.example.edu.cn');
INSERT INTO D_Student (Student_ID, Name, Gender, Major_ID, Phone, Email) VALUES ('IT_STU_025',  '吴婷', 'F', 9006, '13800000025', 'wuting@stu.example.edu.cn');
INSERT INTO D_Student (Student_ID, Name, Gender, Major_ID, Phone, Email) VALUES ('IT_STU_026',  '徐媛', 'F', 9005, '13800000026', 'xuyuan@stu.example.edu.cn');
INSERT INTO D_Student (Student_ID, Name, Gender, Major_ID, Phone, Email) VALUES ('IT_STU_027',  '孙琳', 'F', 9006, '13800000027', 'sunlin@stu.example.edu.cn');
INSERT INTO D_Student (Student_ID, Name, Gender, Major_ID, Phone, Email) VALUES ('IT_STU_028',  '马慧', 'F', 9005, '13800000028', 'mahui@stu.example.edu.cn');
INSERT INTO D_Student (Student_ID, Name, Gender, Major_ID, Phone, Email) VALUES ('IT_STU_029',  '朱萍', 'F', 9006, '13800000029', 'zhuping@stu.example.edu.cn');
INSERT INTO D_Student (Student_ID, Name, Gender, Major_ID, Phone, Email) VALUES ('IT_STU_030',  '胡珊', 'F', 9005, '13800000030', 'hushan@stu.example.edu.cn');
INSERT INTO D_Student (Student_ID, Name, Gender, Major_ID, Phone, Email) VALUES ('IT_STU_031',  '刘志强', 'M', 9004, '13800000031', 'liuzhiqiang@stu.example.edu.cn');

-- ===== 7. 登录账号（D_User_Account）35 个 =====
-- 演示密码统一明文 Temp@123（BCrypt），Is_First_Login='N' 登录即用；
-- IT_STU_005 为"新开通账号"：初始密码 123456、Is_First_Login='Y'（首登强制改密 C1-003）。
-- 学生账号 908001..908030；管理员账号 908031..908035。
INSERT INTO D_User_Account (Account_ID, Login_Name, Password_Hash, Account_Status, Student_ID, Admin_ID, Is_First_Login, Create_Time)
SELECT 908000 + n,
       'IT_STU_' || LPAD(n, 3, '0'),
       CASE WHEN n = 5 THEN '$2a$11$HiG4gNIxzg16n6u33UgVkOP.yMFKZZxRMLTiVeDkF1WkzAkttmVyu'
            ELSE '$2a$11$jP.12TGx4RZOEzncoreME.yM25dBcY9uqkOsJL8YSp1sjHGNH0w8e' END,
       '正常',
       'IT_STU_' || LPAD(n, 3, '0'),
       NULL,
       CASE WHEN n = 5 THEN 'Y' ELSE 'N' END,
       DATE '2026-02-01'
FROM (SELECT LEVEL n FROM DUAL CONNECT BY LEVEL <= 30);

INSERT INTO D_User_Account (Account_ID, Login_Name, Password_Hash, Account_Status, Student_ID, Admin_ID, Is_First_Login, Create_Time)
VALUES (908031, 'IT_ADMIN_001', '$2a$11$jP.12TGx4RZOEzncoreME.yM25dBcY9uqkOsJL8YSp1sjHGNH0w8e', '正常', NULL, 'IT_ADMIN_001', 'N', DATE '2026-02-01');
INSERT INTO D_User_Account (Account_ID, Login_Name, Password_Hash, Account_Status, Student_ID, Admin_ID, Is_First_Login, Create_Time)
VALUES (908032, 'IT_REPAIR_001', '$2a$11$jP.12TGx4RZOEzncoreME.yM25dBcY9uqkOsJL8YSp1sjHGNH0w8e', '正常', NULL, 'IT_REPAIR_001', 'N', DATE '2026-02-01');
INSERT INTO D_User_Account (Account_ID, Login_Name, Password_Hash, Account_Status, Student_ID, Admin_ID, Is_First_Login, Create_Time)
VALUES (908033, 'IT_REPAIR_002', '$2a$11$jP.12TGx4RZOEzncoreME.yM25dBcY9uqkOsJL8YSp1sjHGNH0w8e', '正常', NULL, 'IT_REPAIR_002', 'N', DATE '2026-02-01');
INSERT INTO D_User_Account (Account_ID, Login_Name, Password_Hash, Account_Status, Student_ID, Admin_ID, Is_First_Login, Create_Time)
VALUES (908034, 'IT_COUN_001',   '$2a$11$jP.12TGx4RZOEzncoreME.yM25dBcY9uqkOsJL8YSp1sjHGNH0w8e', '正常', NULL, 'IT_COUN_001',   'N', DATE '2026-02-01');
INSERT INTO D_User_Account (Account_ID, Login_Name, Password_Hash, Account_Status, Student_ID, Admin_ID, Is_First_Login, Create_Time)
VALUES (908035, 'IT_SUPER_001',  '$2a$11$jP.12TGx4RZOEzncoreME.yM25dBcY9uqkOsJL8YSp1sjHGNH0w8e', '正常', NULL, 'IT_SUPER_001',  'N', DATE '2026-02-01');

-- ===== 8. 住宿分配（D_Bed_Allocation）30 条 =====
-- 28 名在住 + 2 条历史（008 8/10 换寝、031 7/31 退宿）。
-- 002 / 005 无床位。
INSERT INTO D_Bed_Allocation (Allocation_ID, Student_ID, Room_ID, Bed_No, CheckIn_Date, CheckOut_Date) VALUES (9500001, 'IT_STU_001', 900101, 1, DATE '2026-02-01', NULL);
INSERT INTO D_Bed_Allocation (Allocation_ID, Student_ID, Room_ID, Bed_No, CheckIn_Date, CheckOut_Date) VALUES (9500002, 'IT_STU_003', 900102, 1, DATE '2026-02-01', NULL);
INSERT INTO D_Bed_Allocation (Allocation_ID, Student_ID, Room_ID, Bed_No, CheckIn_Date, CheckOut_Date) VALUES (9500003, 'IT_STU_004', 900102, 2, DATE '2026-02-01', NULL);
INSERT INTO D_Bed_Allocation (Allocation_ID, Student_ID, Room_ID, Bed_No, CheckIn_Date, CheckOut_Date) VALUES (9500004, 'IT_STU_006', 900103, 1, DATE '2026-02-01', NULL);
INSERT INTO D_Bed_Allocation (Allocation_ID, Student_ID, Room_ID, Bed_No, CheckIn_Date, CheckOut_Date) VALUES (9500005, 'IT_STU_007', 900103, 2, DATE '2026-02-01', NULL);
INSERT INTO D_Bed_Allocation (Allocation_ID, Student_ID, Room_ID, Bed_No, CheckIn_Date, CheckOut_Date) VALUES (9500006, 'IT_STU_008', 900103, 3, DATE '2026-02-01', DATE '2026-08-10');
INSERT INTO D_Bed_Allocation (Allocation_ID, Student_ID, Room_ID, Bed_No, CheckIn_Date, CheckOut_Date) VALUES (9500007, 'IT_STU_008', 900104, 1, DATE '2026-08-10', NULL);
INSERT INTO D_Bed_Allocation (Allocation_ID, Student_ID, Room_ID, Bed_No, CheckIn_Date, CheckOut_Date) VALUES (9500008, 'IT_STU_009', 900105, 1, DATE '2026-02-01', NULL);
INSERT INTO D_Bed_Allocation (Allocation_ID, Student_ID, Room_ID, Bed_No, CheckIn_Date, CheckOut_Date) VALUES (9500009, 'IT_STU_010', 900105, 2, DATE '2026-02-01', NULL);
INSERT INTO D_Bed_Allocation (Allocation_ID, Student_ID, Room_ID, Bed_No, CheckIn_Date, CheckOut_Date) VALUES (9500010, 'IT_STU_011', 900105, 3, DATE '2026-02-01', NULL);
INSERT INTO D_Bed_Allocation (Allocation_ID, Student_ID, Room_ID, Bed_No, CheckIn_Date, CheckOut_Date) VALUES (9500011, 'IT_STU_012', 900105, 4, DATE '2026-02-01', NULL);
INSERT INTO D_Bed_Allocation (Allocation_ID, Student_ID, Room_ID, Bed_No, CheckIn_Date, CheckOut_Date) VALUES (9500012, 'IT_STU_013', 900106, 1, DATE '2026-02-01', NULL);
INSERT INTO D_Bed_Allocation (Allocation_ID, Student_ID, Room_ID, Bed_No, CheckIn_Date, CheckOut_Date) VALUES (9500013, 'IT_STU_014', 900106, 2, DATE '2026-02-01', NULL);
INSERT INTO D_Bed_Allocation (Allocation_ID, Student_ID, Room_ID, Bed_No, CheckIn_Date, CheckOut_Date) VALUES (9500014, 'IT_STU_015', 900107, 1, DATE '2026-02-01', NULL);
INSERT INTO D_Bed_Allocation (Allocation_ID, Student_ID, Room_ID, Bed_No, CheckIn_Date, CheckOut_Date) VALUES (9500015, 'IT_STU_016', 900201, 1, DATE '2026-02-01', NULL);
INSERT INTO D_Bed_Allocation (Allocation_ID, Student_ID, Room_ID, Bed_No, CheckIn_Date, CheckOut_Date) VALUES (9500016, 'IT_STU_017', 900201, 2, DATE '2026-02-01', NULL);
INSERT INTO D_Bed_Allocation (Allocation_ID, Student_ID, Room_ID, Bed_No, CheckIn_Date, CheckOut_Date) VALUES (9500017, 'IT_STU_018', 900201, 3, DATE '2026-02-01', NULL);
INSERT INTO D_Bed_Allocation (Allocation_ID, Student_ID, Room_ID, Bed_No, CheckIn_Date, CheckOut_Date) VALUES (9500018, 'IT_STU_019', 900202, 1, DATE '2026-02-01', NULL);
INSERT INTO D_Bed_Allocation (Allocation_ID, Student_ID, Room_ID, Bed_No, CheckIn_Date, CheckOut_Date) VALUES (9500019, 'IT_STU_020', 900202, 2, DATE '2026-02-01', NULL);
INSERT INTO D_Bed_Allocation (Allocation_ID, Student_ID, Room_ID, Bed_No, CheckIn_Date, CheckOut_Date) VALUES (9500020, 'IT_STU_021', 900203, 1, DATE '2026-02-01', NULL);
INSERT INTO D_Bed_Allocation (Allocation_ID, Student_ID, Room_ID, Bed_No, CheckIn_Date, CheckOut_Date) VALUES (9500021, 'IT_STU_022', 900203, 2, DATE '2026-02-01', NULL);
INSERT INTO D_Bed_Allocation (Allocation_ID, Student_ID, Room_ID, Bed_No, CheckIn_Date, CheckOut_Date) VALUES (9500022, 'IT_STU_023', 900203, 3, DATE '2026-02-01', NULL);
INSERT INTO D_Bed_Allocation (Allocation_ID, Student_ID, Room_ID, Bed_No, CheckIn_Date, CheckOut_Date) VALUES (9500023, 'IT_STU_024', 900203, 4, DATE '2026-02-01', NULL);
INSERT INTO D_Bed_Allocation (Allocation_ID, Student_ID, Room_ID, Bed_No, CheckIn_Date, CheckOut_Date) VALUES (9500024, 'IT_STU_025', 900204, 1, DATE '2026-02-01', NULL);
INSERT INTO D_Bed_Allocation (Allocation_ID, Student_ID, Room_ID, Bed_No, CheckIn_Date, CheckOut_Date) VALUES (9500025, 'IT_STU_026', 900205, 1, DATE '2026-02-01', NULL);
INSERT INTO D_Bed_Allocation (Allocation_ID, Student_ID, Room_ID, Bed_No, CheckIn_Date, CheckOut_Date) VALUES (9500026, 'IT_STU_027', 900205, 2, DATE '2026-02-01', NULL);
INSERT INTO D_Bed_Allocation (Allocation_ID, Student_ID, Room_ID, Bed_No, CheckIn_Date, CheckOut_Date) VALUES (9500027, 'IT_STU_028', 900206, 1, DATE '2026-02-01', NULL);
INSERT INTO D_Bed_Allocation (Allocation_ID, Student_ID, Room_ID, Bed_No, CheckIn_Date, CheckOut_Date) VALUES (9500028, 'IT_STU_029', 900206, 2, DATE '2026-02-01', NULL);
INSERT INTO D_Bed_Allocation (Allocation_ID, Student_ID, Room_ID, Bed_No, CheckIn_Date, CheckOut_Date) VALUES (9500029, 'IT_STU_030', 900206, 3, DATE '2026-02-01', NULL);
INSERT INTO D_Bed_Allocation (Allocation_ID, Student_ID, Room_ID, Bed_No, CheckIn_Date, CheckOut_Date) VALUES (9500030, 'IT_STU_031', 900107, 2, DATE '2026-02-01', DATE '2026-07-31');

-- ===== 9. 资产（D_Asset）每房 4 件标准家具 + 3 件特殊 =====
INSERT INTO D_Asset (Asset_ID, Room_ID, Asset_Name, Quantity, Status)
SELECT r.Room_ID * 10 + k,
       r.Room_ID,
       CASE k WHEN 1 THEN '床' WHEN 2 THEN '书桌' WHEN 3 THEN '椅子' ELSE '衣柜' END,
       1, '正常'
FROM D_Room r,
     (SELECT LEVEL k FROM DUAL CONNECT BY LEVEL <= 4)
WHERE r.Room_ID BETWEEN 900101 AND 900124
   OR r.Room_ID BETWEEN 900201 AND 900224;

-- 特殊资产：损坏 / 缺失样本（供 DORM-16/17/18 损耗预警演示）
INSERT INTO D_Asset (Asset_ID, Room_ID, Asset_Name, Quantity, Status) VALUES (9300001, 900101, '空调',   1, '损坏');
INSERT INTO D_Asset (Asset_ID, Room_ID, Asset_Name, Quantity, Status) VALUES (9300002, 900105, '热水器', 1, '损坏');
INSERT INTO D_Asset (Asset_ID, Room_ID, Asset_Name, Quantity, Status) VALUES (9300003, 900102, '台灯',   1, '缺失');

-- ===== 10. 资产损耗预警（D_Asset_Warning） =====
-- 9300001 未处理（待报修）；9300002 已处理；9300003 标记重点。
INSERT INTO D_Asset_Warning (Warning_ID, Asset_ID, Create_Time, Note, Handle_Action, Handle_Time, Handled)
VALUES (9300001, 9300001, DATE '2026-08-01', '空调无法制冷，待报修', NULL, NULL, '否');
INSERT INTO D_Asset_Warning (Warning_ID, Asset_ID, Create_Time, Note, Handle_Action, Handle_Time, Handled)
VALUES (9300002, 9300002, DATE '2026-07-20', '热水器不加热', '处理', DATE '2026-07-20', '是');
INSERT INTO D_Asset_Warning (Warning_ID, Asset_ID, Create_Time, Note, Handle_Action, Handle_Time, Handled)
VALUES (9300003, 9300003, DATE '2026-07-25', '台灯缺失', '标记重点', DATE '2026-07-25', '是');

-- ===== 11. 回填房间占用数（Occupancy 不手写，按活动分配计算） =====
UPDATE D_Room r
SET r.Occupancy = (SELECT COUNT(*)
                     FROM D_Bed_Allocation a
                    WHERE a.Room_ID = r.Room_ID
                      AND a.CheckOut_Date IS NULL);

-- 完成确认
SELECT 'MASTER DATA DONE' AS MESSAGE FROM DUAL;
