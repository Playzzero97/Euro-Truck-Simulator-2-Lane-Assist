namespace ETS2LA.Settings.Global;

[Serializable]
public class UserSettings
{
    [NonSerialized]
    private static readonly Lazy<UserSettings> _instance = new(() => new UserSettings(loadSettings: true));
    public static UserSettings Current => _instance.Value;

    public string UserId { get; set; } = Guid.NewGuid().ToString();
    public bool IsTelemetryEnabled { get; set; } = true;

    [NonSerialized]
    private SettingsHandler? _settingsHandler;

    public UserSettings(bool loadSettings = false)
    {
        if (loadSettings)
        {
            _settingsHandler = new SettingsHandler();
            var loadedSettings = _settingsHandler.Load<UserSettings>("UserSettings.json");
            if (loadedSettings != null)
            {
                UserId = loadedSettings.UserId;
                IsTelemetryEnabled = loadedSettings.IsTelemetryEnabled;
            }
            else
            {
                Save();
            }
            _settingsHandler.RegisterListener<UserSettings>("UserSettings.json", OnSettingsChanged);
        }
    }

    public UserSettings() { }

    public void Save()
    {
        _settingsHandler?.Save<UserSettings>("UserSettings.json", this);
    }

    public void OnSettingsChanged(UserSettings newSettings)
    {
        UserId = newSettings.UserId;
        IsTelemetryEnabled = newSettings.IsTelemetryEnabled;
    }
}