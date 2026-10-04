using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using WebAPICore.Api.Data;
using WebAPICore.Api.Middleware;
using WebAPICore.Api.Models.Entities;
using WebAPICore.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container (支援 MVC Views 與 API Controllers).
builder.Services.AddControllersWithViews();
builder.Services.AddOpenApi();

// 資料庫配置：Supabase (PostgreSQL)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// 業務邏輯服務註冊 (Scoped)
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IPurchaseOrderService, PurchaseOrderService>();
builder.Services.AddScoped<ISupplierService, SupplierService>();
builder.Services.AddScoped<IAuthService, AuthService>();

// 全域例外處理 + ProblemDetails (RFC 7807)
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// 經典 Session 與 HttpContext 存取器註冊 (依據 @mvc-session-auth 規範)
builder.Services.AddHttpContextAccessor();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = "WebAPICore.Session";
});

var app = builder.Build();

// Middleware 順序：例外處理放最前面，確保攔截後續所有管線中的未捕捉例外
app.UseExceptionHandler();
app.UseStatusCodePages();

// 啟用 OpenAPI 與現代化 Scalar 互動文件（線上展示支援，訪問 /scalar/v1）
app.MapOpenApi();
app.MapScalarApiReference();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    // 練習用：故意丟例外，觀察全域錯誤處理的輸出（僅 Development）
    app.MapGet("/debug/throw", () => { throw new InvalidOperationException("boom"); });
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseSession();
app.UseAuthorization();

// 預設 MVC 路由 (首頁導向 HomeController.Index)
app.MapDefaultControllerRoute();

// API 路由對應
app.MapControllers();

// 初始化 4 大預設角色測試帳號與預設供應商種子資料 (防禦性連線保護)
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // 自動檢查並為 products 資料表補上 SupplierId 關聯外鍵欄位 (若尚未存在)
        try
        {
            await db.Database.ExecuteSqlRawAsync(@"
                ALTER TABLE products ADD COLUMN IF NOT EXISTS ""SupplierId"" uuid NULL;
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_constraint WHERE conname = 'FK_products_suppliers_SupplierId'
                    ) THEN
                        ALTER TABLE products 
                        ADD CONSTRAINT ""FK_products_suppliers_SupplierId"" 
                        FOREIGN KEY (""SupplierId"") REFERENCES suppliers (""Id"") ON DELETE SET NULL;
                    END IF;
                END $$;
            ");
            logger.LogInformation("資料庫結構同步檢查完成 (products.SupplierId)。");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "執行 products.SupplierId 結構同步時發生例外，忽略並繼續。");
        }

        // 帳號種子資料
        if (!await db.Users.AnyAsync())
        {
            await authService.RegisterAsync(new WebAPICore.Api.Dtos.CreateUserRequest("admin", "Admin888!", "👑 系統管理員", "Admin", "資訊部"));
            await authService.RegisterAsync(new WebAPICore.Api.Dtos.CreateUserRequest("manager", "Manager888!", "👔 倉儲主管", "Manager", "營運處"));
            await authService.RegisterAsync(new WebAPICore.Api.Dtos.CreateUserRequest("purchaser", "Buyer888!", "👤 採購專員", "Purchaser", "總務課"));
            await authService.RegisterAsync(new WebAPICore.Api.Dtos.CreateUserRequest("warehouse", "Worker888!", "👷 現場倉管員", "Warehouse", "總務課"));
            logger.LogInformation("種子帳號資料初始化完成。");
        }

        // 供應商種子資料
        if (!await db.Suppliers.AnyAsync())
        {
            var supplierService = scope.ServiceProvider.GetRequiredService<ISupplierService>();
            await supplierService.CreateAsync(new WebAPICore.Api.Dtos.CreateSupplierRequest("SUP-001", "聯強國際股份有限公司", "陳業務", "02-2700-1234", "sales@synnex.example.com", "台北市南港區八德路四段"));
            await supplierService.CreateAsync(new WebAPICore.Api.Dtos.CreateSupplierRequest("SUP-002", "展碁國際股份有限公司", "李經理", "02-2345-6789", "service@weblink.example.com", "新北市中和區中正路"));
            await supplierService.CreateAsync(new WebAPICore.Api.Dtos.CreateSupplierRequest("SUP-003", "精技電腦股份有限公司", "張小姐", "02-8798-8888", "order@unitech.example.com", "台北市內湖區新湖一路"));
            logger.LogInformation("種子供應商資料初始化完成。");
        }

        // 為各家供應商自動建立商品品項 (直接由資料庫段初始化)
        var allSuppliers = await db.Suppliers.AsNoTracking().ToListAsync();
        var supMap = allSuppliers.ToDictionary(s => s.Code, s => s.Id);

        var seedProducts = new List<(string Sku, string Name, string Category, decimal Price, int SafetyStock, int InitialStock, string? SupCode)>
        {
            // SUP-001 聯強國際
            ("SYN-SSD-1TB", "Kingston NV3 1TB PCIe 4.0 M.2 SSD", "電腦周邊", 2150m, 20, 60, "SUP-001"),
            ("SYN-RAM-32G", "美光 Crucial DDR5 5600 32GB 記憶體", "電腦周邊", 3200m, 15, 45, "SUP-001"),
            ("SYN-WIFI-AX", "TP-Link Archer AX72 Pro 雙頻無線路由器", "辦公設備", 3890m, 10, 30, "SUP-001"),

            // SUP-002 展碁國際
            ("WEB-LOGI-MX", "羅技 MX Master 3S 無線智能滑鼠", "電腦周邊", 3690m, 12, 50, "SUP-002"),
            ("WEB-HEAD-RGB", "微星 Immerse GH50 電競耳罩耳機", "電腦周邊", 2490m, 8, 25, "SUP-002"),
            ("WEB-DOC-HUB", "Anker 8合1 USB-C 多功能集線器", "耗材配件", 1890m, 15, 40, "SUP-002"),

            // SUP-003 精技電腦
            ("UNI-MON-27", "ViewSonic 27吋 2K 專業護眼螢幕", "辦公設備", 5990m, 10, 20, "SUP-003"),
            ("UNI-PRN-L3", "Epson L3210 連續供墨多功能印表機", "辦公設備", 4490m, 6, 18, "SUP-003"),
            ("UNI-UPS-1K", "APC 1000VA 在線互動式不斷電系統", "辦公設備", 4800m, 5, 15, "SUP-003"),

            // SUP-004 宏碁資訊
            ("ACR-MON-24", "Acer Nitro 24吋 180Hz 電競螢幕", "電腦周邊", 3490m, 12, 35, "SUP-004"),
            ("ACR-PROJ-X1", "Acer X1228H 4500流明高亮度商用投影機", "辦公設備", 13900m, 4, 10, "SUP-004"),
            ("ACR-MNT-ARM", "Acer 人體工學雙螢幕氣壓支架", "耗材配件", 1590m, 10, 30, "SUP-004"),

            // SUP-005 華碩電腦
            ("ASU-ROGK-01", "ASUS ROG Strix Scope II 機械鍵盤", "電腦周邊", 3990m, 10, 28, "SUP-005"),
            ("ASU-TUF-750", "ASUS TUF Gaming 750W 金牌電源供應器", "電腦周邊", 3290m, 10, 32, "SUP-005"),
            ("ASU-MB-B760", "ASUS TUF GAMING B760-PLUS WIFI 主機板", "電腦周邊", 5490m, 8, 22, "SUP-005")
        };

        foreach (var item in seedProducts)
        {
            var exists = await db.Products.AnyAsync(p => p.Sku == item.Sku);
            if (!exists)
            {
                var prodId = Guid.NewGuid();
                Guid? targetSupId = (item.SupCode != null && supMap.TryGetValue(item.SupCode, out var sid)) ? sid : null;

                var newProd = new Product
                {
                    Id = prodId,
                    Sku = item.Sku,
                    Name = item.Name,
                    Category = item.Category,
                    UnitPrice = item.Price,
                    SafetyStock = item.SafetyStock,
                    SupplierId = targetSupId,
                    CreatedAt = DateTime.UtcNow
                };

                var newStock = new ProductStock
                {
                    ProductId = prodId,
                    CurrentQty = item.InitialStock,
                    OnOrderQty = 0,
                    UpdatedAt = DateTime.UtcNow
                };

                newProd.Stock = newStock;
                db.Products.Add(newProd);
            }
        }
        await db.SaveChangesAsync();
        logger.LogInformation("多供應商專屬商品品項種子檢查/建立完成。");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "初始化種子資料時發生異常，允許應用程式繼續啟動。");
    }
}

app.Run();

// 讓測試專案的 WebApplicationFactory<Program> 能存取
public partial class Program { }