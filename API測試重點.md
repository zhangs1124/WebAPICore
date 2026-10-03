# API 測試重點

## 測試分類
- **CRUD 行為**
- **驗證 (Validation)**
- **協定層錯誤 (Protocol‑level Errors)**
- **額外邊界與安全測試**

## 現有測試概覽 (BooksApiTests.cs 已覆蓋)
- `GetAll_Returns200_WithBooks`
- `GetById_Unknown_Returns404`
- `Create_Returns201_AndLocationPointsToTheNewBook`
- `Create_IgnoresClientSuppliedId`
- `Put_IsIdempotent_AndReplacesAllFields`
- `Put_Unknown_Returns404`
- `Patch_ChangesOnlyTheGivenFields`
- `Patch_Unknown_Returns404`
- `Delete_Returns204_ThenBookIsGone_AndSecondDeleteIs404`
- `Create_InvalidInput_Returns400_WithFieldError` (title/author/year 必填、長度、範圍)
- `Create_YearBoundary`、`Create_TitleLengthBoundary`、`Create_AuthorLengthBoundary`
- `Create_WrongTypeForYear_Returns400`
- `Put_MissingAuthor_Returns400`
- `Put_ValidationRunsBeforeLookup_UnknownIdWithBadBodyIs400Not404`
- `Patch_EmptyTitle_Returns400`
- `Create_MalformedJson_Returns400`
- `Create_EmptyBody_Returns400`
- `Create_UnsupportedContentType_Returns415`
- `GetById_NonIntegerId_Returns404_BecauseOfRouteConstraint`
- `Put_WithoutId_Returns405`
- `UnhandledException_Returns500ProblemDetails_WithoutLeakingMessage`

## 缺口測試（2026-10 實測後的狀態）
| 測試項目 | 實測結果 | 狀態 |
|----------|----------|------|
| `PATCH` 空白 Body | 400 | 已補：`Patch_EmptyBody_Returns400` |
| `PATCH` 傳 `{}` | 200，資料不變 | 已補：`Patch_EmptyObject_Returns200_AndChangesNothing`（設計待決定，見下） |
| `PUT` / `PATCH` 超長 `title` (>200) | 400 | 已補：`Put_TitleTooLong_Returns400`、`Patch_TitleTooLong_Returns400` |
| `GET` / `DELETE` / `PUT` / `PATCH` 的 Id 為 `0`、`-1` | 404 | 已補：`NonPositiveId_Returns404`（8 組） |
| `DELETE` / `PUT` / `PATCH` 不存在 Id | 404 | 原本已涵蓋，不另補 |
| `OPTIONS` 預檢請求 (CORS) | 目前 405（尚未設定 CORS） | 等階段 5 做完 CORS 再補，預期才會是 200 加 `Access-Control-Allow-*` |
| `HEAD` 請求 | 目前 405（`[HttpGet]` 不會自動處理 HEAD） | 待決定要不要支援；要支援得加 `[HttpHead]` |
| `GET /api/books?page=2&size=1` | 200，參數被忽略，回全部資料 | 等階段 3 實作分頁後再補 |

### 修正先前的說明
- Id 為 0、負數回 404 的原因：`{id:int}` 限制**接受** 0 和負數，請求會進到 Controller，是查不到資料才回 404，並不是被路由擋掉。若想讓這類 Id 回 400，要另外加驗證。

### 待決定的設計
- `HEAD` 要不要支援？
- `PATCH` 傳 `{}` 要維持 200，還是改成要求至少一個欄位（400）？改的話要同步修改 `Patch_EmptyObject...` 這個測試。

## 測試命名與實作建議
- **命名規則**：`Method_Condition_ExpectedResult`（如 `Patch_EmptyBody_Returns400`）
- **使用 Theory + InlineData / MemberData** 來集中管理多組邊界值
- 抽出共用方法以減少重複：
  ```csharp
  private static StringContent CreateJsonContent(string json) =>
      new StringContent(json, Encoding.UTF8, "application/json");

  private static async Task<JsonElement> ReadProblemAsync(HttpResponseMessage resp) =>
      await resp.Content.ReadFromJsonAsync<JsonElement>();
  ```

## 測試範例 (示範程式碼)
```csharp
[Fact]
public async Task Patch_EmptyBody_Returns400()
{
    var book = await CreateBookAsync();
    var content = new StringContent(string.Empty, Encoding.UTF8, "application/json");
    var response = await _client.PatchAsync($"/api/books/{book.Id}", content);
    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
}

[Theory]
[InlineData(0)]
[InlineData(-1)]
public async Task Delete_InvalidId_Returns404(int id)
{
    var response = await _client.DeleteAsync($"/api/books/{id}");
    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
}
```

## 使用建議
- 在建立新 API 時，先檢視此文件確認必寫的測試項目。
- 測試完成後，將新測試加入 `BooksApiTests.cs`，保持與此清單同步。
- 定期檢視此文件是否有遺漏的新需求或邊界條件。
