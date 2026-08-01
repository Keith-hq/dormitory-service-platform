# Oracle 26AI 容器环境启动说明

## 快速启动

```bash
# 1. 进入目录
cd ops-local-oracle

# 2. 启动容器（首次启动需 3~8 分钟初始化）
docker compose up -d

# 3. 查看初始化进度
docker logs -f dorm-oracle-db

# 4. 看到以下日志表示就绪：
# Pluggable database DORMPDB opened read write
```

## 连接参数

| 参数 | 值 |
|---|---|
| **主机** | `localhost` |
| **端口** | `1521` |
| **SID** | `XE`（CDB 实例名） |
| **Service Name** | `DORMPDB`（PDB 服务名，推荐使用） |
| **SYSTEM 管理员** | `system` / `Dorm@123456` |
| **DORM_OPER 业务账号** | `DORM_OPER` / `Dorm@2026` |

## 常用命令

| 操作 | 命令 |
|---|---|
| 停止容器 | `docker compose down` |
| 重启容器 | `docker restart dorm-oracle-db` |
| 进入容器 | `docker exec -it dorm-oracle-db bash` |
| 查看日志 | `docker logs -f dorm-oracle-db` |
| 删除容器和数据 | `docker compose down -v` |

## 容器内连接 Oracle

```bash
docker exec -it dorm-oracle-db bash
sqlplus / as sysdba

-- 切换到 DORMPDB
ALTER SESSION SET CONTAINER = DORMPDB;

-- 验证 DORM_OPER 用户
SELECT username FROM dba_users WHERE username = 'DORM_OPER';
```
