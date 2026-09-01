using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PressureExpress.Editor
{
    /// <summary>
    /// Writes steam_appid.txt next to the built player.
    ///
    /// Without a Steamworks partner appid the game cannot be launched through Steam, so builds are
    /// started by running the exe directly. In that case SteamAPI_Init REQUIRES steam_appid.txt to
    /// sit beside the executable. Its absence was silently disabling Steam in every build, which is
    /// exactly the kind of thing that is invisible until someone tries to invite a friend.
    /// </summary>
    public class SteamAppIdPostBuild : IPostprocessBuildWithReport
    {
        private const string FileName = "steam_appid.txt";
        private const string FallbackAppId = "480";

        public int callbackOrder => 0;

        public void OnPostprocessBuild(BuildReport report)
        {
            var summary = report.summary;

            if (summary.platform != BuildTarget.StandaloneWindows64 &&
                summary.platform != BuildTarget.StandaloneWindows &&
                summary.platform != BuildTarget.StandaloneLinux64 &&
                summary.platform != BuildTarget.StandaloneOSX)
            {
                return;
            }

            try
            {
                string outputPath = summary.outputPath;
                if (string.IsNullOrEmpty(outputPath))
                {
                    Debug.LogWarning($"[SteamAppIdPostBuild] Build report has no output path, skipping {FileName}.");
                    return;
                }

                // On Windows/Linux outputPath is the executable; on macOS it is the .app bundle.
                // The parent directory is the correct destination in both cases.
                string targetDir = Path.GetDirectoryName(outputPath);
                if (string.IsNullOrEmpty(targetDir) || !Directory.Exists(targetDir))
                {
                    Debug.LogWarning($"[SteamAppIdPostBuild] Could not resolve build directory from '{outputPath}', skipping {FileName}.");
                    return;
                }

                string appId = ReadProjectAppId();
                string destination = Path.Combine(targetDir, FileName);

                // No trailing newline: Steam parses this file strictly.
                File.WriteAllText(destination, appId);

                Debug.Log($"[SteamAppIdPostBuild] Wrote {FileName} (appid {appId}) to {destination}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SteamAppIdPostBuild] Failed to write {FileName}: {e}");
            }
        }

        /// <summary>
        /// The project root copy is the single source of truth, so switching to a real Steamworks
        /// appid later means editing one file rather than hunting call sites.
        /// </summary>
        private static string ReadProjectAppId()
        {
            try
            {
                string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
                if (!string.IsNullOrEmpty(projectRoot))
                {
                    string source = Path.Combine(projectRoot, FileName);
                    if (File.Exists(source))
                    {
                        string value = File.ReadAllText(source).Trim();
                        if (!string.IsNullOrEmpty(value)) return value;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SteamAppIdPostBuild] Could not read project {FileName}: {e.Message}");
            }

            Debug.LogWarning($"[SteamAppIdPostBuild] No usable {FileName} at the project root, falling back to {FallbackAppId}.");
            return FallbackAppId;
        }
    }
}
