namespace PhotoCull.Properties;

internal sealed partial class Settings : global::System.Configuration.ApplicationSettingsBase
{
    private static Settings defaultInstance = (Settings)Synchronized(new Settings());

    public static Settings Default => defaultInstance;

    [global::System.Configuration.UserScopedSettingAttribute()]
    [global::System.Configuration.DefaultSettingValueAttribute("")]
    public string LicenseCode
    {
        get => (string)this["LicenseCode"];
        set { this["LicenseCode"] = value; }
    }
}
