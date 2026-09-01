using System;
using System.Text;
using Cysharp.Threading.Tasks;
using Netcode.Transports.Facepunch;
using Steamworks;
using Steamworks.Data;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PressureExpress.Network
{
    /// <summary>
    /// Owns a multiplayer session end to end: hosting, joining, authorising incoming connections,
    /// loading the game scene and tearing everything down again.
    ///
    /// Every flow here is await driven. The old code raced an awaited CreateLobbyAsync against an
    /// OnLobbyCreated callback and an OnLobbyEntered callback, which is why the room code was
    /// sometimes written after the lobby had already been published, and why the host could decide
    /// it was a client. There is exactly one path through each operation now.
    /// </summary>
    public class SessionService : MonoBehaviour
    {
        // Lobby data keys. The game key exists because on appid 480 the lobby list is the shared
        // global Spacewar pool: without it, a stranger whose lobby happens to carry the same code
        // key would be returned by our query.
        private const string GameKey = "px_game";
        private const string GameValue = "PressureExpress";
        private const string CodeKey = "px_code";

        private const int CodeAttempts = 5;

        public static SessionService Instance { get; private set; }

        [Header("Scenes")]
        [SerializeField] private string gameSceneName = "MainLevel";
        [SerializeField] private string menuSceneName = "MainMenu";

        [Header("Session")]
        [SerializeField] private int maxPlayers = 4;
        [SerializeField] private float connectTimeoutSeconds = 12f;
        [SerializeField] private float shutdownTimeoutSeconds = 5f;

        public NetworkMode Mode { get; private set; } = NetworkMode.LocalLoopback;
        public SessionState State { get; private set; } = SessionState.Idle;
        public string CurrentCode { get; private set; } = string.Empty;
        public Lobby? CurrentLobby { get; private set; }
        public bool HasLobby => CurrentLobby.HasValue;

        public event Action<SessionState> StateChanged;
        public event Action<string> StatusChanged;

        private SteamService _steam;
        private NetworkManager _networkManager;
        private FacepunchTransport _facepunch;
        private UnityTransport _unityTransport;
        private bool _disconnectHooked;

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

            UnhookDisconnect();
            if (_steam != null) _steam.JoinRequested -= HandleSteamJoinRequested;
            Instance = null;
        }

        public void Initialize(NetworkMode mode, SteamService steam, NetworkManager networkManager)
        {
            Mode = mode;
            _steam = steam;
            _networkManager = networkManager;

            if (_networkManager == null)
            {
                Debug.LogError("[SessionService] No NetworkManager supplied. Hosting and joining will not work.");
                return;
            }

            _facepunch = _networkManager.GetComponent<FacepunchTransport>();
            _unityTransport = _networkManager.GetComponent<UnityTransport>();

            if (mode == NetworkMode.Steam && _facepunch == null)
            {
                Debug.LogError("[SessionService] Steam mode but the NetworkManager prefab has no FacepunchTransport.");
            }
            if (mode == NetworkMode.LocalLoopback && _unityTransport == null)
            {
                Debug.LogError("[SessionService] LocalLoopback mode but the NetworkManager prefab has no UnityTransport.");
            }

            // Enabled on both ends: the client must agree with the server's network config, and the
            // client's ConnectionData is what the server's approval callback inspects.
            _networkManager.NetworkConfig.ConnectionApproval = true;

            if (_steam != null) _steam.JoinRequested += HandleSteamJoinRequested;

            HookDisconnect();
        }

        #region Hosting

        public async UniTask<SessionResult> HostAsync()
        {
            if (State != SessionState.Idle) return SessionResult.AlreadyInSession;
            if (_networkManager == null) return SessionResult.Failed;

            SetState(SessionState.Hosting);

            try
            {
                SessionResult result = Mode == NetworkMode.Steam
                    ? await HostSteamAsync()
                    : await HostLocalAsync();

                if (result != SessionResult.Success)
                {
                    await LeaveSessionAsync(returnToMenu: false);
                }

                return result;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                await LeaveSessionAsync(returnToMenu: false);
                return SessionResult.Failed;
            }
        }

        private async UniTask<SessionResult> HostLocalAsync()
        {
            string code = RoomCode.Generate();
            ushort port = RoomCode.ToLoopbackPort(code);

            // Binding the listen address to 0.0.0.0 rather than 127.0.0.1 keeps a second machine on
            // the LAN able to connect if you ever want that, without changing the client path.
            _unityTransport.SetConnectionData("127.0.0.1", port, "0.0.0.0");

            CurrentCode = code;
            ApplyHostApproval();
            SetConnectionPayload(0, code);

            Debug.Log($"[SessionService] LOCAL host on 127.0.0.1:{port} with code {code}.");

            if (!_networkManager.StartHost()) return SessionResult.Failed;

            if (!await LoadGameSceneAsync()) return SessionResult.Failed;

            SetState(SessionState.InSession);
            JoinVoiceChannel(code);
            return SessionResult.Success;
        }

        private async UniTask<SessionResult> HostSteamAsync()
        {
            if (_steam == null || !_steam.IsReady) return SessionResult.SteamUnavailable;

            string code = await ReserveRoomCodeAsync();
            if (string.IsNullOrEmpty(code)) return SessionResult.CodeUnavailable;

            Lobby? created = await SteamMatchmaking.CreateLobbyAsync(maxPlayers);
            if (!created.HasValue) return SessionResult.LobbyCreateFailed;

            Lobby lobby = created.Value;
            CurrentLobby = lobby;
            CurrentCode = code;

            // Hide it explicitly first rather than relying on whatever type CreateLobbyAsync
            // produces, write the data, and only then publish. A client searching for the code can
            // then never find the lobby during the window before the code is attached to it.
            lobby.SetInvisible();
            lobby.SetData(GameKey, GameValue);
            lobby.SetData(CodeKey, code);
            lobby.SetJoinable(true);
            lobby.SetPublic();

            // Deliberately no SetGameServer: this is pure peer to peer over the Steam relay, and
            // advertising a game server changes how Steam resolves the friends-list "Join Game".

            _steam.PublishConnectString(lobby.Id);

            ApplyHostApproval();
            SetConnectionPayload(_steam.LocalSteamId.Value, code);

            Debug.Log($"[SessionService] STEAM host, lobby {lobby.Id.Value}, code {code}.");

            if (!_networkManager.StartHost()) return SessionResult.Failed;

            if (!await LoadGameSceneAsync()) return SessionResult.Failed;

            SetState(SessionState.InSession);
            JoinVoiceChannel(lobby.Id.Value.ToString());
            return SessionResult.Success;
        }

        /// <summary>
        /// Generates a code and checks nobody already published it, retrying on collision.
        /// A small race remains between the check and the publish, which the approval callback
        /// catches: a client arriving at the wrong host is rejected on the code mismatch.
        /// </summary>
        private async UniTask<string> ReserveRoomCodeAsync()
        {
            for (int attempt = 0; attempt < CodeAttempts; attempt++)
            {
                string candidate = RoomCode.Generate();
                if (!await CodeIsTakenAsync(candidate)) return candidate;

                Debug.LogWarning($"[SessionService] Room code {candidate} is already in use, regenerating.");
            }

            return null;
        }

        private async UniTask<bool> CodeIsTakenAsync(string code)
        {
            try
            {
                Lobby[] found = await SteamMatchmaking.LobbyList
                    .FilterDistanceWorldwide()
                    .WithKeyValue(GameKey, GameValue)
                    .WithKeyValue(CodeKey, code)
                    .WithMaxResults(5)
                    .RequestAsync();

                return found != null && found.Length > 0;
            }
            catch (Exception e)
            {
                // A failed query must not block hosting; worst case we collide and the approval
                // check rejects the mismatched client.
                Debug.LogWarning($"[SessionService] Room code availability check failed: {e.Message}");
                return false;
            }
        }

        #endregion

        #region Joining

        public async UniTask<SessionResult> JoinByCodeAsync(string rawCode)
        {
            string code = RoomCode.Normalize(rawCode);
            if (!RoomCode.IsValid(code)) return SessionResult.InvalidCode;

            if (State != SessionState.Idle) return SessionResult.AlreadyInSession;
            if (_networkManager == null) return SessionResult.Failed;

            SetState(SessionState.Searching);

            try
            {
                SessionResult result = Mode == NetworkMode.Steam
                    ? await JoinSteamByCodeAsync(code)
                    : await JoinLocalAsync(code);

                if (result != SessionResult.Success)
                {
                    await LeaveSessionAsync(returnToMenu: false);
                }

                return result;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                await LeaveSessionAsync(returnToMenu: false);
                return SessionResult.Failed;
            }
        }

        private async UniTask<SessionResult> JoinLocalAsync(string code)
        {
            ushort port = RoomCode.ToLoopbackPort(code);
            _unityTransport.SetConnectionData("127.0.0.1", port);

            CurrentCode = code;
            ClearHostApproval();
            SetConnectionPayload(0, code);

            Debug.Log($"[SessionService] LOCAL join 127.0.0.1:{port} with code {code}.");

            SetState(SessionState.Connecting);
            if (!_networkManager.StartClient()) return SessionResult.Failed;

            SessionResult connected = await AwaitConnectedAsync();
            if (connected != SessionResult.Success) return connected;

            SetState(SessionState.InSession);
            JoinVoiceChannel(code);
            return SessionResult.Success;
        }

        private async UniTask<SessionResult> JoinSteamByCodeAsync(string code)
        {
            if (_steam == null || !_steam.IsReady) return SessionResult.SteamUnavailable;

            Lobby[] found = await SteamMatchmaking.LobbyList
                .FilterDistanceWorldwide()
                .WithKeyValue(GameKey, GameValue)
                .WithKeyValue(CodeKey, code)
                .WithMaxResults(20)
                .RequestAsync();

            if (found == null || found.Length == 0) return SessionResult.CodeNotFound;

            // Re-validate rather than trusting the filter and taking found[0]. On appid 480 the
            // query runs against a lobby pool shared with every other Spacewar project.
            Lobby? match = null;
            foreach (Lobby candidate in found)
            {
                if (candidate.GetData(GameKey) != GameValue) continue;
                if (candidate.GetData(CodeKey) != code) continue;

                match = candidate;
                break;
            }

            if (!match.HasValue) return SessionResult.CodeNotFound;

            Lobby lobby = match.Value;
            if (lobby.MaxMembers > 0 && lobby.MemberCount >= lobby.MaxMembers) return SessionResult.LobbyFull;

            return await EnterSteamLobbyAsync(lobby, code);
        }

        /// <summary>
        /// The single Steam client path. Invites, friends-list joins and room codes all end up here,
        /// so there is one place where StartClient happens and one place that can fail.
        /// </summary>
        private async UniTask<SessionResult> EnterSteamLobbyAsync(Lobby lobby, string expectedCode)
        {
            SetState(SessionState.Connecting);

            RoomEnter enter = await lobby.Join();
            if (enter != RoomEnter.Success)
            {
                Debug.LogError($"[SessionService] Steam refused the lobby join: {enter}.");
                return MapRoomEnter(enter);
            }

            CurrentLobby = lobby;

            string publishedCode = lobby.GetData(CodeKey);
            CurrentCode = string.IsNullOrEmpty(publishedCode) ? (expectedCode ?? string.Empty) : publishedCode;

            // Owner is resolved from lobby metadata which can land a frame or two after the join.
            float ownerDeadline = Time.realtimeSinceStartup + 5f;
            while (lobby.Owner.Id.Value == 0 && Time.realtimeSinceStartup < ownerDeadline)
            {
                await UniTask.Yield();
            }

            if (lobby.Owner.Id.Value == 0)
            {
                Debug.LogError("[SessionService] Joined the lobby but never resolved its owner.");
                return SessionResult.Failed;
            }

            _facepunch.targetSteamId = lobby.Owner.Id;
            ClearHostApproval();
            SetConnectionPayload(_steam.LocalSteamId.Value, CurrentCode);

            Debug.Log($"[SessionService] STEAM join, lobby {lobby.Id.Value}, host {lobby.Owner.Id.Value}, code {CurrentCode}.");

            if (!_networkManager.StartClient()) return SessionResult.Failed;

            SessionResult connected = await AwaitConnectedAsync();
            if (connected != SessionResult.Success) return connected;

            // Publish our own connect string so our friends can join through us as well.
            _steam.PublishConnectString(lobby.Id);

            SetState(SessionState.InSession);
            JoinVoiceChannel(lobby.Id.Value.ToString());
            return SessionResult.Success;
        }

        /// <summary>Join a lobby by id. Used by invites, friends-list joins and +connect_lobby.</summary>
        public async UniTask<SessionResult> JoinLobbyAsync(SteamId lobbyId)
        {
            if (Mode != NetworkMode.Steam) return SessionResult.SteamUnavailable;
            if (_steam == null || !_steam.IsReady) return SessionResult.SteamUnavailable;
            if (_networkManager == null) return SessionResult.Failed;

            // Accepting an invite mid-game has to unwind the current session and get back to the
            // menu scene first, otherwise we would StartClient on top of a live host.
            if (State != SessionState.Idle)
            {
                await LeaveSessionAsync();
            }

            try
            {
                SessionResult result = await EnterSteamLobbyAsync(new Lobby(lobbyId), null);
                if (result != SessionResult.Success)
                {
                    RaiseStatus(result.ToMessage());
                    await LeaveSessionAsync(returnToMenu: false);
                }
                return result;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                await LeaveSessionAsync(returnToMenu: false);
                return SessionResult.Failed;
            }
        }

        private void HandleSteamJoinRequested(SteamId lobbyId)
        {
            JoinLobbyAsync(lobbyId).Forget();
        }

        private static SessionResult MapRoomEnter(RoomEnter enter)
        {
            switch (enter)
            {
                case RoomEnter.Full:
                    return SessionResult.LobbyFull;

                case RoomEnter.DoesntExist:
                    return SessionResult.CodeNotFound;

                case RoomEnter.NotAllowed:
                case RoomEnter.Banned:
                case RoomEnter.Limited:
                case RoomEnter.ClanDisabled:
                case RoomEnter.CommunityBan:
                case RoomEnter.MemberBlockedYou:
                case RoomEnter.YouBlockedMember:
                    return SessionResult.JoinDenied;

                default:
                    return SessionResult.Failed;
            }
        }

        /// <summary>
        /// Polls for a connected client rather than subscribing, because NGO reports a rejected
        /// connection by shutting the client down rather than by raising a distinct event.
        /// </summary>
        private async UniTask<SessionResult> AwaitConnectedAsync()
        {
            float deadline = Time.realtimeSinceStartup + connectTimeoutSeconds;

            while (Time.realtimeSinceStartup < deadline)
            {
                if (_networkManager.IsConnectedClient) return SessionResult.Success;

                if (!_networkManager.IsListening)
                {
                    string reason = _networkManager.DisconnectReason;
                    if (!string.IsNullOrEmpty(reason))
                    {
                        Debug.LogError($"[SessionService] Host refused the connection: {reason}");
                        RaiseStatus(reason);
                        return SessionResult.JoinDenied;
                    }

                    return SessionResult.ConnectTimeout;
                }

                await UniTask.Yield();
            }

            Debug.LogError($"[SessionService] Timed out after {connectTimeoutSeconds}s waiting for the host.");
            return SessionResult.ConnectTimeout;
        }

        #endregion

        #region Connection approval

        private void ApplyHostApproval()
        {
            _networkManager.NetworkConfig.ConnectionApproval = true;
            _networkManager.ConnectionApprovalCallback = ApprovalCheck;
        }

        private void ClearHostApproval()
        {
            _networkManager.NetworkConfig.ConnectionApproval = true;
            _networkManager.ConnectionApprovalCallback = null;
        }

        private void SetConnectionPayload(ulong steamId, string code)
        {
            _networkManager.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes($"{steamId}|{code}");
        }

        /// <summary>
        /// Runs on the host for every incoming connection.
        ///
        /// Note on trust: FacepunchTransport does not surface the peer's Steam identity to NGO, so
        /// the SteamId here is self reported. Checking it against the lobby membership stops a
        /// stranger who merely enumerated the public lobby list, which is the realistic threat on a
        /// shared appid; it is not proof of identity.
        /// </summary>
        private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request,
                                   NetworkManager.ConnectionApprovalResponse response)
        {
            // PlayerSpawner owns player spawning, so NGO must not create one itself.
            response.CreatePlayerObject = false;
            response.Pending = false;

            // The host approves itself. NGO invokes this synchronously from HostServerInitialize
            // with ClientNetworkId set to ServerClientId, before LocalClientId is meaningful, so
            // compare against the constant rather than LocalClientId.
            if (request.ClientNetworkId == NetworkManager.ServerClientId)
            {
                response.Approved = true;
                return;
            }

            string payload = request.Payload != null && request.Payload.Length > 0
                ? Encoding.UTF8.GetString(request.Payload)
                : string.Empty;

            int separator = payload.IndexOf('|');
            string claimedId = separator >= 0 ? payload.Substring(0, separator) : string.Empty;
            string claimedCode = separator >= 0 ? payload.Substring(separator + 1) : string.Empty;

            if (!string.Equals(claimedCode, CurrentCode, StringComparison.Ordinal))
            {
                Debug.LogWarning($"[SessionService] Rejected client {request.ClientNetworkId}: room code mismatch.");
                response.Approved = false;
                response.Reason = "Wrong room code.";
                return;
            }

            if (Mode == NetworkMode.Steam)
            {
                if (!ulong.TryParse(claimedId, out ulong steamId) || !IsLobbyMember(steamId))
                {
                    Debug.LogWarning($"[SessionService] Rejected client {request.ClientNetworkId}: not in the Steam lobby.");
                    response.Approved = false;
                    response.Reason = "You are not a member of this lobby.";
                    return;
                }
            }

            response.Approved = true;
        }

        private bool IsLobbyMember(ulong steamId)
        {
            if (!CurrentLobby.HasValue) return false;

            foreach (Friend member in CurrentLobby.Value.Members)
            {
                if (member.Id.Value == steamId) return true;
            }

            return false;
        }

        #endregion

        #region Leaving

        private void HookDisconnect()
        {
            if (_disconnectHooked || _networkManager == null) return;

            _networkManager.OnClientDisconnectCallback += HandleClientDisconnect;
            _disconnectHooked = true;
        }

        private void UnhookDisconnect()
        {
            if (!_disconnectHooked || _networkManager == null) return;

            _networkManager.OnClientDisconnectCallback -= HandleClientDisconnect;
            _disconnectHooked = false;
        }

        private void HandleClientDisconnect(ulong clientId)
        {
            if (_networkManager == null) return;

            // On the host this fires for remote clients leaving, which PlayerSpawner already handles.
            if (_networkManager.IsServer) return;

            // While connecting, AwaitConnectedAsync owns the outcome.
            if (State != SessionState.InSession) return;

            Debug.Log("[SessionService] Lost the host, returning to the menu.");
            RaiseStatus("Host left the game.");
            LeaveSessionAsync().Forget();
        }

        /// <summary>
        /// The only way out of a session. Everything else (the pause menu, losing the host,
        /// accepting an invite mid-game, quitting) funnels through here so the teardown order can
        /// never drift between call sites.
        /// </summary>
        public async UniTask LeaveSessionAsync(bool returnToMenu = true)
        {
            if (State == SessionState.Leaving) return;

            SetState(SessionState.Leaving);

            if (_steam != null) _steam.ClearConnectString();

            if (VivoxManager.Instance != null)
            {
                try
                {
                    await VivoxManager.Instance.LeaveCurrentChannelAsync();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[SessionService] Leaving the voice channel failed: {e.Message}");
                }
            }

            if (CurrentLobby.HasValue)
            {
                try
                {
                    CurrentLobby.Value.Leave();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[SessionService] Leaving the Steam lobby failed: {e.Message}");
                }
                CurrentLobby = null;
            }

            if (_networkManager != null)
            {
                _networkManager.ConnectionApprovalCallback = null;

                if (_networkManager.IsListening)
                {
                    _networkManager.Shutdown();

                    float deadline = Time.realtimeSinceStartup + shutdownTimeoutSeconds;
                    while (_networkManager.IsListening && Time.realtimeSinceStartup < deadline)
                    {
                        await UniTask.Yield();
                    }

                    if (_networkManager.IsListening)
                    {
                        Debug.LogWarning("[SessionService] NetworkManager did not finish shutting down in time.");
                    }
                }
            }

            CurrentCode = string.Empty;
            SetState(SessionState.Idle);

            if (returnToMenu && SceneManager.GetActiveScene().name != menuSceneName)
            {
                // A plain scene load, not the networked one: there is no session left to sync.
                await SceneManager.LoadSceneAsync(menuSceneName, LoadSceneMode.Single).ToUniTask();
            }
        }

        #endregion

        #region Helpers

        public void OpenInviteOverlay()
        {
            if (_steam == null || !CurrentLobby.HasValue)
            {
                RaiseStatus("You need to be in a Steam room to invite friends.");
                return;
            }

            _steam.OpenInviteOverlay(CurrentLobby.Value.Id);
        }

        /// <summary>
        /// NetworkManager.SceneManager only exists between start and shutdown, so wait for it -
        /// but with a deadline, because if StartHost failed it would never appear and an unbounded
        /// wait would hang the host silently in the menu, which is the failure mode we just removed.
        /// </summary>
        private async UniTask<bool> LoadGameSceneAsync()
        {
            float deadline = Time.realtimeSinceStartup + 10f;
            while (_networkManager.SceneManager == null && Time.realtimeSinceStartup < deadline)
            {
                await UniTask.Yield();
            }

            if (_networkManager.SceneManager == null)
            {
                Debug.LogError("[SessionService] NetworkManager.SceneManager never became available.");
                return false;
            }

            SceneEventProgressStatus status = _networkManager.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
            if (status != SceneEventProgressStatus.Started)
            {
                Debug.LogError($"[SessionService] Could not load '{gameSceneName}': {status}.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Fire and forget on purpose. Voice must never gate hosting, joining or scene loading:
        /// the old code awaited Vivox before every host and hid LoadLobbyScene inside a Vivox null
        /// check, so a missing voice manager silently stranded the host in the menu.
        /// </summary>
        private void JoinVoiceChannel(string channelName)
        {
            JoinVoiceChannelAsync(channelName).Forget();
        }

        private async UniTaskVoid JoinVoiceChannelAsync(string channelName)
        {
            try
            {
                float deadline = Time.realtimeSinceStartup + 15f;
                while ((VivoxManager.Instance == null || !VivoxManager.Instance.IsInitialized) &&
                       Time.realtimeSinceStartup < deadline)
                {
                    await UniTask.Yield();
                }

                if (VivoxManager.Instance == null || !VivoxManager.Instance.IsInitialized)
                {
                    Debug.LogWarning("[SessionService] Voice chat is unavailable, continuing without it.");
                    return;
                }

                await VivoxManager.Instance.JoinChannelAsync(channelName);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SessionService] Joining the voice channel failed: {e.Message}");
            }
        }

        private void SetState(SessionState state)
        {
            if (State == state) return;

            State = state;
            StateChanged?.Invoke(state);
        }

        private void RaiseStatus(string message)
        {
            StatusChanged?.Invoke(message);
        }

        #endregion
    }
}
