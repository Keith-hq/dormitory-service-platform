#!/bin/bash
# ============================================================
# 一键部署/更新脚本（CI 每次 push develop 自动调用）
# 用法：bash deploy.sh [--pull-images] [--init-db]
#
# 选项：
#   --pull-images    从镜像仓库拉取最新镜像（否则使用本地镜像）
#   --init-db        首次部署时初始化数据库（init-db.sh 全量路径）
#
# 流程（6 步，失败即中止、非零退出）：
#   1. .env 校验
#   2. 拉取镜像（--pull-images 时）
#   3. 首次路径（--init-db）：拉起全部容器 + init-db.sh 全量建库
#   4. 增量迁移（每次部署恒执行）：拉起 oracle-db + migrate-db.sh
#      ——迁移失败中止于本步，不重启应用容器（旧容器继续服务，CI 红门禁）
#   5. compose up -d --remove-orphans（应用容器滚动更新）
#   6. 健康检查（api /health + 前端首页 + Oracle healthcheck）+ 旧镜像清理
#
# 退出码：任一步失败或任一必需服务最终不健康时返回非零（供 CI 判定失败）
# ============================================================

set -euo pipefail

cd "$(dirname "$0")/.."

PULL_IMAGES=false
INIT_DB=false

for arg in "$@"; do
    case "$arg" in
        --pull-images) PULL_IMAGES=true ;;
        --init-db)     INIT_DB=true ;;
        *) echo "未知参数: $arg"; echo "用法: bash deploy.sh [--pull-images] [--init-db]"; exit 1 ;;
    esac
done

COMPOSE_FILE="docker/docker-compose.yml"

# ---- 就绪检查：有上限重试，最终失败返回非零 ----
# $1=URL  $2=服务名  $3=重试次数（默认30，间隔5秒）
check_ready() {
    local url="$1" name="$2" retries="${3:-30}"
    local i
    for i in $(seq 1 "$retries"); do
        if curl -fsS "$url" >/dev/null 2>&1; then
            echo "  ✓ $name 就绪"
            return 0
        fi
        if [ "$i" -lt "$retries" ]; then
            echo "  … $name 未就绪（$i/$retries），5 秒后重试"
            sleep 5
        fi
    done
    echo "  ✗ $name 最终不健康（重试 $retries 次仍失败）"
    return 1
}

echo "=========================================="
echo "  Dormitory Platform - 部署"
echo "  时间: $(date '+%Y-%m-%d %H:%M:%S')"
echo "=========================================="

# ---- [1/6] 检查 .env ----
if [ ! -f "docker/.env" ]; then
    echo "❌ docker/.env 不存在，请从 .env.example 复制并填写:"
    echo "   cp docker/.env.example docker/.env"
    echo "   vim docker/.env"
    exit 1
fi
echo "[1/6] ✓ .env 文件存在"

# ---- [2/6] 拉取镜像 ----
if $PULL_IMAGES; then
    echo "[2/6] 拉取最新镜像..."
    docker compose -f "$COMPOSE_FILE" pull
else
    echo "[2/6] 使用本地镜像（跳过拉取）..."
fi

# ---- [3/6] 首次部署路径：拉起全部容器 + init-db.sh 全量建库 ----
if $INIT_DB; then
    echo "[3/6] 首次部署：拉起容器并初始化数据库..."
    docker compose -f "$COMPOSE_FILE" up -d --remove-orphans
    bash "$(dirname "$0")/init-db.sh"
else
    echo "[3/6] 非首次部署，跳过 init-db.sh"
fi

# ---- [4/6] 增量数据库迁移（每次部署恒执行）----
# 先确保 oracle-db 在跑（冷启动/刚 rebuild 场景），再跑 migrate-db.sh。
# 迁移失败 → 本步非零退出 → 不执行 [5/6]，旧应用容器继续服务。
echo "[4/6] 拉起 oracle-db 并执行增量迁移..."
docker compose -f "$COMPOSE_FILE" up -d oracle-db
bash "$(dirname "$0")/migrate-db.sh"

# ---- [5/6] 启动/更新全部服务 ----
echo "[5/6] 启动/更新服务..."
docker compose -f "$COMPOSE_FILE" up -d --remove-orphans

# ---- [6/6] 健康检查（失败则非零退出）+ 旧镜像清理（非致命）----
echo "[6/6] 健康检查..."

FAILED=0
check_ready "http://localhost:5000/health" "后端 /health" 30 || FAILED=1
check_ready "http://localhost:80/" "前端首页" 30 || FAILED=1

echo -n "  Oracle 数据库: "
if docker exec dorm-oracle-db healthcheck.sh 2>/dev/null; then
    echo "✓"
else
    echo "✗（检查日志: docker logs dorm-oracle-db）"
    FAILED=1
fi

# 清理旧镜像（保留最近 48h 内使用过的；失败不影响部署结果）
docker image prune -af --filter "until=48h" >/dev/null 2>&1 || true

echo ""
if [ "$FAILED" -ne 0 ]; then
    echo "❌ 部署完成但存在不健康服务，请检查日志:"
    echo "   docker compose -f $COMPOSE_FILE ps"
    echo "   docker compose -f $COMPOSE_FILE logs --tail 50"
    exit 1
fi

echo "=========================================="
echo "  部署完成，全部服务健康！"
echo ""
echo "  状态检查: docker compose -f $COMPOSE_FILE ps"
echo "  日志查看: docker compose -f $COMPOSE_FILE logs -f"
# hostname -I 为 GNU/Linux 特有，Windows Git Bash 下回退为占位符
IP_ADDR=$(hostname -I 2>/dev/null | awk '{print $1}' || true)
echo "  前端访问: http://${IP_ADDR:-<服务器公网IP>}"
echo "=========================================="
