using System.ComponentModel.DataAnnotations;

namespace WebAPICore.Api.Dtos;

/// <summary>
/// 建立採購單明細項請求
/// </summary>
public record CreatePurchaseOrderItemRequest(
    [Required(ErrorMessage = "必須指定商品 ID")]
    Guid ProductId,

    [Range(1, 100000, ErrorMessage = "採購數量必須大於 0")]
    int OrderedQty,

    [Range(0.01, 10000000, ErrorMessage = "進貨單價必須大於 0")]
    decimal UnitPrice
);

/// <summary>
/// 建立採購單請求 DTO
/// </summary>
public record CreatePurchaseOrderRequest(
    [Required(ErrorMessage = "供應商名稱不可為空")]
    [MaxLength(100, ErrorMessage = "供應商名稱不可超過 100 個字元")]
    string SupplierName,

    Guid? SupplierId = null,

    [MaxLength(50, ErrorMessage = "部門名稱不可超過 50 個字元")]
    string Department = "總務課",

    string? Remark = null,

    [Required(ErrorMessage = "採購明細不可為空")]
    [MinLength(1, ErrorMessage = "至少需包含一項採購明細")]
    List<CreatePurchaseOrderItemRequest> Items = null!,

    string CreatedBy = "Purchaser"
);

/// <summary>
/// 採購單明細回應 DTO
/// </summary>
public record PurchaseOrderItemResponse(
    Guid Id,
    Guid ProductId,
    string ProductName,
    string Sku,
    int OrderedQty,
    int ReceivedQty,
    decimal UnitPrice,
    decimal Subtotal
);

/// <summary>
/// 採購單主檔回應 DTO
/// </summary>
public record PurchaseOrderResponse(
    Guid Id,
    string PoNumber,
    string SupplierName,
    Guid? SupplierId,
    string Department,
    string Status,
    decimal TotalAmount,
    string? Remark,
    string CreatedBy,
    DateTime CreatedAt,
    string? ApprovedBy,
    DateTime? ApprovedAt,
    IReadOnlyList<PurchaseOrderItemResponse> Items
);

/// <summary>
/// 採購單單項到貨驗收請求
/// </summary>
public record ReceivePurchaseOrderItemRequest(
    [Required(ErrorMessage = "必須指定明細項 ID")]
    Guid ItemId,

    [Range(1, 100000, ErrorMessage = "驗收數量必須大於 0")]
    int Quantity
);

/// <summary>
/// 採購單整批到貨驗收入庫請求 DTO
/// </summary>
public record ReceivePurchaseOrderRequest(
    [Required(ErrorMessage = "必須指定採購單 ID")]
    Guid PurchaseOrderId,

    [Required(ErrorMessage = "驗收明細清單不可為空")]
    [MinLength(1, ErrorMessage = "至少需填寫一項驗收商品")]
    List<ReceivePurchaseOrderItemRequest> Items,

    [MaxLength(50, ErrorMessage = "驗收操作人員不可超過 50 個字元")]
    string Operator = "Warehouse"
);

