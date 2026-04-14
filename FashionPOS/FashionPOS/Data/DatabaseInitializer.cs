using Dapper;
using Microsoft.Data.Sqlite;
using BCrypt.Net;
using System;
using System.Linq;

namespace FashionPOS.Data
{
    public class DatabaseInitializer
    {
        private readonly DatabaseContext _context;

        public DatabaseInitializer(DatabaseContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Initializes the database schema and seeds default data if needed.
        /// </summary>
        public void Initialize()
        {
            using (var connection = _context.CreateConnection())
            {
                // Create all tables
                CreateTables(connection);

                // Seed default data if Users table is empty
                SeedDefaultData(connection);
            }
        }

        private void CreateTables(SqliteConnection connection)
        {
            // Single batch execution of all CREATE TABLE statements
            var sql = @"
                CREATE TABLE IF NOT EXISTS Users (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Username TEXT NOT NULL UNIQUE,
                    PasswordHash TEXT NOT NULL,
                    FullName TEXT NOT NULL,
                    Role TEXT NOT NULL CHECK(Role IN ('Owner','Manager','Cashier')),
                    IsActive INTEGER NOT NULL DEFAULT 1,
                    CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
                    LastLogin TEXT
                );

                CREATE TABLE IF NOT EXISTS Collections (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL UNIQUE,
                    Description TEXT
                );

                CREATE TABLE IF NOT EXISTS Categories (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL UNIQUE,
                    Description TEXT
                );

                CREATE TABLE IF NOT EXISTS Products (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    SKU TEXT UNIQUE,
                    CollectionId INTEGER REFERENCES Collections(Id),
                    CategoryId INTEGER REFERENCES Categories(Id),
                    Size TEXT,
                    Color TEXT,
                    CostPrice REAL NOT NULL DEFAULT 0,
                    SellingPrice REAL NOT NULL DEFAULT 0,
                    StockQuantity INTEGER NOT NULL DEFAULT 0,
                    LowStockThreshold INTEGER NOT NULL DEFAULT 5,
                    ImagePath TEXT,
                    Barcode TEXT,
                    IsActive INTEGER NOT NULL DEFAULT 1,
                    CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
                    UpdatedAt TEXT NOT NULL DEFAULT (datetime('now'))
                );

                CREATE TABLE IF NOT EXISTS Events (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    Location TEXT,
                    StartedAt TEXT NOT NULL DEFAULT (datetime('now')),
                    EndedAt TEXT,
                    IsActive INTEGER NOT NULL DEFAULT 1,
                    CreatedBy INTEGER REFERENCES Users(Id)
                );

                CREATE TABLE IF NOT EXISTS Sales (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    SaleNumber TEXT NOT NULL UNIQUE,
                    UserId INTEGER NOT NULL REFERENCES Users(Id),
                    EventId INTEGER REFERENCES Events(Id),
                    TotalAmount REAL NOT NULL,
                    TotalCost REAL NOT NULL,
                    Profit REAL NOT NULL,
                    PaymentMethod TEXT NOT NULL DEFAULT 'Cash',
                    Note TEXT,
                    CreatedAt TEXT NOT NULL DEFAULT (datetime('now'))
                );

                CREATE TABLE IF NOT EXISTS SaleItems (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    SaleId INTEGER NOT NULL REFERENCES Sales(Id),
                    ProductId INTEGER NOT NULL REFERENCES Products(Id),
                    ProductName TEXT NOT NULL,

                    Size TEXT,
                    Color TEXT,
                    Quantity INTEGER NOT NULL,
                    UnitPrice REAL NOT NULL,
                    UnitCost REAL NOT NULL
                );

                CREATE TABLE IF NOT EXISTS StockMovements (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ProductId INTEGER NOT NULL REFERENCES Products(Id),
                    UserId INTEGER NOT NULL REFERENCES Users(Id),
                    Type TEXT NOT NULL CHECK(Type IN ('Sale','Restock','Adjustment','Transfer')),
                    Quantity INTEGER NOT NULL,
                    Note TEXT,
                    CreatedAt TEXT NOT NULL DEFAULT (datetime('now'))
                );

                CREATE TABLE IF NOT EXISTS AuditLogs (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER REFERENCES Users(Id),
                    Action TEXT NOT NULL,
                    Details TEXT,
                    CreatedAt TEXT NOT NULL DEFAULT (datetime('now'))
                );

                CREATE INDEX IF NOT EXISTS idx_products_collection ON Products(CollectionId);
                CREATE INDEX IF NOT EXISTS idx_products_category ON Products(CategoryId);
                CREATE INDEX IF NOT EXISTS idx_sales_date        ON Sales(CreatedAt);
                CREATE INDEX IF NOT EXISTS idx_saleitems_sale    ON SaleItems(SaleId);
                CREATE INDEX IF NOT EXISTS idx_stock_product     ON StockMovements(ProductId);
            ";

            connection.Execute(sql);
            EnsureProductSchema(connection);
        }

        private void EnsureProductSchema(SqliteConnection connection)
        {
            try
            {
                // Check if Collections table exists (new schema indicator)
                var hasCollections = connection.QuerySingle<int>(
                    "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Collections'");

                if (hasCollections == 0)
                {
                    // Old schema detected - create Collections table first
                    connection.Execute(@"
                        CREATE TABLE Collections (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            Name TEXT NOT NULL UNIQUE,
                            Description TEXT
                        );
                    "
                    );
                }

                // Check if Products has CollectionId column
                var hasCollectionId = connection.QuerySingle<int>(
                    "SELECT COUNT(*) FROM pragma_table_info('Products') WHERE name = 'CollectionId'");

                if (hasCollectionId == 0)
                {
                    connection.Execute("ALTER TABLE Products ADD COLUMN CollectionId INTEGER REFERENCES Collections(Id);");
                }
            }
            catch { }
        }

        private void SeedDefaultData(SqliteConnection connection)
        {
            // Ensure default collections exist
            var insertCollectionsSql = "INSERT OR IGNORE INTO Collections (Name) VALUES (@Name)";
            var collections = new[]
            {
                new { Name = "Autumn & Winter" },
                new { Name = "Summer & Spring" },
                new { Name = "Ramadan" },
                new { Name = "Evening Wear" }
            };
            foreach (var collection in collections)
            {
                try { connection.Execute(insertCollectionsSql, collection); } catch { }
            }

            // Ensure default categories exist
            var insertCategoriesSql = "INSERT OR IGNORE INTO Categories (Name) VALUES (@Name)";
            var categories = new[]
            {
                new { Name = "Cardigans & Kaftans" },
                new { Name = "Dresses" },
                new { Name = "Jackets & Coats" },
                new { Name = "Jumpsuits" },
                new { Name = "Sets" },
                new { Name = "Shirts" }
            };
            foreach (var category in categories)
            {
                try { connection.Execute(insertCategoriesSql, category); } catch { }
            }

            // Try to remove old generic category placeholders
            try
            {
                connection.Execute("DELETE FROM Categories WHERE Name IN ('Collections', 'Evening Wear and Categories')");
            }
            catch { }

            // Check and seed default users
            var insertUserSql = @"
                INSERT OR IGNORE INTO Users (Username, PasswordHash, FullName, Role, IsActive, CreatedAt)
                VALUES (@Username, @PasswordHash, @FullName, @Role, @IsActive, @CreatedAt)
            ";

            // Seed admin if not exists
            var adminExists = connection.QuerySingle<int>("SELECT COUNT(*) FROM Users WHERE Username = 'admin'") > 0;
            if (!adminExists)
            {
                connection.Execute(insertUserSql, new
                {
                    Username = "admin",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                    FullName = "System Owner",
                    Role = "Owner",
                    IsActive = 1,
                    CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                });
            }

            // Seed gerbera if not exists
            var gerberaExists = connection.QuerySingle<int>("SELECT COUNT(*) FROM Users WHERE Username = 'gerbera'") > 0;
            if (!gerberaExists)
            {
                connection.Execute(insertUserSql, new
                {
                    Username = "gerbera",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("123"),
                    FullName = "Gerbera User",
                    Role = "Owner",
                    IsActive = 1,
                    CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                });
            }
        }
    }
}
