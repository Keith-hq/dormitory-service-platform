SET DEFINE OFF;
SET AUTOCOMMIT ON;
-- 03 设施与报修：设施/预约/共享物品/借用/报修工单/维修日志/耗材/耗材出库/附件/资产-工单关联
-- 依赖：01_master_data.sql 已执行。
-- P1 链路 C4（报修 SLA 派单）与 C5（设施预约/共享物品）。

-- ===== 1. 设施 D_Facility（8 个，Facility_Code 唯一） =====
INSERT INTO D_Facility (Facility_ID, Building_ID, Facility_Code, Facility_Type, Status) VALUES (901201, 9001, 'FAC-9001-01', '自习室', '正常');
INSERT INTO D_Facility (Facility_ID, Building_ID, Facility_Code, Facility_Type, Status) VALUES (901202, 9001, 'FAC-9001-02', '健身房', '正常');
INSERT INTO D_Facility (Facility_ID, Building_ID, Facility_Code, Facility_Type, Status) VALUES (901203, 9001, 'FAC-9001-03', '洗衣房', '维修');
INSERT INTO D_Facility (Facility_ID, Building_ID, Facility_Code, Facility_Type, Status) VALUES (901204, 9001, 'FAC-9001-04', '开水间', '正常');
INSERT INTO D_Facility (Facility_ID, Building_ID, Facility_Code, Facility_Type, Status) VALUES (901205, 9002, 'FAC-9002-01', '自习室', '正常');
INSERT INTO D_Facility (Facility_ID, Building_ID, Facility_Code, Facility_Type, Status) VALUES (901206, 9002, 'FAC-9002-02', '健身房', '正常');
INSERT INTO D_Facility (Facility_ID, Building_ID, Facility_Code, Facility_Type, Status) VALUES (901207, 9002, 'FAC-9002-03', '洗衣房', '正常');
INSERT INTO D_Facility (Facility_ID, Building_ID, Facility_Code, Facility_Type, Status) VALUES (901208, 9002, 'FAC-9002-04', '开水间', '停用');

-- ===== 2. 设施预约 D_Facility_Booking（每设施最多 1 条活动预约） =====
-- 已预约：901301 / 901302；使用中：901303；已完成 / 已失效：其余。
INSERT INTO D_Facility_Booking (Booking_ID, Facility_ID, Student_ID, Create_Time, Start_Time, End_Time, Status)
VALUES (901301, 901201, 'IT_STU_001', DATE '2026-08-16', TO_DATE('2026-08-18 09:00', 'YYYY-MM-DD HH24:MI'), TO_DATE('2026-08-18 11:00', 'YYYY-MM-DD HH24:MI'), '已预约');
INSERT INTO D_Facility_Booking (Booking_ID, Facility_ID, Student_ID, Create_Time, Start_Time, End_Time, Status)
VALUES (901302, 901205, 'IT_STU_016', DATE '2026-08-16', TO_DATE('2026-08-17 19:00', 'YYYY-MM-DD HH24:MI'), TO_DATE('2026-08-17 21:00', 'YYYY-MM-DD HH24:MI'), '已预约');
INSERT INTO D_Facility_Booking (Booking_ID, Facility_ID, Student_ID, Create_Time, Start_Time, End_Time, Status)
VALUES (901303, 901202, 'IT_STU_001', DATE '2026-08-16', TO_DATE('2026-08-17 09:00', 'YYYY-MM-DD HH24:MI'), TO_DATE('2026-08-17 11:00', 'YYYY-MM-DD HH24:MI'), '使用中');
INSERT INTO D_Facility_Booking (Booking_ID, Facility_ID, Student_ID, Create_Time, Start_Time, End_Time, Status)
VALUES (901304, 901206, 'IT_STU_019', DATE '2026-08-14', TO_DATE('2026-08-15 18:00', 'YYYY-MM-DD HH24:MI'), TO_DATE('2026-08-15 19:00', 'YYYY-MM-DD HH24:MI'), '已完成');
INSERT INTO D_Facility_Booking (Booking_ID, Facility_ID, Student_ID, Create_Time, Start_Time, End_Time, Status)
VALUES (901305, 901202, 'IT_STU_006', DATE '2026-08-10', TO_DATE('2026-08-12 18:00', 'YYYY-MM-DD HH24:MI'), TO_DATE('2026-08-12 19:00', 'YYYY-MM-DD HH24:MI'), '已完成');
INSERT INTO D_Facility_Booking (Booking_ID, Facility_ID, Student_ID, Create_Time, Start_Time, End_Time, Status)
VALUES (901306, 901201, 'IT_STU_009', DATE '2026-08-13', TO_DATE('2026-08-13 09:00', 'YYYY-MM-DD HH24:MI'), TO_DATE('2026-08-13 11:00', 'YYYY-MM-DD HH24:MI'), '已完成');
INSERT INTO D_Facility_Booking (Booking_ID, Facility_ID, Student_ID, Create_Time, Start_Time, End_Time, Status)
VALUES (901307, 901201, 'IT_STU_003', DATE '2026-08-15', TO_DATE('2026-08-16 10:00', 'YYYY-MM-DD HH24:MI'), TO_DATE('2026-08-16 12:00', 'YYYY-MM-DD HH24:MI'), '已失效');
INSERT INTO D_Facility_Booking (Booking_ID, Facility_ID, Student_ID, Create_Time, Start_Time, End_Time, Status)
VALUES (901308, 901205, 'IT_STU_017', DATE '2026-08-12', TO_DATE('2026-08-14 15:00', 'YYYY-MM-DD HH24:MI'), TO_DATE('2026-08-14 17:00', 'YYYY-MM-DD HH24:MI'), '已完成');

-- ===== 3. 共享物品 D_Shared_Item（Available_Qty 与借用对齐） =====
INSERT INTO D_Shared_Item (Item_ID, Item_Name, Building_ID, Total_Qty, Available_Qty, Status, DESCRIPTION) VALUES (901001, '爱心雨伞',   9001, 10, 10, '正常', '宿舍楼公共爱心雨伞');
INSERT INTO D_Shared_Item (Item_ID, Item_Name, Building_ID, Total_Qty, Available_Qty, Status, DESCRIPTION) VALUES (901002, '工具箱',     9001, 5,  5,  '正常', NULL);
INSERT INTO D_Shared_Item (Item_ID, Item_Name, Building_ID, Total_Qty, Available_Qty, Status, DESCRIPTION) VALUES (901003, '羽毛球拍',   9001, 2,  1,  '正常', '1 副已借出');
INSERT INTO D_Shared_Item (Item_ID, Item_Name, Building_ID, Total_Qty, Available_Qty, Status, DESCRIPTION) VALUES (901004, '吹风机',     9002, 3,  3,  '正常', NULL);
INSERT INTO D_Shared_Item (Item_ID, Item_Name, Building_ID, Total_Qty, Available_Qty, Status, DESCRIPTION) VALUES (901005, '充电宝',     9002, 4,  4,  '正常', NULL);
INSERT INTO D_Shared_Item (Item_ID, Item_Name, Building_ID, Total_Qty, Available_Qty, Status, DESCRIPTION) VALUES (901006, '共享自行车', 9002, 1,  0,  '正常', '超时未还（扣分样本）');

-- ===== 4. 借用记录 D_Item_Loan（Idempotency_Key 唯一） =====
-- 901101 已归还；901102 借出未还（库存-1）；901103 超时未还（OVERDUE-901103 扣分）。
INSERT INTO D_Item_Loan (Loan_ID, Item_ID, Student_ID, Borrow_Time, Due_Time, Return_Time, Idempotency_Key)
VALUES (901101, 901003, 'IT_STU_001', DATE '2026-08-10', DATE '2026-08-17', DATE '2026-08-15', 'IT-C5-001-1');
INSERT INTO D_Item_Loan (Loan_ID, Item_ID, Student_ID, Borrow_Time, Due_Time, Return_Time, Idempotency_Key)
VALUES (901102, 901003, 'IT_STU_006', DATE '2026-08-15', DATE '2026-08-22', NULL, 'IT-C5-002-1');
INSERT INTO D_Item_Loan (Loan_ID, Item_ID, Student_ID, Borrow_Time, Due_Time, Return_Time, Idempotency_Key)
VALUES (901103, 901006, 'IT_STU_028', DATE '2026-08-01', DATE '2026-08-08', NULL, 'IT-C5-003-1');

-- ===== 5. 报修工单 D_Repair_Ticket（20 张，状态 4 值全覆盖） =====
-- 903003 为紧急超时未派单（Escalation_Time 非空，SLA 升级样本）。
INSERT INTO D_Repair_Ticket (Ticket_ID, Student_ID, Room_ID, Issue_Desc, Submit_Time, Status, SLA_Level, Deadline, Assigned_To, Escalation_Time)
VALUES (903001, 'IT_STU_001', 900101, '宿舍门锁松动，无法正常锁门', TO_DATE('2026-08-15 10:00', 'YYYY-MM-DD HH24:MI'), '待处理', '普通', NULL, NULL, NULL);
INSERT INTO D_Repair_Ticket (Ticket_ID, Student_ID, Room_ID, Issue_Desc, Submit_Time, Status, SLA_Level, Deadline, Assigned_To, Escalation_Time)
VALUES (903002, 'IT_STU_004', 900102, '洗手间水龙头滴水', TO_DATE('2026-08-16 09:00', 'YYYY-MM-DD HH24:MI'), '待处理', '普通', NULL, NULL, NULL);
INSERT INTO D_Repair_Ticket (Ticket_ID, Student_ID, Room_ID, Issue_Desc, Submit_Time, Status, SLA_Level, Deadline, Assigned_To, Escalation_Time)
VALUES (903003, 'IT_STU_006', 900103, '房间灯管闪烁，影响照明', TO_DATE('2026-08-14 00:00', 'YYYY-MM-DD HH24:MI'), '待处理', '紧急', TO_DATE('2026-08-14 04:00', 'YYYY-MM-DD HH24:MI'), NULL, TO_DATE('2026-08-14 02:00', 'YYYY-MM-DD HH24:MI'));
INSERT INTO D_Repair_Ticket (Ticket_ID, Student_ID, Room_ID, Issue_Desc, Submit_Time, Status, SLA_Level, Deadline, Assigned_To, Escalation_Time)
VALUES (903004, 'IT_STU_001', 900101, '空调漏水', TO_DATE('2026-08-15 14:00', 'YYYY-MM-DD HH24:MI'), '已派单', '普通', DATE '2026-08-19', 'IT_REPAIR_001', NULL);
INSERT INTO D_Repair_Ticket (Ticket_ID, Student_ID, Room_ID, Issue_Desc, Submit_Time, Status, SLA_Level, Deadline, Assigned_To, Escalation_Time)
VALUES (903005, 'IT_STU_007', 900103, '窗户关不上', TO_DATE('2026-08-14 11:00', 'YYYY-MM-DD HH24:MI'), '已派单', '普通', DATE '2026-08-18', 'IT_REPAIR_002', NULL);
INSERT INTO D_Repair_Ticket (Ticket_ID, Student_ID, Room_ID, Issue_Desc, Submit_Time, Status, SLA_Level, Deadline, Assigned_To, Escalation_Time)
VALUES (903006, 'IT_STU_009', 900105, '灯泡损坏', TO_DATE('2026-08-10 08:00', 'YYYY-MM-DD HH24:MI'), '已完成', '普通', NULL, 'IT_REPAIR_001', NULL);
INSERT INTO D_Repair_Ticket (Ticket_ID, Student_ID, Room_ID, Issue_Desc, Submit_Time, Status, SLA_Level, Deadline, Assigned_To, Escalation_Time)
VALUES (903007, 'IT_STU_010', 900105, '下水道堵塞', TO_DATE('2026-08-09 15:00', 'YYYY-MM-DD HH24:MI'), '已完成', '普通', NULL, 'IT_REPAIR_001', NULL);
INSERT INTO D_Repair_Ticket (Ticket_ID, Student_ID, Room_ID, Issue_Desc, Submit_Time, Status, SLA_Level, Deadline, Assigned_To, Escalation_Time)
VALUES (903008, 'IT_STU_013', 900106, '书桌抽屉损坏', TO_DATE('2026-08-05 10:00', 'YYYY-MM-DD HH24:MI'), '已完成', '普通', NULL, 'IT_REPAIR_001', NULL);
INSERT INTO D_Repair_Ticket (Ticket_ID, Student_ID, Room_ID, Issue_Desc, Submit_Time, Status, SLA_Level, Deadline, Assigned_To, Escalation_Time)
VALUES (903009, 'IT_STU_016', 900201, '门锁故障', TO_DATE('2026-08-06 09:00', 'YYYY-MM-DD HH24:MI'), '已完成', '普通', NULL, 'IT_REPAIR_002', NULL);
INSERT INTO D_Repair_Ticket (Ticket_ID, Student_ID, Room_ID, Issue_Desc, Submit_Time, Status, SLA_Level, Deadline, Assigned_To, Escalation_Time)
VALUES (903010, 'IT_STU_019', 900202, '水龙头老化需更换', TO_DATE('2026-08-03 14:00', 'YYYY-MM-DD HH24:MI'), '已完成', '普通', NULL, 'IT_REPAIR_002', NULL);
INSERT INTO D_Repair_Ticket (Ticket_ID, Student_ID, Room_ID, Issue_Desc, Submit_Time, Status, SLA_Level, Deadline, Assigned_To, Escalation_Time)
VALUES (903011, 'IT_STU_021', 900203, '窗帘脱落', TO_DATE('2026-08-08 16:00', 'YYYY-MM-DD HH24:MI'), '已完成', '普通', NULL, 'IT_REPAIR_002', NULL);
INSERT INTO D_Repair_Ticket (Ticket_ID, Student_ID, Room_ID, Issue_Desc, Submit_Time, Status, SLA_Level, Deadline, Assigned_To, Escalation_Time)
VALUES (903012, 'IT_STU_025', 900204, '台灯不亮', TO_DATE('2026-08-07 10:00', 'YYYY-MM-DD HH24:MI'), '已完成', '普通', NULL, 'IT_REPAIR_002', NULL);
INSERT INTO D_Repair_Ticket (Ticket_ID, Student_ID, Room_ID, Issue_Desc, Submit_Time, Status, SLA_Level, Deadline, Assigned_To, Escalation_Time)
VALUES (903013, 'IT_STU_026', 900205, '插座冒火花', TO_DATE('2026-08-02 08:30', 'YYYY-MM-DD HH24:MI'), '已完成', '紧急', TO_DATE('2026-08-02 12:30', 'YYYY-MM-DD HH24:MI'), 'IT_REPAIR_002', NULL);
INSERT INTO D_Repair_Ticket (Ticket_ID, Student_ID, Room_ID, Issue_Desc, Submit_Time, Status, SLA_Level, Deadline, Assigned_To, Escalation_Time)
VALUES (903014, 'IT_STU_028', 900206, '床板松动', TO_DATE('2026-08-04 11:00', 'YYYY-MM-DD HH24:MI'), '已完成', '普通', NULL, 'IT_REPAIR_002', NULL);
INSERT INTO D_Repair_Ticket (Ticket_ID, Student_ID, Room_ID, Issue_Desc, Submit_Time, Status, SLA_Level, Deadline, Assigned_To, Escalation_Time)
VALUES (903015, 'IT_STU_003', 900102, '空调遥控器失灵', TO_DATE('2026-08-01 09:00', 'YYYY-MM-DD HH24:MI'), '已完成', '普通', NULL, 'IT_REPAIR_001', NULL);
INSERT INTO D_Repair_Ticket (Ticket_ID, Student_ID, Room_ID, Issue_Desc, Submit_Time, Status, SLA_Level, Deadline, Assigned_To, Escalation_Time)
VALUES (903016, 'IT_STU_008', 900104, '椅子损坏', TO_DATE('2026-08-11 13:00', 'YYYY-MM-DD HH24:MI'), '已完成', '普通', NULL, 'IT_REPAIR_001', NULL);
INSERT INTO D_Repair_Ticket (Ticket_ID, Student_ID, Room_ID, Issue_Desc, Submit_Time, Status, SLA_Level, Deadline, Assigned_To, Escalation_Time)
VALUES (903017, 'IT_STU_001', 900101, '墙体疑似裂缝', TO_DATE('2026-08-12 10:00', 'YYYY-MM-DD HH24:MI'), '已撤销', '普通', NULL, NULL, NULL);
INSERT INTO D_Repair_Ticket (Ticket_ID, Student_ID, Room_ID, Issue_Desc, Submit_Time, Status, SLA_Level, Deadline, Assigned_To, Escalation_Time)
VALUES (903018, 'IT_STU_012', 900105, '水龙头漏水', TO_DATE('2026-08-06 09:30', 'YYYY-MM-DD HH24:MI'), '已完成', '普通', NULL, 'IT_REPAIR_001', NULL);
INSERT INTO D_Repair_Ticket (Ticket_ID, Student_ID, Room_ID, Issue_Desc, Submit_Time, Status, SLA_Level, Deadline, Assigned_To, Escalation_Time)
VALUES (903019, 'IT_STU_006', 900103, '衣柜门脱落', TO_DATE('2026-08-03 10:00', 'YYYY-MM-DD HH24:MI'), '已完成', '普通', NULL, 'IT_REPAIR_001', NULL);
INSERT INTO D_Repair_Ticket (Ticket_ID, Student_ID, Room_ID, Issue_Desc, Submit_Time, Status, SLA_Level, Deadline, Assigned_To, Escalation_Time)
VALUES (903020, 'IT_STU_014', 900106, '洗手池堵塞', TO_DATE('2026-08-17 09:00', 'YYYY-MM-DD HH24:MI'), '待处理', '普通', NULL, NULL, NULL);

-- ===== 6. 维修日志 D_Repair_Log（一票一结，Repair_Result 契约枚举） =====
INSERT INTO D_Repair_Log (Log_ID, Ticket_ID, Admin_ID, Process_Desc, Resolve_Time, Repair_Result)
VALUES (904001, 903006, 'IT_REPAIR_001', '更换损坏灯泡', TO_DATE('2026-08-11 10:00', 'YYYY-MM-DD HH24:MI'), '已修复');
INSERT INTO D_Repair_Log (Log_ID, Ticket_ID, Admin_ID, Process_Desc, Resolve_Time, Repair_Result)
VALUES (904002, 903007, 'IT_REPAIR_001', '疏通下水道', TO_DATE('2026-08-09 16:00', 'YYYY-MM-DD HH24:MI'), '已修复');
INSERT INTO D_Repair_Log (Log_ID, Ticket_ID, Admin_ID, Process_Desc, Resolve_Time, Repair_Result)
VALUES (904003, 903008, 'IT_REPAIR_001', '维修书桌抽屉滑轨', TO_DATE('2026-08-06 15:00', 'YYYY-MM-DD HH24:MI'), '已修复');
INSERT INTO D_Repair_Log (Log_ID, Ticket_ID, Admin_ID, Process_Desc, Resolve_Time, Repair_Result)
VALUES (904004, 903009, 'IT_REPAIR_002', '门锁损坏需整体更换', TO_DATE('2026-08-07 11:00', 'YYYY-MM-DD HH24:MI'), '需更换配件');
INSERT INTO D_Repair_Log (Log_ID, Ticket_ID, Admin_ID, Process_Desc, Resolve_Time, Repair_Result)
VALUES (904005, 903010, 'IT_REPAIR_002', '更换老化水龙头', TO_DATE('2026-08-04 15:00', 'YYYY-MM-DD HH24:MI'), '已修复');
INSERT INTO D_Repair_Log (Log_ID, Ticket_ID, Admin_ID, Process_Desc, Resolve_Time, Repair_Result)
VALUES (904006, 903011, 'IT_REPAIR_002', '重装窗帘轨道', TO_DATE('2026-08-08 17:00', 'YYYY-MM-DD HH24:MI'), '已修复');
INSERT INTO D_Repair_Log (Log_ID, Ticket_ID, Admin_ID, Process_Desc, Resolve_Time, Repair_Result)
VALUES (904007, 903012, 'IT_REPAIR_002', '更换灯泡', TO_DATE('2026-08-07 10:30', 'YYYY-MM-DD HH24:MI'), '已修复');
INSERT INTO D_Repair_Log (Log_ID, Ticket_ID, Admin_ID, Process_Desc, Resolve_Time, Repair_Result)
VALUES (904008, 903013, 'IT_REPAIR_002', '更换插座面板并检查线路', TO_DATE('2026-08-02 12:00', 'YYYY-MM-DD HH24:MI'), '需更换配件');
INSERT INTO D_Repair_Log (Log_ID, Ticket_ID, Admin_ID, Process_Desc, Resolve_Time, Repair_Result)
VALUES (904009, 903014, 'IT_REPAIR_002', '加固床板连接件', TO_DATE('2026-08-05 14:00', 'YYYY-MM-DD HH24:MI'), '已修复');
INSERT INTO D_Repair_Log (Log_ID, Ticket_ID, Admin_ID, Process_Desc, Resolve_Time, Repair_Result)
VALUES (904010, 903015, 'IT_REPAIR_001', '更换遥控器电池', TO_DATE('2026-08-02 10:00', 'YYYY-MM-DD HH24:MI'), '已修复');
INSERT INTO D_Repair_Log (Log_ID, Ticket_ID, Admin_ID, Process_Desc, Resolve_Time, Repair_Result)
VALUES (904011, 903016, 'IT_REPAIR_001', '检查确认椅子结构性损坏', TO_DATE('2026-08-12 14:00', 'YYYY-MM-DD HH24:MI'), '无法修复');
INSERT INTO D_Repair_Log (Log_ID, Ticket_ID, Admin_ID, Process_Desc, Resolve_Time, Repair_Result)
VALUES (904012, 903018, 'IT_REPAIR_001', '更换水龙头密封圈', TO_DATE('2026-08-06 10:30', 'YYYY-MM-DD HH24:MI'), '已修复');
INSERT INTO D_Repair_Log (Log_ID, Ticket_ID, Admin_ID, Process_Desc, Resolve_Time, Repair_Result)
VALUES (904013, 903019, 'IT_REPAIR_001', '修复衣柜门铰链', TO_DATE('2026-08-03 11:00', 'YYYY-MM-DD HH24:MI'), '已修复');

-- ===== 7. 耗材 D_Repair_Material =====
INSERT INTO D_Repair_Material (Material_ID, Material_Name, Unit, Stock_Qty) VALUES (902101, '灯泡',      '个', 100);
INSERT INTO D_Repair_Material (Material_ID, Material_Name, Unit, Stock_Qty) VALUES (902102, '水龙头',    '个', 20);
INSERT INTO D_Repair_Material (Material_ID, Material_Name, Unit, Stock_Qty) VALUES (902103, '门锁',      '把', 15);
INSERT INTO D_Repair_Material (Material_ID, Material_Name, Unit, Stock_Qty) VALUES (902104, '合页',      '个', 30);
INSERT INTO D_Repair_Material (Material_ID, Material_Name, Unit, Stock_Qty) VALUES (902105, '插座',      '个', 25);
INSERT INTO D_Repair_Material (Material_ID, Material_Name, Unit, Stock_Qty) VALUES (902106, '螺丝包',    '包', 50);

-- ===== 8. 耗材出库 D_Repair_Material_Usage（Idempotency_Key 唯一） =====
INSERT INTO D_Repair_Material_Usage (Usage_ID, Ticket_ID, Material_ID, Quantity, Use_Time, Idempotency_Key)
VALUES (902201, 903006, 902101, 2, TO_DATE('2026-08-11 10:00', 'YYYY-MM-DD HH24:MI'), 'IT-C4-006-1');
INSERT INTO D_Repair_Material_Usage (Usage_ID, Ticket_ID, Material_ID, Quantity, Use_Time, Idempotency_Key)
VALUES (902202, 903007, 902102, 1, TO_DATE('2026-08-09 16:00', 'YYYY-MM-DD HH24:MI'), 'IT-C4-007-1');
INSERT INTO D_Repair_Material_Usage (Usage_ID, Ticket_ID, Material_ID, Quantity, Use_Time, Idempotency_Key)
VALUES (902203, 903009, 902103, 1, TO_DATE('2026-08-07 11:00', 'YYYY-MM-DD HH24:MI'), 'IT-C4-009-1');
INSERT INTO D_Repair_Material_Usage (Usage_ID, Ticket_ID, Material_ID, Quantity, Use_Time, Idempotency_Key)
VALUES (902204, 903013, 902105, 1, TO_DATE('2026-08-02 12:00', 'YYYY-MM-DD HH24:MI'), 'IT-C4-013-1');
INSERT INTO D_Repair_Material_Usage (Usage_ID, Ticket_ID, Material_ID, Quantity, Use_Time, Idempotency_Key)
VALUES (902205, 903014, 902106, 1, TO_DATE('2026-08-05 14:00', 'YYYY-MM-DD HH24:MI'), 'IT-C4-014-1');
INSERT INTO D_Repair_Material_Usage (Usage_ID, Ticket_ID, Material_ID, Quantity, Use_Time, Idempotency_Key)
VALUES (902206, 903010, 902102, 1, TO_DATE('2026-08-04 15:00', 'YYYY-MM-DD HH24:MI'), 'IT-C4-010-1');

-- ===== 9. 报修附件 D_Repair_Attachment（Storage_Ref 唯一，虚拟路径） =====
INSERT INTO D_Repair_Attachment (Attachment_ID, Ticket_ID, Storage_Ref, Original_Name, Content_Type, File_Size, Create_Time)
VALUES (905001, 903003, 'uploads/2026/08/repair-903003-1.jpg', '灯管闪烁照片.jpg', 'image/jpeg', 204800, DATE '2026-08-14');
INSERT INTO D_Repair_Attachment (Attachment_ID, Ticket_ID, Storage_Ref, Original_Name, Content_Type, File_Size, Create_Time)
VALUES (905002, 903006, 'uploads/2026/08/repair-903006-1.jpg', '坏灯泡照片.jpg', 'image/jpeg', 102400, DATE '2026-08-10');

-- ===== 10. 资产-工单关联 D_Asset_Repair（900101 空调损坏 → 903004 空调漏水） =====
INSERT INTO D_Asset_Repair (Link_ID, Asset_ID, Ticket_ID, Create_Time)
VALUES (930001, 9300001, 903004, DATE '2026-08-15');

-- 完成确认
SELECT 'REPAIR & MAINTENANCE DONE' AS MESSAGE FROM DUAL;
