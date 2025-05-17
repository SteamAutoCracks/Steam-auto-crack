using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Serilog;
using SteamAutoCrack.Core.Utils;
using SteamAutoCrack.ViewModels;

namespace SteamAutoCrack.Views;

/// <summary>
///     AppIDFinder.xaml 的交互逻辑
/// </summary>
public delegate void AppIDFinderClosingHandler();

public delegate void AppIDFinderOKHandler(uint appid);

public partial class AppIDFinder : Window
{
    private readonly ILogger _log;
    private readonly AppIDFinderViewModel viewModel = new();

    public AppIDFinder(string appname)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.AppName = appname;
        viewModel.SearchBtnString = Properties.Resources.Loading;
        Search.IsEnabled = false;
        AppName.IsEnabled = false;
        _log = Log.ForContext<AppIDFinder>();
        Task.Run(async () =>
        {
            try
            {
                await SteamAppList.WaitForReady().ConfigureAwait(false);
                Dispatcher.Invoke(() =>
                {
                    if (viewModel.AppName != string.Empty)
                    {
                        Search_Click(new object(), new RoutedEventArgs());
                    }
                    else
                    {
                        Search.IsEnabled = true;
                        AppName.IsEnabled = true;
                        viewModel.SearchBtnString = Properties.Resources.Search;
                    }
                });
            }
            catch (Exception ex)
            {
                _log.Error(ex, Properties.Resources.FailedToLoadAppListPleaseRestartSteamAutoCrackToTryAgain);
                Dispatcher.Invoke(() => { viewModel.SearchBtnString = Properties.Resources.Failed; });
            }
        });
    }

    public event AppIDFinderClosingHandler? ClosingEvent;
    public event AppIDFinderOKHandler? OKEvent;

    protected override void OnClosing(CancelEventArgs e)
    {
        StrikeEvent();
    }

    private void StrikeEvent()
    {
        ClosingEvent?.Invoke();
    }

    private void OK_Click(object sender, RoutedEventArgs e)
    {
        var apps = Apps.SelectedCells.ToList();
        if (apps.Count != 0)
        {
            var app = (SteamApp)apps[0].Item;
            if (app.AppId.HasValue)
                OKEvent?.Invoke(app.AppId.Value);
            else
                OKEvent?.Invoke(0);
            Close();
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        ClosingEvent?.Invoke();
        Close();
    }

    private void Apps_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        var apps = Apps.SelectedCells.ToList();
        if (apps.Count != 0)
        {
            var app = (SteamApp)apps[0].Item;
            if (app.AppId.HasValue)
            {
                OKEvent?.Invoke(app.AppId.Value);
                Close();
            }
        }
    }

    private async void Search_Click(object sender, RoutedEventArgs e)
    {
        viewModel.SearchBtnString = Properties.Resources.Searching;
        Search.IsEnabled = false;
        AppName.IsEnabled = false;
        if (Fuzzy.IsChecked == true)
            viewModel.Apps = await SteamAppList.GetListOfAppsByNameFuzzy(AppName.Text).ConfigureAwait(false);
        else
            viewModel.Apps = await SteamAppList.GetListOfAppsByName(AppName.Text).ConfigureAwait(false);
        Dispatcher.Invoke(() =>
        {
            Search.IsEnabled = true;
            AppName.IsEnabled = true;
        });
        viewModel.SearchBtnString = Properties.Resources.Search;
    }
}