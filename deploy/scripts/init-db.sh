#!/bin/bash
# ============================================================
# 数据库初始化脚本（首次部署执行一次）
# 用法：bash init-db.sh
#
# 前置条件：
#   1. Oracle 容器已启动且健康
#   2. docker/.env 中已设置 ORACLE_PASSWORD
# ============================================================

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
DB_DIR="$SCRIPT_DIR/../../database"

echo "=========================================="
echo "  Dormitory Platform - 数据库初始化"
echo "  时间: $(date '+%Y-%m-%d %H:%M:%S')"
echo "=========================================="

# 检查 Oracle 是否就绪
echo "[0/5] 检查 Oracle 状态..."
if ! docker exec dorm-oracle-db healthcheck.sh 2>/dev/null; then
    echo "  ⏳ Oracle 尚未就绪，等待中..."
    for i in $(seq 1 30); do
        sleep 10
        if docker exec dorm-oracle-db healthcheck.sh 2>/dev/null; then
            break
        fi
        echo "    等待中... ($i/30)"
    done
fi
echo "  ✓ Oracle 已就绪"

# 注意：以下步骤需要数据库管理员权限
# DORM_OPER 用户创建和授权通常在 DBeaver 中已完成
# 此脚本仅执行 DML/DDL 脚本，不创建用户

echo ""
echo "[1/5] 执行基础建表脚本..."
echo "  ⚠️ 请确保已通过 DBeaver 或 sqlplus 创建 DORM_OPER 用户并授权"
echo "  手动执行命令参考："
echo "    sqlplus system/${ORACLE_PASSWORD}@DORMPDB"
echo "    CREATE USER DORM_OPER IDENTIFIED BY your_password;"
echo "    GRANT CONNECT, RESOURCE, UNLIMITED TABLESPACE TO DORM_OPER;"
echo ""

# 脚本列表（按依赖顺序）
SCRIPTS=(
    # 基础表
    "ddl/foundation/001_create_tables.sql"
    # 扩展表
    "ddl/extensions/010_extension_tables.sql"
    "ddl/extensions/011_notification_type_check.sql"
    "ddl/extensions/012_notification_id_sequence.sql"
    "ddl/extensions/013_notification_char_length.sql"
    "ddl/extensions/014_credit_log_sequence.sql"
    "ddl/extensions/015_facility_notice_sequences.sql"
    "ddl/extensions/016_d_room_floor_status.sql"
    "ddl/extensions/017_d_leave_application_reason.sql"
    # 存储过程
    "sp/sp_fee_sharing.sql"
    "sp/sp_billing.sql"
    "sp/sp_facility_booking.sql"
)

echo "[2/5] 执行 DDL 和存储过程脚本..."
for sql_file in "${SCRIPTS[@]}"; do
    full_path="$DB_DIR/$sql_file"
    if [ -f "$full_path" ]; then
        echo "  执行: $sql_file"
        # 通过 sqlplus 执行（需要容器内已安装 sqlplus）
        docker exec -i dorm-oracle-db bash -c \
            "sqlplus -S DORM_OPER/\${ORACLE_PASSWORD}@DORMPDB" < "$full_path" 2>/dev/null || \
            echo "    ⚠️ $sql_file 执行可能有警告（检查是否已存在）"
    else
        echo "    ⚠️ 文件不存在: $full_path"
    fi
done

echo ""
echo "[3/5] 执行校验脚本..."
VERIFY_SCRIPTS=(
    "verify/foundation_schema_checks.sql"
    "verify/extension_schema_checks.sql"
)
for verify_file in "${VERIFY_SCRIPTS[@]}"; do
    full_path="$DB_DIR/$verify_file"
    if [ -f "$full_path" ]; then
        echo "  校验: $verify_file"
        docker exec -i dorm-oracle-db bash -c \
            "sqlplus -S DORM_OPER/\${ORACLE_PASSWORD}@DORMPDB" < "$full_path" 2>/dev/null || true
    fi
done

echo ""
echo "[4/5] 创建必要的序列（如有遗漏）..."
# 确保 EF Core 依赖的序列存在
docker exec -i dorm-oracle-db bash -c \
    "sqlplus -S DORM_OPER/\${ORACLE_PASSWORD}@DORMPDB" <<'EOSQL' 2>/dev/null || true
-- 楼栋 ID 序列
BEGIN EXECUTE IMMEDIATE 'CREATE SEQUENCE SEQ_D_BUILDING START WITH 100 INCREMENT BY 1 NOCACHE'; EXCEPTION WHEN OTHERS THEN NULL; END;
/
-- 房间 ID 序列
BEGIN EXECUTE IMMEDIATE 'CREATE SEQUENCE SEQ_D_ROOM START WITH 1000 INCREMENT BY 1 NOCACHE'; EXCEPTION WHEN OTHERS THEN NULL; END;
/
-- 资产 ID 序列
BEGIN EXECUTE IMMEDIATE 'CREATE SEQUENCE SEQ_D_ASSET START WITH 1 INCREMENT BY 1 NOCACHE'; EXCEPTION WHEN OTHERS THEN NULL; END;
/
EOSQL

echo "  ✓ 序列检查完成"

echo ""
echo "[5/5] 初始化完成！"
echo ""
echo "  后续步骤:"
echo "    1. 在 DBeaver 中连接数据库验证表结构"
echo "    2. 运行 deploy/scripts/test-connection.sh 验证连接"
echo "=========================================="
