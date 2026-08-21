#!/usr/bin/env bash
# 难点④ 三审并发测试运行器（双会话同 Key 竞态）
# 验证：借用/耗材出库并发重放由唯一索引兜底——只产生 1 行记录、库存只扣一次。
# 前置：SP 已部署到 dorm-oracle-db 容器（sp_shared_item.sql 已执行）。
# 用法：bash database/sp/run_shared_item_race.sh
set -e
CONTAINER=dorm-oracle-db
DIR="$(cd "$(dirname "$0")" && pwd)"

# 1. 拷贝脚本进容器
for f in race_setup race_borrow race_consume race_verify; do
    docker cp "$DIR/test_sp_shared_item_$f.sql" "$CONTAINER:/tmp/$f.sql"
done

run_sql() {
    docker exec "$CONTAINER" bash -c "sqlplus -S /nolog <<SQLEOF
CONNECT \"DORM_OPER\"/\"Dorm@2026\"@\"//localhost:1521/DORMDB\"
@$1
SQLEOF"
}

# 2. 基线准备（必须在并发会话启动前完成）
run_sql /tmp/race_setup.sql

# 3. 四个并发会话：两个借用 + 两个耗材出库（各自同 Key）
run_sql /tmp/race_borrow.sql  > /tmp/rb1.log 2>&1 &
run_sql /tmp/race_borrow.sql  > /tmp/rb2.log 2>&1 &
run_sql /tmp/race_consume.sql > /tmp/rc1.log 2>&1 &
run_sql /tmp/race_consume.sql > /tmp/rc2.log 2>&1 &
wait

# 4. 校验不变量
run_sql /tmp/race_verify.sql
echo "===== worker outputs ====="
cat /tmp/rb1.log /tmp/rb2.log /tmp/rc1.log /tmp/rc2.log
