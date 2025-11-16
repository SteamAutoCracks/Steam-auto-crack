using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using SteamAutoCrack.Core.Config;
using SteamAutoCrack.Core.Utils;
using SteamAutoCrack.Properties;

namespace SteamAutoCrack.ViewModels;

internal class MainWindowViewModel : INotifyPropertyChanged
{
    public MainWindowViewModel()
    {
        GameInfoAPIs = Enum.GetValues(typeof(EMUGameInfoConfig.GeneratorGameInfoAPI))
            .Cast<EMUGameInfoConfig.GeneratorGameInfoAPI>().ToList();
        Languages = Enum.GetValues(typeof(EMUConfig.Languages)).Cast<EMUConfig.Languages>().ToList();
        SteamAPICheckBypassModes = Enum.GetValues(typeof(SteamStubUnpackerConfig.SteamAPICheckBypassModes))
            .Cast<SteamStubUnpackerConfig.SteamAPICheckBypassModes>().ToList();
        SteamAPICheckBypassDLLs = Enum.GetValues(typeof(SteamStubUnpackerConfig.SteamAPICheckBypassDLLs))
            .Cast<SteamStubUnpackerConfig.SteamAPICheckBypassDLLs>().ToList();
    }

    #region INPC

    public event PropertyChangedEventHandler? PropertyChanged;

    private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
    {
        if (Config.SaveCrackConfig) Config.SaveConfig();

        if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
    }

    public void ReloadValue()
    {
        var type = GetType();

        foreach (var property in type.GetProperties(
                     BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            var propertyName = property.Name;

            if (propertyName.StartsWith("_")) propertyName = propertyName.Substring(1);

            NotifyPropertyChanged(propertyName);
        }
    }

    #endregion

    #region BasicConfigs

    public string InputPath
    {
        get => Config.InputPath;

        set
        {
            if (value != Config.InputPath)
            {
                Config.InputPath = value;
                NotifyPropertyChanged();
            }
        }
    }

    public string StartBtnString
    {
        get => _StartBtnString;
        set
        {
            _StartBtnString = value;
            NotifyPropertyChanged();
        }
    }

    public string Ver => "SteamAutoCrack " + Assembly.GetExecutingAssembly().GetName().Version;

    private string _StartBtnString = Resources.Start;

    #endregion

    #region ProcessConfigs

    public bool GenerateEMUGameInfo
    {
        get => Config.ProcessConfigs.GenerateEMUGameInfo;

        set
        {
            if (value != Config.ProcessConfigs.GenerateEMUGameInfo)
            {
                Config.ProcessConfigs.GenerateEMUGameInfo = value;
                NotifyPropertyChanged();
            }
        }
    }

    public bool GenerateEMUConfig
    {
        get => Config.ProcessConfigs.GenerateEMUConfig;

        set
        {
            if (value != Config.ProcessConfigs.GenerateEMUConfig)
            {
                Config.ProcessConfigs.GenerateEMUConfig = value;
                NotifyPropertyChanged();
            }
        }
    }

    public bool Unpack
    {
        get => Config.ProcessConfigs.Unpack;

        set
        {
            if (value != Config.ProcessConfigs.Unpack)
            {
                Config.ProcessConfigs.Unpack = value;
                NotifyPropertyChanged();
            }
        }
    }

    public bool ApplyEMU
    {
        get => Config.ProcessConfigs.ApplyEMU;

        set
        {
            if (value != Config.ProcessConfigs.ApplyEMU)
            {
                Config.ProcessConfigs.ApplyEMU = value;
                NotifyPropertyChanged();
            }
        }
    }

    public bool GenerateCrackOnly
    {
        get => Config.ProcessConfigs.GenerateCrackOnly;

        set
        {
            if (value != Config.ProcessConfigs.GenerateCrackOnly)
            {
                Config.ProcessConfigs.GenerateCrackOnly = value;
                NotifyPropertyChanged();
            }
        }
    }

    public bool Restore
    {
        get => Config.ProcessConfigs.Restore;

        set
        {
            if (value != Config.ProcessConfigs.Restore)
            {
                Config.ProcessConfigs.Restore = value;
                NotifyPropertyChanged();
            }
        }
    }

    #endregion

    #region EMUGameInfoConfigs

    public string AppID
    {
        get => Config.EMUGameInfoConfigs.AppID;

        set
        {
            if (value != Config.EMUGameInfoConfigs.AppID)
            {
                Config.EMUGameInfoConfigs.AppID = value;
                NotifyPropertyChanged();
            }
        }
    }

    public string SteamWebAPIKey
    {
        get => Config.EMUGameInfoConfigs.SteamWebAPIKey;

        set
        {
            if (value != Config.EMUGameInfoConfigs.SteamWebAPIKey)
            {
                Config.EMUGameInfoConfigs.SteamWebAPIKey = value;
                NotifyPropertyChanged();
            }
        }
    }

    public bool UseXan105API
    {
        get => Config.EMUGameInfoConfigs.UseXan105API;

        set
        {
            if (value != Config.EMUGameInfoConfigs.UseXan105API)
            {
                Config.EMUGameInfoConfigs.UseXan105API = value;
                NotifyPropertyChanged();
            }
        }
    }

    public bool UseSteamWebAppList
    {
        get => Config.EMUGameInfoConfigs.UseSteamWebAppList;

        set
        {
            if (value != Config.EMUGameInfoConfigs.UseSteamWebAppList)
            {
                Config.EMUGameInfoConfigs.UseSteamWebAppList = value;
                NotifyPropertyChanged();
            }
        }
    }

    public bool GenerateImages
    {
        get => Config.EMUGameInfoConfigs.GenerateImages;

        set
        {
            if (value != Config.EMUGameInfoConfigs.GenerateImages)
            {
                Config.EMUGameInfoConfigs.GenerateImages = value;
                NotifyPropertyChanged();
            }
        }
    }

    public EMUGameInfoConfig.GeneratorGameInfoAPI GameInfoAPI
    {
        get => Config.EMUGameInfoConfigs.GameInfoAPI;

        set
        {
            if (value != Config.EMUGameInfoConfigs.GameInfoAPI)
            {
                Config.EMUGameInfoConfigs.GameInfoAPI = value;
                NotifyPropertyChanged();
            }
        }
    }

    public List<EMUGameInfoConfig.GeneratorGameInfoAPI> GameInfoAPIs { get; set; }

    #endregion

    #region EMUConfigs

    public EMUConfig.Languages Language
    {
        get => Config.EMUConfigs.Language;

        set
        {
            if (value != Config.EMUConfigs.Language)
            {
                Config.EMUConfigs.Language = value;
                NotifyPropertyChanged();
            }
        }
    }

    public List<EMUConfig.Languages> Languages { get; set; }

    public string SteamID
    {
        get => Config.EMUConfigs.SteamID;

        set
        {
            if (value != Config.EMUConfigs.SteamID)
            {
                Config.EMUConfigs.SteamID = value;
                NotifyPropertyChanged();
            }
        }
    }

    public string AccountName
    {
        get => Config.EMUConfigs.AccountName;

        set
        {
            if (value != Config.EMUConfigs.AccountName)
            {
                Config.EMUConfigs.AccountName = value;
                NotifyPropertyChanged();
            }
        }
    }

    public string ListenPort
    {
        get => Config.EMUConfigs.ListenPort;

        set
        {
            if (value != Config.EMUConfigs.ListenPort)
            {
                Config.EMUConfigs.ListenPort = value;
                NotifyPropertyChanged();
            }
        }
    }

    public string CustomIP
    {
        get => Config.EMUConfigs.CustomIP;

        set
        {
            if (value != Config.EMUConfigs.CustomIP)
            {
                Config.EMUConfigs.CustomIP = value;
                NotifyPropertyChanged();
            }
        }
    }

    public bool UseCustomIP
    {
        get => Config.EMUConfigs.UseCustomIP;

        set
        {
            if (value != Config.EMUConfigs.UseCustomIP)
            {
                Config.EMUConfigs.UseCustomIP = value;
                NotifyPropertyChanged();
            }
        }
    }

    public string Ticket
    {
        get => Config.EMUConfigs.Ticket;

        set
        {
            if (value != Config.EMUConfigs.Ticket)
            {
                Config.EMUConfigs.Ticket = value;
                NotifyPropertyChanged();
            }
        }
    }

    public bool DisableNetworking
    {
        get => Config.EMUConfigs.DisableNetworking;

        set
        {
            if (value != Config.EMUConfigs.DisableNetworking)
            {
                Config.EMUConfigs.DisableNetworking = value;
                NotifyPropertyChanged();
            }
        }
    }

    public bool Offline
    {
        get => Config.EMUConfigs.Offline;

        set
        {
            if (value != Config.EMUConfigs.Offline)
            {
                Config.EMUConfigs.Offline = value;
                NotifyPropertyChanged();
            }
        }
    }

    public bool EnableOverlay
    {
        get => Config.EMUConfigs.EnableOverlay;

        set
        {
            if (value != Config.EMUConfigs.EnableOverlay)
            {
                Config.EMUConfigs.EnableOverlay = value;
                NotifyPropertyChanged();
            }
        }
    }

    #endregion

    #region SteamStubUnpackerConfigs

    public bool KeepBind
    {
        get => Config.SteamStubUnpackerConfigs.KeepBind;

        set
        {
            if (value != Config.SteamStubUnpackerConfigs.KeepBind)
            {
                Config.SteamStubUnpackerConfigs.KeepBind = value;
                NotifyPropertyChanged();
            }
        }
    }

    public bool KeepStub
    {
        get => Config.SteamStubUnpackerConfigs.KeepStub;

        set
        {
            if (value != Config.SteamStubUnpackerConfigs.KeepStub)
            {
                Config.SteamStubUnpackerConfigs.KeepStub = value;
                NotifyPropertyChanged();
            }
        }
    }

    public bool Realign
    {
        get => Config.SteamStubUnpackerConfigs.Realign;

        set
        {
            if (value != Config.SteamStubUnpackerConfigs.Realign)
            {
                Config.SteamStubUnpackerConfigs.Realign = value;
                NotifyPropertyChanged();
            }
        }
    }

    public bool ReCalcChecksum
    {
        get => Config.SteamStubUnpackerConfigs.ReCalcChecksum;

        set
        {
            if (value != Config.SteamStubUnpackerConfigs.ReCalcChecksum)
            {
                Config.SteamStubUnpackerConfigs.ReCalcChecksum = value;
                NotifyPropertyChanged();
            }
        }
    }

    public bool UseExperimentalFeatures
    {
        get => Config.SteamStubUnpackerConfigs.UseExperimentalFeatures;

        set
        {
            if (value != Config.SteamStubUnpackerConfigs.UseExperimentalFeatures)
            {
                Config.SteamStubUnpackerConfigs.UseExperimentalFeatures = value;
                NotifyPropertyChanged();
            }
        }
    }

    public SteamStubUnpackerConfig.SteamAPICheckBypassModes SteamAPICheckBypassMode
    {
        get => Config.SteamStubUnpackerConfigs.SteamAPICheckBypassMode;

        set
        {
            if (value != Config.SteamStubUnpackerConfigs.SteamAPICheckBypassMode)
            {
                Config.SteamStubUnpackerConfigs.SteamAPICheckBypassMode = value;
                NotifyPropertyChanged();
            }
        }
    }

    public List<SteamStubUnpackerConfig.SteamAPICheckBypassModes> SteamAPICheckBypassModes { get; set; }

    public string SteamAPICheckBypassNthTime
    {
        get => string.Join(",", Config.SteamStubUnpackerConfigs.SteamAPICheckBypassNthTime);
        set
        {
            var currentValue = string.Join(",", Config.SteamStubUnpackerConfigs.SteamAPICheckBypassNthTime);
            if (value != currentValue)
            {
                var parts = value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                var newList = new List<UInt64>();
                foreach (var part in parts)
                {
                    if (UInt64.TryParse(part.Trim(), out var num))
                    {
                        newList.Add(num);
                    }
                    else
                    {
                        // Invalid input
                        return;
                    }
                }
                Config.SteamStubUnpackerConfigs.SteamAPICheckBypassNthTime = newList;
                NotifyPropertyChanged();
            }
        }
    }

    public SteamStubUnpackerConfig.SteamAPICheckBypassDLLs SteamAPICheckBypassDLL
    {
        get => Config.SteamStubUnpackerConfigs.SteamAPICheckBypassDLL;

        set
        {
            if (value != Config.SteamStubUnpackerConfigs.SteamAPICheckBypassDLL)
            {
                Config.SteamStubUnpackerConfigs.SteamAPICheckBypassDLL = value;
                NotifyPropertyChanged();
            }
        }
    }

    public List<SteamStubUnpackerConfig.SteamAPICheckBypassDLLs> SteamAPICheckBypassDLLs { get; set; }

    #endregion

    #region EMUApplyConfigs

    public string LocalSave
    {
        get => Config.EMUApplyConfigs.LocalSave;

        set
        {
            if (value != Config.EMUApplyConfigs.LocalSave)
            {
                Config.EMUApplyConfigs.LocalSave = value;
                NotifyPropertyChanged();
            }
        }
    }

    public bool UseLocalSave
    {
        get => Config.EMUApplyConfigs.UseLocalSave;

        set
        {
            if (value != Config.EMUApplyConfigs.UseLocalSave)
            {
                Config.EMUApplyConfigs.UseLocalSave = value;
                NotifyPropertyChanged();
            }
        }
    }

    public bool UseGoldbergExperimental
    {
        get => Config.EMUApplyConfigs.UseGoldbergExperimental;

        set
        {
            if (value != Config.EMUApplyConfigs.UseGoldbergExperimental)
            {
                Config.EMUApplyConfigs.UseGoldbergExperimental = value;
                NotifyPropertyChanged();
            }
        }
    }

    public bool GenerateInterfacesFile
    {
        get => Config.EMUApplyConfigs.GenerateInterfacesFile;

        set
        {
            if (value != Config.EMUApplyConfigs.GenerateInterfacesFile)
            {
                Config.EMUApplyConfigs.GenerateInterfacesFile = value;
                NotifyPropertyChanged();
            }
        }
    }

    public bool ForceGenerateInterfacesFiles
    {
        get => Config.EMUApplyConfigs.ForceGenerateInterfacesFiles;

        set
        {
            if (value != Config.EMUApplyConfigs.ForceGenerateInterfacesFiles)
            {
                Config.EMUApplyConfigs.ForceGenerateInterfacesFiles = value;
                NotifyPropertyChanged();
            }
        }
    }

    #endregion

    #region GenCrackOnlyConfigs

    public string OutputPath
    {
        get => Config.GenCrackOnlyConfigs.OutputPath;

        set
        {
            if (value != Config.GenCrackOnlyConfigs.OutputPath)
            {
                Config.GenCrackOnlyConfigs.OutputPath = value;
                NotifyPropertyChanged();
            }
        }
    }

    public bool CreateReadme
    {
        get => Config.GenCrackOnlyConfigs.CreateReadme;

        set
        {
            if (value != Config.GenCrackOnlyConfigs.CreateReadme)
            {
                Config.GenCrackOnlyConfigs.CreateReadme = value;
                NotifyPropertyChanged();
            }
        }
    }

    public bool Pack
    {
        get => Config.GenCrackOnlyConfigs.Pack;

        set
        {
            if (value != Config.GenCrackOnlyConfigs.Pack)
            {
                Config.GenCrackOnlyConfigs.Pack = value;
                NotifyPropertyChanged();
            }
        }
    }

    #endregion
}