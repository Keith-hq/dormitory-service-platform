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
echo "[1/9] 设置时区..."
timedatectl set-timezone Asia/Shanghai
echo "当前时间: $(date)"

echo "[2/9] 配置 Swap（2GB，防止 Oracle OOM）..."
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
echo "[3/9] 安装 Docker..."
if command -v docker &> /dev/null; then
    echo "  Docker 已安装: $(docker --version)"
else
    # 中国大陆网络 get.docker.com 直连会被重置（curl 35），优先阿里云 Docker CE 源，失败再回落官方
    if curl -fsSL https://mirrors.aliyun.com/docker-ce/linux/ubuntu/gpg | \
            gpg --dearmor -o /etc/apt/keyrings/docker.gpg 2>/dev/null \
        && mkdir -p /etc/apt/sources.list.d \
        && echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://mirrors.aliyun.com/docker-ce/linux/ubuntu $(. /etc/os-release && echo "$VERSION_CODENAME") stable" \
            > /etc/apt/sources.list.d/docker.list \
        && apt-get update -qq \
        && apt-get install -y -qq docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin; then
        echo "  Docker 安装完成（阿里云源）"
    else
        echo "  阿里云源安装失败，回落官方源..."
        curl -fsSL https://get.docker.com | sh
        echo "  Docker 安装完成（官方源）"
    fi
    systemctl enable docker
    systemctl start docker
fi

echo "[4/9] 配置 Docker 镜像加速（中国大陆网络必需）..."
mkdir -p /etc/docker
# 公共加速器；若拉取 gvenzl/oracle-xe 等镜像缓慢，
# 可替换为阿里云个人加速地址（cr.console.aliyun.com → 容器镜像服务 → 加速器）
cat > /etc/docker/daemon.json <<'EOF'
{
  "registry-mirrors": [
    "https://docker.m.daocloud.io",
    "https://docker.1ms.run"
  ]
}
EOF
systemctl restart docker
echo "  加速器已写入 /etc/docker/daemon.json"

echo "[5/9] 配置 Docker Compose..."
if docker compose version &> /dev/null; then
    echo "  Docker Compose 可用"
else
    echo "  ⚠️ Docker Compose 插件未找到，请确认 Docker 版本 ≥ 23"
fi

# ---- 2. 防火墙 ----
echo "[6/9] 配置防火墙（UFW）..."
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
echo "[7/9] 创建部署目录..."
mkdir -p /opt/dorm-platform/{docker,nginx/conf.d,nginx/certs,scripts,backup,frontend/dist}
chown -R "$(logname):$(logname)" /opt/dorm-platform
echo "  目录结构:"
find /opt/dorm-platform -type d | sort

# ---- 4. 可选回退路径（容器化部署不需要）----
echo "[8/9] 安装 .NET 8 Runtime 与 Nginx（可选，非容器化调试用）..."
if command -v dotnet &> /dev/null; then
    echo "  .NET 已安装: $(dotnet --version)"
else
    # 使用 Ubuntu 仓库（22.04+ 包含 .NET 8）
    apt-get update -qq
    apt-get install -y -qq dotnet-runtime-8.0 2>/dev/null || \
        echo "  ⚠️ .NET 8 安装失败（可忽略，容器部署不需要）"
fi
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
echo "    1. 上传部署文件到 /opt/dorm-platform/（docker/ scripts/ database/）"
echo "    2. docker/.env.example 复制为 .env 并填写密码/密钥"
echo "    3. bash scripts/deploy.sh --pull-images --init-db 启动全部服务"
echo "=========================================="
