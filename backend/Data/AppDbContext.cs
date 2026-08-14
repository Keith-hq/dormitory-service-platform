using Microsoft.EntityFrameworkCore;
using TemplateDormApi.Models;

namespace TemplateDormApi.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // ===== 基础表 =====
    public DbSet<Building> Buildings => Set<Building>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Admin> Admins => Set<Admin>();
    public DbSet<College> Colleges => Set<College>();
    public DbSet<Major> Majors => Set<Major>();
    public DbSet<Notice> Notices => Set<Notice>();
    public DbSet<NoticeDisplay> NoticeDisplays => Set<NoticeDisplay>();
    public DbSet<VisitorLog> VisitorLogs => Set<VisitorLog>();
    public DbSet<UtilityFee> UtilityFees => Set<UtilityFee>();
    public DbSet<HygieneRecord> HygieneRecords => Set<HygieneRecord>();
    public DbSet<HygieneComment> HygieneComments => Set<HygieneComment>();
    public DbSet<WaterOrder> WaterOrders => Set<WaterOrder>();
    public DbSet<BedAllocation> BedAllocations => Set<BedAllocation>();
    public DbSet<RepairTicket> RepairTickets => Set<RepairTicket>();
    public DbSet<RepairLog> RepairLogs => Set<RepairLog>();
    public DbSet<RepairAttachment> RepairAttachments => Set<RepairAttachment>();
    public DbSet<LateEntry> LateEntries => Set<LateEntry>();
    public DbSet<AccessLog> AccessLogs => Set<AccessLog>();
    public DbSet<LeaveApplication> LeaveApplications => Set<LeaveApplication>();
    public DbSet<ParcelRecord> ParcelRecords => Set<ParcelRecord>();
    public DbSet<ViolationRecord> ViolationRecords => Set<ViolationRecord>();

    // ===== 扩展表 =====
    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<CreditAccount> CreditAccounts => Set<CreditAccount>();
    public DbSet<CreditLog> CreditLogs => Set<CreditLog>();
    public DbSet<Facility> Facilities => Set<Facility>();
    public DbSet<FacilityBooking> FacilityBookings => Set<FacilityBooking>();
    public DbSet<SharedItem> SharedItems => Set<SharedItem>();
    public DbSet<ItemLoan> ItemLoans => Set<ItemLoan>();
    public DbSet<RepairMaterial> RepairMaterials => Set<RepairMaterial>();
    public DbSet<RepairMaterialUsage> RepairMaterialUsages => Set<RepairMaterialUsage>();
    public DbSet<FeeDetail> FeeDetails => Set<FeeDetail>();
    public DbSet<WalletAccount> WalletAccounts => Set<WalletAccount>();
    public DbSet<WalletLog> WalletLogs => Set<WalletLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ============================================================
        // 1. 基础表映射
        // ============================================================

        // ---- College 学院 ----
        modelBuilder.Entity<College>(entity =>
        {
            entity.ToTable("D_COLLEGE");
            entity.HasKey(e => e.CollegeId);
            entity.Property(e => e.CollegeId).HasColumnName("COLLEGE_ID");
            entity.Property(e => e.CollegeName).HasColumnName("COLLEGE_NAME").HasMaxLength(100).IsRequired();
            entity.Property(e => e.CounselorName).HasColumnName("COUNSELOR_NAME").HasMaxLength(50);
            entity.Property(e => e.ContactPhone).HasColumnName("CONTACT_PHONE").HasMaxLength(20);
        });

        // ---- Building 楼栋 ----
        modelBuilder.Entity<Building>(entity =>
        {
            entity.ToTable("D_BUILDING");
            entity.HasKey(e => e.BuildingId);
            entity.Property(e => e.BuildingId).HasColumnName("BUILDING_ID");
            entity.Property(e => e.BuildingName).HasColumnName("BUILDING_NAME").HasMaxLength(50).IsRequired();
            entity.Property(e => e.BuildingType).HasColumnName("BUILDING_TYPE").HasMaxLength(20).IsRequired();
            entity.Property(e => e.FloorCount).HasColumnName("TOTAL_FLOORS");
        });

        // ---- Admin 管理员 ----
        modelBuilder.Entity<Admin>(entity =>
        {
            entity.ToTable("D_ADMIN");
            entity.HasKey(e => e.AdminId);
            entity.Property(e => e.AdminId).HasColumnName("ADMIN_ID").HasMaxLength(20);
            entity.Property(e => e.AdminName).HasColumnName("ADMIN_NAME").HasMaxLength(50).IsRequired();
            entity.Property(e => e.Phone).HasColumnName("PHONE").HasMaxLength(20);
            entity.Property(e => e.RoleLevel).HasColumnName("ROLE_LEVEL").HasMaxLength(20).IsRequired();
            entity.Property(e => e.BuildingId).HasColumnName("BUILDING_ID");

            entity.HasOne<Building>()
                  .WithMany()
                  .HasForeignKey(e => e.BuildingId)
                  .HasConstraintName("FK_D_ADMIN_BUILDING");
        });

        // ---- Major 专业 ----
        modelBuilder.Entity<Major>(entity =>
        {
            entity.ToTable("D_MAJOR");
            entity.HasKey(e => e.MajorId);
            entity.Property(e => e.MajorId).HasColumnName("MAJOR_ID");
            entity.Property(e => e.CollegeId).HasColumnName("COLLEGE_ID");
            entity.Property(e => e.MajorName).HasColumnName("MAJOR_NAME").HasMaxLength(100).IsRequired();

            entity.HasOne<College>()
                  .WithMany()
                  .HasForeignKey(e => e.CollegeId)
                  .HasConstraintName("FK_D_MAJOR_COLLEGE");
        });

        // ---- Room 房间 ----
        modelBuilder.Entity<Room>(entity =>
        {
            entity.ToTable("D_ROOM");
            entity.HasKey(e => e.RoomId);
            entity.Property(e => e.RoomId).HasColumnName("ROOM_ID");
            entity.Property(e => e.BuildingId).HasColumnName("BUILDING_ID");
            entity.Property(e => e.RoomNumber).HasColumnName("ROOM_NUMBER").HasMaxLength(20).IsRequired();
            entity.Property(e => e.Capacity).HasColumnName("CAPACITY").HasDefaultValue(4);
            entity.Property(e => e.Occupancy).HasColumnName("OCCUPANCY").HasDefaultValue(0);
            entity.Property(e => e.Floor).HasColumnName("FLOOR");
            entity.Property(e => e.Status).HasColumnName("STATUS").HasMaxLength(10).IsRequired();
            entity.Property(e => e.PowerStatus).HasColumnName("POWER_STATUS").HasMaxLength(10).IsRequired();

            entity.HasOne<Building>()
                  .WithMany()
                  .HasForeignKey(e => e.BuildingId)
                  .HasConstraintName("FK_D_ROOM_BUILDING");

            entity.HasCheckConstraint("CK_D_ROOM_POWER_STATUS", "POWER_STATUS IN ('正常', '断电')");
        });

        // ---- Student 学生 ----
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

            entity.HasOne<Major>()
                  .WithMany()
                  .HasForeignKey(e => e.MajorId)
                  .HasConstraintName("FK_D_STUDENT_MAJOR");
        });

        // ---- Asset 资产 ----
        modelBuilder.Entity<Asset>(entity =>
        {
            entity.ToTable("D_ASSET");
            entity.HasKey(e => e.AssetId);
            entity.Property(e => e.AssetId).HasColumnName("ASSET_ID");
            entity.Property(e => e.RoomId).HasColumnName("ROOM_ID");
            entity.Property(e => e.AssetName).HasColumnName("ASSET_NAME").HasMaxLength(50).IsRequired();
            entity.Property(e => e.Quantity).HasColumnName("QUANTITY").HasDefaultValue(1);
            entity.Property(e => e.Status).HasColumnName("STATUS").HasMaxLength(20).IsRequired();

            entity.HasOne<Room>()
                  .WithMany()
                  .HasForeignKey(e => e.RoomId)
                  .HasConstraintName("FK_D_ASSET_ROOM");
        });

        // ---- Notice 公告 ----
        modelBuilder.Entity<Notice>(entity =>
        {
            entity.ToTable("D_NOTICE");
            entity.HasKey(e => e.NoticeId);
            entity.Property(e => e.NoticeId).HasColumnName("NOTICE_ID");
            entity.Property(e => e.AdminId).HasColumnName("ADMIN_ID").HasMaxLength(20);
            entity.Property(e => e.Title).HasColumnName("TITLE").HasMaxLength(100).IsRequired();
            entity.Property(e => e.Content).HasColumnName("CONTENT").HasMaxLength(1000).IsRequired();
            entity.Property(e => e.PublishTime).HasColumnName("PUBLISH_TIME").IsRequired();

            entity.HasOne<Admin>()
                  .WithMany()
                  .HasForeignKey(e => e.AdminId)
                  .HasConstraintName("FK_D_NOTICE_ADMIN");

            entity.HasOne(n => n.Display)
                  .WithOne(d => d.Notice)
                  .HasForeignKey<NoticeDisplay>(d => d.NoticeId)
                  .HasConstraintName("FK_D_NOTICE_DISPLAY");
        });

        // ---- NoticeDisplay 公告置顶 ----
        modelBuilder.Entity<NoticeDisplay>(entity =>
        {
            entity.ToTable("D_NOTICE_DISPLAY");
            entity.HasKey(e => e.NoticeId);
            entity.Property(e => e.NoticeId).HasColumnName("NOTICE_ID");
            entity.Property(e => e.IsPinned).HasColumnName("IS_PINNED").HasMaxLength(10).IsRequired();
            entity.Property(e => e.PinTime).HasColumnName("PIN_TIME");
        });

        // ---- VisitorLog 访客日志 ----
        modelBuilder.Entity<VisitorLog>(entity =>
        {
            entity.ToTable("D_VISITOR_LOG");
            entity.HasKey(e => e.VisitorId);
            entity.Property(e => e.VisitorId).HasColumnName("VISITOR_ID");
            entity.Property(e => e.BuildingId).HasColumnName("BUILDING_ID");
            entity.Property(e => e.VisitorName).HasColumnName("VISITOR_NAME").HasMaxLength(50).IsRequired();
            entity.Property(e => e.VisitReason).HasColumnName("VISIT_REASON").HasMaxLength(200).IsRequired();
            entity.Property(e => e.EntryTime).HasColumnName("ENTRY_TIME").IsRequired();
            entity.Property(e => e.LeaveTime).HasColumnName("LEAVE_TIME");

            entity.HasOne<Building>()
                  .WithMany()
                  .HasForeignKey(e => e.BuildingId)
                  .HasConstraintName("FK_D_VISITOR_LOG_BUILDING");
        });

        // ---- UtilityFee 水电费 ----
        modelBuilder.Entity<UtilityFee>(entity =>
        {
            entity.ToTable("D_UTILITY_FEE");
            entity.HasKey(e => e.FeeId);
            entity.Property(e => e.FeeId).HasColumnName("FEE_ID");
            entity.Property(e => e.RoomId).HasColumnName("ROOM_ID").IsRequired();
            entity.Property(e => e.YearMonth).HasColumnName("YEAR_MONTH").HasMaxLength(10).IsRequired();
            entity.Property(e => e.WaterFee).HasColumnName("WATER_FEE").HasPrecision(8, 2);
            entity.Property(e => e.PowerFee).HasColumnName("POWER_FEE").HasPrecision(8, 2);
            entity.Property(e => e.IsPaid).HasColumnName("IS_PAID").HasMaxLength(10).HasDefaultValue("否");
            entity.Property(e => e.PublishStatus).HasColumnName("PUBLISH_STATUS").HasMaxLength(10).IsRequired().HasDefaultValue("未发布");

            entity.HasOne<Room>()
                  .WithMany()
                  .HasForeignKey(e => e.RoomId)
                  .HasConstraintName("FK_D_UTILITY_FEE_ROOM");

            entity.HasIndex(e => new { e.RoomId, e.YearMonth })
                  .IsUnique()
                  .HasDatabaseName("UK_D_UTILITY_FEE_ROOM_MONTH");

            entity.HasCheckConstraint("CK_D_UTILITY_FEE_PUB", "PUBLISH_STATUS IN ('未发布', '已发布')");
        });

        // ---- HygieneRecord 卫生检查 ----
        modelBuilder.Entity<HygieneRecord>(entity =>
        {
            entity.ToTable("D_HYGIENE_RECORD");
            entity.HasKey(e => e.RecordId);
            entity.Property(e => e.RecordId).HasColumnName("RECORD_ID");
            entity.Property(e => e.RoomId).HasColumnName("ROOM_ID");
            entity.Property(e => e.CheckDate).HasColumnName("CHECK_DATE").IsRequired();
            entity.Property(e => e.Score).HasColumnName("SCORE").HasPrecision(4, 1).IsRequired();
            entity.Property(e => e.InspectorId).HasColumnName("INSPECTOR_ID").HasMaxLength(20);

            entity.HasOne<Room>()
                  .WithMany()
                  .HasForeignKey(e => e.RoomId)
                  .HasConstraintName("FK_D_HYGIENE_RECORD_ROOM");

            entity.HasOne<Admin>()
                  .WithMany()
                  .HasForeignKey(e => e.InspectorId)
                  .HasConstraintName("FK_D_HYGIENE_RECORD_ADMIN");

            // 与 HygieneComment 一对一关系配置在 HygieneComment 中
        });

        // ---- HygieneComment 卫生检查评语 ----
        modelBuilder.Entity<HygieneComment>(entity =>
        {
            entity.ToTable("D_HYGIENE_COMMENT");
            entity.HasKey(e => e.RecordId);
            entity.Property(e => e.RecordId).HasColumnName("RECORD_ID");
            entity.Property(e => e.CommentText).HasColumnName("\"COMMENT\"").HasMaxLength(500); // 注意引号

            entity.HasOne<HygieneRecord>()
                  .WithOne(e => e.Comment)
                  .HasForeignKey<HygieneComment>(e => e.RecordId)
                  .HasConstraintName("FK_D_HYGIENE_COMMENT_RECORD");
        });

        // ---- WaterOrder 桶装水订单 ----
        modelBuilder.Entity<WaterOrder>(entity =>
        {
            entity.ToTable("D_WATER_ORDER");
            entity.HasKey(e => e.OrderId);
            entity.Property(e => e.OrderId).HasColumnName("ORDER_ID");
            entity.Property(e => e.RoomId).HasColumnName("ROOM_ID");
            entity.Property(e => e.OrderTime).HasColumnName("ORDER_TIME").IsRequired();
            entity.Property(e => e.Quantity).HasColumnName("QUANTITY").HasDefaultValue(1);
            entity.Property(e => e.Status).HasColumnName("STATUS").HasMaxLength(20).HasDefaultValue("未送达");

            entity.HasOne<Room>()
                  .WithMany()
                  .HasForeignKey(e => e.RoomId)
                  .HasConstraintName("FK_D_WATER_ORDER_ROOM");
        });

        // ---- BedAllocation 床位分配 ----
        modelBuilder.Entity<BedAllocation>(entity =>
        {
            entity.ToTable("D_BED_ALLOCATION");
            entity.HasKey(e => e.AllocationId);
            entity.Property(e => e.AllocationId).HasColumnName("ALLOCATION_ID");
            entity.Property(e => e.StudentId).HasColumnName("STUDENT_ID").HasMaxLength(20);
            entity.Property(e => e.RoomId).HasColumnName("ROOM_ID");
            entity.Property(e => e.BedNo).HasColumnName("BED_NO").IsRequired();
            entity.Property(e => e.CheckInDate).HasColumnName("CHECKIN_DATE").IsRequired();
            entity.Property(e => e.CheckOutDate).HasColumnName("CHECKOUT_DATE");

            entity.HasOne<Student>()
                  .WithMany()
                  .HasForeignKey(e => e.StudentId)
                  .HasConstraintName("FK_D_BED_ALLOCATION_STUDENT");

            entity.HasOne<Room>()
                  .WithMany()
                  .HasForeignKey(e => e.RoomId)
                  .HasConstraintName("FK_D_BED_ALLOCATION_ROOM");

            entity.HasCheckConstraint("CK_D_BED_ALLOC_BED", "BED_NO >= 1");

            // 唯一索引 UK_D_BED_ALLOC_ACTIVE 在 DDL 中已创建，EF 无法表达，但无需映射。
        });

        // ---- RepairTicket 报修单 ----
        modelBuilder.Entity<RepairTicket>(entity =>
        {
            entity.ToTable("D_REPAIR_TICKET");
            entity.HasKey(e => e.TicketId);
            entity.Property(e => e.TicketId).HasColumnName("TICKET_ID");
            entity.Property(e => e.StudentId).HasColumnName("STUDENT_ID").HasMaxLength(20);
            entity.Property(e => e.RoomId).HasColumnName("ROOM_ID");
            entity.Property(e => e.IssueDescription).HasColumnName("ISSUE_DESC").HasMaxLength(500).IsRequired();
            entity.Property(e => e.SubmitTime).HasColumnName("SUBMIT_TIME").IsRequired();
            entity.Property(e => e.Status).HasColumnName("STATUS").HasMaxLength(20).HasDefaultValue("待处理");
            entity.Property(e => e.SlaLevel).HasColumnName("SLA_LEVEL").HasMaxLength(10).IsRequired();
            entity.Property(e => e.Deadline).HasColumnName("DEADLINE");
            entity.Property(e => e.AssignedTo).HasColumnName("ASSIGNED_TO").HasMaxLength(20);

            entity.HasOne<Student>()
                  .WithMany()
                  .HasForeignKey(e => e.StudentId)
                  .HasConstraintName("FK_D_REPAIR_TICKET_STUDENT");

            entity.HasOne<Room>()
                  .WithMany()
                  .HasForeignKey(e => e.RoomId)
                  .HasConstraintName("FK_D_REPAIR_TICKET_ROOM");

            entity.HasOne<Admin>()
                  .WithMany()
                  .HasForeignKey(e => e.AssignedTo)
                  .HasConstraintName("FK_D_REPAIR_TICKET_ASSIGNED_ADMIN");

            entity.HasCheckConstraint("CK_D_REPAIR_TICKET_SLA_LEVEL", "SLA_LEVEL IN ('普通', '紧急')");
        });

        // ---- RepairLog 报修处理日志 ----
        modelBuilder.Entity<RepairLog>(entity =>
        {
            entity.ToTable("D_REPAIR_LOG");
            entity.HasKey(e => e.LogId);
            entity.Property(e => e.LogId).HasColumnName("LOG_ID");
            entity.Property(e => e.TicketId).HasColumnName("TICKET_ID");
            entity.Property(e => e.AdminId).HasColumnName("ADMIN_ID").HasMaxLength(20);
            entity.Property(e => e.ProcessDescription).HasColumnName("PROCESS_DESC").HasMaxLength(500);
            entity.Property(e => e.ResolveTime).HasColumnName("RESOLVE_TIME").IsRequired();

            entity.HasOne<RepairTicket>()
                  .WithOne()
                  .HasForeignKey<RepairLog>(e => e.TicketId)
                  .HasConstraintName("FK_D_REPAIR_LOG_TICKET");

            entity.HasOne<Admin>()
                  .WithMany()
                  .HasForeignKey(e => e.AdminId)
                  .HasConstraintName("FK_D_REPAIR_LOG_ADMIN");

            entity.HasIndex(e => e.TicketId)
                  .IsUnique()
                  .HasDatabaseName("UK_D_REPAIR_LOG_TICKET");
        });

        // ---- RepairAttachment 报修附件 ----
        modelBuilder.Entity<RepairAttachment>(entity =>
        {
            entity.ToTable("D_REPAIR_ATTACHMENT");
            entity.HasKey(e => e.AttachmentId);
            entity.Property(e => e.AttachmentId).HasColumnName("ATTACHMENT_ID");
            entity.Property(e => e.TicketId).HasColumnName("TICKET_ID").IsRequired();
            entity.Property(e => e.StorageRef).HasColumnName("STORAGE_REF").HasMaxLength(500).IsRequired();
            entity.Property(e => e.OriginalName).HasColumnName("ORIGINAL_NAME").HasMaxLength(255);
            entity.Property(e => e.ContentType).HasColumnName("CONTENT_TYPE").HasMaxLength(100);
            entity.Property(e => e.FileSize).HasColumnName("FILE_SIZE");
            entity.Property(e => e.CreateTime).HasColumnName("CREATE_TIME").HasDefaultValueSql("SYSDATE").ValueGeneratedOnAdd();

            entity.HasOne<RepairTicket>()
                  .WithMany(e => e.Attachments)
                  .HasForeignKey(e => e.TicketId)
                  .HasConstraintName("FK_D_REPAIR_ATTACHMENT_TICKET");
        });

        // ---- LateEntry 晚归记录 ----
        modelBuilder.Entity<LateEntry>(entity =>
        {
            entity.ToTable("D_LATE_ENTRY");
            entity.HasKey(e => e.RecordId);
            entity.Property(e => e.RecordId).HasColumnName("RECORD_ID");
            entity.Property(e => e.StudentId).HasColumnName("STUDENT_ID").HasMaxLength(20);
            entity.Property(e => e.ReturnTime).HasColumnName("RETURN_TIME").IsRequired();
            entity.Property(e => e.Reason).HasColumnName("REASON").HasMaxLength(200);

            entity.HasOne<Student>()
                  .WithMany()
                  .HasForeignKey(e => e.StudentId)
                  .HasConstraintName("FK_D_LATE_ENTRY_STUDENT");
        });

        // ---- AccessLog 门禁日志 ----
        modelBuilder.Entity<AccessLog>(entity =>
        {
            entity.ToTable("D_ACCESS_LOG");
            entity.HasKey(e => e.LogId);
            entity.Property(e => e.LogId).HasColumnName("LOG_ID");
            entity.Property(e => e.StudentId).HasColumnName("STUDENT_ID").HasMaxLength(20);
            entity.Property(e => e.BuildingId).HasColumnName("BUILDING_ID");
            entity.Property(e => e.SwipeTime).HasColumnName("SWIPE_TIME").IsRequired();
            entity.Property(e => e.Direction).HasColumnName("DIRECTION").HasMaxLength(10).IsRequired();

            entity.HasOne<Student>()
                  .WithMany()
                  .HasForeignKey(e => e.StudentId)
                  .HasConstraintName("FK_D_ACCESS_LOG_STUDENT");

            entity.HasOne<Building>()
                  .WithMany()
                  .HasForeignKey(e => e.BuildingId)
                  .HasConstraintName("FK_D_ACCESS_LOG_BUILDING");
        });

        // ---- LeaveApplication 离校报备 ----
        modelBuilder.Entity<LeaveApplication>(entity =>
        {
            entity.ToTable("D_LEAVE_APPLICATION");
            entity.HasKey(e => e.ApplyId);
            entity.Property(e => e.ApplyId).HasColumnName("APPLY_ID");
            entity.Property(e => e.StudentId).HasColumnName("STUDENT_ID").HasMaxLength(20);
            entity.Property(e => e.LeaveDate).HasColumnName("LEAVE_DATE").IsRequired();
            entity.Property(e => e.ReturnDate).HasColumnName("RETURN_DATE").IsRequired();
            entity.Property(e => e.Destination).HasColumnName("DESTINATION").HasMaxLength(200).IsRequired();
            entity.Property(e => e.Status).HasColumnName("STATUS").HasMaxLength(20).HasDefaultValue("待批");

            entity.HasOne<Student>()
                  .WithMany()
                  .HasForeignKey(e => e.StudentId)
                  .HasConstraintName("FK_D_LEAVE_APPLICATION_STUDENT");
        });

        // ---- ParcelRecord 快递记录 ----
        modelBuilder.Entity<ParcelRecord>(entity =>
        {
            entity.ToTable("D_PARCEL_RECORD");
            entity.HasKey(e => e.ParcelId);
            entity.Property(e => e.ParcelId).HasColumnName("PARCEL_ID");
            entity.Property(e => e.StudentId).HasColumnName("STUDENT_ID").HasMaxLength(20);
            entity.Property(e => e.ArriveTime).HasColumnName("ARRIVE_TIME").IsRequired();
            entity.Property(e => e.PickupTime).HasColumnName("PICKUP_TIME");
            entity.Property(e => e.CourierCompany).HasColumnName("COURIER_COMPANY").HasMaxLength(50);

            entity.HasOne<Student>()
                  .WithMany()
                  .HasForeignKey(e => e.StudentId)
                  .HasConstraintName("FK_D_PARCEL_RECORD_STUDENT");
        });

        // ---- ViolationRecord 违规记录 ----
        modelBuilder.Entity<ViolationRecord>(entity =>
        {
            entity.ToTable("D_VIOLATION_RECORD");
            entity.HasKey(e => e.RecordId);
            entity.Property(e => e.RecordId).HasColumnName("RECORD_ID");
            entity.Property(e => e.StudentId).HasColumnName("STUDENT_ID").HasMaxLength(20);
            entity.Property(e => e.RoomId).HasColumnName("ROOM_ID");
            entity.Property(e => e.VioType).HasColumnName("VIO_TYPE").HasMaxLength(50).IsRequired();
            entity.Property(e => e.VioDate).HasColumnName("VIO_DATE").IsRequired();
            entity.Property(e => e.Penalty).HasColumnName("PENALTY").HasMaxLength(100);

            entity.HasOne<Student>()
                  .WithMany()
                  .HasForeignKey(e => e.StudentId)
                  .HasConstraintName("FK_D_VIOLATION_RECORD_STUDENT");

            entity.HasOne<Room>()
                  .WithMany()
                  .HasForeignKey(e => e.RoomId)
                  .HasConstraintName("FK_D_VIOLATION_RECORD_ROOM");
        });

        // ============================================================
        // 2. 扩展表映射
        // ============================================================

        // ---- UserAccount 用户账号 ----
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
            entity.Property(e => e.CreateTime).HasColumnName("CREATE_TIME").HasDefaultValueSql("SYSDATE").ValueGeneratedOnAdd();

            entity.HasIndex(e => e.LoginName).IsUnique().HasDatabaseName("UK_D_USER_LOGIN");
            entity.HasIndex(e => e.StudentId).IsUnique().HasDatabaseName("UK_D_USER_STUDENT");
            entity.HasIndex(e => e.AdminId).IsUnique().HasDatabaseName("UK_D_USER_ADMIN");
            entity.HasCheckConstraint("CK_D_USER_STATUS", "ACCOUNT_STATUS IN ('ACTIVE', 'INACTIVE', 'LOCKED')");

            // 外键关系（DDL 未定义，但为完整可加，但不强制）
            // entity.HasOne<Student>().WithMany().HasForeignKey(e => e.StudentId);
            // entity.HasOne<Admin>().WithMany().HasForeignKey(e => e.AdminId);
        });

        // ---- Notification 通知 ----
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("D_NOTIFICATION");
            entity.HasKey(e => e.NotificationId);
            entity.Property(e => e.NotificationId).HasColumnName("NOTIFICATION_ID");
            entity.Property(e => e.RecipientAccountId).HasColumnName("RECIPIENT_ACCOUNT_ID").IsRequired();
            entity.Property(e => e.Title).HasColumnName("TITLE").HasMaxLength(100).IsRequired();
            entity.Property(e => e.Content).HasColumnName("CONTENT").HasMaxLength(1000).IsRequired();
            entity.Property(e => e.NotificationType).HasColumnName("NOTIFICATION_TYPE").HasMaxLength(20).IsRequired();
            entity.Property(e => e.ReadTime).HasColumnName("READ_TIME");
            entity.Property(e => e.CreateTime).HasColumnName("CREATE_TIME").HasDefaultValueSql("SYSDATE").ValueGeneratedOnAdd();

            entity.HasOne<UserAccount>()
                  .WithMany()
                  .HasForeignKey(e => e.RecipientAccountId)
                  .HasConstraintName("FK_D_NOTIFICATION_ACCOUNT")
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasCheckConstraint("CK_D_NOTIFICATION_TYPE", "NOTIFICATION_TYPE IN ('SYSTEM', 'BILL', 'REPAIR', 'CREDIT')");
        });

        // ---- CreditAccount 信用分账户 ----
        modelBuilder.Entity<CreditAccount>(entity =>
        {
            entity.ToTable("D_CREDIT_ACCOUNT");
            entity.HasKey(e => e.StudentId);
            entity.Property(e => e.StudentId).HasColumnName("STUDENT_ID").HasMaxLength(20);
            entity.Property(e => e.CurrentScore).HasColumnName("CURRENT_SCORE").HasDefaultValue(100).IsRequired();
            entity.Property(e => e.UpdatedTime).HasColumnName("UPDATED_TIME").HasDefaultValueSql("SYSDATE").ValueGeneratedOnAdd();

            entity.HasOne<Student>()
                  .WithOne()
                  .HasForeignKey<CreditAccount>(e => e.StudentId)
                  .HasConstraintName("FK_D_CREDIT_ACCOUNT_STUDENT");
        });

        // ---- CreditLog 信用分流水 ----
        modelBuilder.Entity<CreditLog>(entity =>
        {
            entity.ToTable("D_CREDIT_LOG");
            entity.HasKey(e => e.LogId);
            entity.Property(e => e.LogId).HasColumnName("LOG_ID");
            entity.Property(e => e.StudentId).HasColumnName("STUDENT_ID").HasMaxLength(20).IsRequired();
            entity.Property(e => e.ScoreChange).HasColumnName("SCORE_CHANGE").IsRequired();
            entity.Property(e => e.Reason).HasColumnName("REASON").HasMaxLength(200).IsRequired();
            entity.Property(e => e.EventKey).HasColumnName("EVENT_KEY").HasMaxLength(100).IsRequired();
            entity.Property(e => e.CreateTime).HasColumnName("CREATE_TIME").HasDefaultValueSql("SYSDATE").ValueGeneratedOnAdd();

            entity.HasOne<CreditAccount>()
                  .WithMany()
                  .HasForeignKey(e => e.StudentId)
                  .HasConstraintName("FK_D_CREDIT_LOG_ACCOUNT")
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- Facility 公共设施 ----
        modelBuilder.Entity<Facility>(entity =>
        {
            entity.ToTable("D_FACILITY");
            entity.HasKey(e => e.FacilityId);
            entity.Property(e => e.FacilityId).HasColumnName("FACILITY_ID");
            entity.Property(e => e.BuildingId).HasColumnName("BUILDING_ID").IsRequired();
            entity.Property(e => e.FacilityCode).HasColumnName("FACILITY_CODE").HasMaxLength(30).IsRequired();
            entity.Property(e => e.FacilityType).HasColumnName("FACILITY_TYPE").HasMaxLength(30).IsRequired();
            entity.Property(e => e.Status).HasColumnName("STATUS").HasMaxLength(10).IsRequired();

            entity.HasOne<Building>()
                  .WithMany()
                  .HasForeignKey(e => e.BuildingId)
                  .HasConstraintName("FK_D_FACILITY_BUILDING");
        });

        // ---- FacilityBooking 设施预约 ----
        modelBuilder.Entity<FacilityBooking>(entity =>
        {
            entity.ToTable("D_FACILITY_BOOKING");
            entity.HasKey(e => e.BookingId);
            entity.Property(e => e.BookingId).HasColumnName("BOOKING_ID");
            entity.Property(e => e.FacilityId).HasColumnName("FACILITY_ID").IsRequired();
            entity.Property(e => e.StudentId).HasColumnName("STUDENT_ID").HasMaxLength(20).IsRequired();
            entity.Property(e => e.CreateTime).HasColumnName("CREATE_TIME").IsRequired();
            entity.Property(e => e.StartTime).HasColumnName("START_TIME");
            entity.Property(e => e.EndTime).HasColumnName("END_TIME");
            entity.Property(e => e.Status).HasColumnName("STATUS").HasMaxLength(10).IsRequired();

            entity.HasOne<Facility>()
                  .WithMany()
                  .HasForeignKey(e => e.FacilityId)
                  .HasConstraintName("FK_D_FACILITY_BOOKING_FACILITY");
        });

        // ---- SharedItem 共享物品 ----
        modelBuilder.Entity<SharedItem>(entity =>
        {
            entity.ToTable("D_SHARED_ITEM");
            entity.HasKey(e => e.ItemId);
            entity.Property(e => e.ItemId).HasColumnName("ITEM_ID");
            entity.Property(e => e.ItemName).HasColumnName("ITEM_NAME").HasMaxLength(50).IsRequired();
            entity.Property(e => e.BuildingId).HasColumnName("BUILDING_ID");
            entity.Property(e => e.TotalQty).HasColumnName("TOTAL_QTY");
            entity.Property(e => e.AvailableQty).HasColumnName("AVAILABLE_QTY");
            entity.Property(e => e.Status).HasColumnName("STATUS").HasMaxLength(10).IsRequired();

            entity.HasOne<Building>()
                  .WithMany()
                  .HasForeignKey(e => e.BuildingId)
                  .HasConstraintName("FK_D_SHARED_ITEM_BUILDING");
        });

        // ---- ItemLoan 物品借用 ----
        modelBuilder.Entity<ItemLoan>(entity =>
        {
            entity.ToTable("D_ITEM_LOAN");
            entity.HasKey(e => e.LoanId);
            entity.Property(e => e.LoanId).HasColumnName("LOAN_ID");
            entity.Property(e => e.ItemId).HasColumnName("ITEM_ID");
            entity.Property(e => e.StudentId).HasColumnName("STUDENT_ID").HasMaxLength(20).IsRequired();
            entity.Property(e => e.BorrowTime).HasColumnName("BORROW_TIME").IsRequired();
            entity.Property(e => e.DueTime).HasColumnName("DUE_TIME").IsRequired();
            entity.Property(e => e.ReturnTime).HasColumnName("RETURN_TIME");
            entity.Property(e => e.IdempotencyKey).HasColumnName("IDEMPOTENCY_KEY").HasMaxLength(100);

            entity.HasOne<SharedItem>()
                  .WithMany()
                  .HasForeignKey(e => e.ItemId)
                  .HasConstraintName("FK_D_ITEM_LOAN_ITEM");
        });

        // ---- RepairMaterial 维修耗材 ----
        modelBuilder.Entity<RepairMaterial>(entity =>
        {
            entity.ToTable("D_REPAIR_MATERIAL");
            entity.HasKey(e => e.MaterialId);
            entity.Property(e => e.MaterialId).HasColumnName("MATERIAL_ID");
            entity.Property(e => e.MaterialName).HasColumnName("MATERIAL_NAME").HasMaxLength(50).IsRequired();
            entity.Property(e => e.Unit).HasColumnName("UNIT").HasMaxLength(20).IsRequired();
            entity.Property(e => e.StockQty).HasColumnName("STOCK_QTY");
        });

        // ---- RepairMaterialUsage 耗材使用记录 ----
        modelBuilder.Entity<RepairMaterialUsage>(entity =>
        {
            entity.ToTable("D_REPAIR_MATERIAL_USAGE");
            entity.HasKey(e => e.UsageId);
            entity.Property(e => e.UsageId).HasColumnName("USAGE_ID");
            entity.Property(e => e.TicketId).HasColumnName("TICKET_ID");
            entity.Property(e => e.MaterialId).HasColumnName("MATERIAL_ID");
            entity.Property(e => e.Quantity).HasColumnName("QUANTITY");
            entity.Property(e => e.UseTime).HasColumnName("USE_TIME");
            entity.Property(e => e.IdempotencyKey).HasColumnName("IDEMPOTENCY_KEY").HasMaxLength(100);

            entity.HasOne<RepairTicket>()
                  .WithMany()
                  .HasForeignKey(e => e.TicketId)
                  .HasConstraintName("FK_D_REPAIR_MAT_USE_TICKET");

            entity.HasOne<RepairMaterial>()
                  .WithMany()
                  .HasForeignKey(e => e.MaterialId)
                  .HasConstraintName("FK_D_REPAIR_MAT_USE_MATERIAL");
        });

        // ---- FeeDetail 分摊明细 ----
        modelBuilder.Entity<FeeDetail>(entity =>
        {
            entity.ToTable("D_FEE_DETAIL");
            entity.HasKey(e => e.DetailId);
            entity.Property(e => e.DetailId).HasColumnName("DETAIL_ID");
            entity.Property(e => e.FeeId).HasColumnName("FEE_ID");
            entity.Property(e => e.StudentId).HasColumnName("STUDENT_ID").IsRequired();
            entity.Property(e => e.RoomId).HasColumnName("ROOM_ID");
            entity.Property(e => e.WaterShare).HasColumnName("WATER_SHARE").HasPrecision(18, 2);
            entity.Property(e => e.PowerShare).HasColumnName("POWER_SHARE").HasPrecision(18, 2);
            entity.Property(e => e.StayDays).HasColumnName("STAY_DAYS");
            entity.Property(e => e.TotalDays).HasColumnName("TOTAL_DAYS");
            entity.Property(e => e.BillType).HasColumnName("BILL_TYPE").IsRequired();
            entity.Property(e => e.IsPaid).HasColumnName("IS_PAID").IsRequired();
            entity.Property(e => e.CreateTime).HasColumnName("CREATE_TIME").HasDefaultValueSql("SYSDATE").ValueGeneratedOnAdd();

            entity.HasOne<Student>()
                  .WithMany()
                  .HasForeignKey(e => e.StudentId)
                  .HasConstraintName("FK_D_FEE_DETAIL_STUDENT");

            entity.HasOne<Room>()
                  .WithMany()
                  .HasForeignKey(e => e.RoomId)
                  .HasConstraintName("FK_D_FEE_DETAIL_ROOM");

            entity.HasOne<UtilityFee>()
                  .WithMany()
                  .HasForeignKey(e => e.FeeId)
                  .HasConstraintName("FK_D_FEE_DETAIL_FEE");
        });

        // ---- WalletAccount 钱包账户 ----
        modelBuilder.Entity<WalletAccount>(entity =>
        {
            entity.ToTable("D_WALLET_ACCOUNT");
            entity.HasKey(e => e.StudentId);
            entity.Property(e => e.StudentId).HasColumnName("STUDENT_ID").HasMaxLength(20);
            entity.Property(e => e.Balance).HasColumnName("BALANCE").HasPrecision(18, 2).HasDefaultValue(0);

            entity.HasOne<Student>()
                  .WithOne()
                  .HasForeignKey<WalletAccount>(e => e.StudentId)
                  .HasConstraintName("FK_D_WALLET_ACCOUNT_STUDENT");
        });

        // ---- WalletLog 钱包流水 ----
        modelBuilder.Entity<WalletLog>(entity =>
        {
            entity.ToTable("D_WALLET_LOG");
            entity.HasKey(e => e.LogId);
            entity.Property(e => e.LogId).HasColumnName("LOG_ID");
            entity.Property(e => e.StudentId).HasColumnName("STUDENT_ID").IsRequired();
            entity.Property(e => e.Amount).HasColumnName("AMOUNT").HasPrecision(18, 2);
            entity.Property(e => e.TransactionType).HasColumnName("TRANSACTION_TYPE").IsRequired();
            entity.Property(e => e.BeforeBalance).HasColumnName("BEFORE_BALANCE").HasPrecision(18, 2);
            entity.Property(e => e.AfterBalance).HasColumnName("AFTER_BALANCE").HasPrecision(18, 2);
            entity.Property(e => e.DetailId).HasColumnName("DETAIL_ID");
            entity.Property(e => e.IdempotencyKey).HasColumnName("IDEMPOTENCY_KEY").IsRequired();
            entity.Property(e => e.CreateTime).HasColumnName("CREATE_TIME").HasDefaultValueSql("SYSDATE").ValueGeneratedOnAdd();

            entity.HasOne<WalletAccount>()
                  .WithMany()
                  .HasForeignKey(e => e.StudentId)
                  .HasConstraintName("FK_D_WALLET_LOG_ACCOUNT");
        });

        // ---- AuditEvent 审计日志 ----
        modelBuilder.Entity<AuditEvent>(entity =>
        {
            entity.ToTable("D_AUDIT_EVENT");
            entity.HasKey(e => e.AuditId);
            entity.Property(e => e.AuditId).HasColumnName("AUDIT_ID").ValueGeneratedOnAdd();
            entity.Property(e => e.ActorAccountId).HasColumnName("ACTOR_ACCOUNT_ID");
            entity.Property(e => e.EventType).HasColumnName("EVENT_TYPE").HasMaxLength(50).IsRequired();
            entity.Property(e => e.TargetType).HasColumnName("TARGET_TYPE").HasMaxLength(50);
            entity.Property(e => e.TargetId).HasColumnName("TARGET_ID").HasMaxLength(50);
            entity.Property(e => e.EventTime).HasColumnName("EVENT_TIME").HasDefaultValueSql("SYSDATE").ValueGeneratedOnAdd();

            entity.HasOne<UserAccount>()
                  .WithMany()
                  .HasForeignKey(e => e.ActorAccountId)
                  .HasConstraintName("FK_D_AUDIT_ACCOUNT");
        });
    }
}