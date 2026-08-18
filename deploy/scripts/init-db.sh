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
#   授权      → 本脚本 [2/7]（SYSTEM 身份，幂等 GRANT）
#   内存压制  → 本脚本 [3/7]（sysdba OS 认证，SGA 1G / PGA 512M，幂等）
#   DDL/SP    → 本脚本 [4/7]（DORM_OPER 身份，WHENEVER SQLERROR 严格传播）
#   校验      → 本脚本 [6/7]（两组 verify，失败即退出）
#
# 错误策略：任何 SQL 失败 → sqlplus 非零退出 → 脚本立即中止（set -euo pipefail）
# 幂等策略：仅两类操作允许重复执行（白名单）——
#   a) 授权语句（GRANT 天然幂等）
#   b) 序列与主键触发器创建（先查 user_sequences/user_triggers 存在性再建，
#      无 WHEN OTHERS 吞错）
#   DDL 仅在空 schema（user_tables=0）时执行；已初始化则明确跳过。
# ============================================================

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
DB_DIR="$SCRIPT_DIR/../../database"
# 兼容两种目录布局：
#   a) 仓库布局 deploy/scripts/ → ../../database（仓库根下的 database/）
#   b) 服务器扁平布局 scripts/ → ../database（setup-server.sh 上传 docker/ scripts/ database/ 的约定）
if [ ! -d "$DB_DIR" ] && [ -d "$SCRIPT_DIR/../database" ]; then
    DB_DIR="$SCRIPT_DIR/../database"
fi
# 容器名可用环境变量覆盖（本地验证时避免与开发实例冲突）
CONTAINER="${DB_CONTAINER:-dorm-oracle-db}"
# 服务名参数化：gvenzl 监听注册的服务名随实例配置大小写不同
# （云端 compose 为 DORMPDB；旧本地容器可能注册为小写 dormpdb）
DB_SERVICE="${DB_SERVICE:-DORMPDB}"

# 密码从 docker/.env 提取（grep 而非 source——不做 shell 解析，
# 兼容 ! @ # $ % ^ & * 等特殊字符；CRLF 行尾一并剥除）
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

echo "=========================================="
echo "  Dormitory Platform - 数据库初始化"
echo "  时间: $(date '+%Y-%m-%d %H:%M:%S')"
echo "=========================================="

# ---- sqlplus 执行封装：注入 WHENEVER SQLERROR，错误即非零退出 ----
# 连接采用 /nolog + SQL 内 CONNECT 命令：密码以双引号包裹直达 sqlplus 层，
# 含 @ 等特殊字符的密码安全（命令行参数方式会被 sqlplus 按第一个 @ 分割破坏）
run_sql() {   # $1=文件  $2=用户（SYSTEM / DORM_OPER）
    local f="$1" user="$2"
    { echo "WHENEVER SQLERROR EXIT SQL.SQLCODE"
      echo "CONNECT $user/\"$DB_PASSWORD\"@localhost:1521/$DB_SERVICE"
      cat "$f"; } | \
        docker exec -i "$CONTAINER" bash -c 'sqlplus -S -L /nolog'
}

run_sql_system() {  # 以 SYSTEM 执行（密码与 ORACLE_PASSWORD 相同，gvenzl 约定）
    run_sql "$1" SYSTEM
}

run_sql_sysdba() {  # 以 sysdba OS 认证在 CDB 根容器执行（无需密码，同 [1/6] 探测通道）
    local f="$1"
    { echo "WHENEVER SQLERROR EXIT SQL.SQLCODE"
      cat "$f"; } | \
        docker exec -i "$CONTAINER" bash -c 'sqlplus -S -L / as sysdba'
}

run_sql_oper() {    # 以 DORM_OPER 执行（密码 = APP_USER_PASSWORD = ORACLE_PASSWORD）
    run_sql "$1" DORM_OPER
}

# 查询封装：输出仅保留最后一个非空值（供 CNT 类判断）
run_query() {   # $1=用户  $2=SQL文件
    run_sql "$2" "$1" | tr -d '[:space:]' | tail -1
}

# ---- [1/6] 等待 Oracle 就绪（有上限，失败即退出）----
# 用 sysdba OS 认证探测（不依赖监听服务名/healthcheck 脚本，
# 旧容器 ORACLE_DATABASE 与实际 PDB 名不一致时 healthcheck.sh 会误报失败）
echo "[1/6] 等待 Oracle 就绪..."
READY=0
for i in $(seq 1 36); do
    if echo "SELECT 1 FROM dual; EXIT;" | \
        docker exec -i "$CONTAINER" bash -c 'sqlplus -S -L / as sysdba' >/dev/null 2>&1; then
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
cat > /tmp/initdb_check_user.sql <<'EOSQL'
SET HEADING OFF FEEDBACK OFF PAGESIZE 0
SELECT COUNT(*) FROM dba_users WHERE username = 'DORM_OPER';
EXIT;
EOSQL
USER_READY=0
for i in $(seq 1 12); do
    CNT=$(run_query SYSTEM /tmp/initdb_check_user.sql)
    if [ "$CNT" = "1" ]; then USER_READY=1; break; fi
    echo "  … DORM_OPER 尚未创建（$i/12），5 秒后重试"
    sleep 5
done
rm -f /tmp/initdb_check_user.sql
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

# ---- [3/7] Oracle 内存参数压制（sysdba，幂等）----
# 4G 轻量服务器：镜像默认 SGA/PGA 合计可逼近 2G+，与 .NET/nginx/OS 抢内存
# → swap 抖动 → CPU 飙升。先按现状条件清零 memory_target（AMM 模式下
# sga_target/pga_aggregate_target 不允许单独设置），再设 SGA=1G / PGA=512M。
# SCOPE=BOTH：立即生效 + 持久化到 spfile，容器重启后仍有效。
# ⚠️ 必须 sysdba：SYSTEM@<服务名> 连接落在 PDB 内，SGA_TARGET/MEMORY_TARGET
#    属 CDB 级参数，PDB 内 ALTER 静默无效果（不报错、值不变，2026-08-18 实测）。
echo "[3/7] 设置 Oracle 内存参数（SGA 1G / PGA 512M）..."
cat > /tmp/initdb_memory.sql <<'EOSQL'
DECLARE
  v_mt NUMBER;
BEGIN
  SELECT TO_NUMBER(value) INTO v_mt FROM v$parameter WHERE name = 'memory_target';
  IF v_mt > 0 THEN
    EXECUTE IMMEDIATE 'ALTER SYSTEM SET MEMORY_TARGET=0 SCOPE=BOTH';
  END IF;
  EXECUTE IMMEDIATE 'ALTER SYSTEM SET SGA_TARGET=1073741824 SCOPE=BOTH';
  EXECUTE IMMEDIATE 'ALTER SYSTEM SET PGA_AGGREGATE_TARGET=536870912 SCOPE=BOTH';
END;
/
EXIT;
EOSQL
run_sql_sysdba /tmp/initdb_memory.sql
rm -f /tmp/initdb_memory.sql
echo "  ✓ 内存参数已设置（SGA_TARGET=1G, PGA_AGGREGATE_TARGET=512M）"

# ---- [4/7] DDL + 存储过程（仅空 schema 执行）----
echo "[4/7] 检查 schema 状态..."
cat > /tmp/initdb_table_cnt.sql <<'EOSQL'
SET HEADING OFF FEEDBACK OFF PAGESIZE 0
SELECT COUNT(*) FROM user_tables;
EXIT;
EOSQL
TABLE_CNT=$(run_query DORM_OPER /tmp/initdb_table_cnt.sql)
rm -f /tmp/initdb_table_cnt.sql

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
    "ddl/extensions/019_shared_item_idempotency.sql"
    "ddl/extensions/020_repair_late_hygiene_room_sequences.sql"
    "ddl/extensions/021_sla_dispatch.sql"
    "ddl/extensions/022_fee_detail_dedup_uk.sql"
    "ddl/extensions/023_dorm_checkout_sequences_room_unique.sql"
    "ddl/extensions/024_vote_visitor_parcel_sequences.sql"
    "ddl/extensions/025_audit_details_college_major_sequences.sql"
    "ddl/extensions/026_utility_fee_sequence.sql"
    "ddl/extensions/027_add_admin_post.sql"
    "ddl/extensions/028_user_account_sequence.sql"
    "ddl/extensions/029_asset_shareditem_cleaning_sequences_and_tables.sql"
    "ddl/extensions/030_add_token_version_and_first_login.sql"
    "ddl/extensions/031_audit_event_sequence.sql"
    "ddl/extensions/032_admin_role_include_counselor.sql"
    # 存储过程
    "sp/sp_fee_sharing.sql"
    "sp/sp_billing.sql"
    "sp/sp_facility_booking.sql"
    "sp/sp_shared_item.sql"
    "sp/sp_sla_dispatch.sql"
    "sp/sp_wallet.sql"
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

# ---- [5/7] 序列补建（幂等白名单：先查存在性）----
echo "[5/7] 序列与主键触发器检查与补建..."
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

  -- 主键生成触发器（幂等白名单：先查 user_triggers 存在性）。
  -- D_Building/D_Room/D_Asset 的 NUMBER 主键无 IDENTITY 也无生成触发器，
  -- EF 按 identity 语义省略主键列 → 插入 NULL → ORA-01400（API 新增 500）。
  -- 触发器在 NEW 值为 NULL 时才赋序列值，手工指定 ID 的插入不受影响。
  SELECT COUNT(*) INTO cnt FROM user_triggers WHERE trigger_name = 'TRG_D_BUILDING_ID_BI';
  IF cnt = 0 THEN
    EXECUTE IMMEDIATE 'CREATE TRIGGER TRG_D_BUILDING_ID_BI BEFORE INSERT ON D_Building FOR EACH ROW WHEN (NEW.Building_ID IS NULL) BEGIN SELECT SEQ_D_BUILDING.NEXTVAL INTO :NEW.Building_ID FROM dual; END;';
  END IF;

  SELECT COUNT(*) INTO cnt FROM user_triggers WHERE trigger_name = 'TRG_D_ROOM_ID_BI';
  IF cnt = 0 THEN
    EXECUTE IMMEDIATE 'CREATE TRIGGER TRG_D_ROOM_ID_BI BEFORE INSERT ON D_Room FOR EACH ROW WHEN (NEW.Room_ID IS NULL) BEGIN SELECT SEQ_D_ROOM.NEXTVAL INTO :NEW.Room_ID FROM dual; END;';
  END IF;

  SELECT COUNT(*) INTO cnt FROM user_triggers WHERE trigger_name = 'TRG_D_ASSET_ID_BI';
  IF cnt = 0 THEN
    EXECUTE IMMEDIATE 'CREATE TRIGGER TRG_D_ASSET_ID_BI BEFORE INSERT ON D_Asset FOR EACH ROW WHEN (NEW.Asset_ID IS NULL) BEGIN SELECT SEQ_D_ASSET.NEXTVAL INTO :NEW.Asset_ID FROM dual; END;';
  END IF;
END;
/
EXIT;
EOSQL
run_sql_oper /tmp/initdb_seq.sql
rm -f /tmp/initdb_seq.sql
echo "  ✓ 序列与触发器检查完成"

# ---- [6/7] 校验脚本（严格模式：任一异常即失败）----
echo "[6/7] 执行校验脚本..."
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

# ---- [7/7] 汇总 ----
echo "[7/7] 数据库初始化完成！"
echo ""
echo "  对象统计:"
cat > /tmp/initdb_summary.sql <<'EOSQL'
SET HEADING OFF FEEDBACK OFF PAGESIZE 0
SELECT '表: ' || COUNT(*) FROM user_tables;
SELECT '序列: ' || COUNT(*) FROM user_sequences;
SELECT '存储过程: ' || COUNT(*) FROM user_procedures WHERE object_type = 'PROCEDURE';
EXIT;
EOSQL
run_sql_oper /tmp/initdb_summary.sql
rm -f /tmp/initdb_summary.sql
echo "=========================================="
