using Microsoft.EntityFrameworkCore;
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
    public DbSet<CreditAccount> CreditAccounts => Set<CreditAccount>();
    public DbSet<CreditLog> CreditLogs => Set<CreditLog>();
    public DbSet<Facility> Facilities => Set<Facility>();
    public DbSet<Notice> Notices => Set<Notice>();
    public DbSet<NoticeDisplay> NoticeDisplays => Set<NoticeDisplay>();

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
            entity.Property(e => e.RoomId).HasColumnName("ROOM_ID");
            entity.Property(e => e.BuildingId).HasColumnName("BUILDING_ID");
            entity.Property(e => e.RoomNumber).HasColumnName("ROOM_NUMBER").HasMaxLength(20).IsRequired();
            entity.Property(e => e.Capacity).HasColumnName("CAPACITY");
            entity.Property(e => e.Occupancy).HasColumnName("OCCUPANCY");
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

        // 仅供 SELECT FOR UPDATE 锁定父行，不参与增删改。
        modelBuilder.Entity<CreditStudentLock>(entity =>
        {
            entity.ToTable("D_STUDENT");
            entity.HasKey(e => e.StudentId);
            entity.Property(e => e.StudentId)
                .HasColumnName("STUDENT_ID")
                .HasMaxLength(20);
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
    }
}
