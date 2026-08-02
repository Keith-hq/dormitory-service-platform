# dormitory-service-platform

高校宿舍后勤与共享生活服务平台｜数据库课程设计项目

**技术栈：** Vue3 + Vite + Pinia + ASP.NET Core .NET8 C# + Oracle 26AI + EF Core + Docker Compose + Nginx

**功能覆盖：** 宿舍资产、住宿管理、水电分摊、共享借用、访客二维码、维修 SLA 派单、退宿清算等高并发业务；内置标准化前后端样板间、Oracle 容器一键环境、CI/CD 自动部署、Apifox 统一接口契约。

---

## 分支命名规范

> **统一前缀 + 描述，小写英文，横线分隔。**

标准分支命名格式：`{前缀}/{模块名}-{功能简述}`

### 受保护分支

| 分支 | 说明 |
|---|---|
| `master` | 生产分支，只接受 PR 合并，需 Review + CI 通过 |
| `develop` | 开发主分支，M1 样板间代码在此维护 |

### 1. 功能开发分支 `feature/`

- **前缀：** `feature/`
- **格式：** `feature/{模块名}-{功能简述}`
- **适用：** 新业务功能、页面、接口、难点业务逻辑开发
- **示例：**

| 分支名 | 说明 |
|---|---|
| `feature/building-room-assets` | 楼栋房间资产模块 |
| `feature/shared-goods-borrow` | 共享物品借用归还 |
| `feature/utility-bill-calc` | 水电费分摊难点功能 |
| `feature/checkout-settle` | 退宿清算核心逻辑 |
| `feature/visitor-qrcode` | 访客二维码模块 |

### 2. Bug 修复分支 `fix/`

- **前缀：** `fix/`
- **格式：** `fix/{模块名}-{问题简述}`
- **适用：** 联调、集成测试发现的逻辑报错、页面 bug、SQL 异常
- **示例：**

| 分支名 | 说明 |
|---|---|
| `fix/login-jwt-auth` | 登录鉴权失效修复 |
| `fix/oracle-conn-timeout` | Oracle 数据库连接超时修复 |
| `fix/credit-score-deduct` | 信用分扣减事务 bug |
| `fix/room-status-sync` | 房间状态同步异常修复 |

### 3. 运维 / 环境部署分支 `ops/`

- **前缀：** `ops/`
- **格式：** `ops/{操作类型}-{目标}`
- **适用：** Docker 配置、Nginx、CI/CD、云端部署、环境脚本调整
- **示例：**

| 分支名 | 说明 |
|---|---|
| `ops/local-oracle-compose` | Oracle 容器环境配置 |
| `ops/nginx-reverse-proxy` | 云端 Nginx 反向代理配置 |
| `ops/cicd-auto-deploy` | 自动化部署流水线脚本 |
| `ops/backup-restore` | 数据库备份恢复脚本 |

### 4. 文档分支 `docs/`

- **前缀：** `docs/`
- **格式：** `docs/{文档类型}-{内容}`
- **适用：** 需求文档、数据库设计、样板间教程、部署报告、PPT 素材
- **示例：**

| 分支名 | 说明 |
|---|---|
| `docs/template-dev-guide` | 样板间开发教程 |
| `docs/deploy-check-report` | 云端部署验证报告 |
| `docs/m1-acceptance-file` | M1 里程碑验收材料 |
| `docs/api-contract-spec` | 接口契约规范文档 |

### 5. 重构 / 优化分支 `refactor/`

- **前缀：** `refactor/`
- **格式：** `refactor/{优化对象}-{优化点}`
- **适用：** 代码结构重构、SQL 索引优化、公共服务改造、规范统一
- **示例：**

| 分支名 | 说明 |
|---|---|
| `refactor/common-notice-service` | 通知中心公共服务重构 |
| `refactor/oracle-sql-index` | Oracle 慢 SQL 索引优化 |
| `refactor/api-response-unify` | 接口返回格式统一改造 |

---

## 分支操作规范

```bash
# 从 develop 拉取最新代码
git checkout develop
git pull origin develop

# 创建功能分支
git checkout -b feature/模块名-功能描述

# 开发完成后提交
git add .
git commit -m "feat(模块名): 变更描述"

# 推送到远程
git push -u origin feature/模块名-功能描述

# 在 GitHub 创建 PR → 目标分支 develop
# PR 标题格式：feat(模块名): 变更描述
```

### 禁止事项

- ❌ 直接在 `master` 或 `develop` 分支上开发
- ❌ 分支名使用中文或空格
- ❌ 一个分支混合多种类型变更（如 feature + fix）
- ❌ 提交包含硬编码密码、密钥

---

## 连接参数速查

| 服务 | 地址 | 账号 / 密码 |
|---|---|---|
| Oracle PDB | `localhost:1521/DORMPDB` | `DORM_OPER` / `Dorm@2026` |
| 后端 Swagger | `http://localhost:5000/swagger` | — |
| 前端页面 | `http://localhost:3000` | — |
| DBeaver 连接 | Service Name: `DORMPDB` | `system` / `Dorm@123456` |