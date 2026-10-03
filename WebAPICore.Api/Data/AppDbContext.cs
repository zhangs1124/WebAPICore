using Microsoft.EntityFrameworkCore;
using WebAPICore.Api.Models.Entities;

namespace WebAPICore.Api.Data;

/// <summary>
/// 進銷存系統資料庫上下文 (PostgreSQL on Supabase)
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductStock> ProductStocks => Set<ProductStock>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 1. 商品主檔配置
        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("products");
            entity.HasKey(p => p.Id);

            entity.Property(p => p.Sku)
                .HasMaxLength(50)
                .IsRequired();

            entity.HasIndex(p => p.Sku)
                .IsUnique();

            entity.Property(p => p.Name)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(p => p.Category)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(p => p.UnitPrice)
                .HasPrecision(12, 2);

            entity.Property(p => p.SafetyStock)
                .HasDefaultValue(10);
        });

        // 2. 庫存即時檔配置
        modelBuilder.Entity<ProductStock>(entity =>
        {
            entity.ToTable("product_stocks");
            entity.HasKey(s => s.ProductId);

            // 資料庫約束：庫存不可小於 0 (防超賣最底層保證)
            entity.ToTable(t => t.HasCheckConstraint("CK_ProductStock_CurrentQty_NonNegative", "\"CurrentQty\" >= 0"));

            // 啟用 PostgreSQL 原生 xmin 隱藏欄位作為樂觀鎖併發控制 Token
            entity.Property(s => s.Version)
                .IsRowVersion();

            // 一對一關聯
            entity.HasOne(s => s.Product)
                .WithOne(p => p.Stock)
                .HasForeignKey<ProductStock>(s => s.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 3. 庫存異動流水帳配置
        modelBuilder.Entity<StockMovement>(entity =>
        {
            entity.ToTable("stock_movements");
            entity.HasKey(m => m.Id);

            entity.Property(m => m.MovementType)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(m => m.Reason)
                .HasMaxLength(255);

            entity.Property(m => m.Operator)
                .HasMaxLength(50)
                .IsRequired();

            // 關聯到商品
            entity.HasOne(m => m.Product)
                .WithMany(p => p.Movements)
                .HasForeignKey(m => m.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            // 索引優化：依商品 ID 與時間降冪建立複合索引，加速歷史流水查詢
            entity.HasIndex(m => new { m.ProductId, m.CreatedAt });
        });
    }
}