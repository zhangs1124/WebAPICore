namespace WebAPICore.Api.Models.Entities;

/// <summary>
/// 供應商基本主檔實體
/// </summary>
public class Supplier
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// 供應商編號代碼 (如：SUP-001)
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// 供應商公司名稱
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 主要聯絡人
    /// </summary>
    public string ContactPerson { get; set; } = string.Empty;

    /// <summary>
    /// 聯絡電話
    /// </summary>
    public string Phone { get; set; } = string.Empty;

    /// <summary>
    /// 電子信箱
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// 公司登記/聯絡地址
    /// </summary>
    public string? Address { get; set; }

    /// <summary>
    /// 是否為合作中正常狀態
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// 建立時間 (UTC)
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // 導覽屬性：此供應商的採購訂購單與供貨商品清單
    public ICollection<PurchaseOrder> PurchaseOrders { get; set; } = new List<PurchaseOrder>();
    public ICollection<Product> Products { get; set; } = new List<Product>();
}

