# 数据库脚本

## 当前基线

当前 foundation 包含原有 20 张表，以及以下 5 个已经确认的字段：

- `D_Room.Power_Status`
- `D_Repair_Ticket.SLA_Level`
- `D_Repair_Ticket.Deadline`
- `D_Repair_Ticket.Assigned_To`
- `D_Admin.Building_ID`

`D_Water_Order` 之所以保留在 foundation 中，仅因为它属于原有 20 张表的技术验证基线。桶装水业务已经暂缓，当前主线业务不得依赖此表。

## 执行顺序

1. 启动 `deploy/oracle/` 中说明的 Oracle XE 环境。
2. 使用 DBeaver 连接项目专用用户。
3. 在干净的 schema 中执行 `ddl/foundation/001_create_tables.sql`。
4. 执行 `verify/foundation_schema_checks.sql` 并保留结果。
5. 重建干净 schema 或容器，再重复执行这两个脚本。

foundation 脚本是可复现的正式来源。DBeaver 只用于执行和检查脚本。本次数据库变更不要修改 `deploy/oracle/`。

基础基线完成评审后，扩展必须使用独立且有编号的脚本添加。不要通过重写 foundation 脚本来增加业务表或改变既有字段语义。

## 当前扩展基线

已裁决的扩展表共 25 张：`010_extension_tables.sql` 定义 23 张，迁移 029 新增 `D_Asset_Repair` / `D_Asset_Warning`（资产管理扩展，见迁移 029）：

- 费用与钱包：`D_Fee_Detail`、`D_Wallet_Account`、`D_Wallet_Log`、`D_Fee_Deduction_Attempt`
- 信用与共享物品：`D_Credit_Account`、`D_Credit_Log`、`D_Shared_Item`、`D_Item_Loan`
- 设施与清洁：`D_Facility`、`D_Facility_Booking`、`D_Cleaning_Task`
- 维修扩展：`D_Repair_Material`、`D_Repair_Material_Usage`、`D_Repair_Attachment`
- 账号与治理：`D_User_Account`、`D_Notification`、`D_Visitor_Authorization`、`D_Audit_Event`
- 宿舍治理与退宿：`D_Room_Vote`、`D_Room_Vote_Response`、`D_Checkout_Log`、`D_Notice_Display`、`D_Hygiene_Comment`

扩展脚本不修改 foundation，也不创建 `D_Facility_Usage`、`D_Repair_SLA_Event`、`D_Role`、`D_Notification_Recipient` 或 `D_Fee_Adjustment`。设施使用次数直接由预约记录统计，SLA 升级事件写入审计事件，公告置顶和卫生评语分别放在扩展表中。退宿检查只保留水电和共享物品，快递业务暂缓。

Oracle 的 `COMMENT` 是关键字，因此 `D_Hygiene_Comment` 中按裁决保留的评语列使用带引号的标识符 `"COMMENT"`。后端查询该列时也必须使用 `"COMMENT"`；其余表字段均使用普通未加引号标识符。

## 完整执行顺序

在干净的 schema 中按以下顺序执行：

1. `ddl/foundation/001_create_tables.sql`
2. `ddl/extensions/010_extension_tables.sql`
3. `ddl/extensions/011_notification_type_check.sql`
4. `ddl/extensions/012_notification_id_sequence.sql`
5. `ddl/extensions/013_notification_char_length.sql`
6. `ddl/extensions/014_credit_log_sequence.sql`
7. `ddl/extensions/015_facility_notice_sequences.sql`
8. `ddl/extensions/016_d_room_floor_status.sql`
9. `ddl/extensions/017_d_leave_application_reason.sql`
10. `ddl/extensions/018_d_student_email.sql`
11. `ddl/extensions/019_shared_item_idempotency.sql`
12. `ddl/extensions/020_repair_late_hygiene_room_sequences.sql`
13. `ddl/extensions/021_sla_dispatch.sql`
14. `ddl/extensions/022_fee_detail_dedup_uk.sql`
15. `ddl/extensions/023_dorm_checkout_sequences_room_unique.sql`
16. `ddl/extensions/024_vote_visitor_parcel_sequences.sql`
17. `ddl/extensions/025_audit_details_college_major_sequences.sql`（D_Audit_Event.DETAILS + 学院/专业序列）
18. `ddl/extensions/026_utility_fee_sequence.sql`（D_Utility_Fee 序列）
19. `ddl/extensions/027_add_admin_post.sql`（D_Admin.POST 宿管岗位字段）
20. `ddl/extensions/028_user_account_sequence.sql`（D_USER_ACCOUNT 序列）
21. `ddl/extensions/029_asset_shareditem_cleaning_sequences_and_tables.sql`（资产/共享物品/保洁主数据前置）
22. `ddl/extensions/030_add_token_version_and_first_login.sql`（TokenVersion / IsFirstLogin）
23. `ddl/extensions/031_audit_event_sequence.sql`（D_Audit_Event 主键序列 + 触发器）
24. `ddl/extensions/032_admin_role_include_counselor.sql`（CK_D_ADMIN_ROLE 纳入"辅导员"，ADR-0007）
25. `verify/foundation_schema_checks.sql`
26. `verify/extension_schema_checks.sql`

`010_extension_tables.sql` 是一次性建表脚本。若表已存在，请使用全新的 schema 或容器进行复现，不要通过删表来绕过依赖问题。
`011` 至 `032` 是按编号顺序执行的增量迁移；
已有环境只执行尚未应用的迁移，不要重复执行已完成的 `ALTER TABLE` 脚本。

## 存储过程执行顺序

在 `sp/` 目录下按依赖顺序执行（`@` 或整段粘贴均可）：

1. `sp_fee_sharing.sql`（创建 SEQ_FEE_DETAIL + SP_Calc_Monthly_Fee / SP_Calc_Checkout_Fee）
2. `sp_billing.sql`（创建 SEQ_WALLET_LOG、SEQ_FEE_DED_ATT + SP_Auto_Deduct / SP_Check_Power_Cut / SP_Restore_Power）
3. `sp_wallet.sql`（SP_Manual_Pay / SP_Recharge，复用 sp_billing.sql 的 SEQ_WALLET_LOG，不内联 DDL）

测试脚本（`sp/`）：

- `test_sp_checkout_fee.sql`：难点⑥ 退宿结算回归（16 断言，月份无关化，自清理）
- `test_sp_wallet.sql`：难点② 充值/缴费（49 断言，含 SP_Auto_Deduct 双方向竞态验证 T15/T16/T17，自清理）

测试均为 `EXIT :g_fail` 模式（0=全过，1=有失败），供 CI 判定。
