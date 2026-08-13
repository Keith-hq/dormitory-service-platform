#!/bin/bash
# ============================================================
# 数据库初始化脚本（首次部署执行一次）
# 用法：bash init-db.sh
#
# 前置条件：
#   1. docker compose up 已启动（gvenzl 首次启动会按 APP_USER 自动创建 DORM_OPER）
#   2. Oracle 容器健康
#
# 唯一初始化路径：
#   用户创建  → gvenzl 镜像首次启动（APP_USER/APP_USER_PASSWORD 环境变量）
#   授权      → 本脚本 [2/6]（SYSTEM 身份，幂等 GRANT）
#   DDL/SP    → 本脚本 [3/6]（DORM_OPER 身份，WHENEVER SQLERROR 严格传播）
#   校验      → 本脚本 [4/6]（两组 verify，失败即退出）
#
# 错误策略：任何 SQL 失败 → sqlplus 非零退出 → 脚本立即中止（set -euo pipefail）
# 幂等策略：仅两类操作允许重复执行（白名单）——
#   a) 授权语句（GRANT 天然幂等）
#   b) 序列创建（先查 user_sequences 存在性再建，无 WHEN OTHERS 吞错）
#   DDL 仅在空 schema（user_tables=0）时执行；已初始化则明确跳过。
# ============================================================

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
DB_DIR="$SCRIPT_DIR/../../database"
# 容器名可用环境变量覆盖（本地验证时避免与开发实例冲突）
CONTAINER="${DB_CONTAINER:-dorm-oracle-db}"

echo "=========================================="
echo "  Dormitory Platform - 数据库初始化"
echo "  时间: $(date '+%Y-%m-%d %H:%M:%S')"
echo "=========================================="

# ---- sqlplus 执行封装：注入 WHENEVER SQLERROR，错误即非零退出 ----
run_sql() {   # $1=文件  $2=连接串（如 DORM_OPER/...@localhost:1521/DORMPDB）
    local f="$1" conn="$2"
    { echo "WHENEVER SQLERROR EXIT SQL.SQLCODE"; cat "$f"; } | \
        docker exec -i "$CONTAINER" bash -c "sqlplus -S -L \"$conn\""
}

run_sql_system() {  # 以 SYSTEM 执行（密码与 ORACLE_PASSWORD 相同，gvenzl 约定）
    run_sql "$1" "SYSTEM/\${ORACLE_PASSWORD}@localhost:1521/DORMPDB"
}

run_sql_oper() {    # 以 DORM_OPER 执行（密码 = APP_USER_PASSWORD = ORACLE_PASSWORD）
    run_sql "$1" "DORM_OPER/\${ORACLE_PASSWORD}@localhost:1521/DORMPDB"
}

# ---- [1/6] 等待 Oracle 就绪（有上限，失败即退出）----
echo "[1/6] 等待 Oracle 就绪..."
READY=0
for i in $(seq 1 36); do
    if docker exec "$CONTAINER" healthcheck.sh >/dev/null 2>&1; then
        READY=1
        break
    fi
    echo "  … 等待中（$i/36）"
    sleep 10
done
if [ "$READY" -ne 1 ]; then
    echo "❌ Oracle 在 6 分钟内未就绪，初始化中止"
    exit 1
fi
echo "  ✓ Oracle 已就绪"

# ---- [2/6] 等待 DORM_OPER 出现并补齐授权（SYSTEM，幂等）----
echo "[2/6] 确认 DORM_OPER 存在并授权..."
USER_READY=0
for i in $(seq 1 12); do
    CNT=$(docker exec "$CONTAINER" bash -c "sqlplus -S -L SYSTEM/\${ORACLE_PASSWORD}@localhost:1521/DORMPDB <<'EOSQL' | tr -d '[:space:]'
SET HEADING OFF FEEDBACK OFF PAGESIZE 0
SELECT COUNT(*) FROM dba_users WHERE username = 'DORM_OPER';
EXIT;
EOSQL")
    if [ "$CNT" = "1" ]; then USER_READY=1; break; fi
    echo "  … DORM_OPER 尚未创建（$i/12），5 秒后重试"
    sleep 5
done
if [ "$USER_READY" -ne 1 ]; then
    echo "❌ DORM_OPER 未由镜像自动创建。请检查 docker-compose.yml 中"
    echo "   APP_USER / APP_USER_PASSWORD 环境变量，并确认使用了全新数据卷"
    exit 1
fi

cat > /tmp/initdb_grants.sql <<'EOSQL'
GRANT UNLIMITED TABLESPACE TO DORM_OPER;
GRANT CREATE VIEW TO DORM_OPER;
GRANT CREATE SEQUENCE TO DORM_OPER;
GRANT CREATE PROCEDURE TO DORM_OPER;
GRANT CREATE TRIGGER TO DORM_OPER;
-- expdp/impdp 备份需要读写 DATA_PUMP_DIR（21c 默认仅授给 EXP/IMP_FULL_DATABASE）
GRANT READ, WRITE ON DIRECTORY DATA_PUMP_DIR TO DORM_OPER;
EXIT;
EOSQL
run_sql_system /tmp/initdb_grants.sql
rm -f /tmp/initdb_grants.sql
echo "  ✓ DORM_OPER 存在，授权完成"

# ---- [3/6] DDL + 存储过程（仅空 schema 执行）----
echo "[3/6] 检查 schema 状态..."
TABLE_CNT=$(docker exec "$CONTAINER" bash -c "sqlplus -S -L DORM_OPER/\${ORACLE_PASSWORD}@localhost:1521/DORMPDB <<'EOSQL' | tr -d '[:space:]'
SET HEADING OFF FEEDBACK OFF PAGESIZE 0
SELECT COUNT(*) FROM user_tables;
EXIT;
EOSQL")

SCRIPTS=(
    # 基础表
    "ddl/foundation/001_create_tables.sql"
    # 扩展表（迁移按编号顺序）
    "ddl/extensions/010_extension_tables.sql"
    "ddl/extensions/011_notification_type_check.sql"
    "ddl/extensions/012_notification_id_sequence.sql"
    "ddl/extensions/013_notification_char_length.sql"
    "ddl/extensions/014_credit_log_sequence.sql"
    "ddl/extensions/015_facility_notice_sequences.sql"
    "ddl/extensions/016_d_room_floor_status.sql"
    "ddl/extensions/017_d_leave_application_reason.sql"
    "ddl/extensions/018_d_student_email.sql"
    # 存储过程
    "sp/sp_fee_sharing.sql"
    "sp/sp_billing.sql"
    "sp/sp_facility_booking.sql"
)

if [ "$TABLE_CNT" != "0" ]; then
    echo "  ⚠️ schema 已有 $TABLE_CNT 张表，跳过 DDL/SP（白名单幂等场景：重复初始化）"
else
    echo "  空 schema，开始执行 DDL 与存储过程..."
    for sql_file in "${SCRIPTS[@]}"; do
        full_path="$DB_DIR/$sql_file"
        if [ ! -f "$full_path" ]; then
            echo "❌ 脚本不存在: $full_path"
            exit 1
        fi
        echo "  执行: $sql_file"
        run_sql_oper "$full_path"   # 失败 → sqlplus 非零 → 脚本中止
    done
    echo "  ✓ DDL 与存储过程全部执行成功"
fi

# ---- [4/6] 序列补建（幂等白名单：先查存在性）----
echo "[4/6] 序列存在性检查与补建..."
cat > /tmp/initdb_seq.sql <<'EOSQL'
DECLARE
  cnt NUMBER;
BEGIN
  SELECT COUNT(*) INTO cnt FROM user_sequences WHERE sequence_name = 'SEQ_D_BUILDING';
  IF cnt = 0 THEN EXECUTE IMMEDIATE 'CREATE SEQUENCE SEQ_D_BUILDING START WITH 100 INCREMENT BY 1 NOCACHE'; END IF;

  SELECT COUNT(*) INTO cnt FROM user_sequences WHERE sequence_name = 'SEQ_D_ROOM';
  IF cnt = 0 THEN EXECUTE IMMEDIATE 'CREATE SEQUENCE SEQ_D_ROOM START WITH 1000 INCREMENT BY 1 NOCACHE'; END IF;

  SELECT COUNT(*) INTO cnt FROM user_sequences WHERE sequence_name = 'SEQ_D_ASSET';
  IF cnt = 0 THEN EXECUTE IMMEDIATE 'CREATE SEQUENCE SEQ_D_ASSET START WITH 1 INCREMENT BY 1 NOCACHE'; END IF;
END;
/
EXIT;
EOSQL
run_sql_oper /tmp/initdb_seq.sql
rm -f /tmp/initdb_seq.sql
echo "  ✓ 序列检查完成"

# ---- [5/6] 校验脚本（严格模式：任一异常即失败）----
echo "[5/6] 执行校验脚本..."
VERIFY_SCRIPTS=(
    "verify/foundation_schema_checks.sql"
    "verify/extension_schema_checks.sql"
)
for verify_file in "${VERIFY_SCRIPTS[@]}"; do
    full_path="$DB_DIR/$verify_file"
    if [ ! -f "$full_path" ]; then
        echo "❌ 校验脚本不存在: $full_path"
        exit 1
    fi
    echo "  校验: $verify_file"
    run_sql_oper "$full_path"
done
echo "  ✓ 全部校验通过"

# ---- [6/6] 汇总 ----
echo "[6/6] 数据库初始化完成！"
echo ""
echo "  对象统计:"
docker exec "$CONTAINER" bash -c "sqlplus -S -L DORM_OPER/\${ORACLE_PASSWORD}@localhost:1521/DORMPDB <<'EOSQL'
SET HEADING OFF FEEDBACK OFF PAGESIZE 0
SELECT '表: ' || COUNT(*) FROM user_tables;
SELECT '序列: ' || COUNT(*) FROM user_sequences;
SELECT '存储过程: ' || COUNT(*) FROM user_procedures WHERE object_type = 'PROCEDURE';
EXIT;
EOSQL"
echo "=========================================="
