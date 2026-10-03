namespace WebAPICore.Api.Models.Entities;

/// <summary>
/// 採購訂購單實體 (請購與訂購合一)
/// </summary>
public class PurchaseOrder
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// 採購單號 (如：PO-20261003-001)
    /// </summary>
    public string PoNumber { get; set; } = string.Empty;

    /// <summary>
    /// 供應商名稱
    /// </summary>
    public string SupplierName { get; set; } = string.Empty;

    /// <summary>
    /// 關聯之供應商主檔 ID (選填)
    /// </summary>
    public Guid? SupplierId { get; set; }

    /// <summary>
    /// 申請單位/部門代碼 (例如：總務課、製造課、研發課)
    /// </summary>
    public string Department { get; set; } = "總務課";

    /// <summary>
    /// 採購單狀態：Draft(草稿/待審), Approved(已核准下單), Completed(全數到貨結案), Cancelled(作廢)
    /// </summary>
    public string Status { get; set; } = "Draft";

    /// <summary>
    /// 採購總金額
    /// </summary>
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// 備註說明
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>
    /// 建單人員
    /// </summary>
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>
    /// 建單時間 (UTC)
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 審核主管人員
    /// </summary>
    public string? ApprovedBy { get; set; }

    /// <summary>
    /// 審核時間 (UTC)
    /// </summary>
    public DateTime? ApprovedAt { get; set; }

    // 導覽屬性：單身明細與供應商
    public Supplier? Supplier { get; set; }
    public ICollection<PurchaseOrderItem> Items { get; set; } = new List<PurchaseOrderItem>();
}
