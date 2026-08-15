using Microsoft.EntityFrameworkCore;
using TemplateDormApi.DTO;
using TemplateDormApi.Models;

namespace TemplateDormApi.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Building> Buildings => Set<Building>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<FeeDetail> FeeDetails => Set<FeeDetail>();
    public DbSet<WalletAccount> WalletAccounts => Set<WalletAccount>();
    public DbSet<WalletLog> WalletLogs => Set<WalletLog>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<CreditAccount> CreditAccounts => Set<CreditAccount>();
    public DbSet<CreditLog> CreditLogs => Set<CreditLog>();
    public DbSet<Facility> Facilities => Set<Facility>();
    public DbSet<Notice> Notices => Set<Notice>();
    public DbSet<NoticeDisplay> NoticeDisplays => Set<NoticeDisplay>();
    public DbSet<BedAllocation> BedAllocations => Set<BedAllocation>();
    public DbSet<RepairTicket> RepairTickets => Set<RepairTicket>();
    public DbSet<LateEntry> LateEntries => Set<LateEntry>();
    public DbSet<HygieneRecord> HygieneRecords => Set<HygieneRecord>();
    public DbSet<HygieneComment> HygieneComments => Set<HygieneComment>();
    public DbSet<RepairLog> RepairLogs => Set<RepairLog>();
    public DbSet<RepairAttachment> RepairAttachments => Set<RepairAttachment>();
    public DbSet<UtilityFee> UtilityFees => Set<UtilityFee>();
    public DbSet<FacilityBooking> FacilityBookings => Set<FacilityBooking>();
    public DbSet<Admin> Admins => Set<Admin>();
    public DbSet<PendingRepairTicketDto> PendingRepairTicketDtos => Set<PendingRepairTicketDto>();
    public DbSet<SharedItem> SharedItems => Set<SharedItem>();
    public DbSet<ItemLoan> ItemLoans => Set<ItemLoan>();
    public DbSet<RepairMaterial> RepairMaterials => Set<RepairMaterial>();
    public DbSet<RepairMaterialUsage> RepairMaterialUsages => Set<RepairMaterialUsage>();

    // ===== 住宿全生命周期（刘润东）：离校报备 / 退宿清算 =====
    // 注：BedAllocation、ItemLoan 的 DbSet 由住宿/共享物品模块声明，此处不重复声明。
    public DbSet<LeaveApplication> LeaveApplications => Set<LeaveApplication>();
    public DbSet<CheckoutLog> CheckoutLogs => Set<CheckoutLog>();

    // ===== 退宿三步校验只读数据源（写入方分别为快递/共享物品模块）=====
    public DbSet<ParcelRecord> ParcelRecords => Set<ParcelRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ===== Building 楼栋实体映射（D_Building，C-002 按 D_ 表映射）=====
        modelBuilder.Entity<Building>(entity =>
        {
            entity.ToTable("D_BUILDING");
            entity.HasKey(e => e.BuildingId);
            entity.Property(e => e.BuildingId).HasColumnName("BUILDING_ID");
            entity.Property(e => e.BuildingName).HasColumnName("BUILDING_NAME").HasMaxLength(50).IsRequired();
            entity.Property(e => e.BuildingType).HasColumnName("BUILDING_TYPE").HasMaxLength(20).IsRequired();
            entity.Property(e => e.FloorCount).HasColumnName("TOTAL_FLOORS");
        });

        // ===== Room 房间实体映射（D_Room）=====
        modelBuilder.Entity<Room>(entity =>
        {
            entity.ToTable("D_ROOM");
            entity.HasKey(e => e.RoomId);
            entity.Property(e => e.RoomId).HasColumnName("ROOM_ID")
                  .ValueGeneratedOnAdd();
            entity.Property(e => e.BuildingId).HasColumnName("BUILDING_ID");
            entity.Property(e => e.RoomNumber).HasColumnName("ROOM_NUMBER").HasMaxLength(20).IsRequired();
            entity.Property(e => e.Capacity).HasColumnName("CAPACITY");
            entity.Property(e => e.Occupancy).HasColumnName("OCCUPANCY");
            entity.Property(e => e.Floor).HasColumnName("FLOOR");
            entity.Property(e => e.Status).HasColumnName("STATUS").HasMaxLength(10).IsRequired();
            entity.Property(e => e.PowerStatus).HasColumnName("POWER_STATUS").HasMaxLength(10).IsRequired();

            entity.HasOne<Building>()
                  .WithMany()
                  .HasForeignKey(e => e.BuildingId);
        });

        // ===== Asset 资产实体映射（D_Asset）=====
        modelBuilder.Entity<Asset>(entity =>
        {
            entity.ToTable("D_ASSET");
            entity.HasKey(e => e.AssetId);
            entity.Property(e => e.AssetId).HasColumnName("ASSET_ID");
            entity.Property(e => e.RoomId).HasColumnName("ROOM_ID");
            entity.Property(e => e.AssetName).HasColumnName("ASSET_NAME").HasMaxLength(50).IsRequired();
            entity.Property(e => e.Quantity).HasColumnName("QUANTITY");
            entity.Property(e => e.Status).HasColumnName("STATUS").HasMaxLength(20).IsRequired();

            entity.HasOne<Room>()
                  .WithMany()
                  .HasForeignKey(e => e.RoomId);
        });

        // ===== UserAccount 账户实体映射 =====
        modelBuilder.Entity<UserAccount>(entity =>
        {
            entity.ToTable("D_USER_ACCOUNT");
            entity.HasKey(e => e.AccountId);
            entity.Property(e => e.AccountId).HasColumnName("ACCOUNT_ID");
            entity.Property(e => e.LoginName).HasColumnName("LOGIN_NAME").HasMaxLength(50).IsRequired();
            entity.Property(e => e.PasswordHash).HasColumnName("PASSWORD_HASH").HasMaxLength(255).IsRequired();
            entity.Property(e => e.AccountStatus).HasColumnName("ACCOUNT_STATUS").HasMaxLength(10).IsRequired();
            entity.Property(e => e.StudentId).HasColumnName("STUDENT_ID").HasMaxLength(20);
            entity.Property(e => e.AdminId).HasColumnName("ADMIN_ID").HasMaxLength(20);
        });

        modelBuilder.Entity<Student>(entity =>
        {
            entity.ToTable("D_STUDENT");
            entity.HasKey(e => e.StudentId);
            entity.Property(e => e.StudentId).HasColumnName("STUDENT_ID").HasMaxLength(20);
            entity.Property(e => e.Name).HasColumnName("NAME").HasMaxLength(50).IsRequired();
            entity.Property(e => e.Gender).HasColumnName("GENDER").HasMaxLength(10);
            entity.Property(e => e.MajorId).HasColumnName("MAJOR_ID");
            entity.Property(e => e.Phone).HasColumnName("PHONE").HasMaxLength(20);
            entity.Property(e => e.Email).HasColumnName("EMAIL").HasMaxLength(200);
        });

        modelBuilder.Entity<Admin>(entity =>
        {
            entity.ToTable("D_ADMIN");
            entity.HasKey(e => e.AdminId);
            entity.Property(e => e.AdminId).HasColumnName("ADMIN_ID").HasMaxLength(20);
            entity.Property(e => e.AdminName).HasColumnName("ADMIN_NAME").HasMaxLength(50).IsRequired();
            entity.Property(e => e.Phone).HasColumnName("PHONE").HasMaxLength(20);
            entity.Property(e => e.RoleLevel).HasColumnName("ROLE_LEVEL").HasMaxLength(20).IsRequired();
            entity.Property(e => e.BuildingId).HasColumnName("BUILDING_ID");
        });

        modelBuilder.Entity<BedAllocation>(entity =>
        {
            entity.ToTable("D_BED_ALLOCATION");
            entity.HasKey(e => e.AllocationId);
            // 迁移 023：主键由序列 SEQ_D_BED_ALLOCATION_ID + 触发器（WHEN NEW IS NULL，显式值兼容）生成
            entity.Property(e => e.AllocationId).HasColumnName("ALLOCATION_ID")
                  .ValueGeneratedOnAdd();
            entity.Property(e => e.StudentId).HasColumnName("STUDENT_ID").HasMaxLength(20);
            entity.Property(e => e.RoomId).HasColumnName("ROOM_ID");
            entity.Property(e => e.BedNo).HasColumnName("BED_NO").IsRequired();
            entity.Property(e => e.CheckInDate).HasColumnName("CHECKIN_DATE").IsRequired();
            // CheckOut_Date 作并发令牌（退宿/调寝模块）：同一分配只能被一个事务写入退宿日期，
            // 保证调寝并发"仅一次生效"与退宿幂等（配合 UK_D_BED_ALLOC_ACTIVE 房间床位唯一）。
            entity.Property(e => e.CheckOutDate).HasColumnName("CHECKOUT_DATE")
                  .IsConcurrencyToken();
        });

        modelBuilder.Entity<RepairTicket>(entity =>
        {
            entity.ToTable("D_REPAIR_TICKET");
            entity.HasKey(e => e.TicketId);
            entity.Property(e => e.TicketId).HasColumnName("TICKET_ID").ValueGeneratedOnAdd();
            entity.Property(e => e.StudentId).HasColumnName("STUDENT_ID").HasMaxLength(20);
            entity.Property(e => e.RoomId).HasColumnName("ROOM_ID");
            entity.Property(e => e.IssueDescription).HasColumnName("ISSUE_DESC").HasMaxLength(500).IsRequired();
            entity.Property(e => e.SubmitTime).HasColumnName("SUBMIT_TIME").IsRequired();
            entity.Property(e => e.Status).HasColumnName("STATUS").HasMaxLength(20);
            entity.Property(e => e.SlaLevel).HasColumnName("SLA_LEVEL").HasMaxLength(10).IsRequired();
            entity.Property(e => e.Deadline).HasColumnName("DEADLINE");
            entity.Property(e => e.AssignedTo).HasColumnName("ASSIGNED_TO").HasMaxLength(20);
            // 难点⑤ 迁移 021 新增列：SLA 首次升级标记（NULL=未升级）
            entity.Property(e => e.EscalationTime).HasColumnName("ESCALATION_TIME");

            entity.HasOne(e => e.Log)
                .WithOne()
                .HasForeignKey<RepairLog>(e => e.TicketId);
            entity.HasMany(e => e.Attachments)
                .WithOne()
                .HasForeignKey(e => e.TicketId);
        });

        modelBuilder.Entity<RepairLog>(entity =>
        {
            entity.ToTable("D_REPAIR_LOG");
            entity.HasKey(e => e.LogId);
            entity.Property(e => e.LogId).HasColumnName("LOG_ID");
            entity.Property(e => e.TicketId).HasColumnName("TICKET_ID");
            entity.Property(e => e.AdminId).HasColumnName("ADMIN_ID").HasMaxLength(20);
            entity.Property(e => e.ProcessDescription).HasColumnName("PROCESS_DESC").HasMaxLength(500);
            // 难点⑤ 迁移 021 新增列：完工结果（对齐契约 result 字段）
            entity.Property(e => e.RepairResult).HasColumnName("REPAIR_RESULT").HasMaxLength(200);
            entity.Property(e => e.ResolveTime).HasColumnName("RESOLVE_TIME").IsRequired();
        });

        modelBuilder.Entity<RepairAttachment>(entity =>
        {
            entity.ToTable("D_REPAIR_ATTACHMENT");
            entity.HasKey(e => e.AttachmentId);
            entity.Property(e => e.AttachmentId).HasColumnName("ATTACHMENT_ID").ValueGeneratedOnAdd();
            entity.Property(e => e.TicketId).HasColumnName("TICKET_ID").IsRequired();
            entity.Property(e => e.StorageRef).HasColumnName("STORAGE_REF").HasMaxLength(500).IsRequired();
            entity.Property(e => e.OriginalName).HasColumnName("ORIGINAL_NAME").HasMaxLength(255);
            entity.Property(e => e.ContentType).HasColumnName("CONTENT_TYPE").HasMaxLength(100);
            entity.Property(e => e.FileSize).HasColumnName("FILE_SIZE");
            entity.Property(e => e.CreateTime).HasColumnName("CREATE_TIME").HasDefaultValueSql("SYSDATE").ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<LateEntry>(entity =>
        {
            entity.ToTable("D_LATE_ENTRY");
            entity.HasKey(e => e.RecordId);
            entity.Property(e => e.RecordId).HasColumnName("RECORD_ID").ValueGeneratedOnAdd();
            entity.Property(e => e.StudentId).HasColumnName("STUDENT_ID").HasMaxLength(20);
            entity.Property(e => e.ReturnTime).HasColumnName("RETURN_TIME").IsRequired();
            entity.Property(e => e.Reason).HasColumnName("REASON").HasMaxLength(200);
        });

        modelBuilder.Entity<HygieneRecord>(entity =>
        {
            entity.ToTable("D_HYGIENE_RECORD");
            entity.HasKey(e => e.RecordId);
            entity.Property(e => e.RecordId).HasColumnName("RECORD_ID").ValueGeneratedOnAdd();
            entity.Property(e => e.RoomId).HasColumnName("ROOM_ID");
            entity.Property(e => e.CheckDate).HasColumnName("CHECK_DATE").IsRequired();
            entity.Property(e => e.Score).HasColumnName("SCORE").HasPrecision(4, 1).IsRequired();
            entity.Property(e => e.InspectorId).HasColumnName("INSPECTOR_ID").HasMaxLength(20);

            entity.HasOne(e => e.Comment)
                .WithOne(e => e.Record)
                .HasForeignKey<HygieneComment>(e => e.RecordId);
        });

        modelBuilder.Entity<HygieneComment>(entity =>
        {
            entity.ToTable("D_HYGIENE_COMMENT");
            entity.HasKey(e => e.RecordId);
            entity.Property(e => e.RecordId).HasColumnName("RECORD_ID").ValueGeneratedNever();
            entity.Property(e => e.CommentText).HasColumnName("COMMENT").HasMaxLength(500);
        });

        modelBuilder.Entity<UtilityFee>(entity =>
        {
            entity.ToTable("D_UTILITY_FEE");
            entity.HasKey(e => e.FeeId);
            // 主键由 SEQ_D_UTILITY_FEE_ID + TRG_D_UTILITY_FEE_ID_BI 回填（迁移 023），
            // 与 Room 同一模式：EF 插入后经 RETURNING 读回生成值
            entity.Property(e => e.FeeId).HasColumnName("FEE_ID")
                  .ValueGeneratedOnAdd();
            entity.Property(e => e.RoomId).HasColumnName("ROOM_ID");
            entity.Property(e => e.YearMonth).HasColumnName("YEAR_MONTH").HasMaxLength(10).IsRequired();
            entity.Property(e => e.WaterFee).HasColumnName("WATER_FEE").HasPrecision(8, 2);
            entity.Property(e => e.PowerFee).HasColumnName("POWER_FEE").HasPrecision(8, 2);
            entity.Property(e => e.IsPaid).HasColumnName("IS_PAID").HasMaxLength(10);
            entity.Property(e => e.PublishStatus).HasColumnName("PUBLISH_STATUS").HasMaxLength(10).IsRequired();
        });

        modelBuilder.Entity<FacilityBooking>(entity =>
        {
            entity.ToTable("D_FACILITY_BOOKING");
            entity.HasKey(e => e.BookingId);
            entity.Property(e => e.BookingId).HasColumnName("BOOKING_ID");
            entity.Property(e => e.FacilityId).HasColumnName("FACILITY_ID");
            entity.Property(e => e.StudentId).HasColumnName("STUDENT_ID").HasMaxLength(20).IsRequired();
            entity.Property(e => e.CreateTime).HasColumnName("CREATE_TIME").IsRequired();
            entity.Property(e => e.StartTime).HasColumnName("START_TIME");
            entity.Property(e => e.EndTime).HasColumnName("END_TIME");
            entity.Property(e => e.Status).HasColumnName("STATUS").HasMaxLength(10).IsRequired();
        });

        // ===== Notification 通知实体映射 =====
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("D_NOTIFICATION");
            entity.HasKey(e => e.NotificationId);
            entity.Property(e => e.NotificationId)
                .HasColumnName("NOTIFICATION_ID")
                .ValueGeneratedOnAdd();
            entity.Property(e => e.RecipientAccountId)
                .HasColumnName("RECIPIENT_ACCOUNT_ID")
                .IsRequired();
            entity.Property(e => e.Title)
                .HasColumnName("TITLE")
                .HasMaxLength(100)
                .IsRequired();
            entity.Property(e => e.Content)
                .HasColumnName("CONTENT")
                .HasMaxLength(1000)
                .IsRequired();
            entity.Property(e => e.NotificationType)
                .HasColumnName("NOTIFICATION_TYPE")
                .HasMaxLength(20)
                .IsRequired();
            entity.Property(e => e.ReadTime).HasColumnName("READ_TIME");
            entity.Property(e => e.CreateTime)
                .HasColumnName("CREATE_TIME")
                .HasDefaultValueSql("SYSDATE")
                .ValueGeneratedOnAdd()
                .IsRequired();

            entity.HasOne<UserAccount>()
                .WithMany()
                .HasForeignKey(e => e.RecipientAccountId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_D_NOTIFICATION_ACCOUNT");
        });

        // ===== CreditAccount 信用分账户实体映射 =====
        modelBuilder.Entity<CreditAccount>(entity =>
        {
            entity.ToTable("D_CREDIT_ACCOUNT");
            entity.HasKey(e => e.StudentId);
            entity.Property(e => e.StudentId)
                .HasColumnName("STUDENT_ID")
                .HasMaxLength(20);
            entity.Property(e => e.CurrentScore)
                .HasColumnName("CURRENT_SCORE")
                .HasDefaultValue(100)
                .IsRequired();
            entity.Property(e => e.UpdatedTime)
                .HasColumnName("UPDATED_TIME")
                .HasDefaultValueSql("SYSDATE")
                .ValueGeneratedOnAdd()
                .IsRequired();
        });

        // ===== CreditLog 信用分流水实体映射 =====
        modelBuilder.Entity<CreditLog>(entity =>
        {
            entity.ToTable("D_CREDIT_LOG");
            entity.HasKey(e => e.LogId);
            entity.Property(e => e.LogId)
                .HasColumnName("LOG_ID")
                .ValueGeneratedOnAdd();
            entity.Property(e => e.StudentId)
                .HasColumnName("STUDENT_ID")
                .HasMaxLength(20)
                .IsRequired();
            entity.Property(e => e.ScoreChange)
                .HasColumnName("SCORE_CHANGE")
                .IsRequired();
            entity.Property(e => e.Reason)
                .HasColumnName("REASON")
                .HasMaxLength(200)
                .IsRequired();
            entity.Property(e => e.EventKey)
                .HasColumnName("EVENT_KEY")
                .HasMaxLength(100)
                .IsRequired();
            entity.Property(e => e.CreateTime)
                .HasColumnName("CREATE_TIME")
                .HasDefaultValueSql("SYSDATE")
                .ValueGeneratedOnAdd()
                .IsRequired();

            entity.HasOne<CreditAccount>()
                .WithMany()
                .HasForeignKey(e => e.StudentId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_D_CREDIT_LOG_ACCOUNT");
        });

        // ===== Facility 公共设施实体映射（D_FACILITY，主键由序列+触发器生成）=====
        modelBuilder.Entity<Facility>(entity =>
        {
            entity.ToTable("D_FACILITY");
            entity.HasKey(e => e.FacilityId);
            entity.Property(e => e.FacilityId)
                .HasColumnName("FACILITY_ID")
                .ValueGeneratedOnAdd();
            entity.Property(e => e.BuildingId)
                .HasColumnName("BUILDING_ID")
                .IsRequired();
            entity.Property(e => e.FacilityCode)
                .HasColumnName("FACILITY_CODE")
                .HasMaxLength(30)
                .IsRequired();
            entity.Property(e => e.FacilityType)
                .HasColumnName("FACILITY_TYPE")
                .HasMaxLength(30)
                .IsRequired();
            entity.Property(e => e.Status)
                .HasColumnName("STATUS")
                .HasMaxLength(10)
                .IsRequired();

            entity.HasOne<Building>()
                .WithMany()
                .HasForeignKey(e => e.BuildingId)
                .HasConstraintName("FK_D_FACILITY_BUILDING");
        });

        // ===== Notice 公告实体映射（D_NOTICE，主键由序列+触发器生成）=====
        modelBuilder.Entity<Notice>(entity =>
        {
            entity.ToTable("D_NOTICE");
            entity.HasKey(e => e.NoticeId);
            entity.Property(e => e.NoticeId)
                .HasColumnName("NOTICE_ID")
                .ValueGeneratedOnAdd();
            entity.Property(e => e.AdminId).HasColumnName("ADMIN_ID").HasMaxLength(20);
            entity.Property(e => e.Title).HasColumnName("TITLE").HasMaxLength(100).IsRequired();
            entity.Property(e => e.Content).HasColumnName("CONTENT").HasMaxLength(1000).IsRequired();
            entity.Property(e => e.PublishTime).HasColumnName("PUBLISH_TIME").IsRequired();

            entity.HasOne(n => n.Display)
                .WithOne(d => d.Notice)
                .HasForeignKey<NoticeDisplay>(d => d.NoticeId);
        });

        // ===== NoticeDisplay 公告置顶（1:1，对应 D_NOTICE_DISPLAY，共享主键 Notice_ID）=====
        // NoticeId 不设 ValueGeneratedOnAdd：置顶行必须引用父公告的 Notice_ID，
        // EF 会在 1:1 共享主键关系中自动把 Notice 生成的主键传播过来。
        modelBuilder.Entity<NoticeDisplay>(entity =>
        {
            entity.ToTable("D_NOTICE_DISPLAY");
            entity.HasKey(e => e.NoticeId);
            entity.Property(e => e.NoticeId).HasColumnName("NOTICE_ID");
            entity.Property(e => e.IsPinned).HasColumnName("IS_PINNED").HasMaxLength(10).IsRequired();
            entity.Property(e => e.PinTime).HasColumnName("PIN_TIME");
        });

        // ===== LeaveApplication 离校报备（D_LEAVE_APPLICATION，迁移 017 加 REASON 列）=====
        modelBuilder.Entity<LeaveApplication>(entity =>
        {
            entity.ToTable("D_LEAVE_APPLICATION");
            entity.HasKey(e => e.ApplyId);
            // 迁移 023：主键由序列 SEQ_D_LEAVE_APPLICATION_ID + 触发器（WHEN NEW IS NULL，显式值兼容）生成
            entity.Property(e => e.ApplyId).HasColumnName("APPLY_ID")
                  .ValueGeneratedOnAdd();
            entity.Property(e => e.StudentId).HasColumnName("STUDENT_ID").HasMaxLength(20);
            entity.Property(e => e.LeaveDate).HasColumnName("LEAVE_DATE").IsRequired();
            entity.Property(e => e.ReturnDate).HasColumnName("RETURN_DATE").IsRequired();
            entity.Property(e => e.Destination).HasColumnName("DESTINATION").HasMaxLength(200).IsRequired();
            entity.Property(e => e.Status).HasColumnName("STATUS").HasMaxLength(20).IsRequired().HasDefaultValue("待批");
            entity.Property(e => e.Reason).HasColumnName("REASON").HasMaxLength(200);
        });

        // ===== CheckoutLog 退宿清算（D_CHECKOUT_LOG）=====
        // Status 作并发令牌：并发 confirm/cancel 只有一个生效，另一个重读后按幂等语义返回。
        modelBuilder.Entity<CheckoutLog>(entity =>
        {
            entity.ToTable("D_CHECKOUT_LOG");
            entity.HasKey(e => e.LogId);
            // 迁移 023：主键由序列 SEQ_D_CHECKOUT_LOG_ID + 触发器（WHEN NEW IS NULL，显式值兼容）生成
            entity.Property(e => e.LogId).HasColumnName("LOG_ID")
                  .ValueGeneratedOnAdd();
            entity.Property(e => e.AllocationId).HasColumnName("ALLOCATION_ID").IsRequired();
            entity.Property(e => e.RequestTime).HasColumnName("REQUEST_TIME").IsRequired();
            entity.Property(e => e.ResultTime).HasColumnName("RESULT_TIME");
            entity.Property(e => e.FeeCheck).HasColumnName("FEE_CHECK").HasMaxLength(10);
            entity.Property(e => e.ItemCheck).HasColumnName("ITEM_CHECK").HasMaxLength(10);
            entity.Property(e => e.Status).HasColumnName("STATUS").HasMaxLength(10).IsRequired().IsConcurrencyToken();
            entity.Property(e => e.RejectReason).HasColumnName("REJECT_REASON").HasMaxLength(500);
        });

        // ===== 三步校验只读数据源（不配置导航/外键，仅按列读写）=====
        modelBuilder.Entity<ParcelRecord>(entity =>
        {
            entity.ToTable("D_PARCEL_RECORD");
            entity.HasKey(e => e.ParcelId);
            entity.Property(e => e.ParcelId).HasColumnName("PARCEL_ID");
            entity.Property(e => e.StudentId).HasColumnName("STUDENT_ID").HasMaxLength(20);
            entity.Property(e => e.ArriveTime).HasColumnName("ARRIVE_TIME");
            entity.Property(e => e.PickupTime).HasColumnName("PICKUP_TIME");
            entity.Property(e => e.CourierCompany).HasColumnName("COURIER_COMPANY").HasMaxLength(50);
        });

        // ===== PendingRepairTicketDto：DORM-26 列表投影（无键，仅供 SqlQueryRaw 查询）=====
        modelBuilder.Entity<PendingRepairTicketDto>(entity =>
        {
            entity.HasNoKey();
        });

        // ===== SharedItem 共享物品实体映射（D_Shared_Item，难点④）=====
        // 注：Item_ID 实际由 SP 内部 SEQ_ITEM_LOAN 生成，非 IDENTITY；
        // ValueGeneratedOnAdd 仅用于 EF 不主动发送该列，写入全走 SP 不受影响。
        modelBuilder.Entity<SharedItem>(entity =>
        {
            entity.ToTable("D_SHARED_ITEM");
            entity.HasKey(e => e.ItemId);
            entity.Property(e => e.ItemId).HasColumnName("ITEM_ID")
                  .ValueGeneratedOnAdd();
            entity.Property(e => e.ItemName).HasColumnName("ITEM_NAME").HasMaxLength(50).IsRequired();
            entity.Property(e => e.BuildingId).HasColumnName("BUILDING_ID");
            entity.Property(e => e.TotalQty).HasColumnName("TOTAL_QTY");
            entity.Property(e => e.AvailableQty).HasColumnName("AVAILABLE_QTY");
            entity.Property(e => e.Status).HasColumnName("STATUS").HasMaxLength(10).IsRequired();
        });

        // ===== ItemLoan 共享物品借还记录实体映射（D_Item_Loan，难点④）=====
        // 注：写入必须走存储过程（Loan_ID 由 SEQ_ITEM_LOAN 生成，
        // Idempotency_Key 由 SP 落库并受唯一索引 UK_D_ITEM_LOAN_IDEM 兜底，迁移 019）。
        modelBuilder.Entity<ItemLoan>(entity =>
        {
            entity.ToTable("D_ITEM_LOAN");
            entity.HasKey(e => e.LoanId);
            entity.Property(e => e.LoanId).HasColumnName("LOAN_ID")
                  .ValueGeneratedOnAdd();
            entity.Property(e => e.ItemId).HasColumnName("ITEM_ID");
            entity.Property(e => e.StudentId).HasColumnName("STUDENT_ID").HasMaxLength(20).IsRequired();
            entity.Property(e => e.BorrowTime).HasColumnName("BORROW_TIME").IsRequired();
            entity.Property(e => e.DueTime).HasColumnName("DUE_TIME").IsRequired();
            entity.Property(e => e.ReturnTime).HasColumnName("RETURN_TIME");
            entity.Property(e => e.IdempotencyKey).HasColumnName("IDEMPOTENCY_KEY").HasMaxLength(100);
        });

        // ===== RepairMaterial 维修耗材实体映射（D_Repair_Material，难点④）=====
        modelBuilder.Entity<RepairMaterial>(entity =>
        {
            entity.ToTable("D_REPAIR_MATERIAL");
            entity.HasKey(e => e.MaterialId);
            entity.Property(e => e.MaterialId).HasColumnName("MATERIAL_ID")
                  .ValueGeneratedOnAdd();
            entity.Property(e => e.MaterialName).HasColumnName("MATERIAL_NAME").HasMaxLength(50).IsRequired();
            entity.Property(e => e.Unit).HasColumnName("UNIT").HasMaxLength(20).IsRequired();
            entity.Property(e => e.StockQty).HasColumnName("STOCK_QTY");
        });

        // ===== RepairMaterialUsage 维修耗材消耗记录实体映射（D_Repair_Material_Usage，难点④）=====
        // 注：写入必须走存储过程（Usage_ID 由 SEQ_MATERIAL_USAGE 生成，
        // Idempotency_Key 由 SP 落库并受唯一索引 UK_D_REPAIR_MAT_USE_IDEM 兜底，迁移 019）。
        modelBuilder.Entity<RepairMaterialUsage>(entity =>
        {
            entity.ToTable("D_REPAIR_MATERIAL_USAGE");
            entity.HasKey(e => e.UsageId);
            entity.Property(e => e.UsageId).HasColumnName("USAGE_ID")
                  .ValueGeneratedOnAdd();
            entity.Property(e => e.TicketId).HasColumnName("TICKET_ID");
            entity.Property(e => e.MaterialId).HasColumnName("MATERIAL_ID");
            entity.Property(e => e.Quantity).HasColumnName("QUANTITY");
            entity.Property(e => e.UseTime).HasColumnName("USE_TIME");
            entity.Property(e => e.IdempotencyKey).HasColumnName("IDEMPOTENCY_KEY").HasMaxLength(100);
        });
    }
}
