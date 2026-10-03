# 對話摘要主檔 (Dialog Summary Master)

此檔案用於記錄每次對話的超精簡進度與重點，以便於開啟新對話時能快速檢索整體專案脈絡，節省 Token 消耗。

---

## 📜 歷史對話紀錄清單

### [2026-10-03 對話紀錄](dialog_backup_20261003.md)
- **核心主題**：
  1. Postman 免費版 vs 專業版功能與選型比較（產出 [Postman.md](../../Postman.md)）。
  2. Web API 邊界值測試概念與現有測試審查，產出 [API測試重點.md](../../API測試重點.md)。
  3. 第三章面試題：EF Core vs Dapper 技術選型（CQS/CQRS 混合模式）。
  4. EF Core Change Tracker 效能大躍進：ExecuteUpdate/ExecuteDelete 免記憶體批次更新、AutoDetectChangesEnabled、AsNoTracking 與 AsSplitQuery。
- **產出/異動檔案**：
  - Postman.md
  - API測試重點.md
  - WebAPI面試重點.md
  - doc/Conversation/dialog_backup_20261003.md
- **當前專案狀態**：完成進銷存雲端實作（Supabase PostgreSQL + EF Core ACID 交易）、Tabler SaaS 儀表板、Scalar API 文件，並透過 Docker 成功上線至 Render 雲端平台與 GitHub！
- **2026-10-03 晚間重大里程碑**：
  1. 產出 [WebAPI請求生命週期詳解.md](../../WebAPI請求生命週期詳解.md)。
  2. 串接 Supabase 雲端資料庫（`products`, `product_stocks`, `stock_movements`，含 CHECK 約束與樂觀鎖）。
  3. 實作 `InventoryService` ACID 交易與防超賣機制。
  4. 套用 `@mvc-tabler-layout` 打造極簡現代 Tabler 儀表板，支援 SPA 磨砂彈窗與深淺主題防閃爍。
  5. GitHub 公開上線：[https://github.com/zhangs1124/WebAPICore](https://github.com/zhangs1124/WebAPICore)。
  6. Render 雲端平台 100% 部署上線 (Live)：[https://webapicore.onrender.com/](https://webapicore.onrender.com/)。
  7. **待辦清單 (Todo)**：加入 JWT 身分驗證與角色權限控制 (Role-based Auth：Admin, Warehouse, Viewer)、實戰展示 401 與 403。
  8. **待辦清單實體化**：於 [doc/待辦清單.md](../待辦清單.md) 建立完整的五大優先級 ERP/WMS 未開發功能藍圖。
  9. **全域開發規範深化**：於 [GEMINI.md](file:///C:/Users/michael/.gemini/GEMINI.md) 納入「專案待辦清單規範」，定義開新對話自動載入、完成劃記、架構收錄與 Git 例外管理。
31:   10. **企業級 ERP 兩層選單與全寬大表重構**：實作二層手風琴選單、全選單收折/展開切換 (100% 全寬)、採購與供應商及使用者全寬大表、右側抽屜專注填表，49 項測試全過並經 Playwright 驗證無誤。