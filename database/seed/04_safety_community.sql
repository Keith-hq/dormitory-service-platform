-- 04 安全社区：门禁/晚归/访客授权/访客登记/快递/投票+响应/违规/订水
-- 依赖：01/03 已执行。
-- 链路：C8 安全社区（访客二维码/投票/快递）、ACCESS-01/02 门禁与楼内密度。

-- ===== 1. 门禁记录 D_Access_Log（在住学生 × 近 7 天 × 进出各 1 = 数百条，循环生成） =====
INSERT INTO D_Access_Log (Log_ID, Student_ID, Building_ID, Swipe_Time, Direction)
SELECT 909201 + ROWNUM, a.Student_ID, r.Building_ID,
       TO_DATE('2026-08-11', 'YYYY-MM-DD') + (d - 1)
         + (CASE WHEN s = 1 THEN 0.37 ELSE 0.75 END),
       CASE WHEN s = 1 THEN '进' ELSE '出' END
FROM (SELECT Student_ID, Room_ID FROM D_Bed_Allocation WHERE CheckOut_Date IS NULL) a
JOIN D_Room r ON r.Room_ID = a.Room_ID
CROSS JOIN (SELECT LEVEL d FROM DUAL CONNECT BY LEVEL <= 7) days
CROSS JOIN (SELECT LEVEL s FROM DUAL CONNECT BY LEVEL <= 2) sides;

-- ===== 2. 晚归记录 D_Late_Entry（1 条未补说明 / 1 条已补说明） =====
INSERT INTO D_Late_Entry (Record_ID, Student_ID, Return_Time, Reason)
VALUES (909301, 'IT_STU_001', TO_DATE('2026-08-15 23:40', 'YYYY-MM-DD HH24:MI'), NULL);
INSERT INTO D_Late_Entry (Record_ID, Student_ID, Return_Time, Reason)
VALUES (909302, 'IT_STU_006', TO_DATE('2026-08-12 00:10', 'YYYY-MM-DD HH24:MI'), '实习加班');

-- ===== 3. 访客授权 D_Visitor_Authorization（有效/已过期/已撤销 各 1，Token 唯一） =====
-- 注意：Create_Time 显式设置，保证 Expires_Time > Create_Time（CK 约束）。
INSERT INTO D_Visitor_Authorization (Authorization_ID, Student_ID, Room_ID, Visitor_Name, Visit_Reason, Authorization_Token, Expires_Time, Status, Create_Time)
VALUES (909001, 'IT_STU_001', 900101, '李明',   '看望同学',     'VST-IT-001', DATE '2026-08-20', '有效',   DATE '2026-08-14');
INSERT INTO D_Visitor_Authorization (Authorization_ID, Student_ID, Room_ID, Visitor_Name, Visit_Reason, Authorization_Token, Expires_Time, Status, Create_Time)
VALUES (909002, 'IT_STU_016', 900201, '周阿姨', '家长来访',     'VST-IT-002', DATE '2026-08-10', '已过期', DATE '2026-08-05');
INSERT INTO D_Visitor_Authorization (Authorization_ID, Student_ID, Room_ID, Visitor_Name, Visit_Reason, Authorization_Token, Expires_Time, Status, Create_Time)
VALUES (909003, 'IT_STU_001', 900101, '王叔叔', '送生活用品',   'VST-IT-003', DATE '2026-08-18', '已撤销', DATE '2026-08-14');

-- ===== 4. 访客登记 D_Visitor_Log（未离开 / 已离开） =====
INSERT INTO D_Visitor_Log (Visitor_ID, Building_ID, Visitor_Name, Visit_Reason, Entry_Time, Leave_Time)
VALUES (909101, 9001, '李明',   '看望同学', DATE '2026-08-15', NULL);
INSERT INTO D_Visitor_Log (Visitor_ID, Building_ID, Visitor_Name, Visit_Reason, Entry_Time, Leave_Time)
VALUES (909102, 9001, '周阿姨', '家长来访', DATE '2026-08-09', DATE '2026-08-09');

-- ===== 5. 快递 D_Parcel_Record（004 未取 = C2-004/C8-003 校验样本） =====
INSERT INTO D_Parcel_Record (Parcel_ID, Student_ID, Arrive_Time, Pickup_Time, Courier_Company)
VALUES (909401, 'IT_STU_004', DATE '2026-08-15', NULL, '顺丰');
INSERT INTO D_Parcel_Record (Parcel_ID, Student_ID, Arrive_Time, Pickup_Time, Courier_Company)
VALUES (909402, 'IT_STU_001', DATE '2026-08-12', DATE '2026-08-13', '中通');

-- ===== 6. 房间投票 D_Room_Vote + D_Room_Vote_Response（一人一票复合主键） =====
INSERT INTO D_Room_Vote (Vote_ID, Room_ID, Initiator_Student_ID, Topic, Create_Time, Deadline, Eligible_Count, Status)
VALUES (909501, 900105, 'IT_STU_012', '是否同意在楼层增设公共洗衣机', DATE '2026-08-14', DATE '2026-08-20', 4, '进行中');
INSERT INTO D_Room_Vote_Response (Vote_ID, Student_ID, Choice, Vote_Time) VALUES (909501, 'IT_STU_009', '同意',   DATE '2026-08-14');
INSERT INTO D_Room_Vote_Response (Vote_ID, Student_ID, Choice, Vote_Time) VALUES (909501, 'IT_STU_010', '同意',   DATE '2026-08-14');
INSERT INTO D_Room_Vote_Response (Vote_ID, Student_ID, Choice, Vote_Time) VALUES (909501, 'IT_STU_011', '不同意', DATE '2026-08-15');
INSERT INTO D_Room_Vote_Response (Vote_ID, Student_ID, Choice, Vote_Time) VALUES (909501, 'IT_STU_012', '同意',   DATE '2026-08-15');

INSERT INTO D_Room_Vote (Vote_ID, Room_ID, Initiator_Student_ID, Topic, Create_Time, Deadline, Eligible_Count, Status)
VALUES (909502, 900203, 'IT_STU_021', '是否组织寝室集体活动', DATE '2026-07-20', DATE '2026-07-27', 4, '已通过');
INSERT INTO D_Room_Vote_Response (Vote_ID, Student_ID, Choice, Vote_Time) VALUES (909502, 'IT_STU_021', '同意',   DATE '2026-07-21');
INSERT INTO D_Room_Vote_Response (Vote_ID, Student_ID, Choice, Vote_Time) VALUES (909502, 'IT_STU_022', '同意',   DATE '2026-07-21');
INSERT INTO D_Room_Vote_Response (Vote_ID, Student_ID, Choice, Vote_Time) VALUES (909502, 'IT_STU_023', '同意',   DATE '2026-07-22');
INSERT INTO D_Room_Vote_Response (Vote_ID, Student_ID, Choice, Vote_Time) VALUES (909502, 'IT_STU_024', '不同意', DATE '2026-07-23');

-- ===== 7. 违规 D_Violation_Record（联动信用分扣分，见 05 信用分） =====
INSERT INTO D_Violation_Record (Record_ID, Student_ID, Room_ID, Vio_Type, Vio_Date, Penalty)
VALUES (909601, 'IT_STU_003', 900102, '使用违规电器', DATE '2026-07-25', '警告并没收');
INSERT INTO D_Violation_Record (Record_ID, Student_ID, Room_ID, Vio_Type, Vio_Date, Penalty)
VALUES (909602, 'IT_STU_007', 900103, '晚归未登记',   DATE '2026-08-02', '扣信用分5分');
INSERT INTO D_Violation_Record (Record_ID, Student_ID, Room_ID, Vio_Type, Vio_Date, Penalty)
VALUES (909603, 'IT_STU_009', 900105, '公共区域大声喧哗', DATE '2026-07-28', '批评教育');
INSERT INTO D_Violation_Record (Record_ID, Student_ID, Room_ID, Vio_Type, Vio_Date, Penalty)
VALUES (909604, 'IT_STU_003', 900102, '走廊堆放杂物', DATE '2026-08-01', '整改并扣分');

-- ===== 8. 桶装水 D_Water_Order（WATER 无控制器，按契约造状态样本兜底） =====
INSERT INTO D_Water_Order (Order_ID, Room_ID, Order_Time, Quantity, Status)
VALUES (909701, 900101, DATE '2026-08-16', 2, '未送达');
INSERT INTO D_Water_Order (Order_ID, Room_ID, Order_Time, Quantity, Status)
VALUES (909702, 900102, DATE '2026-08-15', 1, '已送达');
INSERT INTO D_Water_Order (Order_ID, Room_ID, Order_Time, Quantity, Status)
VALUES (909703, 900201, DATE '2026-08-14', 1, '配送中');

-- 完成确认
SELECT 'SAFETY & COMMUNITY DONE' AS MESSAGE FROM DUAL;
