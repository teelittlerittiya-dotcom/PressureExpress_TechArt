using System.Collections.Generic;
using UnityEngine;

namespace PressureExpress.Framework
{
    /// <summary>
    /// Display and audio settings, independent of any UI system so both the uGUI SettingsMenu and
    /// GameBootstrap can use it.
    ///
    /// Fixes a real bug in the original SettingsMenu: it read PlayerPrefs into its widgets on Start
    /// but never applied them, so a saved volume, fullscreen state or resolution was silently
    /// ignored on every launch. <see cref="Apply"/> is called once from GameBootstrap.
    /// </summary>
    public static class DisplaySettings
    {
        public const string VolumeKey = "Volume";
        public const string FullscreenKey = "Fullscreen";
        public const string ResolutionKey = "Resolution";

        public static float Volume => PlayerPrefs.GetFloat(VolumeKey, 1f);
        public static bool Fullscreen => PlayerPrefs.GetInt(FullscreenKey, 1) == 1;
        public static int SavedResolutionIndex => PlayerPrefs.GetInt(ResolutionKey, -1);

        /// <summary>Applies everything that was persisted. Call once at boot, before any menu exists.</summary>
        public static void Apply()
        {
            AudioListener.volume = Volume;

            bool fullscreen = Fullscreen;
            List<Resolution> resolutions = BuildResolutionList();
            int index = SavedResolutionIndex;

            if (index >= 0 && index < resolutions.Count)
            {
                Resolution res = resolutions[index];
                Screen.SetResolution(res.width, res.height, fullscreen);
            }
            else
            {
                Screen.fullScreen = fullscreen;
            }
        }

        public static void SetVolume(float volume)
        {
            AudioListener.volume = volume;
            PlayerPrefs.SetFloat(VolumeKey, volume);
            PlayerPrefs.Save();
        }

        public static void SetFullscreen(bool fullscreen)
        {
            Screen.fullScreen = fullscreen;
            PlayerPrefs.SetInt(FullscreenKey, fullscreen ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static void SetResolution(int index)
        {
            List<Resolution> resolutions = BuildResolutionList();
            if (index < 0 || index >= resolutions.Count) return;

            Resolution res = resolutions[index];
            Screen.SetResolution(res.width, res.height, Screen.fullScreen);
            PlayerPrefs.SetInt(ResolutionKey, index);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Screen.resolutions repeats each size once per supported refresh rate, which fills the
        /// dropdown with duplicates. Collapse to unique width x height.
        /// </summary>
        public static List<Resolution> BuildResolutionList()
        {
            var seen = new HashSet<long>();
            var result = new List<Resolution>();

            foreach (Resolution res in Screen.resolutions)
            {
                long key = ((long)res.width << 32) | (uint)res.height;
                if (seen.Add(key)) result.Add(res);
            }

            return result;
        }

        /// <summary>Index into <see cref="BuildResolutionList"/> matching the current screen size.</summary>
        public static int CurrentResolutionIndex(List<Resolution> resolutions)
        {
            for (int i = 0; i < resolutions.Count; i++)
            {
                if (resolutions[i].width == Screen.width && resolutions[i].height == Screen.height)
                {
                    return i;
                }
            }

            return 0;
        }
    }
}
