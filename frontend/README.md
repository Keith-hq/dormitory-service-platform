# 前端应用

本目录是宿舍服务管理平台的 Vue 3 + Vite 前端应用。

## 开发命令

```bash
npm ci
npm run dev
```

默认开发地址为 `http://localhost:3000`，`/api` 请求会代理到 `http://localhost:5000`。

## 目录约定

- `src/api/`：按业务模块封装 HTTP 请求。
- `src/components/`：跨页面复用的展示和交互组件。
- `src/layouts/`：页面布局组件，当前保留为扩展入口。
- `src/router/`：路由定义。
- `src/stores/`：Pinia 状态仓库。
- `src/views/`：页面级组件。
- `tests/`：前端单元测试和组件测试。

所有接口请求通过 `src/utils/request.js`，不要在页面中直接创建 Axios 实例。
