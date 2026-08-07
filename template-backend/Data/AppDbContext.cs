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
    }
}
