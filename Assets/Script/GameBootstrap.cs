using System;
using Cysharp.Threading.Tasks;
using PressureExpress.Framework;
using PressureExpress.Network;
using Steamworks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Sole owner of every persistent manager. Lives in the Bootstrap scene (build index 0) and nowhere
/// else, so no other scene may contain a NetworkManager, a SteamService or a SessionService.
///
/// This replaces AppicationManager, whose SpawnManagerObj cloned a manager that was already alive in
/// MainMenu. The clone destroyed itself, and on the way out its FacepunchTransport.OnDestroy called
/// the static SteamClient.Shutdown() - killing Steam for the surviving instance and silently routing
/// every host and join to a loopback host nobody could reach.
/// </summary>
public class GameBootstrap : MonoBehaviour
{
    [Header("Network prefabs - exactly one is instantiated, chosen by mode")]
    [Tooltip("UnityTransport only. Must NOT have a FacepunchTransport, or Steam will initialise in the Editor.")]
    [SerializeField] private NetworkManager localNetworkPrefab;
    [Tooltip("FacepunchTransport only.")]
    [SerializeField] private NetworkManager steamNetworkPrefab;

    [Header("Services")]
    [SerializeField] private SteamService steamServicePrefab;
    [SerializeField] private SessionService sessionServicePrefab;
    [SerializeField] private NetworkDebugOverlay debugOverlayPrefab;

    [Tooltip("Optional. A persistent pause/session UI kept alive across scenes. Leave empty if you " +
             "put your SessionPanel on the in-game canvas in MainLevel instead.")]
    [SerializeField] private GameObject sessionUIPrefab;

    [Header("Optional managers - never allowed to block or fail the boot")]
    [SerializeField] private AnalyticManager analyticManagerPrefab;
    [SerializeField] private DiscordManager discordManagerPrefab;
    [SerializeField] private VivoxManager voiceManagerPrefab;

    [Header("Flow")]
    [SerializeField] private string mainMenuScene = "MainMenu";
    [SerializeField] private float steamInitTimeoutSeconds = 5f;

    private static bool _hasBooted;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _hasBooted = false;
    }

    private void Awake()
    {
        if (_hasBooted)
        {
            // Bootstrap is only ever entered once. If the scene is somehow reloaded, do nothing
            // rather than spawning a second set of managers.
            Destroy(gameObject);
            return;
        }

        _hasBooted = true;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        BootAsync().Forget();
    }

    private async UniTaskVoid BootAsync()
    {
        NetworkMode mode = ResolveMode();
        Debug.Log($"[GameBootstrap] Booting in {mode} mode.");

        // SettingsMenu loaded saved values into its widgets but never applied them, so
        // volume/fullscreen/resolution were silently ignored every launch. Apply them first.
        DisplaySettings.Apply();

        EnsureUpdateManager();

        NetworkManager networkManager = InstantiateNetworkManager(mode);

        SteamService steam = Instantiate(steamServicePrefab);
        SessionService session = Instantiate(sessionServicePrefab);

        if (debugOverlayPrefab != null)
        {
            Instantiate(debugOverlayPrefab);
        }

        if (sessionUIPrefab != null)
        {
            GameObject sessionUI = Instantiate(sessionUIPrefab);
            DontDestroyOnLoad(sessionUI);
        }

        // The only awaited step. Steam's own init is synchronous inside the transport's Awake, so
        // this costs nothing when Steam is running and gives a definitive answer when it is not.
        await steam.InitializeAsync(mode, steamInitTimeoutSeconds);

        session.Initialize(mode, steam, networkManager);

        SpawnOptionalManagers();

        await SceneManager.LoadSceneAsync(mainMenuScene, LoadSceneMode.Single).ToUniTask();

        TryAutoJoinFromLaunchArguments(mode, steam, session);
    }

    /// <summary>
    /// Decided once, here, and never revisited. Builds are Steam only: if Steam is unavailable the
    /// UI says so rather than quietly starting a loopback host that nobody in the world can join.
    /// </summary>
    private static NetworkMode ResolveMode()
    {
        return Application.isEditor ? NetworkMode.LocalLoopback : NetworkMode.Steam;
    }

    private NetworkManager InstantiateNetworkManager(NetworkMode mode)
    {
        NetworkManager prefab = mode == NetworkMode.Steam ? steamNetworkPrefab : localNetworkPrefab;

        if (prefab == null)
        {
            Debug.LogError($"[GameBootstrap] No NetworkManager prefab assigned for {mode} mode.");
            return null;
        }

        NetworkManager instance = Instantiate(prefab);
        DontDestroyOnLoad(instance.gameObject);
        return instance;
    }

    private static void EnsureUpdateManager()
    {
        if (UpdateManager.Instance != null) return;

        var go = new GameObject("UpdateManager");
        go.AddComponent<UpdateManager>();
    }

    private void SpawnOptionalManagers()
    {
        // Each of these self-initialises in its own Start and routes UnityServices through
        // UnityServicesBootstrap, so nothing here is awaited and nothing can throw into the boot.
        SpawnSafely(analyticManagerPrefab, "AnalyticManager");
        SpawnSafely(voiceManagerPrefab, "VivoxManager");

        DiscordManager discord = SpawnSafely(discordManagerPrefab, "DiscordManager");
        if (discord != null)
        {
            try
            {
                discord.InitDiscord().Forget();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GameBootstrap] Discord initialisation failed: {e.Message}");
            }
        }
    }

    private static T SpawnSafely<T>(T prefab, string label) where T : MonoBehaviour
    {
        if (prefab == null)
        {
            Debug.LogWarning($"[GameBootstrap] No {label} prefab assigned, skipping.");
            return null;
        }

        try
        {
            return Instantiate(prefab);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[GameBootstrap] Could not spawn {label}: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// Steam appends "+connect_lobby &lt;id&gt;" when the game is launched from a friend's
    /// "Join Game". Handled after the menu has loaded so a failure has somewhere to be shown.
    /// </summary>
    private static void TryAutoJoinFromLaunchArguments(NetworkMode mode, SteamService steam, SessionService session)
    {
        if (mode != NetworkMode.Steam || steam == null || !steam.IsReady) return;
        if (!SteamService.TryGetLaunchLobbyId(out ulong lobbyId)) return;

        Debug.Log($"[GameBootstrap] Launched with +connect_lobby {lobbyId}, joining.");
        session.JoinLobbyAsync(new SteamId { Value = lobbyId }).Forget();
    }
}
