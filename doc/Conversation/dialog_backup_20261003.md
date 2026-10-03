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