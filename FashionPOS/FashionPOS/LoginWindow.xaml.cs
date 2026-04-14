using System.Windows;
using System.Windows.Input;
using FashionPOS.ViewModels;

namespace FashionPOS
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
        }

        private void SignInButton_Click(object sender, RoutedEventArgs e)
        {
            var viewModel = DataContext as LoginViewModel;
            if (viewModel?.LoginCommand != null)
            {
                // Pass the password from PasswordBox to the command
                viewModel.LoginCommand.Execute(PasswordInput.Password);
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SignInButton_Click(sender, e);
                e.Handled = true;
            }
        }
    }
}
