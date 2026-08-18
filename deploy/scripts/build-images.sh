#!/bin/bash
# ============================================================
# 服务器本地构建镜像脚本（dorm-api / dorm-web）
# 用法：在仓库根目录执行  bash deploy/scripts/build-images.sh
# Dockerfile 为多阶段自包含构建，无需预置 publish/ 或 dist/ 产物
# ============================================================

set -euo pipefail

cd "$(dirname "$0")/../.."

# npm 镜像源（中国大陆服务器默认走 npmmirror；境外可直接
# NPM_REGISTRY=https://registry.npmjs.org bash deploy/scripts/build-images.sh）
NPM_REGISTRY="${NPM_REGISTRY:-https://registry.npmmirror.com}"

# apt 镜像源（中国大陆服务器默认走阿里云 Debian 源，
# 境外可直接 APT_MIRROR=deb.debian.org bash deploy/scripts/build-images.sh）
APT_MIRROR="${APT_MIRROR:-mirrors.aliyun.com}"

# 镜像名从 docker/.env 读取仓库前缀（缺省 ghcr.io/keith-hq）
DOCKER_REGISTRY="ghcr.io/keith-hq"
if [ -f deploy/docker/.env ]; then
    ENV_REGISTRY=$(grep -E '^DOCKER_REGISTRY=' deploy/docker/.env | head -1 | cut -d= -f2- | tr -d '\r')
    [ -n "$ENV_REGISTRY" ] && DOCKER_REGISTRY="$ENV_REGISTRY"
fi

echo "=========================================="
echo "  服务器本地构建 dorm-api / dorm-web"
echo "  时间: $(date '+%Y-%m-%d %H:%M:%S')"
echo "=========================================="

echo "[1/2] 构建后端镜像 $DOCKER_REGISTRY/dorm-api:latest"
echo "      （apt 镜像源: $APT_MIRROR；首次需拉取 dotnet/sdk:8.0 并 restore NuGet，耗时数分钟）"
docker build -f deploy/docker/Dockerfile.backend -t "$DOCKER_REGISTRY/dorm-api:latest" \
    --build-arg APT_MIRROR="$APT_MIRROR" .

echo "[2/2] 构建前端镜像 $DOCKER_REGISTRY/dorm-web:latest"
echo "      （npm 镜像源: $NPM_REGISTRY）"
docker build --build-arg NPM_REGISTRY="$NPM_REGISTRY" \
    -f deploy/docker/Dockerfile.frontend -t "$DOCKER_REGISTRY/dorm-web:latest" .

echo "=========================================="
echo "  两个镜像构建完成:"
docker images | grep -E 'dorm-api|dorm-web'
echo "=========================================="
