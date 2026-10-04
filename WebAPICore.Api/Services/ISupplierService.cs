using WebAPICore.Api.Dtos;

namespace WebAPICore.Api.Services;

/// <summary>
/// 供應商主檔業務服務介面
/// </summary>
public interface ISupplierService
{
    /// <summary>
    /// 建立供應商主檔
    /// </summary>
    Task<SupplierResponse> CreateAsync(CreateSupplierRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查詢所有供應商清單 (可依是否合作中過濾)
    /// </summary>
    Task<IReadOnlyList<SupplierResponse>> GetAllAsync(bool? activeOnly = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 依 ID 取得供應商詳細資料
    /// </summary>
    Task<SupplierResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新供應商基本主檔資料
    /// </summary>
    Task<SupplierResponse> UpdateAsync(Guid id, UpdateSupplierRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 切換供應商啟用/停用合作狀態
    /// </summary>
    Task<bool> ToggleActiveAsync(Guid id, CancellationToken cancellationToken = default);
}

