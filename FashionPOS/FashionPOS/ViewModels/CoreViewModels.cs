using System;
using System.Windows;
using FashionPOS.Helpers;
using FashionPOS.Models;
using FashionPOS.Services;

namespace FashionPOS.ViewModels
{
    public class LoginViewModel : BaseViewModel
    {
        private readonly AuthService _authService;
        private string _username = string.Empty;
        private string _errorMessage = string.Empty;
        private bool _isLoading;

        public string Username
        {
            get => _username;
            set
            {
                if (SetProperty(ref _username, value) && !string.IsNullOrWhiteSpace(ErrorMessage))
                    ErrorMessage = string.Empty;
            }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public RelayCommand LoginCommand { get; }

        public Action<User>? OnLoginSuccess { get; set; }

        public LoginViewModel(AuthService authService)
        {
            _authService = authService;
            LoginCommand = new RelayCommand(ExecuteLogin);
        }

        private void ExecuteLogin(object? parameter)
        {
            IsLoading = true;
            ErrorMessage = string.Empty;

            try
            {
                if (string.IsNullOrWhiteSpace(Username))
                {
                    ErrorMessage = "Username is required";
                    IsLoading = false;
                    return;
                }

                var password = parameter as string;
                if (string.IsNullOrEmpty(password))
                {
                    ErrorMessage = "Password is required";
                    IsLoading = false;
                    return;
                }

                var user = _authService.Login(Username, password);
                if (user != null)
                {
                    OnLoginSuccess?.Invoke(user);
                }
                else
                {
                    ErrorMessage = "Invalid username or password";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Critical Error: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    public class MainViewModel : BaseViewModel
    {
        private readonly AuthService _authService;
        private BaseViewModel _currentView = null!;
        private string _currentSection = "Dashboard";
        private User? _currentUser;

        public BaseViewModel CurrentView
        {
            get => _currentView;
            set => SetProperty(ref _currentView, value);
        }

        public string CurrentSection
        {
            get => _currentSection;
            set => SetProperty(ref _currentSection, value);
        }

        public User? CurrentUser
        {
            get => _currentUser;
            set
            {
                if (SetProperty(ref _currentUser, value))
                {
                    OnPropertyChanged(nameof(CanAccessDashboard));
                    OnPropertyChanged(nameof(CanManageInventory));
                    OnPropertyChanged(nameof(CanViewReports));
                    OnPropertyChanged(nameof(CanManageUsers));
                }
            }
        }

        public bool CanAccessDashboard => CurrentUser?.CanAccessDashboard ?? false;
        public bool CanManageInventory => CurrentUser?.CanManageInventory ?? false;
        public bool CanViewReports => CurrentUser?.CanViewReports ?? false;
        public bool CanManageUsers => CurrentUser?.CanManageUsers ?? false;

        public RelayCommand NavigateCommand { get; }
        public RelayCommand LogoutCommand { get; }

        public Action? OnLogout { get; set; }

        public DashboardViewModel? DashboardViewModel { get; set; }
        public POSViewModel POSViewModel { get; set; } = null!;
        public InventoryViewModel? InventoryViewModel { get; set; }
        public ReportsViewModel? ReportsViewModel { get; set; }
        public UsersViewModel? UsersViewModel { get; set; }
        public EventViewModel EventViewModel { get; set; } = null!;

        public MainViewModel(AuthService authService)
        {
            _authService = authService;
            NavigateCommand = new RelayCommand(ExecuteNavigate);
            LogoutCommand = new RelayCommand(ExecuteLogout);
        }

        public void Initialize(User user)
        {
            CurrentUser = user;
            var startSection = CanAccessDashboard ? "Dashboard" : "POS";
            ExecuteNavigate(startSection);
        }

        private void ExecuteNavigate(object? parameter)
        {
            if (parameter is string section)
            {
                if (section == "Dashboard" && !CanAccessDashboard)
                    section = "POS";
                if (section == "Inventory" && !CanManageInventory)
                    section = "POS";
                if (section == "Reports" && !CanViewReports)
                    section = "POS";
                if (section == "Users" && !CanManageUsers)
                    section = "POS";

                CurrentSection = section;

                var viewModel = section switch
                {
                    "Dashboard" => DashboardViewModel as BaseViewModel,
                    "POS" => POSViewModel,
                    "Inventory" => InventoryViewModel as BaseViewModel,
                    "Reports" => ReportsViewModel as BaseViewModel,
                    "Users" => UsersViewModel as BaseViewModel,
                    "Event" => EventViewModel,
                    _ => POSViewModel
                };

                CurrentView = viewModel ?? POSViewModel;

                // Refresh data if needed
                if (CurrentView is DashboardViewModel dashVM)
                    dashVM.Refresh();
                else if (CurrentView is POSViewModel posVM)
                    posVM.LoadProducts();
                else if (CurrentView is InventoryViewModel invVM)
                    invVM.LoadProducts();
                else if (CurrentView is ReportsViewModel repVM)
                    repVM.LoadReport();
                else if (CurrentView is UsersViewModel usrVM)
                    usrVM.LoadUsers();
                else if (CurrentView is EventViewModel evtVM)
                    evtVM.Refresh();
            }
        }

        private void ExecuteLogout(object? parameter)
        {
            _authService.Logout();
            OnLogout?.Invoke();
        }
    }
}
