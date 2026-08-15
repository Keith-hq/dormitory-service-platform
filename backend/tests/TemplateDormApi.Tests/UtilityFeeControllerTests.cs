using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using TemplateDormApi.Controllers;
using TemplateDormApi.Data;
using TemplateDormApi.DTO;
using TemplateDormApi.Models;
using TemplateDormApi.Security;
using TemplateDormApi.Services;

namespace TemplateDormApi.Tests;

/// <summary>
/// 水电账单控制器测试（难点② DORM-19~25）：
/// 1. 类级 DormAdmin 策略挂载（宿管端写操作）；
/// 2. 路由模板对齐契约；
/// 3. yearMonth 格式/范围校验入口拒绝；
/// 4. 成功路径转发（服务层假实现）。
/// </summary>
public class UtilityFeeControllerTests
{
    // ==================== 鉴权与路由挂载 ====================

    [Fact]
    public void Controller_RequiresDormAdminPolicy()
    {
        var attribute = typeof(UtilityFeeController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .Single();

        Assert.Equal(AuthPolicies.DormAdmin, attribute.Policy);
    }

    [Fact]
    public void Endpoints_MatchContractRoutes()
    {
        AssertRoute(nameof(UtilityFeeController.CreateBill), "utility-fees", "POST");
        AssertRoute(nameof(UtilityFeeController.UpdateBill), "utility-fees/{id:long}", "PUT");
        AssertRoute(nameof(UtilityFeeController.PublishBill), "utility-fees/{id:long}/publish", "POST");
        AssertRoute(nameof(UtilityFeeController.AllocateBill), "utility-fees/{id:long}/allocate", "POST");
        AssertRoute(nameof(UtilityFeeController.GetBillDetails), "utility-fees/{id:long}/details", "GET");
        AssertRoute(nameof(UtilityFeeController.GetBills), "utility-fees", "GET");
        AssertRoute(nameof(UtilityFeeController.GetPowerStatus), "rooms/{roomId:long}/power-status", "GET");
    }

    // ==================== DORM-19 录账单 ====================

    [Fact]
    public async Task CreateBill_Success_ReturnsFeeId()
    {
        var fake = new FakeUtilityFeeService { CreatedFeeId = 70001 };
        var controller = CreateController(fake);

        var result = await controller.CreateBill(new CreateUtilityFeeRequest
        {
            RoomId = 900101,
            YearMonth = "2026-07",
            WaterFee = 30m,
            ElecFee = 70m
        });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(200, GetApiCode(ok.Value));
        Assert.Equal(900101, fake.LastCreateRequest!.RoomId);
        Assert.Equal("2026-07", fake.LastCreateRequest!.YearMonth);
        Assert.Equal(30m, fake.LastCreateRequest!.WaterFee);
        Assert.Equal(70m, fake.LastCreateRequest!.ElecFee);
    }

    [Theory]
    [InlineData("2026-7")]
    [InlineData("2026-13")]
    [InlineData("abc")]
    public async Task CreateBill_InvalidYearMonth_Returns400(string yearMonth)
    {
        var fake = new FakeUtilityFeeService();
        var controller = CreateController(fake);

        var result = await controller.CreateBill(new CreateUtilityFeeRequest
        {
            RoomId = 900101,
            YearMonth = yearMonth,
            WaterFee = 30m,
            ElecFee = 70m
        });

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.False(fake.CreateCalled);
    }

    [Fact]
    public async Task CreateBill_OutOfRangeYearMonth_Returns400()
    {
        var fake = new FakeUtilityFeeService();
        var controller = CreateController(fake);

        var result = await controller.CreateBill(new CreateUtilityFeeRequest
        {
            RoomId = 900101,
            YearMonth = "2019-12",   // 早于 2020-01 下限
            WaterFee = 30m,
            ElecFee = 70m
        });

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.False(fake.CreateCalled);
    }

    // ==================== DORM-20/21/22/23/24 ====================

    [Fact]
    public async Task UpdateBill_Success_Forwards()
    {
        var fake = new FakeUtilityFeeService();
        var controller = CreateController(fake);

        var result = await controller.UpdateBill(5, new UpdateUtilityFeeRequest { WaterFee = 40m, ElecFee = 60m });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(200, GetApiCode(ok.Value));
        Assert.Equal(5, fake.LastUpdateFeeId);
        Assert.Equal(40m, fake.LastUpdateRequest!.WaterFee);
        Assert.Equal(60m, fake.LastUpdateRequest!.ElecFee);
    }

    [Fact]
    public async Task PublishBill_Success_Forwards()
    {
        var fake = new FakeUtilityFeeService();
        var controller = CreateController(fake);

        var result = await controller.PublishBill(6);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(200, GetApiCode(ok.Value));
        Assert.Equal(6, fake.LastPublishFeeId);
    }

    [Fact]
    public async Task AllocateBill_ReturnsAllocateResult()
    {
        var fake = new FakeUtilityFeeService
        {
            AllocateResult = new AllocateResultDto { FeeId = 7, YearMonth = "2026-07", DetailCount = 3 }
        };
        var controller = CreateController(fake);

        var result = await controller.AllocateBill(7);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<AllocateResultDto>>(ok.Value);
        Assert.Equal(3, response.Data!.DetailCount);
    }

    [Fact]
    public async Task GetBillDetails_ReturnsDetails()
    {
        var fake = new FakeUtilityFeeService
        {
            Details = new UtilityFeeDetailsDto { FeeId = 8, YearMonth = "2026-07" }
        };
        var controller = CreateController(fake);

        var result = await controller.GetBillDetails(8);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<UtilityFeeDetailsDto>>(ok.Value);
        Assert.Equal(8, response.Data!.FeeId);
    }

    [Fact]
    public async Task GetBills_InvalidYearMonth_Returns400()
    {
        var fake = new FakeUtilityFeeService();
        var controller = CreateController(fake);

        var result = await controller.GetBills(yearMonth: "bad");

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.False(fake.GetBillsCalled);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetBills_InvalidBuildingId_Returns400(long buildingId)
    {
        var fake = new FakeUtilityFeeService();
        var controller = CreateController(fake);

        var result = await controller.GetBills(buildingId: buildingId);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.False(fake.GetBillsCalled);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetBills_InvalidPage_Returns400(int page)
    {
        var fake = new FakeUtilityFeeService();
        var controller = CreateController(fake);

        var result = await controller.GetBills(page: page);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.False(fake.GetBillsCalled);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task GetBills_InvalidPageSize_Returns400(int pageSize)
    {
        var fake = new FakeUtilityFeeService();
        var controller = CreateController(fake);

        var result = await controller.GetBills(pageSize: pageSize);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.False(fake.GetBillsCalled);
    }

    [Fact]
    public async Task GetBills_ForwardsFilters()
    {
        var fake = new FakeUtilityFeeService();
        var controller = CreateController(fake);

        var result = await controller.GetBills(
            buildingId: 1, yearMonth: "2026-07", isPaid: false, publishStatus: "已发布", page: 2, pageSize: 20);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(200, GetApiCode(ok.Value));
        Assert.Equal(1, fake.LastGetBillsBuildingId);
        Assert.Equal("2026-07", fake.LastGetBillsYearMonth);
        Assert.False(fake.LastGetBillsIsPaid);
        Assert.Equal("已发布", fake.LastGetBillsPublishStatus);
        Assert.Equal(2, fake.LastGetBillsPage);
        Assert.Equal(20, fake.LastGetBillsPageSize);
    }

    // ==================== DORM-25 供电状态 ====================

    [Fact]
    public async Task GetPowerStatus_ForwardsToBillingService()
    {
        var fakeBilling = new FakeBillingService { PowerStatus = "断电" };
        var controller = CreateController(new FakeUtilityFeeService(), fakeBilling);

        var result = await controller.GetPowerStatus(900101);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(200, GetApiCode(ok.Value));
        Assert.Equal(900101, fakeBilling.LastPowerStatusRoomId);
    }

    // ==================== 辅助 ====================

    private static int GetApiCode(object? value)
        => (int)(value!.GetType().GetProperty("Code")!.GetValue(value) ?? 0);

    private static void AssertRoute(string methodName, string expectedTemplate, string expectedVerb)
    {
        var attribute = typeof(UtilityFeeController)
            .GetMethod(methodName)!
            .GetCustomAttributes(true)
            .OfType<HttpMethodAttribute>()
            .Single();

        Assert.Equal(expectedTemplate, attribute.Template);

        var verbs = attribute switch
        {
            HttpGetAttribute => "GET",
            HttpPostAttribute => "POST",
            HttpPutAttribute => "PUT",
            _ => string.Empty
        };
        Assert.Equal(expectedVerb, verbs);
    }

    private static UtilityFeeController CreateController(
        FakeUtilityFeeService service,
        FakeBillingService? billingService = null)
        => new(service, billingService ?? new FakeBillingService());

    private sealed class FakeUtilityFeeService : IUtilityFeeService
    {
        public long CreatedFeeId { get; set; }
        public AllocateResultDto? AllocateResult { get; set; }
        public UtilityFeeDetailsDto? Details { get; set; }

        public bool CreateCalled { get; private set; }
        public bool GetBillsCalled { get; private set; }
        public CreateUtilityFeeRequest? LastCreateRequest { get; private set; }
        public long? LastUpdateFeeId { get; private set; }
        public UpdateUtilityFeeRequest? LastUpdateRequest { get; private set; }
        public long? LastPublishFeeId { get; private set; }
        public long? LastGetBillsBuildingId { get; private set; }
        public string? LastGetBillsYearMonth { get; private set; }
        public bool? LastGetBillsIsPaid { get; private set; }
        public string? LastGetBillsPublishStatus { get; private set; }
        public int LastGetBillsPage { get; private set; }
        public int LastGetBillsPageSize { get; private set; }

        public Task<long> CreateBill(CreateUtilityFeeRequest request)
        {
            CreateCalled = true;
            LastCreateRequest = request;
            return Task.FromResult(CreatedFeeId);
        }

        public Task UpdateBill(long feeId, UpdateUtilityFeeRequest request)
        {
            LastUpdateFeeId = feeId;
            LastUpdateRequest = request;
            return Task.CompletedTask;
        }

        public Task PublishBill(long feeId)
        {
            LastPublishFeeId = feeId;
            return Task.CompletedTask;
        }

        public Task<AllocateResultDto> AllocateBill(long feeId)
            => Task.FromResult(AllocateResult ?? new AllocateResultDto { FeeId = feeId });

        public Task<UtilityFeeDetailsDto> GetBillDetails(long feeId)
            => Task.FromResult(Details ?? new UtilityFeeDetailsDto { FeeId = feeId });

        public Task<PagedResult<UtilityFeeListItemDto>> GetBills(
            long? buildingId, string? yearMonth, bool? isPaid, string? publishStatus, int page, int pageSize)
        {
            GetBillsCalled = true;
            LastGetBillsBuildingId = buildingId;
            LastGetBillsYearMonth = yearMonth;
            LastGetBillsIsPaid = isPaid;
            LastGetBillsPublishStatus = publishStatus;
            LastGetBillsPage = page;
            LastGetBillsPageSize = pageSize;
            return Task.FromResult(new PagedResult<UtilityFeeListItemDto>());
        }
    }

    private sealed class FakeBillingService : IBillingService
    {
        public string PowerStatus { get; set; } = "正常";
        public int? LastPowerStatusRoomId { get; private set; }

        public Task AutoDeduct(int attemptNo, string yearMonth) => Task.CompletedTask;

        public Task CheckPowerCut(string yearMonth) => Task.CompletedTask;

        public Task RestorePower() => Task.CompletedTask;

        public Task<decimal> GetBalance(string studentId) => Task.FromResult(0m);

        public Task<List<WalletLog>> GetWalletLogs(string studentId, string yearMonth)
            => Task.FromResult(new List<WalletLog>());

        public Task<string> GetPowerStatus(int roomId)
        {
            LastPowerStatusRoomId = roomId;
            return Task.FromResult(PowerStatus);
        }
    }
}
