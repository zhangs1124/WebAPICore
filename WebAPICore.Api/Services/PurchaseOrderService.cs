using Microsoft.EntityFrameworkCore;
using WebAPICore.Api.Data;
using WebAPICore.Api.Dtos;
using WebAPICore.Api.Models.Entities;

namespace WebAPICore.Api.Services;

/// <summary>
/// 採購訂購單業務實作
/// </summary>
public class PurchaseOrderService : IPurchaseOrderService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<PurchaseOrderService> _logger;

    public PurchaseOrderService(AppDbContext dbContext, ILogger<PurchaseOrderService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<PurchaseOrderResponse> CreateAsync(CreatePurchaseOrderRequest request, CancellationToken cancellationToken = default)
    {
        // 1. 驗證商品是否存在
        var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
        var existingProducts = await _dbContext.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        if (existingProducts.Count != productIds.Count)
        {
            throw new KeyNotFoundException("採購明細中包含不存在的商品 ID。");
        }

        // 2. 自動生成採購單號 (PO-yyyyMMdd-GUID前4碼)
        var datePrefix = DateTime.UtcNow.ToString("yyyyMMdd");
        var shortSuffix = Guid.NewGuid().ToString("N")[..4].ToUpperInvariant();
        var poNumber = $"PO-{datePrefix}-{shortSuffix}";

        // 3. 建立實體
        var po = new PurchaseOrder
        {
            Id = Guid.NewGuid(),
            PoNumber = poNumber,
            SupplierName = request.SupplierName,
            SupplierId = request.SupplierId,
            Department = string.IsNullOrWhiteSpace(request.Department) ? "總務課" : request.Department,
            Status = "Draft",
            Remark = request.Remark,
            CreatedBy = request.CreatedBy,
            CreatedAt = DateTime.UtcNow,
            TotalAmount = request.Items.Sum(i => i.OrderedQty * i.UnitPrice)
        };

        foreach (var item in request.Items)
        {
            po.Items.Add(new PurchaseOrderItem
            {
                Id = Guid.NewGuid(),
                PurchaseOrderId = po.Id,
                ProductId = item.ProductId,
                OrderedQty = item.OrderedQty,
                ReceivedQty = 0,
                UnitPrice = item.UnitPrice
            });
        }

        _dbContext.PurchaseOrders.Add(po);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("成功建立採購單 {PoNumber}，總金額: {Total}", po.PoNumber, po.TotalAmount);

        return MapToResponse(po, existingProducts);
    }

    /// <inheritdoc />
    public async Task<PurchaseOrderResponse> ApproveAsync(Guid id, string approvedBy, CancellationToken cancellationToken = default)
    {
        var po = await _dbContext.PurchaseOrders
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (po == null)
        {
            throw new KeyNotFoundException($"找不到採購單 ID 為 {id} 的資料。");
        }

        if (po.Status != "Draft")
        {
            throw new InvalidOperationException($"採購單當前狀態為【{po.Status}】，僅有草稿 (Draft) 狀態才可執行審核。");
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            po.Status = "Approved";
            po.ApprovedBy = approvedBy;
            po.ApprovedAt = DateTime.UtcNow;

            // 將各明細項之訂購數量累加至商品的在途量 (OnOrderQty)
            var productIds = po.Items.Select(i => i.ProductId).Distinct().ToList();
            var stocks = await _dbContext.ProductStocks
                .Where(s => productIds.Contains(s.ProductId))
                .ToDictionaryAsync(s => s.ProductId, cancellationToken);

            foreach (var item in po.Items)
            {
                if (stocks.TryGetValue(item.ProductId, out var stock))
                {
                    stock.OnOrderQty += item.OrderedQty;
                    stock.UpdatedAt = DateTime.UtcNow;
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation("採購單 {PoNumber} 審核通過，核准主管: {ApprovedBy}，已同步累加在途採購量",
                po.PoNumber, approvedBy);

            return await GetByIdAsync(po.Id, cancellationToken);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "採購單 {Id} 審核失敗，交易已撤銷", id);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<PurchaseOrderResponse> ReceiveInboundAsync(ReceivePurchaseOrderRequest request, CancellationToken cancellationToken = default)
    {
        var po = await _dbContext.PurchaseOrders
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == request.PurchaseOrderId, cancellationToken);

        if (po == null)
        {
            throw new KeyNotFoundException($"找不到採購單 ID 為 {request.PurchaseOrderId} 的資料。");
        }

        if (po.Status != "Approved")
        {
            throw new InvalidOperationException($"採購單當前狀態為【{po.Status}】，只有已核准 (Approved) 狀態的單據才可進行驗收入庫。");
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var productIds = po.Items.Select(i => i.ProductId).Distinct().ToList();
            var stocks = await _dbContext.ProductStocks
                .Where(s => productIds.Contains(s.ProductId))
                .ToDictionaryAsync(s => s.ProductId, cancellationToken);

            foreach (var reqItem in request.Items)
            {
                var poItem = po.Items.FirstOrDefault(i => i.Id == reqItem.ItemId);
                if (poItem == null)
                {
                    throw new KeyNotFoundException($"採購單中找不到明細 ID 為 {reqItem.ItemId} 的商品。");
                }

                var remainingQty = poItem.OrderedQty - poItem.ReceivedQty;
                if (reqItem.Quantity > remainingQty)
                {
                    throw new InvalidOperationException($"驗收數量 ({reqItem.Quantity}) 超過採購單尚未到貨數量 ({remainingQty})。");
                }

                // 1. 更新單身累計到貨量
                poItem.ReceivedQty += reqItem.Quantity;

                // 2. 更新即時庫存：扣減在途量、增加入庫現有庫存
                if (stocks.TryGetValue(poItem.ProductId, out var stock))
                {
                    var previousQty = stock.CurrentQty;
                    stock.OnOrderQty = Math.Max(0, stock.OnOrderQty - reqItem.Quantity);
                    stock.CurrentQty += reqItem.Quantity;
                    stock.UpdatedAt = DateTime.UtcNow;

                    // 3. 寫入流水帳
                    var movement = new StockMovement
                    {
                        Id = Guid.NewGuid(),
                        ProductId = poItem.ProductId,
                        MovementType = "INBOUND",
                        Quantity = reqItem.Quantity,
                        PreviousQty = previousQty,
                        NewQty = stock.CurrentQty,
                        Reason = $"採購單 {po.PoNumber} 驗收入庫",
                        Operator = request.Operator,
                        CreatedAt = DateTime.UtcNow
                    };

                    _dbContext.StockMovements.Add(movement);
                }
            }

            // 檢查是否全數到齊，若是則自動結案
            if (po.Items.All(i => i.ReceivedQty >= i.OrderedQty))
            {
                po.Status = "Completed";
                _logger.LogInformation("採購單 {PoNumber} 所有商品已全數驗收到貨，自動轉為 Completed 結案", po.PoNumber);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return await GetByIdAsync(po.Id, cancellationToken);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "採購單 {Id} 驗收入庫失敗，交易已撤銷", request.PurchaseOrderId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<PurchaseOrderResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var po = await _dbContext.PurchaseOrders
            .AsNoTracking()
            .Include(p => p.Items)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (po == null)
        {
            throw new KeyNotFoundException($"找不到採購單 ID 為 {id} 的資料。");
        }

        var itemResponses = po.Items.Select(i => new PurchaseOrderItemResponse(
            i.Id,
            i.ProductId,
            i.Product.Name,
            i.Product.Sku,
            i.OrderedQty,
            i.ReceivedQty,
            i.UnitPrice,
            i.OrderedQty * i.UnitPrice
        )).ToList();

        return new PurchaseOrderResponse(
            po.Id,
            po.PoNumber,
            po.SupplierName,
            po.SupplierId,
            po.Department,
            po.Status,
            po.TotalAmount,
            po.Remark,
            po.CreatedBy,
            po.CreatedAt,
            po.ApprovedBy,
            po.ApprovedAt,
            itemResponses
        );
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PurchaseOrderResponse>> GetAllAsync(string? status = null, string? department = null, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.PurchaseOrders
            .AsNoTracking()
            .Include(p => p.Items)
                .ThenInclude(i => i.Product)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(p => p.Status == status);
        }

        // 資料級權限過濾：若指定部門則只查該部門
        if (!string.IsNullOrWhiteSpace(department))
        {
            query = query.Where(p => p.Department == department);
        }

        var list = await query
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);

        return list.Select(po => new PurchaseOrderResponse(
            po.Id,
            po.PoNumber,
            po.SupplierName,
            po.SupplierId,
            po.Department,
            po.Status,
            po.TotalAmount,
            po.Remark,
            po.CreatedBy,
            po.CreatedAt,
            po.ApprovedBy,
            po.ApprovedAt,
            po.Items.Select(i => new PurchaseOrderItemResponse(
                i.Id,
                i.ProductId,
                i.Product.Name,
                i.Product.Sku,
                i.OrderedQty,
                i.ReceivedQty,
                i.UnitPrice,
                i.OrderedQty * i.UnitPrice
            )).ToList()
        )).ToList();
    }

    private static PurchaseOrderResponse MapToResponse(PurchaseOrder po, Dictionary<Guid, Product> products)
    {
        var itemResponses = po.Items.Select(i =>
        {
            var productName = products.TryGetValue(i.ProductId, out var prod) ? prod.Name : string.Empty;
            var sku = prod?.Sku ?? string.Empty;
            return new PurchaseOrderItemResponse(
                i.Id,
                i.ProductId,
                productName,
                sku,
                i.OrderedQty,
                i.ReceivedQty,
                i.UnitPrice,
                i.OrderedQty * i.UnitPrice
            );
        }).ToList();

        return new PurchaseOrderResponse(
            po.Id,
            po.PoNumber,
            po.SupplierName,
            po.SupplierId,
            po.Department,
            po.Status,
            po.TotalAmount,
            po.Remark,
            po.CreatedBy,
            po.CreatedAt,
            po.ApprovedBy,
            po.ApprovedAt,
            itemResponses
        );
    }
}
