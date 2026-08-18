#!/bin/bash
# ============================================================
# 增量数据库迁移脚本（每次部署自动执行，幂等）
# 用法：bash migrate-db.sh
#
# 职责：
#   1. 按编号应用 database/ddl/extensions/ 下尚未应用的迁移
#      （记录表 D_APP_MIGRATION 是唯一真相，只跑新编号——
#        016/017/018/019 等非幂等脚本重跑必然报错，故绝不整体重放）
#   2. 每次部署恒重跑 sp/sp_*.sql（CREATE OR REPLACE，安全）
#   3. 本次有新增迁移时，追加执行两组 verify 硬门禁（WHENEVER SQLERROR）
#
# 首次运行引导：schema 已有表且记录表为空 → 将全部已发现迁移标记为
#   已应用（既有环境"已全量"假设，与 init-db.sh 全量清单一致）。
#   schema 为空 → 报错退出，提示先跑 init-db.sh（首次部署路径）。
#
# 失败策略：任何 SQL 错误（WHENEVER SQLERROR）→ sqlplus 非零退出
#   → set -e 中止 → deploy.sh 不重启容器（旧容器继续服务）。
#   记录表"成功才写入"，半程失败后重跑从断点续跑。
#
# 新迁移缺 / 结束符兜底：文件含 PL/SQL 块（DECLARE）但独立 / 行数
#   不足时，复制临时副本 sed 补 / 后执行并打印醒目 WARN——
#   DBeaver 风格脚本在 sqlplus 下会静默吞块（2026-08-18 实测 029 事故）。
# ============================================================

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
DB_DIR="$SCRIPT_DIR/../../database"
# 兼容两种目录布局（同 init-db.sh）：
#   a) 仓库布局 deploy/scripts/ → ../../database
#   b) 服务器扁平布局 scripts/ → ../database
if [ ! -d "$DB_DIR" ] && [ -d "$SCRIPT_DIR/../database" ]; then
    DB_DIR="$SCRIPT_DIR/../database"
fi
CONTAINER="${DB_CONTAINER:-dorm-oracle-db}"
DB_SERVICE="${DB_SERVICE:-DORMPDB}"

# 密码从 docker/.env 提取（grep 而非 source——不做 shell 解析，
# 兼容 ! @ # $ % ^ & * 等特殊字符；CRLF 行尾一并剥除）
ENV_FILE="$SCRIPT_DIR/../docker/.env"
if [ ! -f "$ENV_FILE" ]; then
    echo "❌ docker/.env 不存在，无法获取 ORACLE_PASSWORD"
    exit 1
fi
DB_PASSWORD=$(grep -E '^ORACLE_PASSWORD=' "$ENV_FILE" | head -1 | cut -d= -f2-)
DB_PASSWORD="${DB_PASSWORD%$'\r'}"
if [ -z "$DB_PASSWORD" ]; then
    echo "❌ docker/.env 中未找到 ORACLE_PASSWORD"
    exit 1
fi

# ---- sqlplus 执行封装（同 init-db.sh：WHENEVER SQLERROR 严格传播）----
run_sql() {   # $1=文件  $2=用户
    local f="$1" user="$2"
    { echo "WHENEVER SQLERROR EXIT SQL.SQLCODE"
      echo "CONNECT $user/\"$DB_PASSWORD\"@localhost:1521/$DB_SERVICE"
      cat "$f"; } | \
        docker exec -i "$CONTAINER" bash -c 'sqlplus -S -L /nolog'
}

run_sql_oper() { run_sql "$1" DORM_OPER; }

# 查询封装：输出仅保留最后一个非空值（供 CNT 类单值判断）
run_query() {   # $1=SQL文件
    run_sql_oper "$1" | tr -d '[:space:]' | tail -1
}

# 清单查询封装：每行一个值（保留换行，供 comm 差集比对）
run_list() {    # $1=SQL文件
    run_sql_oper "$1" | sed 's/^[[:space:]]*//; s/[[:space:]]*$//' | grep -v '^$' || true
}

echo "=========================================="
echo "  Dormitory Platform - 数据库迁移"
echo "  时间: $(date '+%Y-%m-%d %H:%M:%S')"
echo "=========================================="

# ---- [1/6] 等待 Oracle 就绪（同 init-db.sh [1/6] 探测通道）----
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
    echo "❌ Oracle 在 6 分钟内未就绪，迁移中止"
    exit 1
fi
echo "  ✓ Oracle 已就绪"

# ---- [2/6] 空库判断（必须先于记录表创建，避免记录表自身污染计数）----
cat > /tmp/migrate_table_cnt.sql <<'EOSQL'
SET HEADING OFF FEEDBACK OFF PAGESIZE 0
SELECT COUNT(*) FROM user_tables;
EXIT;
EOSQL
TABLE_CNT=$(run_query /tmp/migrate_table_cnt.sql)
rm -f /tmp/migrate_table_cnt.sql

if [ "$TABLE_CNT" = "0" ]; then
    echo "❌ schema 为空（0 张表）。首次部署请先执行："
    echo "   bash scripts/init-db.sh    # 或 bash scripts/deploy.sh --init-db"
    exit 1
fi
echo "[2/6] schema 已有 $TABLE_CNT 张表"

# ---- [3/6] 迁移记录表（幂等创建，DORM_OPER 下）----
cat > /tmp/migrate_check_tbl.sql <<'EOSQL'
SET HEADING OFF FEEDBACK OFF PAGESIZE 0
SELECT COUNT(*) FROM user_tables WHERE table_name = 'D_APP_MIGRATION';
EXIT;
EOSQL
MIG_TBL_CNT=$(run_query /tmp/migrate_check_tbl.sql)
rm -f /tmp/migrate_check_tbl.sql

if [ "$MIG_TBL_CNT" = "0" ]; then
    cat > /tmp/migrate_create_tbl.sql <<'EOSQL'
CREATE TABLE D_APP_MIGRATION (
    SCRIPT_NAME VARCHAR2(120) CONSTRAINT PK_D_APP_MIGRATION PRIMARY KEY,
    APPLIED_AT  TIMESTAMP DEFAULT SYSTIMESTAMP NOT NULL
);
EXIT;
EOSQL
    run_sql_oper /tmp/migrate_create_tbl.sql
    rm -f /tmp/migrate_create_tbl.sql
    echo "[3/6] ✓ 迁移记录表 D_APP_MIGRATION 已创建"
else
    echo "[3/6] 迁移记录表已存在"
fi

# ---- 迁移文件发现（按编号序；foundation/seed/sp 均不在此目录）----
DISCOVERED=$(find "$DB_DIR/ddl/extensions" -maxdepth 1 -name '[0-9]*.sql' \
    | sed 's|.*/||' | LC_ALL=C sort)
DISCOVERED_CNT=$(echo "$DISCOVERED" | grep -c . || true)

# ---- [4/6] 首次引导 backfill / 增量应用 ----
cat > /tmp/migrate_count.sql <<'EOSQL'
SET HEADING OFF FEEDBACK OFF PAGESIZE 0
SELECT COUNT(*) FROM D_APP_MIGRATION;
EXIT;
EOSQL
MIG_CNT=$(run_query /tmp/migrate_count.sql)
rm -f /tmp/migrate_count.sql

NEW_APPLIED=0

if [ "$MIG_CNT" = "0" ]; then
    # 引导模式：既有环境视为"已应用全部已发现迁移"（与 init-db.sh 全量清单一致）
    {
        for s in $DISCOVERED; do
            echo "INSERT INTO D_APP_MIGRATION (SCRIPT_NAME) VALUES ('$s');"
        done
        echo "COMMIT;"
        echo "EXIT;"
    } > /tmp/migrate_backfill.sql
    run_sql_oper /tmp/migrate_backfill.sql
    rm -f /tmp/migrate_backfill.sql
    echo "[4/6] 引导模式：schema 已有表且无迁移记录，将全部 $DISCOVERED_CNT 个已发现迁移标记为已应用"
else
    # 增量模式：已应用集合 vs 已发现集合，差集即待应用（按编号序）
    cat > /tmp/migrate_list.sql <<'EOSQL'
SET HEADING OFF FEEDBACK OFF PAGESIZE 0
SELECT SCRIPT_NAME FROM D_APP_MIGRATION ORDER BY SCRIPT_NAME;
EXIT;
EOSQL
    APPLIED_LIST=$(run_list /tmp/migrate_list.sql)
    rm -f /tmp/migrate_list.sql
    echo "$APPLIED_LIST" > /tmp/migrate_applied.txt
    echo "$DISCOVERED"    > /tmp/migrate_discovered.txt
    PENDING=$(comm -13 /tmp/migrate_applied.txt /tmp/migrate_discovered.txt)
    rm -f /tmp/migrate_applied.txt /tmp/migrate_discovered.txt

    if [ -z "$PENDING" ]; then
        echo "[4/6] 无新增迁移（已应用 $MIG_CNT 个，均已同步）"
    else
        for s in $PENDING; do
            file="$DB_DIR/ddl/extensions/$s"
            # 缺 / 兜底：PL/SQL 块数 > 独立 / 行数 → 临时副本补 /
            DECLARE_CNT=$(grep -cE '^\s*DECLARE' "$file" || true)
            SLASH_CNT=$(grep -cE '^\s*/\s*$' "$file" || true)
            if [ "$DECLARE_CNT" -gt 0 ] && [ "$SLASH_CNT" -lt "$DECLARE_CNT" ]; then
                tmp=$(mktemp /tmp/migrate_XXXXXX.sql)
                sed 's/^\s*END;\s*$/END;\n\//' "$file" > "$tmp"
                echo "⚠️  WARN: $s 含 PL/SQL 块但缺少 / 结束符（DBeaver 风格），已自动补齐执行临时副本"
                run_sql_oper "$tmp"
                rm -f "$tmp"
            else
                run_sql_oper "$file"
            fi
            # 执行成功才记录（失败在 run_sql_oper 内已非零退出，不会走到这里）
            cat > /tmp/migrate_record.sql <<EOSQL
INSERT INTO D_APP_MIGRATION (SCRIPT_NAME) VALUES ('$s');
COMMIT;
EXIT;
EOSQL
            run_sql_oper /tmp/migrate_record.sql
            rm -f /tmp/migrate_record.sql
            NEW_APPLIED=$((NEW_APPLIED + 1))
            echo "  ✓ 已应用并记录: $s"
        done
    fi
fi

# ---- [5/6] SP 恒重跑（CREATE OR REPLACE 安全；sp_* 通配天然排除 test_*）----
SP_CNT=0
for sp in "$DB_DIR"/sp/sp_*.sql; do
    [ -f "$sp" ] || continue
    run_sql_oper "$sp"
    SP_CNT=$((SP_CNT + 1))
done
echo "[5/6] 存储过程已重跑: $SP_CNT 个"

# ---- [6/6] verify 硬门禁（仅本次有新增迁移时执行）----
if [ "$NEW_APPLIED" -gt 0 ]; then
    echo "[6/6] 执行校验脚本（本次新增 $NEW_APPLIED 个迁移）..."
    run_sql_oper "$DB_DIR/verify/foundation_schema_checks.sql"
    run_sql_oper "$DB_DIR/verify/extension_schema_checks.sql"
    echo "  ✓ 全部校验通过"
else
    echo "[6/6] 本次无新增迁移，跳过校验"
fi

# ---- 汇总（重查一次记录表，保证 backfill 分支计数准确）----
cat > /tmp/migrate_count.sql <<'EOSQL'
SET HEADING OFF FEEDBACK OFF PAGESIZE 0
SELECT COUNT(*) FROM D_APP_MIGRATION;
EXIT;
EOSQL
FINAL_CNT=$(run_query /tmp/migrate_count.sql)
rm -f /tmp/migrate_count.sql

echo "=========================================="
echo "  迁移完成：记录表 $FINAL_CNT 个 / 本次新增 $NEW_APPLIED / SP $SP_CNT 个"
echo "=========================================="
