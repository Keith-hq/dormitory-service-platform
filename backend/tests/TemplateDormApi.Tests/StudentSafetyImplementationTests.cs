using Microsoft.AspNetCore.Http;
using TemplateDormApi.Data;
using TemplateDormApi.DTO;
using TemplateDormApi.Exceptions;
using TemplateDormApi.Models;
using TemplateDormApi.Repository;
using TemplateDormApi.Services;

namespace TemplateDormApi.Tests;

public sealed class StudentSafetyImplementationTests
{
    [Fact]
    public async Task AccommodationQueries_SeparateCurrentAndCompletedRecords()
    {
        await using var context = TestDbContextFactory.Create();
        AddStudentAccount(context, 101, "20260001");
        context.BedAllocations.AddRange(
            new BedAllocation
            {
                AllocationId = 1,
                StudentId = "20260001",
                RoomId = 101,
                BedNo = 1,
                CheckInDate = new DateTime(2025, 9, 1),
                CheckOutDate = new DateTime(2026, 1, 15)
            },
            new BedAllocation
            {
                AllocationId = 2,
                StudentId = "20260001",
                RoomId = 202,
                BedNo = 2,
                CheckInDate = new DateTime(2026, 2, 20)
            });
        await context.SaveChangesAsync();

        var service = CreateStudentProfileService(context);
        var current = await service.GetCurrentAccommodationAsync(
            "20260001", 101, CancellationToken.None);
        var history = await service.GetAccommodationHistoryAsync(
            "20260001", 101, CancellationToken.None);

        Assert.Equal(2, current.AllocationId);
        Assert.Equal(202, current.RoomId);
        Assert.Single(history);
        Assert.Equal(1, history[0].AllocationId);
    }

    [Fact]
    public async Task RepairTicketQuery_ReturnsOnlyOwnedStudentAndUsesPaging()
    {
        await using var context = TestDbContextFactory.Create();
        AddStudentAccount(context, 101, "20260001");
        context.RepairTickets.AddRange(
            CreateTicket(1, "20260001", new DateTime(2026, 8, 10, 8, 0, 0)),
            CreateTicket(2, "20260001", new DateTime(2026, 8, 11, 8, 0, 0)),
            CreateTicket(3, "20260002", new DateTime(2026, 8, 12, 8, 0, 0)));
        await context.SaveChangesAsync();

        var service = new RepairService(
            new RepairRepository(context),
            CreateIdentityService(context),
            new UnusedFileStorageService());
        var result = await service.GetStudentTicketsAsync(
            "20260001",
            101,
            new RepairTicketQueryDto { Page = 1, PageSize = 1 },
            CancellationToken.None);

        Assert.Equal(2, result.Total);
        Assert.Single(result.Items);
        Assert.Equal(2, result.Items[0].TicketId);
    }

    [Fact]
    public async Task LateEntryQueriesAndReasonUpdate_EnforceOwnershipAndTwentyFourHours()
    {
        await using var context = TestDbContextFactory.Create();
        AddStudentAccount(context, 101, "20260001");
        AddStudentAccount(context, 102, "20260002");
        var now = DateTime.Now;
        context.LateEntries.AddRange(
            new LateEntry
            {
                RecordId = 1,
                StudentId = "20260001",
                ReturnTime = now.AddHours(-2)
            },
            new LateEntry
            {
                RecordId = 2,
                StudentId = "20260001",
                ReturnTime = now.AddHours(-25)
            },
            new LateEntry
            {
                RecordId = 3,
                StudentId = "20260002",
                ReturnTime = now.AddHours(-1)
            });
        await context.SaveChangesAsync();

        var service = new LateEntryService(
            new LateEntryRepository(context),
            CreateIdentityService(context));
        var page = await service.GetStudentEntriesAsync(
            "20260001",
            101,
            new LateEntryQueryDto { Page = 1, PageSize = 10 },
            CancellationToken.None);
        var updated = await service.UpdateReasonAsync(
            1,
            101,
            new UpdateLateEntryReasonRequest { Reason = "  校外活动延迟  " },
            CancellationToken.None);

        Assert.Equal(2, page.Total);
        Assert.Equal("校外活动延迟", updated.Reason);

        var ownershipError = await Assert.ThrowsAsync<BusinessException>(() =>
            service.UpdateReasonAsync(
                3,
                101,
                new UpdateLateEntryReasonRequest { Reason = "无权修改" },
                CancellationToken.None));
        Assert.Equal(StatusCodes.Status403Forbidden, ownershipError.HttpStatus);

        var expiredError = await Assert.ThrowsAsync<BusinessException>(() =>
            service.UpdateReasonAsync(
                2,
                101,
                new UpdateLateEntryReasonRequest { Reason = "已经超时" },
                CancellationToken.None));
        Assert.Equal(StatusCodes.Status409Conflict, expiredError.HttpStatus);
    }

    [Fact]
    public async Task HygieneQueriesAndUpdate_ReadCommentAndEnforceTwentyFourHours()
    {
        await using var context = TestDbContextFactory.Create();
        AddStudentAccount(context, 101, "20260001");
        var now = DateTime.Now;
        context.BedAllocations.Add(new BedAllocation
        {
            AllocationId = 1,
            StudentId = "20260001",
            RoomId = 201,
            BedNo = 1,
            CheckInDate = now.AddMonths(-1)
        });
        context.HygieneRecords.AddRange(
            new HygieneRecord
            {
                RecordId = 1,
                RoomId = 201,
                CheckDate = now.AddHours(-2),
                Score = 89.5m,
                InspectorId = "A001",
                Comment = new HygieneComment
                {
                    RecordId = 1,
                    CommentText = "桌面需整理"
                }
            },
            new HygieneRecord
            {
                RecordId = 2,
                RoomId = 201,
                CheckDate = now.AddHours(-25),
                Score = 90,
                InspectorId = "A001"
            });
        await context.SaveChangesAsync();

        var service = new HygieneService(new HygieneRepository(context));
        var records = await service.GetRoomRecordsAsync(
            201,
            101,
            isDormAdmin: false,
            CancellationToken.None);
        var updated = await service.UpdateAsync(
            1,
            new UpdateHygieneRecordRequest { Score = 92, Comment = "整改完成" },
            CancellationToken.None);

        Assert.Equal(2, records.Count);
        Assert.Equal(89.5m, records[0].Score);
        Assert.Equal("桌面需整理", records[0].Comment);
        Assert.Equal(92m, updated.Score);
        Assert.Equal("整改完成", updated.Comment);

        var expiredError = await Assert.ThrowsAsync<BusinessException>(() =>
            service.UpdateAsync(
                2,
                new UpdateHygieneRecordRequest { Score = 95 },
                CancellationToken.None));
        Assert.Equal(StatusCodes.Status409Conflict, expiredError.HttpStatus);
    }

    [Fact]
    public async Task HygieneQuery_RejectsAnotherRoomAndStudentWithoutCurrentAccommodation()
    {
        await using var context = TestDbContextFactory.Create();
        AddStudentAccount(context, 101, "20260001");
        AddStudentAccount(context, 102, "20260002");
        context.BedAllocations.Add(new BedAllocation
        {
            AllocationId = 1,
            StudentId = "20260001",
            RoomId = 201,
            BedNo = 1,
            CheckInDate = DateTime.Now.AddMonths(-1)
        });
        await context.SaveChangesAsync();

        var service = new HygieneService(new HygieneRepository(context));

        var otherRoomError = await Assert.ThrowsAsync<BusinessException>(() =>
            service.GetRoomRecordsAsync(
                202,
                101,
                isDormAdmin: false,
                CancellationToken.None));
        Assert.Equal(StatusCodes.Status403Forbidden, otherRoomError.HttpStatus);

        var noAccommodationError = await Assert.ThrowsAsync<BusinessException>(() =>
            service.GetRoomRecordsAsync(
                201,
                102,
                isDormAdmin: false,
                CancellationToken.None));
        Assert.Equal(StatusCodes.Status403Forbidden, noAccommodationError.HttpStatus);
    }

    [Fact]
    public async Task HygieneQuery_AllowsDormAdminWithoutStudentAccommodation()
    {
        await using var context = TestDbContextFactory.Create();
        context.HygieneRecords.Add(new HygieneRecord
        {
            RecordId = 1,
            RoomId = 201,
            CheckDate = DateTime.Now,
            Score = 91.5m,
            InspectorId = "A001"
        });
        await context.SaveChangesAsync();

        var service = new HygieneService(new HygieneRepository(context));
        var records = await service.GetRoomRecordsAsync(
            201,
            accountId: null,
            isDormAdmin: true,
            CancellationToken.None);

        Assert.Single(records);
        Assert.Equal(91.5m, records[0].Score);
    }

    private static StudentProfileService CreateStudentProfileService(AppDbContext context)
        => new(
            new StudentProfileRepository(context),
            CreateIdentityService(context));

    private static StudentIdentityService CreateIdentityService(AppDbContext context)
        => new(new UserAccountRepository(context));

    private static void AddStudentAccount(AppDbContext context, int accountId, string studentId)
    {
        context.UserAccounts.Add(new UserAccount
        {
            AccountId = accountId,
            LoginName = $"student-{studentId}",
            PasswordHash = "test-only",
            AccountStatus = "正常",
            StudentId = studentId
        });
    }

    private static RepairTicket CreateTicket(long ticketId, string studentId, DateTime submitTime)
        => new()
        {
            TicketId = ticketId,
            StudentId = studentId,
            RoomId = 101,
            IssueDescription = "宿舍水龙头漏水，需要维修处理",
            SubmitTime = submitTime,
            Status = "待处理",
            SlaLevel = "普通"
        };

    private sealed class UnusedFileStorageService : IFileStorageService
    {
        public Task<FileUploadResultDto> SaveAsync(IFormFile file, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<FileUrlDto> GetUrlAsync(string storageRef, string baseUrl, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<FileDeleteResultDto> DeleteAsync(string storageRef, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }
}
