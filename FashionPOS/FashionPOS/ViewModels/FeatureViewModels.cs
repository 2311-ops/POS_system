using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using FashionPOS.Helpers;
using FashionPOS.Models;
using FashionPOS.Services;

namespace FashionPOS.ViewModels
{
    public class POSViewModel : BaseViewModel
    {
        private readonly ProductService _productService;
        private readonly SaleService _saleService;
        private readonly InvoiceService _invoiceService;
        private readonly EventService _eventService;
        private readonly AuthService _authService;

        private ObservableCollection<Product> _products = new();
        private ObservableCollection<CartItem> _cart = new();
        private string _searchQuery = string.Empty;
        private string _statusMessage = string.Empty;
        private string _paymentMethod = "Cash";
        private bool _isProcessing;

        public ObservableCollection<Product> Products
        {
            get => _products;
            set => SetProperty(ref _products, value);
        }

        public ObservableCollection<CartItem> Cart
        {
            get => _cart;
            set => SetProperty(ref _cart, value);
        }

        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                if (SetProperty(ref _searchQuery, value))
                    LoadProducts();
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public string PaymentMethod
        {
            get => _paymentMethod;
            set => SetProperty(ref _paymentMethod, value);
        }

        public bool IsProcessing
        {
            get => _isProcessing;
            set => SetProperty(ref _isProcessing, value);
        }

        public decimal CartTotal => Cart.Sum(x => x.TotalPrice);
        public int CartItemCount => Cart.Sum(x => x.Quantity);
        public bool CartHasItems => Cart.Count > 0;

        public RelayCommand AddToCartCommand { get; }
        public RelayCommand RemoveFromCartCommand { get; }
        public RelayCommand IncrementQtyCommand { get; }
        public RelayCommand DecrementQtyCommand { get; }
        public RelayCommand ClearCartCommand { get; }
        public RelayCommand CompleteSaleCommand { get; }

        public POSViewModel(ProductService productService, SaleService saleService,
                           InvoiceService invoiceService, EventService eventService, AuthService authService)
        {
            _productService = productService;
            _saleService = saleService;
            _invoiceService = invoiceService;
            _eventService = eventService;
            _authService = authService;

            AddToCartCommand = new RelayCommand(ExecuteAddToCart);
            RemoveFromCartCommand = new RelayCommand(ExecuteRemoveFromCart);
            IncrementQtyCommand = new RelayCommand(ExecuteIncrementQty);
            DecrementQtyCommand = new RelayCommand(ExecuteDecrementQty);
            ClearCartCommand = new RelayCommand(_ => ExecuteClearCart());
            CompleteSaleCommand = new RelayCommand(_ => ExecuteCompleteSale());

            LoadProducts();
        }

        public void QuickAddFromSearch()
        {
            if (Products.Count == 0)
            {
                StatusMessage = "No matching product found";
                return;
            }

            var exactBarcodeMatch = Products.FirstOrDefault(p =>
                !string.IsNullOrWhiteSpace(p.Barcode) &&
                string.Equals(p.Barcode, SearchQuery?.Trim(), StringComparison.OrdinalIgnoreCase));

            ExecuteAddToCart(exactBarcodeMatch ?? Products[0]);
        }

        public void LoadProducts()
        {
            try
            {
                var products = string.IsNullOrWhiteSpace(SearchQuery)
                    ? _productService.GetAll()
                    : _productService.Search(SearchQuery);

                Products = new ObservableCollection<Product>(products);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading products: {ex.Message}";
            }
        }

        private void ExecuteAddToCart(object? parameter)
        {
            if (parameter is Product product)
            {
                if (product.IsOutOfStock)
                {
                    StatusMessage = $"{product.Name} is out of stock";
                    return;
                }

                var existing = Cart.FirstOrDefault(x => x.Product?.Id == product.Id);
                if (existing != null)
                {
                    if (existing.Quantity >= product.StockQuantity)
                    {
                        StatusMessage = $"Cannot exceed available stock ({product.StockQuantity})";
                        return;
                    }
                    existing.Quantity++;
                }
                else
                {
                    Cart.Add(new CartItem { Product = product, Quantity = 1 });
                }

                StatusMessage = $"Added {product.Name} to cart";
                OnPropertyChanged(nameof(CartTotal));
                OnPropertyChanged(nameof(CartItemCount));
                OnPropertyChanged(nameof(CartHasItems));
            }
        }

        private void ExecuteRemoveFromCart(object? parameter)
        {
            if (parameter is CartItem item)
            {
                Cart.Remove(item);
                StatusMessage = $"Removed {item.Product?.Name} from cart";
                OnPropertyChanged(nameof(CartTotal));
                OnPropertyChanged(nameof(CartItemCount));
                OnPropertyChanged(nameof(CartHasItems));
            }
        }

        private void ExecuteIncrementQty(object? parameter)
        {
            if (parameter is CartItem item && item.Product != null)
            {
                if (item.Quantity < item.Product.StockQuantity)
                {
                    item.Quantity++;
                    OnPropertyChanged(nameof(CartTotal));
                    OnPropertyChanged(nameof(CartItemCount));
                }
                else
                {
                    StatusMessage = "Cannot exceed available stock";
                }
            }
        }

        private void ExecuteDecrementQty(object? parameter)
        {
            if (parameter is CartItem item)
            {
                if (item.Quantity > 1)
                {
                    item.Quantity--;
                }
                else
                {
                    Cart.Remove(item);
                }
                OnPropertyChanged(nameof(CartTotal));
                OnPropertyChanged(nameof(CartItemCount));
                OnPropertyChanged(nameof(CartHasItems));
            }
        }

        private void ExecuteClearCart()
        {
            Cart.Clear();
            StatusMessage = "Cart cleared";
            OnPropertyChanged(nameof(CartTotal));
            OnPropertyChanged(nameof(CartItemCount));
            OnPropertyChanged(nameof(CartHasItems));
        }

        private void ExecuteCompleteSale()
        {
            if (!CartHasItems)
            {
                StatusMessage = "Cart is empty";
                return;
            }

            IsProcessing = true;
            try
            {
                var currentUser = _authService.CurrentUser;
                if (currentUser == null)
                {
                    StatusMessage = "User session lost";
                    return;
                }

                var cartList = Cart.ToList();
                int? eventId = _eventService.GetActiveEvent()?.Id;

                var sale = _saleService.CompleteSale(cartList, currentUser.Id, PaymentMethod, eventId);

                StatusMessage = $"Sale #{sale.SaleNumber} completed. Total: {sale.TotalAmount:C}";

                try
                {
                    var invoicePath = _invoiceService.GenerateInvoicePdf(sale);
                }
                catch { }

                ExecuteClearCart();
                LoadProducts();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error completing sale: {ex.Message}";
            }
            finally
            {
                IsProcessing = false;
            }
        }
    }

    public class InventoryViewModel : BaseViewModel
    {
        private readonly ProductService _productService;
        private readonly ImportService _importService;
        private readonly AuthService _authService;

        private ObservableCollection<Product> _products = new();
        private ObservableCollection<Product> _allProducts = new();
        private string _searchQuery = string.Empty;
        private string _statusMessage = string.Empty;
        private bool _isLoading;

        public ObservableCollection<Product> Products
        {
            get => _products;
            set => SetProperty(ref _products, value);
        }

        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                if (SetProperty(ref _searchQuery, value))
                    FilterProducts();
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public RelayCommand AddProductCommand { get; }
        public RelayCommand EditProductCommand { get; }
        public RelayCommand DeleteProductCommand { get; }
        public RelayCommand ImportExcelCommand { get; }
        public RelayCommand ImportCsvCommand { get; }
        public RelayCommand ImportShopifyCommand { get; }
        public RelayCommand RefreshCommand { get; }

        public Action<Product?>? ShowProductDialog { get; set; }
        public Action? ShowImportDialog { get; set; }
        public Action? ShowShopifyDialog { get; set; }

        public InventoryViewModel(ProductService productService, ImportService importService, AuthService authService)
        {
            _productService = productService;
            _importService = importService;
            _authService = authService;

            AddProductCommand = new RelayCommand(_ => ShowProductDialog?.Invoke(null));
            EditProductCommand = new RelayCommand(p => ShowProductDialog?.Invoke(p as Product));
            DeleteProductCommand = new RelayCommand(DeleteProduct);
            ImportExcelCommand = new RelayCommand(_ => ShowImportDialog?.Invoke());
            ImportCsvCommand = new RelayCommand(_ => ShowImportDialog?.Invoke());
            ImportShopifyCommand = new RelayCommand(_ => ShowShopifyDialog?.Invoke());
            RefreshCommand = new RelayCommand(_ => LoadProducts());

            LoadProducts();
        }

        public void LoadProducts()
        {
            IsLoading = true;
            try
            {
                _allProducts = new ObservableCollection<Product>(_productService.GetAll());
                FilterProducts();
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void FilterProducts()
        {
            if (string.IsNullOrWhiteSpace(SearchQuery))
            {
                Products = _allProducts;
            }
            else
            {
                var filtered = _allProducts
                    .Where(p => p.Name.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                                p.SKU?.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) == true)
                    .ToList();
                Products = new ObservableCollection<Product>(filtered);
            }
        }

        public void SaveProduct(Product product)
        {
            try
            {
                _productService.Save(product);
                StatusMessage = "Product saved successfully";
                LoadProducts();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error saving product: {ex.Message}";
            }
        }

        private void DeleteProduct(object? parameter)
        {
            if (parameter is Product product)
            {
                try
                {
                    _productService.Delete(product.Id);
                    StatusMessage = "Product deleted successfully";
                    LoadProducts();
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Error deleting product: {ex.Message}";
                }
            }
        }

        public void DoImportExcel(string filePath)
        {
            IsLoading = true;
            try
            {
                var user = _authService.CurrentUser;
                if (user == null) return;

                var result = _importService.ImportFromExcel(filePath, user.Id);
                StatusMessage = $"Import complete: {result.Imported} imported, {result.Updated} updated, {result.Failed} failed";
                LoadProducts();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Import failed: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        public void DoImportCsv(string filePath)
        {
            IsLoading = true;
            try
            {
                var user = _authService.CurrentUser;
                if (user == null) return;

                var result = _importService.ImportFromCsv(filePath, user.Id);
                StatusMessage = $"Import complete: {result.Imported} imported, {result.Updated} updated, {result.Failed} failed";
                LoadProducts();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Import failed: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task DoImportShopifyAsync(string shopDomain, string accessToken)
        {
            IsLoading = true;
            try
            {
                var user = _authService.CurrentUser;
                if (user == null) return;

                var result = await _importService.ImportFromShopifyAsync(shopDomain, accessToken, user.Id);
                StatusMessage = result.Errors.Any()
                    ? string.Join(" | ", result.Errors.Take(3))
                    : $"Shopify import complete: {result.Imported} imported, {result.Updated} updated";
                LoadProducts();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Shopify import failed: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    public class DashboardViewModel : BaseViewModel
    {
        private readonly SaleService _saleService;
        private DashboardStats _stats = new();
        private bool _isLoading;

        public DashboardStats Stats
        {
            get => _stats;
            set => SetProperty(ref _stats, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public RelayCommand RefreshCommand { get; }

        public DashboardViewModel(SaleService saleService)
        {
            _saleService = saleService;
            RefreshCommand = new RelayCommand(_ => Refresh());
        }

        public void Refresh()
        {
            IsLoading = true;
            try
            {
                Stats = _saleService.GetDashboardStats();
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    public class ReportsViewModel : BaseViewModel
    {
        private readonly SaleService _saleService;
        private readonly ReportService _reportService;

        private DateTime _fromDate = DateTime.Now.Date;
        private DateTime _toDate = DateTime.Now.Date;
        private ObservableCollection<Sale> _sales = new();
        private string _statusMessage = string.Empty;
        private bool _isLoading;

        public DateTime FromDate
        {
            get => _fromDate;
            set => SetProperty(ref _fromDate, value);
        }

        public DateTime ToDate
        {
            get => _toDate;
            set => SetProperty(ref _toDate, value);
        }

        public ObservableCollection<Sale> Sales
        {
            get => _sales;
            set => SetProperty(ref _sales, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public decimal TotalRevenue => Sales.Sum(x => x.TotalAmount);
        public decimal TotalProfit => Sales.Sum(x => x.Profit);
        public int TotalTransactions => Sales.Count;

        public RelayCommand LoadReportCommand { get; }
        public RelayCommand ExportExcelCommand { get; }

        public ReportsViewModel(SaleService saleService, ReportService reportService)
        {
            _saleService = saleService;
            _reportService = reportService;

            LoadReportCommand = new RelayCommand(_ => LoadReport());
            ExportExcelCommand = new RelayCommand(_ => ExecuteExportExcel());

            LoadReport();
        }

        public void LoadReport()
        {
            IsLoading = true;
            try
            {
                var sales = _saleService.GetSales(FromDate, ToDate);
                Sales = new ObservableCollection<Sale>(sales);
                OnPropertyChanged(nameof(TotalRevenue));
                OnPropertyChanged(nameof(TotalProfit));
                OnPropertyChanged(nameof(TotalTransactions));
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ExecuteExportExcel()
        {
            try
            {
                var path = _reportService.ExportSalesReportExcel(FromDate, ToDate);
                StatusMessage = $"Report exported to {path}";
                
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{path}\"",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                StatusMessage = $"Export failed: {ex.Message}";
            }
        }
    }

    public class UsersViewModel : BaseViewModel
    {
        private readonly UserService _userService;

        private ObservableCollection<User> _users = new();
        private string _statusMessage = string.Empty;
        private bool _isLoading;

        public ObservableCollection<User> Users
        {
            get => _users;
            set => SetProperty(ref _users, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public RelayCommand AddUserCommand { get; }
        public RelayCommand EditUserCommand { get; }
        public RelayCommand DeleteUserCommand { get; }

        public Action<User?>? ShowUserDialog { get; set; }

        public UsersViewModel(UserService userService)
        {
            _userService = userService;

            AddUserCommand = new RelayCommand(_ => ShowUserDialog?.Invoke(null));
            EditUserCommand = new RelayCommand(u => ShowUserDialog?.Invoke(u as User));
            DeleteUserCommand = new RelayCommand(DeleteUser);

            LoadUsers();
        }

        public void LoadUsers()
        {
            IsLoading = true;
            try
            {
                var users = _userService.GetAll();
                Users = new ObservableCollection<User>(users);
            }
            finally
            {
                IsLoading = false;
            }
        }

        public void SaveUser(User user, string password)
        {
            try
            {
                _userService.Save(user, password);
                StatusMessage = "User saved successfully";
                LoadUsers();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error saving user: {ex.Message}";
            }
        }

        private void DeleteUser(object? parameter)
        {
            if (parameter is User user)
            {
                try
                {
                    _userService.Delete(user.Id);
                    StatusMessage = "User deleted successfully";
                    LoadUsers();
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Error deleting user: {ex.Message}";
                }
            }
        }
    }

    public class EventViewModel : BaseViewModel
    {
        private readonly EventService _eventService;
        private readonly AuthService _authService;

        private BazarEvent? _activeEvent;
        private bool _hasActiveEvent;
        private string _eventName = string.Empty;
        private string _eventLocation = string.Empty;
        private string _statusMessage = string.Empty;

        public BazarEvent? ActiveEvent
        {
            get => _activeEvent;
            set => SetProperty(ref _activeEvent, value);
        }

        public bool HasActiveEvent
        {
            get => _hasActiveEvent;
            set => SetProperty(ref _hasActiveEvent, value);
        }

        public string EventName
        {
            get => _eventName;
            set => SetProperty(ref _eventName, value);
        }

        public string EventLocation
        {
            get => _eventLocation;
            set => SetProperty(ref _eventLocation, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public RelayCommand StartEventCommand { get; }
        public RelayCommand EndEventCommand { get; }
        public RelayCommand RefreshCommand { get; }

        public EventViewModel(EventService eventService, AuthService authService)
        {
            _eventService = eventService;
            _authService = authService;

            StartEventCommand = new RelayCommand(
                _ => ExecuteStartEvent(),
                _ => !HasActiveEvent && !string.IsNullOrWhiteSpace(EventName)
            );
            EndEventCommand = new RelayCommand(_ => ExecuteEndEvent());
            RefreshCommand = new RelayCommand(_ => Refresh());

            Refresh();
        }

        public void Refresh()
        {
            try
            {
                var activeEvent = _eventService.GetActiveEvent();
                if (activeEvent != null)
                {
                    ActiveEvent = _eventService.GetEventStats(activeEvent.Id);
                    HasActiveEvent = true;
                }
                else
                {
                    ActiveEvent = null;
                    HasActiveEvent = false;
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error refreshing event: {ex.Message}";
            }
        }

        private void ExecuteStartEvent()
        {
            try
            {
                var user = _authService.CurrentUser;
                if (user == null) return;

                _eventService.StartEvent(EventName, EventLocation, user.Id);
                StatusMessage = "Event started";
                EventName = string.Empty;
                EventLocation = string.Empty;
                Refresh();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error starting event: {ex.Message}";
            }
        }

        private void ExecuteEndEvent()
        {
            if (ActiveEvent == null) return;

            try
            {
                _eventService.EndEvent(ActiveEvent.Id);
                StatusMessage = "Event ended";
                Refresh();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error ending event: {ex.Message}";
            }
        }
    }
}
