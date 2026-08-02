# 03 — Apifox 接口联调规范

> 契约对齐 + Mock 调试步骤

---

## 一、前置依赖

| 依赖项 | 说明 |
|---|---|
| Apifox | 团队统一使用 Apifox 管理 API 契约 |
| 团队空间 | 加入项目团队空间，获取接口权限 |
| 后端服务 | 后端已有至少一个可调通的接口 |
| 接口契约 | 架构师定义好的统一返回格式 |

---

## 二、分步操作

### 2.1 创建接口分组

1. 打开 Apifox → 选择项目
2. 创建分组：**样板间 - 楼栋资产**
3. 右键分组 → 新建接口

### 2.2 录入接口（以 GET /api/building 为例）

| 配置项 | 值 |
|---|---|
| **请求方法** | GET |
| **接口路径** | `/api/building` |
| **接口名称** | 分页查询楼栋列表 |
| **Query 参数** | `page: 1`、`pageSize: 10` |

### 2.3 定义响应示例

```json
{
  "code": 200,
  "message": "操作成功",
  "data": {
    "items": [
      {
        "buildingId": 1,
        "buildingName": "1号楼",
        "buildingType": "男生宿舍",
        "floorCount": 6,
        "createTime": "2026-08-01T12:00:00"
      }
    ],
    "total": 100
  }
}
```

### 2.4 生成 Mock 数据

1. 点击接口的 **"Mock"** 标签
2. 开启 Mock 服务
3. 编写 Mock 规则（Apifox 智能 Mock 或自定义规则）
4. 复制 Mock URL 供前端开发使用

---

## 三、CRUD 接口契约模板

### 3.1 查询楼栋列表

```
GET /api/building?page=1&pageSize=10
```

响应：

```json
{
  "code": 200,
  "message": "操作成功",
  "data": {
    "items": [],
    "total": 0
  }
}
```

### 3.2 查询楼栋详情

```
GET /api/building/{id}
```

### 3.3 新增楼栋

```
POST /api/building
Content-Type: application/json

{
  "buildingName": "3号楼",
  "buildingType": "女生宿舍",
  "floorCount": 7
}
```

响应：

```json
{
  "code": 200,
  "message": "新增成功",
  "data": {
    "buildingId": 3,
    "buildingName": "3号楼",
    "buildingType": "女生宿舍",
    "floorCount": 7
  }
}
```

### 3.4 编辑楼栋

```
PUT /api/building/{id}
Content-Type: application/json

{
  "buildingName": "3号楼(翻新)",
  "floorCount": 8
}
```

### 3.5 删除楼栋

```
DELETE /api/building/{id}
```

响应：

```json
{
  "code": 200,
  "message": "删除成功",
  "data": null
}
```

---

## 四、验收标准

| 检查项 | 标准 |
|---|---|
| 接口分组 | 分组名称规范，层级清晰 |
| 请求参数 | 路径、Query、Body 参数完整定义 |
| 响应示例 | 每个接口至少有一个成功响应示例 |
| Mock 可用 | 前端可通过 Apifox Mock URL 获取模拟数据 |
| 与后端对齐 | Apifox 接口定义与后端实际返回一致（契约对齐） |
| 统一返回格式 | 所有接口遵循 `{ code, message, data }` 格式 |

---

## 五、常见报错

| 报错信息 | 原因 | 解决方案 |
|---|---|---|
| Mock 数据不返回 | Mock 服务未开启 | 在 Apifox 中开启 Mock 服务开关 |
| Mock 返回与契约不一致 | Mock 规则未设置 | 手动编写 Mock 规则或使用智能 Mock |
| 前端请求 404 | 接口路径拼写不一致 | 核对 Apifox 路径与前端 `api/*.js` 中的路径 |
| 响应字段为 null | 后端未赋值 | 检查后端 Service 层是否正确填充响应 DTO |
| 请求参数传递失败 | 参数位置不匹配 | GET 用 Query、POST/PUT 用 Body |
