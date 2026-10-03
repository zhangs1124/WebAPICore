using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using WebAPICore.Api.Data;
using WebAPICore.Api.Dtos;
using WebAPICore.Api.Models.Entities;
using WebAPICore.Api.Services;

namespace WebAPICore.Tests;

/// <summary>
/// 輕量進銷存核心業務邏輯測試（總量制、採購審核、在途量連動、進貨驗收、盤點校正、RBAC 權限）
/// </summary>
public class InventoryAndPurchaseTests
{
    private static AppDbContext CreateInMemoryDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task PurchaseOrder_CreateAndApprove_ShouldIncreaseOnOrderQty()
    {
        // Arrange (準備測試資料：商品庫存 10，在途量 0)
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var inventoryService = new InventoryService(db, NullLogger<InventoryService>.Instance);
        var poService = new PurchaseOrderService(db, NullLogger<PurchaseOrderService>.Instance);

        var product = new Product
        {
            Id = Guid.NewGuid(),
            Sku = "SKU-TEST-001",
            Name = "機械鍵盤",
            Category = "周邊配件",
            UnitPrice = 2500m,
            SafetyStock = 5
        };
        var stock = new ProductStock
        {
            ProductId = product.Id,
            CurrentQty = 10,
            OnOrderQty = 0
        };
        db.Products.Add(product);
        db.ProductStocks.Add(stock);
        await db.SaveChangesAsync();

        // Act 1: 建立採購單 (採購 50 件)
        var createRequest = new CreatePurchaseOrderRequest(
            SupplierName: "台灣優質電子股份有限公司",
            Remark: "第三季常態補貨",
            Items: new List<CreatePurchaseOrderItemRequest>
            {
                new(product.Id, OrderedQty: 50, UnitPrice: 1800m)
            },
            CreatedBy: "Buyer_Alice"
        );
        var createdPo = await poService.CreateAsync(createRequest);

        // Assert 1: 草稿狀態，在途量尚未增加
        Assert.Equal("Draft", createdPo.Status);
        Assert.Equal(90000m, createdPo.TotalAmount);
        var stockBeforeApprove = await db.ProductStocks.FindAsync(product.Id);
        Assert.Equal(0, stockBeforeApprove!.OnOrderQty);

        // Act 2: 主管審核核准
        var approvedPo = await poService.ApproveAsync(createdPo.Id, approvedBy: "Manager_Bob");

        // Assert 2: 狀態變為 Approved，在途量由 0 累加至 50
        Assert.Equal("Approved", approvedPo.Status);
        Assert.Equal("Manager_Bob", approvedPo.ApprovedBy);
        var stockAfterApprove = await db.ProductStocks.FindAsync(product.Id);
        Assert.Equal(50, stockAfterApprove!.OnOrderQty);
        Assert.Equal(10, stockAfterApprove.CurrentQty); // 現有庫存尚未改變
    }

    [Fact]
    public async Task PurchaseOrder_ReceiveInbound_ShouldUpdateStockAndCompleteWhenFullyReceived()
    {
        // Arrange
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var poService = new PurchaseOrderService(db, NullLogger<PurchaseOrderService>.Instance);

        var product = new Product
        {
            Id = Guid.NewGuid(),
            Sku = "SKU-CPU-01",
            Name = "Intel CPU",
            Category = "處理器",
            UnitPrice = 8000m
        };
        var stock = new ProductStock
        {
            ProductId = product.Id,
            CurrentQty = 5,
            OnOrderQty = 0
        };
        db.Products.Add(product);
        db.ProductStocks.Add(stock);
        await db.SaveChangesAsync();

        // 建立採購單 100 件並核准
        var po = await poService.CreateAsync(new CreatePurchaseOrderRequest(
            SupplierName: "聯強國際",
            Remark: "伺服器專案",
            Items: new List<CreatePurchaseOrderItemRequest> { new(product.Id, 100, 6500m) },
            CreatedBy: "Buyer"
        ));
        await poService.ApproveAsync(po.Id, "Manager");

        var poItemId = po.Items[0].Id;

        // Act 1: 第一次到貨驗收 40 件 (分批到貨)
        var receive1 = await poService.ReceiveInboundAsync(new ReceivePurchaseOrderRequest(
            PurchaseOrderId: po.Id,
            Items: new List<ReceivePurchaseOrderItemRequest> { new(poItemId, 40) },
            Operator: "Warehouse_Charlie"
        ));

        // Assert 1: 在途量扣減 40 (剩 60)，現有庫存增加 40 (5+40=45)，狀態維持 Approved
        Assert.Equal("Approved", receive1.Status);
        var stockAfter1 = await db.ProductStocks.FindAsync(product.Id);
        Assert.Equal(60, stockAfter1!.OnOrderQty);
        Assert.Equal(45, stockAfter1.CurrentQty);

        // Act 2: 第二次到貨驗收剩餘 60 件 (全數到齊)
        var receive2 = await poService.ReceiveInboundAsync(new ReceivePurchaseOrderRequest(
            PurchaseOrderId: po.Id,
            Items: new List<ReceivePurchaseOrderItemRequest> { new(poItemId, 60) },
            Operator: "Warehouse_Charlie"
        ));

        // Assert 2: 在途量歸 0，現有庫存變為 105 (45+60)，採購單自動結案轉為 Completed
        Assert.Equal("Completed", receive2.Status);
        var stockAfter2 = await db.ProductStocks.FindAsync(product.Id);
        Assert.Equal(0, stockAfter2!.OnOrderQty);
        Assert.Equal(105, stockAfter2.CurrentQty);

        // 驗證流水帳記錄共 2 筆 INBOUND
        var movements = await db.StockMovements.Where(m => m.ProductId == product.Id).ToListAsync();
        Assert.Equal(2, movements.Count);
        Assert.All(movements, m => Assert.Equal("INBOUND", m.MovementType));
    }

    [Fact]
    public async Task Inventory_StocktakeAdjustment_ShouldOverwriteQtyAndRecordDifference()
    {
        // Arrange (帳面庫存 20 件，現場摔破 3 件，實盤 17 件)
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var inventoryService = new InventoryService(db, NullLogger<InventoryService>.Instance);

        var product = new Product
        {
            Id = Guid.NewGuid(),
            Sku = "SKU-GLASS-01",
            Name = "高透光玻璃杯",
            Category = "器皿"
        };
        var stock = new ProductStock
        {
            ProductId = product.Id,
            CurrentQty = 20
        };
        db.Products.Add(product);
        db.ProductStocks.Add(stock);
        await db.SaveChangesAsync();

        // Act: 主管執行盤點校正
        var adjustResult = await inventoryService.AdjustStocktakeAsync(new StocktakeAdjustmentRequest(
            ProductId: product.Id,
            ActualQty: 17,
            Reason: "倉庫架子傾倒破損 3 件",
            Operator: "Manager_David"
        ));

        // Assert: 實盤數量覆寫為 17，差額為 -3，記錄 ADJUSTMENT
        Assert.Equal("ADJUSTMENT", adjustResult.MovementType);
        Assert.Equal(-3, adjustResult.Quantity);
        Assert.Equal(20, adjustResult.PreviousQty);
        Assert.Equal(17, adjustResult.NewQty);
        Assert.Equal("Manager_David", adjustResult.Operator);

        var stockInDb = await db.ProductStocks.FindAsync(product.Id);
        Assert.Equal(17, stockInDb!.CurrentQty);
    }

    [Fact]
    public async Task Inventory_ReturnInbound_ShouldIncreaseStock()
    {
        // Arrange (同仁退料 3 件)
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var inventoryService = new InventoryService(db, NullLogger<InventoryService>.Instance);

        var product = new Product
        {
            Id = Guid.NewGuid(),
            Sku = "SKU-SCREW-01",
            Name = "不鏽鋼螺絲包",
            Category = "耗材"
        };
        var stock = new ProductStock
        {
            ProductId = product.Id,
            CurrentQty = 50
        };
        db.Products.Add(product);
        db.ProductStocks.Add(stock);
        await db.SaveChangesAsync();

        // Act: 執行退料入庫
        var returnResult = await inventoryService.ReturnInboundAsync(new StockReturnRequest(
            ProductId: product.Id,
            Quantity: 5,
            Reason: "維修專案結案未用完繳回",
            Operator: "Engineer_Ken"
        ));

        // Assert: 庫存增加為 55
        Assert.Equal(55, returnResult.NewQty);
        Assert.Equal("INBOUND", returnResult.MovementType);

        var stockInDb = await db.ProductStocks.FindAsync(product.Id);
        Assert.Equal(55, stockInDb!.CurrentQty);
    }

    [Fact]
    public async Task Inventory_Outbound_InsufficientStock_ShouldThrowInvalidOperationException()
    {
        // Arrange (現有庫存 10 件，欲出庫 20 件)
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var inventoryService = new InventoryService(db, NullLogger<InventoryService>.Instance);

        var product = new Product
        {
            Id = Guid.NewGuid(),
            Sku = "SKU-MONITOR-01",
            Name = "4K 螢幕",
            Category = "顯示設備"
        };
        var stock = new ProductStock
        {
            ProductId = product.Id,
            CurrentQty = 10
        };
        db.Products.Add(product);
        db.ProductStocks.Add(stock);
        await db.SaveChangesAsync();

        // Act & Assert: 防超賣保護觸發
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            inventoryService.OutboundAsync(new StockOutboundRequest(
                ProductId: product.Id,
                Quantity: 20,
                Reason: "大批銷貨",
                Operator: "Sales"
            ))
        );
    }

    [Fact]
    public async Task AuthService_RegisterAndLogin_ShouldSucceedWithAssignedRole()
    {
        // Arrange
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var authService = new AuthService(db, NullLogger<AuthService>.Instance);

        // Act 1: 註冊四種不同角色
        var warehouseUser = await authService.RegisterAsync(new CreateUserRequest(
            Username: "operator_chen",
            Password: "SecurePassword123!",
            DisplayName: "陳倉管",
            Role: "Warehouse"
        ));

        var managerUser = await authService.RegisterAsync(new CreateUserRequest(
            Username: "manager_lee",
            Password: "ManagerSecret888!",
            DisplayName: "李主管",
            Role: "Manager"
        ));

        // Assert 1: 正確存入角色
        Assert.Equal("Warehouse", warehouseUser.Role);
        Assert.Equal("Manager", managerUser.Role);

        // Act 2: 登入驗證
        var loginResult = await authService.LoginAsync(new LoginRequest("operator_chen", "SecurePassword123!"));

        // Assert 2: 登入成功並取得角色與 Token
        Assert.Equal("operator_chen", loginResult.Username);
        Assert.Equal("陳倉管", loginResult.DisplayName);
        Assert.Equal("Warehouse", loginResult.Role);
        Assert.False(string.IsNullOrWhiteSpace(loginResult.Token));

        // Act 3: 錯誤密碼登入
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            authService.LoginAsync(new LoginRequest("operator_chen", "WrongPassword!")));
    }
}
