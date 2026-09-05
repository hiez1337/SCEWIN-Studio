using System;
using System.Collections.Generic;
using System.Windows;
using ModernWpf.Controls;
using SCEWIN_Studio.ViewModels;
using SCEWIN_Studio.Views;

namespace SCEWIN_Studio;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly Dictionary<string, FrameworkElement> _views = new();

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainViewModel();
        DataContext = _vm;

        InitializeViews();
        NavigateTo("Dashboard");
    }

    private void InitializeViews()
    {
        _views["Dashboard"] = ViewDashboard;
        _views["PciePower"] = ViewPciePower;
        _views["Overclocking"] = ViewOverclocking;
        _views["Memory"] = ViewMemory;
        _views["CpuPower"] = ViewCpuPower;
        _views["RawTokens"] = ViewRawTokens;
        _views["ProfilesDiff"] = ViewProfilesDiff;
        _views["Settings"] = ViewSettings;

        // Pre-bind DataContext to all views
        foreach (var view in _views.Values)
        {
            view.DataContext = _vm;
        }

        // Asynchronously warm up all views layout in the background so tabs switch at 0ms latency
        Dispatcher.InvokeAsync(() =>
        {
            foreach (var view in _views.Values)
            {
                view.Measure(new Size(1000, 800));
            }
        }, System.Windows.Threading.DispatcherPriority.Background);
    }

    private void OnNavItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.InvokedItemContainer is NavigationViewItem item && item.Tag is string tag)
        {
            NavigateTo(tag);
        }
    }

    public void NavigateTo(string tag)
    {
        if (_views.ContainsKey(tag))
        {
            foreach (var pair in _views)
            {
                pair.Value.Visibility = (pair.Key == tag) ? Visibility.Visible : Visibility.Collapsed;
            }

            _vm.CurrentNavView = tag;

            // Sync NavView selection
            SyncNavSelection(tag);
        }
    }

    private void SyncNavSelection(string tag)
    {
        foreach (var obj in NavView.MenuItems)
        {
            if (obj is NavigationViewItem item && (string?)item.Tag == tag)
            {
                NavView.SelectedItem = item;
                return;
            }
        }
        foreach (var obj in NavView.FooterMenuItems)
        {
            if (obj is NavigationViewItem item && (string?)item.Tag == tag)
            {
                NavView.SelectedItem = item;
                return;
            }
        }
    }
}
