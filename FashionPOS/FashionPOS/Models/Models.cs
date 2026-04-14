using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FashionPOS.Models
{
    #region Enums

    public enum UserRole
    {
        Owner,
        Manager,
        Cashier
    }

    public enum MovementType
    {
        Sale,
        Restock,
        Adjustment,
        Transfer
    }

    public enum PaymentMethod
    {
        Cash,
        Card,
        Transfer
    }

    #endregion

    #region Product & Category

    public class Category
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    public class Collection
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    public class Product
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? SKU { get; set; }
        public int? CollectionId { get; set; }
        public string? CollectionName { get; set; }
        public int? CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public string? Size { get; set; }
        public string? Color { get; set; }
        public decimal CostPrice { get; set; }
        public decimal SellingPrice { get; set; }
        public int StockQuantity { get; set; }
        public int LowStockThreshold { get; set; } = 5;
        public string? ImagePath { get; set; }
        public string? Barcode { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        // Computed properties
        public decimal Margin => SellingPrice > 0 ? ((SellingPrice - CostPrice) / SellingPrice) * 100 : 0;
        public bool IsLowStock => StockQuantity > 0 && StockQuantity <= LowStockThreshold;
        public bool IsOutOfStock => StockQuantity <= 0;
    }

    #endregion

    #region User

    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public UserRole Role { get; set; } = UserRole.Cashier;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? LastLogin { get; set; }

        public bool CanAccessPOS => true;
        public bool CanAccessEvent => true;
        public bool CanManageInventory => Role == UserRole.Owner || Role == UserRole.Manager;
        public bool CanViewReports => Role == UserRole.Owner || Role == UserRole.Manager;
        public bool CanManageUsers => Role == UserRole.Owner;
        public bool CanViewCostPrice => Role == UserRole.Owner || Role == UserRole.Manager;
        public bool CanViewProfit => Role == UserRole.Owner;
        public bool CanAccessDashboard => Role == UserRole.Owner || Role == UserRole.Manager;
    }

    #endregion

    #region Sales

    public class SaleItem
    {
        public int Id { get; set; }
        public int SaleId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? Size { get; set; }
        public string? Color { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal UnitCost { get; set; }

        // Computed properties
        public decimal TotalPrice => UnitPrice * Quantity;
        public decimal TotalCost => UnitCost * Quantity;
        public decimal Profit => TotalPrice - TotalCost;
    }

    public class Sale
    {
        public int Id { get; set; }
        public string SaleNumber { get; set; } = string.Empty;
        public int UserId { get; set; }
        public string CashierName { get; set; } = string.Empty;
        public int? EventId { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal TotalCost { get; set; }
        public decimal Profit { get; set; }
        public string PaymentMethod { get; set; } = "Cash";
        public string? Note { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public List<SaleItem> Items { get; set; } = new();
    }

    #endregion

    #region Cart

    public class CartItem : INotifyPropertyChanged
    {
        private int _quantity;

        public Product? Product { get; set; }

        public int Quantity
        {
            get => _quantity;
            set
            {
                if (_quantity != value)
                {
                    _quantity = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(TotalPrice));
                }
            }
        }

        public decimal TotalPrice => Product != null ? Product.SellingPrice * Quantity : 0;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    #endregion

    #region Events

    public class BazarEvent
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Location { get; set; }
        public DateTime StartedAt { get; set; } = DateTime.Now;
        public DateTime? EndedAt { get; set; }
        public bool IsActive { get; set; } = true;
        public int CreatedBy { get; set; }

        // Runtime statistics
        public decimal TotalRevenue { get; set; }
        public decimal TotalProfit { get; set; }
        public int ItemsSold { get; set; }
        public int TransactionCount { get; set; }
    }

    #endregion

    #region Stock & Audit

    public class StockMovement
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // Sale, Restock, Adjustment, Transfer
        public int Quantity { get; set; }
        public string? Note { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public class AuditLog
    {
        public int Id { get; set; }
        public int? UserId { get; set; }
        public string Action { get; set; } = string.Empty;
        public string? Details { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    #endregion

    #region Reports & Dashboard

    public class TopProduct
    {
        public string Name { get; set; } = string.Empty;
        public int QuantitySold { get; set; }
        public decimal Revenue { get; set; }
    }

    public class DashboardStats
    {
        public decimal TodaySales { get; set; }
        public decimal TodayProfit { get; set; }
        public int TodayTransactions { get; set; }
        public int TodayItemsSold { get; set; }
        public decimal WeekSales { get; set; }
        public decimal MonthSales { get; set; }
        public int LowStockCount { get; set; }
        public int OutOfStockCount { get; set; }
        public List<Product> LowStockProducts { get; set; } = new();
        public List<TopProduct> TopProducts { get; set; } = new();
    }

    #endregion

    #region Import

    public class ImportResult
    {
        public int Imported { get; set; }
        public int Updated { get; set; }
        public int Failed { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    #endregion
}
