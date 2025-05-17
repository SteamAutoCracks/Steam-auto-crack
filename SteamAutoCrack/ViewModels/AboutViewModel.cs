using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace SteamAutoCrack.ViewModels;

public class AboutViewModel : INotifyPropertyChanged
{
    public string Ver => "SteamAutoCrack " + Assembly.GetExecutingAssembly().GetName().Version;

    #region INPC

    public event PropertyChangedEventHandler? PropertyChanged;

    private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
    {
        if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion
}