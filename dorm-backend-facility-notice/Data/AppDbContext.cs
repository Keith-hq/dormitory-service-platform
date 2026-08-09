using Microsoft.EntityFrameworkCore;
using DormBackendFacilityNotice.Models;

namespace DormBackendFacilityNotice.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Building> Buildings => Set<Building>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<FeeDetail> FeeDetails => Set<FeeDetail>();
    public DbSet<Facility> Facilities => Set<Facility>();
    public DbSet<Notice> Notices => Set<Notice>();
    public DbSet<NoticeDisplay> NoticeDisplays => Set<NoticeDisplay>();

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

        // ===== Facility 公共设施实体映射（D_FACILITY）=====
        modelBuilder.Entity<Facility>(entity =>
        {
            entity.ToTable("D_FACILITY");
            entity.HasKey(e => e.FacilityId);
            entity.Property(e => e.FacilityId).HasColumnName("FACILITY_ID");
            entity.Property(e => e.BuildingId).HasColumnName("BUILDING_ID");
            entity.Property(e => e.FacilityCode).HasColumnName("FACILITY_CODE").HasMaxLength(30);
            entity.Property(e => e.FacilityType).HasColumnName("FACILITY_TYPE").HasMaxLength(30);
            entity.Property(e => e.Status).HasColumnName("STATUS").HasMaxLength(10);

            entity.HasOne<Building>()
                  .WithMany()
                  .HasForeignKey(e => e.BuildingId);
        });

        // ===== Notice 公告实体映射（D_NOTICE）=====
        modelBuilder.Entity<Notice>(entity =>
        {
            entity.ToTable("D_NOTICE");
            entity.HasKey(e => e.NoticeId);
            entity.Property(e => e.NoticeId).HasColumnName("NOTICE_ID");
            entity.Property(e => e.AdminId).HasColumnName("ADMIN_ID").HasMaxLength(20);
            entity.Property(e => e.Title).HasColumnName("TITLE").HasMaxLength(100);
            entity.Property(e => e.Content).HasColumnName("CONTENT").HasMaxLength(1000);
            entity.Property(e => e.PublishTime).HasColumnName("PUBLISH_TIME");
        });

        // ===== NoticeDisplay 公告置顶（1:1，对应 D_NOTICE_DISPLAY）=====
        modelBuilder.Entity<NoticeDisplay>(entity =>
        {
            entity.ToTable("D_NOTICE_DISPLAY");
            entity.HasKey(e => e.NoticeId);
            entity.Property(e => e.NoticeId).HasColumnName("NOTICE_ID");
            entity.Property(e => e.IsPinned).HasColumnName("IS_PINNED").HasMaxLength(10);
            entity.Property(e => e.PinTime).HasColumnName("PIN_TIME").IsRequired(false);

            entity.HasOne(d => d.Notice)
                  .WithOne(n => n.Display)
                  .HasForeignKey<NoticeDisplay>(d => d.NoticeId);
        });
    }
}
