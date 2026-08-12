#!/bin/bash
# ============================================================
# 一键部署/更新脚本
# 用法：bash deploy.sh [--pull-images]
#
# 选项：
#   --pull-images    从镜像仓库拉取最新镜像（否则使用本地镜像）
#   --init-db        首次部署时初始化数据库
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
    echo "  等待 Oracle 容器就绪..."
    # 等待 Oracle 健康（最多等 5 分钟）
    for i in $(seq 1 30); do
        if docker exec dorm-oracle-db healthcheck.sh 2>/dev/null; then
            echo "  ✓ Oracle 已就绪"
            break
        fi
        echo "  等待中... ($i/30)"
        sleep 10
    done
    # 初始化脚本需要人工确认执行顺序和授权
    echo "  ⚠️ 数据库初始化需手动执行，请参考 deploy/docker/README.md"
fi

# ---- 5. 健康检查 ----
echo "[4/4] 健康检查..."

echo -n "  后端健康检查: "
if curl -fsSo /dev/null http://localhost:5000/health 2>/dev/null; then
    echo "✓"
else
    echo "✗（等待启动中或检查日志: docker logs dorm-api）"
fi

echo -n "  前端页面: "
if curl -fsSo /dev/null http://localhost:80/ 2>/dev/null; then
    echo "✓"
else
    echo "✗（等待启动中或检查日志: docker logs dorm-web）"
fi

echo -n "  Oracle 数据库: "
if docker exec dorm-oracle-db healthcheck.sh 2>/dev/null; then
    echo "✓"
else
    echo "✗（检查日志: docker logs dorm-oracle-db）"
fi

echo ""
echo "=========================================="
echo "  部署完成！"
echo ""
echo "  状态检查: docker compose -f $COMPOSE_FILE ps"
echo "  日志查看: docker compose -f $COMPOSE_FILE logs -f"
echo "  前端访问: http://$(hostname -I | awk '{print $1}')"
echo "=========================================="
