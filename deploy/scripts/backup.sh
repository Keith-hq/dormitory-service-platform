#!/bin/bash
# ============================================================
# 数据库备份脚本
# 用法：bash backup.sh
# 建议：crontab 每日凌晨执行
#   0 3 * * * /opt/dorm-platform/deploy/scripts/backup.sh >> /var/log/dorm-backup.log 2>&1
#
# 行为：
#   - expdp 导出到容器内 DATA_PUMP_DIR，随后 docker cp 到宿主机
#     $BACKUP_DIR（持久目录），校验非零字节后才报告成功
#   - 任一环节失败（expdp 非零 / 产物缺失 / 零字节）→ 脚本非零退出
# ============================================================

set -euo pipefail

# 备份目录/容器名可用环境变量覆盖（本地验证用）
#   BACKUP_DIR_OVERRIDE  宿主机备份目录（默认 /opt/dorm-platform/backup）
#   DB_CONTAINER         Oracle 容器名（默认 dorm-oracle-db）
#   API_CONTAINER        后端容器名（默认 dorm-api）
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

BACKUP_DIR="${BACKUP_DIR_OVERRIDE:-/opt/dorm-platform/backup}"
CONTAINER="${DB_CONTAINER:-dorm-oracle-db}"
# 服务名参数化（旧容器可能注册为小写 dormpdb）
DB_SERVICE="${DB_SERVICE:-DORMPDB}"
RETENTION_DAYS=7
TIMESTAMP=$(date '+%Y%m%d_%H%M%S')

mkdir -p "$BACKUP_DIR"

echo "=========================================="
echo "  Dormitory Platform - 数据备份"
echo "  时间: $(date '+%Y-%m-%d %H:%M:%S')"
echo "=========================================="

# ---- 前置检查 ----
# 注意：不 source docker/.env —— .env 中的密钥可能含 shell 特殊字符
# （! @ # $ % ^ & * 等），source 会破坏脚本解析。密码用 grep 提取，
# 连接一律用 sqlplus CONNECT 命令 / expdp 引号 userid（@ 安全）。
if [ ! -f "$SCRIPT_DIR/../docker/.env" ]; then
    echo "❌ docker/.env 不存在，无法获取 ORACLE_PASSWORD"
    exit 1
fi
DB_PASSWORD=$(grep -E '^ORACLE_PASSWORD=' "$SCRIPT_DIR/../docker/.env" | head -1 | cut -d= -f2-)
DB_PASSWORD="${DB_PASSWORD%$'\r'}"
if [ -z "$DB_PASSWORD" ]; then
    echo "❌ docker/.env 中未找到 ORACLE_PASSWORD"
    exit 1
fi

# 就绪探测用 sysdba OS 认证（不依赖监听服务名/healthcheck.sh）
if ! echo "SELECT 1 FROM dual; EXIT;" | \
    docker exec -i "$CONTAINER" bash -c 'sqlplus -S -L / as sysdba' >/dev/null 2>&1; then
    echo "❌ Oracle 容器未就绪，备份中止"
    exit 1
fi

# ---- 1. 查询容器内 DATA_PUMP_DIR 实际路径 ----
echo "[1/4] 定位 DATA_PUMP_DIR..."
DP_DIR=$(docker exec -i "$CONTAINER" bash -c 'sqlplus -S -L /nolog' <<EOSQL | tr -d '[:space:]' | tail -1
CONNECT SYSTEM/"$DB_PASSWORD"@localhost:1521/$DB_SERVICE
SET HEADING OFF FEEDBACK OFF PAGESIZE 0
SELECT directory_path FROM dba_directories WHERE directory_name = 'DATA_PUMP_DIR';
EXIT;
EOSQL
)
if [ -z "$DP_DIR" ] || [[ "$DP_DIR" == ORA-* ]]; then
    echo "❌ 无法查询 DATA_PUMP_DIR（返回: $DP_DIR）"
    exit 1
fi
echo "  ✓ DATA_PUMP_DIR = $DP_DIR"

# ---- 2. Oracle 数据泵导出（容器内）----
echo "[2/4] Oracle 数据泵导出..."
DUMP_NAME="dormdb_${TIMESTAMP}"
LOG_NAME="dormdb_${TIMESTAMP}.log"

# userid 用单引号包裹、密码保留双引号：expdp 与 sqlplus 相同，
# 含 @ 的密码必须带引号，否则 userid 被按 @ 错误分割
if ! docker exec "$CONTAINER" bash -c \
    "expdp 'DORM_OPER/\"$DB_PASSWORD\"@localhost:1521/$DB_SERVICE' DIRECTORY=DATA_PUMP_DIR DUMPFILE=\"$DUMP_NAME.dmp\" LOGFILE=\"$LOG_NAME\" SCHEMAS=DORM_OPER"; then
    echo "❌ expdp 导出失败（退出码非零）"
    exit 1
fi
echo "  ✓ 导出完成"

# ---- 3. 拷贝产物到宿主机持久目录 ----
echo "[3/4] 拷贝产物到宿主机 $BACKUP_DIR ..."
docker cp "$CONTAINER:${DP_DIR}/${DUMP_NAME}.dmp" "$BACKUP_DIR/${DUMP_NAME}.dmp"
docker cp "$CONTAINER:${DP_DIR}/${LOG_NAME}" "$BACKUP_DIR/${LOG_NAME}"

# 校验：文件存在且非零字节
if [ ! -s "$BACKUP_DIR/${DUMP_NAME}.dmp" ]; then
    echo "❌ 备份文件缺失或为零字节: $BACKUP_DIR/${DUMP_NAME}.dmp"
    exit 1
fi
DMP_SIZE=$(du -h "$BACKUP_DIR/${DUMP_NAME}.dmp" | cut -f1)
echo "  ✓ 备份落盘: ${DUMP_NAME}.dmp（$DMP_SIZE）"

# 基本可恢复性检查：log 中必须出现成功标记
if ! grep -qE "successfully completed|Job .* successfully completed" "$BACKUP_DIR/${LOG_NAME}"; then
    echo "❌ expdp 日志未出现成功标记，备份不可信"
    exit 1
fi
echo "  ✓ expdp 日志含成功标记"

# 清理容器内副本（防撑爆容器可写层）
docker exec "$CONTAINER" rm -f "${DP_DIR}/${DUMP_NAME}.dmp" "${DP_DIR}/${LOG_NAME}"

# ---- 4. 上传文件备份（dorm-api 容器的持久卷）----
echo "[4/4] 上传文件备份..."
UPLOADS_BACKUP="$BACKUP_DIR/uploads_${TIMESTAMP}.tar.gz"
# 用容器内 sh -c 包裹路径，避免 Git Bash/MSYS 将 /app 转成 Windows 路径
if docker exec "${API_CONTAINER:-dorm-api}" sh -c 'test -d /app/uploads' 2>/dev/null; then
    docker exec "${API_CONTAINER:-dorm-api}" sh -c 'tar czf - -C /app uploads' > "$UPLOADS_BACKUP"
    if [ -s "$UPLOADS_BACKUP" ]; then
        echo "  ✓ 上传文件备份: $UPLOADS_BACKUP"
    else
        echo "  ⚠️ 上传目录为空，删除空归档"
        rm -f "$UPLOADS_BACKUP"
    fi
else
    echo "  ⚠️ dorm-api 未运行或 /app/uploads 不存在，跳过"
fi

# ---- 5. 清理旧备份 ----
echo "清理 ${RETENTION_DAYS} 天前的备份..."
find "$BACKUP_DIR" -type f -mtime "+${RETENTION_DAYS}" -delete

# ---- 汇总 ----
echo ""
echo "当前备份文件:"
du -sh "$BACKUP_DIR"/* 2>/dev/null || echo "  （无文件）"
echo ""
echo "✅ 备份完成。保留最近 ${RETENTION_DAYS} 天。"
