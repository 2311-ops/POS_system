using System.Windows.Controls;
using System.Windows.Input;
using FashionPOS.ViewModels;

namespace FashionPOS
{
    public partial class POSView : UserControl
    {
        public POSView()
        {
            InitializeComponent();
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && DataContext is POSViewModel viewModel)
            {
                viewModel.QuickAddFromSearch();
                e.Handled = true;
            }
        }
    }
}
