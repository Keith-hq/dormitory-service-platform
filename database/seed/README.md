# database/seed — 伪数据集（报表丰富版）

> 对应 bug 清单 **GAP-007**（测试数据不可复现）与演示前提 **D0-2**（种子数据已入库）。
> 口径：**报表丰富版** = 主数据演示级（2 楼栋 / 48 房 / 30 学生 / 5 管理员）+ 业务流水铺 3 个月（2026-05/06/07）。
> 设计文档：`work/plans/伪数据集设计-v1.md`。

## 文件与执行顺序

| 文件 | 内容 | 依赖 |
|---|---|---|
| `00_cleanup.sql` | 清理伪数据预留段（9xxxxx / IT_%），幂等可重跑 | 无 |
| `01_master_data.sql` | 学院/专业/楼栋/房间/学生/管理员/账号/住宿分配/资产/预警 | 00 |
| `02_billing_wallet.sql` | 账单 3 月 / 分摊明细 / 钱包 / 日志 / 扣款尝试 | 01 |
| `03_repair_maintenance.sql` | 设施/预约/共享物品/借用/报修/维修/耗材/附件 | 01 |
| `04_safety_community.sql` | 门禁/晚归/访客授权+登记/快递/投票/违规/订水 | 01,03 |
| `05_daily_ops.sql` | 卫生+评论/保洁/信用分/通知/审计/退宿清算/离校报备 | 01,03,04 |
| `99_validate.sql` | 断言（应 0 行）+ 汇总计数 + 状态分布 | 00~05 |

```sql
-- 顺序执行（Oracle / SQL Developer / SQL*Plus）
@database/seed/00_cleanup.sql
@database/seed/01_master_data.sql
@database/seed/02_billing_wallet.sql
@database/seed/03_repair_maintenance.sql
@database/seed/04_safety_community.sql
@database/seed/05_daily_ops.sql
@database/seed/99_validate.sql
```

前提：`foundation/001_create_tables.sql` 与 `extensions/010~032*.sql` 已建库。

## 演示账号（密码）

| 账号 | 角色 | 初始密码 | 说明 |
|---|---|---|---|
| IT_STU_001 | 学生 | `Temp@123` | 主线学生（缴费/报修/预约/信用分演示） |
| IT_STU_003 | 学生 | `Temp@123` | 欠费阻断（房间断电、余额不足、退宿待清算） |
| IT_STU_004 | 学生 | `Temp@123` | 未取快递（退宿/快递校验） |
| IT_STU_005 | 学生 | `123456` | **新开通账号**，首登强制改密（`Is_First_Login='Y'`） |
| IT_STU_002 | 学生 | `Temp@123` | 无床位，供"并发抢床位"演示 |
| IT_ADMIN_001 | 宿管(楼长) | `Temp@123` | 楼栋 9001 主线宿管 |
| IT_REPAIR_001 / 002 | 维修员 | `Temp@123` | 派单 / 并发接单 |
| IT_COUN_001 | 辅导员 | `Temp@123` | 离校报备审批 |
| IT_SUPER_001 | 超级管理员 | `Temp@123` | 档案 / 报表 / 审计 / 导入 |

> 除 IT_STU_005 外全部 `Is_First_Login='N'`（登录即用，不被首登改密中间件拦截）。
> 密码哈希为真实 BCrypt（`$2a$11$...`），重新生成方法见文末。

## 主键与清理约定

- 全部数值主键落在 **9xxxxx 段**，应用序列（1 起）互不撞号；账号/学生/管理员用 `IT_` 前缀。
- 重新灌数：重跑 `00_cleanup.sql` 再执行 01~05 即可（可整体重跑）。

## 演示场景索引（快速定位数据）

| 场景 | 数据 |
|---|---|
| 缴费划扣（C3/S2-6） | 001 的 7 月账单（Fee 920201 / Detail 940001 未缴，钱包 150） |
| 欠费断电 | 900102 `Power_Status='断电'`，003 扣款尝试 9601001/9601002 余额不足 |
| 退宿清算（C2/S3-3） | 003 待清算（909901）、031 已通过（909902）、013 已取消（909903） |
| 并发抢床位（C2/S3-2） | 900101 空床、002 无床位 |
| 账单发布+分摊（S3-4） | 7 月 `MOD(房序,7)=0` 的房间未发布（如 900107） |
| SLA 派单（C4） | 903003 紧急超时未派（Escalation_Time 非空） |
| 设施预约并发（C5） | 901301/901302 已预约、901303 使用中 |
| 共享物品（C5） | 901003 库存-1、901006 超时未还（OVERDUE-901103 扣分） |
| 信用分申诉（C6） | 003/007/009/028 扣分明细（Event_Key 唯一） |
| 离校审批（C2/S4-1） | 909801 待批、909802 已通过、909803 已驳回（Reason 非空） |
| 报表/排名（S4-3） | 3 个月账单/卫生/门禁/信用分明细有量 |

## 已知口径与风险

1. **辅导员角色**：`IT_COUN_001` 按代码口径写 `Role_Level='辅导员'`。扩展迁移 032（ADR-0007）已将 `CK_D_ADMIN_ROLE` 纳入辅导员，按前置 010~032 建库后不再触发 ORA-02290（原风险 R1 已化解）。
2. **通知类型**：一律用中文 6 值（预约/报修/账单/信用/访客/系统），与迁移 011 一致（EF 里的英文旧值不采用）。
3. **报修状态**：统一用 `已撤销`（非"已取消"）。
4. **快递（PKG）/ 桶装水（WATER）**：契约存在但实现侧未落地，本脚本按契约造状态样本兜底，供演示与联调。
5. **Excel 导入样例**：`excel/student_import_sample.csv`（19 有效 + 1 缺必填），列结构为自定义占位——IMPORT-01 契约未锁定时先用 CSV，对齐后可另存为 xlsx。
6. **同步**：脚本基于 `origin/develop`（#55 rbac-audit 已合入）编写，合入前请再次 `git fetch` 核对。

## 重新生成密码哈希（如需改密码）

```bash
# 用后端同款 BCrypt.Net 生成（本项目密码算法 = BCrypt）
dotnet new console -o tmp && cd tmp
dotnet add package BCrypt.Net-Next
# Program.cs: Console.WriteLine(BCrypt.Net.BCrypt.HashPassword("新密码"));
dotnet run
# 将输出哈希替换 01_master_data.sql 中的 $2a$... 串
```
