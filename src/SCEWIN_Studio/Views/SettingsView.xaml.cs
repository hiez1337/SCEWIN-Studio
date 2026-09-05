using System.Windows;
using System.Windows.Controls;
using SCEWIN_Studio.ViewModels;

namespace SCEWIN_Studio.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    private void OnSelectRussian(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.SetLanguage("ru-RU");
        }
    }

    private void OnSelectEnglish(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.SetLanguage("en-US");
        }
    }
}
