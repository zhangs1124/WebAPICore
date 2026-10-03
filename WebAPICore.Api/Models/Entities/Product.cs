namespace WebAPICore.Api.Models.Entities;

/// <summary>
/// 商品基本主檔
/// </summary>
public class Product
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// 商品貨號（庫存單位，唯一鍵）
    /// </summary>
    public string Sku { get; set; } = string.Empty;

    /// <summary>
    /// 商品名稱
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 商品分類
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// 單價
    /// </summary>
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// 安全庫存警戒值（低於此數值系統會發出警示）
    /// </summary>
    public int SafetyStock { get; set; } = 10;

    /// <summary>
    /// 建立時間 (UTC)
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // 導覽屬性
    public ProductStock? Stock { get; set; }
    public ICollection<StockMovement> Movements { get; set; } = new List<StockMovement>();
}