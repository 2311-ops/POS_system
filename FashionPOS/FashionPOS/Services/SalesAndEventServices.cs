using Dapper;
using Microsoft.Data.Sqlite;
using FashionPOS.Models;
using FashionPOS.Data;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace FashionPOS.Services
{
    internal sealed class SalesAggregateRow
    {
        public decimal Total { get; set; }
        public decimal Profit { get; set; }
        public int Transactions { get; set; }
        public int ItemsSold { get; set; }
    }

    internal sealed class TotalOnlyRow
    {
        public decimal Total { get; set; }
    }

    public class SaleService
    {
        private readonly DatabaseContext _context;
        private readonly ProductService _productService;
        private readonly AuthService _authService;

        public SaleService(DatabaseContext context, ProductService productService, AuthService authService)
        {
            _context = context;
            _productService = productService;
            _authService = authService;
        }


        /// <summary>
        /// Completes a sale transaction with cart items.
        /// </summary>
        public Sale CompleteSale(List<CartItem> cartItems, int userId, string paymentMethod,
                                 int? eventId = null, string? note = null)
        {

            if (cartItems == null || cartItems.Count == 0)
                throw new InvalidOperationException("Cart is empty");

            // Validate stock quantities first (using a separate connection)
            using (var validateConn = _context.CreateConnection())
            {
                foreach (var item in cartItems)
                {
                    var product = validateConn.QuerySingleOrDefault<Product>(
                        "SELECT * FROM Products WHERE Id = @Id",
                        new { Id = item.Product?.Id }
                    );

                    if (product == null || product.StockQuantity < item.Quantity)
                        throw new InvalidOperationException(
                            $"Insufficient stock for {item.Product?.Name}. Available: {product?.StockQuantity ?? 0}"
                        );
                }
            }

            // Begin transaction in a new connection (already open from CreateConnection)
            using (var connection = _context.CreateConnection())
            {
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // GUID suffix avoids collisions during rapid consecutive sales.
                        string prefix = eventId.HasValue ? "EV" : "S";
                        string saleNumber = $"{prefix}{DateTime.Now:yyyyMMddHHmmss}{Guid.NewGuid().ToString("N")[..6].ToUpper()}";

                        // Calculate totals
                        decimal totalAmount = cartItems.Sum(x => x.TotalPrice);
                        decimal totalCost = cartItems.Sum(x => (x.Product?.CostPrice ?? 0) * x.Quantity);
                        decimal profit = totalAmount - totalCost;

                        // Insert sale
                        var insertSaleSql = @"
                            INSERT INTO Sales (SaleNumber, UserId, EventId, TotalAmount, TotalCost, Profit, PaymentMethod, Note, CreatedAt)
                            VALUES (@SaleNumber, @UserId, @EventId, @TotalAmount, @TotalCost, @Profit, @PaymentMethod, @Note, @CreatedAt);
                            SELECT last_insert_rowid();
                        ";

                        int saleId = connection.QuerySingle<int>(
                            insertSaleSql,
                            new
                            {
                                SaleNumber = saleNumber,
                                UserId = userId,
                                EventId = eventId,
                                TotalAmount = totalAmount,
                                TotalCost = totalCost,
                                Profit = profit,
                                PaymentMethod = paymentMethod,
                                Note = note,
                                CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                            },
                            transaction
                        );

                        // Insert sale items and update stock
                        foreach (var item in cartItems)
                        {
                            var product = connection.QuerySingleOrDefault<Product>(
                                "SELECT * FROM Products WHERE Id = @Id",
                                new { Id = item.Product?.Id },
                                transaction
                            );

                            if (product != null)
                            {
                                // Insert sale item
                                connection.Execute(
                                    @"INSERT INTO SaleItems 
                                      (SaleId, ProductId, ProductName, Size, Color, Quantity, UnitPrice, UnitCost)
                                      VALUES (@SaleId, @ProductId, @ProductName, @Size, @Color, @Quantity, @UnitPrice, @UnitCost)",
                                    new
                                    {
                                        SaleId = saleId,
                                        ProductId = item.Product?.Id,
                                        ProductName = item.Product?.Name,
                                        Size = item.Product?.Size,
                                        Color = item.Product?.Color,
                                        Quantity = item.Quantity,
                                        UnitPrice = item.Product?.SellingPrice,
                                        UnitCost = product.CostPrice
                                    },
                                    transaction
                                );

                                // Update stock
                                var updatedRows = connection.Execute(
                                    @"UPDATE Products
                                      SET StockQuantity = MAX(0, StockQuantity - @Qty)
                                      WHERE Id = @Id AND StockQuantity >= @Qty",
                                    new { Qty = item.Quantity, Id = item.Product?.Id },
                                    transaction);

                                if (updatedRows == 0)
                                    throw new InvalidOperationException($"Insufficient stock for {item.Product?.Name}");

                                // Log stock movement
                                connection.Execute(
                                    @"INSERT INTO StockMovements (ProductId, UserId, Type, Quantity, CreatedAt)
                                      VALUES (@ProductId, @UserId, 'Sale', @Quantity, @CreatedAt)",
                                    new
                                    {
                                        ProductId = item.Product?.Id,
                                        UserId = userId,
                                        Quantity = -item.Quantity,
                                        CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                                    },
                                    transaction
                                );
                            }
                        }

                        transaction.Commit();

                        // Return populated sale using the same connection (sync, no deadlock)
                        return GetSaleWithItemsSync(saleId, connection)
                            ?? throw new Exception("Failed to retrieve completed sale");
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Gets sales within a date range.
        /// </summary>
        public List<Sale> GetSales(DateTime? from, DateTime? to, int? userId = null)
        {
            using (var connection = _context.CreateConnection())
            {
                var sql = @"
                    SELECT s.*, u.FullName as CashierName
                    FROM Sales s
                    LEFT JOIN Users u ON s.UserId = u.Id
                    LEFT JOIN Events e ON s.EventId = e.Id
                    WHERE (s.EventId IS NULL OR e.IsActive = 0)
                ";

                if (from.HasValue)
                    sql += $" AND DATE(s.CreatedAt) >= '{from:yyyy-MM-dd}'";

                if (to.HasValue)
                    sql += $" AND DATE(s.CreatedAt) <= '{to:yyyy-MM-dd}'";

                if (userId.HasValue)
                    sql += $" AND s.UserId = {userId}";

                sql += " ORDER BY s.CreatedAt DESC";

                return connection.Query<Sale>(sql).ToList();
            }
        }

        /// <summary>
        /// Gets a sale with all its items (synchronous, reuses existing connection).
        /// </summary>
        private Sale? GetSaleWithItemsSync(int saleId, SqliteConnection connection, IDbTransaction? transaction = null)
        {
            var sale = connection.QuerySingleOrDefault<Sale>(
                @"SELECT s.*, u.FullName as CashierName
                  FROM Sales s
                  LEFT JOIN Users u ON s.UserId = u.Id
                  WHERE s.Id = @Id",
                new { Id = saleId },
                transaction
            );

            if (sale != null)
            {
                sale.Items = connection.Query<SaleItem>(
                    "SELECT * FROM SaleItems WHERE SaleId = @SaleId",
                    new { SaleId = saleId },
                    transaction
                ).ToList();
            }

            return sale;
        }

        /// <summary>
        /// Gets a sale with all its items (async public version).
        /// </summary>
        public async Task<Sale?> GetSaleWithItems(int saleId)
        {
            return await Task.Run(() =>
            {
                using (var connection = _context.CreateConnection())
                {
                    return GetSaleWithItemsSync(saleId, connection);
                }
            });
        }

        /// <summary>
        /// Gets comprehensive dashboard statistics.
        /// </summary>
        public DashboardStats GetDashboardStats()
        {
            using (var connection = _context.CreateConnection())
            {
                var today = DateTime.Now.Date;
                var weekAgo = today.AddDays(-7);
                var monthAgo = today.AddDays(-30);

                var stats = new DashboardStats();

                // Today's sales
                var todaySalesRow = connection.QuerySingle<SalesAggregateRow>(
                    @"SELECT COALESCE(SUM(TotalAmount), 0) as Total, 
                             COALESCE(SUM(Profit), 0) as Profit,
                             COUNT(*) as Transactions,
                             COALESCE((
                                 SELECT SUM(si.Quantity)
                                 FROM Sales s2
                                 LEFT JOIN Events e2 ON s2.EventId = e2.Id
                                 JOIN SaleItems si ON si.SaleId = s2.Id
                                 WHERE (s2.EventId IS NULL OR e2.IsActive = 0)
                                 AND DATE(s2.CreatedAt) = @Today
                              ), 0) as ItemsSold
                       FROM Sales s
                       LEFT JOIN Events e ON s.EventId = e.Id
                       WHERE (s.EventId IS NULL OR e.IsActive = 0)
                       AND DATE(s.CreatedAt) = @Today",
                    new { Today = today.ToString("yyyy-MM-dd") }
                );

                stats.TodaySales = todaySalesRow.Total;
                stats.TodayProfit = todaySalesRow.Profit;
                stats.TodayTransactions = todaySalesRow.Transactions;
                stats.TodayItemsSold = todaySalesRow.ItemsSold;

                // Week sales
                var weekSalesRow = connection.QuerySingle<TotalOnlyRow>(
                    @"SELECT COALESCE(SUM(s.TotalAmount), 0) as Total
                      FROM Sales s
                      LEFT JOIN Events e ON s.EventId = e.Id
                      WHERE (s.EventId IS NULL OR e.IsActive = 0)
                      AND DATE(s.CreatedAt) >= @WeekAgo",
                    new { WeekAgo = weekAgo.ToString("yyyy-MM-dd") }
                );

                stats.WeekSales = weekSalesRow.Total;

                // Month sales
                var monthSalesRow = connection.QuerySingle<TotalOnlyRow>(
                    @"SELECT COALESCE(SUM(s.TotalAmount), 0) as Total
                      FROM Sales s
                      LEFT JOIN Events e ON s.EventId = e.Id
                      WHERE (s.EventId IS NULL OR e.IsActive = 0)
                      AND DATE(s.CreatedAt) >= @MonthAgo",
                    new { MonthAgo = monthAgo.ToString("yyyy-MM-dd") }
                );

                stats.MonthSales = monthSalesRow.Total;

                // Low stock and out of stock
                stats.LowStockCount = connection.QuerySingle<int>(
                    "SELECT COUNT(*) FROM Products WHERE IsActive = 1 AND StockQuantity > 0 AND StockQuantity <= LowStockThreshold"
                );

                stats.OutOfStockCount = connection.QuerySingle<int>(
                    "SELECT COUNT(*) FROM Products WHERE IsActive = 1 AND StockQuantity <= 0"
                );

                // Low stock products
                stats.LowStockProducts = connection.Query<Product>(
                    @"SELECT p.*, col.Name as CollectionName, cat.Name as CategoryName
                      FROM Products p
                      LEFT JOIN Collections col ON p.CollectionId = col.Id
                      LEFT JOIN Categories cat ON p.CategoryId = cat.Id
                      WHERE p.IsActive = 1 AND p.StockQuantity <= p.LowStockThreshold
                      ORDER BY p.StockQuantity ASC
                      LIMIT 10"
                ).ToList();

                // Top products
                stats.TopProducts = connection.Query<TopProduct>(
                    @"SELECT p.Name,
                             SUM(si.Quantity) as QuantitySold,
                             COALESCE(SUM(si.UnitPrice * si.Quantity), 0) as Revenue
                      FROM SaleItems si
                      JOIN Sales s ON si.SaleId = s.Id
                      LEFT JOIN Events e ON s.EventId = e.Id
                      JOIN Products p ON si.ProductId = p.Id
                      WHERE (s.EventId IS NULL OR e.IsActive = 0)
                      AND DATE(s.CreatedAt) = @Today
                      GROUP BY p.Id, p.Name
                      ORDER BY QuantitySold DESC
                      LIMIT 5",
                    new { Today = today.ToString("yyyy-MM-dd") }
                ).ToList();

                return stats;
            }
        }
    }

    public class EventService
    {
        private readonly DatabaseContext _context;

        public EventService(DatabaseContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Starts a new bazar event.
        /// </summary>
        public BazarEvent StartEvent(string name, string? location, int userId)
        {
            using (var connection = _context.CreateConnection())
            {
                var sql = @"
                    INSERT INTO Events (Name, Location, StartedAt, IsActive, CreatedBy)
                    VALUES (@Name, @Location, @StartedAt, 1, @CreatedBy);
                    SELECT last_insert_rowid();
                ";

                var eventId = connection.QuerySingle<int>(
                    sql,
                    new
                    {
                        Name = name,
                        Location = location,
                        StartedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        CreatedBy = userId
                    }
                );

                return new BazarEvent
                {
                    Id = eventId,
                    Name = name,
                    Location = location,
                    StartedAt = DateTime.Now,
                    IsActive = true,
                    CreatedBy = userId
                };
            }
        }

        /// <summary>
        /// Gets the currently active event.
        /// </summary>
        public BazarEvent? GetActiveEvent()
        {
            using (var connection = _context.CreateConnection())
            {
                return connection.QuerySingleOrDefault<BazarEvent>(
                    "SELECT * FROM Events WHERE IsActive = 1 ORDER BY StartedAt DESC LIMIT 1"
                );
            }
        }

        /// <summary>
        /// Ends an event.
        /// </summary>
        public void EndEvent(int eventId)
        {
            using (var connection = _context.CreateConnection())
            {
                connection.Execute(
                    @"UPDATE Events SET IsActive = 0, EndedAt = @EndedAt WHERE Id = @Id",
                    new
                    {
                        EndedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        Id = eventId
                    }
                );
            }
        }

        /// <summary>
        /// Gets event statistics.
        /// </summary>
        public BazarEvent GetEventStats(int eventId)
        {
            using (var connection = _context.CreateConnection())
            {
                var eventObj = connection.QuerySingleOrDefault<BazarEvent>(
                    "SELECT * FROM Events WHERE Id = @Id",
                    new { Id = eventId }
                );

                if (eventObj == null)
                    throw new InvalidOperationException("Event not found");

                // Get sales stats for this event
                var stats = connection.QuerySingle<SalesAggregateRow>(
                    @"SELECT COALESCE(SUM(TotalAmount), 0) as Total,
                             COALESCE(SUM(Profit), 0) as Profit,
                             COUNT(*) as Transactions
                      FROM Sales
                      WHERE EventId = @EventId",
                    new { EventId = eventId }
                );

                eventObj.TotalRevenue = stats.Total;
                eventObj.TotalProfit = stats.Profit;
                eventObj.TransactionCount = stats.Transactions;

                // Get items sold
                var itemsSold = connection.QuerySingleOrDefault<int>(
                    @"SELECT COALESCE(SUM(Quantity), 0)
                      FROM SaleItems
                      WHERE SaleId IN (SELECT Id FROM Sales WHERE EventId = @EventId)",
                    new { EventId = eventId }
                );

                eventObj.ItemsSold = itemsSold;

                return eventObj;
            }
        }
    }
}
