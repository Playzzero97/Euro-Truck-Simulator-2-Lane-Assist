using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using ETS2LA.Logging;
using ETS2LA.Settings.Global;

namespace ETS2LA.UI.Views.Settings;

public partial class UserSettingsView : UserControl, INotifyPropertyChanged
{

    public bool NeedsRestart {get; set;} = false;

    public bool IsTelemetryEnabled
    {
        get => UserSettings.Current.IsTelemetryEnabled;
        set
        {
            if (UserSettings.Current.IsTelemetryEnabled != value)
            {
                UserSettings.Current.IsTelemetryEnabled = value;
                UserSettings.Current.Save();
                
                NeedsRestart = true;
                OnPropertyChanged(nameof(IsTelemetryEnabled));
                OnPropertyChanged(nameof(NeedsRestart));
            }
        }
    }

    public UserSettingsView()
    {
        InitializeComponent();
        DataContext = this;
    }


    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
