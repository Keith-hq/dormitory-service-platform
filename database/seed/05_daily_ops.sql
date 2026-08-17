SET DEFINE OFF;
SET AUTOCOMMIT ON;
-- 05 日常运营：卫生+评论/保洁任务/信用分/通知/审计/退宿清算/离校报备
-- 依赖：01/03/04 已执行。
-- P0 链路 C2（退宿清算）、C6（信用分申诉）；报表/排名/通知演示。

-- ===== 1. 卫生记录 D_Hygiene_Record（3 个月 × 在住房间，生成）+ 评论 =====
-- 手写 900101 7 月 1 条带评论；其余批量生成（900101 除外，避免重复月度记录）。
INSERT INTO D_Hygiene_Record (Record_ID, Room_ID, Check_Date, Score, Inspector_ID)
VALUES (9091001, 900101, DATE '2026-07-31', 95, 'IT_ADMIN_001');
INSERT INTO D_Hygiene_Comment (Record_ID, "COMMENT")
VALUES (9091001, '整体整洁，物品摆放有序，评分优秀');

INSERT INTO D_Hygiene_Record (Record_ID, Room_ID, Check_Date, Score, Inspector_ID)
SELECT 9091500 + ROWNUM, r.Room_ID,
       CASE m WHEN 1 THEN DATE '2026-05-31' WHEN 2 THEN DATE '2026-06-30' ELSE DATE '2026-07-31' END,
       72 + MOD((r.Room_ID * m), 26),
       'IT_ADMIN_001'
FROM D_Room r
CROSS JOIN (SELECT LEVEL m FROM DUAL CONNECT BY LEVEL <= 3) mm
WHERE (r.Room_ID BETWEEN 900101 AND 900124 OR r.Room_ID BETWEEN 900201 AND 900224)
  AND r.Room_ID <> 900101;

-- ===== 2. 保洁任务 D_Cleaning_Task（待处理 / 已完成 各 2） =====
INSERT INTO D_Cleaning_Task (Task_ID, Facility_ID, Trigger_Count, Status, Create_Time, Complete_Time)
VALUES (901401, 901203, 1, '待处理', DATE '2026-08-16', NULL);
INSERT INTO D_Cleaning_Task (Task_ID, Facility_ID, Trigger_Count, Status, Create_Time, Complete_Time)
VALUES (901402, 901201, 2, '已完成', DATE '2026-08-12', DATE '2026-08-13');
INSERT INTO D_Cleaning_Task (Task_ID, Facility_ID, Trigger_Count, Status, Create_Time, Complete_Time)
VALUES (901403, 901205, 1, '已完成', DATE '2026-08-10', DATE '2026-08-10');
INSERT INTO D_Cleaning_Task (Task_ID, Facility_ID, Trigger_Count, Status, Create_Time, Complete_Time)
VALUES (901404, 901204, 1, '待处理', DATE '2026-08-17', NULL);

-- ===== 3. 信用分 D_Credit_Account（30 学生，与 D_Credit_Log 对齐） =====
INSERT INTO D_Credit_Account (Student_ID, Current_Score, Updated_Time) VALUES ('IT_STU_001', 100, DATE '2026-02-01');
INSERT INTO D_Credit_Account (Student_ID, Current_Score, Updated_Time) VALUES ('IT_STU_002', 100, DATE '2026-02-01');
INSERT INTO D_Credit_Account (Student_ID, Current_Score, Updated_Time) VALUES ('IT_STU_003', 80,  DATE '2026-08-01');
INSERT INTO D_Credit_Account (Student_ID, Current_Score, Updated_Time) VALUES ('IT_STU_004', 100, DATE '2026-02-01');
INSERT INTO D_Credit_Account (Student_ID, Current_Score, Updated_Time) VALUES ('IT_STU_005', 100, DATE '2026-02-01');
INSERT INTO D_Credit_Account (Student_ID, Current_Score, Updated_Time) VALUES ('IT_STU_006', 100, DATE '2026-02-01');
INSERT INTO D_Credit_Account (Student_ID, Current_Score, Updated_Time) VALUES ('IT_STU_007', 95,  DATE '2026-08-02');
INSERT INTO D_Credit_Account (Student_ID, Current_Score, Updated_Time) VALUES ('IT_STU_008', 100, DATE '2026-02-01');
INSERT INTO D_Credit_Account (Student_ID, Current_Score, Updated_Time) VALUES ('IT_STU_009', 95,  DATE '2026-07-28');
INSERT INTO D_Credit_Account (Student_ID, Current_Score, Updated_Time) VALUES ('IT_STU_010', 100, DATE '2026-02-01');
INSERT INTO D_Credit_Account (Student_ID, Current_Score, Updated_Time) VALUES ('IT_STU_011', 100, DATE '2026-02-01');
INSERT INTO D_Credit_Account (Student_ID, Current_Score, Updated_Time) VALUES ('IT_STU_012', 100, DATE '2026-02-01');
INSERT INTO D_Credit_Account (Student_ID, Current_Score, Updated_Time) VALUES ('IT_STU_013', 100, DATE '2026-02-01');
INSERT INTO D_Credit_Account (Student_ID, Current_Score, Updated_Time) VALUES ('IT_STU_014', 100, DATE '2026-02-01');
INSERT INTO D_Credit_Account (Student_ID, Current_Score, Updated_Time) VALUES ('IT_STU_015', 100, DATE '2026-02-01');
INSERT INTO D_Credit_Account (Student_ID, Current_Score, Updated_Time) VALUES ('IT_STU_016', 100, DATE '2026-02-01');
INSERT INTO D_Credit_Account (Student_ID, Current_Score, Updated_Time) VALUES ('IT_STU_017', 100, DATE '2026-02-01');
INSERT INTO D_Credit_Account (Student_ID, Current_Score, Updated_Time) VALUES ('IT_STU_018', 100, DATE '2026-02-01');
INSERT INTO D_Credit_Account (Student_ID, Current_Score, Updated_Time) VALUES ('IT_STU_019', 100, DATE '2026-02-01');
INSERT INTO D_Credit_Account (Student_ID, Current_Score, Updated_Time) VALUES ('IT_STU_020', 100, DATE '2026-02-01');
INSERT INTO D_Credit_Account (Student_ID, Current_Score, Updated_Time) VALUES ('IT_STU_021', 100, DATE '2026-02-01');
INSERT INTO D_Credit_Account (Student_ID, Current_Score, Updated_Time) VALUES ('IT_STU_022', 100, DATE '2026-02-01');
INSERT INTO D_Credit_Account (Student_ID, Current_Score, Updated_Time) VALUES ('IT_STU_023', 100, DATE '2026-02-01');
INSERT INTO D_Credit_Account (Student_ID, Current_Score, Updated_Time) VALUES ('IT_STU_024', 100, DATE '2026-02-01');
INSERT INTO D_Credit_Account (Student_ID, Current_Score, Updated_Time) VALUES ('IT_STU_025', 100, DATE '2026-02-01');
INSERT INTO D_Credit_Account (Student_ID, Current_Score, Updated_Time) VALUES ('IT_STU_026', 100, DATE '2026-02-01');
INSERT INTO D_Credit_Account (Student_ID, Current_Score, Updated_Time) VALUES ('IT_STU_027', 100, DATE '2026-02-01');
INSERT INTO D_Credit_Account (Student_ID, Current_Score, Updated_Time) VALUES ('IT_STU_028', 85,  DATE '2026-08-10');
INSERT INTO D_Credit_Account (Student_ID, Current_Score, Updated_Time) VALUES ('IT_STU_029', 100, DATE '2026-02-01');
INSERT INTO D_Credit_Account (Student_ID, Current_Score, Updated_Time) VALUES ('IT_STU_030', 100, DATE '2026-02-01');

-- ===== 4. 信用分明细 D_Credit_Log（Event_Key 唯一，与违规/借用关联） =====
INSERT INTO D_Credit_Log (Log_ID, Student_ID, Score_Change, Reason, Event_Key, Create_Time)
VALUES (9700001, 'IT_STU_003', -10, '使用违规电器（警告并没收）', 'VIOL-909601', DATE '2026-07-25');
INSERT INTO D_Credit_Log (Log_ID, Student_ID, Score_Change, Reason, Event_Key, Create_Time)
VALUES (9700002, 'IT_STU_003', -10, '走廊堆放杂物（整改并扣分）', 'VIOL-909604', DATE '2026-08-01');
INSERT INTO D_Credit_Log (Log_ID, Student_ID, Score_Change, Reason, Event_Key, Create_Time)
VALUES (9700003, 'IT_STU_007', -5,  '晚归未登记', 'VIOL-909602', DATE '2026-08-02');
INSERT INTO D_Credit_Log (Log_ID, Student_ID, Score_Change, Reason, Event_Key, Create_Time)
VALUES (9700004, 'IT_STU_009', -5,  '公共区域大声喧哗（批评教育）', 'VIOL-909603', DATE '2026-07-28');
INSERT INTO D_Credit_Log (Log_ID, Student_ID, Score_Change, Reason, Event_Key, Create_Time)
VALUES (9700005, 'IT_STU_028', -15, '共享物品超时未归还', 'OVERDUE-901103', DATE '2026-08-10');

-- ===== 5. 通知 D_Notification（6 类中文值全覆盖，部分已读） =====
INSERT INTO D_Notification (Notification_ID, Recipient_Account_ID, Title, Content, Notification_Type, Read_Time, Create_Time)
VALUES (907001, 908001, '报修派单通知',   '您的报修工单（空调漏水）已派单，维修员即将上门。',   '报修', DATE '2026-08-15', DATE '2026-08-15');
INSERT INTO D_Notification (Notification_ID, Recipient_Account_ID, Title, Content, Notification_Type, Read_Time, Create_Time)
VALUES (907002, 908001, '账单提醒',       '您的 7 月水电账单已发布，请及时缴费。',             '账单', NULL, DATE '2026-08-01');
INSERT INTO D_Notification (Notification_ID, Recipient_Account_ID, Title, Content, Notification_Type, Read_Time, Create_Time)
VALUES (907003, 908001, '预约成功',       '您预约的自习室 8/18 上午时段已确认。',               '预约', NULL, DATE '2026-08-16');
INSERT INTO D_Notification (Notification_ID, Recipient_Account_ID, Title, Content, Notification_Type, Read_Time, Create_Time)
VALUES (907004, 908001, '访客授权成功',   '访客「李明」已获得入楼授权，有效期至 8/20。',         '访客', NULL, DATE '2026-08-14');
INSERT INTO D_Notification (Notification_ID, Recipient_Account_ID, Title, Content, Notification_Type, Read_Time, Create_Time)
VALUES (907005, 908003, '账单欠费提醒',   '您的 7 月账单仍未缴纳，房间将断电，请尽快处理。',     '账单', NULL, DATE '2026-08-05');
INSERT INTO D_Notification (Notification_ID, Recipient_Account_ID, Title, Content, Notification_Type, Read_Time, Create_Time)
VALUES (907006, 908004, '快递到站提醒',   '您的包裹已到达快递驿站，请及时领取。',               '系统', NULL, DATE '2026-08-15');
INSERT INTO D_Notification (Notification_ID, Recipient_Account_ID, Title, Content, Notification_Type, Read_Time, Create_Time)
VALUES (907007, 908006, '报修完成通知',   '您的报修工单（衣柜门脱落）已处理完成。',             '报修', DATE '2026-08-03', DATE '2026-08-03');
INSERT INTO D_Notification (Notification_ID, Recipient_Account_ID, Title, Content, Notification_Type, Read_Time, Create_Time)
VALUES (907008, 908028, '借用逾期提醒',   '您借用的共享自行车已超时，请尽快归还，否则影响信用分。', '信用', NULL, DATE '2026-08-09');
INSERT INTO D_Notification (Notification_ID, Recipient_Account_ID, Title, Content, Notification_Type, Read_Time, Create_Time)
VALUES (907009, 908031, '新工单待派单',   '有新的报修工单等待派单处理。',                       '系统', NULL, DATE '2026-08-14');

-- ===== 5b. 公告 D_Notice + D_Notice_Display（S2-1 公告列表/置顶、C10 双路径） =====
INSERT INTO D_Notice (Notice_ID, Admin_ID, Title, Content, Publish_Time)
VALUES (906001, 'IT_ADMIN_001', '关于8月宿舍安全检查的通知', '8月18日将开展宿舍用电与消防安全检查，请同学们配合。', DATE '2026-08-10');
INSERT INTO D_Notice (Notice_ID, Admin_ID, Title, Content, Publish_Time)
VALUES (906002, 'IT_ADMIN_001', '暑期宿舍用电安全提醒', '严禁使用违规电器，离开房间请断电。', DATE '2026-08-05');
INSERT INTO D_Notice (Notice_ID, Admin_ID, Title, Content, Publish_Time)
VALUES (906003, 'IT_ADMIN_001', '公共设施暂停使用公告', '因维修施工，1号楼洗衣房暂停使用，恢复时间另行通知。', DATE '2026-08-14');
INSERT INTO D_Notice (Notice_ID, Admin_ID, Title, Content, Publish_Time)
VALUES (906004, 'IT_ADMIN_001', '毕业生退宿办理流程说明', '退宿需先完成水电、快递、借用物品三项核查。', DATE '2026-07-30');
INSERT INTO D_Notice (Notice_ID, Admin_ID, Title, Content, Publish_Time)
VALUES (906005, 'IT_SUPER_001', '学院-专业信息核对通知', '请各学院核对本学院专业目录，如有变更请及时反馈。', DATE '2026-08-12');
INSERT INTO D_Notice (Notice_ID, Admin_ID, Title, Content, Publish_Time)
VALUES (906006, 'IT_ADMIN_001', '共享物品借用规则更新', '共享物品借用期限默认7天，逾期将影响信用分。', DATE '2026-08-08');

INSERT INTO D_Notice_Display (Notice_ID, Is_Pinned, Pin_Time) VALUES (906001, '是', DATE '2026-08-10');
INSERT INTO D_Notice_Display (Notice_ID, Is_Pinned, Pin_Time) VALUES (906002, '否', NULL);
INSERT INTO D_Notice_Display (Notice_ID, Is_Pinned, Pin_Time) VALUES (906003, '是', DATE '2026-08-14');
INSERT INTO D_Notice_Display (Notice_ID, Is_Pinned, Pin_Time) VALUES (906004, '否', NULL);
INSERT INTO D_Notice_Display (Notice_ID, Is_Pinned, Pin_Time) VALUES (906005, '否', NULL);
INSERT INTO D_Notice_Display (Notice_ID, Is_Pinned, Pin_Time) VALUES (906006, '否', NULL);

-- ===== 6. 审计 D_Audit_Event（Event_Type 遵循 '{方法} {path}'） =====
INSERT INTO D_Audit_Event (Audit_ID, Actor_Account_ID, Event_Type, Target_Type, Target_ID, Event_Time, DETAILS)
VALUES (9081001, 908001, 'POST /api/auth/login', 'Api', 'IT_STU_001', DATE '2026-08-17', '学生登录成功');
INSERT INTO D_Audit_Event (Audit_ID, Actor_Account_ID, Event_Type, Target_Type, Target_ID, Event_Time, DETAILS)
VALUES (9081002, 908035, 'POST /api/auth/accounts/students', 'UserAccount', 'IT_STU_005', DATE '2026-08-16', '超管开通学生账号');
INSERT INTO D_Audit_Event (Audit_ID, Actor_Account_ID, Event_Type, Target_Type, Target_ID, Event_Time, DETAILS)
VALUES (9081003, 908031, 'POST /api/utility-fees', 'Api', '2026-07', DATE '2026-08-01', '发布 7 月水电账单');
INSERT INTO D_Audit_Event (Audit_ID, Actor_Account_ID, Event_Type, Target_Type, Target_ID, Event_Time, DETAILS)
VALUES (9081004, 908034, 'PUT /api/leave-applications/909802', 'LeaveApplication', 'IT_STU_009', DATE '2026-08-01', '辅导员审批通过离校报备');
INSERT INTO D_Audit_Event (Audit_ID, Actor_Account_ID, Event_Type, Target_Type, Target_ID, Event_Time, DETAILS)
VALUES (9081005, 908001, 'POST /api/repair-tickets', 'Api', '903001', DATE '2026-08-15', '学生提交报修工单');

-- ===== 7. 退宿清算 D_Checkout_Log（待清算 = 003 欠费阻断；已通过 = 031；已取消 = 013） =====
INSERT INTO D_Checkout_Log (Log_ID, Allocation_ID, Request_Time, Result_Time, Fee_Check, Item_Check, Status, Reject_Reason)
VALUES (909901, 9500002, DATE '2026-08-16', NULL, NULL, NULL, '待清算', NULL);
INSERT INTO D_Checkout_Log (Log_ID, Allocation_ID, Request_Time, Result_Time, Fee_Check, Item_Check, Status, Reject_Reason)
VALUES (909902, 9500030, DATE '2026-07-25', DATE '2026-07-31', '通过', '通过', '已通过', NULL);
INSERT INTO D_Checkout_Log (Log_ID, Allocation_ID, Request_Time, Result_Time, Fee_Check, Item_Check, Status, Reject_Reason)
VALUES (909903, 9500012, DATE '2026-08-08', DATE '2026-08-09', NULL, NULL, '已取消', '学生撤销退宿申请');

-- ===== 8. 离校报备 D_Leave_Application（待批/已通过/已驳回 各样本） =====
INSERT INTO D_Leave_Application (Apply_ID, Student_ID, Leave_Date, Return_Date, Destination, Status, Reason)
VALUES (909801, 'IT_STU_006', DATE '2026-08-19', DATE '2026-08-21', '武汉', '待批', NULL);
INSERT INTO D_Leave_Application (Apply_ID, Student_ID, Leave_Date, Return_Date, Destination, Status, Reason)
VALUES (909802, 'IT_STU_009', DATE '2026-08-01', DATE '2026-08-03', '北京', '已通过', NULL);
INSERT INTO D_Leave_Application (Apply_ID, Student_ID, Leave_Date, Return_Date, Destination, Status, Reason)
VALUES (909803, 'IT_STU_014', DATE '2026-08-05', DATE '2026-08-06', '上海', '已驳回', '请假理由不充分，请补充材料');
INSERT INTO D_Leave_Application (Apply_ID, Student_ID, Leave_Date, Return_Date, Destination, Status, Reason)
VALUES (909804, 'IT_STU_016', DATE '2026-08-22', DATE '2026-08-23', '长沙', '待批', NULL);

-- 完成确认
SELECT 'DAILY OPS DONE' AS MESSAGE FROM DUAL;
