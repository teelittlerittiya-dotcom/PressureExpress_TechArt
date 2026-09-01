using System;
using Cysharp.Threading.Tasks;
using Steamworks;
using Steamworks.Data;
using UnityEngine;

namespace PressureExpress.Network
{
    /// <summary>
    /// Owns everything Steam that is NOT the transport: readiness state, rich presence, the invite
    /// overlay, and the three ways Steam can ask us to join someone.
    ///
    /// Deliberately does NOT call SteamClient.Init, SteamClient.RunCallbacks or SteamClient.Shutdown.
    /// FacepunchTransport already does all three in its own Awake/Update/OnDestroy, and having a
    /// second owner is what previously killed Steam process-wide: a duplicated manager was destroyed,
    /// its transport's OnDestroy called the static SteamClient.Shutdown(), and every later
    /// SteamClient.IsValid check failed for the rest of the session.
    /// </summary>
    public class SteamService : MonoBehaviour
    {
        /// <summary>The rich presence key Steam looks for to offer "Join Game" in the friends list.</summary>
        private const string ConnectKey = "connect";

        private const string ConnectLobbyArg = "+connect_lobby";

        public static SteamService Instance { get; private set; }

        public SteamState State { get; private set; } = SteamState.Unknown;
        public NetworkMode Mode { get; private set; } = NetworkMode.LocalLoopback;
        public bool IsReady => State == SteamState.Ready;

        /// <summary>Last connect string we published, surfaced by the debug overlay.</summary>
        public string PublishedConnectString { get; private set; } = string.Empty;

        public SteamId LocalSteamId => SteamClient.IsValid ? SteamClient.SteamId : default;
        public string LocalName => SteamClient.IsValid ? SteamClient.Name : "<no steam>";

        /// <summary>
        /// Raised when Steam wants us to join a lobby: an accepted invite, a friends-list
        /// "Join Game", or the +connect_lobby launch argument.
        /// </summary>
        public event Action<SteamId> JoinRequested;

        private bool _subscribed;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance != this) return;

            Unsubscribe();
            Instance = null;

            // No SteamClient.Shutdown() here on purpose. See the class summary.
        }

        /// <summary>
        /// Waits for the transport's Awake to have initialised Steam, then warms the relay network
        /// and subscribes the join callbacks. Never throws.
        /// </summary>
        public async UniTask InitializeAsync(NetworkMode mode, float timeoutSeconds = 5f)
        {
            Mode = mode;

            if (mode == NetworkMode.LocalLoopback)
            {
                State = SteamState.Disabled;
                Debug.Log("[SteamService] LocalLoopback mode, Steam is intentionally not initialised.");
                return;
            }

            // FacepunchTransport.Awake() performs SteamClient.Init. Script execution order between
            // components is not guaranteed, so poll rather than assume it has already happened.
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!SteamClient.IsValid && Time.realtimeSinceStartup < deadline)
            {
                await UniTask.Yield();
            }

            if (!SteamClient.IsValid)
            {
                State = SteamState.NotRunning;
                Debug.LogError(
                    "[SteamService] Steam is not available. Either Steam is not running, or steam_appid.txt " +
                    "is missing next to the executable. Multiplayer will stay disabled - we never fall back " +
                    "to a loopback host, because that produces a session nobody can join.");
                return;
            }

            try
            {
                // FacepunchTransport already calls this from its InitSteamworks coroutine, but that
                // coroutine only runs after its own WaitUntil(SteamClient.IsValid) resolves. Calling
                // it here as well costs nothing and guarantees the relay is warm before we allow a
                // host or join, rather than being initialised lazily on the first connection.
                SteamNetworkingUtils.InitRelayNetworkAccess();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SteamService] InitRelayNetworkAccess failed: {e.Message}");
            }

            Subscribe();
            State = SteamState.Ready;
            Debug.Log($"[SteamService] Ready as {LocalName} ({LocalSteamId.Value}).");
        }

        private void Subscribe()
        {
            if (_subscribed) return;

            // Fired when a friend's lobby invite is accepted.
            SteamFriends.OnGameLobbyJoinRequested += HandleLobbyJoinRequested;

            // Fired when a friend clicks "Join Game" in the Shift+Tab friends list while our game is
            // already running. This is a DIFFERENT callback to the one above, and without it the
            // friends-list join silently does nothing for an already-running game.
            SteamFriends.OnGameRichPresenceJoinRequested += HandleRichPresenceJoinRequested;

            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;

            SteamFriends.OnGameLobbyJoinRequested -= HandleLobbyJoinRequested;
            SteamFriends.OnGameRichPresenceJoinRequested -= HandleRichPresenceJoinRequested;

            _subscribed = false;
        }

        private void HandleLobbyJoinRequested(Lobby lobby, SteamId friendId)
        {
            Debug.Log($"[SteamService] Lobby invite accepted for lobby {lobby.Id.Value} from {friendId.Value}.");
            JoinRequested?.Invoke(lobby.Id);
        }

        private void HandleRichPresenceJoinRequested(Friend friend, string connectString)
        {
            Debug.Log($"[SteamService] Friends-list join from {friend.Name}, connect string '{connectString}'.");

            if (TryParseConnectString(connectString, out ulong lobbyId))
            {
                JoinRequested?.Invoke(new SteamId { Value = lobbyId });
            }
            else
            {
                Debug.LogWarning($"[SteamService] Could not parse a lobby id out of '{connectString}'.");
            }
        }

        /// <summary>
        /// Publishes the connect string that makes "Join Game" appear next to our name in a friend's
        /// Steam friends list. Clients publish it too, so that THEIR friends can join through them.
        /// </summary>
        public void PublishConnectString(SteamId lobbyId)
        {
            if (!IsReady) return;

            PublishedConnectString = $"{ConnectLobbyArg} {lobbyId.Value}";
            if (!SteamFriends.SetRichPresence(ConnectKey, PublishedConnectString))
            {
                Debug.LogWarning("[SteamService] SetRichPresence(connect) was rejected by Steam.");
            }
        }

        public void ClearConnectString()
        {
            PublishedConnectString = string.Empty;
            if (!SteamClient.IsValid) return;

            SteamFriends.ClearRichPresence();
        }

        /// <summary>Opens Steam's own invite dialog for the given lobby.</summary>
        public void OpenInviteOverlay(SteamId lobbyId)
        {
            if (!IsReady)
            {
                Debug.LogWarning("[SteamService] Cannot open the invite overlay, Steam is not ready.");
                return;
            }

            SteamFriends.OpenGameInviteOverlay(lobbyId);
        }

        /// <summary>
        /// Reads the +connect_lobby argument Steam appends when the game is LAUNCHED from a friend's
        /// "Join Game" (as opposed to it already running, which goes through the callbacks above).
        /// </summary>
        public static bool TryGetLaunchLobbyId(out ulong lobbyId)
        {
            return TryParseConnectString(string.Join(" ", Environment.GetCommandLineArgs()), out lobbyId);
        }

        private static bool TryParseConnectString(string connectString, out ulong lobbyId)
        {
            lobbyId = 0;
            if (string.IsNullOrEmpty(connectString)) return false;

            string[] parts = connectString.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length - 1; i++)
            {
                if (parts[i] == ConnectLobbyArg && ulong.TryParse(parts[i + 1], out lobbyId) && lobbyId != 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
