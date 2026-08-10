# dormitory-service-platform

高校宿舍后勤与共享生活服务平台｜数据库课程设计项目

**技术栈：** Vue3 + Vite + Pinia + ASP.NET Core .NET8 C# + Oracle + EF Core + Docker Compose + Nginx

**功能覆盖：** 宿舍资产、住宿管理、水电分摊、共享借用、访客二维码、维修 SLA 派单、退宿清算等高并发业务；内置标准化前后端项目、Oracle 容器一键环境、CI/CD 自动部署、Apifox 统一接口契约。

后端、前端业务代码与数据库脚本分别维护在 `backend/`、`frontend/`、`database/`；最初 M1 模板快照留档在 `template/` 仅作历史参考；开发流程规范统一维护在 `docs/development/`。

---

## 根目录结构

| 路径 | 内容与职责 |
|---|---|
| `.github/workflows/build-deploy.yml` | CI 工作流：编译后端项目、构建前端项目并上传构建产物 |
| `backend/` | 后端 ASP.NET Core .NET 8 项目（TemplateDormApi），含业务代码与测试（`backend/tests/`） |
| `frontend/` | 前端 Vue 3 + Vite + Pinia 项目，业务页面与通用组件 |
| `database/` | 数据库脚本目录：DDL、迁移、存储过程和结构校验 |
| `deploy/` | 部署与本地运行环境文件，目前包含 Oracle XE 容器配置 |
| `docs/` | 项目总纲、任务看板、开发规范、运维说明和课程交付材料 |
| `template/` | 最初 M1 模板快照留档（`template/backend/`、`template/frontend/`），仅作历史参考，不参与开发与 CI |
| `.gitignore` | 忽略本地环境文件、依赖、构建产物和敏感配置 |
| `README.md` | 仓库结构、使用入口、分支和提交约定 |

## 项目代码

### `backend/`

后端项目按以下职责组织：

- `Controllers/`：HTTP 接口入口。
- `Services/`：业务服务和业务规则。
- `Repository/`：数据访问封装。
- `Models/`：Oracle 实体模型。
- `DTO/`：接口入参和出参模型。
- `Data/`：EF Core `DbContext`。
- `Program.cs`：依赖注入和应用启动配置。

调用方向遵循 `Controller -> Service -> Repository -> Database`。连接字符串只使用 .NET User Secrets 或本机环境变量，不提交密码。

### `frontend/`

前端项目包含：

- `src/api/`：接口调用封装。
- `src/components/`：通用展示和交互组件。
- `src/views/`：页面视图。
- `src/router/`：路由。
- `src/store/`：跨页面状态。
- `src/utils/request.js`：统一 HTTP 请求处理。
- `package.json`、`package-lock.json`：依赖和锁定版本。

### `docs/development/`（开发规范）

- `00-项目规范总则.md`（仓库目录结构、工程红线、契约权威与变更流程、完成定义）
- `01-后端开发规范.md`
- `02-前端开发规范.md`
- `03-Apifox接口联调规范.md`
- `04-本地完整联调步骤.md`
- `05-前端构建与发布规范.md`

规范统一以根目录的 `backend/`、`frontend/` 和 `deploy/oracle/` 为示例路径。

### `template/`（留档）

最初 M1 模板快照（`template/backend/`、`template/frontend/`），与业务代码无关联，仅作历史参考；需要起步参考时可对照，但开发一律在 `backend/`、`frontend/` 中进行。

## 文档目录

| 路径 | 内容 |
|---|---|
| `docs/project/` | `数据库课程设计项目总纲.md`、`任务看板.md`、难点代码文档 |
| `docs/development/` | 项目规范总则（00）、后端/前端开发规范、Apifox 接口联调规范、本地联调步骤、前端构建与发布规范 |
| `docs/operations/` | `环境排错手册.md`、`组员接入指南.md` |
| `docs/deliverables/` | 课程验收和环境交付材料，包括两份 `.docx` 文件 |
| `docs/README.md` | 文档目录说明 |

个人工作记录、未确认方案和职责清单保存在仓库外的 `.agent/` 工作区，不进入公共仓库。

## 本地 Oracle 环境

运维文件位于 `deploy/oracle/`：

- `.env.example`：本地配置模板；复制为 `.env` 后填写 `ORACLE_PASSWORD`，`.env` 不得提交。
- `docker-compose.yml`：Oracle XE 21 容器配置。
- `README-环境启动.md`：启动、连接和常用命令说明。

从仓库根目录启动：

```bash
cd deploy/oracle
copy .env.example .env
# 编辑 .env，填写本机 ORACLE_PASSWORD
docker compose up -d
```

看到 `Pluggable database DORMPDB opened read write` 后，再启动后端和前端：

> 🔐 **启动后端前必须配置 User Secrets**
>
> 本项目使用 .NET User Secrets 管理敏感信息（数据库连接串、JWT Key），**请勿**将凭据写入 `appsettings.json`。
>
> 在 Visual Studio 中右键点击 `backend` 项目 → “管理用户机密”，将以下内容粘贴到 `secrets.json` 中（根据实际环境修改 `Data Source` 和 `Password`）：
>
> ```json
> {
>   "ConnectionStrings": {
>     "OracleConnection": "User Id=DORM_OPER;Password=你的密码;Data Source=localhost:1521/DORMPDB;"
>   },
>   "Jwt": {
>     "Key": "至少32位的随机字符串（如：YourSuperLongSecretKeyAtLeast32CharactersLong!）",
>     "Issuer": "DormitoryPlatform",
>     "Audience": "DormitoryClient"
>   }
> }
> ```
>
> 保存后，再执行 `dotnet run` 启动后端。若未配置，项目启动时会抛出异常提示。

```bash
cd backend
dotnet restore
dotnet run
```

```bash
cd frontend
npm ci
npm run dev
```

默认入口：后端 Swagger 为 `http://localhost:5000/swagger`，前端为 `http://localhost:3000`。

## 分支命名规范

> **统一前缀 + 描述，小写英文，横线分隔。**

标准分支命名格式：`{前缀}/{模块名}-{功能简述}`

### 受保护分支

| 分支 | 说明 |
|---|---|
| `master` | 生产分支，只接受 PR 合并，需 Review + CI 通过 |
| `develop` | 开发主分支，项目代码在此维护 |

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
- **适用：** 需求文档、数据库设计、开发规范、部署报告、PPT 素材
- **示例：**

| 分支名 | 说明 |
|---|---|
| `docs/dev-guide` | 开发规范文档 |
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
| Oracle PDB | `localhost:1521/DORMPDB` | `DORM_OPER`，密码以本地 User Secrets / 初始化脚本说明为准 |
| 后端 Swagger | `http://localhost:5000/swagger` | — |
| 前端页面 | `http://localhost:3000` | — |
| DBeaver 连接 | Service Name: `DORMPDB` | `system`，密码以本地 `.env` 的 `ORACLE_PASSWORD` 为准 |

---

## CI

`.github/workflows/build-deploy.yml` 会在面向 `master` 或 `develop` 的 PR，以及 `develop` 的推送事件中检查两个项目：后端执行 `dotnet restore/build/publish`，前端执行 `npm ci` 和 `npm run check`。云端部署部分按运维阶段计划启用。
