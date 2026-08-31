using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TemplateDormApi.DTO;
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
    public DbSet<RoomVote> RoomVotes => Set<RoomVote>();
    public DbSet<RoomVoteResponse> RoomVoteResponses => Set<RoomVoteResponse>();
    public DbSet<VisitorAuthorization> VisitorAuthorizations => Set<VisitorAuthorization>();
    public DbSet<VisitorRegistry> VisitorRegistries => Set<VisitorRegistry>();
    public DbSet<BedAllocation> BedAllocations => Set<BedAllocation>();
    public DbSet<RepairTicket> RepairTickets => Set<RepairTicket>();
    public DbSet<LateEntry> LateEntries => Set<LateEntry>();
    public DbSet<VisitorLog> VisitorLogs => Set<VisitorLog>();
    public DbSet<UtilityFee> UtilityFees => Set<UtilityFee>();
    public DbSet<HygieneRecord> HygieneRecords => Set<HygieneRecord>();
    public DbSet<HygieneComment> HygieneComments => Set<HygieneComment>();
    public DbSet<WaterOrder> WaterOrders => Set<WaterOrder>();
    public DbSet<RepairLog> RepairLogs => Set<RepairLog>();
    public DbSet<RepairAttachment> RepairAttachments => Set<RepairAttachment>();
    public DbSet<AccessLog> AccessLogs => Set<AccessLog>();
    public DbSet<ViolationRecord> ViolationRecords => Set<ViolationRecord>();

    // ===== 扩展表 =====
    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<CreditAccount> CreditAccounts => Set<CreditAccount>();
    public DbSet<CreditLog> CreditLogs => Set<CreditLog>();
    public DbSet<CreditAppeal> CreditAppeals => Set<CreditAppeal>();
    public DbSet<Facility> Facilities => Set<Facility>();
    public DbSet<FacilityBooking> FacilityBookings => Set<FacilityBooking>();
    public DbSet<PendingRepairTicketDto> PendingRepairTicketDtos => Set<PendingRepairTicketDto>();
    public DbSet<SharedItem> SharedItems => Set<SharedItem>();
    public DbSet<ItemLoan> ItemLoans => Set<ItemLoan>();
    public DbSet<RepairMaterial> RepairMaterials => Set<RepairMaterial>();
    public DbSet<RepairMaterialUsage> RepairMaterialUsages => Set<RepairMaterialUsage>();
    public DbSet<FeeDetail> FeeDetails => Set<FeeDetail>();
    public DbSet<WalletAccount> WalletAccounts => Set<WalletAccount>();
    public DbSet<WalletLog> WalletLogs => Set<WalletLog>();

    // ===== 资产/保洁/共享物品主数据（刘鸿铭域，迁移 029 扩展表）=====
    public DbSet<CleaningTask> CleaningTasks => Set<CleaningTask>();
    public DbSet<AssetRepair> AssetRepairs => Set<AssetRepair>();
    public DbSet<AssetWarning> AssetWarnings => Set<AssetWarning>();

    // ===== 住宿全生命周期（刘润东）：离校报备 / 退宿清算 =====
    // 注：BedAllocation、ItemLoan 的 DbSet 由住宿/共享物品模块声明，此处不重复声明。
    public DbSet<LeaveApplication> LeaveApplications => Set<LeaveApplication>();
    public DbSet<CheckoutLog> CheckoutLogs => Set<CheckoutLog>();

    // ===== 退宿三步校验只读数据源（写入方分别为快递/共享物品模块）=====
    public DbSet<ParcelRecord> ParcelRecords => Set<ParcelRecord>();

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
            entity.Property(e => e.CollegeId).HasColumnName("COLLEGE_ID").ValueGeneratedOnAdd(); ;
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
            entity.Property(e => e.Post).HasColumnName("POST").HasMaxLength(50);
            entity.Property(e => e.TokenVersion).HasColumnName("TOKEN_VERSION");
        });

        // ---- Major 专业 ----
        modelBuilder.Entity<Major>(entity =>
        {
            entity.ToTable("D_MAJOR");
            entity.HasKey(e => e.MajorId);
            entity.Property(e => e.MajorId).HasColumnName("MAJOR_ID");
            entity.Property(e => e.CollegeId).HasColumnName("COLLEGE_ID");
            entity.Property(e => e.MajorName).HasColumnName("MAJOR_NAME").HasMaxLength(100).IsRequired();
        });

        // ---- Room 房间 ----
        modelBuilder.Entity<Room>(entity =>
        {
            entity.ToTable("D_ROOM", t => t.HasCheckConstraint("CK_D_ROOM_POWER_STATUS", "POWER_STATUS IN ('正常', '断电')"));
            entity.HasKey(e => e.RoomId);
            entity.Property(e => e.RoomId).HasColumnName("ROOM_ID");
            entity.Property(e => e.BuildingId).HasColumnName("BUILDING_ID");
            entity.Property(e => e.RoomNumber).HasColumnName("ROOM_NUMBER").HasMaxLength(20).IsRequired();
            entity.Property(e => e.Capacity).HasColumnName("CAPACITY").HasDefaultValue(4);
            entity.Property(e => e.Occupancy).HasColumnName("OCCUPANCY").HasDefaultValue(0);
            entity.Property(e => e.Floor).HasColumnName("FLOOR");
            entity.Property(e => e.Status).HasColumnName("STATUS").HasMaxLength(10).IsRequired();
            entity.Property(e => e.PowerStatus).HasColumnName("POWER_STATUS").HasMaxLength(10).IsRequired();

            entity.HasOne(e => e.Building)
                  .WithMany(b => b.Rooms)
                  .HasForeignKey(e => e.BuildingId)
                  .HasConstraintName("FK_D_ROOM_BUILDING");
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
        });

        // ---- Asset 资产 ----
        modelBuilder.Entity<Asset>(entity =>
        {
            entity.ToTable("D_ASSET");
            entity.HasKey(e => e.AssetId);
            entity.Property(e => e.AssetId).HasColumnName("ASSET_ID").ValueGeneratedOnAdd();
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
            entity.ToTable("D_UTILITY_FEE", b => b.HasCheckConstraint("CK_D_UTILITY_FEE_PUB", "PUBLISH_STATUS IN ('未发布', '已发布')"));
            entity.HasKey(e => e.FeeId);
            entity.Property(e => e.FeeId).HasColumnName("FEE_ID");
            entity.Property(e => e.RoomId).HasColumnName("ROOM_ID").IsRequired();
            entity.Property(e => e.YearMonth).HasColumnName("YEAR_MONTH").HasMaxLength(10).IsRequired();
            entity.Property(e => e.WaterFee).HasColumnName("WATER_FEE").HasPrecision(8, 2);
            entity.Property(e => e.PowerFee).HasColumnName("POWER_FEE").HasPrecision(8, 2);
            entity.Property(e => e.IsPaid).HasColumnName("IS_PAID").HasMaxLength(10).HasDefaultValue("否");
            entity.Property(e => e.PublishStatus).HasColumnName("PUBLISH_STATUS").HasMaxLength(10).IsRequired().HasDefaultValue("未发布");

            entity.HasOne(e => e.Room)
                  .WithMany()
                  .HasForeignKey(e => e.RoomId)
                  .HasConstraintName("FK_D_UTILITY_FEE_ROOM");

            entity.HasIndex(e => new { e.RoomId, e.YearMonth })
                  .IsUnique()
                  .HasDatabaseName("UK_D_UTILITY_FEE_ROOM_MONTH");
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
            entity.Property(e => e.CommentText).HasColumnName("COMMENT").HasMaxLength(500);

            entity.HasOne(e => e.Record)
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
            entity.ToTable("D_BED_ALLOCATION", b => b.HasCheckConstraint("CK_D_BED_ALLOC_BED", "BED_NO >= 1"));
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
            entity.Property(e => e.CheckOutDate).HasColumnName("CHECKOUT_DATE");

            entity.HasOne<Student>()
                  .WithMany()
                  .HasForeignKey(e => e.StudentId)
                  .HasConstraintName("FK_D_BED_ALLOCATION_STUDENT");

            entity.HasOne<Room>()
                  .WithMany()
                  .HasForeignKey(e => e.RoomId)
                  .HasConstraintName("FK_D_BED_ALLOCATION_ROOM");

            // 唯一索引 UK_D_BED_ALLOC_ACTIVE 在 DDL 中已创建，EF 无法表达，但无需映射。
        });

        // ---- RepairTicket 报修单 ----
        modelBuilder.Entity<RepairTicket>(entity =>
        {
            entity.ToTable("D_REPAIR_TICKET", b => b.HasCheckConstraint("CK_D_REPAIR_TICKET_SLA_LEVEL", "SLA_LEVEL IN ('普通', '紧急')"));
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
            // 难点⑤ 迁移 021 新增列：SLA 首次升级标记（NULL=未升级）
            entity.Property(e => e.EscalationTime).HasColumnName("ESCALATION_TIME");
            // 迁移 040 新增列：接单时间（NULL=未接单；NOT NULL=已由指派维修员接单）
            entity.Property(e => e.ClaimTime).HasColumnName("CLAIM_TIME");

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

            entity.HasOne(t => t.Log)
                  .WithOne(l => l.Ticket)
                  .HasForeignKey<RepairLog>(l => l.TicketId)
                  .HasConstraintName("FK_D_REPAIR_LOG_TICKET");

            entity.HasMany(t => t.Attachments)
                  .WithOne(a => a.Ticket)
                  .HasForeignKey(a => a.TicketId)
                  .HasConstraintName("FK_D_REPAIR_ATTACHMENT_TICKET");
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
            // 难点⑤ 迁移 021 新增列：完工结果（对齐契约 result 字段）
            entity.Property(e => e.RepairResult).HasColumnName("REPAIR_RESULT").HasMaxLength(200);
            entity.Property(e => e.ResolveTime).HasColumnName("RESOLVE_TIME").IsRequired();

            // 四审真实 Oracle 实测（IT-C4-001 执行现场）：同一关系若在两侧实体
            // 各配置一次（即使签名相同），EF 会额外生成影子 FK 属性 "TicketId1"，
            // Oracle provider 将其作为物理列拼入 SELECT → ORA-00904。
            // 该关系统一只在 RepairTicket 侧配置（WithOne(l => l.Ticket)），此处不再重复。

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

            // 关系已统一在 RepairTicket 侧配置（WithOne(a => a.Ticket)），此处不重复配置，
            // 避免 EF 生成影子 FK 列（同 RepairLog 注释，IT-C4-001 执行现场 ORA-00904）。
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

            entity.HasOne(e => e.Student)
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
            entity.Property(e => e.Detail).HasColumnName("DETAIL").HasMaxLength(500);
            entity.Property(e => e.RecordBy).HasColumnName("RECORD_BY").HasMaxLength(20);

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
            entity.ToTable("D_USER_ACCOUNT", b => b.HasCheckConstraint("CK_D_USER_STATUS", "ACCOUNT_STATUS IN ('正常', '停用')"));
            entity.HasKey(e => e.AccountId);
            entity.Property(e => e.AccountId).HasColumnName("ACCOUNT_ID").ValueGeneratedOnAdd();
            entity.Property(e => e.LoginName).HasColumnName("LOGIN_NAME").HasMaxLength(50).IsRequired();
            entity.Property(e => e.PasswordHash).HasColumnName("PASSWORD_HASH").HasMaxLength(255).IsRequired();
            entity.Property(e => e.AccountStatus).HasColumnName("ACCOUNT_STATUS").HasMaxLength(10).IsRequired();
            entity.Property(e => e.StudentId).HasColumnName("STUDENT_ID").HasMaxLength(20);
            entity.Property(e => e.AdminId).HasColumnName("ADMIN_ID").HasMaxLength(20);
            entity.Property(e => e.CreateTime).HasColumnName("CREATE_TIME").HasDefaultValueSql("SYSDATE").ValueGeneratedOnAdd();

            entity.HasIndex(e => e.LoginName).IsUnique().HasDatabaseName("UK_D_USER_LOGIN");
            entity.HasIndex(e => e.StudentId).IsUnique().HasDatabaseName("UK_D_USER_STUDENT");
            entity.HasIndex(e => e.AdminId).IsUnique().HasDatabaseName("UK_D_USER_ADMIN");
            entity.Property(e => e.IsFirstLogin).HasColumnName("IS_FIRST_LOGIN").HasMaxLength(1);

            // 外键关系（DDL 未定义，但为完整可加，但不强制）
            // entity.HasOne<Student>().WithMany().HasForeignKey(e => e.StudentId);
            // entity.HasOne<Admin>().WithMany().HasForeignKey(e => e.AdminId);
        });

        // ---- Notification 通知 ----
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("D_NOTIFICATION", b => b.HasCheckConstraint("CK_D_NOTIFICATION_TYPE", "NOTIFICATION_TYPE IN ('SYSTEM', 'BILL', 'REPAIR', 'CREDIT')"));
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

        // ---- CreditAppeal 信用分申诉（APPEAL-01/02/03）----
        modelBuilder.Entity<CreditAppeal>(entity =>
        {
            entity.ToTable("D_CREDIT_APPEAL");
            entity.HasKey(e => e.AppealId);
            entity.Property(e => e.AppealId).HasColumnName("APPEAL_ID");
            entity.Property(e => e.CreditLogId).HasColumnName("CREDIT_LOG_ID").IsRequired();
            entity.Property(e => e.StudentId).HasColumnName("STUDENT_ID").HasMaxLength(20).IsRequired();
            entity.Property(e => e.Reason).HasColumnName("REASON").HasMaxLength(200).IsRequired();
            entity.Property(e => e.Status).HasColumnName("STATUS").HasMaxLength(20).IsRequired();
            entity.Property(e => e.ResultDesc).HasColumnName("RESULT_DESC").HasMaxLength(200);
            entity.Property(e => e.ReviewedBy).HasColumnName("REVIEWED_BY").HasMaxLength(20);
            entity.Property(e => e.ReviewTime).HasColumnName("REVIEW_TIME");
            entity.Property(e => e.CreateTime).HasColumnName("CREATE_TIME").HasDefaultValueSql("SYSDATE").ValueGeneratedOnAdd();

            entity.HasIndex(e => new { e.StudentId, e.CreateTime, e.AppealId });
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
            entity.Property(e => e.CreateTime).HasColumnName("CREATE_TIME").HasDefaultValueSql("SYSDATE").ValueGeneratedOnAdd();
            entity.Property(e => e.StartTime).HasColumnName("START_TIME");
            entity.Property(e => e.EndTime).HasColumnName("END_TIME");
            entity.Property(e => e.Status).HasColumnName("STATUS").HasMaxLength(10).IsRequired();

            entity.HasOne<Facility>()
                  .WithMany()
                  .HasForeignKey(e => e.FacilityId)
                  .HasConstraintName("FK_D_FACILITY_BOOKING_FACILITY");
        });

        // ===== RoomVote 房间投票实体映射（D_ROOM_VOTE，主键由序列+触发器生成）=====
        modelBuilder.Entity<RoomVote>(entity =>
        {
            entity.ToTable("D_ROOM_VOTE");
            entity.HasKey(e => e.VoteId);
            entity.Property(e => e.VoteId).HasColumnName("VOTE_ID").ValueGeneratedOnAdd();
            entity.Property(e => e.RoomId).HasColumnName("ROOM_ID").IsRequired();
            entity.Property(e => e.InitiatorStudentId).HasColumnName("INITIATOR_STUDENT_ID").HasMaxLength(20).IsRequired();
            entity.Property(e => e.Topic).HasColumnName("TOPIC").HasMaxLength(200).IsRequired();
            entity.Property(e => e.CreateTime).HasColumnName("CREATE_TIME").IsRequired();
            entity.Property(e => e.Deadline).HasColumnName("DEADLINE").IsRequired();
            entity.Property(e => e.EligibleCount).HasColumnName("ELIGIBLE_COUNT").IsRequired();
            entity.Property(e => e.Status).HasColumnName("STATUS").HasMaxLength(10).IsRequired();

            entity.HasOne<Room>()
                .WithMany()
                .HasForeignKey(e => e.RoomId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_D_ROOM_VOTE_ROOM");

            entity.HasOne<Student>()
                .WithMany()
                .HasForeignKey(e => e.InitiatorStudentId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_D_ROOM_VOTE_INIT");
        });

        // ===== RoomVoteResponse 投票响应（复合主键 Vote_ID + Student_ID，天然一人一票）=====
        modelBuilder.Entity<RoomVoteResponse>(entity =>
        {
            entity.ToTable("D_ROOM_VOTE_RESPONSE");
            entity.HasKey(e => new { e.VoteId, e.StudentId });
            entity.Property(e => e.VoteId).HasColumnName("VOTE_ID");
            entity.Property(e => e.StudentId).HasColumnName("STUDENT_ID").HasMaxLength(20);
            entity.Property(e => e.Choice).HasColumnName("CHOICE").HasMaxLength(10).IsRequired();
            entity.Property(e => e.VoteTime).HasColumnName("VOTE_TIME").IsRequired();

            entity.HasOne<RoomVote>()
                .WithMany()
                .HasForeignKey(e => e.VoteId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_D_ROOM_VOTE_RESP_VOTE");

            entity.HasOne<Student>()
                .WithMany()
                .HasForeignKey(e => e.StudentId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_D_ROOM_VOTE_RESP_STU");
        });

        // ===== VisitorAuthorization 访客授权实体映射（D_VISITOR_AUTHORIZATION）=====
        modelBuilder.Entity<VisitorAuthorization>(entity =>
        {
            entity.ToTable("D_VISITOR_AUTHORIZATION");
            entity.HasKey(e => e.AuthorizationId);
            entity.Property(e => e.AuthorizationId).HasColumnName("AUTHORIZATION_ID").ValueGeneratedOnAdd();
            entity.Property(e => e.StudentId).HasColumnName("STUDENT_ID").HasMaxLength(20).IsRequired();
            entity.Property(e => e.RoomId).HasColumnName("ROOM_ID").IsRequired();
            entity.Property(e => e.VisitorName).HasColumnName("VISITOR_NAME").HasMaxLength(50).IsRequired();
            entity.Property(e => e.VisitReason).HasColumnName("VISIT_REASON").HasMaxLength(200);
            entity.Property(e => e.AuthorizationToken).HasColumnName("AUTHORIZATION_TOKEN").HasMaxLength(100).IsRequired();
            entity.Property(e => e.ExpiresTime).HasColumnName("EXPIRES_TIME").IsRequired();
            entity.Property(e => e.Status).HasColumnName("STATUS").HasMaxLength(10).IsRequired();
            entity.Property(e => e.CreateTime).HasColumnName("CREATE_TIME").IsRequired();

            entity.HasIndex(e => e.AuthorizationToken).IsUnique();

            entity.HasOne<Student>()
                .WithMany()
                .HasForeignKey(e => e.StudentId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_D_VISITOR_AUTH_STU");

            entity.HasOne<Room>()
                .WithMany()
                .HasForeignKey(e => e.RoomId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_D_VISITOR_AUTH_ROOM");
        });

        // ===== VisitorRegistry 门岗登记实体映射（D_VISITOR_REGISTRY，VST-01/02/03）=====
        modelBuilder.Entity<VisitorRegistry>(entity =>
        {
            entity.ToTable("D_VISITOR_REGISTRY");
            entity.HasKey(e => e.RegistryId);
            entity.Property(e => e.RegistryId).HasColumnName("REGISTRY_ID");
            entity.Property(e => e.QrToken).HasColumnName("QR_TOKEN").HasMaxLength(100);
            entity.Property(e => e.VisitorName).HasColumnName("VISITOR_NAME").HasMaxLength(50).IsRequired();
            entity.Property(e => e.Phone).HasColumnName("PHONE").HasMaxLength(20);
            entity.Property(e => e.StudentId).HasColumnName("STUDENT_ID").HasMaxLength(20);
            entity.Property(e => e.EnterTime).HasColumnName("ENTER_TIME").HasDefaultValueSql("SYSDATE").ValueGeneratedOnAdd();
            entity.Property(e => e.ExitTime).HasColumnName("EXIT_TIME");
            entity.Property(e => e.Status).HasColumnName("STATUS").HasMaxLength(20).IsRequired();
            entity.Property(e => e.CreateTime).HasColumnName("CREATE_TIME").HasDefaultValueSql("SYSDATE").ValueGeneratedOnAdd();

            entity.HasIndex(e => e.QrToken).IsUnique();
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
            entity.Property(e => e.ItemId).HasColumnName("ITEM_ID").ValueGeneratedOnAdd();
            entity.Property(e => e.ItemName).HasColumnName("ITEM_NAME").HasMaxLength(50).IsRequired();
            entity.Property(e => e.BuildingId).HasColumnName("BUILDING_ID");
            entity.Property(e => e.TotalQty).HasColumnName("TOTAL_QTY");
            entity.Property(e => e.AvailableQty).HasColumnName("AVAILABLE_QTY");
            entity.Property(e => e.Status).HasColumnName("STATUS").HasMaxLength(10).IsRequired();
            entity.Property(e => e.Description).HasColumnName("DESCRIPTION").HasMaxLength(200);

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
            entity.Property(e => e.Details).HasColumnName("DETAILS").HasMaxLength(2000);
        });

        // ===== 资产/保洁/共享物品主数据（迁移 029）=====

        // ---- CleaningTask 保洁任务 ----
        modelBuilder.Entity<CleaningTask>(entity =>
        {
            entity.ToTable("D_CLEANING_TASK");
            entity.HasKey(e => e.TaskId);
            entity.Property(e => e.TaskId).HasColumnName("TASK_ID").ValueGeneratedOnAdd();
            entity.Property(e => e.FacilityId).HasColumnName("FACILITY_ID");
            entity.Property(e => e.TriggerCount).HasColumnName("TRIGGER_COUNT");
            entity.Property(e => e.Status).HasColumnName("STATUS").HasMaxLength(10).IsRequired();
            entity.Property(e => e.CreateTime).HasColumnName("CREATE_TIME").HasDefaultValueSql("SYSDATE").ValueGeneratedOnAdd();
            entity.Property(e => e.CompleteTime).HasColumnName("COMPLETE_TIME");

            entity.HasOne<Facility>()
                  .WithMany()
                  .HasForeignKey(e => e.FacilityId)
                  .HasConstraintName("FK_D_CLEANING_FACILITY");
        });

        // ---- AssetRepair 资产转报修关联 ----
        modelBuilder.Entity<AssetRepair>(entity =>
        {
            entity.ToTable("D_ASSET_REPAIR");
            entity.HasKey(e => e.LinkId);
            entity.Property(e => e.LinkId).HasColumnName("LINK_ID").ValueGeneratedOnAdd();
            entity.Property(e => e.AssetId).HasColumnName("ASSET_ID");
            entity.Property(e => e.TicketId).HasColumnName("TICKET_ID");
            entity.Property(e => e.CreateTime).HasColumnName("CREATE_TIME").HasDefaultValueSql("SYSDATE").ValueGeneratedOnAdd();

            entity.HasOne<Asset>()
                  .WithMany()
                  .HasForeignKey(e => e.AssetId)
                  .HasConstraintName("FK_D_ASSET_REPAIR_ASSET");

            entity.HasOne<RepairTicket>()
                  .WithMany()
                  .HasForeignKey(e => e.TicketId)
                  .HasConstraintName("FK_D_ASSET_REPAIR_TICKET");
        });

        // ---- AssetWarning 损耗预警 ----
        modelBuilder.Entity<AssetWarning>(entity =>
        {
            entity.ToTable("D_ASSET_WARNING");
            entity.HasKey(e => e.WarningId);
            entity.Property(e => e.WarningId).HasColumnName("WARNING_ID").ValueGeneratedOnAdd();
            entity.Property(e => e.AssetId).HasColumnName("ASSET_ID");
            entity.Property(e => e.CreateTime).HasColumnName("CREATE_TIME").HasDefaultValueSql("SYSDATE").ValueGeneratedOnAdd();
            entity.Property(e => e.Note).HasColumnName("NOTE").HasMaxLength(500);
            entity.Property(e => e.HandleAction).HasColumnName("HANDLE_ACTION").HasMaxLength(20);
            entity.Property(e => e.HandleTime).HasColumnName("HANDLE_TIME");
            entity.Property(e => e.Handled).HasColumnName("HANDLED").HasMaxLength(10).IsRequired();

            entity.HasOne<Asset>()
                  .WithMany()
                  .HasForeignKey(e => e.AssetId)
                  .HasConstraintName("FK_D_ASSET_WARNING_ASSET");
        });
    }
}
