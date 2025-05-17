using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using SteamAutoCrack.Core.Utils;

namespace SteamAutoCrack.ViewModels;

internal class AppIDFinderViewModel : INotifyPropertyChanged
{
    #region INPC

    public event PropertyChangedEventHandler? PropertyChanged;

    private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
    {
        if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
    }

    public void ReloadValue()
    {
    }

    public string AppName
    {
        get => _AppName;

        set
        {
            if (value != _AppName)
            {
                _AppName = value;
                NotifyPropertyChanged();
            }
        }
    }

    public bool Fuzzy
    {
        get => _Fuzzy;

        set
        {
            if (value != _Fuzzy)
            {
                _Fuzzy = value;
                NotifyPropertyChanged("AppName");
            }
        }
    }

    private bool _Fuzzy;

    public string SearchBtnString
    {
        get => _SearchBtnString;
        set
        {
            _SearchBtnString = value;
            NotifyPropertyChanged();
        }
    }

    public IEnumerable<SteamApp> Apps
    {
        get => _apps!;
        set
        {
            _apps = value;
            NotifyPropertyChanged();
        }
    }

    private IEnumerable<SteamApp>? _apps;

    public SteamApp Selected { get; set; } = new();

    private string _SearchBtnString = "Search";
    private string _AppName = string.Empty;

    #endregion
}