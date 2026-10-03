using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WebAPICore.Api.Dtos;
using WebAPICore.Api.Models;
using WebAPICore.Api.Services;

namespace WebAPICore.Api.Controllers;

/// <summary>
/// 系統帳號登入與工作階段管理 (遵循 @mvc-session-auth 規範)
/// </summary>
public class AccountController : Controller
{
    private readonly IAuthService _authService;
    private readonly ILogger<AccountController> _logger;

    public AccountController(IAuthService authService, ILogger<AccountController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    /// <summary>
    /// 登入畫面 (獨立頁面 Layout = null)
    /// </summary>
    [HttpGet]
    public IActionResult Login()
    {
        // 若已經登入，直接導向首頁
        var sessionJson = HttpContext.Session.GetString("UserSession");
        if (!string.IsNullOrEmpty(sessionJson))
        {
            return RedirectToAction("Index", "Home");
        }

        return View();
    }

    /// <summary>
    /// 處理登入請求 (AJAX 提交，嚴禁全頁重新整理)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var loginResult = await _authService.LoginAsync(request, cancellationToken);

            var userSession = new UserSession
            {
                UserId = loginResult.UserId,
                Username = loginResult.Username,
                DisplayName = loginResult.DisplayName,
                Role = loginResult.Role,
                Department = loginResult.Department,
                LoginTime = DateTime.UtcNow
            };

            // 寫入 Session
            HttpContext.Session.SetString("UserSession", JsonSerializer.Serialize(userSession));

            _logger.LogInformation("使用者 {Username} ({Role} - {Department}) 成功登入系統", userSession.Username, userSession.Role, userSession.Department);

            return Json(new
            {
                success = true,
                message = "登入成功！正在進入系統...",
                displayName = userSession.DisplayName,
                role = userSession.Role,
                department = userSession.Department,
                redirectUrl = Url.Action("Index", "Home")
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Json(new
            {
                success = false,
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "登入處理發生未知錯誤");
            return Json(new
            {
                success = false,
                message = "登入失敗，請稍後重試。"
            });
        }
    }

    /// <summary>
    /// 登出作業
    /// </summary>
    [HttpGet]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction("Login");
    }

    /// <summary>
    /// 取得目前 Session 登入者身分
    /// </summary>
    [HttpGet]
    public IActionResult GetCurrentSession()
    {
        var sessionJson = HttpContext.Session.GetString("UserSession");
        if (string.IsNullOrEmpty(sessionJson))
        {
            return Json(new { isAuthenticated = false });
        }

        var session = JsonSerializer.Deserialize<UserSession>(sessionJson);
        return Json(new
        {
            isAuthenticated = true,
            userId = session?.UserId,
            username = session?.Username,
            displayName = session?.DisplayName,
            role = session?.Role,
            department = session?.Department
        });
    }
}

