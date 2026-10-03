using System.ComponentModel.DataAnnotations;

namespace WebAPICore.Api.Dtos;

/// <summary>
/// 使用者登入請求 DTO
/// </summary>
public record LoginRequest(
    [Required(ErrorMessage = "帳號不可為空")]
    string Username,

    [Required(ErrorMessage = "密碼不可為空")]
    string Password
);

/// <summary>
/// 使用者登入成功回應 DTO
/// </summary>
public record LoginResponse(
    Guid UserId,
    string Username,
    string DisplayName,
    string Role,
    string Department,
    string Token
);

/// <summary>
/// 建立/註冊帳號請求 DTO
/// </summary>
public record CreateUserRequest(
    [Required(ErrorMessage = "帳號不可為空")]
    [MaxLength(50, ErrorMessage = "帳號不可超過 50 個字元")]
    string Username,

    [Required(ErrorMessage = "密碼不可為空")]
    [MinLength(6, ErrorMessage = "密碼長度至少需 6 碼")]
    string Password,

    [Required(ErrorMessage = "顯示姓名不可為空")]
    [MaxLength(100, ErrorMessage = "顯示姓名不可超過 100 個字元")]
    string DisplayName,

    [Required(ErrorMessage = "角色不可為空")]
    string Role,

    string? Department = "總務課"
);

