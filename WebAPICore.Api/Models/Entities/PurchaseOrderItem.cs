namespace WebAPICore.Api.Models.Entities;

/// <summary>
/// 採購訂購單身明細實體
/// </summary>
public class PurchaseOrderItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// 所屬採購單 ID
    /// </summary>
    public Guid PurchaseOrderId { get; set; }

    /// <summary>
    /// 採購商品 ID
    /// </summary>
    public Guid ProductId { get; set; }

    /// <summary>
    /// 訂購數量
    /// </summary>
    public int OrderedQty { get; set; }

    /// <summary>
    /// 累計已到貨驗收數量 (支援分批驗收)
    /// </summary>
    public int ReceivedQty { get; set; } = 0;

    /// <summary>
    /// 採購進貨單價
    /// </summary>
    public decimal UnitPrice { get; set; }

    // 導覽屬性
    public PurchaseOrder PurchaseOrder { get; set; } = null!;
    public Product Product { get; set; } = null!;
}
