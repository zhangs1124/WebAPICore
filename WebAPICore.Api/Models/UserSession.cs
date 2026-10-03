namespace WebAPICore.Api.Models;

/// <summary>
/// 登入使用者 Session 模型 (依據 @mvc-session-auth 規範)
/// </summary>
public class UserSession
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = "Warehouse"; // Admin, Manager, Purchaser, Warehouse
    public DateTime LoginTime { get; set; } = DateTime.UtcNow;
}
