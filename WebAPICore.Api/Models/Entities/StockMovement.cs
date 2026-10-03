namespace WebAPICore.Api.Models.Entities;

/// <summary>
/// 庫存異動流水帳（不可竄改的稽核記錄）
/// </summary>
public class StockMovement
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// 關聯商品 ID
    /// </summary>
    public Guid ProductId { get; set; }

    /// <summary>
    /// 異動類型：INBOUND (入庫), OUTBOUND (出庫), ADJUSTMENT (盤點調整)
    /// </summary>
    public string MovementType { get; set; } = string.Empty;

    /// <summary>
    /// 異動數量（入庫為正，出庫為負）
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// 異動前庫存
    /// </summary>
    public int PreviousQty { get; set; }

    /// <summary>
    /// 異動後庫存
    /// </summary>
    public int NewQty { get; set; }

    /// <summary>
    /// 異動原因 / 單號備註
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// 操作人員代碼或系統名稱
    /// </summary>
    public string Operator { get; set; } = "System";

    /// <summary>
    /// 發生時間 (UTC)
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // 導覽屬性
    public Product Product { get; set; } = null!;
}