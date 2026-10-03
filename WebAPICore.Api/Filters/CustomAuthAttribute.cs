using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using WebAPICore.Api.Models;

namespace WebAPICore.Api.Filters;

/// <summary>
/// 經典 Session 驗證與角色權限過濾器 (依據 @mvc-session-auth 規範)
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class CustomAuthAttribute : ActionFilterAttribute
{
    /// <summary>
    /// 允許的角色清單 (以逗號分隔，例如 "Admin,Manager")
    /// </summary>
    public string? Roles { get; set; }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var sessionJson = context.HttpContext.Session.GetString("UserSession");

        // 1. 未登入攔截
        if (string.IsNullOrEmpty(sessionJson))
        {
            HandleUnauthorized(context);
            return;
        }

        var userSession = JsonSerializer.Deserialize<UserSession>(sessionJson);
        if (userSession == null)
        {
            HandleUnauthorized(context);
            return;
        }

        // 2. 角色權限檢查 (RBAC)
        if (!string.IsNullOrWhiteSpace(Roles))
        {
            var allowedRoles = Roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (!allowedRoles.Contains(userSession.Role, StringComparer.OrdinalIgnoreCase))
            {
                HandleForbidden(context, userSession.Role);
                return;
            }
        }

        base.OnActionExecuting(context);
    }

    private static void HandleUnauthorized(ActionExecutingContext context)
    {
        var isApiOrAjax = context.HttpContext.Request.Path.StartsWithSegments("/api")
            || context.HttpContext.Request.Headers["X-Requested-With"] == "XMLHttpRequest";

        if (isApiOrAjax)
        {
            context.Result = new JsonResult(new
            {
                title = "尚未登入",
                detail = "請先登入系統後再進行操作",
                status = StatusCodes.Status401Unauthorized
            })
            {
                StatusCode = StatusCodes.Status401Unauthorized
            };
        }
        else
        {
            context.Result = new RedirectToActionResult("Login", "Account", null);
        }
    }

    private static void HandleForbidden(ActionExecutingContext context, string currentRole)
    {
        var isApiOrAjax = context.HttpContext.Request.Path.StartsWithSegments("/api")
            || context.HttpContext.Request.Headers["X-Requested-With"] == "XMLHttpRequest";

        if (isApiOrAjax)
        {
            context.Result = new JsonResult(new
            {
                title = "權限不足",
                detail = $"當前身分【{currentRole}】無權存取此功能",
                status = StatusCodes.Status403Forbidden
            })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }
        else
        {
            context.Result = new ForbidResult();
        }
    }
}
