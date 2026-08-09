using System.Configuration;

namespace AsusFanControlKimera.Properties
{
    internal sealed class Settings : ApplicationSettingsBase
    {
        private static readonly Settings instance = (Settings)Synchronized(new Settings());
        internal static Settings Default { get { return instance; } }

        [UserScopedSetting, DefaultSettingValue("System")]
        public string Mode { get { return (string)this["Mode"]; } set { this["Mode"] = value; } }

        [UserScopedSetting, DefaultSettingValue("70")]
        public int ManualSpeed { get { return (int)this["ManualSpeed"]; } set { this["ManualSpeed"] = value; } }

        [UserScopedSetting, DefaultSettingValue("True")]
        public bool SafeLimits { get { return (bool)this["SafeLimits"]; } set { this["SafeLimits"] = value; } }

        [UserScopedSetting, DefaultSettingValue("True")]
        public bool ReleaseOnExit { get { return (bool)this["ReleaseOnExit"]; } set { this["ReleaseOnExit"] = value; } }

        [UserScopedSetting, DefaultSettingValue("True")]
        public bool MinimizeToTray { get { return (bool)this["MinimizeToTray"]; } set { this["MinimizeToTray"] = value; } }

        [UserScopedSetting, DefaultSettingValue("False")]
        public bool StartMinimized { get { return (bool)this["StartMinimized"]; } set { this["StartMinimized"] = value; } }

        [UserScopedSetting, DefaultSettingValue("3")]
        public int Hysteresis { get { return (int)this["Hysteresis"]; } set { this["Hysteresis"] = value; } }

        [UserScopedSetting, DefaultSettingValue("2000")]
        public int RefreshInterval { get { return (int)this["RefreshInterval"]; } set { this["RefreshInterval"] = value; } }

        [UserScopedSetting, DefaultSettingValue(FanCurveDefault)]
        public string Curve { get { return (string)this["Curve"]; } set { this["Curve"] = value; } }

        [UserScopedSetting, DefaultSettingValue("")]
        public string CurveProfiles
        {
            get { return (string)this["CurveProfiles"]; }
            set { this["CurveProfiles"] = value; }
        }

        [UserScopedSetting, DefaultSettingValue("")]
        public string ActiveCurveProfile
        {
            get { return (string)this["ActiveCurveProfile"]; }
            set { this["ActiveCurveProfile"] = value; }
        }

        [UserScopedSetting, DefaultSettingValue("True")]
        public bool UpgradeRequired
        {
            get { return (bool)this["UpgradeRequired"]; }
            set { this["UpgradeRequired"] = value; }
        }

        [UserScopedSetting, DefaultSettingValue("")]
        public string InteractiveUserSid
        {
            get { return (string)this["InteractiveUserSid"]; }
            set { this["InteractiveUserSid"] = value; }
        }

        [UserScopedSetting, DefaultSettingValue("False")]
        public bool DebugEnabled
        {
            get { return (bool)this["DebugEnabled"]; }
            set { this["DebugEnabled"] = value; }
        }

        [UserScopedSetting, DefaultSettingValue("it")]
        public string Language
        {
            get { return (string)this["Language"]; }
            set { this["Language"] = value; }
        }

        private const string FanCurveDefault = "20,40-50,40-60,50-70,65-80,80-90,95-100,100";
    }
}
