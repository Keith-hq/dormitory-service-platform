using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.IdentityModel.Tokens.Jwt;
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

    [Fact]
    public async Task Login_FirstTime_ReturnsNeedChangePasswordTrue()
    {
        // Arrange: 创建一个新学生账号（IsFirstLogin = 'Y'）
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var studentId = $"STU_{suffix}";
        var loginName = $"student_{suffix}";
        var password = "Test@123";

        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.Students.Add(new Student
            {
                StudentId = studentId,
                Name = "首次登录测试学生"
            });
            context.UserAccounts.Add(new UserAccount
            {
                LoginName = loginName,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                AccountStatus = "正常",
                StudentId = studentId,
                IsFirstLogin = "Y" // 显式设置首次登录
            });
            await context.SaveChangesAsync();
        }

        // Act: 登录该账号
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.PostAsJsonAsync("/api/auth/login", new { loginName, password });
        response.EnsureSuccessStatusCode();
        var json = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());

        // Assert: needChangePassword 应为 true
        var needChangePassword = json.GetProperty("data").GetProperty("needChangePassword").GetBoolean();
        Assert.True(needChangePassword);
    }

    [Fact]
    public async Task Login_AfterPasswordChange_ReturnsNeedChangePasswordFalse()
    {
        // Arrange: 创建学生账号
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var studentId = $"STU_{suffix}";
        var loginName = $"student_{suffix}";
        var oldPassword = "Test@123";
        var newPassword = "NewPass@456";

        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.Students.Add(new Student
            {
                StudentId = studentId,
                Name = "修改密码测试学生"
            });
            context.UserAccounts.Add(new UserAccount
            {
                LoginName = loginName,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(oldPassword),
                AccountStatus = "正常",
                StudentId = studentId,
                IsFirstLogin = "Y"
            });
            await context.SaveChangesAsync();
        }

        // 1. 首次登录获取 token
        _client.DefaultRequestHeaders.Authorization = null;
        var firstLoginResponse = await _client.PostAsJsonAsync("/api/auth/login", new { loginName, password = oldPassword });
        firstLoginResponse.EnsureSuccessStatusCode();
        var firstLoginJson = JsonSerializer.Deserialize<JsonElement>(await firstLoginResponse.Content.ReadAsStringAsync());
        var token = firstLoginJson.GetProperty("data").GetProperty("token").GetString();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // 2. 修改密码
        var changePasswordResponse = await _client.PutAsJsonAsync("/api/auth/password", new
        {
            oldPassword,
            newPassword
        });
        changePasswordResponse.EnsureSuccessStatusCode();

        // 3. 用新密码重新登录
        _client.DefaultRequestHeaders.Authorization = null;
        var secondLoginResponse = await _client.PostAsJsonAsync("/api/auth/login", new { loginName, password = newPassword });
        secondLoginResponse.EnsureSuccessStatusCode();
        var secondLoginJson = JsonSerializer.Deserialize<JsonElement>(await secondLoginResponse.Content.ReadAsStringAsync());

        // Assert: needChangePassword 应为 false
        var needChangePassword = secondLoginJson.GetProperty("data").GetProperty("needChangePassword").GetBoolean();
        Assert.False(needChangePassword);
    }

    [Fact]
    public async Task DisableAdmin_OldTokenInvalid()
    {
        // Arrange: 创建超级管理员和楼长管理员
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var superAdminId = $"SUPER_{suffix}";
        var targetAdminId = $"ADMIN_{suffix}";
        var superLoginName = $"super_{suffix}";
        var targetLoginName = $"admin_{suffix}";
        var password = "Test@123";

        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.Admins.Add(new Admin
            {
                AdminId = superAdminId,
                AdminName = "测试超级管理员",
                RoleLevel = "超级管理员",
                TokenVersion = 0
            });
            context.UserAccounts.Add(new UserAccount
            {
                LoginName = superLoginName,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                AccountStatus = "正常",
                AdminId = superAdminId,
                IsFirstLogin = "N"
            });

            context.Admins.Add(new Admin
            {
                AdminId = targetAdminId,
                AdminName = "测试楼长",
                RoleLevel = "楼长",
                TokenVersion = 0
            });
            context.UserAccounts.Add(new UserAccount
            {
                LoginName = targetLoginName,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                AccountStatus = "正常",
                AdminId = targetAdminId,
                IsFirstLogin = "N"
            });

            await context.SaveChangesAsync();
        }

        // 1. 登录超级管理员，获取 tokenSuper
        _client.DefaultRequestHeaders.Authorization = null;
        var loginSuperResponse = await _client.PostAsJsonAsync("/api/auth/login", new { loginName = superLoginName, password });
        loginSuperResponse.EnsureSuccessStatusCode();
        var loginSuperJson = JsonSerializer.Deserialize<JsonElement>(await loginSuperResponse.Content.ReadAsStringAsync());
        var tokenSuper = loginSuperJson.GetProperty("data").GetProperty("token").GetString();

        // 2. 登录楼长管理员，获取旧 token (tokenTarget)
        _client.DefaultRequestHeaders.Authorization = null;
        var loginTargetResponse = await _client.PostAsJsonAsync("/api/auth/login", new { loginName = targetLoginName, password });
        loginTargetResponse.EnsureSuccessStatusCode();
        var loginTargetJson = JsonSerializer.Deserialize<JsonElement>(await loginTargetResponse.Content.ReadAsStringAsync());
        var tokenTarget = loginTargetJson.GetProperty("data").GetProperty("token").GetString();

        // 3. 使用超级管理员 token 停用楼长
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenSuper);
        var disableResponse = await _client.PutAsJsonAsync($"/api/admins/{targetAdminId}/disable", new { reason = "测试停用" });
        disableResponse.EnsureSuccessStatusCode();

        // 4. 验证数据库中的 TokenVersion 已自增为 1
        using (var verifyScope = _factory.Services.CreateScope())
        {
            var verifyContext = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var updatedAdmin = await verifyContext.Admins.FindAsync(targetAdminId);
            Assert.NotNull(updatedAdmin);
            Assert.Equal(1, updatedAdmin.TokenVersion);
        }

        // 5. 验证旧 token 中的 TokenVersion 仍为 0
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(tokenTarget);
        var tokenVersionFromToken = jwtToken.Claims.FirstOrDefault(c => c.Type == "TokenVersion")?.Value;
        Assert.Equal("0", tokenVersionFromToken);

        // 6. 使用旧 token 访问受保护接口，应返回 401
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenTarget);
        var protectedResponse = await _client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, protectedResponse.StatusCode);
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
