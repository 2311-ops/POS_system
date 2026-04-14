using System;
using System.Threading.Tasks;
using System.Windows;

namespace FashionPOS.Views
{
    public partial class ShopifyImportDialog : Window
    {
        public Func<string, string, Task>? OnImport { get; set; }

        public ShopifyImportDialog()
        {
            InitializeComponent();
        }

        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            var domain = ShopDomainBox.Text.Trim()
                .Replace("https://", "").Replace("http://", "").TrimEnd('/');
            var token = AccessTokenBox.Password.Trim();

            if (string.IsNullOrWhiteSpace(domain) || string.IsNullOrWhiteSpace(token))
            {
                MessageBox.Show(
                    "Please enter both your shop domain and access token.",
                    "Missing Fields", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ConnectButton.IsEnabled = false;
            ConnectButton.Content = "Importing...";
            StatusPanel.Visibility = Visibility.Visible;
            StatusText.Text = "Connecting to Shopify...";

            try
            {
                if (OnImport != null)
                    await OnImport(domain, token);

                StatusText.Text = "✓ Import complete!";
                ImportProgress.IsIndeterminate = false;
                ImportProgress.Value = 100;

                await Task.Delay(800);
                DialogResult = true;
            }
            catch (Exception ex)
            {
                StatusPanel.Visibility = Visibility.Collapsed;
                ConnectButton.IsEnabled = true;
                ConnectButton.Content = "Connect & Import";

                MessageBox.Show(
                    $"Shopify import failed:\n\n{ex.Message}\n\n" +
                    "Check your domain and token are correct.",
                    "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
