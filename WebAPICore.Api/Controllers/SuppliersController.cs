using Microsoft.AspNetCore.Mvc;
using WebAPICore.Api.Dtos;
using WebAPICore.Api.Filters;
using WebAPICore.Api.Services;

namespace WebAPICore.Api.Controllers;

/// <summary>
/// 供應商資料管理 API 控制器
/// </summary>
[ApiController]
[Route("api/v1/suppliers")]
[Produces("application/json")]
public class SuppliersController : ControllerBase
{
    private readonly ISupplierService _supplierService;
    private readonly ILogger<SuppliersController> _logger;

    public SuppliersController(ISupplierService supplierService, ILogger<SuppliersController> logger)
    {
        _supplierService = supplierService;
        _logger = logger;
    }

    /// <summary>
    /// 查詢所有供應商清單 (支援啟用狀態篩選)
    /// </summary>
    [HttpGet]
    [CustomAuth(Roles = "Admin,Manager,Purchaser")]
    [ProducesResponseType(typeof(IReadOnlyList<SupplierResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SupplierResponse>>> GetAll(
        [FromQuery] bool? activeOnly,
        CancellationToken cancellationToken)
    {
        var suppliers = await _supplierService.GetAllAsync(activeOnly, cancellationToken);
        return Ok(suppliers);
    }

    /// <summary>
    /// 依供應商 ID 查詢單筆詳情
    /// </summary>
    [HttpGet("{id:guid}")]
    [CustomAuth(Roles = "Admin,Manager,Purchaser")]
    [ProducesResponseType(typeof(SupplierResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SupplierResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var supplier = await _supplierService.GetByIdAsync(id, cancellationToken);
            return Ok(supplier);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails
            {
                Title = "找不到供應商",
                Detail = ex.Message,
                Status = StatusCodes.Status404NotFound
            });
        }
    }

    /// <summary>
    /// 建立新供應商 (採購人員、主管與管理員專屬)
    /// </summary>
    [HttpPost]
    [CustomAuth(Roles = "Admin,Manager,Purchaser")]
    [ProducesResponseType(typeof(SupplierResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SupplierResponse>> Create(
        [FromBody] CreateSupplierRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _supplierService.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "建立供應商失敗",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
    }

    /// <summary>
    /// 更新供應商基本資料 (採購人員、主管與管理員專屬)
    /// </summary>
    [HttpPut("{id:guid}")]
    [CustomAuth(Roles = "Admin,Manager,Purchaser")]
    [ProducesResponseType(typeof(SupplierResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SupplierResponse>> Update(
        Guid id,
        [FromBody] UpdateSupplierRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _supplierService.UpdateAsync(id, request, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails
            {
                Title = "找不到供應商",
                Detail = ex.Message,
                Status = StatusCodes.Status404NotFound
            });
        }
    }

    /// <summary>
    /// 切換供應商啟用/停用合作狀態 (主管與管理員專屬)
    /// </summary>
    [HttpPost("{id:guid}/toggle-active")]
    [CustomAuth(Roles = "Admin,Manager")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<bool>> ToggleActive(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var isActive = await _supplierService.ToggleActiveAsync(id, cancellationToken);
            return Ok(new { id, isActive });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails
            {
                Title = "找不到供應商",
                Detail = ex.Message,
                Status = StatusCodes.Status404NotFound
            });
        }
    }
}

