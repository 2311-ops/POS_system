using Dapper;
using FashionPOS;
using FashionPOS.Data;
using FashionPOS.Models;
using FashionPOS.Services;

SqlMapper.AddTypeHandler(new UserRoleTypeHandler());

var db = new DatabaseContext();
var initializer = new DatabaseInitializer(db);
initializer.Initialize();

var auth = new AuthService(db);
var prod = new ProductService(db);
var sale = new SaleService(db, prod, auth);
var user = new UserService(db);
var ev = new EventService(db);

int passed = 0, failed = 0;

void Test(string name, Func<bool> test)
{
    try
    {
        if (test()) { Console.WriteLine($"  [PASS] {name}"); passed++; }
        else { Console.WriteLine($"  [FAIL] {name}"); failed++; }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  [ERROR] {name} -- {ex.Message}");
        failed++;
    }
}

Console.WriteLine("\n=== AUTH SERVICE ===");

Test("Owner login succeeds with correct credentials", () => auth.Login("admin", "admin123") != null);
Test("Login fails with wrong password", () => auth.Login("admin", "wrongpassword") == null);
Test("Login fails with non-existent user", () => auth.Login("nobody", "test") == null);
Test("CurrentUser is set after login", () => { auth.Login("admin", "admin123"); return auth.CurrentUser != null; });
Test("CurrentUser is null after logout", () => { auth.Login("admin", "admin123"); auth.Logout(); return auth.CurrentUser == null; });

Console.WriteLine("\n=== USER PERMISSIONS ===");

var owner = new User { Role = UserRole.Owner };
var manager = new User { Role = UserRole.Manager };
var cashier = new User { Role = UserRole.Cashier };

Test("Owner can view cost price", () => owner.CanViewCostPrice);
Test("Owner can view profit", () => owner.CanViewProfit);
Test("Owner can manage users", () => owner.CanManageUsers);
Test("Owner can manage inventory", () => owner.CanManageInventory);
Test("Owner can access dashboard", () => owner.CanAccessDashboard);
Test("Manager can manage inventory", () => manager.CanManageInventory);
Test("Manager can view reports", () => manager.CanViewReports);
Test("Manager CANNOT manage users", () => !manager.CanManageUsers);
Test("Manager CANNOT view profit", () => !manager.CanViewProfit);
Test("Cashier CANNOT view cost price", () => !cashier.CanViewCostPrice);
Test("Cashier CANNOT access dashboard", () => !cashier.CanAccessDashboard);
Test("Cashier CANNOT view reports", () => !cashier.CanViewReports);
Test("Cashier CAN access POS", () => cashier.CanAccessPOS);
Test("Cashier CAN access events", () => cashier.CanAccessEvent);

Console.WriteLine("\n=== PRODUCT SERVICE ===");

var testProduct = new Product
{
    Name = $"TEST_PRODUCT_{DateTime.Now.Ticks}",
    SKU = $"TEST-SKU-{DateTime.Now.Ticks}",
    SellingPrice = 100,
    CostPrice = 60,
    StockQuantity = 10,
    LowStockThreshold = 3,
    IsActive = true
};

var savedId = prod.Save(testProduct);
Test("Product saves and returns valid Id", () => savedId > 0);

var fetched = prod.GetById(savedId);
Test("Saved product can be fetched by Id", () => fetched != null);
Test("Fetched product name matches", () => fetched?.Name == testProduct.Name);
Test("Fetched product price matches", () => fetched?.SellingPrice == 100);
Test("Margin calculated correctly (40%)", () => fetched?.Margin == 40);
Test("IsLowStock false when stock=10, thresh=3", () => fetched?.IsLowStock == false);
Test("IsOutOfStock false when stock=10", () => fetched?.IsOutOfStock == false);

testProduct.Id = savedId;
testProduct.StockQuantity = 2;
prod.Save(testProduct);
var updated = prod.GetById(savedId);
Test("Product update works", () => updated?.StockQuantity == 2);
Test("IsLowStock true when stock=2, thresh=3", () => updated?.IsLowStock == true);

prod.AdjustStock(savedId, 1, 5, MovementType.Restock.ToString(), "Test restock");
var afterRestock = prod.GetById(savedId);
Test("AdjustStock adds quantity correctly", () => afterRestock?.StockQuantity == 7);

prod.AdjustStock(savedId, 1, -3, MovementType.Adjustment.ToString(), "Test deduct");
var afterDeduct = prod.GetById(savedId);
Test("AdjustStock deducts quantity correctly", () => afterDeduct?.StockQuantity == 4);

var searchResults = prod.Search("TEST_PRODUCT");
Test("Search returns results", () => searchResults.Any());
Test("Search finds by partial name", () => searchResults.Any(p => p.Id == savedId));

testProduct.StockQuantity = 2;
testProduct.LowStockThreshold = 5;
prod.Save(testProduct);
var lowStock = prod.GetLowStock();
Test("GetLowStock returns low stock products", () => lowStock.Any(p => p.Id == savedId));

prod.Delete(savedId);
var deleted = prod.GetById(savedId);
Test("Soft delete sets IsActive=false", () => deleted?.IsActive == false);
Test("GetAll excludes deleted products", () => !prod.GetAll(activeOnly: true).Any(p => p.Id == savedId));

Console.WriteLine("\n=== SALE SERVICE ===");

var saleProduct = new Product
{
    Name = $"SALE_TEST_{DateTime.Now.Ticks}",
    SellingPrice = 200,
    CostPrice = 120,
    StockQuantity = 5,
    IsActive = true,
    CategoryId = prod.GetCategories().First().Id
};
var spId = prod.Save(saleProduct);
saleProduct.Id = spId;

var cart = new List<CartItem>
{
    new CartItem { Product = saleProduct, Quantity = 2 }
};

Test("CompleteSale throws on empty cart", () =>
{
    try { sale.CompleteSale(new List<CartItem>(), 1, "Cash"); return false; }
    catch (InvalidOperationException) { return true; }
});

var completedSale = sale.CompleteSale(cart, 1, "Cash");
Test("Sale completes successfully", () => completedSale.Id > 0);
Test("Sale number is generated", () => !string.IsNullOrWhiteSpace(completedSale.SaleNumber));
Test("Sale total = price x qty (200x2=400)", () => completedSale.TotalAmount == 400);
Test("Sale cost = cost x qty (120x2=240)", () => completedSale.TotalCost == 240);
Test("Sale profit = total - cost (400-240=160)", () => completedSale.Profit == 160);
Test("Sale payment method saved", () => completedSale.PaymentMethod == "Cash");

var afterSale = prod.GetById(spId);
Test("Stock deducted after sale (5-2=3)", () => afterSale?.StockQuantity == 3);

var bigCart = new List<CartItem>
{
    new CartItem { Product = afterSale!, Quantity = 99 }
};
Test("CompleteSale blocks overselling", () =>
{
    try { sale.CompleteSale(bigCart, 1, "Cash"); return false; }
    catch (InvalidOperationException) { return true; }
});

var loaded = sale.GetSaleWithItems(completedSale.Id).GetAwaiter().GetResult();
Test("GetSaleWithItems loads sale", () => loaded != null);
Test("GetSaleWithItems includes items", () => loaded?.Items.Count > 0);
Test("SaleItem quantity correct", () => loaded?.Items.First().Quantity == 2);
Test("SaleItem unit price correct", () => loaded?.Items.First().UnitPrice == 200);

var stats = sale.GetDashboardStats();
Test("GetDashboardStats returns object", () => stats != null);
Test("TodaySales >= completed sale amount", () => stats.TodaySales >= 400);
Test("TodayTransactions >= 1", () => stats.TodayTransactions >= 1);
Test("LowStockCount is non-negative", () => stats.LowStockCount >= 0);

Console.WriteLine("\n=== EVENT SERVICE ===");

var staleEvent = ev.GetActiveEvent();
if (staleEvent != null)
    ev.EndEvent(staleEvent.Id);

var testEvent = ev.StartEvent("Test Bazar", "Test Location", 1);
Test("Event starts with valid Id", () => testEvent.Id > 0);
Test("Event is active after start", () => testEvent.IsActive);

var activeEvent = ev.GetActiveEvent();
Test("GetActiveEvent returns started event", () => activeEvent?.Id == testEvent.Id);

var eventCart = new List<CartItem>
{
    new CartItem { Product = afterSale!, Quantity = 1 }
};
var eventSale = sale.CompleteSale(eventCart, 1, "Card", testEvent.Id);
Test("Sale links to event", () => eventSale.EventId == testEvent.Id);

var eventStats = ev.GetEventStats(testEvent.Id);
Test("Event stats show revenue", () => eventStats.TotalRevenue > 0);
Test("Event stats show profit", () => eventStats.TotalProfit > 0);
Test("Event items sold count = 1", () => eventStats.ItemsSold >= 1);
Test("Event transaction count >= 1", () => eventStats.TransactionCount >= 1);

ev.EndEvent(testEvent.Id);
var endedEvent = ev.GetActiveEvent();
Test("GetActiveEvent returns null after end", () => endedEvent == null || endedEvent.Id != testEvent.Id);

Console.WriteLine("\n=== USER SERVICE ===");

var testUser = new User
{
    Username = $"testcashier_{DateTime.Now.Ticks}",
    FullName = "Test Cashier",
    Role = UserRole.Cashier,
    IsActive = true
};
user.Save(testUser, "testpass123");

var allUsers = user.GetAll();
Test("New user appears in GetAll", () => allUsers.Any(u => u.Username == testUser.Username));

var savedUser = allUsers.First(u => u.Username == testUser.Username);
Test("Saved user role is Cashier", () => savedUser.Role == UserRole.Cashier);

var cashierLogin = auth.Login(testUser.Username, "testpass123");
Test("New cashier can login", () => cashierLogin != null);

user.Save(savedUser, "newpassword456");
Test("Password change works -- old password rejected", () => auth.Login(testUser.Username, "testpass123") == null);
Test("Password change works -- new password accepted", () => auth.Login(testUser.Username, "newpassword456") != null);

user.Delete(savedUser.Id);
Test("Deleted user cannot login", () => auth.Login(testUser.Username, "newpassword456") == null);

Console.WriteLine("\n=== CATEGORIES ===");

var cats = prod.GetCategories();
var expectedCats = new[]
{
    "Cardigans & Kaftans", "Dresses", "Jackets & Coats",
    "Jumpsuits", "Sets", "Shirts"
};

Test("Exactly 6 categories exist", () => cats.Count == 6);
foreach (var expected in expectedCats)
    Test($"Category exists: {expected}", () => cats.Any(c => c.Name == expected));

Console.WriteLine($"\n{new string('-', 40)}");
Console.WriteLine($"  PASSED: {passed}");
Console.WriteLine($"  FAILED: {failed}");
Console.WriteLine($"  TOTAL:  {passed + failed}");
Console.WriteLine($"{new string('-', 40)}");

if (failed == 0) Console.WriteLine("\n  ALL TESTS PASSED");
else Console.WriteLine($"\n  {failed} TEST(S) FAILED -- fix issues above");
