<div align="center">

<img src="https://capsule-render.vercel.app/api?type=waving&color=0:1E293B,100:7C3AED&height=200&section=header&text=Dormitory%20Platform&fontSize=54&fontColor=ffffff&fontAlignY=34&desc=University%20Housing%20%C2%B7%20Shared%20Living%20%C2%B7%20Full%20Lifecycle&descSize=16&descAlignY=58" width="100%" alt="Dormitory Service Platform"/>

<h1>高校宿舍后勤与共享生活服务平台</h1>

<p>
<b>住宿 · 资产 · 账单 · 维修 · 共享物品 · 访客 · 宿舍治理</b><br/>
把散落在纸质台账里的宿舍运营，收进一套状态可审计的服务里
</p>

<p>
  <img src="https://img.shields.io/badge/状态-源码完整_线上已下线-f59e0b?style=for-the-badge" alt="status"/>
  <img src="https://img.shields.io/badge/后端-ASP.NET_Core_8-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" alt=".NET"/>
  <img src="https://img.shields.io/badge/前端-Vue_3-42B883?style=for-the-badge&logo=vuedotjs&logoColor=white" alt="Vue"/>
  <img src="https://img.shields.io/badge/数据库-Oracle_21c-F80000?style=for-the-badge&logo=oracle&logoColor=white" alt="Oracle"/>
</p>

<p>
  <a href="#-功能">功能</a> ·
  <a href="#-技术栈">技术栈</a> ·
  <a href="#-仓库结构">仓库结构</a> ·
  <a href="#-本地开发">本地开发</a> ·
  <a href="#-部署">部署</a> ·
  <a href="#-ci--cd">CI/CD</a>
</p>

</div>

> [!WARNING]
> **线上状态：原云服务器已到期，旧网站目前不可访问。**
> 本仓库保留完整源码和部署资产；重新上线需要准备一台新服务器，并按下方步骤配置环境。
> README 不提供失效的线上地址或在线状态徽章。

---

## ✨ 功能

| 模块 | 能力 |
|:--|:--|
| 🛏️ **住宿管理** | 楼栋、房间、床位分配、入住与退宿清算 |
| 💳 **费用与钱包** | 水电分摊、账单发布、缴费、充值、断电与恢复 |
| 🧰 **资产与维修** | 资产台账、报修、附件、维修派单与 SLA 升级 |
| 🤝 **共享生活** | 共享物品借用归还、设施预约、保洁申请 |
| 🛡️ **宿舍治理** | 访客二维码、门岗登记、晚归、卫生、违规与投票 |
| 🔔 **平台服务** | JWT 鉴权、通知、审计、定时任务、文件存储与健康检查 |

---

## 🛠️ 技术栈

<h3 align="center">前端</h3>

<p align="center">
  <img src="https://img.shields.io/badge/Vue-3-42B883?style=for-the-badge&logo=vuedotjs&logoColor=white" alt="Vue"/>
  <img src="https://img.shields.io/badge/Vite-8-646CFF?style=for-the-badge&logo=vite&logoColor=white" alt="Vite"/>
  <img src="https://img.shields.io/badge/Pinia-状态管理-FFD859?style=for-the-badge&logo=pinia&logoColor=black" alt="Pinia"/>
  <img src="https://img.shields.io/badge/Nginx-反向代理-009639?style=for-the-badge&logo=nginx&logoColor=white" alt="Nginx"/>
</p>

<h3 align="center">后端与数据</h3>

<p align="center">
  <img src="https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" alt=".NET"/>
  <img src="https://img.shields.io/badge/EF_Core-ORM-68217A?style=for-the-badge&logo=dotnet&logoColor=white" alt="EF Core"/>
  <img src="https://img.shields.io/badge/Quartz.NET-定时任务-512BD4?style=for-the-badge" alt="Quartz"/>
  <img src="https://img.shields.io/badge/Oracle-XE_21-F80000?style=for-the-badge&logo=oracle&logoColor=white" alt="Oracle"/>
</p>

<h3 align="center">交付</h3>

<p align="center">
  <img src="https://img.shields.io/badge/Docker-Compose-2496ED?style=for-the-badge&logo=docker&logoColor=white" alt="Docker"/>
  <img src="https://img.shields.io/badge/GHCR-镜像仓库-181717?style=for-the-badge&logo=github&logoColor=white" alt="GHCR"/>
  <img src="https://img.shields.io/badge/GitHub_Actions-流水线-2088FF?style=for-the-badge&logo=githubactions&logoColor=white" alt="Actions"/>
</p>

---

## 📁 仓库结构

```
backend/                         ASP.NET Core Web API 与 xUnit 测试
frontend/                        Vue 3 前端、Mock 适配器与前端测试
database/                        DDL、增量迁移、存储过程、种子与校验脚本
deploy/oracle/                   本地 Oracle XE 容器
deploy/docker/                   生产 Compose、Dockerfile 与环境模板
deploy/nginx/                    宿主机 Nginx 配置与证书目录
deploy/scripts/                  初始化、迁移、部署、备份和证书脚本
docs/                            项目、开发规范和交付材料
.github/workflows/               构建与 GHCR 镜像发布流水线
```

历史模板和未引用的脚手架资源已从仓库移除；日常开发只在 `backend/`、`frontend/` 和 `database/` 中进行。

---

## 🧰 本地开发

### 环境要求

- Docker Engine 和 Compose v2
- .NET SDK 8.0
- Node.js `20.19.0`（以 `frontend/.nvmrc` 为准）和 npm 10+
- 至少 5 GB 可用内存（Oracle XE 建议为 Docker 分配更多）

### 1. 启动 Oracle

```bash
cd deploy/oracle
cp .env.example .env
# 编辑 .env，设置强密码
docker compose up -d
docker logs -f dorm-oracle-db
```

看到 `Pluggable database DORMPDB opened read write` 后，容器会提供 `DORMPDB` 和 `DORM_OPER` 账号。首次创建业务表，从仓库根目录执行：

```bash
cp deploy/docker/.env.example deploy/docker/.env
# 让 deploy/docker/.env 的 ORACLE_PASSWORD 与 deploy/oracle/.env 一致
bash deploy/scripts/init-db.sh
```

> Windows PowerShell 可用 `Copy-Item .env.example .env`。数据库脚本的完整顺序见 [database/README.md](database/README.md)。

### 2. 配置后端密钥

**不要把密码或密钥写入 `appsettings.json`**，使用 .NET User Secrets：

```bash
cd backend
dotnet user-secrets init
dotnet user-secrets set 'ConnectionStrings:OracleConnection' 'User Id=DORM_OPER;Password=你的密码;Data Source=localhost:1521/DORMPDB;'
dotnet user-secrets set 'Jwt:Key' '至少32位的随机字符串'
dotnet user-secrets set 'Jwt:Issuer' 'DormitoryPlatform'
dotnet user-secrets set 'Jwt:Audience' 'DormitoryClient'
dotnet run
```

- Swagger：`http://localhost:5000/swagger`
- 健康检查：`http://localhost:5000/health`

### 3. 启动前端

```bash
cd frontend
npm ci
npm run dev
```

默认地址 `http://localhost:3000`，Vite 将 `/api` 代理到 `http://localhost:5000`。
需要离线演示时，在未提交的 `.env.local` 中设置 `VITE_USE_MOCK=true`；Mock 只在开发服务器生效，生产构建会强制关闭。

```bash
npm run check                    # lint + format:check + production build
node --test tests/*.test.js
```

---

## 🚀 部署

原服务器已过期，以下流程用于在新的 Ubuntu 22.04/24.04 amd64 服务器上重新上线。
建议内存至少 5 GB，开放 SSH `22`、HTTP `80`、HTTPS `443`。生产 Compose 会把 Oracle `1521` 和 API `5000` 绑定到服务器本机，避免绕过 UFW 暴露到公网。

### 1. 初始化服务器

```bash
git clone --branch develop https://github.com/Keith-hq/dormitory-service-platform.git /tmp/dormitory-service-platform
cd /tmp/dormitory-service-platform
sudo bash deploy/scripts/setup-server.sh
```

脚本会安装 Docker/Compose、配置 2 GB swap 和 UFW，并创建 `/opt/dorm-platform`。把生产部署所需文件复制过去：

```bash
sudo cp -a deploy/docker deploy/nginx deploy/scripts database /opt/dorm-platform/
cd /opt/dorm-platform
```

### 2. 写入生产配置

```bash
cp docker/.env.example docker/.env
chmod 600 docker/.env
```

| 变量 | 说明 |
|:--|:--|
| `ORACLE_PASSWORD` | Oracle 管理员和 `DORM_OPER` 密码 |
| `JWT_KEY` | 至少 32 位的 JWT 签名密钥 |
| `SERVICE_KEY` | 内部服务调用密钥 |
| `DOCKER_REGISTRY` | 默认 `ghcr.io/keith-hq`，可替换为自己的镜像仓库 |
| `API_TAG` / `WEB_TAG` | 镜像标签，默认 `latest` |

> `.env` 只保存在服务器，**禁止提交到 Git 或打印到 CI 日志**。

### 3. 配置 HTTPS

```bash
bash scripts/ssl-gen.sh <服务器公网IP>
```

生成有效期 365 天的自签名证书，浏览器会提示不受信。正式域名上线时，应替换为受信任 CA 签发的 `nginx/certs/dorm.crt` 与 `dorm.key`，并确认 DNS 已指向新服务器。

### 4. 首次启动与后续更新

```bash
# 首次：启动 Oracle、执行全量建库和增量迁移，再启动应用容器
bash scripts/deploy.sh --pull-images --init-db

# 后续：只拉新镜像并执行尚未应用的迁移
bash scripts/deploy.sh --pull-images
```

若使用自建镜像，在包含完整 `backend/`、`frontend/` 源码的 checkout 根目录执行 `bash deploy/scripts/build-images.sh`，推送后再改服务器 `docker/.env` 中的仓库与标签。生产服务器只需保留部署文件和镜像，**不需要复制源码**。

### 5. 验证、日志与备份

```bash
docker compose --env-file docker/.env -f docker/docker-compose.yml ps
docker compose --env-file docker/.env -f docker/docker-compose.yml logs -f dorm-api
curl http://127.0.0.1:5000/health
curl -k https://<服务器公网IP>/
bash scripts/backup.sh
```

确认 API、前端首页和 Oracle healthcheck 均正常后再切换 DNS。建议每天 03:00 用 cron 执行 `scripts/backup.sh`，并将备份复制到独立存储。
生产 Swagger 默认可访问 `/swagger/`，正式公网环境应在 Nginx 或网络层限制访问。

### 数据库迁移纪律

- 新表、字段、索引、序列或触发器**必须新增** `database/ddl/extensions/NNN_*.sql`，不能修改或删除已应用的迁移
- 存储过程统一放在 `database/sp/`，使用 `CREATE OR REPLACE`
- 迁移失败时 `deploy.sh` 会停在迁移阶段，避免替换仍可用的应用容器

---

## 🤖 CI / CD

`.github/workflows/build-deploy.yml` 当前做三件事：

| 触发 | 行为 |
|:--|:--|
| PR 到 `master` 或 `develop` | 还原并编译 .NET、前端 lint / 格式检查 / 生产构建、构建镜像验证，**不推送** |
| push 到 `develop` | 上述检查通过后，将 `dorm-api` 与 `dorm-web` 推送到 GHCR |
| 上线 | **不会自动 SSH 上线**——服务器已过期，生产上线按上方步骤手工执行 |

---

## 🌿 分支与提交

日常开发建议从 `develop` 创建 `feature/`、`fix/`、`ops/`、`docs/` 或 `refactor/` 分支，经 PR 合并。
提交信息遵循 Conventional Commits：

```text
docs(readme): 补充新服务器部署流程
refactor(repo): 清理历史模板与脚手架资源
```

当前维护分支：`develop`；生产分支：`master`。

---

## 📚 进一步阅读

- [frontend/README.md](frontend/README.md)：前端环境变量、Mock 和构建说明
- [database/README.md](database/README.md)：DDL、迁移、存储过程和种子数据顺序
- [deploy/oracle/README-环境启动.md](deploy/oracle/README-环境启动.md)：本地 Oracle 操作
- [docs/development/04-本地完整联调步骤.md](docs/development/04-本地完整联调步骤.md)：本地全链路联调
- [docs/development/05-前端构建与发布规范.md](docs/development/05-前端构建与发布规范.md)：前端构建和发布约定

---

<div align="center">

<sub>服务器会过期，源码和部署脚本不会。按 README 走一遍就能重新上线。</sub>

</div>
