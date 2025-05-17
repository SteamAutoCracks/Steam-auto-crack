using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;
using Antelcat.I18N.Attributes;
using Microsoft.Win32;
using Serilog;
using Serilog.Events;
using SteamAutoCrack.Core.Config;
using SteamAutoCrack.Core.Utils;
using SteamAutoCrack.Properties;
using SteamAutoCrack.Utils;
using SteamAutoCrack.ViewModels;
using SteamAutoCrack.Views;
using WPFCustomMessageBox;

namespace SteamAutoCrack;

//Auto generated class should be partial
[ResourceKeysOf(typeof(Resources))]
public partial class LangKeys
{
}

public partial class MainWindow
{
    private readonly ILogger _log;

    private readonly MainWindowViewModel viewModel = new();
    private bool bAboutOpened;
    private bool bAppIDFinderOpened;

    private bool bSettingsOpened;
    private bool bStarted;
    private CancellationTokenSource cts = new();

    private enum UIState
    {
        Idle,
        Processing,
        ProcessingNoStop,
        AnotherWindowOpened,
    }

    private UIState _currentUIState = UIState.Idle;
    private class UIStates
    {
        public bool _init { get; set; } = false; // Indicate if the UI state is initialized or not
        public bool GenerateEMUGameInfo { get; set; } = true;
        public bool GenerateEMUConfig { get; set; } = true;
        public bool Unpack { get; set; } = true;
        public bool ApplyEMU { get; set; } = true;
        public bool GenerateCrackOnly { get; set; } = false;
    }
    private UIStates _prevuiStates = new()
    {
        GenerateEMUGameInfo = true,
        GenerateEMUConfig = true,
        Unpack = true,
        ApplyEMU = true,
        GenerateCrackOnly = false
    };

    /// <summary>
    /// UI State Machine
    /// </summary>
    private void UpdateUIState(UIState newState, bool set_restore = false)
    {
        _currentUIState = newState;
        Dispatcher.Invoke(() =>
        {
            switch (newState)
            {
                case UIState.Idle:
                    Start.IsEnabled = true;
                    AppIDFinder.IsEnabled = true;
                    Settings.IsEnabled = true;
                    GenerateEMUGameInfoGrid.IsEnabled = true;
                    GenerateEMUConfigGrid.IsEnabled = true;
                    UnpackGrid.IsEnabled = true;
                    ApplyEMUGrid.IsEnabled = true;
                    GenerateCrackOnlyGrid.IsEnabled = true;
                    // Allow other areas when not enabled restore
                    if (set_restore && _prevuiStates._init)
                    {
                        if (viewModel.Restore)
                        {
                            _prevuiStates.GenerateEMUGameInfo = viewModel.GenerateEMUGameInfo;
                            _prevuiStates.GenerateEMUConfig = viewModel.GenerateEMUConfig;
                            _prevuiStates.Unpack = viewModel.Unpack;
                            _prevuiStates.ApplyEMU = viewModel.ApplyEMU;
                            _prevuiStates.GenerateCrackOnly = viewModel.GenerateCrackOnly;

                            viewModel.GenerateEMUGameInfo = false;
                            viewModel.GenerateEMUConfig = false;
                            viewModel.Unpack = false;
                            viewModel.ApplyEMU = false;
                            viewModel.GenerateCrackOnly = false;
                        }
                        else
                        {
                            viewModel.GenerateEMUGameInfo = _prevuiStates.GenerateEMUGameInfo;
                            viewModel.GenerateEMUConfig = _prevuiStates.GenerateEMUConfig;
                            viewModel.Unpack = _prevuiStates.Unpack;
                            viewModel.ApplyEMU = _prevuiStates.ApplyEMU;
                            viewModel.GenerateCrackOnly = _prevuiStates.GenerateCrackOnly;
                        }

                    }
                    _prevuiStates._init = true;
                    GenerateEMUGameInfo.IsEnabled = !viewModel.Restore;
                    GenerateEMUConfig.IsEnabled = !viewModel.Restore;
                    Unpack.IsEnabled = !viewModel.Restore;
                    ApplyEMU.IsEnabled = !viewModel.Restore;
                    GenerateCrackOnly.IsEnabled = !viewModel.Restore;
                    Restore.IsEnabled = true;
                    InputPath.IsEnabled = true;
                    Select.IsEnabled = true;
                    viewModel.StartBtnString = Properties.Resources.Start;
                    break;
                case UIState.Processing:
                    Start.IsEnabled = true;
                    AppIDFinder.IsEnabled = false;
                    Settings.IsEnabled = false;
                    GenerateEMUGameInfoGrid.IsEnabled = false;
                    GenerateEMUConfigGrid.IsEnabled = false;
                    UnpackGrid.IsEnabled = false;
                    ApplyEMUGrid.IsEnabled = false;
                    GenerateCrackOnlyGrid.IsEnabled = false;
                    GenerateEMUGameInfo.IsEnabled = false;
                    GenerateEMUConfig.IsEnabled = false;
                    Unpack.IsEnabled = false;
                    ApplyEMU.IsEnabled = false;
                    GenerateCrackOnly.IsEnabled = false;
                    Restore.IsEnabled = false;
                    InputPath.IsEnabled = false;
                    Select.IsEnabled = false;
                    viewModel.StartBtnString = Properties.Resources.Stop;
                    break;
                case UIState.ProcessingNoStop:
                    Start.IsEnabled = false;
                    AppIDFinder.IsEnabled = false;
                    Settings.IsEnabled = false;
                    GenerateEMUGameInfoGrid.IsEnabled = false;
                    GenerateEMUConfigGrid.IsEnabled = false;
                    UnpackGrid.IsEnabled = false;
                    ApplyEMUGrid.IsEnabled = false;
                    GenerateCrackOnlyGrid.IsEnabled = false;
                    GenerateEMUGameInfo.IsEnabled = false;
                    GenerateEMUConfig.IsEnabled = false;
                    Unpack.IsEnabled = false;
                    ApplyEMU.IsEnabled = false;
                    GenerateCrackOnly.IsEnabled = false;
                    Restore.IsEnabled = false;
                    InputPath.IsEnabled = false;
                    Select.IsEnabled = false;
                    break;
                case UIState.AnotherWindowOpened:
                    Start.IsEnabled = false;
                    AppIDFinder.IsEnabled = false;
                    Settings.IsEnabled = false;
                    break;
            }
        });
    }

    public MainWindow()
    {
        if (Config.SaveCrackConfig) Config.LoadConfig();
        Thread.CurrentThread.CurrentUICulture = CultureInfo.CreateSpecificCulture(Config.GetLanguage());
        InitializeComponent();
        if (Config.LogToFile)
            Log.Logger = new LoggerConfiguration()
                .Enrich.WithProperty("SourceContext", null)
                .MinimumLevel.ControlledBy(Config.loggingLevelSwitch)
                .WriteTo.ListViewSink(LogBox)
                .WriteTo.File("error.log", LogEventLevel.Error,
                    "[{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}", shared: true)
                .WriteTo.File("log.log",
                    outputTemplate: "[{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}", shared: true)
                .CreateLogger();
        else
            Log.Logger = new LoggerConfiguration()
                .Enrich.WithProperty("SourceContext", null)
                .MinimumLevel.ControlledBy(Config.loggingLevelSwitch)
                .WriteTo.ListViewSink(LogBox)
                .WriteTo.File("error.log", LogEventLevel.Error,
                    "[{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}", shared: true)
                .CreateLogger();
        _log = Log.ForContext<MainWindow>();
        DataContext = viewModel;
        viewModel.StartBtnString = Properties.Resources.Start;
        Config.OnLanguageChanged += newLanguage =>
        {
            I18NExtension.Culture = new CultureInfo(Config.GetLanguage());
            viewModel.StartBtnString = Properties.Resources.Start;
        };
        Loaded += MainWindow_Loaded;
        Task.Run(async () => { await SteamAppList.Initialize().ConfigureAwait(false); });
        Task.Run(() => { CheckGoldberg(); });
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        SteamAPICheckBypassMode_SelectionChanged(null, null);
        UpdateUIState(UIState.Idle);
    }

    private void MainWindow_Closing(object sender, CancelEventArgs e)
    {
        Environment.Exit(0);
    }

    #region GenCrackOnly

    private void SelectOutpath_Click(object sender, RoutedEventArgs e)
    {
        var selector = new OpenFolderDialog();
        selector.Multiselect = false;
        if (selector.ShowDialog() == true) viewModel.OutputPath = selector.FolderName;
    }

    #endregion

    #region SteamStubUnpacker

    private void SteamAPICheckBypassMode_SelectionChanged(object? sender, SelectionChangedEventArgs? e)
    {
        if (SteamAPICheckBypassNthTime == null)
            return;
        if (viewModel.SteamAPICheckBypassMode == SteamStubUnpackerConfig.SteamAPICheckBypassModes.Disabled ||
            viewModel.SteamAPICheckBypassMode == SteamStubUnpackerConfig.SteamAPICheckBypassModes.All)
            SteamAPICheckBypassNthTime.IsEnabled = false;
        else
            SteamAPICheckBypassNthTime.IsEnabled = true;
    }

    #endregion

    #region Basic

    private void Start_Click(object sender, RoutedEventArgs e)
    {
        Task.Run(async () =>
        {
            if (bStarted)
            {
                Dispatcher.Invoke(() => { Start.IsEnabled = false; });
                cts.Cancel();
                return;
            }

            UpdateUIState(UIState.ProcessingNoStop);

            cts = new CancellationTokenSource();

            if (viewModel.GenerateEMUGameInfo && viewModel.AppID == string.Empty && viewModel.InputPath != string.Empty)
            {
                _log.Information(Properties.Resources.EmptyAppIDPleaseSelectOneUsingAppIDFinder);
                if (!bAppIDFinderOpened)
                    Dispatcher.Invoke(() =>
                    {
                        bAppIDFinderOpened = true;
                        var finder = new AppIDFinder(GetAppName());
                        finder.ClosingEvent += AppIDFinderClosedStart;
                        finder.OKEvent += AppIDFinderOKStart;
                        finder.Show();
                    });

                return;
            }

            UpdateUIState(UIState.Processing);

            bStarted = true;
            await new Processor().ProcessFileGUI(cts.Token).ConfigureAwait(false);
            UpdateUIState(UIState.Idle);
            bStarted = false;
        });
    }

    private void AppIDFinderClosedStart()
    {
        if (bStarted) return;
        Task.Run(() =>
        {
            bAppIDFinderOpened = false;
            UpdateUIState(UIState.Idle);
        });
    }

    private void AppIDFinderOKStart(uint appid)
    {
        bStarted = true;
        Task.Run(async () =>
        {
            viewModel.AppID = appid.ToString();
            bAppIDFinderOpened = false;
            UpdateUIState(UIState.Processing);

            await new Processor().ProcessFileGUI(cts.Token).ConfigureAwait(false);
            UpdateUIState(UIState.Idle);
            bStarted = false;
        });
    }

    private void Clear_Log_Click(object sender, RoutedEventArgs e)
    {
        LogBox.Items.Clear();
    }

    private void InputPath_PreviewDragOver(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
    }

    private void InputPath_PreviewDrop(object sender, DragEventArgs e)
    {
        try
        {
            var file = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (file.Length == 1 && (File.Exists(file[0]) || Directory.Exists(file[0]))) viewModel.InputPath = file[0];
            e.Handled = true;
        }
        catch (Exception ex)
        {
            _log.Error(ex, "");
        }
    }

    private void Select_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var result = CustomMessageBox.ShowYesNoCancel(Properties.Resources.SelectFolderOrFile,
                Properties.Resources.SelectFolderOrFile, Properties.Resources.Folder, Properties.Resources.File,
                Properties.Resources.Cancel);
            if (result == MessageBoxResult.Yes)
            {
                var selector = new OpenFolderDialog();
                selector.Multiselect = false;
                if (selector.ShowDialog() == true) viewModel.InputPath = selector.FolderName;
            }

            if (result == MessageBoxResult.No)
            {
                var selector = new OpenFileDialog();
                selector.Multiselect = false;
                selector.Filter = "Game Files|*.exe;steam_api.dll;steam_api64.dll" +
                                  "|All Files|*.*";
                if (selector.ShowDialog() == true) viewModel.InputPath = selector.FileName;
            }
        }
        catch (Exception ex)
        {
            _log.Error(ex, Properties.Resources.ErrorWhenSelectingFile);
        }
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

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Environment.Exit(0);
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        if (!bSettingsOpened)
        {
            bSettingsOpened = true;
            UpdateUIState(UIState.AnotherWindowOpened);
            var settings = new Settings();
            settings.ClosingEvent += SettingClosed;
            settings.ReloadValueEvent += viewModel.ReloadValue;
            settings.ReloadValueEvent += settings.ReloadValue;
            settings.Show();
        }
    }

    private void AppIDFinder_Click(object sender, RoutedEventArgs e)
    {
        if (!bAppIDFinderOpened)
        {
            bAppIDFinderOpened = true;
            UpdateUIState(UIState.AnotherWindowOpened);
            var finder = new AppIDFinder(GetAppName());
            finder.ClosingEvent += AppIDFinderClosed;
            finder.OKEvent += AppIDFinderOK;
            finder.Show();
        }
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        if (!bAboutOpened)
        {
            bAboutOpened = true;

            var about = new About();
            about.ClosingEvent += AboutClosed;
            about.Show();
        }
    }

    private string GetAppName()
    {
        try
        {
            if (viewModel.InputPath != string.Empty)
            {
                var path = Path.GetRelativePath(Path.Combine(viewModel.InputPath, ".."), viewModel.InputPath);
                if (path[path.Length - 1].ToString() == @"\") path = path.Substring(0, path.Length - 1);

                return path;
            }

            return string.Empty;
            ;
        }
        catch (Exception ex)
        {
            _log.Information(ex, Properties.Resources.CannotGetAppNameFromInputPath);
            return string.Empty;
        }
    }

    private void AppIDFinderClosed()
    {
        bAppIDFinderOpened = false;
        UpdateUIState(UIState.Idle);
    }

    private void AppIDFinderOK(uint appid)
    {
        viewModel.AppID = appid.ToString();
        bAppIDFinderOpened = false;
        UpdateUIState(UIState.Idle);
    }

    private void AboutClosed()
    {
        bAboutOpened = false;
    }

    private void SettingClosed()
    {
        bSettingsOpened = false;
        UpdateUIState(UIState.Idle);
    }

    public void CheckGoldberg()
    {
        if (!GoldbergStatus())
            Application.Current.Dispatcher.Invoke((Action)(() =>
            {
                var result = CustomMessageBox.ShowYesNo(
                    Properties.Resources.GoldbergEmulatorFileIsMissingNDownloadGoldbergEmulator1 + "\n" +
                    Properties.Resources.GoldbergEmulatorFileIsMissingNDownloadGoldbergEmulator2,
                    Properties.Resources.GoldbergEmulatorFileIsMissingNDownloadGoldbergEmulator2,
                    Properties.Resources.Download, Properties.Resources.Cancel);
                if (result == MessageBoxResult.Yes)
                    Task.Run(async () =>
                    {
                        var updater = new EMUUpdater();
                        await updater.Init().ConfigureAwait(false);
                        await updater.Download(true).ConfigureAwait(false);
                    });
            }));
    }

    public bool GoldbergStatus()
    {
        try
        {
            _log.Debug(Properties.Resources.CheckingAllGoldbergEmulatorFileExistsOrNot);
            if (!Directory.Exists(Config.GoldbergPath)) return false;
            var filelist = new List<string>
            {
                Path.Combine(Config.GoldbergPath, "x64", "steam_api64.dll"),
                Path.Combine(Config.GoldbergPath, "x32", "steam_api.dll"),
                Path.Combine(Config.GoldbergPath, "experimental", "x64", "steam_api64.dll"),
                Path.Combine(Config.GoldbergPath, "experimental", "x32", "steam_api.dll")
            };
            foreach (var file in filelist)
                if (!File.Exists(file))
                    return false;
            return true;
        }
        catch (Exception ex)
        {
            _log.Error(ex, Properties.Resources.FailedToCheckGoldbergEmulator);
            return false;
        }
    }

    #endregion

    #region GenerateEMUGameInfo

    private void AppID_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !uint.TryParse(AppID.Text + e.Text, out _);
    }

    private void AppID_Pasting(object sender, DataObjectPastingEventArgs e)
    {
        if (e.DataObject.GetDataPresent(typeof(string)))
        {
            var text = (string)e.DataObject.GetData(typeof(string));
            if (!uint.TryParse(text, out _)) e.CancelCommand();
        }
        else
        {
            e.CancelCommand();
        }
    }

    #endregion

    #region GenerateEMUConfig

    private void SteamID_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !ulong.TryParse(AppID.Text + e.Text, out _);
    }

    private void SteamID_Pasting(object sender, DataObjectPastingEventArgs e)
    {
        if (e.DataObject.GetDataPresent(typeof(string)))
        {
            var text = (string)e.DataObject.GetData(typeof(string));
            if (!ulong.TryParse(text, out _)) e.CancelCommand();
        }
        else
        {
            e.CancelCommand();
        }
    }

    private void ListenPort_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !ushort.TryParse(AppID.Text + e.Text, out var i);
    }

    private void ListenPort_Pasting(object sender, DataObjectPastingEventArgs e)
    {
        if (e.DataObject.GetDataPresent(typeof(string)))
        {
            var text = (string)e.DataObject.GetData(typeof(string));
            if (!ushort.TryParse(text, out _)) e.CancelCommand();
        }
        else
        {
            e.CancelCommand();
        }
    }

    private void OpenExample_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (Directory.Exists(Path.Combine(Config.GoldbergPath, "steam_settings.EXAMPLE")))
                Process.Start("explorer.exe", Path.Combine(Config.GoldbergPath, "steam_settings.EXAMPLE"));
            else
                _log.Error("Goldberg Steam Emulator Config EXAMPLE folder missing.");
            e.Handled = true;
        }
        catch (Exception ex)
        {
            _log.Error(ex, "");
        }
    }

    private void OpenConfigFolder_Click(object sender, RoutedEventArgs e)
    {
        if (Directory.Exists(Config.EMUConfigPath))
            Process.Start("explorer.exe", Config.EMUConfigPath);
        else
            _log.Information("Goldberg Steam Emulator Config EXAMPLE folder not exist.");
        e.Handled = true;
    }

    #endregion

    #region Restore

    private void Restore_Checked(object sender, RoutedEventArgs e)
    {
        UpdateUIState(UIState.Idle, true);
    }

    private void Restore_Unchecked(object sender, RoutedEventArgs e)
    {
        UpdateUIState(UIState.Idle, true);
    }

    #endregion
}