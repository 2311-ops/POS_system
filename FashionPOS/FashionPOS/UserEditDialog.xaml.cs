using System;
using System.Linq;
using System.Windows;
using FashionPOS.Models;

namespace FashionPOS
{
    public partial class UserEditDialog : Window
    {
        private readonly User _workingUser;

        public User? User { get; private set; }
        public string Password { get; private set; } = string.Empty;

        public UserEditDialog(User? user = null)
        {
            InitializeComponent();

            RoleBox.ItemsSource = Enum.GetValues(typeof(UserRole)).Cast<UserRole>().ToList();
            Title = user == null ? "Create User" : "Edit User";

            DialogHeaderText.Text = user == null ? "CREATE OPERATOR" : "EDIT OPERATOR";
            SaveButton.Content = user == null ? "CREATE USER" : "SAVE CHANGES";

            _workingUser = user != null
                ? new User
                {
                    Id = user.Id,
                    Username = user.Username,
                    PasswordHash = user.PasswordHash,
                    FullName = user.FullName,
                    Role = user.Role,
                    IsActive = user.IsActive,
                    CreatedAt = user.CreatedAt,
                    LastLogin = user.LastLogin
                }
                : new User
                {
                    IsActive = true,
                    Role = UserRole.Cashier,
                    CreatedAt = DateTime.Now
                };

            FullNameBox.Text = _workingUser.FullName;
            UsernameBox.Text = _workingUser.Username;
            RoleBox.SelectedItem = _workingUser.Role;
            IsActiveCheck.IsChecked = _workingUser.IsActive;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(FullNameBox.Text))
            {
                MessageBox.Show("Full name is required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(UsernameBox.Text))
            {
                MessageBox.Show("Username is required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Password = PasswordInput.Password.Trim();
            if (_workingUser.Id == 0 && string.IsNullOrWhiteSpace(Password))
            {
                MessageBox.Show("Password is required for new users.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _workingUser.FullName = FullNameBox.Text.Trim();
            _workingUser.Username = UsernameBox.Text.Trim();
            _workingUser.Role = RoleBox.SelectedItem is UserRole role ? role : UserRole.Cashier;
            _workingUser.IsActive = IsActiveCheck.IsChecked ?? true;

            User = _workingUser;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
