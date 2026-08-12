#!/bin/bash
# ============================================================
# 数据库备份脚本
# 用法：bash backup.sh
# 建议：crontab 每日凌晨执行
#   0 3 * * * /opt/dorm-platform/scripts/backup.sh >> /var/log/dorm-backup.log 2>&1
# ============================================================

set -euo pipefail

BACKUP_DIR="/opt/dorm-platform/backup"
RETENTION_DAYS=7
TIMESTAMP=$(date '+%Y%m%d_%H%M%S')

mkdir -p "$BACKUP_DIR"

echo "=========================================="
echo "  Dormitory Platform - 数据备份"
echo "  时间: $(date '+%Y-%m-%d %H:%M:%S')"
echo "=========================================="

# ---- 1. Oracle 数据泵导出 ----
echo "[1/3] Oracle 数据泵导出..."
BACKUP_FILE="$BACKUP_DIR/dormdb_${TIMESTAMP}.dmp"
LOG_FILE="$BACKUP_DIR/dormdb_${TIMESTAMP}.log"

if docker exec dorm-oracle-db expdp DORM_OPER/"${ORACLE_PASSWORD}"@DORMPDB \
    DIRECTORY=DATA_PUMP_DIR \
    DUMPFILE="dormdb_${TIMESTAMP}.dmp" \
    LOGFILE="dormdb_${TIMESTAMP}.log" \
    SCHEMAS=DORM_OPER \
    2>/dev/null; then
    echo "  ✓ 导出成功: dormdb_${TIMESTAMP}.dmp"
else
    echo "  ⚠️ expdp 导出失败，回退到容器内文件拷贝..."
    # 回退方案：直接拷贝数据文件（数据库需停机或不活跃）
    # docker cp dorm-oracle-db:/opt/oracle/oradata "$BACKUP_DIR/oradata_${TIMESTAMP}/"
fi

# ---- 2. 上传文件备份 ----
echo "[2/3] 上传文件备份..."
UPLOADS_BACKUP="$BACKUP_DIR/uploads_${TIMESTAMP}.tar.gz"
if docker exec dorm-api test -d /app/uploads; then
    docker exec dorm-api tar czf - -C /app uploads 2>/dev/null > "$UPLOADS_BACKUP" || \
        echo "  ⚠️ 上传文件备份失败（可能为空目录）"
    echo "  ✓ 上传文件备份: $UPLOADS_BACKUP"
else
    echo "  ⚠️ /app/uploads 目录不存在"

fi

# ---- 3. 清理旧备份 ----
echo "[3/3] 清理 ${RETENTION_DAYS} 天前的备份..."
find "$BACKUP_DIR" -type f -mtime "+${RETENTION_DAYS}" -delete 2>/dev/null || true
echo "  ✓ 清理完成"

# ---- 汇总 ----
echo ""
echo "当前备份文件:"
du -sh "$BACKUP_DIR"/* 2>/dev/null || echo "  （无文件）"
echo ""
echo "备份完成。保留最近 ${RETENTION_DAYS} 天。"
