SET DEFINE OFF;
SET AUTOCOMMIT ON;
-- 伪数据集清理脚本（幂等，先清预留段再插入）。
-- 清理规则：
--   - 数值主键表：删除伪数据段的行。
--     主键段分两种：学院/专业/楼栋用 4 位 9001..9006（阈值 >=9000）；
--     其余表用 6 位 900101..（阈值 >=900000）。应用序列 1 起，不撞号。
--   - 字符串主键表（学生/管理员/钱包/信用分/账号）：删除 IT_ 前缀。
-- 执行顺序：子表在前、父表在后，避免外键冲突。
-- 本脚本不触碰 DDL 基线（foundation 20 表冻结），仅做 DML 清理。

-- ===== 1) 子表（引用他人，先删） =====
DELETE FROM D_Room_Vote_Response WHERE Student_ID LIKE 'IT\_%' ESCAPE '\';
DELETE FROM D_Room_Vote             WHERE Vote_ID >= 900000;
DELETE FROM D_Checkout_Log           WHERE Log_ID >= 900000;
DELETE FROM D_Bed_Allocation         WHERE Allocation_ID >= 900000;
DELETE FROM D_Fee_Deduction_Attempt  WHERE Attempt_ID >= 900000;
DELETE FROM D_Wallet_Log             WHERE Log_ID >= 900000;
DELETE FROM D_Fee_Detail             WHERE Detail_ID >= 900000;
DELETE FROM D_Wallet_Account         WHERE Student_ID LIKE 'IT\_%' ESCAPE '\';
DELETE FROM D_Credit_Log             WHERE Log_ID >= 900000;
DELETE FROM D_Credit_Account         WHERE Student_ID LIKE 'IT\_%' ESCAPE '\';
DELETE FROM D_Utility_Fee            WHERE Fee_ID >= 900000;
DELETE FROM D_Asset_Repair           WHERE Link_ID >= 900000;
DELETE FROM D_Asset_Warning          WHERE Warning_ID >= 900000;
DELETE FROM D_Asset                  WHERE Asset_ID >= 900000;
DELETE FROM D_Repair_Material_Usage  WHERE Usage_ID >= 900000;
DELETE FROM D_Repair_Attachment      WHERE Attachment_ID >= 900000;
DELETE FROM D_Repair_Log             WHERE Log_ID >= 900000;
DELETE FROM D_Repair_Ticket          WHERE Ticket_ID >= 900000;
DELETE FROM D_Repair_Material        WHERE Material_ID >= 900000;
DELETE FROM D_Item_Loan              WHERE Loan_ID >= 900000;
DELETE FROM D_Shared_Item            WHERE Item_ID >= 900000;
DELETE FROM D_Cleaning_Task          WHERE Task_ID >= 900000;
DELETE FROM D_Facility_Booking       WHERE Booking_ID >= 900000;
DELETE FROM D_Facility               WHERE Facility_ID >= 900000;
DELETE FROM D_Visitor_Authorization  WHERE Authorization_ID >= 900000;
DELETE FROM D_Notification           WHERE Notification_ID >= 900000;
DELETE FROM D_Audit_Event            WHERE Audit_ID >= 900000;
DELETE FROM D_User_Account           WHERE Login_Name LIKE 'IT\_%' ESCAPE '\';
DELETE FROM D_Visitor_Log            WHERE Visitor_ID >= 900000;
DELETE FROM D_Access_Log             WHERE Log_ID >= 900000;
DELETE FROM D_Late_Entry             WHERE Record_ID >= 900000;
DELETE FROM D_Parcel_Record          WHERE Parcel_ID >= 900000;
DELETE FROM D_Violation_Record       WHERE Record_ID >= 900000;
DELETE FROM D_Leave_Application      WHERE Apply_ID >= 900000;
DELETE FROM D_Water_Order            WHERE Order_ID >= 900000;
DELETE FROM D_Hygiene_Comment        WHERE Record_ID >= 900000;
DELETE FROM D_Hygiene_Record         WHERE Record_ID >= 900000;
DELETE FROM D_Notice_Display         WHERE Notice_ID >= 900000;
DELETE FROM D_Notice                 WHERE Notice_ID >= 900000;

-- ===== 2) 父表（被引用，最后删） =====
DELETE FROM D_Student   WHERE Student_ID LIKE 'IT\_%' ESCAPE '\';
DELETE FROM D_Admin     WHERE Admin_ID   LIKE 'IT\_%' ESCAPE '\';
DELETE FROM D_Room      WHERE Room_ID >= 900000;
DELETE FROM D_Major     WHERE Major_ID >= 9000;
DELETE FROM D_Building  WHERE Building_ID >= 9000;
DELETE FROM D_College   WHERE College_ID >= 9000;

-- 完成后返回清理结果汇总
SELECT 'CLEANUP DONE: 伪数据预留段（9xxxxx / IT_%）已清空' AS MESSAGE FROM DUAL;
