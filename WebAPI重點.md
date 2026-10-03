# 面試說的「熟悉 Web API」要懂哪些東西

面試官問這個，通常不只是問你會不會寫 Controller，而是看你對整條請求流程的理解。以下由基礎到進階分層整理，並補上 ASP.NET Core 的對應項目。

## 1. HTTP 基礎（必考）
- **HTTP 方法**：GET、POST、PUT、PATCH、DELETE 的語意。
  - **冪等性（Idempotent）**：PUT 和 DELETE 重複呼叫結果相同，POST 則不是。這題很常考。
  - **安全性（Safe）**：GET 不應該改變資料。
- **狀態碼**：
  - 2xx：200、201（Created）、204（No Content）。
  - 4xx：400、401、403、404、409、422。
  - 5xx：500、502、503。
  - 401 和 403 的差別，是最常被問的一題。
- **Header**：Content-Type、Accept、Authorization、Cache-Control、ETag。
- **Request/Response 結構**：URL、Query String、Route、Body。
- HTTP 與 HTTPS、TLS 的基本概念。
- HTTP/1.1、HTTP/2、HTTP/3 的差異（知道概念即可）。

## 2. RESTful 設計
- REST 的六個約束：無狀態（Stateless）、Client-Server、可快取等。
- **資源導向的 URL 設計**：用 `/users/1/orders`，不用 `/getUserOrders`。
- 複數名詞、巢狀資源、過濾 / 排序 / 分頁（pagination）怎麼設計。
- **版本管理**：URL 版本（`/v1/`）、Header 版本、Query 版本。
- HATEOAS（知道概念即可，實務很少用）。
- 常被拿來比較：REST 與 GraphQL、gRPC、SOAP 的差異和適用場景。

## 3. 資料格式與傳輸
- **JSON** 序列化 / 反序列化，包含日期格式、camelCase 和 PascalCase、null 處理。
- 檔案上傳與下載（multipart/form-data）。
- DTO 與 Entity 為什麼要分開，以及 Model Binding、Model Validation。

## 4. 驗證與授權（Auth）：面試重點
- **Authentication 與 Authorization** 的差別。
- **JWT**：結構（Header、Payload、Signature）、Access Token 與 Refresh Token、過期處理、存放位置。
- **OAuth 2.0 / OpenID Connect**：流程的基本概念。
- API Key、Basic Auth、Cookie 與 Token 的比較。
- 角色式（Role-based）與政策式（Policy-based）授權。

## 5. 安全性
- **CORS**：為什麼會發生，以及怎麼設定。
- OWASP Top 10：SQL Injection、XSS、CSRF、敏感資料外洩。
- Rate Limiting 與 Throttling。
- 輸入驗證、HTTPS 強制、機敏資訊不放在 URL 或 Log。

## 6. 後端實作能力（以 ASP.NET Core 為例）
- **Middleware pipeline**：請求經過的順序、自訂 Middleware。
- **Dependency Injection**：Singleton、Scoped、Transient 的生命週期差異。
- **Filter**：Authorization、Action、Exception、Result Filter。
- Routing（Attribute Routing）、Model Binding（`[FromBody]`、`[FromQuery]` 等）。
- **全域例外處理**：統一錯誤格式，可參考 ProblemDetails（RFC 7807）。
- Configuration 與 Options pattern、Logging（Serilog 等）。
- **Entity Framework Core / Dapper**：基本 CRUD、N+1 問題、交易（Transaction）、Migration。
- **非同步**：`async/await` 的正確用法、避免 `.Result` 造成死鎖。
- 單元測試與整合測試（xUnit、Moq、`WebApplicationFactory`）。

## 7. 文件與測試工具
- **Swagger / OpenAPI**：自動產生文件、設定 JWT 驗證按鈕。
- **Postman**：寫 Collection、環境變數、簡單的自動化測試。
- 前後端串接時怎麼對規格、怎麼除錯。

## 8. 進階（資深職位才會深問）
- **快取**：In-Memory、Redis、HTTP Cache、ETag。
- **效能**：分頁、索引、壓縮、非同步、連線池。
- **併發控制**：樂觀鎖（RowVersion）和悲觀鎖。
- **冪等設計**：付款類 API 怎麼避免重複扣款（Idempotency Key）。
- 微服務、API Gateway、服務間通訊、Message Queue。
- 部署與維運：Docker、CI/CD、Health Check、監控與 Log 追蹤。

---

## 面試準備建議

**常見的實際問題**：
1. PUT 和 PATCH 有什麼差別？
2. 401 和 403 的差別？
3. JWT 的運作流程？Token 過期了怎麼辦？
4. 什麼是 CORS？前端為什麼會被擋？
5. DI 的三種生命週期差在哪？
6. 怎麼設計一個分頁 API？
7. 怎麼做全域錯誤處理？
8. API 版本怎麼管理？

**優先順序**：
- **初階 / 轉職**：先把第 1、2、3、4、6 項（HTTP、REST、JSON、Auth、基本實作）講清楚就夠。
- **中階**：加上安全性、EF Core 效能、測試、快取。
- **資深**：加上架構設計、併發、冪等、微服務、維運。

**最有效的準備方式**：自己做一個小專案，例如有登入（JWT）、CRUD、分頁、全域錯誤處理、Swagger 文件、單元測試的 API。面試時能講「我做過什麼、遇到什麼問題、怎麼解決」，比背定義更有說服力。

---

## 練習路線（對應本專案 WebAPICore）

| 階段 | 內容 | 狀態 |
|------|------|------|
| 1 | HTTP + REST 基本 CRUD（Books，In-Memory） | 骨架已建立 |
| 2 | DTO、驗證、全域錯誤處理（ProblemDetails） | 待做 |
| 3 | DI + EF Core（SQLite）、分頁排序過濾 | 待做 |
| 4 | JWT 驗證與授權（Role / Policy） | 待做 |
| 5 | CORS、Rate Limiting、API 版本、Swagger、Serilog | 待做 |
| 6 | 單元測試與整合測試 | 待做 |
| 7 | 快取、樂觀鎖、Idempotency Key、Docker、CI（選做） | 待做 |

啟動 API：`dotnet run --project WebAPICore.Api`
執行測試：`dotnet test`
-------------------


接下來的順序
階段	做什麼	學到什麼	對應面試題
3	用 EF Core + SQLite 取代靜態 List，拆出 Service 層，加分頁、排序、過濾	DI 與三種生命週期、Migration、async/await、N+1 與 AsNoTracking、Entity 與 DTO 分離的實際好處	DI 生命週期？怎麼設計分頁 API？什麼是 N+1？
4	註冊 / 登入、JWT（Access + Refresh Token）、[Authorize]、角色與政策授權	認證與授權的差別、JWT 結構、密碼雜湊	401 vs 403？JWT 怎麼運作？Token 過期怎麼辦？
5	CORS、Rate Limiting、API 版本管理、Swagger（含 JWT 按鈕）、Serilog 日誌	跨域原理、防濫用、版本策略、可觀測性	什麼是 CORS？API 版本怎麼管？
6	單元測試（xUnit + Moq）加強整合測試	Service 層單元測試、Mock、測試資料隔離	怎麼測試你的 API？Mock 是什麼？
7（選做）	快取、樂觀鎖、Idempotency Key、Docker、CI	併發、效能、部署	付款 API 怎麼避免重複扣款？
我的建議
---

## 9. 附錄：資料存取層技術選型與 EF Core 近代效能革新

### Q1：EF Core 與 Dapper 該如何選擇？（面試常考）
- **EF Core (全功能 ORM)**：
  - **優勢**：開發速度快、LINQ 型別安全、自動遷移（Migrations）、Change Tracker 自動追蹤變更、內建 Unit of Work 與交易控制。
  - **劣勢**：複雜查詢可能產生低效 SQL、大量寫入時 Change Tracker 有額外記憶體與計算開銷。
- **Dapper (微型 ORM)**：
  - **優勢**：效能逼近原生 ADO.NET、記憶體開銷低、直接掌控原生 SQL、適合報表與 Stored Procedure。
  - **劣勢**：需自行維護 SQL 字串、無自動 Migration、重構欄位時缺少編譯時期型別檢查。
- **架構建議（加分回答）**：
  建議採用 **CQS（命令查詢職責分離）** 混合模式：
  1. **寫入（Command）**：使用 **EF Core**，確保業務邏輯、實體約束與資料庫交易一致性。
  2. **讀取（Query）**：一般查詢使用 **EF Core + AsNoTracking()**；針對百萬級高併發或複雜報表查詢，採用 **Dapper** 直接下最佳化 SQL。

---

### Q2：EF Core 的 Change Tracker 近代有哪些效能重大改進？（高階面試亮點）
1. **直譯式批次更新與刪除（ExecuteUpdate / ExecuteDelete）**：
   - **過去痛點**：以往更新資料必須先將實體 SELECT 到記憶體中，由 Change Tracker 建立快照監控，最後 SaveChanges() 逐筆發送 UPDATE，耗費記憶體與網路 Round-trip。
   - **現代改進**：可以直接生成單一條 UPDATE/DELETE SQL，完全不載入實體至記憶體、完全不經過 Change Tracker：
     `csharp
     await context.Books
         .Where(b => b.PublishYear < 2000)
         .ExecuteUpdateAsync(s => s.SetProperty(b => b.Price, b => b.Price * 0.9m));
     `
2. **大量寫入時關閉自動變更偵測（AutoDetectChangesEnabled）**：
   - 批次匯入大量資料時，預設每新增一筆就會遍歷所有實體進行快照比對（複雜度達 (N^2)$）。
   - 改善作法：
     `csharp
     context.ChangeTracker.AutoDetectChangesEnabled = false;
     // 大量加入實體 context.Books.AddRange(books);
     context.ChangeTracker.DetectChanges(); // 最後手動觸發一次比對
     await context.SaveChangesAsync();
     `
3. **唯讀查詢最佳化（AsNoTracking 與 AsNoTrackingWithIdentityResolution）**：
   - 不需要修改的查詢一律加上 .AsNoTracking()，不建立快照物件，直接釋放 CPU 與 GC 壓力。
   - 若查詢關聯中有多個相同實體，可使用 .AsNoTrackingWithIdentityResolution() 兼顧效能與物件圖譜去重。
4. **拆分查詢（AsSplitQuery）解決笛卡兒積爆炸**：
   - 多個 Include() 關聯集合時，單一 SQL JOIN 會產生大量重複列（Cartesian Explosion）。
   - 使用 .AsSplitQuery() 拆成多個獨立查詢，降低資料庫負擔與記憶體佔用。

---

## 10. 附錄：Swagger / OpenAPI 安全防禦實務與生產環境保護（資安與面試必考）

### Q1：為什麼在正式環境（Production）不能任意開放 `/swagger` 或 API 互動介面？
1. **資訊外洩與攻擊地圖公開（Reconnaissance）**：
   - 任何人均可檢視所有後端內部路由（如 `/api/admin/roles`、`/api/internal/debug`）、參數格式、資料驗證規則。
   - DTO 型別結構直接外洩，暴露資料庫 Schema 設計。
2. **降低攻擊門檻（白箱攻擊與 BOLA / IDOR）**：
   - 攻擊者無需盲測，直接在 UI 上檢視哪些端點缺少授權驗證，透過「Try it out」直接發動越權存取或注入攻擊。
3. **套件維護與 CVE 漏洞隱患**：
   - 舊版 `Swashbuckle.AspNetCore` 因維護停滯，若有解析漏洞易遭利用。

### Q2：實務上如何落實保護？（防禦三大做法）

#### 做法一：環境隔離（預設做法）
限定僅在開發或測試環境註冊與渲染 Swagger / Scalar 路由：
```csharp
if (app.Environment.IsDevelopment())
{
    // 只有本機開發或測試機才看得到
    app.UseSwagger();
    app.UseSwaggerUI();
    // 或使用 .NET 9 原生 OpenAPI + Scalar:
    // app.MapOpenApi();
    // app.MapScalarApiReference();
}
```

#### 做法二：若正式環境一定要看，必須「上鎖」
如果正式機或 Staging 環境需要給外部合作廠商或前端查閱，**絕對不能裸奔**：
1. **限定內網存取**：透過 Nginx / Reverse Proxy 限制只有公司 VPN 或內部 IP 可以訪問 `/swagger` 或 `/scalar`。
2. **加上帳密保護 (Basic Auth)**：替 `/swagger` 路徑加裝 Middleware，必須輸入授權帳密才能解鎖頁面。

#### 做法三：隱藏內部敏感 API
對於一些管理員專用、內部排程或除錯 API，可以在 Controller 或 Action 上加上屬性，避免被收錄進 OpenAPI 文件：
```csharp
[ApiExplorerSettings(IgnoreApi = true)] // 不會在 Swagger / Scalar 上顯示
[HttpDelete("internal/nuke-all-cache")]
public IActionResult NukeCache()
{
    // 內部作業邏輯...
    return NoContent();
}
```