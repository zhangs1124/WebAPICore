using Microsoft.EntityFrameworkCore;
using WebAPICore.Api.Data;
using WebAPICore.Api.Dtos;
using WebAPICore.Api.Models.Entities;

namespace WebAPICore.Api.Services;

/// <summary>
/// 供應商業務服務實作
/// </summary>
public class SupplierService : ISupplierService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<SupplierService> _logger;

    public SupplierService(AppDbContext dbContext, ILogger<SupplierService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<SupplierResponse> CreateAsync(CreateSupplierRequest request, CancellationToken cancellationToken = default)
    {
        var codeExists = await _dbContext.Suppliers
            .AnyAsync(s => s.Code == request.Code.Trim(), cancellationToken);

        if (codeExists)
        {
            throw new InvalidOperationException($"供應商代碼【{request.Code}】已經存在。");
        }

        var supplier = new Supplier
        {
            Id = Guid.NewGuid(),
            Code = request.Code.Trim().ToUpperInvariant(),
            Name = request.Name.Trim(),
            ContactPerson = request.ContactPerson.Trim(),
            Phone = request.Phone.Trim(),
            Email = request.Email?.Trim(),
            Address = request.Address?.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Suppliers.Add(supplier);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("成功建立供應商主檔: {Code} - {Name}", supplier.Code, supplier.Name);

        return MapToResponse(supplier);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SupplierResponse>> GetAllAsync(bool? activeOnly = null, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Suppliers.AsNoTracking().AsQueryable();

        if (activeOnly.HasValue)
        {
            query = query.Where(s => s.IsActive == activeOnly.Value);
        }

        var list = await query
            .OrderBy(s => s.Code)
            .ToListAsync(cancellationToken);

        return list.Select(MapToResponse).ToList();
    }

    /// <inheritdoc />
    public async Task<SupplierResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var supplier = await _dbContext.Suppliers
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (supplier == null)
        {
            throw new KeyNotFoundException($"找不到 ID 為 {id} 的供應商。");
        }

        return MapToResponse(supplier);
    }

    /// <inheritdoc />
    public async Task<SupplierResponse> UpdateAsync(Guid id, UpdateSupplierRequest request, CancellationToken cancellationToken = default)
    {
        var supplier = await _dbContext.Suppliers.FindAsync([id], cancellationToken);
        if (supplier == null)
        {
            throw new KeyNotFoundException($"找不到 ID 為 {id} 的供應商。");
        }

        supplier.Name = request.Name.Trim();
        supplier.ContactPerson = request.ContactPerson.Trim();
        supplier.Phone = request.Phone.Trim();
        supplier.Email = request.Email?.Trim();
        supplier.Address = request.Address?.Trim();

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("成功更新供應商主檔: {Code} - {Name}", supplier.Code, supplier.Name);
        return MapToResponse(supplier);
    }

    /// <inheritdoc />
    public async Task<bool> ToggleActiveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var supplier = await _dbContext.Suppliers.FindAsync([id], cancellationToken);
        if (supplier == null)
        {
            throw new KeyNotFoundException($"找不到 ID 為 {id} 的供應商。");
        }

        supplier.IsActive = !supplier.IsActive;
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("供應商 {Code} 狀態切換為: {IsActive}", supplier.Code, supplier.IsActive);
        return supplier.IsActive;
    }

    private static SupplierResponse MapToResponse(Supplier s) =>
        new(s.Id, s.Code, s.Name, s.ContactPerson, s.Phone, s.Email, s.Address, s.IsActive, s.CreatedAt);
}

