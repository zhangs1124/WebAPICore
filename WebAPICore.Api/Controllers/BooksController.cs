using Microsoft.AspNetCore.Mvc;
using WebAPICore.Api.Dtos;
using WebAPICore.Api.Models;

namespace WebAPICore.Api.Controllers;

// [ApiController] 會在 Model 驗證失敗時自動回 400 + ValidationProblemDetails，
// 所以 action 內不需要自己檢查 ModelState.IsValid。
[ApiController]
[Route("api/[controller]")]
public class BooksController : ControllerBase
{
    // 階段 1：先用靜態 List 模擬資料庫（階段 3 會換成 EF Core）
    private static readonly List<Book> _books =
    [
        new() { Id = 1, Title = "Clean Code", Author = "Robert C. Martin", Year = 2008 },
        new() { Id = 2, Title = "The Pragmatic Programmer", Author = "Andrew Hunt", Year = 1999 },
    ];
    private static int _nextId = 3;

    private static BookResponse ToResponse(Book b) => new(b.Id, b.Title, b.Author, b.Year);

    // GET api/books  -> 200
    [HttpGet]
    public ActionResult<IEnumerable<BookResponse>> GetAll() =>
        Ok(_books.Select(ToResponse));

    // GET api/books/1  -> 200 / 404
    [HttpGet("{id:int}")]
    public ActionResult<BookResponse> GetById(int id)
    {
        var book = _books.FirstOrDefault(b => b.Id == id);
        return book is null ? NotFound() : Ok(ToResponse(book));
    }

    // POST api/books  -> 201 + Location header / 400
    [HttpPost]
    public ActionResult<BookResponse> Create(BookRequest request)
    {
        var book = new Book
        {
            Id = _nextId++,
            Title = request.Title,
            Author = request.Author,
            Year = request.Year,
        };
        _books.Add(book);
        return CreatedAtAction(nameof(GetById), new { id = book.Id }, ToResponse(book));
    }

    // PUT api/books/1  -> 204 / 400 / 404（整筆取代，冪等）
    [HttpPut("{id:int}")]
    public IActionResult Replace(int id, BookRequest request)
    {
        var book = _books.FirstOrDefault(b => b.Id == id);
        if (book is null) return NotFound();

        book.Title = request.Title;
        book.Author = request.Author;
        book.Year = request.Year;
        return NoContent();
    }

    // PATCH api/books/1  -> 200 / 400 / 404（只更新有傳的欄位）
    [HttpPatch("{id:int}")]
    public ActionResult<BookResponse> Update(int id, BookPatchRequest patch)
    {
        var book = _books.FirstOrDefault(b => b.Id == id);
        if (book is null) return NotFound();

        if (patch.Title is not null) book.Title = patch.Title;
        if (patch.Author is not null) book.Author = patch.Author;
        if (patch.Year is not null) book.Year = patch.Year.Value;
        return Ok(ToResponse(book));
    }

    // DELETE api/books/1  -> 204 / 404
    [HttpDelete("{id:int}")]
    public IActionResult Delete(int id)
    {
        var book = _books.FirstOrDefault(b => b.Id == id);
        if (book is null) return NotFound();

        _books.Remove(book);
        return NoContent();
    }
}
