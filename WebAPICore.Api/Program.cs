using Microsoft.EntityFrameworkCore;
using WebAPICore.Api.Data;
using WebAPICore.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// 資料庫配置：Supabase (PostgreSQL)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// 全域例外處理 + ProblemDetails (RFC 7807)
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

// Middleware 順序：例外處理放最前面，確保攔截後續所有管線中的未捕捉例外
app.UseExceptionHandler();
app.UseStatusCodePages();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    // 練習用：故意丟例外，觀察全域錯誤處理的輸出（僅 Development）
    app.MapGet("/debug/throw", () => { throw new InvalidOperationException("boom"); });
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

// 讓測試專案的 WebApplicationFactory<Program> 能存取
public partial class Program { }