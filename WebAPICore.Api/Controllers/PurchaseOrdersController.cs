using Microsoft.AspNetCore.Mvc;
using WebAPICore.Api.Dtos;
using WebAPICore.Api.Services;

namespace WebAPICore.Api.Controllers;

[ApiController]
[Route("api/v1/purchase-orders")]
[Produces("application/json")]
public class PurchaseOrdersController : ControllerBase
{
    private readonly IPurchaseOrderService _purchaseOrderService;

    public PurchaseOrdersController(IPurchaseOrderService purchaseOrderService)
    {
        _purchaseOrderService = purchaseOrderService;
    }

    /// <summary>
    /// 建立採購訂購單草稿 (請購暨訂購合一)
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(PurchaseOrderResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PurchaseOrderResponse>> Create(
        [FromBody] CreatePurchaseOrderRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _purchaseOrderService.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails
            {
                Title = "採購商品不存在",
                Detail = ex.Message,
                Status = StatusCodes.Status404NotFound
            });
        }
    }

    /// <summary>
    /// 主管審核核准採購單（狀態變更為 Approved，並自動累加在途採購量 OnOrderQty）
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    [ProducesResponseType(typeof(PurchaseOrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PurchaseOrderResponse>> Approve(
        Guid id,
        [FromQuery] string approvedBy = "Manager",
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _purchaseOrderService.ApproveAsync(id, approvedBy, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails
            {
                Title = "找不到採購單",
                Detail = ex.Message,
                Status = StatusCodes.Status404NotFound
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "採購單審核失敗",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
    }

    /// <summary>
    /// 查詢採購單詳細資料（含明細與已到貨進度）
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PurchaseOrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PurchaseOrderResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _purchaseOrderService.GetByIdAsync(id, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails
            {
                Title = "找不到採購單",
                Detail = ex.Message,
                Status = StatusCodes.Status404NotFound
            });
        }
    }

    /// <summary>
    /// 查詢採購單列表（可依狀態 Draft / Approved / Completed 過濾）
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PurchaseOrderResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PurchaseOrderResponse>>> GetAll(
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var result = await _purchaseOrderService.GetAllAsync(status, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// 採購單貨到驗收入庫（扣減在途量、增加入庫現有庫存；到齊自動轉 Completed）
    /// </summary>
    [HttpPost("receive")]
    [ProducesResponseType(typeof(PurchaseOrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PurchaseOrderResponse>> Receive(
        [FromBody] ReceivePurchaseOrderRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _purchaseOrderService.ReceiveInboundAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails
            {
                Title = "找不到採購單或明細",
                Detail = ex.Message,
                Status = StatusCodes.Status404NotFound
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "驗收入庫失敗",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
    }
}
