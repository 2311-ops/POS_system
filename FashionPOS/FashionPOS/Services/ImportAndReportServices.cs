using Dapper;
using ClosedXML.Excel;
using iTextSharp.text;
using iTextSharp.text.pdf;
using FashionPOS.Models;
using FashionPOS.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FashionPOS.Services
{
    public class ImportService
    {
        private readonly DatabaseContext _context;
        private readonly ProductService _productService;

        public ImportService(DatabaseContext context, ProductService productService)
        {
            _context = context;
            _productService = productService;
        }

        /// <summary>
        /// Imports products from an Excel file.
        /// </summary>
        public Models.ImportResult ImportFromExcel(string filePath, int userId)
        {
            var result = new Models.ImportResult();

            try
            {
                using (var workbook = new XLWorkbook(filePath))
                {
                    var worksheet = workbook.Worksheet(1);
                    var rows = worksheet.RangeUsed().RowsUsed().Skip(1); // Skip header

                    foreach (var row in rows)
                    {
                        try
                        {
                            var name = row.Cell(1).GetValue<string>()?.Trim();
                            var sku = row.Cell(2).GetValue<string>()?.Trim();
                            var category = row.Cell(3).GetValue<string>()?.Trim();
                            var size = row.Cell(4).GetValue<string>()?.Trim();
                            var color = row.Cell(5).GetValue<string>()?.Trim();
                            var costPriceStr = row.Cell(6).GetValue<string>() ?? "0";
                            var sellingPriceStr = row.Cell(7).GetValue<string>() ?? "0";
                            var stockStr = row.Cell(8).GetValue<string>() ?? "0";
                            var barcode = row.Cell(9).GetValue<string>()?.Trim();

                            if (string.IsNullOrEmpty(name))
                                continue;

                            decimal.TryParse(costPriceStr, out decimal costPrice);
                            decimal.TryParse(sellingPriceStr, out decimal sellingPrice);
                            int.TryParse(stockStr, out int stock);

                            // Auto-create category if needed
                            int? categoryId = null;
                            if (!string.IsNullOrEmpty(category))
                            {
                                using (var conn = _context.CreateConnection())
                                {
                                    var cat = conn.QuerySingleOrDefault<Category>(
                                        "SELECT * FROM Categories WHERE Name = @Name",
                                        new { Name = category }
                                    );

                                    if (cat == null)
                                    {
                                        conn.Execute("INSERT INTO Categories (Name) VALUES (@Name)", new { Name = category });
                                        cat = conn.QuerySingle<Category>(
                                            "SELECT * FROM Categories WHERE Name = @Name",
                                            new { Name = category }
                                        );
                                    }

                                    categoryId = cat.Id;
                                }
                            }

                            // Upsert by SKU
                            var product = new Models.Product
                            {
                                Name = name,
                                SKU = sku,
                                CategoryId = categoryId,
                                Size = size,
                                Color = color,
                                CostPrice = costPrice,
                                SellingPrice = sellingPrice,
                                StockQuantity = stock,
                                Barcode = barcode,
                                IsActive = true
                            };

                            if (!string.IsNullOrEmpty(sku))
                            {
                                using (var conn = _context.CreateConnection())
                                {
                                    var existing = conn.QuerySingleOrDefault<Product>(
                                        "SELECT * FROM Products WHERE SKU = @SKU",
                                        new { SKU = sku }
                                    );

                                    if (existing != null)
                                    {
                                        product.Id = existing.Id;
                                        _productService.Save(product);
                                        result.Updated++;
                                    }
                                    else
                                    {
                                        _productService.Save(product);
                                        result.Imported++;
                                    }
                                }
                            }
                            else
                            {
                                _productService.Save(product);
                                result.Imported++;
                            }
                        }
                        catch (Exception ex)
                        {
                            result.Failed++;
                            result.Errors.Add($"Row error: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Import failed: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Imports products from a CSV file.
        /// </summary>
        public Models.ImportResult ImportFromCsv(string filePath, int userId)
        {
            var result = new Models.ImportResult();

            try
            {
                var lines = File.ReadAllLines(filePath);
                if (lines.Length < 1)
                    return result;

                // Parse header
                var headers = lines[0].Split(',').Select(x => x.Trim().ToLower()).ToList();
                var nameIdx = headers.IndexOf("name");
                var skuIdx = headers.IndexOf("sku");
                var categoryIdx = headers.IndexOf("category");
                var sizeIdx = headers.IndexOf("size");
                var colorIdx = headers.IndexOf("color");
                var costPriceIdx = headers.IndexOf("costprice");
                var sellingPriceIdx = headers.IndexOf("sellingprice");
                var stockIdx = headers.IndexOf("stock");
                var barcodeIdx = headers.IndexOf("barcode");

                for (int i = 1; i < lines.Length; i++)
                {
                    try
                    {
                        var cells = lines[i].Split(',').Select(x => x.Trim()).ToArray();

                        var name = nameIdx >= 0 && nameIdx < cells.Length ? cells[nameIdx] : "";
                        if (string.IsNullOrEmpty(name))
                            continue;

                        var sku = skuIdx >= 0 && skuIdx < cells.Length ? cells[skuIdx] : "";
                        var category = categoryIdx >= 0 && categoryIdx < cells.Length ? cells[categoryIdx] : "";
                        var size = sizeIdx >= 0 && sizeIdx < cells.Length ? cells[sizeIdx] : "";
                        var color = colorIdx >= 0 && colorIdx < cells.Length ? cells[colorIdx] : "";
                        var costPriceStr = costPriceIdx >= 0 && costPriceIdx < cells.Length ? cells[costPriceIdx] : "0";
                        var sellingPriceStr = sellingPriceIdx >= 0 && sellingPriceIdx < cells.Length ? cells[sellingPriceIdx] : "0";
                        var stockStr = stockIdx >= 0 && stockIdx < cells.Length ? cells[stockIdx] : "0";
                        var barcode = barcodeIdx >= 0 && barcodeIdx < cells.Length ? cells[barcodeIdx] : "";

                        decimal.TryParse(costPriceStr, out decimal costPrice);
                        decimal.TryParse(sellingPriceStr, out decimal sellingPrice);
                        int.TryParse(stockStr, out int stock);

                        // Auto-create category if needed
                        int? categoryId = null;
                        if (!string.IsNullOrEmpty(category))
                        {
                            using (var conn = _context.CreateConnection())
                            {
                                var cat = conn.QuerySingleOrDefault<Category>(
                                    "SELECT * FROM Categories WHERE Name = @Name",
                                    new { Name = category }
                                );

                                if (cat == null)
                                {
                                    conn.Execute("INSERT INTO Categories (Name) VALUES (@Name)", new { Name = category });
                                    cat = conn.QuerySingle<Category>(
                                        "SELECT * FROM Categories WHERE Name = @Name",
                                        new { Name = category }
                                    );
                                }

                                categoryId = cat.Id;
                            }
                        }

                        var product = new Models.Product
                        {
                            Name = name,
                            SKU = sku,
                            CategoryId = categoryId,
                            Size = size,
                            Color = color,
                            CostPrice = costPrice,
                            SellingPrice = sellingPrice,
                            StockQuantity = stock,
                            Barcode = barcode,
                            IsActive = true
                        };

                        if (!string.IsNullOrEmpty(sku))
                        {
                            using (var conn = _context.CreateConnection())
                            {
                                var existing = conn.QuerySingleOrDefault<Product>(
                                    "SELECT * FROM Products WHERE SKU = @SKU",
                                    new { SKU = sku }
                                );

                                if (existing != null)
                                {
                                    product.Id = existing.Id;
                                    _productService.Save(product);
                                    result.Updated++;
                                }
                                else
                                {
                                    _productService.Save(product);
                                    result.Imported++;
                                }
                            }
                        }
                        else
                        {
                            _productService.Save(product);
                            result.Imported++;
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Failed++;
                        result.Errors.Add($"Line {i}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Import failed: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Imports products from a Shopify export CSV file.
        /// </summary>
        public Models.ImportResult ImportFromShopifyCsv(string filePath, int userId)
        {
            var result = new Models.ImportResult();
            var lines = ParseCsvFile(filePath);
            if (lines.Length < 2) return result;

            var headers = ParseCsvLine(lines[0]);

            int Col(string name) => Array.FindIndex(headers,
                h => h.Trim().Equals(name, StringComparison.OrdinalIgnoreCase));

            int idxTitle = Col("Title");
            int idxSKU = Col("Variant SKU");
            int idxPrice = Col("Variant Price");
            int idxCost = Col("Cost per item");
            int idxQty = Col("Variant Inventory Qty");
            int idxBarcode = Col("Variant Barcode");
            int idxType = Col("Type");
            int idxOpt1Name = Col("Option1 Name");
            int idxOpt1Val = Col("Option1 Value");
            int idxOpt2Name = Col("Option2 Name");
            int idxOpt2Val = Col("Option2 Value");
            int idxOpt3Name = Col("Option3 Name");
            int idxOpt3Val = Col("Option3 Value");
            int idxColor = Col("Color (product.metafields.shopify.color-pattern)");
            int idxSize = Col("Size (product.metafields.shopify.size)");
            int idxStatus = Col("Status");

            using var conn = _context.CreateConnection();
            var categories = conn.Query<Category>("SELECT * FROM Categories")
                .ToDictionary(c => c.Name.ToLower(), c => c.Id);

            string currentTitle = "";
            string currentType = "";

            for (int i = 1; i < lines.Length; i++)
            {
                try
                {
                    var cols = ParseCsvLine(lines[i]);
                    if (cols.Length < 5) continue;

                    string Get(int idx) => idx >= 0 && idx < cols.Length
                        ? CleanShopifyText(cols[idx]) : "";

                    var title = Get(idxTitle);
                    if (!string.IsNullOrWhiteSpace(title))
                    {
                        currentTitle = title;
                        currentType = Get(idxType);
                    }
                    if (string.IsNullOrWhiteSpace(currentTitle)) continue;

                    var status = Get(idxStatus);
                    if (status.Equals("draft", StringComparison.OrdinalIgnoreCase)) continue;

                    string size = "";
                    string color = "";

                    var opt1Name = Get(idxOpt1Name).ToLower();
                    var opt2Name = Get(idxOpt2Name).ToLower();
                    var opt3Name = Get(idxOpt3Name).ToLower();

                    if (opt1Name.Contains("size")) size = Get(idxOpt1Val);
                    else if (opt2Name.Contains("size")) size = Get(idxOpt2Val);
                    else if (opt3Name.Contains("size")) size = Get(idxOpt3Val);

                    if (opt1Name.Contains("color") || opt1Name.Contains("colour"))
                        color = Get(idxOpt1Val);
                    else if (opt2Name.Contains("color") || opt2Name.Contains("colour"))
                        color = Get(idxOpt2Val);
                    else if (opt3Name.Contains("color") || opt3Name.Contains("colour"))
                        color = Get(idxOpt3Val);

                    if (string.IsNullOrWhiteSpace(size)) size = Get(idxSize);
                    if (string.IsNullOrWhiteSpace(color)) color = Get(idxColor);

                    var variantSuffix = new[] { size, color }
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .ToList();
                    var productName = variantSuffix.Any()
                        ? $"{currentTitle} — {string.Join(" / ", variantSuffix)}"
                        : currentTitle;

                    int? categoryId = null;
                    if (!string.IsNullOrWhiteSpace(currentType))
                    {
                        var catKey = currentType.ToLower();
                        if (!categories.TryGetValue(catKey, out var catId))
                        {
                            catId = conn.ExecuteScalar<int>(
                                "INSERT INTO Categories (Name) VALUES (@name); SELECT last_insert_rowid();",
                                new { name = currentType });
                            categories[catKey] = catId;
                        }
                        categoryId = catId;
                    }

                    decimal.TryParse(Get(idxPrice),
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var price);
                    decimal.TryParse(Get(idxCost),
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var cost);
                    int.TryParse(Get(idxQty), out var qty);

                    var sku = Get(idxSKU);

                    var product = new Product
                    {
                        Name = productName,
                        SKU = string.IsNullOrWhiteSpace(sku) ? null : sku,
                        CategoryId = categoryId,
                        Size = string.IsNullOrWhiteSpace(size) ? null : size,
                        Color = string.IsNullOrWhiteSpace(color) ? null : color,
                        SellingPrice = price,
                        CostPrice = cost,
                        StockQuantity = Math.Max(0, qty),
                        Barcode = Get(idxBarcode),
                        LowStockThreshold = 5,
                        IsActive = true,
                    };

                    Product? existing = null;
                    if (!string.IsNullOrWhiteSpace(sku))
                        existing = conn.QueryFirstOrDefault<Product>(
                            "SELECT Id FROM Products WHERE SKU=@sku", new { sku });
                    else
                        existing = conn.QueryFirstOrDefault<Product>(
                            "SELECT Id FROM Products WHERE Name=@name", new { name = productName });

                    if (existing != null)
                    {
                        product.Id = existing.Id;
                        result.Updated++;
                    }
                    else
                    {
                        result.Imported++;
                    }

                    _productService.Save(product);
                }
                catch (Exception ex)
                {
                    result.Failed++;
                    result.Errors.Add($"Row {i + 1}: {ex.Message}");
                }
            }

            return result;
        }

        private static string[] ParseCsvFile(string filePath)
        {
            var content = File.ReadAllText(filePath, Encoding.UTF8);
            var rows = new List<string>();
            var current = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < content.Length; i++)
            {
                char c = content[i];
                if (c == '"')
                {
                    if (inQuotes && i + 1 < content.Length && content[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                        current.Append(c);
                    }
                }
                else if ((c == '\r' || c == '\n') && !inQuotes)
                {
                    if (c == '\r' && i + 1 < content.Length && content[i + 1] == '\n')
                        i++;

                    rows.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            if (current.Length > 0)
                rows.Add(current.ToString());

            return rows.ToArray();
        }

        private static string CleanShopifyText(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return string.Empty;

            var cleaned = raw.Trim();
            cleaned = cleaned.Trim('"');

            // Strip simple HTML tags and encoded characters.
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, "<[^>]+>", string.Empty);
            cleaned = System.Net.WebUtility.HtmlDecode(cleaned);

            // Remove common Shopify metafield header noise.
            cleaned = cleaned.Replace("product.metafields.shopify.", string.Empty, StringComparison.OrdinalIgnoreCase);
            cleaned = cleaned.Replace("Body (HTML)", string.Empty, StringComparison.OrdinalIgnoreCase);
            cleaned = cleaned.Replace("Title", string.Empty, StringComparison.OrdinalIgnoreCase);

            return cleaned.Trim();
        }

        private static string[] ParseCsvLine(string line)
        {
            var fields = new List<string>();
            bool inQuotes = false;
            var current = new System.Text.StringBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    fields.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            fields.Add(current.ToString());
            return fields.ToArray();
        }

        /// <summary>
        /// Imports products from Shopify store using the ShopifySharp client.
        /// </summary>
        public async Task<Models.ImportResult> ImportFromShopifyAsync(string shopDomain, string accessToken, int userId)
        {
            if (string.IsNullOrWhiteSpace(shopDomain))
                throw new ArgumentException("Shop domain is required.", nameof(shopDomain));
            if (string.IsNullOrWhiteSpace(accessToken))
                throw new ArgumentException("Access token is required.", nameof(accessToken));

            var result = new Models.ImportResult();
            var existingProducts = _productService.GetAll(activeOnly: false)
                .ToDictionary(p => BuildShopifyKey(p.SKU, p.Name, p.Size, p.Color), StringComparer.OrdinalIgnoreCase);

            var shopifyService = new ShopifySharp.ProductService(shopDomain, accessToken);
            var filter = new ShopifySharp.Filters.ProductListFilter { Limit = 250 };
            int? sinceId = null;

            var shopifyProducts = new List<ShopifySharp.Product>();
            while (true)
            {
                filter.SinceId = sinceId;
                var pageResult = await shopifyService.ListAsync(filter);
                var page = pageResult?.Items?.ToList() ?? new List<ShopifySharp.Product>();
                if (page.Count == 0)
                    break;

                shopifyProducts.AddRange(page);
                if (page.Count < 250)
                    break;

                var lastId = page.Max(p => p.Id);
                sinceId = lastId.HasValue ? (int?)Convert.ToInt32(lastId.Value) : null;
            }

            foreach (var shopifyProduct in shopifyProducts)
            {
                if (shopifyProduct == null || shopifyProduct.Variants == null)
                    continue;

                foreach (var variant in shopifyProduct.Variants)
                {
                    if (variant == null || variant.InventoryQuantity <= 0)
                        continue;

                    var sku = variant.SKU?.Trim();
                    var name = string.IsNullOrWhiteSpace(shopifyProduct.Title)
                        ? "Shopify Product"
                        : shopifyProduct.Title.Trim();

                    var size = GetOptionValue(shopifyProduct, variant, "Size");
                    var color = GetOptionValue(shopifyProduct, variant, "Color");

                    decimal sellingPrice = variant.Price ?? 0m;
                    decimal costPrice = 0m;

                    var product = new Product
                    {
                        Name = name,
                        SKU = sku,
                        CategoryId = null,
                        CategoryName = string.IsNullOrWhiteSpace(shopifyProduct.ProductType)
                            ? shopifyProduct.Vendor
                            : shopifyProduct.ProductType,
                        Size = size,
                        Color = color,
                        CostPrice = costPrice,
                        SellingPrice = sellingPrice,
                        StockQuantity = variant.InventoryQuantity.HasValue ? Convert.ToInt32(variant.InventoryQuantity.Value) : 0,
                        LowStockThreshold = 5,
                        Barcode = variant.Barcode,
                        IsActive = true,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };

                    var key = BuildShopifyKey(sku, name, size, color);
                    if (existingProducts.TryGetValue(key, out var existing))
                    {
                        product.Id = existing.Id;
                        product.CreatedAt = existing.CreatedAt;
                        _productService.Save(product);
                        result.Updated++;
                    }
                    else
                    {
                        _productService.Save(product);
                        result.Imported++;
                        existingProducts[key] = product;
                    }
                }
            }

            return result;
        }

        private static string BuildShopifyKey(string? sku, string name, string? size, string? color)
        {
            if (!string.IsNullOrWhiteSpace(sku))
                return sku.Trim().ToLowerInvariant();

            return $"{name.Trim().ToLowerInvariant()}|{size?.Trim().ToLowerInvariant()}|{color?.Trim().ToLowerInvariant()}";
        }

        private static string? GetOptionValue(ShopifySharp.Product product, ShopifySharp.ProductVariant variant, string optionName)
        {
            if (product.Options == null)
                return null;

            var options = product.Options.ToList();
            var option = options
                .FirstOrDefault(o => string.Equals(o.Name, optionName, StringComparison.OrdinalIgnoreCase));
            if (option == null)
                return null;

            switch (options.IndexOf(option))
            {
                case 0: return variant.Option1?.Trim();
                case 1: return variant.Option2?.Trim();
                case 2: return variant.Option3?.Trim();
                default: return null;
            }
        }
    }

    public class InvoiceService
    {
        /// <summary>
        /// Generates an invoice PDF for a sale.
        /// </summary>
        public string GenerateInvoicePdf(Sale sale, string storeName = "Gerber Weeza")
        {
            try
            {
                // Create output directory
                string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string invoicesDir = Path.Combine(documentsPath, "GerberWeeza", "Invoices");
                Directory.CreateDirectory(invoicesDir);

                string filePath = Path.Combine(invoicesDir, $"Invoice_{sale.SaleNumber}.pdf");

                using (var document = new Document(PageSize.A5, 30, 30, 30, 30))
                {
                    using (var writer = PdfWriter.GetInstance(document, new FileStream(filePath, FileMode.Create)))
                    {
                        document.Open();

                        // Header
                        var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16);
                        var subtitleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14);
                        var normalFont = FontFactory.GetFont(FontFactory.HELVETICA, 10);
                        var boldFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10);

                        var storeNamePara = new Paragraph(storeName, titleFont);
                        storeNamePara.Alignment = Element.ALIGN_CENTER;
                        document.Add(storeNamePara);

                        var invoiceTitle = new Paragraph("INVOICE", subtitleFont);
                        invoiceTitle.Alignment = Element.ALIGN_CENTER;
                        document.Add(invoiceTitle);

                        document.Add(new Paragraph(" "));

                        // Invoice details
                        document.Add(new Paragraph($"Invoice #: {sale.SaleNumber}", normalFont));
                        document.Add(new Paragraph($"Date: {sale.CreatedAt:yyyy-MM-dd HH:mm}", normalFont));
                        document.Add(new Paragraph($"Cashier: {sale.CashierName}", normalFont));

                        document.Add(new Paragraph(" "));

                        // Items table
                        var table = new PdfPTable(4) { WidthPercentage = 100 };
                        table.SetWidths(new float[] { 3f, 1f, 1.5f, 1.5f });

                        var headerCell = new PdfPCell(new Phrase("Item", boldFont));
                        headerCell.BackgroundColor = new BaseColor(200, 200, 200);
                        table.AddCell(headerCell);

                        headerCell = new PdfPCell(new Phrase("Qty", boldFont));
                        headerCell.BackgroundColor = new BaseColor(200, 200, 200);
                        table.AddCell(headerCell);

                        headerCell = new PdfPCell(new Phrase("Price", boldFont));
                        headerCell.BackgroundColor = new BaseColor(200, 200, 200);
                        table.AddCell(headerCell);

                        headerCell = new PdfPCell(new Phrase("Total", boldFont));
                        headerCell.BackgroundColor = new BaseColor(200, 200, 200);
                        table.AddCell(headerCell);

                        foreach (var item in sale.Items)
                        {
                            table.AddCell(new PdfPCell(new Phrase($"{item.ProductName}", normalFont)));
                            table.AddCell(new PdfPCell(new Phrase($"{item.Quantity}", normalFont)));
                            table.AddCell(new PdfPCell(new Phrase($"{item.UnitPrice:C}", normalFont)));
                            table.AddCell(new PdfPCell(new Phrase($"{item.TotalPrice:C}", normalFont)));
                        }

                        document.Add(table);

                        document.Add(new Paragraph(" "));

                        // Totals
                        var rightAlign = new Paragraph($"TOTAL: {sale.TotalAmount:C}", boldFont);
                        rightAlign.Alignment = Element.ALIGN_RIGHT;
                        document.Add(rightAlign);

                        var paymentPara = new Paragraph($"Payment: {sale.PaymentMethod}", normalFont);
                        paymentPara.Alignment = Element.ALIGN_RIGHT;
                        document.Add(paymentPara);

                        document.Add(new Paragraph(" "));

                        var thankYou = new Paragraph("Thank you for your purchase!", normalFont);
                        thankYou.Alignment = Element.ALIGN_CENTER;
                        document.Add(thankYou);

                        document.Close();
                    }
                }

                return filePath;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to generate invoice: {ex.Message}", ex);
            }
        }
    }

    public class ReportService
    {
        private readonly DatabaseContext _context;

        public ReportService(DatabaseContext context)
        {
            _context = context;
        }

        public string ExportSalesReportExcel(DateTime fromDate, DateTime toDate)
        {
            try
            {
                string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string reportsDir = Path.Combine(documentsPath, "GerberWeeza", "Reports");
                Directory.CreateDirectory(reportsDir);

                string filePath = Path.Combine(reportsDir, $"Report_{fromDate:yyyy-MM-dd}_to_{toDate:yyyy-MM-dd}.xlsx");

                using var connection = _context.CreateConnection();
                var sales = connection.Query(
                    @"SELECT s.SaleNumber,
                             s.CreatedAt,
                             s.TotalAmount,
                             s.Profit,
                             s.PaymentMethod,
                             u.FullName as CashierName,
                             COALESCE(SUM(si.Quantity), 0) as ItemCount
                      FROM Sales s
                      LEFT JOIN Users u ON s.UserId = u.Id
                      LEFT JOIN SaleItems si ON si.SaleId = s.Id
                      WHERE DATE(s.CreatedAt) >= @FromDate
                        AND DATE(s.CreatedAt) <= @ToDate
                      GROUP BY s.Id, s.SaleNumber, s.CreatedAt, s.TotalAmount, s.Profit, s.PaymentMethod, u.FullName
                      ORDER BY s.CreatedAt DESC",
                    new
                    {
                        FromDate = fromDate.ToString("yyyy-MM-dd"),
                        ToDate = toDate.ToString("yyyy-MM-dd")
                    }
                ).ToList();

                var totalRevenue = sales.Sum(s => s.TotalAmount);
                var totalProfit = sales.Sum(s => s.Profit);
                var totalTransactions = sales.Count;

                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("Daily Sales");

                    // Title
                    worksheet.Cell("A1").Value = "Sales Report";
                    worksheet.Cell("A1").Style.Font.Bold = true;
                    worksheet.Cell("A1").Style.Font.FontSize = 14;
                    worksheet.Cell("A2").Value = $"{fromDate:yyyy-MM-dd} to {toDate:yyyy-MM-dd}";

                    // Summary
                    worksheet.Cell("A3").Value = "Total Sales:";
                    worksheet.Cell("B3").Value = totalRevenue;

                    worksheet.Cell("A4").Value = "Total Profit:";
                    worksheet.Cell("B4").Value = totalProfit;

                    worksheet.Cell("A5").Value = "Transactions:";
                    worksheet.Cell("B5").Value = totalTransactions;

                    // Headers
                    worksheet.Cell("A7").Value = "Sale #";
                    worksheet.Cell("B7").Value = "Time";
                    worksheet.Cell("C7").Value = "Cashier";
                    worksheet.Cell("D7").Value = "Items";
                    worksheet.Cell("E7").Value = "Total";
                    worksheet.Cell("F7").Value = "Payment";

                    // Format headers
                    for (char c = 'A'; c <= 'F'; c++)
                    {
                        worksheet.Cell($"{c}7").Style.Font.Bold = true;
                        worksheet.Cell($"{c}7").Style.Fill.BackgroundColor = XLColor.LightGray;
                    }

                    var row = 8;
                    foreach (var sale in sales)
                    {
                        worksheet.Cell(row, 1).Value = sale.SaleNumber;
                        worksheet.Cell(row, 2).Value = Convert.ToDateTime(sale.CreatedAt).ToString("yyyy-MM-dd HH:mm");
                        worksheet.Cell(row, 3).Value = sale.CashierName;
                        worksheet.Cell(row, 4).Value = sale.ItemCount;
                        worksheet.Cell(row, 5).Value = sale.TotalAmount;
                        worksheet.Cell(row, 6).Value = sale.PaymentMethod;
                        row++;
                    }

                    worksheet.Columns().AdjustToContents();
                    workbook.SaveAs(filePath);
                }

                return filePath;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to export report: {ex.Message}", ex);
            }
        }
    }
}
