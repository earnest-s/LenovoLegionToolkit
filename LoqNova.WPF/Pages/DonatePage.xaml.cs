using System.Windows;

namespace LoqNova.WPF.Pages;

public partial class DonatePage
{
    public DonatePage()
    {
        InitializeComponent();
    }

    private void PayPalDonateButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
    }
}
