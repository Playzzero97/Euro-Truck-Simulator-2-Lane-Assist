using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.ComponentModel;

using Avalonia.Interactivity;
using Avalonia.Controls;
using Avalonia.Input;

using ETS2LA.Game;
using ETS2LA.Notifications;
using ETS2LA.Logging;

namespace ETS2LA.UI.Views.Settings;

public partial class SDKSettings : UserControl
{
    public ObservableCollection<GameItem> Games { get; } = new();

    # if LINUX
    public bool IsLinux => true;
    # else
    public bool IsLinux => false;
    # endif

    public SDKSettings()
    {
        InitializeComponent();
        DataContext = this;
        UpdateGamesList();
    }

    private void UpdateGamesList()
    {
        Games.Clear();
        foreach (var installation in GameHandler.Current.Installations)
        {
            Games.Add(new GameItem(installation));
        }
    }

    private void OnTriggerChange(object? sender, PointerPressedEventArgs e)
    {
        if (!e.Properties.IsLeftButtonPressed)
            return;

        if (sender is Control { Tag: GameItem item })
        {
            Task.Run(() => item.TriggerChange());
        }
    }

    private void OnTriggerChangeKey(object? sender, KeyEventArgs e)
    {
        if (sender is Control { Tag: GameItem item })
        {
            if (e.Key == Key.Enter || e.Key == Key.Space)
            {
                Task.Run(() => item.TriggerChange());
            }
        }
    }
}

public class GameItem : INotifyPropertyChanged
{
    public string Name => GetName();
    public string Version => installation.Version;
    public string UpdatedVersion { get; set; } = string.Empty;
    public string Path => installation.Path;
    public bool IsUnknownVersion => !Version.Contains(".");
    public bool IsSDKInstalled => installation.IsSDKInstalled(IsUnknownVersion ? UpdatedVersion : Version);

    public string AutomationName => GetAutomationName();

    private Installation installation;

    public GameItem(Installation installation)
    {
        this.installation = installation;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public void TriggerChange()
    {
        if (IsSDKInstalled)
        {
            if (installation.UninstallSDK(IsUnknownVersion ? UpdatedVersion : Version))
            {
                Logger.Info($"Uninstalled SDK for {Name} at {Path}");
                NotificationHandler.Current.SendNotification(new Notification
                {
                    Id = "ETS2LA.UI.SDKSettings.Uninstall",
                    Title = $"Uninstalled SDK for {Name}",
                    Content = $"Successfully uninstalled SDK for {Name} at {Path}.",
                    Level = NotificationLevel.Success
                });
            }
            else
            {
                Logger.Error($"Failed to uninstall SDK for {Name} at {Path}");
                NotificationHandler.Current.SendNotification(new Notification
                {
                    Id = "ETS2LA.UI.SDKSettings.UninstallFailed",
                    Title = $"Failed to uninstall SDK for {Name}",
                    Content = $"An error occurred while uninstalling SDK for {Name} at {Path}. Please check the logs for more details.",
                    Level = NotificationLevel.Danger
                });
            }
        }
        else
        {
            if (installation.InstallSDK(IsUnknownVersion ? UpdatedVersion : Version))
            {
                Logger.Info($"Installed SDK for {Name} at {Path}");
                NotificationHandler.Current.SendNotification(new Notification
                {
                    Id = "ETS2LA.UI.SDKSettings.Install",
                    Title = $"Installed SDK for {Name}",
                    Content = $"Successfully installed SDK for {Name} at {Path}.",
                    Level = NotificationLevel.Success
                });
            }
            else
            {
                Logger.Error($"Failed to install SDK for {Name} at {Path}");
                NotificationHandler.Current.SendNotification(new Notification
                {
                    Id = "ETS2LA.UI.SDKSettings.InstallFailed",
                    Title = $"Failed to install SDK for {Name}",
                    Content = $"An error occurred while installing SDK for {Name} at {Path}. Please check the logs for more details.",
                    Level = NotificationLevel.Danger
                });
            }
        }

        OnPropertyChanged(nameof(IsSDKInstalled));
    }

    private string GetName()
    {
        return installation.Type == GameType.EuroTruckSimulator2 ? "Euro Truck Simulator 2" : "American Truck Simulator";
    }

    private string GetAutomationName()
    {
        return $"{Name} {Version}, SDK is {(IsSDKInstalled ? "Installed" : "Not Installed")} at {Path}, button";
    }
}
