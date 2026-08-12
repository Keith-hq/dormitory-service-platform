#!/bin/bash
# ============================================================
# 云端服务器初始化脚本
# 适用：Ubuntu 22.04 / 24.04（amd64）
# 用法：sudo bash setup-server.sh
# ============================================================

set -euo pipefail

echo "=========================================="
echo "  Dormitory Platform - 服务器初始化"
echo "  执行时间: $(date '+%Y-%m-%d %H:%M:%S')"
echo "=========================================="

# ---- 0. 系统基础 ----
echo "[1/8] 设置时区..."
timedatectl set-timezone Asia/Shanghai
echo "当前时间: $(date)"

echo "[2/8] 配置 Swap（2GB，防止 Oracle OOM）..."
if swapon --show | grep -q .; then
    echo "  Swap 已存在，跳过"
else
    fallocate -l 2G /swapfile
    chmod 600 /swapfile
    mkswap /swapfile
    swapon /swapfile
    echo '/swapfile none swap sw 0 0' >> /etc/fstab
    echo "  Swap 创建完成"
fi

# ---- 1. Docker ----
echo "[3/8] 安装 Docker..."
if command -v docker &> /dev/null; then
    echo "  Docker 已安装: $(docker --version)"
else
    curl -fsSL https://get.docker.com | sh
    systemctl enable docker
    systemctl start docker
    echo "  Docker 安装完成"
fi

echo "[4/8] 配置 Docker Compose..."
if docker compose version &> /dev/null; then
    echo "  Docker Compose 可用"
else
    echo "  ⚠️ Docker Compose 插件未找到，请确认 Docker 版本 ≥ 23"
fi

# ---- 2. 防火墙 ----
echo "[5/8] 配置防火墙（UFW）..."
ufw --force reset
ufw default deny incoming
ufw default allow outgoing
ufw allow OpenSSH
ufw allow 80/tcp
ufw allow 443/tcp
ufw --force enable
echo "  防火墙规则:"
ufw status numbered

# ---- 3. 目录规划 ----
echo "[6/8] 创建部署目录..."
mkdir -p /opt/dorm-platform/{docker,nginx/conf.d,nginx/certs,scripts,backup,frontend/dist}
chown -R "$(logname):$(logname)" /opt/dorm-platform
echo "  目录结构:"
find /opt/dorm-platform -type d | sort

# ---- 4. .NET 8 Runtime（回退路径）----
echo "[7/8] 安装 .NET 8 Runtime（可选，用于非容器化调试）..."
if command -v dotnet &> /dev/null; then
    echo "  .NET 已安装: $(dotnet --version)"
else
    # 使用 Ubuntu 仓库（22.04+ 包含 .NET 8）
    apt-get update -qq
    apt-get install -y -qq dotnet-runtime-8.0 2>/dev/null || \
        echo "  ⚠️ .NET 8 安装失败（可忽略，容器部署不需要）"
fi

# ---- 5. Nginx（宿主机，回退路径）----
echo "[8/8] 安装 Nginx（可选，用于非容器化网关）..."
if command -v nginx &> /dev/null; then
    echo "  Nginx 已安装: $(nginx -v 2>&1)"
else
    apt-get install -y -qq nginx 2>/dev/null || \
        echo "  ⚠️ Nginx 安装失败（可忽略，容器化部署使用 dorm-web 网关）"
fi

echo ""
echo "=========================================="
echo "  服务器初始化完成！"
echo ""
echo "  后续步骤:"
echo "    1. 将 deploy/ 目录上传到 /opt/dorm-platform/"
echo "    2. 复制 .env.example 为 .env 并填写密码/密钥"
echo "    3. 执行 deploy.sh 启动服务"
echo "=========================================="
