# 對話備份紀錄 - 2026-10-03

## 📌 今日討論主題與成果摘要
1. **Postman 免費版 vs 專業版 (Professional)**：比較使用者人數、Request 額度、Mock Server、監控頻率與 CI/CD Newman 限制，產出分析並存檔於 Postman.md。
2. **Web API 測試重點與邊界值測試 (Boundary Value Testing)**：
   - 釐清「邊界值即是規格的極限條件」（如年份極值 1800/2100、字串極限長度、負數/0 頁碼、空白 Body、非法 GUID 等）。
   - 審查現有 WebAPICore.Tests/BooksApiTests.cs，產出完整的 API測試重點.md，涵蓋既有覆蓋項目、建議補齊之邊界與協議層測試、測試命名標準與 xUnit 範例代碼。
3. **第三章面試核心：EF Core vs Dapper 技術選型**：
   - 分析兩者在開發效率、維護成本、記憶體消耗與原生 SQL 控制力上的取捨。
   - 提出現代主流的混搭模式（Command 用 EF Core，Query 用 Dapper / AsNoTracking）。
4. **EF Core 近代效能革新（Change Tracker 與批次操作）**：
   - 針對使用者提出的「EF Core Tracking 改善效能」深入探討 EF Core 7/8/9 的重大進步。
   - 包含 ExecuteUpdateAsync / ExecuteDeleteAsync 繞過記憶體追蹤直譯 SQL。
   - AutoDetectChangesEnabled 關閉與手動偵測時機、AsNoTracking / AsNoTrackingWithIdentityResolution、AsSplitQuery 解決笛卡兒積，以及 PropertyAccessMode.Field。

---

## 💡 詳細技術精華整理

### 1. EF Core vs Dapper 選型指南 (面試與實務架構)

| 評估維度 | Entity Framework Core | Dapper |
| :--- | :--- | :--- |
| **本質** | 全功能 ORM (Full-featured ORM) | 輕量級微型 ORM (Micro-ORM / Object Mapper) |
| **優勢** | 開發極快、LINQ 型別安全、自動 Migration、單元工作模式 (Unit of Work)、自動 Change Tracking | 效能接近原生 ADO.NET、記憶體開銷極低、直接掌控原生 SQL、支援複雜報表與 Stored Procedure |
| **劣勢** | 複雜查詢可能產生低效 SQL、大量寫入時 Change Tracker 有記憶體開銷 | 需手寫 SQL、無自動關聯載入、無 Migration 機制、重構欄位時編譯器無法自動檢查 |
| **面試加分結論** | 商業系統通常建議 **「混合模式 (CQS/CQRS)」**：<br>1. **寫入 (Command)**：使用 EF Core，利用 DbContext 交易一致性與驗證機制確保資料完整。<br>2. **讀取 (Query)**：一般查詢使用 EF Core + AsNoTracking()；針對高併發或百萬級報表查詢使用 Dapper 或原生 SQL。 |

---

### 2. EF Core 近代 Change Tracker 與效能大升級

使用者提到「最近 EF Core 追蹤（Tracking）有改變功能改善效能」，主要包含以下幾項里程碑進化：

1. **直譯批次更新/刪除 (ExecuteUpdate / ExecuteDelete)**：
   - **過去痛點**：以往更新 10,000 筆資料，必須先 SELECT 載入記憶體、Change Tracker 逐筆建立快照 (Snapshot)、修改屬性、最後呼叫 SaveChanges() 逐筆發送 UPDATE，極度耗費記憶體與網路 I/O。
   - **最新改善**：直接生成一條批次 SQL：
     `csharp
     await context.Books
         .Where(b => b.PublishYear < 2000)
         .ExecuteUpdateAsync(s => s.SetProperty(b => b.Price, b => b.Price * 0.9m));
     `
     **完全繞過 Change Tracker，不載入實體至記憶體，直接在資料庫完成批次更新！**

2. **批次操作時關閉變更追蹤 (AutoDetectChangesEnabled = false)**：
   - 當必須進行大量實體新增/修改時，預設每次加入實體都會觸發變更偵測演算法 (N^2)$。
   - 改善技巧：
     `csharp
     context.ChangeTracker.AutoDetectChangesEnabled = false;
     // 大量 AddRange 或修改
     context.ChangeTracker.DetectChanges(); // 最後手動呼叫一次
     await context.SaveChangesAsync();
     `

3. **唯讀查詢最佳化 (AsNoTracking 與 IdentityResolution)**：
   - 查詢時使用 .AsNoTracking()，EF Core 不會建立快照與監控實體，節省大量的 CPU 與記憶體。
   - 若關聯資料中有重複參照（例如多筆訂單指向同一個顧客），可使用 .AsNoTrackingWithIdentityResolution()，兼顧不追蹤的效能與物件圖譜的一致性。

4. **拆分查詢 (AsSplitQuery) 避免笛卡兒積爆炸**：
   - 當包含多個 Include() 集合時，單一 SQL JOIN 可能導致重複資料暴增。
   - EF Core 支援拆分查詢，將集合關聯拆成多個獨立 SELECT 查詢，大幅降低記憶體與傳輸頻寬消耗。

---

## 📂 異動與產生檔案
1. Postman.md (已轉換為 UTF-8 BOM) - Postman 免費版與專業版功能與選型分析。
2. API測試重點.md (已轉換為 UTF-8 BOM) - 完整的 API 測試與邊界值測試檢查清單。
3. WebAPI面試重點.md (已轉換為 UTF-8 BOM) - 面試指南與各階段練習路線。
4. doc/Conversation/dialog_backup_20261003.md (UTF-8 BOM) - 本次對話精華完整存檔。
5. doc/Conversation/dialog_summary_master.md (UTF-8 BOM) - 對話摘要總表，供新對話快速讀取。

---

## 🎯 下一步任務規劃 (階段 3 實作)
- [ ] 於 WebAPICore.Api 引入 EF Core 與 SQLite（或 In-Memory DB for Testing）。
- [ ] 建立 BookDbContext 與實體設定。
- [ ] 建立 Service 層與介面，將 BooksController 的靜態 List 抽離。
- [ ] 實作分頁（Pagination）、排序（Sorting）與條件過濾（Filtering）。
- [ ] 將 Change Tracker 效能原則（如 AsNoTracking）落實於讀取 API 中。

---

## 🚀 2026-10-03 晚間進度備份：雲端進銷存系統全棧實作、GitHub 上線與 Render 雲端部署

### 一、本次對話核心成果與突破
1. **Web API 請求生命週期面試深度剖析**：
   - 完成從客戶端 ➜ 反向代理 ➜ Kestrel ➜ Middleware 洋蔥管線 ➜ Filter ➜ Model Binding ➜ Controller ➜ Service ➜ EF Core ➜ 資料庫之全鏈路解析。
   - 產出教學與面試精華文件：[WebAPI請求生命週期詳解.md](../../WebAPI請求生命週期詳解.md)。
2. **進銷存（ERP）雲端資料庫架構落成**：
   - 於 **Supabase** 建立雲端 PostgreSQL 資料庫（專案：`進銷存API`）。
   - 解決直連 IPv6 逾時問題，切換至 Supabase 官方 IPv4 連線池（Pooler: `aws-0-ap-southeast-2.pooler.supabase.com:5432`）。
   - 透過 EF Core Code-First 建立並同步實體資料表：
     - `products`：商品基本主檔。
     - `product_stocks`：庫存即時檔（包含 `CHECK ("CurrentQty" >= 0)` 防超賣約束與 `xmin` 樂觀鎖版本）。
     - `stock_movements`：不可竄改的異動流水帳（Audit Ledger）。
3. **核心商業邏輯與 API 端點實作**：
   - 實作 `IInventoryService` / `InventoryService`，落實 **ACID 資料庫交易保證（`BeginTransactionAsync`）**，確保庫存異動與流水帳記錄同生共死。
   - 實作 `ProductsController`（商品 CRUD、關鍵字分頁查詢）與 `InventoryController`（入庫、出庫、安全庫存警戒清單、流水帳對帳）。
   - 整合 `Scalar.AspNetCore` 現代化 OpenAPI 互動文件介面（`/scalar/v1`）。
4. **前端介面全面升級（套用本地技能 `@mvc-tabler-layout`）**：
   - 遵循 Modal-First 與 Partial View First 規範。
   - 實作 `_Layout`、`_Sidebar`（240px 緊湊欄）、`_Header`（深淺模式切換、搜尋、鈴鐺）、`_Footer`、`_ProductModal`、`_StockModal` 與 `Index.cshtml`。
   - 4 欄式 KPI 營運指標卡（品項數、在庫總量、警戒數、DB 健康度）。
   - 即時 Fetch/AJAX 無跳轉 SPA CRUD，出庫防超賣 SweetAlert2 即時警告。
5. **DevOps、GitHub 與 Render 全自動部署上線**：
   - 建立多階段建置 [Dockerfile](../../Dockerfile) 與專業 [README.md](../../README.md)。
   - 透過 `GITHUB_TOKEN` API 自動建立公開儲存庫並推送到 GitHub：[https://github.com/zhangs1124/WebAPICore](https://github.com/zhangs1124/WebAPICore)。
   - 透過 **Playwright MCP 瀏覽器自動化** 在 Render 平台建立 Web Service，完成 Docker 映像檔雲端編譯與部署。
   - 成功實現 100% 正式上線（Live）：[https://webapicore.onrender.com/](https://webapicore.onrender.com/)。

---

### 二、遭遇問題與解決方案 (Bug & Pitfalls)
1. **Windows cmd.exe 執行 `start.bat` 報 `嚜濃echo` 亂碼**：
   - **原因**：`.bat` 批次檔若帶有 UTF-8 BOM，`cmd.exe` 會將 BOM 字元誤判為非法指令。
   - **修復**：重新以純 ASCII（無 BOM）格式寫入 `start.bat`，執行順暢。
2. **本機啟動時首頁報 500 錯誤**：
   - **原因**：先前建立 Razor 視圖時 `_Layout.cshtml` 等部分檔案未完全寫入，導致 Razor 引擎找不到版面。
   - **修復**：重新完整補齊 `_Layout`、`_Header`、`_Footer`、`_StockModal`，並確認全數為 UTF-8 BOM。
3. **Render 上線後 `/scalar/v1` 報 404**：
   - **原因**：`MapScalarApiReference` 原先被限制在 `app.Environment.IsDevelopment()` 區塊內，Production 環境被隱藏。
   - **修復**：將其移至外層全域啟用，推送到 GitHub 觸發 Render 自動熱更新部署。

---

### 三、待辦清單 (Todo List / 下一步任務)

- [ ] **任務 1：JWT 身分驗證與角色權限控制 (Role-based Authorization)**
  - [ ] 在 Supabase 新增 `users` 資料表（帳號、PasswordHash、Role、CreatedAt）。
  - [ ] 安裝 `Microsoft.AspNetCore.Authentication.JwtBearer` 並配置簽發與驗證中介軟體。
  - [ ] 實作 `AuthController`：`POST /api/v1/auth/login` 與 `POST /api/v1/auth/register`。
  - [ ] API 端點掛上權限守門員：
    - `[Authorize(Roles = "Admin")]`：新增商品。
    - `[Authorize(Roles = "Admin,Warehouse")]`：入庫與出庫作業。
    - `[AllowAnonymous]`：訪客唯讀查詢。
  - [ ] 實戰驗證 `401 Unauthorized` 與 `403 Forbidden` 錯誤攔截。
  - [ ] Tabler 儀表板加入「登入狀態」與「快速角色身分切換」。
  - [ ] Scalar API 文件支援 Bearer Token 授權輸入。
- [ ] **任務 2：進銷存業務進階升級（評估）**
  - [ ] 單據主明細整批出入庫（InboundOrder / OutboundOrder Master-Detail 批次過帳）。
  - [ ] 庫存二階段狀態機（Available 可用、On-Hand 現有、Reserved 預扣鎖定）。
- [ ] **任務 3：自動化測試與 GitHub Actions CI/CD**
  - [ ] 撰寫 `Task.WhenAll` 高並發出庫搶購測試（防超賣驗證）。
  - [ ] 配置 GitHub Actions 每次 Push 自動跑測試並產生 Coverage Report。

---

## 📅 2026-10-03 (夜間追加篇：待辦清單實體化與全域規則深化)

### 一、重要架構決策與實作

1. **實體待辦清單建立**：
   - 依據開發藍圖與實務需要，於 `doc/待辦清單.md`（及英文別名 `doc/todo.md`）建立完整的 WMS/ERP 系統功能未開發清單。
   - 區分五大優先級：安全性與權限 (Security & Auth)、業務深化與單據化 (WMS & Order Processing)、盤點與報表 (Audit & Reporting)、高並發防超賣壓測 (Testing)、維運監控與保活 (DevOps)。

2. **全域開發規範 (Global Rules) 升級**：
   - 於使用者全域規範檔案 `C:\Users\michael\.gemini\GEMINI.md` 中，正式加入「**### 5. 專案待辦清單規範 (Project Backlog & Todo Management)**」。
   - 定義 5 大自動化工作流：
     1. **路徑標準**：固定存放在 `doc/待辦清單.md`。
     2. **開新對話自動載入**：AI 開新對話主動研讀待辦清單並匯報優先項目。
     3. **完成功能自動劃記**：階段性開發完成自動將 `[ ]` 標註為 `[x]` 並填入日期。
     4. **未來需求主動收錄**：對話討論到延伸規格但未立即實作時，主動記錄防止遺忘。
     5. **維持 Git 規範**：單純修改待辦清單排除頻繁 Commit，隨原始碼提交。

3. **雲端保活機制諮詢**：
   - 分析 Render 服務 15 分鐘無流量休眠與冷啟動特性。
   - 分析 Supabase 免費專案 7 天無資料庫操作自動暫停機制。
   - 確保持續有 HTTP 登入存取或心跳排程 (`/healthz`) 可同時維持 Render 與 Supabase 活躍狀態。

### 二、異動與產出檔案

- [doc/待辦清單.md](file:///d:/project/WebAPICore/doc/待辦清單.md) (新增)
- [doc/todo.md](file:///d:/project/WebAPICore/doc/todo.md) (新增)
- [C:\Users\michael\.gemini\GEMINI.md](file:///C:/Users/michael/.gemini/GEMINI.md) (更新，加入待辦清單全域規範)
- [doc/Conversation/dialog_backup_20261003.md](file:///d:/project/WebAPICore/doc/Conversation/dialog_backup_20261003.md) (追加備份)
- [doc/Conversation/dialog_summary_master.md](file:///d:/project/WebAPICore/doc/Conversation/dialog_summary_master.md) (更新摘要)


---

## 📅 2026-10-03 (夜間升級篇：二層級手風琴選單、全寬大表與右側抽屜重構)

### 一、重要架構決策與實作

1. **二層級手風琴摺疊導覽選單 (Two-level Accordion Menu)**：
   - 於 `Views/Shared/_Sidebar.cshtml` 實作可展開/收合之兩層選單結構：
     - 📊 營運總覽
     - 📦 庫存管理 ▾（商品即時庫存、安全庫存預警）
     - 🛒 採購進貨 ▾（採購單列表 PO）
     - 🏢 廠商主檔 ▾（供應商資料維護）
     - ⚙️ 系統設定 ▾（帳號與部門權限，Admin 專屬）

2. **側邊欄整體手風琴平滑收折/隱藏切換 (Collapsible Sidebar)**：
   - 頂部導覽列與側邊欄頂部提供「收折/展開」漢堡切換按鈕。
   - 點擊後透過 CSS 動畫將側邊欄平滑收折（`-250px` 移出可視區），中央主工作區自動展開至 **100% 全寬度**，專注檢閱大數據報表；狀態持久化儲存於 `localStorage`。

3. **模組全寬大表與右側抽屜職責分離 (Full-Table View & Drawer Separation)**：
   - **中央主工作區**：全面升級為全寬數據大表（採購單大表、供應商主檔大表、使用者權限大表、庫存總表），提供部門下拉篩選、狀態過濾與搜尋。
   - **右側滑出抽屜 (Offcanvas Drawer)**：專注作為「新增填表」與「明細審查/盤點校正」面板，使用者在保留主畫面上下文的狀態下進行操作，體驗流暢。

4. **供應商主檔與部門資料隔離落實**：
   - 建立 `suppliers` 資料表與 EF Core Migration `AddSupplierAndDepartment`。
   - 採購單開單連動供應商下拉選單，並在入庫驗收時扣減在途量、增加庫存。
   - 使用者帳號與單據全面綁定 `Department`，落實資料列級隔離。

5. **單元測試與 Playwright 端對端驗證**：
   - `WebAPICore.Tests` 49 項單元測試 100% 通過。
   - 透過 Playwright 實測側邊欄收折、視圖切換、抽屜建立廠商、審批採購單、驗收入庫與庫存數值即時連動。