# 2026-10-05 對話備份紀錄

## 📌 任務背景與使用者核心需求
1. **供應商維護與編輯**：原本僅有新增供應商與狀態切換，缺乏編輯功能；需補齊供應商「✏️ 編輯」按鈕與滑出抽屜（Drawer）修改作業（代碼為系統主鍵鎖定不可改）。
2. **商品主檔與供應商關聯管理**：商品主檔缺乏對應供應商關聯與編輯功能。需在商品模型增加 `SupplierId` 關聯、商品大表增加「主要供應商」欄位與「✏️ 編輯」按鈕。
3. **採購單供應商-商品連動過濾（Cascading Filter）**：在開立採購單時，選擇特定供應商後，商品下拉選單自動發送 AJAX 依 `supplierId` 篩選其供貨商品，並在點選商品時自動帶入預設進貨單價。
4. **多供應商商品種子資料注入**：使用者要求「幫我產生每個供應商多筆商品，直接從資料庫段輸入」，已於系統後端自動為 5 家供應商（聯強、展碁、精技、宏碁、華碩）各建立 3 筆高價值多樣化產品與對應在庫存量。

---

## 🛠️ 本次架構調整與實作明細

### 1. 後端實體與資料庫結構 (Entities & Migrations)
- [`Product.cs`](file:///d:/project/WebAPICore/WebAPICore.Api/Models/Entities/Product.cs)：加入 `SupplierId`（Guid?）外鍵與 `Supplier` 導覽屬性。
- [`Supplier.cs`](file:///d:/project/WebAPICore/WebAPICore.Api/Models/Entities/Supplier.cs)：加入 `Products` 集合導覽屬性。
- [`AppDbContext.cs`](file:///d:/project/WebAPICore/WebAPICore.Api/Data/AppDbContext.cs)：建立 `Product.HasOne(p => p.Supplier).WithMany(s => s.Products).HasForeignKey(p => p.SupplierId).OnDelete(DeleteBehavior.SetNull)` 關聯配置。
- [`Program.cs`](file:///d:/project/WebAPICore/WebAPICore.Api/Program.cs)：
  - 加入防禦性自動遷移 SQL：`ALTER TABLE products ADD COLUMN IF NOT EXISTS "SupplierId" uuid NULL;` 確保資料庫結構與 EF Core 模型 100% 同步。
  - 加入 5 大供應商專屬商品品項種子注入（涵蓋 SSD、DDR5、路由器、滑鼠、電競耳機、集線器、螢幕、印表機、不斷電系統、投影機、雙螢幕支架、ROG 鍵盤、金牌電源、TUF 主機板等共 15 項真實商品與在庫庫存）。

### 2. DTO 與 API 服務
- [`SupplierDtos.cs`](file:///d:/project/WebAPICore/WebAPICore.Api/Dtos/SupplierDtos.cs)：加入 `UpdateSupplierRequest`。
- [`InventoryDtos.cs`](file:///d:/project/WebAPICore/WebAPICore.Api/Dtos/InventoryDtos.cs)：`CreateProductRequest` 加入 `SupplierId`；新增 `UpdateProductRequest`；`ProductDetailResponse` 擴充 `SupplierId` 與 `SupplierName`。
- [`ISupplierService.cs`](file:///d:/project/WebAPICore/WebAPICore.Api/Services/ISupplierService.cs) & [`SupplierService.cs`](file:///d:/project/WebAPICore/WebAPICore.Api/Services/SupplierService.cs)：實作 `UpdateAsync(Guid id, UpdateSupplierRequest request)`。
- [`SuppliersController.cs`](file:///d:/project/WebAPICore/WebAPICore.Api/Controllers/SuppliersController.cs)：提供 `PUT /api/v1/suppliers/{id}` 端點。
- [`ProductsController.cs`](file:///d:/project/WebAPICore/WebAPICore.Api/Controllers/ProductsController.cs)：
  - `GetProducts` 端點支援 `[FromQuery] Guid? supplierId` 連動過濾查詢與 `Supplier.Name` 關聯。
  - `CreateProduct` 支援關聯供應商。
  - 新增 `PUT /api/v1/products/{id}` 支援商品主檔修改。

### 3. 前端 UI 與 SPA 交互體驗
- [`_SupplierDrawer.cshtml`](file:///d:/project/WebAPICore/WebAPICore.Api/Views/Shared/_SupplierDrawer.cshtml)：擴充支援新增/編輯兩種模式（代碼唯讀保護）。
- [`_ProductModal.cshtml`](file:///d:/project/WebAPICore/WebAPICore.Api/Views/Home/_ProductModal.cshtml)：加入「主要合作供應商」下拉選單、SKU 鎖定保護、編輯時隱藏期初庫存。
- [`Index.cshtml`](file:///d:/project/WebAPICore/WebAPICore.Api/Views/Home/Index.cshtml)：
  - 供應商大表：新增「✏️ 編輯」按鈕與 `openEditSupplierDrawer(id)`。
  - 商品大表：增加「主要供應商」徽章欄位、「✏️ 編輯」按鈕與 `openEditProductModal(id)`。
  - 採購單抽屜：監聽 `#poSupplierSelect` 變更事件，動態連動過濾 `#poProductSelect` 並於選取商品時自動帶入預設單價至 `#poUnitPrice`。

---

## 🧪 驗證與測試結果
1. **單元測試**：
   - `WebAPICore.Tests` 50 項單元測試 100% 通過（含新增之 `SupplierService_Update_ShouldModifyFieldsAndPersist`）。
2. **Playwright 自動化實機測試**：
   - 登入 `http://localhost:5092/` 後台。
   - 檢視供應商大表：5 家供應商皆呈現「✏️ 編輯」按鈕與正常狀態。
   - 檢視商品大表：各供應商商品皆已由資料庫段初始化完成，正確顯示所屬供應商徽章。
   - 檢視採購單抽屜：點選「華碩電腦股份有限公司」後，商品選單即時精準過濾出 3 款華碩商品，點擊商品即自動填入預設進價 NT$ 5490.00。

---

## 📁 檔案編碼規範檢查
- 所有新增與修改之檔案均維持 **UTF-8 BOM** 規範，中文註解與文案無亂碼。
