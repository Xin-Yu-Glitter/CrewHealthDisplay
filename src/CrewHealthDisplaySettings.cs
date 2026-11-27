using Barotrauma;
using Microsoft.Xna.Framework;
using System;
using System.Globalization;
using System.IO;
using System.Xml.Linq;

namespace CrewHealthDisplayMod
{
    public static class CrewHealthDisplaySettings
    {
        public const float DefaultDisplayScale = 1.25f;
        public const float DefaultPositionX = 1.0f;
        public const float DefaultPositionY = 0.0f;
        public const float MinDisplayScale = 0.85f;
        public const float MaxDisplayScale = 1.75f;
        public const float DisplayScaleStep = 0.05f;
        public const bool DefaultHideAICrew = false;
        public const bool DefaultHideDeadCrew = false;
        public const bool DefaultHideHealthyCrew = false;
        public const bool DefaultAlwaysShowSelf = true;
        public const int DefaultHideHealthyThresholdPercent = 50;
        public const int MinHideHealthyThresholdPercent = 1;
        public const int MaxHideHealthyThresholdPercent = 100;

        public static float DisplayScale { get; private set; } = DefaultDisplayScale;
        public static float PositionX { get; private set; } = DefaultPositionX;
        public static float PositionY { get; private set; } = DefaultPositionY;
        public static bool HideAICrew { get; private set; } = DefaultHideAICrew;
        public static bool HideDeadCrew { get; private set; } = DefaultHideDeadCrew;
        public static bool HideHealthyCrew { get; private set; } = DefaultHideHealthyCrew;
        public static bool AlwaysShowSelf { get; private set; } = DefaultAlwaysShowSelf;
        public static int HideHealthyThresholdPercent { get; private set; } = DefaultHideHealthyThresholdPercent;

        public static void LoadConfig()
        {
            try
            {
                string path = GetConfigPath();
                if (!File.Exists(path)) { return; }

                XDocument doc = XDocument.Load(path);
                string scaleText = doc.Root?.Attribute("DisplayScale")?.Value;
                if (float.TryParse(scaleText, NumberStyles.Float, CultureInfo.InvariantCulture, out float loadedScale))
                {
                    DisplayScale = ClampAndSnapScale(loadedScale);
                }

                string xText = doc.Root?.Attribute("PositionX")?.Value;
                if (float.TryParse(xText, NumberStyles.Float, CultureInfo.InvariantCulture, out float loadedX))
                {
                    PositionX = MathHelper.Clamp(loadedX, 0.0f, 1.0f);
                }

                string yText = doc.Root?.Attribute("PositionY")?.Value;
                if (float.TryParse(yText, NumberStyles.Float, CultureInfo.InvariantCulture, out float loadedY))
                {
                    PositionY = MathHelper.Clamp(loadedY, 0.0f, 1.0f);
                }

                string hideAIText = doc.Root?.Attribute("HideAICrew")?.Value;
                if (bool.TryParse(hideAIText, out bool loadedHideAICrew))
                {
                    HideAICrew = loadedHideAICrew;
                }

                string hideDeadText = doc.Root?.Attribute("HideDeadCrew")?.Value;
                if (bool.TryParse(hideDeadText, out bool loadedHideDeadCrew))
                {
                    HideDeadCrew = loadedHideDeadCrew;
                }

                string hideHealthyText = doc.Root?.Attribute("HideHealthyCrew")?.Value;
                if (bool.TryParse(hideHealthyText, out bool loadedHideHealthyCrew))
                {
                    HideHealthyCrew = loadedHideHealthyCrew;
                }

                string alwaysShowSelfText = doc.Root?.Attribute("AlwaysShowSelf")?.Value;
                if (bool.TryParse(alwaysShowSelfText, out bool loadedAlwaysShowSelf))
                {
                    AlwaysShowSelf = loadedAlwaysShowSelf;
                }

                string hideHealthyThresholdText = doc.Root?.Attribute("HideHealthyThresholdPercent")?.Value;
                if (int.TryParse(hideHealthyThresholdText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int loadedHideHealthyThreshold))
                {
                    HideHealthyThresholdPercent = ClampInt(loadedHideHealthyThreshold, MinHideHealthyThresholdPercent, MaxHideHealthyThresholdPercent);
                }
            }
            catch (Exception ex)
            {
                CrewHealthDisplay.Log($"Failed to load config; using defaults. {ex.Message}");
                Reset(save: false);
            }
        }

        public static void Apply(float displayScale, float positionX, float positionY, bool hideAICrew, bool hideDeadCrew, bool hideHealthyCrew, bool alwaysShowSelf, int hideHealthyThresholdPercent, bool save)
        {
            DisplayScale = ClampAndSnapScale(displayScale);
            PositionX = MathHelper.Clamp(positionX, 0.0f, 1.0f);
            PositionY = MathHelper.Clamp(positionY, 0.0f, 1.0f);
            HideAICrew = hideAICrew;
            HideDeadCrew = hideDeadCrew;
            HideHealthyCrew = hideHealthyCrew;
            AlwaysShowSelf = alwaysShowSelf;
            HideHealthyThresholdPercent = ClampInt(hideHealthyThresholdPercent, MinHideHealthyThresholdPercent, MaxHideHealthyThresholdPercent);
            if (save)
            {
                SaveConfig();
            }
        }

        public static void Reset(bool save)
        {
            Apply(DefaultDisplayScale, DefaultPositionX, DefaultPositionY, DefaultHideAICrew, DefaultHideDeadCrew, DefaultHideHealthyCrew, DefaultAlwaysShowSelf, DefaultHideHealthyThresholdPercent, save);
        }

        public static string GetScaleLabel()
        {
            return $"{DisplayScale:0.00}x";
        }

        private static float ClampAndSnapScale(float value)
        {
            float clamped = MathHelper.Clamp(value, MinDisplayScale, MaxDisplayScale);
            return (float)Math.Round(clamped / DisplayScaleStep) * DisplayScaleStep;
        }

        private static int ClampInt(int value, int min, int max)
        {
            return Math.Max(min, Math.Min(max, value));
        }

        private static void SaveConfig()
        {
            try
            {
                string path = GetConfigPath();
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                XDocument doc = new XDocument(
                    new XElement("CrewHealthDisplayConfig",
                        new XAttribute("DisplayScale", DisplayScale.ToString("0.00", CultureInfo.InvariantCulture)),
                        new XAttribute("PositionX", PositionX.ToString("0.000", CultureInfo.InvariantCulture)),
                        new XAttribute("PositionY", PositionY.ToString("0.000", CultureInfo.InvariantCulture)),
                        new XAttribute("HideAICrew", HideAICrew),
                        new XAttribute("HideDeadCrew", HideDeadCrew),
                        new XAttribute("HideHealthyCrew", HideHealthyCrew),
                        new XAttribute("AlwaysShowSelf", AlwaysShowSelf),
                        new XAttribute("HideHealthyThresholdPercent", HideHealthyThresholdPercent)));
                doc.Save(path);
            }
            catch (Exception ex)
            {
                CrewHealthDisplay.Log($"Failed to save config. {ex.Message}");
            }
        }

        private static string GetConfigPath()
        {
            return Path.Combine(SaveUtil.DefaultSaveFolder, "CrewHealthDisplay", "CrewHealthDisplayConfig.xml");
        }
    }
}
