using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using TemplateDormApi.Data;
using TemplateDormApi.Models;
using Xunit;

namespace TemplateDormApi.Tests;

public class AuthTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public AuthTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateAdminAccount_Unauthenticated_Returns401()
    {
        var request = new
        {
            adminId = "test_admin",
            name = "Test Admin",
            role = "楼长",
            buildingId = 1
        };
        var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/api/auth/accounts/admins", content);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateAdminAccount_NonSuperAdmin_Returns403()
    {
        // 1. 准备数据：插入一条学生记录（因为注册接口要求 Student 存在）
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        context.Students.Add(new Student
        {
            StudentId = "STU999",
            Name = "测试学生"
        });
        await context.SaveChangesAsync();

        // 2. 注册学生账号
        var registerPayload = new { studentId = "STU999", loginName = "teststudent", password = "Test@123" };
        var registerContent = new StringContent(JsonSerializer.Serialize(registerPayload), Encoding.UTF8, "application/json");
        var registerResponse = await _client.PostAsync("/api/auth/accounts/students", registerContent);
        registerResponse.EnsureSuccessStatusCode();

        // 3. 登录（测试环境已跳过验证码）
        var loginPayload = new { loginName = "teststudent", password = "Test@123" };
        var loginContent = new StringContent(JsonSerializer.Serialize(loginPayload), Encoding.UTF8, "application/json");
        var loginResponse = await _client.PostAsync("/api/auth/login", loginContent);
        loginResponse.EnsureSuccessStatusCode();

        var loginJson = await loginResponse.Content.ReadAsStringAsync();
        var token = JsonSerializer.Deserialize<JsonElement>(loginJson)
            .GetProperty("data").GetProperty("token").GetString();

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // 4. 调用创建宿管账号接口（期望 403）
        var request = new
        {
            adminId = "test_admin2",
            name = "Test Admin 2",
            role = "楼长",
            buildingId = 1
        };
        var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/api/auth/accounts/admins", content);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}