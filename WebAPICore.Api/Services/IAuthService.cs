using WebAPICore.Api.Dtos;
using WebAPICore.Api.Models.Entities;

namespace WebAPICore.Api.Services;

/// <summary>
/// 系統權限與使用者認證服務介面
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// 建立/註冊使用者帳號 (指定角色)
    /// </summary>
    Task<User> RegisterAsync(CreateUserRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 登入驗證並產生登入資訊
    /// </summary>
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 依帳號名稱查詢使用者
    /// </summary>
    Task<User?> GetUserByUsernameAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查詢所有使用者清單
    /// </summary>
    Task<IReadOnlyList<User>> GetUsersAsync(CancellationToken cancellationToken = default);
}
