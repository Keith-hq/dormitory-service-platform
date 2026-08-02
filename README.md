# dormitory-service-platform

高校宿舍后勤与共享生活服务平台，数据库课程设计项目。

当前仓库先维护可复用的前后端样板、Oracle 本地环境、项目文档和 CI；`backend/`、`frontend/`、`database/` 是后续业务实现的根目录约定，当前保持为空。

## 根目录结构

| 路径 | 内容与职责 |
|---|---|
| `.github/workflows/build-deploy.yml` | CI 工作流：编译后端样板、构建前端样板并上传构建产物 |
| `backend/` | 后端业务实现目录，当前为空，后续按项目规范放置实际业务代码 |
| `database/` | 数据库脚本目录，当前为空，后续放置 DDL、存储过程、视图和初始化数据 |
| `deploy/` | 部署与本地运行环境文件，目前包含 Oracle XE 容器配置 |
| `docs/` | 项目总纲、任务看板、运维说明和课程交付材料 |
| `frontend/` | 前端业务实现目录，当前为空，后续放置实际业务页面和测试 |
| `template-backend/` | ASP.NET Core .NET 8 后端样板，可复制后作为业务模块起点 |
| `template-frontend/` | Vue 3 + Vite + Pinia 前端样板，可复制后作为业务页面起点 |
| `template-demo-guide/` | 4 份样板开发、接口联调和本地联调教程 |
| `.gitignore` | 忽略本地环境文件、依赖、构建产物和敏感配置 |
| `README.md` | 仓库结构、使用入口、分支和提交约定 |

空目录不会被 Git 单独记录，因此远程仓库可能不显示 `backend/`、`frontend/`、`database/` 的目录项；目录职责以本说明为准。业务代码落地前，不要把模板内容直接恢复到这三个目录。

## 模板内容

### `template-backend/`

后端样板按以下职责组织：

- `Controllers/`：HTTP 接口入口。
- `Services/`：业务服务和业务规则。
- `Repository/`：数据访问封装。
- `Models/`：Oracle 实体模型。
- `DTO/`：接口入参和出参模型。
- `Data/`：EF Core `DbContext`。
- `Program.cs`：依赖注入和应用启动配置。

调用方向遵循 `Controller -> Service -> Repository -> Database`。连接字符串只使用 .NET User Secrets 或本机环境变量，不提交密码。

### `template-frontend/`

前端样板包含：

- `src/api/`：接口调用封装。
- `src/components/`：通用展示和交互组件。
- `src/views/`：页面视图。
- `src/router/`：路由。
- `src/store/`：跨页面状态。
- `src/utils/request.js`：统一 HTTP 请求处理。
- `package.json`、`package-lock.json`：依赖和锁定版本。

### `template-demo-guide/`

- `01-后端样板开发流程.md`
- `02-前端样板开发流程.md`
- `03-Apifox接口联调规范.md`
- `04-本地完整联调步骤.md`

教程统一以根目录的 `template-backend/`、`template-frontend/` 和 `deploy/oracle/` 为示例路径。

## 文档目录

| 路径 | 内容 |
|---|---|
| `docs/project/` | `数据库课程设计项目总纲.md`、`任务看板.md` |
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

看到 `Pluggable database DORMPDB opened read write` 后，再启动样板后端和前端：

```bash
cd template-backend
dotnet restore
dotnet run
```

```bash
cd template-frontend
npm ci
npm run dev
```

默认入口：后端 Swagger 为 `http://localhost:5000/swagger`，前端为 `http://localhost:3000`。

## 技术栈

Vue 3、Vite、Pinia、ASP.NET Core .NET 8、C#、Oracle XE 21、EF Core、Docker Compose、Nginx、Apifox。

## 分支与提交约定

分支从最新 `develop` 创建，名称使用小写英文和横线：

| 前缀 | 用途 | 示例 |
|---|---|---|
| `feature/` | 新功能 | `feature/building-room-assets` |
| `fix/` | 缺陷修复 | `fix/oracle-conn-timeout` |
| `ops/` | 运维和部署 | `ops/local-oracle-compose` |
| `docs/` | 文档变更 | `docs/api-contract-spec` |
| `refactor/` | 结构或质量重构 | `refactor/repository-structure-layout` |

`master` 是发布基线，`develop` 是开发集成分支；禁止直接在这两个分支上开发，功能完成后通过 PR 合并。

提交信息使用英文类型前缀，详细说明可以使用中文，例如：

```text
refactor(repo): 规范化仓库目录结构
```

不要使用中文提交类型前缀，也不要在提交中包含密码、令牌、真实个人信息、`.env` 或构建产物。

## CI

`.github/workflows/build-deploy.yml` 会在面向 `master` 或 `develop` 的 PR，以及 `develop` 的推送事件中检查两个模板项目：后端执行 `dotnet restore/build/publish`，前端执行 `npm ci` 和 `npm run build`。云端部署部分按运维阶段计划启用。
