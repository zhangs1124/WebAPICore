using Microsoft.AspNetCore.Mvc;
using WebAPICore.Api.Dtos;
using WebAPICore.Api.Filters;
using WebAPICore.Api.Services;

namespace WebAPICore.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class InventoryController : ControllerBase
{
    private readonly IInventoryService _inventoryService;

    public InventoryController(IInventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    /// <summary>
    /// 庫存入庫（進貨作業）
    /// </summary>
    [HttpPost("inbound")]
    [CustomAuth(Roles = "Admin,Manager,Warehouse")]
    [ProducesResponseType(typeof(StockMovementResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StockMovementResponse>> Inbound(
        [FromBody] StockInboundRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _inventoryService.InboundAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails
            {
                Title = "找不到商品庫存",
                Detail = ex.Message,
                Status = StatusCodes.Status404NotFound
            });
        }
    }

    /// <summary>
    /// 庫存出庫（銷貨 / 領料，含防超賣與 ACID 交易保證）
    /// </summary>
    [HttpPost("outbound")]
    [CustomAuth(Roles = "Admin,Manager,Warehouse")]
    [ProducesResponseType(typeof(StockMovementResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StockMovementResponse>> Outbound(
        [FromBody] StockOutboundRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _inventoryService.OutboundAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails
            {
                Title = "找不到商品庫存",
                Detail = ex.Message,
                Status = StatusCodes.Status404NotFound
            });
        }
        catch (InvalidOperationException ex)
        {
            // 庫存不足或並發衝突
            return BadRequest(new ProblemDetails
            {
                Title = "出庫操作失敗",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
    }

    /// <summary>
    /// 取得低於安全庫存警戒線的商品清單
    /// </summary>
    [HttpGet("low-stock")]
    [CustomAuth]
    [ProducesResponseType(typeof(IReadOnlyList<ProductDetailResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ProductDetailResponse>>> GetLowStock(CancellationToken cancellationToken)
    {
        var result = await _inventoryService.GetLowStockProductsAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// 查詢指定商品的歷史出入庫流水帳記錄
    /// </summary>
    [HttpGet("{productId:guid}/movements")]
    [CustomAuth]
    [ProducesResponseType(typeof(IReadOnlyList<StockMovementResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<StockMovementResponse>>> GetMovements(
        Guid productId,
        CancellationToken cancellationToken)
    {
        var result = await _inventoryService.GetMovementsByProductAsync(productId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// 庫存盤點調整（主管權限：以現場實盤數量直接校正，記錄盤盈虧，限制 Admin, Manager）
    /// </summary>
    [HttpPost("stocktake")]
    [CustomAuth(Roles = "Admin,Manager")]
    [ProducesResponseType(typeof(StockMovementResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StockMovementResponse>> Stocktake(
        [FromBody] StocktakeAdjustmentRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _inventoryService.AdjustStocktakeAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails
            {
                Title = "找不到商品庫存",
                Detail = ex.Message,
                Status = StatusCodes.Status404NotFound
            });
        }
    }

    /// <summary>
    /// 退料入庫（領料退回或客退入庫）
    /// </summary>
    [HttpPost("return")]
    [CustomAuth(Roles = "Admin,Manager,Warehouse")]
    [ProducesResponseType(typeof(StockMovementResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StockMovementResponse>> ReturnInbound(
        [FromBody] StockReturnRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _inventoryService.ReturnInboundAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails
            {
                Title = "找不到商品庫存",
                Detail = ex.Message,
                Status = StatusCodes.Status404NotFound
            });
        }
    }
}