using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace WebAPICore.Tests;

// 整合測試：用 WebApplicationFactory 在記憶體內啟動整個 API，不需要手動 dotnet run。
// 資料存在 API 的靜態 List，同一個 class 內的測試是依序執行的；
// 每個測試都自己建立要用的資料，不依賴別的測試留下的狀態。
//
// 每個測試都照 Arrange（準備）→ Act（執行）→ Assert（驗證）的順序寫：
//   1. 準備資料（有需要才準備）
//   2. 對 API 發出請求
//   3. 檢查狀態碼與回應內容
//
// 命名規則：方法_條件_預期結果
//
// 【建構式說明】primary constructor 接收 factory；IClassFixture 讓同一個 class 內的所有測試
// 共用「同一個」factory，也就是共用同一個記憶體內的 API 實例（所以靜態 List 的資料會累積）。
public class BooksApiTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    // 打向記憶體內 API 的 HttpClient，不經過真實網路
    private readonly HttpClient _client = factory.CreateClient();

    // 測試端自己定義的回應形狀，用來反序列化 API 回傳的 JSON。
    // 刻意不直接引用 API 專案的 BookResponse，這樣才是從「client 的角度」驗證對外格式。
    // record 有值相等比較，所以可以直接用 Assert.Equal 比整個物件。
    private record BookDto(int Id, string Title, string Author, int Year);

    // 一筆合法的請求內容；匿名物件序列化後欄位是 camelCase（title / author / year）
    private static object ValidBook(string title = "Test Book") =>
        new { title, author = "Tester", year = 2000 };

    // 建立一本書並回傳結果。測試需要「一筆已存在的資料」時用它準備，
    // 這樣每個測試都有自己的資料，不依賴其他測試或 API 預設的 2 筆。
    // 這裡順便斷言 201，如果建立本身就壞了，測試會在準備階段就失敗，不會誤導成別的錯誤。
    private async Task<BookDto> CreateBookAsync(string title = "Test Book")
    {
        var response = await _client.PostAsJsonAsync("/api/books", ValidBook(title));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<BookDto>())!;
    }

    // 把回應讀成 JsonElement，方便檢查 ProblemDetails 裡的任意欄位（如 errors、status）
    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>());

    // ---------- 階段 1：CRUD ----------

    // 列表：回 200 且有資料（API 啟動時就有 2 筆預設資料，所以只檢查「不為空」，
    // 不檢查確切數量，避免受其他測試新增的資料影響）
    [Fact]
    public async Task GetAll_Returns200_WithBooks()
    {
        var response = await _client.GetAsync("/api/books");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var books = await response.Content.ReadFromJsonAsync<List<BookDto>>();
        Assert.NotEmpty(books!);
    }

    // 查不存在的 id：回 404（999999 遠大於任何測試會產生的 id）
    [Fact]
    public async Task GetById_Unknown_Returns404()
    {
        var response = await _client.GetAsync("/api/books/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // 新增：回 201，且 Location header 指向的網址真的查得到這本書。
    // 這驗證了 REST 的約定：建立資源後，要告訴 client 新資源在哪裡。
    [Fact]
    public async Task Create_Returns201_AndLocationPointsToTheNewBook()
    {
        var response = await _client.PostAsJsonAsync("/api/books", ValidBook("Located"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var location = response.Headers.Location;
        Assert.NotNull(location);

        var fetched = await _client.GetFromJsonAsync<BookDto>(location);
        Assert.Equal("Located", fetched!.Title);
    }

    // 防 over-posting：client 在 Body 偷塞 id，伺服器必須忽略，由伺服器自己決定 id。
    // 這就是使用 DTO（BookRequest 沒有 Id 欄位）的實際好處之一。
    [Fact]
    public async Task Create_IgnoresClientSuppliedId()
    {
        var response = await _client.PostAsJsonAsync("/api/books",
            new { id = 99999, title = "Over-posting", author = "x", year = 2018 });

        var created = await response.Content.ReadFromJsonAsync<BookDto>();
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotEqual(99999, created!.Id);
    }

    // PUT 的兩個特性：
    //   1. 冪等：同樣的請求送兩次，結果完全相同（都是 204）。
    //   2. 整筆取代：所有欄位都被新值覆蓋。
    [Fact]
    public async Task Put_IsIdempotent_AndReplacesAllFields()
    {
        var book = await CreateBookAsync();
        var body = new { title = "Replaced", author = "New Author", year = 2024 };

        var first = await _client.PutAsJsonAsync($"/api/books/{book.Id}", body);
        var second = await _client.PutAsJsonAsync($"/api/books/{book.Id}", body);

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        // PUT 成功沒有 Body（204），所以要再 GET 一次確認資料真的被改了
        var fetched = await _client.GetFromJsonAsync<BookDto>($"/api/books/{book.Id}");
        Assert.Equal(new BookDto(book.Id, "Replaced", "New Author", 2024), fetched);
    }

    // PUT 不存在的 id：回 404（這裡的 Body 是合法的，所以不會先被驗證擋成 400）
    [Fact]
    public async Task Put_Unknown_Returns404()
    {
        var response = await _client.PutAsJsonAsync("/api/books/999999", ValidBook());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // PATCH 是部分更新：只傳 year，title 與 author 必須維持原值。
    // 與 PUT 的差別就在這裡：PUT 要給完整資料，PATCH 只給想改的欄位。
    [Fact]
    public async Task Patch_ChangesOnlyTheGivenFields()
    {
        var book = await CreateBookAsync("Before");

        var response = await _client.PatchAsJsonAsync($"/api/books/{book.Id}", new { year = 2020 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var patched = await response.Content.ReadFromJsonAsync<BookDto>();
        Assert.Equal(new BookDto(book.Id, "Before", "Tester", 2020), patched);
    }

    // PATCH 不存在的 id：回 404
    [Fact]
    public async Task Patch_Unknown_Returns404()
    {
        var response = await _client.PatchAsJsonAsync("/api/books/999999", new { year = 2020 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // DELETE 的完整生命週期：
    //   第一次刪除 204 → 第二次刪除 404 → 再查詢 404。
    // 兩次刪除的「回應」不同，但伺服器最終狀態相同（書都不在了），所以 DELETE 仍算冪等。
    [Fact]
    public async Task Delete_Returns204_ThenBookIsGone_AndSecondDeleteIs404()
    {
        var book = await CreateBookAsync();

        var first = await _client.DeleteAsync($"/api/books/{book.Id}");
        var second = await _client.DeleteAsync($"/api/books/{book.Id}");
        var get = await _client.GetAsync($"/api/books/{book.Id}");

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, second.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    // ---------- 階段 2：驗證 ----------

    // 不合法的輸入：每一組資料會各跑一次測試。
    // 除了狀態碼是 400，還檢查回應的 errors 裡有出問題的那個欄位，
    // 確認錯誤訊息有指出「哪個欄位錯了」，而不只是籠統的失敗。
    [Theory]
    [InlineData("", "x", 2000, "Title")]
    [InlineData("a", "", 2000, "Author")]
    [InlineData("a", "x", 3000, "Year")]
    [InlineData("a", "x", 1000, "Year")]
    public async Task Create_InvalidInput_Returns400_WithFieldError(
        string title, string author, int year, string expectedField)
    {
        var response = await _client.PostAsJsonAsync("/api/books", new { title, author, year });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await ReadJsonAsync(response);
        Assert.True(problem.GetProperty("errors").TryGetProperty(expectedField, out _));
    }

    // 邊界值：剛好在範圍邊緣的值最容易出錯（例如把含端點寫成不含端點）
    // 年份合法範圍是 1450～2100（含兩端），所以 1449 與 2101 要被擋，1450 與 2100 要通過。
    [Theory]
    [InlineData(1449, HttpStatusCode.BadRequest)]
    [InlineData(1450, HttpStatusCode.Created)]
    [InlineData(2100, HttpStatusCode.Created)]
    [InlineData(2101, HttpStatusCode.BadRequest)]
    public async Task Create_YearBoundary(int year, HttpStatusCode expected)
    {
        var response = await _client.PostAsJsonAsync("/api/books",
            new { title = "Boundary", author = "x", year });

        Assert.Equal(expected, response.StatusCode);
    }

    // 標題長度上限 200：剛好 200 字通過，201 字被擋
    [Theory]
    [InlineData(200, HttpStatusCode.Created)]
    [InlineData(201, HttpStatusCode.BadRequest)]
    public async Task Create_TitleLengthBoundary(int length, HttpStatusCode expected)
    {
        var response = await _client.PostAsJsonAsync("/api/books",
            new { title = new string('a', length), author = "x", year = 2000 });

        Assert.Equal(expected, response.StatusCode);
    }

    // 作者長度上限 100：剛好 100 字通過，101 字被擋
    [Theory]
    [InlineData(100, HttpStatusCode.Created)]
    [InlineData(101, HttpStatusCode.BadRequest)]
    public async Task Create_AuthorLengthBoundary(int length, HttpStatusCode expected)
    {
        var response = await _client.PostAsJsonAsync("/api/books",
            new { title = "Boundary", author = new string('a', length), year = 2000 });

        Assert.Equal(expected, response.StatusCode);
    }

    // 型別錯誤：year 應該是整數卻傳字串，JSON 反序列化就會失敗，回 400
    [Fact]
    public async Task Create_WrongTypeForYear_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/books",
            new { title = "a", author = "x", year = "abc" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // PUT 是整筆取代，所以必填欄位（author）缺少時要回 400
    [Fact]
    public async Task Put_MissingAuthor_Returns400()
    {
        var book = await CreateBookAsync();

        var response = await _client.PutAsJsonAsync($"/api/books/{book.Id}", new { title = "a", year = 2000 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // 執行順序：Model 驗證發生在 action 執行「之前」，所以
    // id 不存在 + Body 不合法時，得到的是 400，不是 404。
    // 這個順序是 [ApiController] 的行為，面試常拿來問「驗證和找資料誰先？」
    [Fact]
    public async Task Put_ValidationRunsBeforeLookup_UnknownIdWithBadBodyIs400Not404()
    {
        var response = await _client.PutAsJsonAsync("/api/books/999999",
            new { title = "", author = "x", year = 2000 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // PATCH 欄位「可以省略」，但「有傳就要合法」：傳空字串的標題要被擋
    [Fact]
    public async Task Patch_EmptyTitle_Returns400()
    {
        var book = await CreateBookAsync();

        var response = await _client.PatchAsJsonAsync($"/api/books/{book.Id}", new { title = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // 長度驗證要在所有寫入方法上一致，不是只有 POST 有擋（PUT 版）
    [Fact]
    public async Task Put_TitleTooLong_Returns400()
    {
        var book = await CreateBookAsync();

        var response = await _client.PutAsJsonAsync($"/api/books/{book.Id}",
            new { title = new string('a', 201), author = "x", year = 2000 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // 同上（PATCH 版）
    [Fact]
    public async Task Patch_TitleTooLong_Returns400()
    {
        var book = await CreateBookAsync();

        var response = await _client.PatchAsJsonAsync($"/api/books/{book.Id}",
            new { title = new string('a', 201) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // PATCH 完全沒有 Body：回 400。
    // 注意這和下一個測試（送 `{}`）不同：這裡連 JSON 都沒有，Model Binding 拿不到物件。
    [Fact]
    public async Task Patch_EmptyBody_Returns400()
    {
        var book = await CreateBookAsync();
        var content = new StringContent("", Encoding.UTF8, "application/json");

        var response = await _client.PatchAsync($"/api/books/{book.Id}", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // 目前的行為：沒有任何要改的欄位也算成功，資料不變。
    // 若之後決定改成要求至少一個欄位（回 400），這個測試要跟著改。
    [Fact]
    public async Task Patch_EmptyObject_Returns200_AndChangesNothing()
    {
        var book = await CreateBookAsync();

        var response = await _client.PatchAsJsonAsync($"/api/books/{book.Id}", new { });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var patched = await response.Content.ReadFromJsonAsync<BookDto>();
        Assert.Equal(book, patched);
    }

    // ---------- 協定層錯誤 ----------
    // 這一區測的是「請求本身格式有問題」，連進到驗證邏輯之前就該被擋下來

    // JSON 語法錯誤（括號沒閉合）：無法解析，回 400
    [Fact]
    public async Task Create_MalformedJson_Returns400()
    {
        var content = new StringContent("{ \"title\": \"a\", ", Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/api/books", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // 空 Body：回 400
    [Fact]
    public async Task Create_EmptyBody_Returns400()
    {
        var content = new StringContent("", Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/api/books", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // Content-Type 不是 JSON：伺服器沒有對應的格式處理器，回 415 Unsupported Media Type
    [Fact]
    public async Task Create_UnsupportedContentType_Returns415()
    {
        var content = new StringContent("hello", Encoding.UTF8, "text/plain");

        var response = await _client.PostAsync("/api/books", content);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    // id 不是整數：路由限制 {id:int} 根本不匹配這個網址，所以是「路由找不到」的 404，
    // 請求不會進到 Controller。
    [Fact]
    public async Task GetById_NonIntegerId_Returns404_BecauseOfRouteConstraint()
    {
        var response = await _client.GetAsync("/api/books/abc");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // {id:int} 限制接受 0 和負數，所以請求會進到 Controller，是「查不到資料」才回 404，
    // 並不是被路由擋掉。（若想讓這類 id 回 400，要另外加驗證。）
    // 這裡用 HttpRequestMessage 自己組請求，才能用同一個測試涵蓋四種 HTTP 方法；
    // PUT 與 PATCH 需要帶合法 Body，否則會先因驗證失敗回 400，測不到我們想測的 404。
    [Theory]
    [InlineData("GET", 0)]
    [InlineData("GET", -1)]
    [InlineData("DELETE", 0)]
    [InlineData("DELETE", -1)]
    [InlineData("PUT", 0)]
    [InlineData("PUT", -1)]
    [InlineData("PATCH", 0)]
    [InlineData("PATCH", -1)]
    public async Task NonPositiveId_Returns404(string method, int id)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), $"/api/books/{id}");
        if (method is "PUT")
            request.Content = JsonContent.Create(ValidBook());
        if (method is "PATCH")
            request.Content = JsonContent.Create(new { year = 2020 });

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // PUT 沒帶 id：網址 /api/books 存在，但不支援 PUT 這個方法，回 405 Method Not Allowed
    // （404 是「網址不存在」，405 是「網址在，但方法不對」）
    [Fact]
    public async Task Put_WithoutId_Returns405()
    {
        var response = await _client.PutAsJsonAsync("/api/books", ValidBook());

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    // ---------- 全域例外處理 ----------

    // 未處理的例外：由 GlobalExceptionHandler 接住，統一回 500 + ProblemDetails。
    // 重點是「不洩漏」：例外訊息是 "boom"，但回應裡不能出現，避免把內部細節暴露給外部。
    // /debug/throw 是只在 Development 環境才有的練習端點（WebApplicationFactory 預設就是 Development）。
    [Fact]
    public async Task UnhandledException_Returns500ProblemDetails_WithoutLeakingMessage()
    {
        var response = await _client.GetAsync("/debug/throw");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("boom", raw);
        var problem = JsonSerializer.Deserialize<JsonElement>(raw);
        Assert.Equal(500, problem.GetProperty("status").GetInt32());
    }
}
