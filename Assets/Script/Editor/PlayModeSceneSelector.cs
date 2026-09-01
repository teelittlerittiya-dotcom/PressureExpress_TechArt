using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PressureExpress.Editor
{
    /// <summary>
    /// Forces Play Mode to start from the Bootstrap scene.
    ///
    /// This used to point at MainMenu. Now that every persistent manager is owned by Bootstrap and
    /// MainMenu contains none of them, entering play mode on any other scene would run with no
    /// NetworkManager, no SteamService and no SessionService at all.
    /// </summary>
    [InitializeOnLoad]
    public static class PlayModeSceneSelector
    {
        private const string MenuPath = "Tools/Always Start From Bootstrap";
        private const string SettingKey = "AlwaysStartFromBootstrapEnabled";
        private const string BootstrapPath = "Assets/Scenes/Bootstrap.unity";

        static PlayModeSceneSelector()
        {
            // delayCall ensures Unity's database and EditorPrefs are fully loaded before execution
            EditorApplication.delayCall += ApplySetting;
        }

        private static void ApplySetting()
        {
            bool isEnabled = EditorPrefs.GetBool(SettingKey, true);
            if (isEnabled)
            {
                SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(BootstrapPath);
                if (sceneAsset != null)
                {
                    EditorSceneManager.playModeStartScene = sceneAsset;
                }
                else
                {
                    EditorSceneManager.playModeStartScene = null;
                    Debug.LogWarning($"[PlayModeSceneSelector] Bootstrap scene not found at path: {BootstrapPath}. " +
                                     "Create it (see the restructure notes) or play mode will start without any managers.");
                }
            }
            else
            {
                EditorSceneManager.playModeStartScene = null;
            }
        }

        [MenuItem(MenuPath)]
        private static void ToggleAction()
        {
            bool isEnabled = EditorPrefs.GetBool(SettingKey, true);
            bool newState = !isEnabled;
            EditorPrefs.SetBool(SettingKey, newState);
            ApplySetting();
            Debug.Log($"[PlayModeSceneSelector] 'Always Start From Bootstrap' is now {(newState ? "ENABLED" : "DISABLED")}.");
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleActionValidate()
        {
            Menu.SetChecked(MenuPath, EditorPrefs.GetBool(SettingKey, true));
            return true;
        }
    }
}
