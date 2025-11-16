using System.CommandLine;
using System.Reflection;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Templates;
using Serilog.Templates.Themes;
using SteamAutoCrack.Core.Config;
using SteamAutoCrack.Core.Utils;

namespace SteamAutoCrack.CLI;

internal class Program
{
    private static async Task<int> Main(string[] args)
    {
        var levelSwitch = new LoggingLevelSwitch();
        Log.Logger = new LoggerConfiguration()
            .Enrich.WithProperty("SourceContext", null)
            .MinimumLevel.ControlledBy(levelSwitch)
            .WriteTo.Console(new ExpressionTemplate(
                "[{@l:u3}] [{Substring(SourceContext, LastIndexOf(SourceContext, '.') + 1)}] {@m}\r\n{@x}",
                theme: TemplateTheme.Literate))
            .CreateLogger();
        levelSwitch.MinimumLevel = LogEventLevel.Information;
        var _log = Log.ForContext<Program>();
        Option<bool> DebugOption = new Option<bool>(
            "--debug",
            "Enable Debug Log.");

        #region crack

        Option<FileInfo?> ConfigOption = new("--config")
        {
            Description = "The process config json file. [Default: config.json in program current running directory]",
            DefaultValueFactory = parseResult => new FileInfo(Config.ConfigPath)
        };

        Option<string> AppIDOption = new("--appid")
        {
            Description = "The game Steam AppID. (Required when Generate Goldberg Steam emulator game info)"
        };

        Argument<string> pathArgument = new("Path")
        {
            Description = "Input Path."
        };

        var crackCommand = new Command("crack", "Start crack process.")
        {
            pathArgument,
            ConfigOption,
            AppIDOption
        };

        crackCommand.SetAction(async (parseResult) =>
        {
            if (parseResult.GetValue(DebugOption)) SetDebugLogLevel(levelSwitch);
            await Process(parseResult.GetValue(pathArgument), parseResult.GetValue(ConfigOption), parseResult.GetValue(AppIDOption));
        });

        #endregion

        #region downloademu

        Option<bool> ForceDownloadOption = new Option<bool>(
            "--force",
            "Force (re)download."
        );

        var downloademuCommand = new Command("downloademu", "Download/Update Goldberg Steam emulator.")
        {
            ForceDownloadOption
        };

        downloademuCommand.SetAction(async (parseResult) =>
        {
            try
            {
                if (parseResult.GetValue(DebugOption)) SetDebugLogLevel(levelSwitch);
                var updater = new EMUUpdater();
                await updater.Init();
                await updater.Download(parseResult.GetValue(ForceDownloadOption));
                _log.Information("Updated Goldberg Steam emulator.");
            }
            catch (Exception ex)
            {
                var _log = Log.ForContext<Program>();
                _log.Error(ex, "Error to Update Steam App List.");
            }
        });

        #endregion

        #region updateapplist

        var updateapplistCommand = new Command("updateapplist", "Force Update Steam App List.");
        updateapplistCommand.SetAction(async (parseResult) =>
        {
            try
            {
                if (parseResult.GetValue(DebugOption)) SetDebugLogLevel(levelSwitch);
                await SteamAppList.Initialize(true).ConfigureAwait(false);
                _log.Information("Steam App List Updated.");
            }
            catch (Exception ex)
            {
                var _log = Log.ForContext<Program>();
                _log.Error(ex, "Error to Update Steam App List.");
            }
        });

        #endregion

        #region createconfig

        Option<FileInfo?> configpathOption = new Option<FileInfo?>(
            "--path",
            "Changes default config path.");
        var createconfigCommand = new Command("createconfig", "Create Default Config File.")
        {
            configpathOption
        };

        createconfigCommand.SetAction(async (parseResult) =>
        {
            try
            {
                if (parseResult.GetValue(DebugOption)) SetDebugLogLevel(levelSwitch);
                var ConfigPath = parseResult.GetValue(configpathOption);
                Config.ConfigPath = ConfigPath == null ? Config.ConfigPath : ConfigPath.FullName;
                if (File.Exists(Config.ConfigPath))
                {
                    _log.Information("Config file already exists.");
                    return;
                }

                Config.ResettoDefaultConfigs();
                Config.SaveConfig();
                _log.Information("Config Created.");
            }
            catch (Exception ex)
            {
                var _log = Log.ForContext<Program>();
                _log.Error(ex, "Error to Create Config.");
            }
        });

        #endregion

        #region rootcommand

        var rootCommand = new RootCommand("SteamAutoCrack " + Assembly.GetExecutingAssembly().GetName().Version +
                                          " - Steam Game Automatic Cracker")
        {
            crackCommand,
            updateapplistCommand,
            downloademuCommand,
            createconfigCommand
        };

        rootCommand.Options.Add(DebugOption);

        #endregion

        return await rootCommand.Parse(args).InvokeAsync();
    }

    private static async Task Process(string InputPath, FileInfo ConfigPath, string AppID)
    {
        try
        {
            var _log = Log.ForContext<Program>();
            Config.ConfigPath = ConfigPath != null && ConfigPath.Exists ? ConfigPath.FullName : Config.ConfigPath;
            if (!Config.LoadConfig()) _log.Warning("Cannot load config. Using Default Config.");
            Config.InputPath = InputPath;
            Config.EMUGameInfoConfigs.AppID = AppID;
            await new Processor().ProcessFileCLI();
        }
        catch (Exception ex)
        {
            var _log = Log.ForContext<Program>();
            _log.Error(ex, "Error to process.");
        }
    }

    private static void SetDebugLogLevel(LoggingLevelSwitch levelSwitch)
    {
        levelSwitch.MinimumLevel = LogEventLevel.Debug;
    }
}