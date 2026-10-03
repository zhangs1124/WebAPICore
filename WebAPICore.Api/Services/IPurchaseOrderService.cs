using WebAPICore.Api.Dtos;

namespace WebAPICore.Api.Services;

/// <summary>
/// 採購訂購單核心業務介面 (請購訂購二合一、進貨驗收、在途量連動)
/// </summary>
public interface IPurchaseOrderService
{
    /// <summary>
    /// 建立採購單草稿
    /// </summary>
    Task<PurchaseOrderResponse> CreateAsync(CreatePurchaseOrderRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 主管審核採購單（狀態轉為 Approved，並將採購商品數量累加至在途量 OnOrderQty）
    /// </summary>
    Task<PurchaseOrderResponse> ApproveAsync(Guid id, string approvedBy, CancellationToken cancellationToken = default);

    /// <summary>
    /// 依 ID 取得採購單詳細資料（含明細與到貨進度）
    /// </summary>
    Task<PurchaseOrderResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查詢採購單清單（可依狀態過濾）
    /// </summary>
    Task<IReadOnlyList<PurchaseOrderResponse>> GetAllAsync(string? status = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 針對採購單進行到貨驗收入庫（更新已到貨量、扣減在途量、增加入庫現有庫存；到齊自動結案）
    /// </summary>
    Task<PurchaseOrderResponse> ReceiveInboundAsync(ReceivePurchaseOrderRequest request, CancellationToken cancellationToken = default);
}
