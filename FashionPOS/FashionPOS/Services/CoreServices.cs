using Dapper;
using Microsoft.Data.Sqlite;
using FashionPOS.Models;
using FashionPOS.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FashionPOS.Services
{
    public class AuthService
    {
        private readonly DatabaseContext _context;
        public User? CurrentUser { get; private set; }

        public AuthService(DatabaseContext context)
        {
            _context = context;
        }

        public User? Login(string username, string password)
        {
            username = username.Trim();
            using (var connection = _context.CreateConnection())
            {
                var user = connection.QuerySingleOrDefault<User>(
                    "SELECT * FROM Users WHERE Username = @Username COLLATE NOCASE AND IsActive = 1",
                    new { Username = username }
                );

                if (user == null)
                    return null;

                if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
                    return null;

                connection.Execute(
                    "UPDATE Users SET LastLogin = @LastLogin WHERE Id = @Id",
                    new { LastLogin = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), Id = user.Id }
                );

                CurrentUser = user;
                return user;
            }
        }

        public void Logout()
        {
            CurrentUser = null;
        }
    }

    public class ProductService
    {
        private readonly DatabaseContext _context;

        public ProductService(DatabaseContext context)
        {
            _context = context;
        }

        public List<Product> GetAll(bool activeOnly = true)
        {
            using (var connection = _context.CreateConnection())
            {
                var sql = @"
                    SELECT p.*, col.Name as CollectionName, cat.Name as CategoryName 
                    FROM Products p
                    LEFT JOIN Collections col ON p.CollectionId = col.Id
                    LEFT JOIN Categories cat ON p.CategoryId = cat.Id
                    WHERE 1=1 " + (activeOnly ? "AND p.IsActive = 1 " : "") + @"
                    ORDER BY p.Name
                ";

                return connection.Query<Product>(sql).ToList();
            }
        }

        public List<Product> Search(string query)
        {
            using (var connection = _context.CreateConnection())
            {
                var sql = @"
                    SELECT p.*, col.Name as CollectionName, cat.Name as CategoryName
                    FROM Products p
                    LEFT JOIN Collections col ON p.CollectionId = col.Id
                    LEFT JOIN Categories cat ON p.CategoryId = cat.Id
                    WHERE p.IsActive = 1 AND (
                        p.Name LIKE @Query OR
                        p.SKU LIKE @Query OR 
                        p.Barcode LIKE @Query OR
                        p.Color LIKE @Query OR
                        p.Size LIKE @Query
                    )
                    LIMIT 50
                ";

                return connection.Query<Product>(sql, new { Query = $"%{query}%" }).ToList();
            }
        }

        public Product? GetById(int id)
        {
            using (var connection = _context.CreateConnection())
            {
                return connection.QuerySingleOrDefault<Product>(
                    @"SELECT p.*, col.Name as CollectionName, cat.Name as CategoryName 
                      FROM Products p
                      LEFT JOIN Collections col ON p.CollectionId = col.Id
                      LEFT JOIN Categories cat ON p.CategoryId = cat.Id
                      WHERE p.Id = @Id",
                    new { Id = id }
                );
            }
        }

        public int Save(Product product)
        {
            using (var connection = _context.CreateConnection())
            {
                product.UpdatedAt = DateTime.Now;
                product.StockQuantity = Math.Max(0, product.StockQuantity);

                if (product.Id == 0)
                {
                    var sql = @"
                        INSERT INTO Products 
                        (Name, SKU, CollectionId, CategoryId, Size, Color, CostPrice, SellingPrice, 
                         StockQuantity, LowStockThreshold, ImagePath, Barcode, IsActive, CreatedAt, UpdatedAt)
                        VALUES (@Name, @SKU, @CollectionId, @CategoryId, @Size, @Color, @CostPrice, @SellingPrice,
                                @StockQuantity, @LowStockThreshold, @ImagePath, @Barcode, @IsActive, @CreatedAt, @UpdatedAt);
                        SELECT last_insert_rowid();
                    ";

                    var id = connection.QuerySingle<int>(sql, product);

                    // Keep stock ledger aligned with on-hand quantity from the first write.
                    if (product.StockQuantity != 0)
                    {
                        var userId = connection.QuerySingleOrDefault<int?>(
                            "SELECT Id FROM Users WHERE IsActive = 1 ORDER BY CASE WHEN Username = 'admin' THEN 0 ELSE 1 END, Id LIMIT 1");

                        if (userId.HasValue)
                        {
                            connection.Execute(
                                @"INSERT INTO StockMovements (ProductId, UserId, Type, Quantity, Note, CreatedAt)
                                  VALUES (@ProductId, @UserId, 'Adjustment', @Quantity, @Note, @CreatedAt)",
                                new
                                {
                                    ProductId = id,
                                    UserId = userId.Value,
                                    Quantity = product.StockQuantity,
                                    Note = "Initial stock on product creation",
                                    CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                                });
                        }
                    }

                    return id;
                }
                else
                {
                    var previousStock = connection.QuerySingleOrDefault<int?>(
                        "SELECT StockQuantity FROM Products WHERE Id = @Id",
                        new { product.Id });

                    var sql = @"
                        UPDATE Products SET
                            Name = @Name, SKU = @SKU, CollectionId = @CollectionId, CategoryId = @CategoryId,
                            Size = @Size, Color = @Color, CostPrice = @CostPrice,
                            SellingPrice = @SellingPrice, StockQuantity = @StockQuantity,
                            LowStockThreshold = @LowStockThreshold, ImagePath = @ImagePath,
                            Barcode = @Barcode, IsActive = @IsActive, UpdatedAt = @UpdatedAt
                        WHERE Id = @Id
                    ";

                    connection.Execute(sql, product);

                    if (previousStock.HasValue)
                    {
                        var delta = product.StockQuantity - previousStock.Value;
                        if (delta != 0)
                        {
                            var userId = connection.QuerySingleOrDefault<int?>(
                                "SELECT Id FROM Users WHERE IsActive = 1 ORDER BY CASE WHEN Username = 'admin' THEN 0 ELSE 1 END, Id LIMIT 1");

                            if (userId.HasValue)
                            {
                                connection.Execute(
                                    @"INSERT INTO StockMovements (ProductId, UserId, Type, Quantity, Note, CreatedAt)
                                      VALUES (@ProductId, @UserId, 'Adjustment', @Quantity, @Note, @CreatedAt)",
                                    new
                                    {
                                        ProductId = product.Id,
                                        UserId = userId.Value,
                                        Quantity = delta,
                                        Note = "Stock adjusted from product edit",
                                        CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                                    });
                            }
                        }
                    }

                    return product.Id;
                }
            }
        }

        public void Delete(int id)
        {
            using (var connection = _context.CreateConnection())
            {
                connection.Execute(
                    "UPDATE Products SET IsActive = 0 WHERE Id = @Id",
                    new { Id = id }
                );
            }
        }

        public List<Collection> GetCollections()
        {
            using (var connection = _context.CreateConnection())
            {
                return connection.Query<Collection>("SELECT * FROM Collections ORDER BY Name").ToList();
            }
        }

        public List<Category> GetCategories()
        {
            using (var connection = _context.CreateConnection())
            {
                return connection.Query<Category>("SELECT * FROM Categories ORDER BY Name").ToList();
            }
        }

        public List<Product> GetLowStock()
        {
            using (var connection = _context.CreateConnection())
            {
                var sql = @"
                    SELECT p.*, col.Name as CollectionName, cat.Name as CategoryName
                    FROM Products p
                    LEFT JOIN Collections col ON p.CollectionId = col.Id
                    LEFT JOIN Categories cat ON p.CategoryId = cat.Id
                    WHERE p.IsActive = 1 AND p.StockQuantity <= p.LowStockThreshold
                    ORDER BY p.StockQuantity ASC
                ";

                return connection.Query<Product>(sql).ToList();
            }
        }

        public void AdjustStock(int productId, int userId, int quantity, string movementType, string? note)
        {
            using (var connection = _context.CreateConnection())
            {
                try
                {
                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            var affected = connection.Execute(
                                @"UPDATE Products
                                  SET StockQuantity = MAX(0, StockQuantity + @Quantity)
                                  WHERE Id = @ProductId",
                                new { Quantity = quantity, ProductId = productId },
                                transaction);

                            if (affected == 0)
                                throw new InvalidOperationException("Product not found");

                            connection.Execute(
                                @"INSERT INTO StockMovements (ProductId, UserId, Type, Quantity, Note, CreatedAt)
                                  VALUES (@ProductId, @UserId, @Type, @Quantity, @Note, @CreatedAt)",
                                new
                                {
                                    ProductId = productId,
                                    UserId = userId,
                                    Type = movementType,
                                    Quantity = quantity,
                                    Note = note,
                                    CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                                },
                                transaction
                            );

                            transaction.Commit();
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Failed to adjust stock: {ex.Message}", ex);
                }
            }
        }
    }

    public class UserService
    {
        private readonly DatabaseContext _context;

        public UserService(DatabaseContext context)
        {
            _context = context;
        }

        public List<User> GetAll()
        {
            using (var connection = _context.CreateConnection())
            {
                return connection.Query<User>("SELECT * FROM Users ORDER BY FullName").ToList();
            }
        }

        public User? GetById(int id)
        {
            using (var connection = _context.CreateConnection())
            {
                return connection.QuerySingleOrDefault<User>(
                    "SELECT * FROM Users WHERE Id = @Id",
                    new { Id = id }
                );
            }
        }

        public void Save(User user, string? plainPassword = null)
        {
            using (var connection = _context.CreateConnection())
            {
                if (user.Id == 0)
                {
                    if (string.IsNullOrEmpty(plainPassword))
                        throw new ArgumentException("Password is required for new users");

                    user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(plainPassword);
                    user.CreatedAt = DateTime.Now;

                    var sql = @"
                        INSERT INTO Users (Username, PasswordHash, FullName, Role, IsActive, CreatedAt)
                        VALUES (@Username, @PasswordHash, @FullName, @Role, @IsActive, @CreatedAt)
                    ";

                    connection.Execute(sql, new
                    {
                        user.Username,
                        user.PasswordHash,
                        user.FullName,
                        Role = user.Role.ToString(),
                        user.IsActive,
                        user.CreatedAt
                    });
                }
                else
                {
                    if (!string.IsNullOrEmpty(plainPassword))
                    {
                        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(plainPassword);
                    }

                    var sql = @"
                        UPDATE Users SET
                            Username = @Username, PasswordHash = @PasswordHash, 
                            FullName = @FullName, Role = @Role, IsActive = @IsActive
                        WHERE Id = @Id
                    ";

                    connection.Execute(sql, new
                    {
                        user.Username,
                        user.PasswordHash,
                        user.FullName,
                        Role = user.Role.ToString(),
                        user.IsActive,
                        user.Id
                    });
                }
            }
        }

        public void Delete(int id)
        {
            using (var connection = _context.CreateConnection())
            {
                connection.Execute(
                    "UPDATE Users SET IsActive = 0 WHERE Id = @Id",
                    new { Id = id }
                );
            }
        }
    }
}
