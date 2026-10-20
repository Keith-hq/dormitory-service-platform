# Oracle XE 21 本地容器环境启动说明

## 快速启动

```bash
# 1. 进入目录
cd deploy/oracle

# 2. 创建本地配置（首次使用）
cp .env.example .env
# 编辑 .env，填写 ORACLE_PASSWORD

# 3. 启动容器（首次启动需 3~8 分钟初始化）
docker compose up -d

# 4. 查看初始化进度
docker logs -f dorm-oracle-db

# 5. 看到以下日志表示就绪：
# Pluggable database DORMPDB opened read write
```

## 连接参数

| 参数 | 值 |
|---|---|
| **主机** | `localhost` |
| **端口** | `1521` |
| **SID** | `XE`（CDB 实例名） |
| **Service Name** | `DORMPDB`（PDB 服务名，推荐使用） |
| **SYSTEM 管理员** | `system` / 本地 `.env` 中的 `ORACLE_PASSWORD` |
| **DORM_OPER 业务账号** | 容器首次启动自动创建；密码与 `ORACLE_PASSWORD` 相同 |

## 常用命令

| 操作 | 命令 |
|---|---|
| 停止容器 | `docker compose down` |
| 重启容器 | `docker restart dorm-oracle-db` |
| 进入容器 | `docker exec -it dorm-oracle-db bash` |
| 查看日志 | `docker logs -f dorm-oracle-db` |
| 删除容器和数据 | `docker compose down -v` |

## 初始化业务表

容器只负责创建 Oracle 实例和 `DORM_OPER` 账号，业务表仍由仓库脚本维护。首次初始化请从仓库根目录执行：

```bash
cp deploy/docker/.env.example deploy/docker/.env
# 将 deploy/docker/.env 中的 ORACLE_PASSWORD 与本目录 .env 保持一致
bash deploy/scripts/init-db.sh
```

已有数据库只执行尚未应用的增量迁移：

```bash
bash deploy/scripts/migrate-db.sh
```

## 容器内连接 Oracle

```bash
docker exec -it dorm-oracle-db bash
sqlplus / as sysdba

-- 切换到 DORMPDB
ALTER SESSION SET CONTAINER = DORMPDB;

-- 如已执行初始化脚本，可验证 DORM_OPER 用户
SELECT username FROM dba_users WHERE username = 'DORM_OPER';
