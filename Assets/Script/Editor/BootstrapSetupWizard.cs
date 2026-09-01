using System.Collections.Generic;
using System.IO;
using System.Linq;
using Netcode.Transports.Facepunch;
using PressureExpress.Network;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PressureExpress.Editor
{
    /// <summary>
    /// One shot setup for the Bootstrap restructure.
    ///
    /// Doing this by hand is where the original bug came from: a manager existed both as a scene
    /// object and as a prefab asset with different serialized values, and nothing made that visible.
    /// Running this menu item produces the two single-transport NetworkManager prefabs, the service
    /// prefabs, and a Bootstrap scene with everything already wired.
    ///
    /// Safe to re-run: existing assets are overwritten in place.
    /// </summary>
    public static class BootstrapSetupWizard
    {
        private const string PrefabFolder = "Assets/Prefab/Bootstrap";
        private const string ScenesFolder = "Assets/Scenes";

        private const string SourceNetworkPrefab = "Assets/Prefab/AppicationManager/[Steam] NetworkManeger.prefab";
        private const string VoicePrefab = "Assets/Prefab/AppicationManager/VoiceMenager.prefab";

        private const string LocalNetworkPrefabPath = PrefabFolder + "/[Local] NetworkManager.prefab";
        private const string SteamNetworkPrefabPath = PrefabFolder + "/[Steam] NetworkManager.prefab";
        private const string SteamServicePrefabPath = PrefabFolder + "/SteamService.prefab";
        private const string SessionServicePrefabPath = PrefabFolder + "/SessionService.prefab";
        private const string DebugOverlayPrefabPath = PrefabFolder + "/NetworkDebugOverlay.prefab";
        private const string BootstrapScenePath = ScenesFolder + "/Bootstrap.unity";

        private const int DefaultMaxPlayers = 4;

        [MenuItem("Tools/PressureExpress/Create Bootstrap Setup")]
        public static void CreateBootstrapSetup()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[BootstrapSetup] Cancelled, current scene has unsaved changes.");
                return;
            }

            EnsureFolder(PrefabFolder);

            NetworkConfigSnapshot config = ReadSourceConfig();

            GameObject localPrefab = CreateNetworkPrefab(LocalNetworkPrefabPath, config, steam: false);
            GameObject steamPrefab = CreateNetworkPrefab(SteamNetworkPrefabPath, config, steam: true);

            GameObject steamService = CreateServicePrefab<SteamService>(SteamServicePrefabPath, "SteamService");
            GameObject sessionService = CreateServicePrefab<SessionService>(SessionServicePrefabPath, "SessionService");
            GameObject debugOverlay = CreateServicePrefab<NetworkDebugOverlay>(DebugOverlayPrefabPath, "NetworkDebugOverlay");

            ConfigureSessionServicePrefab(sessionService);

            CreateBootstrapScene(localPrefab, steamPrefab, steamService, sessionService, debugOverlay);

            RegisterBuildScenes();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                "[BootstrapSetup] Done.\n" +
                $"  {LocalNetworkPrefabPath}  (UnityTransport only)\n" +
                $"  {SteamNetworkPrefabPath}  (FacepunchTransport only)\n" +
                $"  {BootstrapScenePath}      (build index 0)\n" +
                "Remaining manual steps are listed in the restructure notes: strip managers out of " +
                "MainMenu, and remove the stray NetworkManager from MainLevel.");

            EditorUtility.DisplayDialog(
                "Bootstrap setup complete",
                "Created the two NetworkManager prefabs, the service prefabs and Assets/Scenes/Bootstrap.unity, " +
                "and put Bootstrap at build index 0.\n\n" +
                "Still to do by hand:\n" +
                "1. Delete the [Steam] NetworkManeger instance and both AppicationManager objects from MainMenu.\n" +
                "2. Delete the NetworkManager prefab instance from MainLevel.\n" +
                "3. Run Tools > PressureExpress > Create UI Toolkit Setup for the menu and session UI.",
                "OK");
        }

        #region Network prefabs

        private struct NetworkConfigSnapshot
        {
            public List<NetworkPrefabsList> PrefabLists;
            public uint TickRate;
            public int ClientConnectionBufferTimeout;
            public bool EnableSceneManagement;
            public int LoadSceneTimeOut;
            public float SpawnTimeout;
        }

        /// <summary>
        /// Copies the parts of the old NetworkConfig that matter, so the network prefab list and
        /// timings do not silently reset to defaults.
        /// </summary>
        private static NetworkConfigSnapshot ReadSourceConfig()
        {
            var snapshot = new NetworkConfigSnapshot
            {
                PrefabLists = new List<NetworkPrefabsList>(),
                TickRate = 30,
                ClientConnectionBufferTimeout = 10,
                EnableSceneManagement = true,
                LoadSceneTimeOut = 120,
                SpawnTimeout = 10f
            };

            var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourceNetworkPrefab);
            if (source == null)
            {
                Debug.LogWarning($"[BootstrapSetup] Could not find {SourceNetworkPrefab}, using default NetworkConfig values.");
                return snapshot;
            }

            var manager = source.GetComponent<NetworkManager>();
            if (manager == null || manager.NetworkConfig == null)
            {
                Debug.LogWarning("[BootstrapSetup] Source prefab has no NetworkManager, using default NetworkConfig values.");
                return snapshot;
            }

            NetworkConfig config = manager.NetworkConfig;
            snapshot.TickRate = config.TickRate;
            snapshot.ClientConnectionBufferTimeout = config.ClientConnectionBufferTimeout;
            snapshot.EnableSceneManagement = config.EnableSceneManagement;
            snapshot.LoadSceneTimeOut = config.LoadSceneTimeOut;
            snapshot.SpawnTimeout = config.SpawnTimeout;

            if (config.Prefabs != null && config.Prefabs.NetworkPrefabsLists != null)
            {
                snapshot.PrefabLists.AddRange(config.Prefabs.NetworkPrefabsLists.Where(l => l != null));
            }

            if (snapshot.PrefabLists.Count == 0)
            {
                Debug.LogWarning("[BootstrapSetup] Source prefab had no NetworkPrefabsLists. Spawning networked " +
                                 "objects will fail until one is assigned.");
            }

            return snapshot;
        }

        private static GameObject CreateNetworkPrefab(string path, NetworkConfigSnapshot snapshot, bool steam)
        {
            var root = new GameObject(steam ? "[Steam] NetworkManager" : "[Local] NetworkManager");

            try
            {
                var manager = root.AddComponent<NetworkManager>();

                // Exactly one transport per prefab. Having both on one object is what allowed the old
                // code to swap transports at runtime, and it meant FacepunchTransport.Awake ran (and
                // initialised Steam) even in the Editor where the component was disabled.
                NetworkTransport transport = steam
                    ? (NetworkTransport)root.AddComponent<FacepunchTransport>()
                    : root.AddComponent<UnityTransport>();

                NetworkConfig config = manager.NetworkConfig;
                config.NetworkTransport = transport;

                // PlayerSpawner owns player spawning, so NGO must not auto-create one.
                config.PlayerPrefab = null;

                config.ConnectionApproval = true;
                config.TickRate = snapshot.TickRate;
                config.ClientConnectionBufferTimeout = snapshot.ClientConnectionBufferTimeout;
                config.EnableSceneManagement = snapshot.EnableSceneManagement;
                config.LoadSceneTimeOut = snapshot.LoadSceneTimeOut;
                config.SpawnTimeout = snapshot.SpawnTimeout;

                if (config.Prefabs != null && snapshot.PrefabLists.Count > 0)
                {
                    config.Prefabs.NetworkPrefabsLists = new List<NetworkPrefabsList>(snapshot.PrefabLists);
                }

                manager.RunInBackground = true;

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"[BootstrapSetup] Wrote {path}");
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        #endregion

        #region Service prefabs

        private static GameObject CreateServicePrefab<T>(string path, string objectName) where T : Component
        {
            var root = new GameObject(objectName);

            try
            {
                root.AddComponent<T>();
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"[BootstrapSetup] Wrote {path}");
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// The old prefab asset carried roomSize = 0 while only the scene override had 4, so hosting
        /// from the asset would have called CreateLobbyAsync(0). Set the values explicitly here.
        /// </summary>
        private static void ConfigureSessionServicePrefab(GameObject prefab)
        {
            if (prefab == null) return;

            var session = prefab.GetComponent<SessionService>();
            if (session == null) return;

            var so = new SerializedObject(session);
            SetIfPresent(so, "gameSceneName", "MainLevel");
            SetIfPresent(so, "menuSceneName", "MainMenu");
            SetIntIfPresent(so, "maxPlayers", DefaultMaxPlayers);
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(prefab);
        }

        private static void SetIfPresent(SerializedObject so, string field, string value)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p != null) p.stringValue = value;
        }

        private static void SetIntIfPresent(SerializedObject so, string field, int value)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p != null) p.intValue = value;
        }

        #endregion

        #region Bootstrap scene

        private static void CreateBootstrapScene(GameObject localPrefab, GameObject steamPrefab,
                                                 GameObject steamService, GameObject sessionService,
                                                 GameObject debugOverlay)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var root = new GameObject("GameBootstrap");
            var bootstrap = root.AddComponent<GameBootstrap>();

            var so = new SerializedObject(bootstrap);

            AssignComponent<NetworkManager>(so, "localNetworkPrefab", localPrefab);
            AssignComponent<NetworkManager>(so, "steamNetworkPrefab", steamPrefab);
            AssignComponent<SteamService>(so, "steamServicePrefab", steamService);
            AssignComponent<SessionService>(so, "sessionServicePrefab", sessionService);
            AssignComponent<NetworkDebugOverlay>(so, "debugOverlayPrefab", debugOverlay);

            AssignAssetComponent<AnalyticManager>(so, "analyticManagerPrefab",
                FindPrefabWithComponent<AnalyticManager>());
            AssignAssetComponent<DiscordManager>(so, "discordManagerPrefab",
                FindPrefabWithComponent<DiscordManager>());
            AssignAssetComponent<VivoxManager>(so, "voiceManagerPrefab",
                AssetDatabase.LoadAssetAtPath<GameObject>(VoicePrefab));

            SetIfPresent(so, "mainMenuScene", "MainMenu");
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, BootstrapScenePath);
            Debug.Log($"[BootstrapSetup] Wrote {BootstrapScenePath}");
        }

        private static void AssignComponent<T>(SerializedObject so, string field, GameObject prefab) where T : Component
        {
            SerializedProperty p = so.FindProperty(field);
            if (p == null)
            {
                Debug.LogWarning($"[BootstrapSetup] GameBootstrap has no serialized field '{field}'.");
                return;
            }

            p.objectReferenceValue = prefab != null ? prefab.GetComponent<T>() : null;
        }

        private static void AssignAssetComponent<T>(SerializedObject so, string field, GameObject prefab) where T : Component
        {
            if (prefab == null)
            {
                Debug.LogWarning($"[BootstrapSetup] No prefab found for '{field}'. Assign it by hand in Bootstrap.");
                return;
            }

            AssignComponent<T>(so, field, prefab);
        }

        private static GameObject FindPrefabWithComponent<T>() where T : Component
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go != null && go.GetComponent<T>() != null) return go;
            }

            return null;
        }

        #endregion

        #region Build settings

        /// <summary>
        /// Bootstrap must be index 0, and Lobby.unity is dropped: it is unused by the flow and
        /// carries its own NetworkManager, which would collide with the persistent one if loaded.
        /// </summary>
        private static void RegisterBuildScenes()
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();

            scenes.RemoveAll(s => s.path == BootstrapScenePath);
            scenes.RemoveAll(s => s.path == ScenesFolder + "/Lobby.unity");

            scenes.Insert(0, new EditorBuildSettingsScene(BootstrapScenePath, true));

            EditorBuildSettings.scenes = scenes.ToArray();

            Debug.Log("[BootstrapSetup] Build settings updated: Bootstrap is index 0, Lobby.unity removed.");
        }

        #endregion

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
