using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using WebAPICore.Api.Data;
using WebAPICore.Api.Middleware;
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

// 初始化 4 大預設角色測試帳號與預設供應商種子資料
using (var scope = app.Services.CreateScope())
{
    var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // 帳號種子資料
    if (!await db.Users.AnyAsync())
    {
        await authService.RegisterAsync(new WebAPICore.Api.Dtos.CreateUserRequest("admin", "Admin888!", "👑 系統管理員", "Admin", "資訊部"));
        await authService.RegisterAsync(new WebAPICore.Api.Dtos.CreateUserRequest("manager", "Manager888!", "👔 倉儲主管", "Manager", "營運處"));
        await authService.RegisterAsync(new WebAPICore.Api.Dtos.CreateUserRequest("purchaser", "Buyer888!", "👤 採購專員", "Purchaser", "總務課"));
        await authService.RegisterAsync(new WebAPICore.Api.Dtos.CreateUserRequest("warehouse", "Worker888!", "👷 現場倉管員", "Warehouse", "總務課"));
    }

    // 供應商種子資料
    if (!await db.Suppliers.AnyAsync())
    {
        var supplierService = scope.ServiceProvider.GetRequiredService<ISupplierService>();
        await supplierService.CreateAsync(new WebAPICore.Api.Dtos.CreateSupplierRequest("SUP-001", "聯強國際股份有限公司", "陳業務", "02-2700-1234", "sales@synnex.example.com", "台北市南港區八德路四段"));
        await supplierService.CreateAsync(new WebAPICore.Api.Dtos.CreateSupplierRequest("SUP-002", "展碁國際股份有限公司", "李經理", "02-2345-6789", "service@weblink.example.com", "新北市中和區中正路"));
        await supplierService.CreateAsync(new WebAPICore.Api.Dtos.CreateSupplierRequest("SUP-003", "精技電腦股份有限公司", "張小姐", "02-8798-8888", "order@unitech.example.com", "台北市內湖區新湖一路"));
    }
}

app.Run();

// 讓測試專案的 WebApplicationFactory<Program> 能存取
public partial class Program { }
public partial class Program { }