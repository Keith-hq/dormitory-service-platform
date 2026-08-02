# 后端应用

后端采用 ASP.NET Core .NET 8、EF Core 和 Oracle Provider。

从仓库根目录启动：

```bash
dotnet restore backend/src/TemplateDormApi.csproj
dotnet run --project backend/src/TemplateDormApi.csproj
```

目录约定：

- `src/Api/`：HTTP 控制器、鉴权和响应模型。
- `src/Application/`：DTO、应用服务和输入校验。
- `src/Domain/`：实体、状态和业务规则。
- `src/Infrastructure/`：EF Core、Repository 和外部边界。
- `src/Common/`：跨模块共享的异常、日志和通用组件入口。
- `tests/`：后端单元测试与集成测试入口。

连接字符串只能来自 .NET User Secrets 或本机环境变量，不能写入仓库配置文件。
