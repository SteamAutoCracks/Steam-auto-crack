using Microsoft.Win32;
using Serilog;
using SteamAutoCrack.ViewModels;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Navigation;

namespace SteamAutoCrack.Views;

/// <summary>
///     About.xaml 的交互逻辑
/// </summary>
public delegate void AboutClosingHandler();

public partial class About : Window
{
    private readonly ILogger _log = Log.ForContext<About>();
    private readonly AboutViewModel viewModel = new();

    public About()
    {
        InitializeComponent();
#pragma warning disable WPF0001
        var useLightTheme = Registry.GetValue("HKEY_CURRENT_USER\\Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize",
            "AppsUseLightTheme", true) as int?;
        if (useLightTheme != null) ThemeMode = (useLightTheme == 1) ? ThemeMode.Light : ThemeMode.Dark;
        else ThemeMode = ThemeMode.Light;
        DataContext = viewModel;
        _log.Information("Steam Auto Crack " + Assembly.GetExecutingAssembly().GetName().Version);
        _log.Information("Github: https://github.com/SteamAutoCracks/Steam-auto-crack");
        _log.Information("Gitlab: https://gitlab.com/oureveryday/Steam-auto-crack");
    }

    public event AboutClosingHandler? ClosingEvent;

    protected override void OnClosing(CancelEventArgs e)
    {
        StrikeEvent();
    }

    private void StrikeEvent()
    {
        ClosingEvent?.Invoke();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        ClosingEvent?.Invoke();
        Close();
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }
        catch (Exception ex)
        {
            _log.Error(ex, "");
        }
    }
}