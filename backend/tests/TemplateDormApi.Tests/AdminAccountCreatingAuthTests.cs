using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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
    public async Task CreateStudentAccount_Unauthenticated_Returns401()
    {
        var content = JsonContent.Create(new
        {
            studentId = "STU_AUTH_401",
            loginName = "student_auth_401",
            password = "Test@123"
        });

        var response = await _client.PostAsync("/api/auth/accounts/students", content);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateAdminAccount_NonSuperAdmin_Returns403()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var studentId = $"STU_{suffix}";
        var loginName = $"student_{suffix}";

        // 账号创建接口本身只允许超级管理员，这里直接准备普通学生账号。
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        context.Students.Add(new Student
        {
            StudentId = studentId,
            Name = "测试学生"
        });
        context.UserAccounts.Add(new UserAccount
        {
            LoginName = loginName,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test@123"),
            AccountStatus = "正常",
            StudentId = studentId
        });
        await context.SaveChangesAsync();

        await AuthenticateAsync(loginName, "Test@123");

        var request = new
        {
            adminId = $"admin_{suffix}",
            name = "Test Admin 2",
            role = "楼长",
            buildingId = 1
        };
        var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/api/auth/accounts/admins", content);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateCounselorAccount_WithoutBuilding_CanLoginAsCounselor()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var superAdminId = $"SUPER_{suffix}";
        var counselorId = $"COUN_{suffix}";

        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.Admins.Add(new Admin
            {
                AdminId = superAdminId,
                AdminName = "测试超级管理员",
                RoleLevel = "超级管理员"
            });
            context.UserAccounts.Add(new UserAccount
            {
                LoginName = superAdminId,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test@123"),
                AccountStatus = "正常",
                AdminId = superAdminId
            });
            await context.SaveChangesAsync();
        }

        await AuthenticateAsync(superAdminId, "Test@123");
        var createResponse = await _client.PostAsJsonAsync("/api/auth/accounts/admins", new
        {
            adminId = counselorId,
            name = "测试辅导员",
            role = "辅导员",
            post = "年级辅导员"
        });
        createResponse.EnsureSuccessStatusCode();

        var createJson = JsonSerializer.Deserialize<JsonElement>(
            await createResponse.Content.ReadAsStringAsync());
        var initialPassword = createJson.GetProperty("data")
            .GetProperty("initialPassword").GetString();
        Assert.False(string.IsNullOrWhiteSpace(initialPassword));

        _client.DefaultRequestHeaders.Authorization = null;
        var loginJson = await LoginAsync(counselorId, initialPassword!);

        Assert.Equal("counselor", loginJson.GetProperty("data").GetProperty("role").GetString());
    }

    private async Task AuthenticateAsync(string loginName, string password)
    {
        var loginJson = await LoginAsync(loginName, password);
        var token = loginJson.GetProperty("data").GetProperty("token").GetString();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task<JsonElement> LoginAsync(string loginName, string password)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new { loginName, password });
        response.EnsureSuccessStatusCode();
        return JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
    }
}
