namespace WebAPICore.Api.Models.Entities;

/// <summary>
/// 系統使用者帳號與角色實體 (支援 RBAC 權限控管)
/// </summary>
public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// 登入帳號
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// 密碼雜湊
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// 人員顯示姓名 (例如：王大明)
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 系統角色 (Admin, Manager, Purchaser, Warehouse)
    /// </summary>
    public string Role { get; set; } = "Warehouse";

    /// <summary>
    /// 所屬單位/部門代碼 (例如：總務課、製造課、研發課、資材課)
    /// </summary>
    public string Department { get; set; } = "總務課";

    /// <summary>
    /// 帳號是否啟用
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// 建立時間
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
