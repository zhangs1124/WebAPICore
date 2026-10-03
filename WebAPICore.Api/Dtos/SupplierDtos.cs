using System.ComponentModel.DataAnnotations;

namespace WebAPICore.Api.Dtos;

/// <summary>
/// 建立供應商請求 DTO
/// </summary>
public record CreateSupplierRequest(
    [Required(ErrorMessage = "供應商代碼不可為空")]
    [MaxLength(50, ErrorMessage = "代碼長度不可超過 50 個字元")]
    string Code,

    [Required(ErrorMessage = "供應商公司名稱不可為空")]
    [MaxLength(100, ErrorMessage = "名稱長度不可超過 100 個字元")]
    string Name,

    [Required(ErrorMessage = "主要聯絡人不可為空")]
    [MaxLength(50, ErrorMessage = "聯絡人長度不可超過 50 個字元")]
    string ContactPerson,

    [Required(ErrorMessage = "聯絡電話不可為空")]
    [MaxLength(50, ErrorMessage = "電話長度不可超過 50 個字元")]
    string Phone,

    [EmailAddress(ErrorMessage = "電子郵件格式不正確")]
    string? Email,

    [MaxLength(255, ErrorMessage = "地址長度不可超過 255 個字元")]
    string? Address
);

/// <summary>
/// 供應商資料回應 DTO
/// </summary>
public record SupplierResponse(
    Guid Id,
    string Code,
    string Name,
    string ContactPerson,
    string Phone,
    string? Email,
    string? Address,
    bool IsActive,
    DateTime CreatedAt
);

