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
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();
    public DbSet<CreditAccount> CreditAccounts => Set<CreditAccount>();
    public DbSet<CreditLog> CreditLogs => Set<CreditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ===== Building 楼栋实体映射 =====
        modelBuilder.Entity<Building>(entity =>
        {
            entity.ToTable("BUILDINGS");
            entity.HasKey(e => e.BuildingId);
            entity.Property(e => e.BuildingId).HasColumnName("BUILDING_ID");
            entity.Property(e => e.BuildingName).HasColumnName("BUILDING_NAME").HasMaxLength(100);
            entity.Property(e => e.BuildingType).HasColumnName("BUILDING_TYPE").HasMaxLength(50);
            entity.Property(e => e.FloorCount).HasColumnName("FLOOR_COUNT");
            entity.Property(e => e.CreateTime).HasColumnName("CREATE_TIME");
        });

        // ===== Room 房间实体映射 =====
        modelBuilder.Entity<Room>(entity =>
        {
            entity.ToTable("ROOMS");
            entity.HasKey(e => e.RoomId);
            entity.Property(e => e.RoomId).HasColumnName("ROOM_ID");
            entity.Property(e => e.BuildingId).HasColumnName("BUILDING_ID");
            entity.Property(e => e.RoomNumber).HasColumnName("ROOM_NUMBER").HasMaxLength(20);
            entity.Property(e => e.Capacity).HasColumnName("CAPACITY");
            entity.Property(e => e.Occupied).HasColumnName("OCCUPIED");
            entity.Property(e => e.Status).HasColumnName("STATUS").HasMaxLength(20);

            entity.HasOne<Building>()
                  .WithMany()
                  .HasForeignKey(e => e.BuildingId);
        });

        // ===== Asset 资产实体映射 =====
        modelBuilder.Entity<Asset>(entity =>
        {
            entity.ToTable("ASSETS");
            entity.HasKey(e => e.AssetId);
            entity.Property(e => e.AssetId).HasColumnName("ASSET_ID");
            entity.Property(e => e.RoomId).HasColumnName("ROOM_ID");
            entity.Property(e => e.AssetName).HasColumnName("ASSET_NAME").HasMaxLength(100);
            entity.Property(e => e.AssetType).HasColumnName("ASSET_TYPE").HasMaxLength(50);
            entity.Property(e => e.IsDamaged).HasColumnName("IS_DAMAGED");
            entity.Property(e => e.PurchaseDate).HasColumnName("PURCHASE_DATE");

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
    }
}
