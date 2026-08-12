using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TemplateDormApi.Controllers;
using TemplateDormApi.DTO;
using TemplateDormApi.Exceptions;
using TemplateDormApi.Models;
using TemplateDormApi.Repository;
using TemplateDormApi.Services;

namespace TemplateDormApi.Tests;

public sealed class StudentIdentityAndProfileTests
{
    [Fact]
    public async Task EnsureOwnStudentIdAsync_AllowsBoundStudentAndRejectsAnotherStudent()
    {
        await using var context = TestDbContextFactory.Create();
        context.UserAccounts.Add(new UserAccount
        {
            AccountId = 101,
            LoginName = "student-001",
            PasswordHash = "test-only",
            AccountStatus = "正常",
            StudentId = "20260001"
        });
        await context.SaveChangesAsync();

        var service = new StudentIdentityService(new UserAccountRepository(context));

        await service.EnsureOwnStudentIdAsync(101, "20260001", CancellationToken.None);

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            service.EnsureOwnStudentIdAsync(101, "20260002", CancellationToken.None));
        Assert.Equal(StatusCodes.Status403Forbidden, exception.HttpStatus);
    }

    [Fact]
    public async Task UpdateProfileAsync_PersistsEmailAndAcceptsNull()
    {
        await using var context = TestDbContextFactory.Create();
        context.UserAccounts.Add(new UserAccount
        {
            AccountId = 101,
            LoginName = "student-001",
            PasswordHash = "test-only",
            AccountStatus = "正常",
            StudentId = "20260001"
        });
        context.Students.Add(new Student
        {
            StudentId = "20260001",
            Name = "测试学生",
            Phone = "13800000000"
        });
        await context.SaveChangesAsync();

        var identityService = new StudentIdentityService(new UserAccountRepository(context));
        var service = new StudentProfileService(new StudentProfileRepository(context), identityService);

        var updated = await service.UpdateProfileAsync(
            "20260001",
            101,
            new UpdateStudentProfileRequest
            {
                Phone = "13900000000",
                Email = "student@example.com"
            },
            CancellationToken.None);

        Assert.Equal("student@example.com", updated.Email);
        var stored = await context.Students.AsNoTracking().SingleAsync();
        Assert.Equal("student@example.com", stored.Email);
        Assert.Equal("13900000000", stored.Phone);

        await service.UpdateProfileAsync(
            "20260001",
            101,
            new UpdateStudentProfileRequest { Phone = stored.Phone, Email = null },
            CancellationToken.None);

        context.ChangeTracker.Clear();
        Assert.Null((await context.Students.AsNoTracking().SingleAsync()).Email);
    }

    [Fact]
    public async Task StudentProfileController_Returns401WhenAccountClaimIsMissing()
    {
        await using var context = TestDbContextFactory.Create();
        var identityService = new StudentIdentityService(new UserAccountRepository(context));
        var service = new StudentProfileService(new StudentProfileRepository(context), identityService);
        var controller = new StudentProfileController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var action = await controller.GetCurrentAccommodation("20260001", CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(action.Result);
        var response = Assert.IsType<ApiResponse<object>>(unauthorized.Value);
        Assert.Equal(StatusCodes.Status401Unauthorized, response.Code);
    }
}
