using System.Text.RegularExpressions;
using AuthenticodeExaminer;
using IniFile;
using Serilog;

namespace SteamAutoCrack.Core.Utils;

public class EMUApplyConfig
{
    /// <summary>
    ///     Steam emulator config path.
    /// </summary>
    public string ConfigPath { get; set; } = Path.Combine(Config.Config.TempPath, "steam_settings");

    /// <summary>
    ///     Path of steam emulator files.
    /// </summary>
    public string GoldbergPath { get; set; } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Goldberg");

    /// <summary>
    ///     Path to apply steam emulator.
    /// </summary>
    public string ApplyPath { get; set; } = string.Empty;

    /// <summary>
    ///     Emulator save location.
    /// </summary>
    public string LocalSave { get; set; } = Path.Combine("steam_settings", "saves");

    /// <summary>
    ///     Enable change default emulator save location.
    /// </summary>
    public bool UseLocalSave { get; set; } = false;

    /// <summary>
    ///     Use Experimental version of goldberg emulator.
    /// </summary>
    public bool UseGoldbergExperimental { get; set; } = false;

    /// <summary>
    ///     Detect file sign date and generate steam_interfaces.txt
    /// </summary>
    public bool GenerateInterfacesFile { get; set; } = false;

    /// <summary>
    ///     Force generate file steam_interfaces.txt (Ignore file sign date)
    /// </summary>
    public bool ForceGenerateInterfacesFiles { get; set; } = false;

    public static class DefaultConfig
    {
        /// <summary>
        ///     Steam emulator config path.
        /// </summary>
        public static readonly string ConfigPath = Path.Combine(Config.Config.TempPath, "steam_settings");

        /// <summary>
        ///     Path to apply steam emulator.
        /// </summary>
        public static readonly string ApplyPath = string.Empty;

        /// <summary>
        ///     Path of steam emulator files.
        /// </summary>
        public static readonly string GoldbergPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Goldberg");

        /// <summary>
        ///     Emulator save location.
        /// </summary>
        public static readonly string LocalSave = Path.Combine("steam_settings", "saves");

        /// <summary>
        ///     Enable change default emulator save location.
        /// </summary>
        public static readonly bool UseLocalSave = false;

        /// <summary>
        ///     Use Experimental version of goldberg emulator.
        /// </summary>
        public static readonly bool UseGoldbergExperimental = false;

        /// <summary>
        ///     Detect file sign date and generate steam_interfaces.txt
        /// </summary>
        public static readonly bool GenerateInterfacesFile = false;

        /// <summary>
        ///     Force generate file steam_interfaces.txt (Ignore file sign date)
        /// </summary>
        public static readonly bool ForceGenerateInterfacesFiles = false;
    }
}

public interface IEMUApply
{
    public Task<bool> Apply(EMUApplyConfig emuApplyConfig);
}

public class EMUApply : IEMUApply
{
    private readonly ILogger _log;

    private readonly List<string> InterfaceNames = new()
    {
        "SteamClient",
        "SteamGameServer",
        "SteamGameServerStats",
        "SteamUser",
        "SteamFriends",
        "SteamUtils",
        "SteamMatchMaking",
        "SteamMatchMakingServers",
        "STEAMUSERSTATS_INTERFACE_VERSION",
        "STEAMAPPS_INTERFACE_VERSION",
        "SteamNetworking",
        "STEAMREMOTESTORAGE_INTERFACE_VERSION",
        "STEAMSCREENSHOTS_INTERFACE_VERSION",
        "STEAMHTTP_INTERFACE_VERSION",
        "STEAMUNIFIEDMESSAGES_INTERFACE_VERSION",
        "STEAMUGC_INTERFACE_VERSION",
        "STEAMAPPLIST_INTERFACE_VERSION",
        "STEAMMUSIC_INTERFACE_VERSION",
        "STEAMMUSICREMOTE_INTERFACE_VERSION",
        "STEAMHTMLSURFACE_INTERFACE_VERSION_",
        "STEAMINVENTORY_INTERFACE_V",
        "SteamController",
        "SteamMasterServerUpdater",
        "STEAMVIDEO_INTERFACE_V"
    };

    public EMUApply()
    {
        _log = Log.ForContext<EMUApply>();
        Ini.Config.AllowHashForComments(true);
    }

    public async Task<bool> Apply(EMUApplyConfig emuApplyConfig)
    {
        try
        {
            if (!CheckGoldberg(emuApplyConfig))
            {
                _log.Error("Goldberg emulator file missing.");
                return false;
            }

            if (!File.Exists(Path.Combine(emuApplyConfig.ConfigPath, "steam_appid.txt")))
            {
                _log.Error("Emulator Config Not Exist. (Please generate emulator game info first)");
                return false;
            }

            if (string.IsNullOrEmpty(emuApplyConfig.ApplyPath) ||
                !(File.Exists(emuApplyConfig.ApplyPath) || Directory.Exists(emuApplyConfig.ApplyPath)))
            {
                _log.Error("Invaild input path.");
                return false;
            }

            if (File.GetAttributes(emuApplyConfig.ApplyPath).HasFlag(FileAttributes.Directory))
            {
                await ApplytoFolder(emuApplyConfig).ConfigureAwait(false);
            }
            else if (Path.GetFileNameWithoutExtension(emuApplyConfig.ApplyPath) == "steam_api")
            {
                await Applyx86(emuApplyConfig.ApplyPath, emuApplyConfig).ConfigureAwait(false);
            }
            else if (Path.GetFileNameWithoutExtension(emuApplyConfig.ApplyPath) == "steam_api64")
            {
                await Applyx64(emuApplyConfig.ApplyPath, emuApplyConfig).ConfigureAwait(false);
            }
            else
            {
                _log.Error("Cannot apply file \"{path}\". (Wrong file type)", emuApplyConfig.ApplyPath);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to apply Steam emulator.");
            return false;
        }
    }

    public bool CheckGoldberg(EMUApplyConfig emuApplyConfig)
    {
        try
        {
            _log.Debug("Checking all goldberg emulator file exists or not...");
            if (!Directory.Exists(emuApplyConfig.GoldbergPath)) return false;
            var filelist = new List<string>
            {
                Path.Combine(emuApplyConfig.GoldbergPath, "x64", "steam_api64.dll"),
                Path.Combine(emuApplyConfig.GoldbergPath, "x32", "steam_api.dll"),
                Path.Combine(emuApplyConfig.GoldbergPath, "experimental", "x64", "steam_api64.dll"),
                Path.Combine(emuApplyConfig.GoldbergPath, "experimental", "x32", "steam_api.dll")
            };
            foreach (var file in filelist)
                if (!File.Exists(file))
                    return false;
            return true;
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to Check goldberg emulator.");
            return false;
        }
    }

    private async Task Applyx64(string filePath, EMUApplyConfig emuApplyConfig)
    {
        try
        {
            _log.Debug("Applying Steam emulator to \"{filePath}\"...", filePath);
            if (File.Exists(Path.ChangeExtension(filePath, ".dll.bak")))
            {
                _log.Information("Backup file already exists, skipping apply Steam emulator \"{filePath}\"...",
                    filePath);
                return;
            }

            File.Move(filePath, Path.ChangeExtension(filePath, ".dll.bak"));
            if (emuApplyConfig.UseGoldbergExperimental)
                File.Copy(Path.Combine(emuApplyConfig.GoldbergPath, "experimental", "x64", "steam_api64.dll"),
                    filePath);
            else
                File.Copy(Path.Combine(emuApplyConfig.GoldbergPath, "x64", "steam_api64.dll"), filePath);
            if (Directory.Exists(Path.Combine(Path.GetDirectoryName(filePath) ?? String.Empty, "steam_settings")))
            {
                _log.Debug("steam_settings folder already exists, skipping copy steam_settings folder...");
                _log.Information("Steam emulator \"{filePath}\" applied.", filePath);
                return;
            }

            CopyDirectory(new DirectoryInfo(emuApplyConfig.ConfigPath),
                new DirectoryInfo(Path.Combine(Path.GetDirectoryName(filePath) ?? String.Empty, "steam_settings")));
            if (emuApplyConfig.GenerateInterfacesFile)
                await GenerateInterfacesFile(Path.ChangeExtension(filePath, ".dll.bak"),
                    emuApplyConfig.ForceGenerateInterfacesFiles).ConfigureAwait(false);
            if (emuApplyConfig.UseLocalSave)
            {
                var configsuser =
                    new Ini(Path.Combine(Path.GetDirectoryName(filePath) ?? String.Empty, "steam_settings", "configs.user.ini"));

                configsuser.Add(new Section("user::saves", "")
                {
                    new Property("local_save_path", emuApplyConfig.LocalSave,
                        " when this is set, it will force the emu to use the specified location instead of the default global location",
                        " path could be absolute, or relative to the location of the .dll/.so",
                        " leading and trailing whitespaces are trimmed",
                        " when this option is used, the global settings folder is completely ignored, allowing a full portable behavior")
                });

                configsuser.SaveTo(Path.Combine(Path.GetDirectoryName(filePath) ?? String.Empty, "steam_settings", "configs.user.ini"));

                _log.Debug("Saved local_save config to configs.user.ini");
            }
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to Apply Steam emulator \"{filePath}\". Skipping...", filePath);
        }
    }

    private async Task Applyx86(string filePath, EMUApplyConfig emuApplyConfig)
    {
        try
        {
            _log.Debug("Applying Steam emulator to \"{filePath}\"...", filePath);
            if (File.Exists(Path.ChangeExtension(filePath, ".dll.bak")))
            {
                _log.Information("Backup file already exists, skipping apply Steam emulator \"{filePath}\"...",
                    filePath);
                return;
            }

            File.Move(filePath, Path.ChangeExtension(filePath, ".dll.bak"));
            if (emuApplyConfig.UseGoldbergExperimental)
                File.Copy(Path.Combine(emuApplyConfig.GoldbergPath, "experimental", "x32", "steam_api.dll"), filePath);
            else
                File.Copy(Path.Combine(emuApplyConfig.GoldbergPath, "x32", "steam_api.dll"), filePath);
            if (Directory.Exists(Path.Combine(Path.GetDirectoryName(filePath) ?? String.Empty, "steam_settings")))
            {
                _log.Debug("steam_settings folder already exists, skipping copy steam_settings folder...");
                _log.Information("Steam emulator \"{filePath}\" applied.", filePath);
                return;
            }

            CopyDirectory(new DirectoryInfo(emuApplyConfig.ConfigPath),
                new DirectoryInfo(Path.Combine(Path.GetDirectoryName(filePath) ?? String.Empty, "steam_settings")));
            if (emuApplyConfig.GenerateInterfacesFile)
                await GenerateInterfacesFile(Path.ChangeExtension(filePath, ".dll.bak"),
                    emuApplyConfig.ForceGenerateInterfacesFiles).ConfigureAwait(false);
            if (emuApplyConfig.UseLocalSave)
            {
                var configsuser =
                    new Ini(Path.Combine(Path.GetDirectoryName(filePath) ?? String.Empty, "steam_settings", "configs.user.ini"));

                configsuser.Add(new Section("user::saves", "")
                {
                    new Property("local_save_path", emuApplyConfig.LocalSave,
                        " when this is set, it will force the emu to use the specified location instead of the default global location",
                        " path could be absolute, or relative to the location of the .dll/.so",
                        " leading and trailing whitespaces are trimmed",
                        " when this option is used, the global settings folder is completely ignored, allowing a full portable behavior")
                });

                configsuser.SaveTo(Path.Combine(Path.GetDirectoryName(filePath) ?? String.Empty, "steam_settings", "configs.user.ini"));

                _log.Debug("Saved local_save config to configs.user.ini");
            }
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to Apply Steam emulator \"{filePath}\". Skipping...", filePath);
        }
    }

    private async Task ApplytoFolder(EMUApplyConfig emuApplyConfig)
    {
        try
        {
            _log.Debug("Applying Steam emulator to \"{filePath}\"...", emuApplyConfig.ApplyPath);
            foreach (var dllpath in Directory.EnumerateFiles(emuApplyConfig.ApplyPath, "steam_api.dll",
                         SearchOption.AllDirectories)) await Applyx86(dllpath, emuApplyConfig).ConfigureAwait(false);
            foreach (var dllpath in Directory.EnumerateFiles(emuApplyConfig.ApplyPath, "steam_api64.dll",
                         SearchOption.AllDirectories)) await Applyx64(dllpath, emuApplyConfig).ConfigureAwait(false);
            _log.Information("Applied Steam emulator to \"{filePath}\"...", emuApplyConfig.ApplyPath);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to Apply Steam emulator \"{filePath}\". Skipping...", emuApplyConfig.ApplyPath);
        }
    }

    public void CopyDirectory(DirectoryInfo source, DirectoryInfo target)
    {
        Directory.CreateDirectory(target.FullName);

        // Copy each file into the new directory.
        foreach (var fi in source.GetFiles()) fi.CopyTo(Path.Combine(target.FullName, fi.Name), true);

        // Copy each subdirectory using recursion.
        foreach (var diSourceSubDir in source.GetDirectories())
        {
            var nextTargetSubDir =
                target.CreateSubdirectory(diSourceSubDir.Name);
            CopyDirectory(diSourceSubDir, nextTargetSubDir);
        }
    }

    private async Task GenerateInterfacesFile(string filePath, bool force)
    {
        try
        {
            if (File.Exists(Path.Combine(Path.GetDirectoryName(filePath) ?? String.Empty, "steam_settings", "steam_interfaces.txt")))
                _log.Debug("steam_interfaces.txt already exists, skipping generate interfaces.");
            _log.Debug("Generating interface file for \"{filePath}\"", filePath);
            if (!force)
            {
                var extractor = new FileInspector(filePath);
                var signTime = extractor.GetSignatures().FirstOrDefault()?.TimestampSignatures.FirstOrDefault()
                    ?.TimestampDateTime?.UtcDateTime;
                if (signTime == null)
                {
                    _log.Debug("No sign timestamp, skipping generate interface file.");
                    return;
                }

                if (DateTime.Compare((DateTime)signTime, DateTime.Parse("2016-05-01 00:00:00")) >= 0)
                {
                    _log.Debug("Timestamp is after May 2016, skipping generate interface file.");
                    return;
                }
            }

            var result = new HashSet<string>();
            var dllContent = File.ReadAllText(filePath);
            // find interfaces
            foreach (var name in InterfaceNames)
            {
                FindInterfaces(ref result, dllContent, new Regex($"{name}\\d{{3}}"));
                if (!FindInterfaces(ref result, dllContent, new Regex(@"STEAMCONTROLLER_INTERFACE_VERSION\d{3}")))
                    FindInterfaces(ref result, dllContent, new Regex("STEAMCONTROLLER_INTERFACE_VERSION"));
            }

            var dirPath = Path.GetDirectoryName(filePath);
            if (dirPath == null) return;
            await using var destination =
                File.CreateText(Path.Combine(dirPath, "steam_settings", "steam_interfaces.txt"));
            foreach (var s in result) await destination.WriteLineAsync(s).ConfigureAwait(false);
            destination.Close();
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to generate Interfaces File.");
        }
    }

    private static bool FindInterfaces(ref HashSet<string> result, string dllContent, Regex regex)
    {
        var success = false;
        var matches = regex.Matches(dllContent);
        foreach (Match match in matches)
        {
            success = true;
            //result += $@"{match.Value}\n";
            result.Add(match.Value);
        }

        return success;
    }
}