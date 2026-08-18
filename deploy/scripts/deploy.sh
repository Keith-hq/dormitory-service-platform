#!/bin/bash
# ============================================================
# 一键部署/更新脚本
# 用法：bash deploy.sh [--pull-images] [--init-db]
#
# 选项：
#   --pull-images    从镜像仓库拉取最新镜像（否则使用本地镜像）
#   --init-db        首次部署时初始化数据库
#
# 退出码：任一必需服务最终不健康时返回非零（供 CI 判定失败）
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

# ---- 1. 检查 .env ----
if [ ! -f "docker/.env" ]; then
    echo "❌ docker/.env 不存在，请从 .env.example 复制并填写:"
    echo "   cp docker/.env.example docker/.env"
    echo "   vim docker/.env"
    exit 1
fi
echo "✓ .env 文件存在"

# ---- 2. 拉取/构建镜像 ----
if $PULL_IMAGES; then
    echo "[1/4] 拉取最新镜像..."
    docker compose -f "$COMPOSE_FILE" pull
else
    echo "[1/4] 使用本地镜像（跳过拉取）..."
fi

# ---- 3. 启动/更新服务 ----
echo "[2/4] 启动/更新服务..."
docker compose -f "$COMPOSE_FILE" up -d --remove-orphans

# ---- 4. 数据库初始化（首次部署）----
if $INIT_DB; then
    echo "[3/4] 初始化数据库..."
    bash "$(dirname "$0")/init-db.sh"
else
    echo "[3/4] 跳过数据库初始化（如需首次建表请加 --init-db）..."
fi

# ---- 5. 健康检查（失败则非零退出）----
echo "[4/4] 健康检查..."

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
