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
        _views["Dashboard"] = new DashboardView();
        _views["PciePower"] = new PciePowerView();
        _views["Overclocking"] = new OverclockingView();
        _views["Memory"] = new MemoryView();
        _views["CpuPower"] = new CpuPowerView();
        _views["RawTokens"] = new RawTokensView();
        _views["ProfilesDiff"] = new ProfilesDiffView();
        _views["Settings"] = new SettingsView();
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
        if (_views.TryGetValue(tag, out var view))
        {
            view.DataContext = _vm;
            ContentHost.Content = view;
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
