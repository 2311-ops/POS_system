using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using FashionPOS.Models;
using FashionPOS.Services;

namespace FashionPOS.Views
{
    public partial class ProductEditDialog : Window
    {
        private readonly Product _product;

        public Product? Product { get; private set; }

        public ProductEditDialog(IEnumerable<Collection> collections, IEnumerable<Category> categories, Product? product = null)
        {
            InitializeComponent();

            Title = product == null ? "Add Product" : "Edit Product";
            var collectionList = collections.ToList();
            var categoryList = categories.ToList();
            CollectionCombo.ItemsSource = collectionList;
            CategoryCombo.ItemsSource = categoryList;

            _product = product != null
                ? new Product
                {
                    Id = product.Id,
                    Name = product.Name,
                    SKU = product.SKU,
                    CollectionId = product.CollectionId,
                    CollectionName = product.CollectionName,
                    CategoryId = product.CategoryId,
                    CategoryName = product.CategoryName,
                    Size = product.Size,
                    Color = product.Color,
                    CostPrice = product.CostPrice,
                    SellingPrice = product.SellingPrice,
                    StockQuantity = product.StockQuantity,
                    LowStockThreshold = product.LowStockThreshold,
                    Barcode = product.Barcode,
                    IsActive = product.IsActive,
                    CreatedAt = product.CreatedAt,
                    UpdatedAt = product.UpdatedAt
                }
                : new Product
                {
                    IsActive = true,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

            NameBox.Text = _product.Name;
            SkuBox.Text = _product.SKU ?? string.Empty;
            CollectionCombo.SelectedValue = _product.CollectionId;
            CategoryCombo.SelectedValue = _product.CategoryId;
            SizeBox.Text = _product.Size ?? string.Empty;
            ColorBox.Text = _product.Color ?? string.Empty;
            CostPriceBox.Text = _product.CostPrice.ToString(CultureInfo.InvariantCulture);
            SellingPriceBox.Text = _product.SellingPrice.ToString(CultureInfo.InvariantCulture);
            StockBox.Text = _product.StockQuantity.ToString(CultureInfo.InvariantCulture);
            ThresholdBox.Text = _product.LowStockThreshold.ToString(CultureInfo.InvariantCulture);
            BarcodeBox.Text = _product.Barcode ?? string.Empty;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NameBox.Text))
            {
                MessageBox.Show("Product name is required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!decimal.TryParse(CostPriceBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var costPrice))
            {
                MessageBox.Show("Enter a valid cost price.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!decimal.TryParse(SellingPriceBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var sellingPrice))
            {
                MessageBox.Show("Enter a valid selling price.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(StockBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var stockQuantity))
            {
                MessageBox.Show("Enter a valid stock quantity.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(ThresholdBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var lowStockThreshold))
            {
                MessageBox.Show("Enter a valid low stock threshold.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _product.Name = NameBox.Text.Trim();
            _product.SKU = string.IsNullOrWhiteSpace(SkuBox.Text) ? null : SkuBox.Text.Trim();
            _product.CollectionId = CollectionCombo.SelectedValue is int collectionId ? collectionId : null;
            _product.CategoryId = CategoryCombo.SelectedValue is int categoryId ? categoryId : null;
            _product.Size = string.IsNullOrWhiteSpace(SizeBox.Text) ? null : SizeBox.Text.Trim();
            _product.Color = string.IsNullOrWhiteSpace(ColorBox.Text) ? null : ColorBox.Text.Trim();
            _product.CostPrice = costPrice;
            _product.SellingPrice = sellingPrice;
            _product.StockQuantity = stockQuantity;
            _product.LowStockThreshold = lowStockThreshold;
            _product.Barcode = string.IsNullOrWhiteSpace(BarcodeBox.Text) ? null : BarcodeBox.Text.Trim();
            _product.UpdatedAt = DateTime.Now;
            Product = _product;

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
