using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace WebAPICore.Api.Middleware;

// 所有未處理的例外都會到這裡，統一轉成 ProblemDetails (RFC 7807)
public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Unhandled exception on {Method} {Path}",
            httpContext.Request.Method, httpContext.Request.Path);

        var (status, title) = exception switch
        {
            KeyNotFoundException => (StatusCodes.Status404NotFound, "Resource not found"),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred"),
        };

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status,
            Title = title,
            Instance = httpContext.Request.Path,
            // 不把 exception.Message 回給 client，避免洩漏內部細節
        }, cancellationToken);

        return true;
    }
}
