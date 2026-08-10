# AGENTS.md

本文档面向项目中所有协作者的 AI 编程助手（Claude Code、Copilot、Cursor 等），定义**不可修改的区域**和**必须遵守的规范**。请各 AI Agent 在操作前完整阅读本文件。

---

## 一、受保护分支（禁止直接提交）

以下分支**只接受 PR 合并**，任何 Agent 不得直接在其上 commit 或 push：

| 分支 | 保护原因 |
|---|---|
| `master` | 生产分支，需 Review + CI 通过 |
| `develop` | 开发主分支，项目代码在此维护 |

违反后果：直接 push 会被 GitHub 分支保护规则拒绝；若本地未设保护，也会在 PR 阶段被驳回。

---

## 二、不可修改的文件与目录

以下路径为项目级公共资产，**Agent 不得修改其内容**，除非用户明确要求且仅限于该次请求：

### 2.1 根目录公共文件

| 路径 | 不可修改原因 |
|---|---|
| `README.md` | 项目总入口文档，含分支规范、操作规范、连接参数速查——由项目负责人统一维护 |
| `AGENTS.md` | 即本文件，AI 协作协议自身 |
| `.gitignore` | 全局忽略规则，已覆盖 .NET / Node / VS / VS Code / 环境变量——修改需团队共识 |

### 2.2 项目代码（backend/ frontend/）

业务代码**可以修改**，但须遵守以下约束：

| 路径 | 修改约束 |
|---|---|
| `backend/**` | ASP.NET Core 后端项目（TemplateDormApi）——修改时须确保与 `frontend/` 接口契约一致 |
| `frontend/**` | Vue 3 + Vite 前端项目——修改时须确保与 `backend/` 接口契约一致 |
| `docs/development/**` | 开发流程规范——项目代码变更后须同步更新相关规范 |
| `template/**` | 最初 M1 模板快照留档，**只读**，不参与开发与 CI，禁止修改 |

**前后端一致性规则：**

1. **接口契约联动：** 修改后端接口（Controller 路由、DTO 字段、响应结构）时，必须同步更新前端 `src/api/` 中对应的调用代码；反之，前端修改接口期望时，后端必须配套调整
2. **保持联调可用：** 任意一端修改后，须验证 `backend/` 和 `frontend/` 能正常联调，确保 Swagger 接口与前端请求对得上
3. **API 响应格式统一：** 后端统一返回 `{ code, message, data }` 结构，前端 `src/utils/request.js` 统一解析该结构——两端不得单方面偏离此约定
4. **Apifox 契约优先：** 接口变更应先更新 Apifox 文档，再同步修改前后端代码；Apifox 最新导出为契约唯一权威

> **新业务模块开发：** 直接在 `backend/`、`frontend/` 对应子目录中开发，不复制目录；`template/` 留档仅作历史参考。

### 2.3 CI/CD 与部署配置

| 路径 | 不可修改原因 |
|---|---|
| `.github/workflows/build-deploy.yml` | CI 流水线——修改直接影响全团队构建和部署 |
| `deploy/oracle/docker-compose.yml` | Oracle 容器编排——本地开发环境的基础设施定义 |
| `deploy/oracle/.env.example` | 环境变量模板——新增变量需同步通知全组 |

### 2.4 课程交付物

| 路径 | 不可修改原因 |
|---|---|
| `docs/deliverables/*.docx` | 课程验收材料，为二进制文件——仅项目负责人通过 Word 编辑 |
| `docs/project/数据库课程设计项目总纲.md` | 项目总纲，包含需求范围和技术方案 |
| `docs/project/任务看板.md` | 团队任务分配和进度跟踪 |

---

## 三、仓库目录结构与工程红线

### 3.1 目录结构速览

| 路径 | 职责 | Agent 约束 |
|---|---|---|
| `backend/` | 后端 ASP.NET Core 项目（TemplateDormApi），唯一后端开发位置，含测试 `backend/tests/` | 新模块直接在此工程内开发，不复制目录、不新建平行工程 |
| `frontend/` | 前端 Vue 3 + Vite 项目，唯一前端开发位置 | 同上 |
| `database/` | DDL（foundation / extension）、迁移脚本、存储过程、结构校验 | 结构变化用编号迁移脚本，不回写 foundation 基线 |
| `deploy/` | Oracle 容器与环境模板 | 变更需团队确认 |
| `docs/` | 正式文档（project / development / operations / deliverables） | 公共文档修改需评审 |
| `template/` | 最初 M1 模板快照留档 | **只读**，禁止修改、禁止复制使用 |
| `.github/workflows/` | CI 流水线 | 修改需团队确认 |

### 3.2 工程红线（不可违反）

1. **数据拥有者与事务边界：** 跨模块写入必须明确数据拥有者、接口入口和事务边界；模块不得直接修改其他模块或公共服务的表，只能通过接口进入。
2. **幂等与失败回滚：** 扣款、扣分、预约释放、库存变化和定时任务必须考虑幂等、重复执行和失败回滚，不能留下半成品状态。
3. **状态机：** 状态字段按状态机处理；后端校验合法转移、操作者和业务条件，**不信任前端传入的目标状态**。
4. **数据库变化：** 必须有可重建脚本、字段/约束说明、关系说明和受影响接口清单；手工改库不算完成交付。
5. **合并前证据：** 合并前必须留下与风险匹配的证据：构建、测试、接口联调、数据库结果或风险说明。
6. **结论区分：** 输出结论时区分"已验证事实""设计要求""待确认问题"和"个人建议"。
7. **契约权威：** Apifox 锁定定义（最新 OpenAPI 导出）为唯一契约权威，冲突时以 Apifox 为准。

完整总则见 [docs/development/00-项目规范总则.md](./docs/development/00-项目规范总则.md)，具体操作见 `docs/development/01~05` 对应规范。

---

## 四、分支命名强制规范

Agent 创建新分支时，**必须**遵循以下命名格式，不得自创前缀或使用中文/空格：

```
{前缀}/{模块名}-{功能简述}
```

| 前缀 | 用途 | 示例 |
|---|---|---|
| `feature/` | 新功能开发 | `feature/building-room-assets` |
| `fix/` | Bug 修复 | `fix/login-jwt-auth` |
| `ops/` | 运维/部署/环境 | `ops/nginx-reverse-proxy` |
| `docs/` | 文档 | `docs/api-contract-spec` |
| `refactor/` | 重构/优化 | `refactor/api-response-unify` |

**禁止事项：**
- ❌ 直接在 `master` 或 `develop` 上 commit
- ❌ 分支名使用中文、空格或特殊字符
- ❌ 一个分支混合多种类型变更（如 feature + fix + refactor 混在一起）
- ❌ 使用上述五个前缀以外的自定义前缀

---

## 五、Commit 信息规范

Agent 生成的 commit message 必须遵循 [Conventional Commits](https://www.conventionalcommits.org/) 格式：

```
{type}({scope}): {简短描述}
```

| type | 适用场景 |
|---|---|
| `feat` | 新功能 |
| `fix` | Bug 修复 |
| `docs` | 文档变更 |
| `refactor` | 重构（不改变功能） |
| `style` | 格式调整（空白、缩进等） |
| `test` | 测试相关 |
| `chore` | 构建/工具/依赖维护 |
| `ops` | 部署/运维 |

**示例：**
- `feat(building): 添加楼栋信息分页查询接口`
- `fix(auth): 修复 JWT token 过期未刷新问题`
- `docs(agent): 新增 AI 协作协议文件`

---

## 六、安全红线（绝对禁止）

Agent 在任何情况下都**不得**执行以下操作：

1. ❌ **硬编码密码、密钥、连接字符串**——敏感配置只能通过环境变量或 .NET User Secrets 注入
2. ❌ **将 `.env` 文件加入版本控制**——`.env` 已在 `.gitignore` 中排除，Agent 不得通过 `-f` 等方式强行提交
3. ❌ **修改或删除 `.gitignore`** 中的安全相关规则
4. ❌ **暴露 `deploy/oracle/.env.example` 中的实际密码**到日志、注释或文档中——当前示例密码仅供本地开发，云端部署必须更换
5. ❌ **降级依赖版本以绕过安全漏洞**——安全漏洞应升级依赖而非降级

---

## 七、操作边界

### 7.1 Agent 可以做

- 在自己的 feature/fix/docs 分支上自由创建、修改、删除文件
- 运行 `dotnet build`、`npm run dev` 等本地验证命令
- 生成符合项目规范的代码、测试和文档
- 在自己的分支上创建 commit 和 push

### 7.2 Agent 不可以做（除非用户明确指令）

- 向 `master` 或 `develop` 直接 push
- 修改 `.github/workflows/` 下的 CI 配置
- 修改 `deploy/oracle/` 下的 Docker 或环境配置
- 修改根目录 `README.md`、`AGENTS.md`、`.gitignore`
- `git push --force` 到任何远程分支
- 合并 PR（需人工 Review）

### 7.3 需要人工确认的操作

以下操作 Agent 在用户提出时可以执行，但执行前**必须明确告知影响范围**并获得确认：

- 修改 `docs/project/` 或 `docs/operations/` 中的公共文档
- `git rebase`、`git reset --hard` 等改写历史的操作
- 删除远程分支
- 修改 `package.json`、`package-lock.json` 或其他锁定文件

---

## 八、项目代码修改规则

### 8.1 修改项目代码

`backend/`、`frontend/` 是全体成员的开发位置，**允许直接修改**，但修改者必须承担前后端一致性责任：

1. **同步检查：** 修改 `backend/` 的接口签名（Controller 路由、DTO 字段、`ApiResponse` 结构）时，必须检查 `frontend/src/api/` 和对应视图是否受影响，**在同一 PR 内完成前后端配套修改**
2. **验证联调：** 修改完成后，启动两端验证接口能正常调用、数据能正确渲染，不得让项目处于"后端接口改了但前端还调不通"的状态
3. **更新规范：** 若修改涉及开发流程、目录结构或启动方式的变化，同步更新 `docs/development/` 中对应的规范文档
4. **分离关注点：** 结构性改造（如新增中间件、调整分层架构）放在独立 PR 中，不与业务功能混合

### 8.2 新模块开发位置

1. 业务代码一律写在 `backend/`、`frontend/` 现有工程内，不复制目录、不新建平行工程
2. `template/` 留档仅供历史参考，不得从其中复制代码参与开发

---

## 九、个人工作区约定

- 个人工作记录、未确认方案草稿、个人职责清单保存在仓库**外部**的 `.agent/` 工作区，不进入公共仓库
- Agent 不应将调试日志、临时文件、个人笔记提交到仓库中

---

> **协议更新：** 本文件修改需通过 PR 合并到 `develop`。
