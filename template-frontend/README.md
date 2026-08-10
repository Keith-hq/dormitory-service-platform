# 宿舍服务管理平台前端

高校宿舍后勤与共享生活服务平台的 Vue 3 前端样板。业务模块应在此工程规范上开发，统一复用路由、状态管理、请求层和通用组件。

## 环境要求

- Node.js `20.19.0`，以 `.nvmrc` 为准；Vite同时支持Node.js `22.12.0`及以上版本
- npm 10+
- VS Code，并安装工作区推荐的 Vue Official、ESLint、Prettier 插件

如果本机安装了 nvm：

```bash
nvm install
nvm use
```

## 安装与启动

```bash
npm ci
npm run dev
```

默认访问地址：`http://localhost:3000`。未登录访问根路由或 `/building` 时会先跳转到 `/login`。

后端默认运行在 `http://localhost:5000`，本地开发时 `/api` 请求由 Vite 代理到后端。

## 常用命令

```bash
npm run dev          # 启动开发服务器
npm run build        # 生成生产构建
npm run preview      # 本地预览生产构建
npm run lint         # 检查 JavaScript 和 Vue 文件
npm run lint:fix     # 自动修复可安全修复的 ESLint 问题
npm run format       # 使用 Prettier 格式化工程文件
npm run format:check # 检查格式但不改文件
npm run check        # 依次执行 lint、格式检查和生产构建
```

提交代码前至少执行：

```bash
npm run check
```

## 环境变量

复制示例配置，按本地环境调整：

```bash
cp .env.example .env.local
```

| 变量                | 默认值                  | 用途                                                   |
| ------------------- | ----------------------- | ------------------------------------------------------ |
| `VITE_API_BASE_URL` | `/api`                  | 浏览器请求的 API 基地址；也可填写 Apifox Mock 完整地址 |
| `VITE_USE_MOCK`     | `false`                 | `true` 时 Mock 登录与楼栋 CRUD，页面调用方式保持不变   |
| `API_PROXY_TARGET`  | `http://localhost:5000` | `/api` 在本地开发时的代理目标                          |
| `DEV_PORT`          | `3000`                  | Vite 开发服务器端口                                    |

只有以 `VITE_` 开头的变量会暴露给浏览器；`API_PROXY_TARGET`和`DEV_PORT`只由Vite配置读取。不得在任何前端环境文件中存放密码、Token、数据库连接串等秘密。

当 `VITE_API_BASE_URL` 是 `/api` 这类相对路径时，Vite启用本地代理；当它是Apifox Mock等完整URL时，浏览器会直接请求该地址。

### 本地 Mock 样板

后端接口未就绪时，在不提交的 `.env.local` 中开启：

```bash
VITE_USE_MOCK=true
```

测试账号：

| 登录名       | 密码     | 角色      |
| ------------ | -------- | --------- |
| `student001` | `123456` | `student` |

登录页始终调用 `POST /api/auth/login`，楼栋页始终调用 `buildingApi`。Mock 位于请求适配层，当前接管登录与楼栋分页/详情/新增/编辑/删除接口；关闭 `VITE_USE_MOCK` 后请求直接进入真实后端。Mock 数据仅保存在当前页面会话，刷新后恢复初始数据。

临时登录契约：

```json
{
  "code": 200,
  "message": "登录成功",
  "data": {
    "token": "mock-token",
    "userInfo": {
      "id": "student001",
      "name": "测试学生",
      "role": "student"
    }
  }
}
```

## 目录结构

```text
src/
  api/          按业务模块封装接口
  assets/       图片等静态资源
  components/   跨页面复用的通用组件
  mock/         由环境变量启用的本地接口适配器
  router/       路由表、守卫和权限元数据
  store/        Pinia全局状态
  utils/        请求层和无业务归属的工具
  views/        路由级业务页面
```

开发约束：

- 页面通过 `src/api/` 调用接口，不直接创建 Axios 实例。
- 所有接口复用 `src/utils/request.js`，响应会被解构为后端返回的 `data`。
- 登录状态由 Pinia 管理，Token 与最小用户信息持久化；退出或收到 `401` 时统一清理。
- 通知状态由 `useNoticeStore` 管理，统一调用列表、未读数和已读接口；会话结束时同步清理。
- 受保护路由使用 `meta.requiresAuth`；角色字段仅预留，本阶段不做角色级权限。
- 接口字段与角色值以锁定的 Apifox 契约为准。
- 通用列表优先复用 `CrudTable`、`SearchForm`、`StatusTag` 等组件；`CrudTable` 由页面传入 `page` 与稳定 `rowKey`，`SearchForm` 支持回车查询与加载禁用，`StatusTag` 由业务页映射语义色调。
- 只有真正跨页面共享的数据才进入 Pinia。
- 不提交 `node_modules`、`dist`、`.env.local` 或任何敏感信息。

## 新增业务页面

1. 在 `src/api/` 新建模块接口文件。
2. 在 `src/views/` 新建路由级页面。
3. 在 `src/router/index.js` 注册懒加载路由。
4. 按需复用 `src/components/` 中的通用组件。
5. 使用Apifox或真实后端验证请求、错误、空数据和加载状态。

## 联调检查

完整链路为：

```text
Vue (3000) → /api → ASP.NET Core (5000) → Oracle (1521)
```

联调前确认：

- Oracle容器已启动并完成建库。
- 后端Swagger可通过 `http://localhost:5000/swagger` 访问。
- 浏览器Network中的 `/api/*` 请求返回 `{ code, message, data }`。
- `/building` 页面能够查询、新增、编辑和删除数据。

## 登录链路检查

1. 清理浏览器中的 `token` 和 `userInfo`。
2. 直接访问 `/building`，确认跳转到 `/login?redirect=/building`。
3. 使用错误密码登录，确认页面显示错误提示。
4. 使用 Mock 测试账号登录，确认返回 `/building`。
5. 刷新页面，确认登录状态和用户信息仍然存在。
6. 点击“退出登录”，确认状态被清理并返回 `/login`。

## 常见问题

- `npm ci`提示Node版本不符合：执行 `nvm use`，确认`node --version`为20.19.x。
- 端口3000被占用：在`.env.local`中调整`DEV_PORT`。
- 页面提示Network Error：先确认后端5000端口已启动，再检查`API_PROXY_TARGET`。
- API返回404：核对Apifox契约、`VITE_API_BASE_URL`和接口文件中的路径。
- VS Code不自动格式化：安装推荐插件，并确认工作区`settings.json`已生效。
