using Microsoft.EntityFrameworkCore;
using WebAPICore.Api.Data;
using WebAPICore.Api.Dtos;
using WebAPICore.Api.Models.Entities;

namespace WebAPICore.Api.Services;

/// <summary>
/// 庫存異動核心業務實作（支援 ACID 交易與樂觀鎖並發控制）
/// </summary>
public class InventoryService : IInventoryService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<InventoryService> _logger;

    public InventoryService(AppDbContext dbContext, ILogger<InventoryService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<StockMovementResponse> InboundAsync(StockInboundRequest request, CancellationToken cancellationToken = default)
    {
        // 1. 查詢庫存主檔
        var stock = await _dbContext.ProductStocks
            .FirstOrDefaultAsync(s => s.ProductId == request.ProductId, cancellationToken);

        if (stock == null)
        {
            throw new KeyNotFoundException($"找不到商品 ID 為 {request.ProductId} 的庫存記錄。");
        }

        // 2. 開啟資料庫交易 (Transaction) 保證原子性：扣庫存與寫流水帳要嘛全成功，要嘛全撤銷
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var previousQty = stock.CurrentQty;
            stock.CurrentQty += request.Quantity;
            stock.UpdatedAt = DateTime.UtcNow;

            // 建立不可竄改的流水帳記錄
            var movement = new StockMovement
            {
                Id = Guid.NewGuid(),
                ProductId = request.ProductId,
                MovementType = "INBOUND",
                Quantity = request.Quantity,
                PreviousQty = previousQty,
                NewQty = stock.CurrentQty,
                Reason = request.Reason ?? "正常入庫",
                Operator = request.Operator,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.StockMovements.Add(movement);

            // 提交變更
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation("商品 {ProductId} 入庫成功，異動前: {Prev}，異動後: {New}",
                request.ProductId, previousQty, stock.CurrentQty);

            return new StockMovementResponse(
                movement.Id,
                movement.ProductId,
                movement.MovementType,
                movement.Quantity,
                movement.PreviousQty,
                movement.NewQty,
                movement.Reason,
                movement.Operator,
                movement.CreatedAt
            );
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "商品 {ProductId} 入庫失敗，交易已復原", request.ProductId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<StockMovementResponse> OutboundAsync(StockOutboundRequest request, CancellationToken cancellationToken = default)
    {
        // 1. 查詢庫存主檔
        var stock = await _dbContext.ProductStocks
            .FirstOrDefaultAsync(s => s.ProductId == request.ProductId, cancellationToken);

        if (stock == null)
        {
            throw new KeyNotFoundException($"找不到商品 ID 為 {request.ProductId} 的庫存記錄。");
        }

        // 2. 業務層防護：檢查庫存是否充足
        if (stock.CurrentQty < request.Quantity)
        {
            throw new InvalidOperationException($"庫存不足！當前現有庫存為 {stock.CurrentQty} 件，請求出庫數量為 {request.Quantity} 件。");
        }

        // 3. 開啟交易進行出庫與日誌寫入
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var previousQty = stock.CurrentQty;
            stock.CurrentQty -= request.Quantity;
            stock.UpdatedAt = DateTime.UtcNow;

            var movement = new StockMovement
            {
                Id = Guid.NewGuid(),
                ProductId = request.ProductId,
                MovementType = "OUTBOUND",
                Quantity = -request.Quantity,
                PreviousQty = previousQty,
                NewQty = stock.CurrentQty,
                Reason = request.Reason ?? "銷貨出庫",
                Operator = request.Operator,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.StockMovements.Add(movement);

            // 此處若發生並發搶購衝突，EF Core 的 xmin 樂觀鎖或 DB CHECK 約束會阻止超賣
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation("商品 {ProductId} 出庫成功，異動前: {Prev}，異動後: {New}",
                request.ProductId, previousQty, stock.CurrentQty);

            return new StockMovementResponse(
                movement.Id,
                movement.ProductId,
                movement.MovementType,
                movement.Quantity,
                movement.PreviousQty,
                movement.NewQty,
                movement.Reason,
                movement.Operator,
                movement.CreatedAt
            );
        }
        catch (DbUpdateConcurrencyException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogWarning(ex, "商品 {ProductId} 出庫觸發樂觀鎖並發衝突", request.ProductId);
            throw new InvalidOperationException("系統偵測到並發更新衝突，請稍後重試。", ex);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "商品 {ProductId} 出庫失敗，交易已復原", request.ProductId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProductDetailResponse>> GetLowStockProductsAsync(CancellationToken cancellationToken = default)
    {
        var lowStockProducts = await _dbContext.Products
            .AsNoTracking()
            .Include(p => p.Stock)
            .Where(p => p.Stock != null && p.Stock.CurrentQty <= p.SafetyStock)
            .Select(p => new ProductDetailResponse(
                p.Id,
                p.Sku,
                p.Name,
                p.Category,
                p.UnitPrice,
                p.SafetyStock,
                p.Stock != null ? p.Stock.CurrentQty : 0,
                p.Stock != null ? p.Stock.OnOrderQty : 0,
                true,
                p.CreatedAt
            ))
            .ToListAsync(cancellationToken);

        return lowStockProducts;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StockMovementResponse>> GetMovementsByProductAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        var movements = await _dbContext.StockMovements
            .AsNoTracking()
            .Where(m => m.ProductId == productId)
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => new StockMovementResponse(
                m.Id,
                m.ProductId,
                m.MovementType,
                m.Quantity,
                m.PreviousQty,
                m.NewQty,
                m.Reason,
                m.Operator,
                m.CreatedAt
            ))
            .ToListAsync(cancellationToken);

        return movements;
    }

    /// <inheritdoc />
    public async Task<StockMovementResponse> AdjustStocktakeAsync(StocktakeAdjustmentRequest request, CancellationToken cancellationToken = default)
    {
        var stock = await _dbContext.ProductStocks
            .FirstOrDefaultAsync(s => s.ProductId == request.ProductId, cancellationToken);

        if (stock == null)
        {
            throw new KeyNotFoundException($"找不到商品 ID 為 {request.ProductId} 的庫存記錄。");
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var previousQty = stock.CurrentQty;
            var diff = request.ActualQty - previousQty;

            stock.CurrentQty = request.ActualQty;
            stock.UpdatedAt = DateTime.UtcNow;

            var defaultReason = diff >= 0 ? $"盤盈調整 (+{diff})" : $"盤虧調整 ({diff})";
            var movement = new StockMovement
            {
                Id = Guid.NewGuid(),
                ProductId = request.ProductId,
                MovementType = "ADJUSTMENT",
                Quantity = diff,
                PreviousQty = previousQty,
                NewQty = stock.CurrentQty,
                Reason = request.Reason ?? defaultReason,
                Operator = request.Operator,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.StockMovements.Add(movement);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation("商品 {ProductId} 盤點調整完成，異動前: {Prev}，實盤覆寫為: {New}，差額: {Diff}，操作員: {Op}",
                request.ProductId, previousQty, stock.CurrentQty, diff, request.Operator);

            return new StockMovementResponse(
                movement.Id,
                movement.ProductId,
                movement.MovementType,
                movement.Quantity,
                movement.PreviousQty,
                movement.NewQty,
                movement.Reason,
                movement.Operator,
                movement.CreatedAt
            );
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "商品 {ProductId} 盤點調整失敗，交易已復原", request.ProductId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<StockMovementResponse> ReturnInboundAsync(StockReturnRequest request, CancellationToken cancellationToken = default)
    {
        var inboundRequest = new StockInboundRequest(
            request.ProductId,
            request.Quantity,
            request.Reason ?? "退料入庫",
            request.Operator
        );

        return await InboundAsync(inboundRequest, cancellationToken);
    }
}