SET DEFINE OFF;
SET AUTOCOMMIT ON;
-- 99 伪数据集校验：断言 + 汇总。
-- 用法：执行完 00~05 后运行本脚本。
-- 第一部分「断言」每个查询应返回 0 行（有行 = 数据问题）；第二部分「汇总」为计数核对。

-- =====================================================================
-- 一、断言（应无返回行）
-- =====================================================================

-- 1. 孤儿检查：分摊明细引用失效
--（041 起明细不再携带 Room_ID，房间归属完整性由第 2 节账单头检查覆盖）
SELECT '孤儿-Fee_Detail' AS ITEM, fd.Detail_ID, fd.Fee_ID, fd.Student_ID
FROM D_Fee_Detail fd
LEFT JOIN D_Utility_Fee uf ON uf.Fee_ID = fd.Fee_ID
LEFT JOIN D_Student s ON s.Student_ID = fd.Student_ID
WHERE uf.Fee_ID IS NULL OR s.Student_ID IS NULL;

-- 2. 孤儿检查：账单引用房间
SELECT '孤儿-Utility_Fee' AS ITEM, uf.Fee_ID, uf.Room_ID
FROM D_Utility_Fee uf
LEFT JOIN D_Room r ON r.Room_ID = uf.Room_ID
WHERE r.Room_ID IS NULL;

-- 3. 孤儿检查：扣款尝试 / 钱包日志
SELECT '孤儿-Fee_Deduction_Attempt' AS ITEM, fa.Attempt_ID
FROM D_Fee_Deduction_Attempt fa
LEFT JOIN D_Fee_Detail fd ON fd.Detail_ID = fa.Detail_ID
WHERE fd.Detail_ID IS NULL;
SELECT '孤儿-Wallet_Log' AS ITEM, wl.Log_ID
FROM D_Wallet_Log wl
LEFT JOIN D_Wallet_Account wa ON wa.Student_ID = wl.Student_ID
WHERE wa.Student_ID IS NULL;

-- 4. 孤儿检查：住宿分配
SELECT '孤儿-Bed_Allocation' AS ITEM, ba.Allocation_ID
FROM D_Bed_Allocation ba
LEFT JOIN D_Student s ON s.Student_ID = ba.Student_ID
LEFT JOIN D_Room r ON r.Room_ID = ba.Room_ID
WHERE s.Student_ID IS NULL OR r.Room_ID IS NULL;

-- 5. 孤儿检查：报修工单 / 维修日志 / 耗材出库 / 附件
SELECT '孤儿-Repair_Ticket' AS ITEM, t.Ticket_ID
FROM D_Repair_Ticket t
LEFT JOIN D_Student s ON s.Student_ID = t.Student_ID
LEFT JOIN D_Room r ON r.Room_ID = t.Room_ID
LEFT JOIN D_Admin a ON a.Admin_ID = t.Assigned_To
WHERE s.Student_ID IS NULL OR r.Room_ID IS NULL
   OR (t.Assigned_To IS NOT NULL AND a.Admin_ID IS NULL);
SELECT '孤儿-Repair_Log' AS ITEM, l.Log_ID
FROM D_Repair_Log l
LEFT JOIN D_Repair_Ticket t ON t.Ticket_ID = l.Ticket_ID
LEFT JOIN D_Admin a ON a.Admin_ID = l.Admin_ID
WHERE t.Ticket_ID IS NULL OR a.Admin_ID IS NULL;
SELECT '孤儿-Material_Usage' AS ITEM, u.Usage_ID
FROM D_Repair_Material_Usage u
LEFT JOIN D_Repair_Ticket t ON t.Ticket_ID = u.Ticket_ID
LEFT JOIN D_Repair_Material m ON m.Material_ID = u.Material_ID
WHERE t.Ticket_ID IS NULL OR m.Material_ID IS NULL;
SELECT '孤儿-Repair_Attachment' AS ITEM, att.Attachment_ID
FROM D_Repair_Attachment att
LEFT JOIN D_Repair_Ticket t ON t.Ticket_ID = att.Ticket_ID
WHERE t.Ticket_ID IS NULL;

-- 6. 孤儿检查：设施预约 / 保洁 / 共享物品 / 借用
SELECT '孤儿-Facility_Booking' AS ITEM, b.Booking_ID
FROM D_Facility_Booking b
LEFT JOIN D_Facility f ON f.Facility_ID = b.Facility_ID
LEFT JOIN D_Student s ON s.Student_ID = b.Student_ID
WHERE f.Facility_ID IS NULL OR s.Student_ID IS NULL;
SELECT '孤儿-Cleaning_Task' AS ITEM, ct.Task_ID
FROM D_Cleaning_Task ct
LEFT JOIN D_Facility f ON f.Facility_ID = ct.Facility_ID
WHERE f.Facility_ID IS NULL;
SELECT '孤儿-Item_Loan' AS ITEM, il.Loan_ID
FROM D_Item_Loan il
LEFT JOIN D_Shared_Item si ON si.Item_ID = il.Item_ID
LEFT JOIN D_Student s ON s.Student_ID = il.Student_ID
WHERE si.Item_ID IS NULL OR s.Student_ID IS NULL;

-- 7. 孤儿检查：账号绑定（学生/管理员必须其一存在）
SELECT '孤儿-User_Account' AS ITEM, ua.Account_ID, ua.Login_Name
FROM D_User_Account ua
LEFT JOIN D_Student s ON s.Student_ID = ua.Student_ID
LEFT JOIN D_Admin a ON a.Admin_ID = ua.Admin_ID
WHERE (ua.Student_ID IS NOT NULL AND s.Student_ID IS NULL)
   OR (ua.Admin_ID IS NOT NULL AND a.Admin_ID IS NULL);

-- 8. 孤儿检查：通知 / 审计 / 访客授权 / 门禁 / 晚归 / 违规 / 离校 / 退宿
--（041 起 D_Visitor_Log / D_Parcel_Record / D_Water_Order 已删除）
SELECT '孤儿-Notification' AS ITEM, n.Notification_ID
FROM D_Notification n
LEFT JOIN D_User_Account ua ON ua.Account_ID = n.Recipient_Account_ID
WHERE ua.Account_ID IS NULL;
SELECT '孤儿-Audit_Event' AS ITEM, ae.Audit_ID
FROM D_Audit_Event ae
LEFT JOIN D_User_Account ua ON ua.Account_ID = ae.Actor_Account_ID
WHERE ae.Actor_Account_ID IS NOT NULL AND ua.Account_ID IS NULL;
SELECT '孤儿-Visitor_Authorization' AS ITEM, va.Authorization_ID
FROM D_Visitor_Authorization va
LEFT JOIN D_Student s ON s.Student_ID = va.Student_ID
LEFT JOIN D_Room r ON r.Room_ID = va.Room_ID
WHERE s.Student_ID IS NULL OR r.Room_ID IS NULL;
SELECT '孤儿-Access_Log' AS ITEM, al.Log_ID
FROM D_Access_Log al
LEFT JOIN D_Student s ON s.Student_ID = al.Student_ID
LEFT JOIN D_Building b ON b.Building_ID = al.Building_ID
WHERE s.Student_ID IS NULL OR b.Building_ID IS NULL;
SELECT '孤儿-Late_Entry' AS ITEM, le.Record_ID
FROM D_Late_Entry le
LEFT JOIN D_Student s ON s.Student_ID = le.Student_ID
WHERE s.Student_ID IS NULL;
SELECT '孤儿-Violation_Record' AS ITEM, vr.Record_ID
FROM D_Violation_Record vr
LEFT JOIN D_Student s ON s.Student_ID = vr.Student_ID
LEFT JOIN D_Room r ON r.Room_ID = vr.Room_ID
WHERE s.Student_ID IS NULL OR r.Room_ID IS NULL;
SELECT '孤儿-Leave_Application' AS ITEM, la.Apply_ID
FROM D_Leave_Application la
LEFT JOIN D_Student s ON s.Student_ID = la.Student_ID
WHERE s.Student_ID IS NULL;
SELECT '孤儿-Checkout_Log' AS ITEM, cl.Log_ID
FROM D_Checkout_Log cl
LEFT JOIN D_Bed_Allocation ba ON ba.Allocation_ID = cl.Allocation_ID
WHERE ba.Allocation_ID IS NULL;
SELECT '孤儿-Room_Vote' AS ITEM, v.Vote_ID
FROM D_Room_Vote v
LEFT JOIN D_Room r ON r.Room_ID = v.Room_ID
LEFT JOIN D_Student s ON s.Student_ID = v.Initiator_Student_ID
WHERE r.Room_ID IS NULL OR s.Student_ID IS NULL;
SELECT '孤儿-Room_Vote_Response' AS ITEM, vr.Vote_ID, vr.Student_ID
FROM D_Room_Vote_Response vr
LEFT JOIN D_Room_Vote v ON v.Vote_ID = vr.Vote_ID
LEFT JOIN D_Student s ON s.Student_ID = vr.Student_ID
WHERE v.Vote_ID IS NULL OR s.Student_ID IS NULL;

-- 9. 孤儿检查：资产 / 预警 / 资产-工单
SELECT '孤儿-Asset' AS ITEM, a.Asset_ID
FROM D_Asset a
LEFT JOIN D_Room r ON r.Room_ID = a.Room_ID
WHERE r.Room_ID IS NULL;
SELECT '孤儿-Asset_Warning' AS ITEM, aw.Warning_ID
FROM D_Asset_Warning aw
LEFT JOIN D_Asset a ON a.Asset_ID = aw.Asset_ID
WHERE a.Asset_ID IS NULL;
SELECT '孤儿-Asset_Repair' AS ITEM, ar.Link_ID
FROM D_Asset_Repair ar
LEFT JOIN D_Asset a ON a.Asset_ID = ar.Asset_ID
LEFT JOIN D_Repair_Ticket t ON t.Ticket_ID = ar.Ticket_ID
WHERE a.Asset_ID IS NULL OR t.Ticket_ID IS NULL;

-- 10. 一致性：房间占用数 = 活动分配数
SELECT '不一致-房间占用数' AS ITEM, r.Room_ID, r.Occupancy,
       (SELECT COUNT(*) FROM D_Bed_Allocation ba
         WHERE ba.Room_ID = r.Room_ID AND ba.CheckOut_Date IS NULL) AS ACTUAL_ACTIVE
FROM D_Room r
WHERE r.Occupancy <> (SELECT COUNT(*) FROM D_Bed_Allocation ba
                       WHERE ba.Room_ID = r.Room_ID AND ba.CheckOut_Date IS NULL);

-- 11. 一致性：共享物品可用数 = 总数 - 未归还借用
SELECT '不一致-共享物品库存' AS ITEM, si.Item_ID, si.Total_Qty, si.Available_Qty
FROM D_Shared_Item si
WHERE si.Available_Qty <> si.Total_Qty
      - (SELECT COUNT(*) FROM D_Item_Loan il
          WHERE il.Item_ID = si.Item_ID AND il.Return_Time IS NULL);

-- =====================================================================
-- 二、汇总计数（核对用）
-- =====================================================================
SELECT '账号数(学生30+管理员5)' AS ITEM, COUNT(*) AS CNT FROM D_User_Account WHERE Login_Name LIKE 'IT\_%' ESCAPE '\';
SELECT '学生(31=30在册+1已退宿)' AS ITEM, COUNT(*) AS CNT FROM D_Student WHERE Student_ID LIKE 'IT\_%' ESCAPE '\';
SELECT '管理员(5)' AS ITEM, COUNT(*) AS CNT FROM D_Admin WHERE Admin_ID LIKE 'IT\_%' ESCAPE '\';
SELECT '楼栋(2)' AS ITEM, COUNT(*) AS CNT FROM D_Building WHERE Building_ID >= 9000;
SELECT '房间(48)' AS ITEM, COUNT(*) AS CNT FROM D_Room WHERE Room_ID >= 900000;
SELECT '活动住宿分配(28)' AS ITEM, COUNT(*) AS CNT FROM D_Bed_Allocation WHERE Allocation_ID >= 900000 AND CheckOut_Date IS NULL;
SELECT '水电账单(144=3月×48房)' AS ITEM, COUNT(*) AS CNT FROM D_Utility_Fee WHERE Fee_ID >= 900000;
SELECT '分摊明细(手写+生成)' AS ITEM, COUNT(*) AS CNT FROM D_Fee_Detail WHERE Detail_ID >= 900000;
SELECT '报修工单(20)' AS ITEM, COUNT(*) AS CNT FROM D_Repair_Ticket WHERE Ticket_ID >= 900000;
SELECT '卫生记录(144+)' AS ITEM, COUNT(*) AS CNT FROM D_Hygiene_Record WHERE Record_ID >= 900000;
SELECT '门禁记录' AS ITEM, COUNT(*) AS CNT FROM D_Access_Log WHERE Log_ID >= 900000;
SELECT '资产(192+3)' AS ITEM, COUNT(*) AS CNT FROM D_Asset WHERE Asset_ID >= 900000;
SELECT '公告(6,含置顶)' AS ITEM, COUNT(*) AS CNT FROM D_Notice WHERE Notice_ID >= 900000;

-- 状态分布（每行应 >= 1）
SELECT '报修状态分布' AS ITEM, Status, COUNT(*) AS CNT FROM D_Repair_Ticket WHERE Ticket_ID >= 900000 GROUP BY Status ORDER BY Status;
SELECT '预约状态分布' AS ITEM, Status, COUNT(*) AS CNT FROM D_Facility_Booking WHERE Booking_ID >= 900000 GROUP BY Status ORDER BY Status;
SELECT '通知类型分布' AS ITEM, Notification_Type, COUNT(*) AS CNT FROM D_Notification WHERE Notification_ID >= 900000 GROUP BY Notification_Type ORDER BY Notification_Type;
SELECT '退宿状态分布' AS ITEM, Status, COUNT(*) AS CNT FROM D_Checkout_Log WHERE Log_ID >= 900000 GROUP BY Status ORDER BY Status;
SELECT '访客授权状态分布' AS ITEM, Status, COUNT(*) AS CNT FROM D_Visitor_Authorization WHERE Authorization_ID >= 900000 GROUP BY Status ORDER BY Status;

-- 演示账号登录断言（应各返回 1 行，密码均为 Temp@123；IT_STU_005 为 123456 首登）
SELECT '登录断言-学生001' AS ITEM, COUNT(*) AS CNT FROM D_User_Account WHERE Login_Name = 'IT_STU_001' AND Account_Status = '正常' AND Is_First_Login = 'N';
SELECT '登录断言-楼长001' AS ITEM, COUNT(*) AS CNT FROM D_User_Account WHERE Login_Name = 'IT_ADMIN_001' AND Account_Status = '正常' AND Is_First_Login = 'N';
SELECT '登录断言-维修员001' AS ITEM, COUNT(*) AS CNT FROM D_User_Account WHERE Login_Name = 'IT_REPAIR_001' AND Account_Status = '正常' AND Is_First_Login = 'N';
SELECT '登录断言-辅导员001' AS ITEM, COUNT(*) AS CNT FROM D_User_Account WHERE Login_Name = 'IT_COUN_001' AND Account_Status = '正常' AND Is_First_Login = 'N';
SELECT '登录断言-超管001' AS ITEM, COUNT(*) AS CNT FROM D_User_Account WHERE Login_Name = 'IT_SUPER_001' AND Account_Status = '正常' AND Is_First_Login = 'N';
SELECT '首登改密-005(Y)' AS ITEM, COUNT(*) AS CNT FROM D_User_Account WHERE Login_Name = 'IT_STU_005' AND Is_First_Login = 'Y';
