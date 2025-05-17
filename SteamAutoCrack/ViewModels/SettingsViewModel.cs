using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using SteamAutoCrack.Core.Config;
using SteamAutoCrack.Properties;

namespace SteamAutoCrack.ViewModels;

internal class SettingsViewModel : INotifyPropertyChanged
{
    private bool _ForceUpdate;
    private string _UpdateBtnString = Resources.UpdateDownload;

    public SettingsViewModel()
    {
        Languages = Enum.GetValues(typeof(Config.Languages)).Cast<Config.Languages>().ToList();
    }

    public bool SaveCrackConfig
    {
        get => Config.SaveCrackConfig;

        set
        {
            if (value != Config.SaveCrackConfig)
            {
                Config.SaveCrackConfig = value;
                NotifyPropertyChanged();
            }
        }
    }

    public bool EnableDebugLog
    {
        get => Config.EnableDebugLog;

        set
        {
            if (value != Config.EnableDebugLog)
            {
                Config.EnableDebugLog = value;
                NotifyPropertyChanged();
            }
        }
    }

    public bool LogToFile
    {
        get => Config.LogToFile;

        set
        {
            if (value != Config.LogToFile)
            {
                Config.LogToFile = value;
                NotifyPropertyChanged();
            }
        }
    }

    public string GoldbergVer => Config.GetGoldbergVersion();

    public string UpdateBtnString
    {
        get => _UpdateBtnString;
        set
        {
            _UpdateBtnString = value;
            NotifyPropertyChanged();
        }
    }

    public bool ForceUpdate
    {
        get => _ForceUpdate;

        set
        {
            if (value != _ForceUpdate)
            {
                _ForceUpdate = value;
                NotifyPropertyChanged();
            }
        }
    }

    public Config.Languages Language
    {
        get => Config.Language;

        set
        {
            if (value != Config.Language)
            {
                Config.Language = value;
                I18NExtension.Culture = new CultureInfo(Config.GetLanguage());
                NotifyPropertyChanged();
            }
        }
    }

    public List<Config.Languages> Languages { get; set; }

    #region INPC

    public event PropertyChangedEventHandler? PropertyChanged;

    private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
    {
        if (Config.SaveCrackConfig) Config.SaveConfig();
        if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
    }

    public void ReloadValue()
    {
        NotifyPropertyChanged("LogToFile");
        NotifyPropertyChanged("EnableDebugLog");
        NotifyPropertyChanged("SaveCrackConfig");
        NotifyPropertyChanged("GoldbergVer");
        NotifyPropertyChanged("UpdateBtnString");
        NotifyPropertyChanged("ForceUpdate");
    }

    #endregion
}