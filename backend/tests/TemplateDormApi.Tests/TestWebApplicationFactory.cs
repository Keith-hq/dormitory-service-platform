using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.Linq;
using TemplateDormApi.Data;

namespace TemplateDormApi.Tests;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // 强制使用 Test 环境
        builder.UseEnvironment("Test");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            // 添加测试配置（JWT、ServiceKey 等）
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "TestSecretKeyAtLeast32CharsLong!",
                ["Jwt:Issuer"] = "DormitoryPlatform",
                ["Jwt:Audience"] = "DormitoryPlatformClient",
                ["ServiceKey:Shared"] = "TestServiceKey"
            });
        });

        builder.ConfigureServices(services =>
        {
            // 移除所有 AppDbContext 相关的服务注册
            var descriptorsToRemove = services
                .Where(d => d.ServiceType == typeof(AppDbContext) ||
                            d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                            d.ServiceType == typeof(DbContextOptions))
                .ToList();

            foreach (var descriptor in descriptorsToRemove)
                services.Remove(descriptor);

            // 添加 InMemory 数据库
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase("TestDb"));
        });
    }
}