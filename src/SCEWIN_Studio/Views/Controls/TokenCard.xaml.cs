using System.Windows;
using System.Windows.Controls;
using SCEWIN_Studio.Models;

namespace SCEWIN_Studio.Views.Controls;

public partial class TokenCard : UserControl
{
    public TokenCard()
    {
        InitializeComponent();
    }

    private void OnResetClicked(object sender, RoutedEventArgs e)
    {
        if (DataContext is ScewinToken token)
        {
            token.Reset();
        }
    }
}
