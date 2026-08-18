#!/bin/bash
# ============================================================
# 自签名 HTTPS 证书生成脚本（课程演示用，无域名场景）
# 用法：bash deploy/scripts/ssl-gen.sh <服务器公网IP>
#   证书有效期 365 天，过期后删除旧证书重跑本脚本即可续期
# 说明：免费 CA 只签发域名证书，纯 IP 场景用自签名；
#   浏览器会提示"不安全"，点"继续访问"即可（加密是真实的）。
# ============================================================

set -euo pipefail

IP="${1:?用法: bash deploy/scripts/ssl-gen.sh <服务器公网IP>}"
CERT_DIR="${CERT_DIR:-/opt/dorm-platform/nginx/certs}"
mkdir -p "$CERT_DIR"

if [ -f "$CERT_DIR/dorm.crt" ] && [ -f "$CERT_DIR/dorm.key" ]; then
    echo "证书已存在：$CERT_DIR/dorm.crt（如需重新生成请先删除旧证书）"
    exit 0
fi

openssl req -x509 -nodes -days 365 -newkey rsa:2048 \
    -keyout "$CERT_DIR/dorm.key" \
    -out "$CERT_DIR/dorm.crt" \
    -subj "/C=CN/O=DormitoryPlatform/CN=$IP" \
    -addext "subjectAltName=IP:$IP"

chmod 600 "$CERT_DIR/dorm.key"
echo "✓ 自签名证书已生成（有效期 365 天）:"
openssl x509 -in "$CERT_DIR/dorm.crt" -noout -subject -enddate
