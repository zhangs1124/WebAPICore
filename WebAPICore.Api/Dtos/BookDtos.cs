using System.ComponentModel.DataAnnotations;

namespace WebAPICore.Api.Dtos;

// 新增 / 整筆取代：欄位全必填。注意沒有 Id，Id 由伺服器決定。
public class BookRequest
{
    [Required, StringLength(200, MinimumLength = 1)]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 1)]
    public string Author { get; set; } = string.Empty;

    [Range(1450, 2100)]
    public int Year { get; set; }
}

// 部分更新：欄位皆可省略，有傳的才驗證
public class BookPatchRequest
{
    [StringLength(200, MinimumLength = 1)]
    public string? Title { get; set; }

    [StringLength(100, MinimumLength = 1)]
    public string? Author { get; set; }

    [Range(1450, 2100)]
    public int? Year { get; set; }
}

// 回傳給 client 的形狀，與內部 Entity 解耦
public record BookResponse(int Id, string Title, string Author, int Year);
