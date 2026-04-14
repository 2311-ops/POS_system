using System;
using FashionPOS;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        try
        {
            var app = new App();
            app.InitializeComponent();
            var window = new MainWindow();
            Console.WriteLine("MainWindow_OK");
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }
    }
}
