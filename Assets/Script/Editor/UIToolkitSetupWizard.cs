using System.IO;
using PressureExpress.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace PressureExpress.Editor
{
    /// <summary>
    /// Builds the two PanelSettings assets and the two UI prefabs for the UI Toolkit menu.
    ///
    /// The UXML, USS and TSS are authored by hand and live in Assets/UI. Only the binary bits that
    /// cannot be written as text (PanelSettings, prefabs) are generated here.
    ///
    /// Safe to re-run: existing assets are updated in place rather than duplicated.
    /// </summary>
    public static class UIToolkitSetupWizard
    {
        private const string UIFolder = "Assets/UI";
        private const string PrefabFolder = "Assets/Prefab/UI";

        private const string ThemePath = UIFolder + "/PressureExpressTheme.tss";
        private const string MenuPanelSettingsPath = UIFolder + "/PressureExpressPanelSettings.asset";
        private const string OverlayPanelSettingsPath = UIFolder + "/PressureExpressOverlayPanelSettings.asset";

        private const string MainMenuUxml = UIFolder + "/MainMenu.uxml";
        private const string SettingsUxml = UIFolder + "/SettingsPanel.uxml";
        private const string SessionUxml = UIFolder + "/SessionPanel.uxml";

        private const string MainMenuPrefabPath = PrefabFolder + "/UI_MainMenu.prefab";
        private const string SessionPrefabPath = PrefabFolder + "/UI_SessionPanel.prefab";

        [MenuItem("Tools/PressureExpress/Create UI Toolkit Setup")]
        public static void CreateUIToolkitSetup()
        {
            EnsureFolder(PrefabFolder);

            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            if (theme == null)
            {
                Debug.LogError($"[UISetup] Could not load {ThemePath}. Let Unity import Assets/UI first, then re-run.");
                return;
            }

            PanelSettings menuPanel = CreateOrUpdatePanelSettings(MenuPanelSettingsPath, theme, sortingOrder: 0f);
            PanelSettings overlayPanel = CreateOrUpdatePanelSettings(OverlayPanelSettingsPath, theme, sortingOrder: 10f);

            var mainMenuUxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(MainMenuUxml);
            var settingsUxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SettingsUxml);
            var sessionUxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SessionUxml);

            if (mainMenuUxml == null || settingsUxml == null || sessionUxml == null)
            {
                Debug.LogError("[UISetup] One or more UXML files failed to load from Assets/UI. " +
                               "Check the console for UXML import errors and re-run.");
                return;
            }

            CreateMainMenuPrefab(menuPanel, mainMenuUxml, settingsUxml);
            CreateSessionPrefab(overlayPanel, sessionUxml, settingsUxml);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                "[UISetup] Done.\n" +
                $"  {MenuPanelSettingsPath}\n" +
                $"  {OverlayPanelSettingsPath}\n" +
                $"  {MainMenuPrefabPath}\n" +
                $"  {SessionPrefabPath}");

            EditorUtility.DisplayDialog(
                "UI Toolkit setup complete",
                "Created both PanelSettings assets and the UI_MainMenu / UI_SessionPanel prefabs.\n\n" +
                "Still to do:\n" +
                "1. Drop UI_MainMenu into MainMenu.unity and delete the old uGUI menu Canvas.\n" +
                "2. Assign UI_SessionPanel to GameBootstrap's 'Session UI Prefab' field in Bootstrap.unity.\n" +
                "3. Optional: add MMF_Player components to either prefab and hook them to the " +
                "Feel fields on the view scripts.",
                "OK");
        }

        private static PanelSettings CreateOrUpdatePanelSettings(string path, ThemeStyleSheet theme, float sortingOrder)
        {
            var settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(path);
            bool created = false;

            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PanelSettings>();
                created = true;
            }

            settings.themeStyleSheet = theme;

            // Scale with the screen so the pixel-art 9-slices stay proportional across resolutions.
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1920, 1080);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f;
            settings.sortingOrder = sortingOrder;

            if (created)
            {
                AssetDatabase.CreateAsset(settings, path);
            }
            else
            {
                EditorUtility.SetDirty(settings);
            }

            Debug.Log($"[UISetup] {(created ? "Created" : "Updated")} {path}");
            return settings;
        }

        private static void CreateMainMenuPrefab(PanelSettings panel, VisualTreeAsset menuUxml, VisualTreeAsset settingsUxml)
        {
            var root = new GameObject("UI_MainMenu");

            try
            {
                var document = root.AddComponent<UIDocument>();
                document.panelSettings = panel;
                document.visualTreeAsset = menuUxml;

                var view = root.AddComponent<MainMenuView>();
                AssignTemplate(view, settingsUxml);

                PrefabUtility.SaveAsPrefabAsset(root, MainMenuPrefabPath);
                Debug.Log($"[UISetup] Wrote {MainMenuPrefabPath}");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void CreateSessionPrefab(PanelSettings panel, VisualTreeAsset sessionUxml, VisualTreeAsset settingsUxml)
        {
            var root = new GameObject("UI_SessionPanel");

            try
            {
                var document = root.AddComponent<UIDocument>();
                document.panelSettings = panel;
                document.visualTreeAsset = sessionUxml;

                var view = root.AddComponent<SessionPanelView>();
                AssignTemplate(view, settingsUxml);

                PrefabUtility.SaveAsPrefabAsset(root, SessionPrefabPath);
                Debug.Log($"[UISetup] Wrote {SessionPrefabPath}");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>settingsTemplate is a private [SerializeField], so go through SerializedObject.</summary>
        private static void AssignTemplate(Component view, VisualTreeAsset template)
        {
            var so = new SerializedObject(view);
            SerializedProperty property = so.FindProperty("settingsTemplate");

            if (property == null)
            {
                Debug.LogWarning($"[UISetup] {view.GetType().Name} has no 'settingsTemplate' field.");
                return;
            }

            property.objectReferenceValue = template;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;

            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            string leaf = Path.GetFileName(folder);

            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
