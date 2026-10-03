namespace WebAPICore.Api.Models.Entities;

/// <summary>
/// 商品即時庫存檔
/// </summary>
public class ProductStock
{
    /// <summary>
    /// 商品識別碼（同時作為主鍵與外鍵）
    /// </summary>
    public Guid ProductId { get; set; }

    /// <summary>
    /// 當前可用現有庫存（受資料庫 Check 約束保護，不可小於 0）
    /// </summary>
    public int CurrentQty { get; set; }

    /// <summary>
    /// 在途採購量（已核准下單但尚未入庫之總量）
    /// </summary>
    public int OnOrderQty { get; set; } = 0;

    /// <summary>
    /// 最後異動時間
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// PostgreSQL xmin 系統欄位，用作 EF Core 樂觀鎖（並發衝突控制）
    /// </summary>
    public uint Version { get; set; }

    // 導覽屬性
    public Product Product { get; set; } = null!;
}