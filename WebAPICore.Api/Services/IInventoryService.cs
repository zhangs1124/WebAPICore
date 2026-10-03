using WebAPICore.Api.Dtos;

namespace WebAPICore.Api.Services;

/// <summary>
/// 庫存異動核心業務介面
/// </summary>
public interface IInventoryService
{
    /// <summary>
    /// 庫存入庫作業（進貨）
    /// </summary>
    Task<StockMovementResponse> InboundAsync(StockInboundRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 庫存出庫作業（銷貨 / 領料，含防超賣與交易保證）
    /// </summary>
    Task<StockMovementResponse> OutboundAsync(StockOutboundRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得低於安全庫存警戒線的商品清單
    /// </summary>
    Task<IReadOnlyList<ProductDetailResponse>> GetLowStockProductsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得指定商品的歷史異動流水帳
    /// </summary>
    Task<IReadOnlyList<StockMovementResponse>> GetMovementsByProductAsync(Guid productId, CancellationToken cancellationToken = default);
}