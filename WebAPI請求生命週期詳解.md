# Web API 面試核心問題：請求完整生命週期剖析（End-to-End）

## 題目描述
> **面試官提問**：  
> 「請你挑一隻你寫過最熟悉的 API，跟我聊聊從前端送出 HTTP Request、經過網路、進到 ASP.NET Core，一直到存取資料庫並回傳 Response，這中間經歷了哪些過程？」

---

## 核心設計情境設定
以一個標準的建立訂單 API 為例：  
`POST https://api.example.com/api/v1/orders`  
* **Request Header**：`Authorization: Bearer <JWT>`, `Content-Type: application/json`
* **Request Body**：`{ "productId": 101, "quantity": 2 }`

---

## 階段一：網路傳輸與伺服器接收層（Client ➜ OS ➜ Kestrel）

1. **DNS 解析與連線建立**：
   * 用戶端（瀏覽器/App）透過 DNS 解析出伺服器 IP。
   * 建立 **TCP 三方交握（Three-way Handshake）**，接著進行 **TLS/SSL 交握** 完成對稱金鑰協商，建立加密通道（HTTPS）。
2. **反向代理（Reverse Proxy，如 Nginx / IIS / YARP）**：
   * 外部請求通常先抵達反向代理伺服器。
   * **職責**：TLS 卸載（SSL Termination）、DDoS 緩衝、靜態檔案快取、將請求轉發到內網的 Kestrel。
   * **關鍵 Header 傳遞**：反向代理會附加 `X-Forwarded-For`（真實客戶端 IP）與 `X-Forwarded-Proto`（原始協定）。
3. **Kestrel 接收與 `HttpContext` 封裝**：
   * ASP.NET Core 的跨平台非同步 I/O 伺服器 **Kestrel** 監聽 Socket。
   * Kestrel 解析 HTTP 封包（Request Line、Headers、Body Stream），並為該次請求在記憶體中建立 **`HttpContext`**（包含 `HttpRequest`、`HttpResponse`、`User`、`Items` 等核心物件）。

---

## 階段二：ASP.NET Core 中介軟體管線層（Middleware Pipeline）

ASP.NET Core 採用**洋蔥模型（俄羅斯套娃）**，請求會依序「向內穿過」各個 Middleware，最後再「由內向外」原路折返。

```
[Request] 
  ➜ ExceptionHandler 
    ➜ HttpsRedirection 
      ➜ Routing 
        ➜ CORS 
          ➜ Authentication 
            ➜ Authorization 
              ➜ Endpoints (MVC/API Filter)
```

1. **`UseExceptionHandler`（全域例外處理）**：
   * 擺在最外層。它先呼叫 `next()` 讓內部執行；一旦內層拋出未捕捉的 Exception，它會捕捉並將錯誤轉換為統一的 **RFC 7807 `ProblemDetails`** 格式，避免洩漏伺服器敏感堆疊資訊。
2. **`UseRouting`（路由比對）**：
   * 分析 Request URL 與 HTTP Verb（`POST /api/v1/orders`），比對端點清單（Endpoint DataSource），找到匹配的 Action：`OrdersController.CreateOrder()`。
   * 此時只做**路由決策（Routing Decision）**，尚未正式呼叫 Action。
3. **`UseCors`（跨來源資源共用）**：
   * 如果是跨域請求，檢查 `Origin` 標頭是否在允許名單。
   * 若遇到瀏覽器的 `OPTIONS` 預檢請求（Preflight），在此層就會直接回傳 204 No Content，不會繼續往下走。
4. **`UseAuthentication`（身分驗證）**：
   * 解析 Header 的 `Authorization: Bearer <token>`。
   * 透過密碼學簽名金鑰驗證 JWT 是否被竄改、是否過期（`exp`）、發行者（`iss`）是否正確。
   * 驗證通過後，將 Payload 解密轉換成 **`ClaimsPrincipal`** 物件，掛載到 `HttpContext.User`。
5. **`UseAuthorization`（授權檢查）**：
   * 檢查該 Action 是否有 `[Authorize]`、`[Authorize(Roles = "...")]` 或自訂 Policy。
   * 根據剛剛掛在 `HttpContext.User` 上的 Claim 判斷使用者是否具備足夠權限。若驗證無效回傳 `401 Unauthorized`；若已登入但無權限則回傳 `403 Forbidden`。

---

## 階段三：MVC / Web API 內部過濾器管線（Filter Pipeline）

當通過 Middleware 後，請求進入 **Endpoint Execution**，此時會觸發 MVC 的過濾器管線：

1. **Authorization Filter**：最先執行的過濾器（MVC 層級的授權檢查）。
2. **Resource Filter**：常用於快取截流（若有快取直接在此返回，略過後續 Controller）。
3. **Model Binding（模型綁定）與 Validation（模型驗證）**：
   * **綁定**：框架依據 `[FromBody]` 標籤，讀取 Request Body Stream，透過 `System.Text.Json` 將 JSON 反序列化成 C# 的 DTO 物件（如 `CreateOrderRequest`）。
   * **驗證**：依據 DTO 上的 Data Annotations（如 `[Required]`, `[Range(1, 100)]`）或 FluentValidation 進行欄位檢驗。
   * 💡 **亮點**：若有標記 `[ApiController]`，一旦 `ModelState.IsValid == false`，框架會**自動攔截**並回傳 400 Bad Request，根本不需要手寫 `if (!ModelState.IsValid)`。
4. **Action Filter（`OnActionExecuting`）**：
   * 在進入 Controller 具體方法前執行，常用於：全域 Audit Log（記錄是誰在什麼時間點傳入什麼參數）、自訂邏輯防禦。

---

## 階段四：業務邏輯與資料存取層（Controller ➜ Service ➜ EF Core ➜ DB）

1. **Controller 實例化與 DI 容器（Dependency Injection）**：
   * 框架從 DI 容器中建立當前 Controller，並解析其建構式所宣告的相依物件（例如 `IOrderService`、`AppDbContext`）。
   * 這裡生命週期為 **`Scoped`**，與當前 HTTP Request 綁定。
2. **Controller 委派與業務驗證**：
   * Controller 保持輕量（Skinny Controller），只負責協調與接收 DTO，將參數傳入 Service 層。
   * Service 執行商業邏輯（例如：檢查庫存、計算折扣、檢查使用者額度）。
3. **Entity Framework Core 存取資料庫**：
   * Service 呼叫 `_dbContext.Orders.Add(order)`。
   * **Change Tracker**：EF Core 的異動追蹤器將實體狀態標記為 `Added`。
   * 呼叫 `await _dbContext.SaveChangesAsync(cancellationToken)`：
     * EF Core 將實體操作與 LINQ 翻譯成具體的 SQL 語法（如 `INSERT INTO Orders ...`）。
     * 從 **ADO.NET 連線池（Connection Pool）** 借出一條開啟的資料庫連線。
     * 開啟資料庫交易（Transaction），將 SQL 語句發送給 Database（如 SQL Server / PostgreSQL / SQLite）。
     * 執行成功後提交（Commit），連線保持準備釋放狀態。

---

## 階段五：回應流出與序列化（On The Way Out）

1. **Controller 包裝回應**：
   * Controller 收到 Service 回傳的結果，轉換成 ViewModel/DTO（嚴禁直接回傳 EF Entity 避免過度暴露資料與循環引用）。
   * 回傳標準 RESTful 結果：例如 `CreatedAtAction(..., newOrderDto)`，狀態碼為 **`201 Created`**，並在 Header 附上 `Location: /api/v1/orders/{newId}`。
2. **過濾器回程**：
   * 觸發 Action Filter 的 `OnActionExecuted`。
   * 觸發 Result Filter 的 `OnResultExecuting` / `OnResultExecuted`。
3. **Output Formatter（輸出序列化）**：
   * 框架查看 Request Header 的 `Accept`（內容協商 Content Negotiation，預設為 `application/json`）。
   * `System.Text.Json` 將 C# DTO 序列化成 UTF-8 JSON 位元組，直接寫入 `HttpResponse.Body` 的輸出資料流（Response Stream）。
4. **Middleware 洋蔥模型反向折返**：
   * 穿出 Endpoints，回到各個 Middleware `await next()` 之後的代碼。
   * 可以在此處記錄整個請求的**總執行時間**、寫入標準的診斷追蹤 ID（如 `X-Correlation-Id`）。

---

## 階段六：連線關閉與資源釋放（Cleanup）

1. **HTTP 回應抵達前端**：Kestrel 將 Response Header 與 Body 透過 TCP/TLS 回傳給 Client。
2. **DI 容器 Scope 結束（Dispose）**：
   * 當 HTTP 請求結束時，當前 Request 的 **`IServiceScope` 觸發銷毀**。
   * 容器依序呼叫所有標記為 `Scoped` 物件的 `Dispose()` / `DisposeAsync()`。
   * **`DbContext` 被釋放**，其佔用的資料庫連線歸還給 ADO.NET Connection Pool，記憶體資源等待 GC 回收。

---

## 🎯 面試官想聽的「三大殺手級加分亮點」

1. **全非同步與 CancellationToken（執行緒飢餓防禦）**：
   * Controller ➜ Service ➜ EF Core 全程使用 `async/await` 並傳遞 `CancellationToken`，用戶斷線時能及時釋放資料庫查詢，避免 Thread Pool Starvation。
2. **Scoped 與 Singleton 的陷阱（Captive Dependency）**：
   * 嚴禁將 Scoped 物件（如 DbContext）注入 Singleton，避免捕獲相依性導致常駐、記憶體洩漏與併發崩潰。
3. **DTO 與 Entity 隔離 ＆ 統一錯誤格式（ProblemDetails）**：
   * 避免 Over-Posting 與循環參照，全域實作 RFC 7807 ProblemDetails 提供前後端一致的錯誤結構。

---

## 🔍 核心概念拆解與深入探討清單

1. **網路與伺服器層**：Kestrel 是什麼？它跟 IIS / Nginx 有什麼關係？
2. **中介軟體管線（Middleware）**：什麼叫「洋蔥模型（進去又出來）」？`next()` 到底在幹嘛？
3. **驗證與授權**：JWT 進來後到底是怎麼變成 `HttpContext.User` 的？401 跟 403 在管線哪裡被擋掉？
4. **Model Binding 與 Validation**：前端傳來的 JSON 是怎麼自動變成 C# 物件的？
5. **資料庫與 DI 生命週期**：什麼是 `Scoped`？EF Core 的 Change Tracker 與連線池在做什麼？