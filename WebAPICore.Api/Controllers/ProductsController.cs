using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebAPICore.Api.Data;
using WebAPICore.Api.Dtos;
using WebAPICore.Api.Models.Entities;

namespace WebAPICore.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class ProductsController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public ProductsController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// 取得商品清單（支援分頁、關鍵字搜尋與分類篩選）
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ProductDetailResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ProductDetailResponse>>> GetProducts(
        [FromQuery] string? search,
        [FromQuery] string? category,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 10;

        var query = _dbContext.Products
            .AsNoTracking()
            .Include(p => p.Stock)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(p => p.Sku.Contains(s) || p.Name.Contains(s));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(p => p.Category == category.Trim());
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new ProductDetailResponse(
                p.Id,
                p.Sku,
                p.Name,
                p.Category,
                p.UnitPrice,
                p.SafetyStock,
                p.Stock != null ? p.Stock.CurrentQty : 0,
                p.Stock != null ? p.Stock.OnOrderQty : 0,
                p.Stock != null && p.Stock.CurrentQty <= p.SafetyStock,
                p.CreatedAt
            ))
            .ToListAsync(cancellationToken);

        return Ok(new PagedResult<ProductDetailResponse>(items, totalCount, pageNumber, pageSize));
    }

    /// <summary>
    /// 根據商品 ID 取得單一商品明細與即時庫存
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ProductDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDetailResponse>> GetProductById(Guid id, CancellationToken cancellationToken)
    {
        var product = await _dbContext.Products
            .AsNoTracking()
            .Include(p => p.Stock)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (product == null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "找不到商品",
                Detail = $"找不到 ID 為 {id} 的商品",
                Status = StatusCodes.Status404NotFound
            });
        }

        var response = new ProductDetailResponse(
            product.Id,
            product.Sku,
            product.Name,
            product.Category,
            product.UnitPrice,
            product.SafetyStock,
            product.Stock != null ? product.Stock.CurrentQty : 0,
            product.Stock != null ? product.Stock.OnOrderQty : 0,
            product.Stock != null && product.Stock.CurrentQty <= product.SafetyStock,
            product.CreatedAt
        );

        return Ok(response);
    }

    /// <summary>
    /// 建立新商品（自動初始化庫存記錄）
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ProductDetailResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductDetailResponse>> CreateProduct(
        [FromBody] CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        // 檢查 SKU 是否重複
        var exists = await _dbContext.Products
            .AnyAsync(p => p.Sku == request.Sku, cancellationToken);

        if (exists)
        {
            return Conflict(new ProblemDetails
            {
                Title = "SKU 重複",
                Detail = $"商品貨號 '{request.Sku}' 已經存在，請更換其他 SKU",
                Status = StatusCodes.Status409Conflict
            });
        }

        var product = new Product
        {
            Id = Guid.NewGuid(),
            Sku = request.Sku.Trim(),
            Name = request.Name.Trim(),
            Category = request.Category.Trim(),
            UnitPrice = request.UnitPrice,
            SafetyStock = request.SafetyStock,
            CreatedAt = DateTime.UtcNow
        };

        // 自動初始化庫存記錄
        var stock = new ProductStock
        {
            ProductId = product.Id,
            CurrentQty = request.InitialStock,
            UpdatedAt = DateTime.UtcNow
        };

        product.Stock = stock;

        _dbContext.Products.Add(product);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var response = new ProductDetailResponse(
            product.Id,
            product.Sku,
            product.Name,
            product.Category,
            product.UnitPrice,
            product.SafetyStock,
            stock.CurrentQty,
            stock.OnOrderQty,
            stock.CurrentQty <= product.SafetyStock,
            product.CreatedAt
        );

        return CreatedAtAction(nameof(GetProductById), new { id = product.Id }, response);
    }
}