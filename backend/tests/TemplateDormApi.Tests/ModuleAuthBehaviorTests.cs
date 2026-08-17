using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using TemplateDormApi.Data;
using TemplateDormApi.Models;

namespace TemplateDormApi.Tests;

/// <summary>
/// 鉴权行为全链路验证（评审整改 S2，TestWebApplicationFactory + 真实登录发 token）：
/// 未登录访问新保护接口 → 401；学生 token 访问宿管端接口 → 403；
/// 学生 A 操作学生 B 资源（入住/报备）→ 403（服务层归属校验经 ExceptionMiddleware 透出）。
/// </summary>
public class ModuleAuthBehaviorTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ModuleAuthBehaviorTests(TestWebApplicationFactory factory) => _factory = factory;

    private HttpClient CreateClient() => _factory.CreateClient();

    private static async Task<string> LoginAsync(HttpClient client, string loginName, string password = "Test@123")
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { loginName, password });
        response.EnsureSuccessStatusCode();
        var json = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
        return json.GetProperty("data").GetProperty("token").GetString()!;
    }

    /// <summary>造学生 + 登录账户（IsFirstLogin=N 保证登录直接可用），返回 (studentId, loginName)</summary>
    private async Task<(string StudentId, string LoginName)> SeedStudentAsync(string suffix)
    {
        var studentId = $"STU_{suffix}";
        var loginName = $"stu_{suffix}";
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        context.Students.Add(new Student { StudentId = studentId, Name = $"测试学生{suffix}" });
        context.UserAccounts.Add(new UserAccount
        {
            LoginName = loginName,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test@123"),
            AccountStatus = "正常",
            StudentId = studentId,
            IsFirstLogin = "N"
        });
        await context.SaveChangesAsync();
        return (studentId, loginName);
    }

    /// <summary>未登录访问受保护接口 → 401（学生自助仅登录 + 宿管端均覆盖）</summary>
    [Fact]
    public async Task Unauthenticated_ProtectedEndpoints_Return401()
    {
        var client = CreateClient();

        // 学生自助（仅 [Authorize]）
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync("/api/allocations/1/checkout-register", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/checkouts/1")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync("/api/leave-applications", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync("/api/allocations", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/students/S001/leave-applications")).StatusCode);

        // 宿管端（DormAdmin 策略）
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/buildings")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/rooms/1")).StatusCode);
    }

    /// <summary>学生 token（role=student）访问宿管端接口 → 403（DormAdmin 策略要求 admin/super_admin）</summary>
    [Fact]
    public async Task StudentToken_DormAdminEndpoints_Return403()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (_, loginName) = await SeedStudentAsync(suffix);
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await LoginAsync(client, loginName));

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/buildings")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/buildings/1")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/rooms/1")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/rooms/1/occupants")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/allocations/1/transfer",
            new { targetRoomId = 2, targetBedNo = 1 })).StatusCode);
        // 辅导员端报备列表/审批/统计（COUN-01~04）
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/leave-applications")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync("/api/leave-applications/1/approve", new { })).StatusCode);
    }

    /// <summary>学生 A 替学生 B 办理入住 → 403（归属校验在服务层，业务异常透出 403）</summary>
    [Fact]
    public async Task StudentA_CreatesAllocationForStudentB_Returns403()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        await SeedStudentAsync(suffix + "a");
        var (studentB, _) = await SeedStudentAsync(suffix + "b");

        // 造房间（InMemory 不校验楼栋外键，房间存在即可）
        var roomId = Convert.ToInt32(suffix[..6], 16);
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.Rooms.Add(new Room
            {
                RoomId = roomId,
                BuildingId = 1,
                RoomNumber = $"R{suffix[..6]}",
                Capacity = 4,
                Occupancy = 0,
                Status = "正常",
                PowerStatus = "正常"
            });
            await context.SaveChangesAsync();
        }

        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await LoginAsync(client, $"stu_{suffix}a"));

        var response = await client.PostAsJsonAsync("/api/allocations", new
        {
            studentId = studentB,
            roomId,
            bedNo = 1,
            checkInDate = new DateTime(2026, 8, 17)
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>学生 A 替学生 B 提交离校报备 → 403</summary>
    [Fact]
    public async Task StudentA_SubmitsLeaveForStudentB_Returns403()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        await SeedStudentAsync(suffix + "a");
        var (studentB, _) = await SeedStudentAsync(suffix + "b");

        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await LoginAsync(client, $"stu_{suffix}a"));

        var response = await client.PostAsJsonAsync("/api/leave-applications", new
        {
            studentId = studentB,
            leaveDate = new DateTime(2026, 8, 18),
            returnDate = new DateTime(2026, 8, 20),
            destination = "北京"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
