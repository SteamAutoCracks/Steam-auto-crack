using Microsoft.Win32;
using SteamAutoCrack.Core.Config;
using SteamAutoCrack.Core.Utils;
using SteamAutoCrack.ViewModels;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;

namespace SteamAutoCrack.Views;

/// <summary>
///     Settings.xaml 的交互逻辑
/// </summary>
public delegate void SettingsClosingHandler();

public delegate void ReloadValueHandler();

public partial class Settings : Window
{
    private readonly SettingsViewModel viewModel = new();

    public Settings()
    {
        InitializeComponent();
#pragma warning disable WPF0001
        var useLightTheme = Registry.GetValue("HKEY_CURRENT_USER\\Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize",
            "AppsUseLightTheme", true) as int?;
        if (useLightTheme != null) ThemeMode = (useLightTheme == 1) ? ThemeMode.Light : ThemeMode.Dark;
        else ThemeMode = ThemeMode.Light;
        DataContext = viewModel;
    }

    public event SettingsClosingHandler? ClosingEvent;
    public event ReloadValueHandler? ReloadValueEvent;

    public void ReloadValue()
    {
        viewModel.ReloadValue();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        StrikeEvent();
    }

    private void StrikeEvent()
    {
        ClosingEvent?.Invoke();
    }

    private void RestoreConfig_Click(object sender, RoutedEventArgs e)
    {
        Config.ResettoDefaultAll();
        ReloadValueEvent?.Invoke();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        ClosingEvent?.Invoke();
        Close();
    }

    private async void Download_Click(object sender, RoutedEventArgs e)
    {
        await Task.Run(async () =>
        {
            var updater = new EMUUpdater();
            await updater.Init();
            await updater.Download(viewModel.ForceUpdate);
        });
    }

    private void UpdateAppList_Click(object sender, RoutedEventArgs e)
    {
        Task.Run(async () => { await SteamAppList.Initialize(true).ConfigureAwait(false); });
    }
}