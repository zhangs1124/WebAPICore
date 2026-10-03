using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WebAPICore.Api.Dtos;
using WebAPICore.Api.Filters;
using WebAPICore.Api.Models;
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
    /// 建立採購訂購單草稿 (請購暨訂購合一，限制 Purchaser, Manager, Admin)
    /// </summary>
    [HttpPost]
    [CustomAuth(Roles = "Admin,Manager,Purchaser")]
    [ProducesResponseType(typeof(PurchaseOrderResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PurchaseOrderResponse>> Create(
        [FromBody] CreatePurchaseOrderRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var session = GetCurrentSession();
            var department = !string.IsNullOrWhiteSpace(request.Department)
                ? request.Department
                : (session?.Department ?? "總務課");

            var createdBy = !string.IsNullOrWhiteSpace(request.CreatedBy)
                ? request.CreatedBy
                : (session?.DisplayName ?? "採購專員");

            var finalRequest = request with { Department = department, CreatedBy = createdBy };

            var result = await _purchaseOrderService.CreateAsync(finalRequest, cancellationToken);
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
    /// 主管審核核准採購單（狀態變更為 Approved，並自動累加在途採購量 OnOrderQty，限制 Manager, Admin）
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    [CustomAuth(Roles = "Admin,Manager")]
    [ProducesResponseType(typeof(PurchaseOrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PurchaseOrderResponse>> Approve(
        Guid id,
        [FromQuery] string? approvedBy,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var session = GetCurrentSession();
            var finalApprover = !string.IsNullOrWhiteSpace(approvedBy)
                ? approvedBy
                : (session?.DisplayName ?? "倉儲主管");

            var result = await _purchaseOrderService.ApproveAsync(id, finalApprover, cancellationToken);
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
    [CustomAuth]
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
    /// 查詢採購單列表（可依狀態過濾，依角色落實部門/單位資料級權限隔離）
    /// </summary>
    [HttpGet]
    [CustomAuth]
    [ProducesResponseType(typeof(IReadOnlyList<PurchaseOrderResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PurchaseOrderResponse>>> GetAll(
        [FromQuery] string? status,
        [FromQuery] string? department,
        CancellationToken cancellationToken)
    {
        var session = GetCurrentSession();
        string? targetDept = department;

        // 若不是主管 (Manager) 或管理員 (Admin)，強制只能檢視自己部門的單據
        if (session != null && session.Role != "Admin" && session.Role != "Manager")
        {
            targetDept = session.Department;
        }

        var result = await _purchaseOrderService.GetAllAsync(status, targetDept, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// 採購單貨到驗收入庫（扣減在途量、增加入庫現有庫存；到齊自動轉 Completed，限制 Warehouse, Manager, Admin）
    /// </summary>
    [HttpPost("receive")]
    [CustomAuth(Roles = "Admin,Manager,Warehouse")]
    [ProducesResponseType(typeof(PurchaseOrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PurchaseOrderResponse>> Receive(
        [FromBody] ReceivePurchaseOrderRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var session = GetCurrentSession();
            var operatorName = !string.IsNullOrWhiteSpace(request.Operator)
                ? request.Operator
                : (session?.DisplayName ?? "現場倉管員");

            var finalRequest = request with { Operator = operatorName };
            var result = await _purchaseOrderService.ReceiveInboundAsync(finalRequest, cancellationToken);
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

    private UserSession? GetCurrentSession()
    {
        var sessionJson = HttpContext.Session.GetString("UserSession");
        if (string.IsNullOrEmpty(sessionJson)) return null;
        try
        {
            return JsonSerializer.Deserialize<UserSession>(sessionJson);
        }
        catch
        {
            return null;
        }
    }
}

