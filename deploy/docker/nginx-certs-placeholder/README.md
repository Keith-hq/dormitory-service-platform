# 构建期占位证书（非生产密钥）

本目录下的 `dorm.crt` / `dorm.key` 是**仅用于 Docker 构建期 `nginx -t` 校验**的自签名占位证书
（CN=build-placeholder，无任何生产价值，公开提交无风险）。

运行时真实证书由 docker-compose.yml 将宿主机 `../nginx/certs/` 挂载到容器
`/etc/nginx/certs/`，整体覆盖本占位目录。生产证书请用
`deploy/scripts/ssl-gen.sh <公网IP>` 在服务器上生成，勿提交到仓库。
