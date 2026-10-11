namespace XElementArchitecture.Settings;

public static class SettingsManager
{
    public static ConfigSettings Config { get; private set; } = new();

    public static void Initialize(ConfigSettings? config = null)
    {
        Config = config ?? new ConfigSettings();
    }
}
