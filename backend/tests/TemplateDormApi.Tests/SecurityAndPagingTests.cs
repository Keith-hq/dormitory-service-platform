using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using TemplateDormApi.DTO;
using TemplateDormApi.Models;
using TemplateDormApi.Repository;
using TemplateDormApi.Security;
using TemplateDormApi.Services;

namespace TemplateDormApi.Tests;

public class SecurityAndPagingTests
{
    [Fact]
    public void ServiceKeyAuthorizationFilter_AcceptsOnlyOneCorrectNonEmptyHeader()
    {
        var filter = new ServiceKeyAuthorizationFilter(CreateConfiguration());

        var accepted = CreateAuthorizationContext(new StringValues("expected-service-key"));
        filter.OnAuthorization(accepted);
        Assert.Null(accepted.Result);

        var wrong = CreateAuthorizationContext(new StringValues("wrong-service-key"));
        filter.OnAuthorization(wrong);
        AssertUnauthorized(wrong.Result);

        var empty = CreateAuthorizationContext(new StringValues(string.Empty));
        filter.OnAuthorization(empty);
        AssertUnauthorized(empty.Result);

        var multiple = CreateAuthorizationContext(new StringValues(new[] { "expected-service-key", "another-value" }));
        filter.OnAuthorization(multiple);
        AssertUnauthorized(multiple.Result);

        var emptyConfiguredKey = new ServiceKeyAuthorizationFilter(CreateConfiguration(string.Empty));
        var withEmptyConfiguredKey = CreateAuthorizationContext(new StringValues("expected-service-key"));
        emptyConfiguredKey.OnAuthorization(withEmptyConfiguredKey);
        AssertUnauthorized(withEmptyConfiguredKey.Result);
    }

    [Theory]
    [InlineData("101", 101)]
    [InlineData("0", null)]
    [InlineData("-1", null)]
    [InlineData("not-a-number", null)]
    [InlineData("2147483648", null)]
    public void CurrentUser_GetAccountId_RejectsInvalidOrOverflowingSub(string sub, int? expectedAccountId)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, sub)
        }, "test"));

        Assert.Equal(expectedAccountId, CurrentUser.GetAccountId(principal));
    }

    [Fact]
    public async Task BuildingService_GetPagedAsync_FillsPageAndPageSize()
    {
        await using var context = TestDbContextFactory.Create();
        context.Buildings.AddRange(
            new Building { BuildingId = 1, BuildingName = "1号楼", BuildingType = "男生宿舍", FloorCount = 6 },
            new Building { BuildingId = 2, BuildingName = "2号楼", BuildingType = "女生宿舍", FloorCount = 6 });
        await context.SaveChangesAsync();

        var service = new BuildingService(new BuildingRepository(context));
        var result = await service.GetPagedAsync(2, 1);

        Assert.Equal(2, result.Page);
        Assert.Equal(1, result.PageSize);
        Assert.Equal(2, result.Total);
        Assert.Single(result.Items);
    }

    private static IConfiguration CreateConfiguration(string sharedServiceKey = "expected-service-key")
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ServiceKey:Shared"] = sharedServiceKey
            })
            .Build();
    }

    private static AuthorizationFilterContext CreateAuthorizationContext(StringValues serviceKeyValues)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Service-Key"] = serviceKeyValues;
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        return new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>());
    }

    private static void AssertUnauthorized(IActionResult? result)
    {
        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        var response = Assert.IsType<ApiResponse<object>>(unauthorized.Value);
        Assert.Equal(401, response.Code);
    }
}
