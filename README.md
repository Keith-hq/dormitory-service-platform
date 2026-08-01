# dormitory-service-platform

高校宿舍后勤与共享生活服务平台｜数据库课程设计项目

**技术栈：** Vue3 + Vite + Pinia + ASP.NET Core .NET8 C# + Oracle 26AI + EF Core + Docker Compose + Nginx

**功能覆盖：** 宿舍资产、住宿管理、水电分摊、共享借用、访客二维码、维修 SLA 派单、退宿清算等高并发业务；内置标准化前后端样板间、Oracle 容器一键环境、CI/CD 自动部署、Apifox 统一接口契约。

---

## 临时分支命名规则

统一前缀 + 描述，小写英文，横线分隔。

### 1. 功能开发分支（模块组员、难点负责人使用）

- **前缀：** `feature/`
- **格式：** `feature/模块名-功能简述`
- **适用：** 新业务功能、页面、接口、难点业务逻辑开发
- **示例：**
  - `feature/building-room-assets` — 楼栋房间资产模块
  - `feature/shared-goods-borrow` — 共享物品借用归还
  - `feature/utility-bill-calc` — 水电费分摊难点功能
  - `feature/checkout-settle` — 退宿清算核心逻辑

### 2. Bug 修复分支（全员测试后修复使用）

- **前缀：** `fix/`
- **格式：** `fix/模块-问题简述`
- **适用：** 联调、集成测试发现的逻辑报错、页面 bug、SQL 异常
- **示例：**
  - `fix/login-jwt-auth` — 登录鉴权失效修复
  - `fix/oracle-conn-timeout` — Oracle 数据库连接超时修复
  - `fix/credit-score-deduct` — 信用分扣减事务 bug

### 3. 运维 / 环境部署分支（全栈运维专用）

- **前缀：** `ops/`
- **格式：** `ops/操作类型-目标`
- **适用：** Docker 配置、Nginx、CI/CD、云端部署、环境脚本调整
- **示例：**
  - `ops/local-oracle-compose` — Oracle26AI 容器环境配置
  - `ops/nginx-reverse-proxy` — 云端 Nginx 反向代理配置
  - `ops/cicd-auto-deploy` — 自动化部署流水线脚本

### 4. 文档分支（全员补充设计、说明文档）

- **前缀：** `docs/`
- **格式：** `docs/文档类型-内容`
- **适用：** 需求文档、数据库设计、样板间教程、部署报告、PPT 素材
- **示例：**
  - `docs/template-dev-guide` — 样板间开发教程
  - `docs/deploy-check-report` — 云端部署验证报告
  - `docs/m1-acceptance-file` — M1 里程碑验收材料

### 5. 重构 / 优化分支（架构师、后端核心使用）

- **前缀：** `refactor/`
- **格式：** `refactor/优化对象-优化点`
- **适用：** 代码结构重构、SQL 索引优化、公共服务改造、规范统一
- **示例：**
  - `refactor/common-notice-service` — 通知中心公共服务重构
  - `refactor/oracle-sql-index` — Oracle 慢 SQL 索引优化
