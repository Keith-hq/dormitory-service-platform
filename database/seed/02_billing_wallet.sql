SET DEFINE OFF;
SET AUTOCOMMIT ON;
-- 02 缴费钱包：水电账单(3月)/分摊明细/钱包/钱包日志/扣款尝试
-- 依赖：01_master_data.sql 已执行（房间、住宿分配、账号已建）。
-- P0 链路 C3（缴费划扣）：001 可现场缴费；003 欠费阻断（余额不足）。

-- ===== 1. 水电账单 D_Utility_Fee（3 个月 × 48 房） =====
-- 房序 seq：9001 楼 1..24，9002 楼 25..48。Fee_ID = 920000 + 月偏移×100 + 房序。
-- （用内联子查询 rs 代替 WITH，兼容 sqlplus 管道执行。）
-- 2026-05：全部已发布、全部已缴（历史）。
INSERT INTO D_Utility_Fee (Fee_ID, Room_ID, Year_Month, Water_Fee, Power_Fee, Is_Paid, Publish_Status)
SELECT 920000 + rs.seq, rs.Room_ID, '2026-05',
       20 + MOD(rs.seq * 3, 16), 50 + MOD(rs.seq * 7, 41),
       '是', '已发布'
FROM (SELECT Room_ID,
             CASE WHEN Building_ID = 9001 THEN MOD(Room_ID, 100)
                  ELSE 24 + MOD(Room_ID, 100) END AS seq
      FROM D_Room
      WHERE Room_ID BETWEEN 900101 AND 900124
         OR Room_ID BETWEEN 900201 AND 900224) rs;

-- 2026-06：全部已发布，少数历史欠费。
INSERT INTO D_Utility_Fee (Fee_ID, Room_ID, Year_Month, Water_Fee, Power_Fee, Is_Paid, Publish_Status)
SELECT 920000 + 100 + rs.seq, rs.Room_ID, '2026-06',
       20 + MOD(rs.seq * 6, 16), 50 + MOD(rs.seq * 7 + 5, 41),
       CASE WHEN MOD(rs.seq, 11) = 0 THEN '否' ELSE '是' END, '已发布'
FROM (SELECT Room_ID,
             CASE WHEN Building_ID = 9001 THEN MOD(Room_ID, 100)
                  ELSE 24 + MOD(Room_ID, 100) END AS seq
      FROM D_Room
      WHERE Room_ID BETWEEN 900101 AND 900124
         OR Room_ID BETWEEN 900201 AND 900224) rs;

-- 2026-07（演示月）：
--   - 900101 / 900102 已发布但未缴（001 现场缴费、003 欠费阻断）；
--   - MOD(房序,7)=0 的房间未发布（供 S3-4 现场录入发布 + 智能分摊，如 900107）；
--   - 其余已发布已缴。
INSERT INTO D_Utility_Fee (Fee_ID, Room_ID, Year_Month, Water_Fee, Power_Fee, Is_Paid, Publish_Status)
SELECT 920000 + 200 + rs.seq, rs.Room_ID, '2026-07',
       20 + MOD(rs.seq * 9, 16), 50 + MOD(rs.seq * 7 + 10, 41),
       CASE WHEN rs.Room_ID IN (900101, 900102) OR MOD(rs.seq, 7) = 0 THEN '否' ELSE '是' END,
       CASE WHEN MOD(rs.seq, 7) = 0 THEN '未发布' ELSE '已发布' END
FROM (SELECT Room_ID,
             CASE WHEN Building_ID = 9001 THEN MOD(Room_ID, 100)
                  ELSE 24 + MOD(Room_ID, 100) END AS seq
      FROM D_Room
      WHERE Room_ID BETWEEN 900101 AND 900124
         OR Room_ID BETWEEN 900201 AND 900224) rs;

-- 对齐契约样本：900101 7月 water30/elec70；900102 欠费断电房间 water40/elec90。
UPDATE D_Utility_Fee SET Water_Fee = 30, Power_Fee = 70 WHERE Room_ID = 900101 AND Year_Month = '2026-07';
UPDATE D_Utility_Fee SET Water_Fee = 40, Power_Fee = 90 WHERE Room_ID = 900102 AND Year_Month = '2026-07';

-- ===== 2. 分摊明细 D_Fee_Detail =====
-- 2a. 手写 7 月演示明细（940001..940003，P0 缴费链需可引用 Detail_ID）：
--     940001 = 001/900101 未缴（现场缴费）；940002/940003 = 003/004/900102 未缴（欠费阻断）。
INSERT INTO D_Fee_Detail (Detail_ID, Fee_ID, Student_ID, Room_ID, Water_Share, Power_Share, Stay_Days, Total_Days, Bill_Type, Is_Paid, Create_Time)
VALUES (940001, 920201, 'IT_STU_001', 900101, 30.00, 70.00, 30, 30, '月度', '否', DATE '2026-08-01');
INSERT INTO D_Fee_Detail (Detail_ID, Fee_ID, Student_ID, Room_ID, Water_Share, Power_Share, Stay_Days, Total_Days, Bill_Type, Is_Paid, Create_Time)
VALUES (940002, 920202, 'IT_STU_003', 900102, 20.00, 45.00, 30, 30, '月度', '否', DATE '2026-08-01');
INSERT INTO D_Fee_Detail (Detail_ID, Fee_ID, Student_ID, Room_ID, Water_Share, Power_Share, Stay_Days, Total_Days, Bill_Type, Is_Paid, Create_Time)
VALUES (940003, 920202, 'IT_STU_004', 900102, 20.00, 45.00, 30, 30, '月度', '否', DATE '2026-08-01');

-- 2b. 生成 5/6 月明细 + 7 月非演示房间明细（940100+）：
--     按"该月全月在住"的活动分配拆分（Share = 房间账单 / 同房同月在住人数）。
INSERT INTO D_Fee_Detail (Detail_ID, Fee_ID, Student_ID, Room_ID, Water_Share, Power_Share, Stay_Days, Total_Days, Bill_Type, Is_Paid, Create_Time)
SELECT 940100 + ROWNUM, uf.Fee_ID, ba.Student_ID, uf.Room_ID,
       ROUND(uf.Water_Fee / occ.cnt, 2),
       ROUND(uf.Power_Fee / occ.cnt, 2),
       30, 30, '月度', uf.Is_Paid, SYSDATE
FROM D_Utility_Fee uf
JOIN D_Bed_Allocation ba
  ON ba.Room_ID = uf.Room_ID
 AND ba.CheckIn_Date <= LAST_DAY(TO_DATE(uf.Year_Month || '-01', 'YYYY-MM-DD'))
 AND (ba.CheckOut_Date IS NULL OR ba.CheckOut_Date >= LAST_DAY(TO_DATE(uf.Year_Month || '-01', 'YYYY-MM-DD')))
JOIN (SELECT uf2.Fee_ID, COUNT(*) AS cnt
      FROM D_Utility_Fee uf2
      JOIN D_Bed_Allocation ba2
        ON ba2.Room_ID = uf2.Room_ID
       AND ba2.CheckIn_Date <= LAST_DAY(TO_DATE(uf2.Year_Month || '-01', 'YYYY-MM-DD'))
       AND (ba2.CheckOut_Date IS NULL OR ba2.CheckOut_Date >= LAST_DAY(TO_DATE(uf2.Year_Month || '-01', 'YYYY-MM-DD')))
      GROUP BY uf2.Fee_ID) occ
  ON occ.Fee_ID = uf.Fee_ID
WHERE uf.Publish_Status = '已发布'
  AND (uf.Year_Month IN ('2026-05', '2026-06')
       OR (uf.Year_Month = '2026-07' AND uf.Room_ID NOT IN (900101, 900102)));

-- ===== 3. 钱包 D_Wallet_Account（30 学生，余额与日志对齐） =====
INSERT INTO D_Wallet_Account (Student_ID, Balance) VALUES ('IT_STU_001', 150);
INSERT INTO D_Wallet_Account (Student_ID, Balance) VALUES ('IT_STU_002', 100);
INSERT INTO D_Wallet_Account (Student_ID, Balance) VALUES ('IT_STU_003', 20);
INSERT INTO D_Wallet_Account (Student_ID, Balance) VALUES ('IT_STU_004', 80);
INSERT INTO D_Wallet_Account (Student_ID, Balance) VALUES ('IT_STU_005', 100);
INSERT INTO D_Wallet_Account (Student_ID, Balance) VALUES ('IT_STU_006', 120);
INSERT INTO D_Wallet_Account (Student_ID, Balance) VALUES ('IT_STU_007', 90);
INSERT INTO D_Wallet_Account (Student_ID, Balance) VALUES ('IT_STU_008', 200);
INSERT INTO D_Wallet_Account (Student_ID, Balance) VALUES ('IT_STU_009', 120);
INSERT INTO D_Wallet_Account (Student_ID, Balance) VALUES ('IT_STU_010', 100);
INSERT INTO D_Wallet_Account (Student_ID, Balance) VALUES ('IT_STU_011', 100);
INSERT INTO D_Wallet_Account (Student_ID, Balance) VALUES ('IT_STU_012', 80);
INSERT INTO D_Wallet_Account (Student_ID, Balance) VALUES ('IT_STU_013', 100);
INSERT INTO D_Wallet_Account (Student_ID, Balance) VALUES ('IT_STU_014', 100);
INSERT INTO D_Wallet_Account (Student_ID, Balance) VALUES ('IT_STU_015', 100);
INSERT INTO D_Wallet_Account (Student_ID, Balance) VALUES ('IT_STU_016', 120);
INSERT INTO D_Wallet_Account (Student_ID, Balance) VALUES ('IT_STU_017', 100);
INSERT INTO D_Wallet_Account (Student_ID, Balance) VALUES ('IT_STU_018', 100);
INSERT INTO D_Wallet_Account (Student_ID, Balance) VALUES ('IT_STU_019', 100);
INSERT INTO D_Wallet_Account (Student_ID, Balance) VALUES ('IT_STU_020', 100);
INSERT INTO D_Wallet_Account (Student_ID, Balance) VALUES ('IT_STU_021', 100);
INSERT INTO D_Wallet_Account (Student_ID, Balance) VALUES ('IT_STU_022', 100);
INSERT INTO D_Wallet_Account (Student_ID, Balance) VALUES ('IT_STU_023', 100);
INSERT INTO D_Wallet_Account (Student_ID, Balance) VALUES ('IT_STU_024', 100);
INSERT INTO D_Wallet_Account (Student_ID, Balance) VALUES ('IT_STU_025', 100);
INSERT INTO D_Wallet_Account (Student_ID, Balance) VALUES ('IT_STU_026', 100);
INSERT INTO D_Wallet_Account (Student_ID, Balance) VALUES ('IT_STU_027', 100);
INSERT INTO D_Wallet_Account (Student_ID, Balance) VALUES ('IT_STU_028', 100);
INSERT INTO D_Wallet_Account (Student_ID, Balance) VALUES ('IT_STU_029', 100);
INSERT INTO D_Wallet_Account (Student_ID, Balance) VALUES ('IT_STU_030', 100);

-- ===== 4. 钱包日志 D_Wallet_Log（演示学生充值/缴费 + 已缴样本） =====
-- Idempotency_Key 唯一（契约幂等头约定 IT-C3-xxx-x）；Detail_ID 可空。
INSERT INTO D_Wallet_Log (Log_ID, Student_ID, Amount, Transaction_Type, Before_Balance, After_Balance, Detail_ID, Idempotency_Key, Create_Time)
VALUES (9600001, 'IT_STU_001', 150.00, '充值',       0.00, 150.00, NULL, 'IT-C3-001-1', DATE '2026-08-01');
INSERT INTO D_Wallet_Log (Log_ID, Student_ID, Amount, Transaction_Type, Before_Balance, After_Balance, Detail_ID, Idempotency_Key, Create_Time)
VALUES (9600002, 'IT_STU_003', 20.00,  '充值',       0.00, 20.00,  NULL, 'IT-C3-003-1', DATE '2026-08-01');
INSERT INTO D_Wallet_Log (Log_ID, Student_ID, Amount, Transaction_Type, Before_Balance, After_Balance, Detail_ID, Idempotency_Key, Create_Time)
VALUES (9600003, 'IT_STU_004', 80.00,  '充值',       0.00, 80.00,  NULL, 'IT-C3-004-1', DATE '2026-08-01');
INSERT INTO D_Wallet_Log (Log_ID, Student_ID, Amount, Transaction_Type, Before_Balance, After_Balance, Detail_ID, Idempotency_Key, Create_Time)
VALUES (9600004, 'IT_STU_005', 100.00, '充值',       0.00, 100.00, NULL, 'IT-C3-005-1', DATE '2026-08-01');
INSERT INTO D_Wallet_Log (Log_ID, Student_ID, Amount, Transaction_Type, Before_Balance, After_Balance, Detail_ID, Idempotency_Key, Create_Time)
VALUES (9600005, 'IT_STU_002', 100.00, '充值',       0.00, 100.00, NULL, 'IT-C3-002-1', DATE '2026-08-01');
INSERT INTO D_Wallet_Log (Log_ID, Student_ID, Amount, Transaction_Type, Before_Balance, After_Balance, Detail_ID, Idempotency_Key, Create_Time)
VALUES (9600006, 'IT_STU_009', 150.00, '充值',       0.00, 150.00, NULL, 'IT-C3-009-1', DATE '2026-08-01');
INSERT INTO D_Wallet_Log (Log_ID, Student_ID, Amount, Transaction_Type, Before_Balance, After_Balance, Detail_ID, Idempotency_Key, Create_Time)
VALUES (9600007, 'IT_STU_009', 30.00,  '人工缴费',   150.00, 120.00, NULL, 'IT-C3-009-2', DATE '2026-08-05');
INSERT INTO D_Wallet_Log (Log_ID, Student_ID, Amount, Transaction_Type, Before_Balance, After_Balance, Detail_ID, Idempotency_Key, Create_Time)
VALUES (9600008, 'IT_STU_016', 120.00, '充值',       0.00, 120.00, NULL, 'IT-C3-016-1', DATE '2026-08-01');

-- ===== 5. 扣款尝试 D_Fee_Deduction_Attempt（留空，不造样本） =====
-- ⚠️ 历史 schema 缺陷：D_Fee_Deduction_Attempt.RESULT 原为 VARCHAR2(10) 字节语义，
--    写 '余额不足'（4 汉字 = 12 字节）会 ORA-12899；已由迁移
--    035_fix_byte_columns_fee_attempt_audit.sql 改为 VARCHAR2(20 CHAR) 修复。
--    本脚本仍不造扣款尝试样本；003 欠费阻断由 "未缴账单 + 钱包余额 20 < 账单"体现。

-- 完成确认
SELECT 'BILLING & WALLET DONE' AS MESSAGE FROM DUAL;
