using System.IO;
using System.Linq;
using System.Windows;
using System.Data;
using Microsoft.Win32;
using Dapper;
using FashionPOS.Data;
using FashionPOS.Models;
using FashionPOS.Services;
using FashionPOS.ViewModels;
using FashionPOS.Views;

namespace FashionPOS
{
    /// <summary>
    /// Dapper custom type handler for UserRole enum stored as TEXT in SQLite.
    /// </summary>
    public class UserRoleTypeHandler : SqlMapper.TypeHandler<UserRole>
    {
        public override void SetValue(IDbDataParameter parameter, UserRole value)
        {
            parameter.Value = value.ToString();
        }

        public override UserRole Parse(object value)
        {
            if (value is string str && Enum.TryParse<UserRole>(str, true, out var result))
                return result;
            return UserRole.Cashier;
        }
    }

    public partial class App : Application
    {
        private DatabaseContext? _dbContext;
        private AuthService? _authService;
        private ProductService? _productService;
        private SaleService? _saleService;
        private UserService? _userService;
        private EventService? _eventService;
        private ImportService? _importService;
        private InvoiceService? _invoiceService;
        private ReportService? _reportService;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Register Dapper type handler for UserRole enum
            SqlMapper.AddTypeHandler(new UserRoleTypeHandler());

            try
            {
                _dbContext = new DatabaseContext();
                var initializer = new DatabaseInitializer(_dbContext);
                initializer.Initialize();

                _authService = new AuthService(_dbContext);
                _productService = new ProductService(_dbContext);
                _userService = new UserService(_dbContext);
                _saleService = new SaleService(_dbContext, _productService, _authService);
                _eventService = new EventService(_dbContext);
                _importService = new ImportService(_dbContext, _productService);
                _invoiceService = new InvoiceService();
                _reportService = new ReportService(_dbContext);

                ShutdownMode = ShutdownMode.OnLastWindowClose;
                ShowLogin();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Startup failed: {ex.Message}\n\n{ex.StackTrace}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        private void ShowLogin()
        {
            var loginVM = new LoginViewModel(_authService!);
            loginVM.OnLoginSuccess += user =>
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        ShowMain(user);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Could not open the main window: {ex.Message}", "Login Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }));
            };

            var loginWindow = new LoginWindow { DataContext = loginVM };
            loginWindow.Show();
        }

        private void ShowMain(User user)
        {
            DashboardViewModel? dashboardVM = null;
            InventoryViewModel? inventoryVM = null;
            ReportsViewModel? reportsVM = null;
            UsersViewModel? usersVM = null;

            var posVM = new POSViewModel(_productService!, _saleService!, _invoiceService!, _eventService!, _authService!);
            var eventVM = new EventViewModel(_eventService!, _authService!);

            if (user.CanAccessDashboard)
                dashboardVM = new DashboardViewModel(_saleService!);

            if (user.CanManageInventory)
                inventoryVM = new InventoryViewModel(_productService!, _importService!, _authService!);

            if (user.CanViewReports)
                reportsVM = new ReportsViewModel(_saleService!, _reportService!);

            if (user.CanManageUsers)
                usersVM = new UsersViewModel(_userService!);

            posVM.OnSaleCompleted = () =>
            {
                eventVM.Refresh();
                dashboardVM?.Refresh();
            };

            var mainVM = new MainViewModel(_authService!)
            {
                DashboardViewModel = dashboardVM,
                POSViewModel = posVM,
                InventoryViewModel = inventoryVM,
                ReportsViewModel = reportsVM,
                UsersViewModel = usersVM,
                EventViewModel = eventVM
            };

            mainVM.Initialize(user);

            var mainWindow = new MainWindow { DataContext = mainVM };

            if (inventoryVM != null)
            {
                inventoryVM.ShowProductDialog = product =>
                {
                    var dialog = new ProductEditDialog(
                        _productService!.GetCollections(),
                        _productService!.GetCategories(),
                        product)
                    {
                        Owner = mainWindow
                    };

                    if (dialog.ShowDialog() == true && dialog.Product != null)
                    {
                        inventoryVM.SaveProduct(dialog.Product);
                    }
                };

                inventoryVM.ShowImportDialog = () =>
                {
                    var dialog = new OpenFileDialog
                    {
                        Title = "Import Products",
                        Filter = "All Supported|*.xlsx;*.xls;*.csv|Excel|*.xlsx;*.xls|CSV|*.csv"
                    };

                    if (dialog.ShowDialog(mainWindow) == true)
                    {
                        var path = dialog.FileName;
                        var extension = Path.GetExtension(path).ToLowerInvariant();

                        if (extension == ".csv")
                        {
                            var firstLine = File.ReadLines(path).FirstOrDefault() ?? "";
                            bool isShopify = firstLine.Contains("Variant Inventory Qty")
                                            || firstLine.Contains("Variant Price")
                                            || firstLine.Contains("Cost per item");

                            if (isShopify)
                            {
                                var result = _importService!.ImportFromShopifyCsv(path, _authService!.CurrentUser!.Id);
                                inventoryVM.LoadProducts();
                                MessageBox.Show(
                                    $"✓ Shopify CSV import complete!\n\n" +
                                    $"  Added:    {result.Imported}\n" +
                                    $"  Updated:  {result.Updated}\n" +
                                    $"  Failed:   {result.Failed}" +
                                    (result.Errors.Any()
                                        ? $"\n\nFirst error:\n{result.Errors.First()}"
                                        : ""),
                                    "Import Done", MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                            else
                            {
                                var result = _importService!.ImportFromCsv(path, _authService!.CurrentUser!.Id);
                                inventoryVM.LoadProducts();
                                MessageBox.Show(
                                    $"CSV import: {result.Imported} added, {result.Updated} updated, {result.Failed} failed.",
                                    "Done", MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                        }
                        else
                        {
                            inventoryVM.DoImportExcel(path);
                        }
                    }
                };

                inventoryVM.ShowShopifyDialog = () =>
                {
                    var dialog = new ShopifyImportDialog
                    {
                        Owner = mainWindow
                    };

                    dialog.OnImport = async (domain, token) =>
                    {
                        var result = await _importService!.ImportFromShopifyAsync(domain, token,
                            _authService!.CurrentUser!.Id);
                        inventoryVM.LoadProducts();

                        if (result.Errors.Any())
                        {
                            MessageBox.Show(
                                $"Completed with {result.Failed} error(s):\n\n" +
                                string.Join("\n", result.Errors.Take(5)),
                                "Import Warnings", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }

                        MessageBox.Show(
                            $"✓ Shopify Sync Complete!\n\n" +
                            $"  Added:    {result.Imported}\n" +
                            $"  Updated:  {result.Updated}\n" +
                            $"  Failed:   {result.Failed}",
                            "Done", MessageBoxButton.OK, MessageBoxImage.Information);
                    };

                    dialog.ShowDialog();
                };
            }

            if (usersVM != null)
            {
                usersVM.ShowUserDialog = existingUser =>
                {
                    var dialog = new UserEditDialog(existingUser)
                    {
                        Owner = mainWindow
                    };

                    if (dialog.ShowDialog() == true && dialog.User != null)
                    {
                        usersVM.SaveUser(dialog.User, dialog.Password);
                    }
                };
            }

            mainVM.OnLogout += () =>
            {
                ShowLogin();
                foreach (var window in Windows.OfType<MainWindow>().ToList())
                {
                    window.Close();
                }
            };

            MainWindow = mainWindow;
            mainWindow.Show();

            foreach (var window in Windows.OfType<LoginWindow>().ToList())
            {
                window.Close();
            }
        }
    }
}
