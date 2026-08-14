using Microsoft.AspNetCore.Http;
using TemplateDormApi.Data;
using TemplateDormApi.DTO;
using TemplateDormApi.Exceptions;
using TemplateDormApi.Models;
using TemplateDormApi.Repository;
using TemplateDormApi.Services;

namespace TemplateDormApi.Tests;

public sealed class StudentSafetyApprovedImplementationTests
{
    [Fact]
    public async Task RepairCreateAndDetail_UseGeneratedIdAndReturnLogWithAttachments()
    {
        await using var context = TestDbContextFactory.Create();
        AddStudentAccount(context, 101, "20260001");
        AddRoom(context, 201, 1);
        context.BedAllocations.Add(new BedAllocation
        {
            AllocationId = 1,
            StudentId = "20260001",
            RoomId = 201,
            BedNo = 1,
            CheckInDate = DateTime.Now.AddMonths(-1)
        });
        await context.SaveChangesAsync();

        var service = CreateRepairService(context, new FakeFileStorageService());
        var created = await service.CreateAsync(
            101,
            new SubmitRepairTicketRequest
            {
                Description = "宿舍空调无法制冷，需要尽快安排维修",
                Urgency = "紧急"
            },
            CancellationToken.None);

        context.RepairLogs.Add(new RepairLog
        {
            LogId = 1,
            TicketId = created.TicketId,
            AdminId = "A001",
            ProcessDescription = "已检查压缩机",
            ResolveTime = DateTime.Now
        });
        context.RepairAttachments.Add(new RepairAttachment
        {
            AttachmentId = 1,
            TicketId = created.TicketId,
            StorageRef = "2026/08/test.png",
            OriginalName = "fault.png",
            ContentType = "image/png",
            FileSize = 128,
            CreateTime = DateTime.Now
        });
        await context.SaveChangesAsync();

        context.ChangeTracker.Clear();
        var detail = await service.GetByIdAsync(created.TicketId, 101, CancellationToken.None);

        Assert.True(created.TicketId > 0);
        Assert.Equal(201, created.RoomId);
        Assert.Equal("待处理", created.Status);
        Assert.Equal("已检查压缩机", detail.Log?.ProcessDesc);
        Assert.Single(detail.Attachments);
        Assert.Equal("2026/08/test.png", detail.Attachments[0].StorageRef);
    }

    [Fact]
    public async Task RepairCancel_EnforcesStatusDeadlineAndExistingLog()
    {
        await using var context = TestDbContextFactory.Create();
        AddStudentAccount(context, 101, "20260001");
        var now = DateTime.Now;
        context.RepairTickets.AddRange(
            CreateTicket(1, "待处理", now.AddMinutes(-5)),
            CreateTicket(2, "待处理", now.AddMinutes(-11)),
            CreateTicket(3, "已派单", now.AddMinutes(-5), withLog: true));
        await context.SaveChangesAsync();
        var service = CreateRepairService(context, new FakeFileStorageService());

        var cancelled = await service.CancelAsync(1, 101, CancellationToken.None);
        var expired = await Assert.ThrowsAsync<BusinessException>(() =>
            service.CancelAsync(2, 101, CancellationToken.None));
        var started = await Assert.ThrowsAsync<BusinessException>(() =>
            service.CancelAsync(3, 101, CancellationToken.None));

        Assert.Equal("已撤销", cancelled.Status);
        Assert.Equal(StatusCodes.Status409Conflict, expired.HttpStatus);
        Assert.Equal(StatusCodes.Status409Conflict, started.HttpStatus);
    }

    [Fact]
    public async Task RepairAttachments_KeepSuccessfulFilesWhenAnotherFileFails()
    {
        await using var context = TestDbContextFactory.Create();
        AddStudentAccount(context, 101, "20260001");
        context.RepairTickets.Add(CreateTicket(1, "待处理", DateTime.Now));
        await context.SaveChangesAsync();
        var service = CreateRepairService(context, new FakeFileStorageService());
        var files = new List<IFormFile>
        {
            CreateFormFile("ok.png"),
            CreateFormFile("fail.png")
        };

        var uploaded = await service.AddAttachmentsAsync(
            1,
            101,
            new UploadRepairAttachmentsRequest { Files = files },
            CancellationToken.None);

        Assert.Single(uploaded);
        Assert.Equal("ok.png", uploaded[0].OriginalName);
        Assert.Single(context.RepairAttachments);
    }

    [Fact]
    public async Task LateEntryCreate_OnlyAcceptsExistingStudentAfterTwentyThreeThirty()
    {
        await using var context = TestDbContextFactory.Create();
        context.Students.Add(new Student { StudentId = "20260001", Name = "测试学生" });
        AddStudentAccount(context, 101, "20260001");
        context.UserAccounts.Add(new UserAccount
        {
            AccountId = 201,
            LoginName = "admin-a001",
            PasswordHash = "test-only",
            AccountStatus = "正常",
            AdminId = "A001"
        });
        await context.SaveChangesAsync();
        var service = new LateEntryService(
            new LateEntryRepository(context),
            new StudentIdentityService(new UserAccountRepository(context)));
        var lateTime = DateTime.Today.AddDays(-1).AddHours(23).AddMinutes(45);

        var created = await service.CreateAsync(
            201,
            new CreateLateEntryRequest { StudentId = "20260001", RecordTime = lateTime },
            CancellationToken.None);
        var tooEarly = await Assert.ThrowsAsync<BusinessException>(() =>
            service.CreateAsync(
                201,
                new CreateLateEntryRequest
                {
                    StudentId = "20260001",
                    RecordTime = DateTime.Today.AddDays(-1).AddHours(22)
                },
                CancellationToken.None));
        var studentError = await Assert.ThrowsAsync<BusinessException>(() =>
            service.CreateAsync(
                101,
                new CreateLateEntryRequest { StudentId = "20260001", RecordTime = lateTime },
                CancellationToken.None));

        Assert.True(created.RecordId > 0);
        Assert.Null(created.Reason);
        Assert.Equal(StatusCodes.Status400BadRequest, tooEarly.HttpStatus);
        Assert.Equal(StatusCodes.Status403Forbidden, studentError.HttpStatus);
    }

    [Fact]
    public async Task HygieneCreateAndRanking_UseAdminBuildingAndDenseRank()
    {
        await using var context = TestDbContextFactory.Create();
        context.UserAccounts.Add(new UserAccount
        {
            AccountId = 201,
            LoginName = "admin-a001",
            PasswordHash = "test-only",
            AccountStatus = "正常",
            AdminId = "A001"
        });
        context.Admins.Add(new Admin
        {
            AdminId = "A001",
            AdminName = "测试宿管",
            RoleLevel = "宿管",
            BuildingId = 1
        });
        AddRoom(context, 201, 1);
        AddRoom(context, 202, 1);
        AddRoom(context, 203, 1);
        AddRoom(context, 301, 2);
        await context.SaveChangesAsync();
        var service = new HygieneService(new HygieneRepository(context));

        var created = await service.CreateAsync(
            201,
            new CreateHygieneRecordRequest { RoomId = 201, Score = 95, Comment = "整洁" },
            CancellationToken.None);
        context.HygieneRecords.AddRange(
            CreateHygieneRecord(202, 95),
            CreateHygieneRecord(203, 92),
            CreateHygieneRecord(301, 100));
        await context.SaveChangesAsync();

        var ranking = await service.GetRankingsAsync(
            new HygieneRankingQueryDto { YearMonth = DateTime.Now.ToString("yyyy-MM") },
            201,
            isDormAdmin: true,
            CancellationToken.None);

        Assert.True(created.RecordId > 0);
        Assert.Equal("A001", created.InspectorId);
        Assert.Equal(3, ranking.Count);
        Assert.Equal(new[] { 1, 1, 2 }, ranking.Select(item => item.Rank));
        Assert.DoesNotContain(ranking, item => item.RoomId == 301);
    }

    [Fact]
    public async Task StudentReports_ApplyApprovedFeeAndFacilityRules()
    {
        await using var context = TestDbContextFactory.Create();
        AddStudentAccount(context, 101, "20260001");
        context.UtilityFees.AddRange(
            new UtilityFee { FeeId = 1, RoomId = 201, YearMonth = "2026-08", PublishStatus = "已发布" },
            new UtilityFee { FeeId = 2, RoomId = 201, YearMonth = "2026-07", PublishStatus = "已发布" });
        context.FeeDetails.AddRange(
            CreateFeeDetail(1, 1, "20260001", "月度", "是", 10, 20),
            CreateFeeDetail(2, 1, "20260001", "月度", "否", 5, 15),
            CreateFeeDetail(3, 1, "20260001", "退宿", "是", 99, 99),
            CreateFeeDetail(4, 2, "20260001", "月度", "是", 50, 50));
        context.FacilityBookings.AddRange(
            CreateBooking(1, "20260001", new DateTime(2026, 8, 5), "使用中"),
            CreateBooking(2, "20260001", new DateTime(2026, 8, 6), "已完成"),
            CreateBooking(3, "20260001", new DateTime(2026, 8, 7), "已取消"),
            CreateBooking(4, "20260001", new DateTime(2026, 7, 31), "已完成"));
        await context.SaveChangesAsync();
        var service = new StudentReportService(
            new StudentReportRepository(context),
            new StudentIdentityService(new UserAccountRepository(context)));
        var query = new MonthlyReportQueryDto { YearMonth = "2026-08" };

        var fee = await service.GetMonthlyFeeAsync("20260001", 101, query, CancellationToken.None);
        var usage = await service.GetFacilityUsageAsync("20260001", 101, query, CancellationToken.None);

        Assert.Equal(50m, fee.UtilityTotal);
        Assert.Equal(30m, fee.PaidTotal);
        Assert.Equal(2, usage.UsageCount);
    }

    private static RepairService CreateRepairService(AppDbContext context, IFileStorageService storage)
        => new(
            new RepairRepository(context),
            new StudentIdentityService(new UserAccountRepository(context)),
            storage);

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

    private static void AddRoom(AppDbContext context, int roomId, int buildingId)
        => context.Rooms.Add(new Room
        {
            RoomId = roomId,
            BuildingId = buildingId,
            RoomNumber = roomId.ToString(),
            Capacity = 4,
            Occupancy = 1
        });

    private static RepairTicket CreateTicket(
        long id,
        string status,
        DateTime submitTime,
        bool withLog = false)
        => new()
        {
            TicketId = id,
            StudentId = "20260001",
            RoomId = 201,
            IssueDescription = "宿舍水龙头漏水，需要维修处理",
            SubmitTime = submitTime,
            Status = status,
            SlaLevel = "普通",
            Log = withLog
                ? new RepairLog
                {
                    LogId = id,
                    AdminId = "A001",
                    ProcessDescription = "已开始维修",
                    ResolveTime = submitTime
                }
                : null
        };

    private static HygieneRecord CreateHygieneRecord(int roomId, decimal score)
        => new()
        {
            RoomId = roomId,
            CheckDate = DateTime.Now,
            Score = score,
            InspectorId = "A001"
        };

    private static FeeDetail CreateFeeDetail(
        int detailId,
        long feeId,
        string studentId,
        string billType,
        string isPaid,
        decimal water,
        decimal power)
        => new()
        {
            DetailId = detailId,
            FeeId = feeId,
            StudentId = studentId,
            RoomId = 201,
            WaterShare = water,
            PowerShare = power,
            StayDays = 31,
            TotalDays = 31,
            BillType = billType,
            IsPaid = isPaid,
            CreateTime = DateTime.Now
        };

    private static FacilityBooking CreateBooking(long id, string studentId, DateTime startTime, string status)
        => new()
        {
            BookingId = id,
            FacilityId = 1,
            StudentId = studentId,
            CreateTime = startTime.AddDays(-1),
            StartTime = startTime,
            EndTime = startTime.AddHours(1),
            Status = status
        };

    private static IFormFile CreateFormFile(string fileName)
    {
        var content = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
        return new FormFile(new MemoryStream(content), 0, content.Length, "files", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/png"
        };
    }

    private sealed class FakeFileStorageService : IFileStorageService
    {
        public Task<FileUploadResultDto> SaveAsync(IFormFile file, CancellationToken cancellationToken)
        {
            if (file.FileName.StartsWith("fail", StringComparison.OrdinalIgnoreCase))
            {
                throw new BusinessException(400, "模拟单文件上传失败");
            }

            return Task.FromResult(new FileUploadResultDto
            {
                StorageRef = $"2026/08/{file.FileName}"
            });
        }

        public Task<FileUrlDto> GetUrlAsync(string storageRef, string baseUrl, CancellationToken cancellationToken)
            => Task.FromResult(new FileUrlDto { Url = $"{baseUrl}/{storageRef}" });

        public Task<FileDeleteResultDto> DeleteAsync(string storageRef, CancellationToken cancellationToken)
            => Task.FromResult(new FileDeleteResultDto { StorageRef = storageRef, Deleted = true });
    }
}
