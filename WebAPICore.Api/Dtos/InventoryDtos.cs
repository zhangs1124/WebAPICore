using System.ComponentModel.DataAnnotations;

namespace WebAPICore.Api.Dtos;

/// <summary>
/// 建立商品請求 DTO
/// </summary>
public record CreateProductRequest(
    [Required(ErrorMessage = "商品貨號 (SKU) 不可為空")]
    [MaxLength(50, ErrorMessage = "SKU 長度不可超過 50 個字元")]
    string Sku,

    [Required(ErrorMessage = "商品名稱不可為空")]
    [MaxLength(100, ErrorMessage = "名稱長度不可超過 100 個字元")]
    string Name,

    [Required(ErrorMessage = "商品分類不可為空")]
    [MaxLength(50, ErrorMessage = "分類長度不可超過 50 個字元")]
    string Category,

    [Range(0.01, 10000000, ErrorMessage = "單價必須大於 0")]
    decimal UnitPrice,

    [Range(0, 100000, ErrorMessage = "安全庫存量不可為負數")]
    int SafetyStock = 10,

    [Range(0, 100000, ErrorMessage = "初始庫存量不可為負數")]
    int InitialStock = 0
);

/// <summary>
/// 商品明細與庫存回應 DTO
/// </summary>
public record ProductDetailResponse(
    Guid Id,
    string Sku,
    string Name,
    string Category,
    decimal UnitPrice,
    int SafetyStock,
    int CurrentQty,
    bool IsLowStock,
    DateTime CreatedAt
);

/// <summary>
/// 庫存入庫請求 DTO (進貨)
/// </summary>
public record StockInboundRequest(
    [Required(ErrorMessage = "必須指定商品 ID")]
    Guid ProductId,

    [Range(1, 100000, ErrorMessage = "入庫數量必須大於 0")]
    int Quantity,

    [MaxLength(255, ErrorMessage = "原因長度不可超過 255 個字元")]
    string? Reason,

    [MaxLength(50, ErrorMessage = "操作人員長度不可超過 50 個字元")]
    string Operator = "Admin"
);

/// <summary>
/// 庫存出庫請求 DTO (銷貨 / 領料)
/// </summary>
public record StockOutboundRequest(
    [Required(ErrorMessage = "必須指定商品 ID")]
    Guid ProductId,

    [Range(1, 100000, ErrorMessage = "出庫數量必須大於 0")]
    int Quantity,

    [MaxLength(255, ErrorMessage = "原因長度不可超過 255 個字元")]
    string? Reason,

    [MaxLength(50, ErrorMessage = "操作人員長度不可超過 50 個字元")]
    string Operator = "Admin"
);

/// <summary>
/// 庫存異動流水帳回應 DTO
/// </summary>
public record StockMovementResponse(
    Guid Id,
    Guid ProductId,
    string MovementType,
    int Quantity,
    int PreviousQty,
    int NewQty,
    string? Reason,
    string Operator,
    DateTime CreatedAt
);

/// <summary>
/// 通用分頁查詢回應封裝
/// </summary>
public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int PageNumber,
    int PageSize
)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;
}