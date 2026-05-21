using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace F2B.Browser.IExplore.Com
{
    /// <summary>
    /// Relaxes IE zone / ActiveX prompts for local automation (HKCU only, current Windows user).
    /// </summary>
    public static class IeSecurityConfigurator
    {
        private const string ZonesRoot =
            @"Software\Microsoft\Windows\CurrentVersion\Internet Settings\Zones";

        private const string ZoneMapDomains =
            @"Software\Microsoft\Windows\CurrentVersion\Internet Settings\ZoneMap\Domains";

        private const string FeatureControlRoot =
            @"Software\Microsoft\Internet Explorer\Main\FeatureControl";

        private static bool _applied;

        public static void ApplyAutomationPolicy()
        {
            if (_applied)
                return;

            try
            {
                ApplyZonePolicy(0);
                ApplyZonePolicy(2);
                TrustHost("127.0.0.1");
                TrustHost("localhost");

                var exeName = Path.GetFileName(Process.GetCurrentProcess().MainModule.FileName);
                SetFeature(exeName, "FEATURE_LOCALMACHINE_LOCKDOWN", 0);
                SetFeature(exeName, "FEATURE_BLOCK_LMZ_SCRIPT", 0);
                SetFeature(exeName, "FEATURE_RESTRICT_ACTIVEXINSTALL", 0);
                SetFeature("iexplore.exe", "FEATURE_LOCALMACHINE_LOCKDOWN", 0);
                SetFeature("iexplore.exe", "FEATURE_BLOCK_LMZ_SCRIPT", 0);

                using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Internet Explorer\ActiveX Filter"))
                {
                    if (key != null)
                        key.SetValue("Enabled", 0, RegistryValueKind.DWord);
                }

                _applied = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Warning: could not apply IE automation policy: " + ex.Message);
            }
        }

        private static void ApplyZonePolicy(int zoneIndex)
        {
            using (var zone = Registry.CurrentUser.CreateSubKey(ZonesRoot + "\\" + zoneIndex))
            {
                if (zone == null)
                    return;

                // 0 = Enable, 1 = Prompt, 3 = Disable (value names are numeric strings in the registry)
                SetDword(zone, "1004", 0);
                SetDword(zone, "1200", 0);
                SetDword(zone, "1201", 0);
                SetDword(zone, "1206", 0);
                SetDword(zone, "1207", 0);
                SetDword(zone, "1208", 0);
                SetDword(zone, "1209", 0);
                SetDword(zone, "1400", 0);
                SetDword(zone, "1402", 0);
                SetDword(zone, "1405", 0);
                SetDword(zone, "1406", 0);
                SetDword(zone, "1601", 0);
                SetDword(zone, "2000", 0);
                SetDword(zone, "2201", 0);
            }
        }

        private static void TrustHost(string host)
        {
            using (var domains = Registry.CurrentUser.CreateSubKey(ZoneMapDomains))
            {
                if (domains == null)
                    return;

                using (var hostKey = domains.CreateSubKey(host))
                {
                    if (hostKey != null)
                        hostKey.SetValue("http", 2, RegistryValueKind.DWord);
                }
            }
        }

        private static void SetFeature(string appName, string featureName, int value)
        {
            using (var feature = Registry.CurrentUser.CreateSubKey(FeatureControlRoot + "\\" + featureName))
            {
                if (feature != null)
                    feature.SetValue(appName, value, RegistryValueKind.DWord);
            }
        }

        private static void SetDword(RegistryKey key, string name, int value) =>
            key.SetValue(name, value, RegistryValueKind.DWord);
    }
}
