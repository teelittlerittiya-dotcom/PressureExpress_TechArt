using System.Text;
using Steamworks;
using Steamworks.Data;
using Unity.Netcode;
using UnityEngine;

namespace PressureExpress.Network
{
    /// <summary>
    /// F3 overlay showing the live networking state inside a build.
    ///
    /// The Editor is locked to LocalLoopback, so the Steam path can only ever be observed from a
    /// build on a second machine with no debugger attached. This is the only window into it.
    /// Watching SteamClient.IsValid flip from true to false in the first frames is what identifies
    /// a duplicate manager calling SteamClient.Shutdown.
    /// </summary>
    public class NetworkDebugOverlay : MonoBehaviour
    {
        [SerializeField] private bool visibleOnStart;
        [SerializeField] private int fontSize = 13;

        private bool _visible;
        private GUIStyle _style;
        private readonly StringBuilder _sb = new StringBuilder(1024);

        private bool _steamWasValid;
        private bool _steamEverInvalidated;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            _visible = visibleOnStart;
        }

        private void Update()
        {
            if (ToggleRequested()) _visible = !_visible;

            // Latch the transition, because a shutdown that happens during startup is otherwise
            // invisible by the time anyone opens the overlay.
            bool valid = SteamClient.IsValid;
            if (_steamWasValid && !valid) _steamEverInvalidated = true;
            _steamWasValid = valid;
        }

        private static bool ToggleRequested()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null) return keyboard.f3Key.wasPressedThisFrame;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.F3);
#else
            return false;
#endif
        }

        private void OnGUI()
        {
            if (!_visible) return;

            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = fontSize,
                    richText = true,
                    wordWrap = false
                };
            }

            string text = BuildReport();

            const float width = 460f;
            float height = _style.CalcHeight(new GUIContent(text), width) + 20f;

            GUI.Box(new Rect(8f, 8f, width, height), GUIContent.none);
            GUI.Label(new Rect(18f, 16f, width - 20f, height), text, _style);
        }

        private string BuildReport()
        {
            _sb.Clear();
            _sb.AppendLine("<b>NETWORK DEBUG (F3)</b>");

            SessionService session = SessionService.Instance;
            SteamService steam = SteamService.Instance;

            _sb.AppendLine(session != null
                ? $"Mode: <b>{session.Mode}</b>   State: <b>{session.State}</b>"
                : "Mode: <b>no SessionService</b>");

            if (steam != null)
            {
                _sb.AppendLine($"Steam: <b>{steam.State}</b>   IsValid: <b>{SteamClient.IsValid}</b>");
                if (SteamClient.IsValid)
                {
                    _sb.AppendLine($"  Me: {steam.LocalName} ({steam.LocalSteamId.Value})");
                }
                _sb.AppendLine($"  connect: {(string.IsNullOrEmpty(steam.PublishedConnectString) ? "<i>not published</i>" : steam.PublishedConnectString)}");
            }
            else
            {
                _sb.AppendLine("Steam: <b>no SteamService</b>");
            }

            if (_steamEverInvalidated)
            {
                _sb.AppendLine("<color=red>  SteamClient was valid and then became invalid - something called SteamClient.Shutdown()</color>");
            }

            if (session != null)
            {
                _sb.AppendLine($"Room code: <b>{(string.IsNullOrEmpty(session.CurrentCode) ? "-" : session.CurrentCode)}</b>");

                if (session.CurrentLobby.HasValue)
                {
                    Lobby lobby = session.CurrentLobby.Value;
                    _sb.AppendLine($"Lobby: {lobby.Id.Value}");
                    _sb.AppendLine($"  owner: {lobby.Owner.Id.Value}   members: {lobby.MemberCount}/{lobby.MaxMembers}");
                }
                else
                {
                    _sb.AppendLine("Lobby: <i>none</i>");
                }
            }

            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null)
            {
                _sb.AppendLine("NGO: <color=red>no NetworkManager.Singleton</color>");
            }
            else
            {
                _sb.AppendLine($"NGO: listening={nm.IsListening} host={nm.IsHost} server={nm.IsServer} client={nm.IsClient}");
                _sb.AppendLine($"  connectedClient={nm.IsConnectedClient}   localId={nm.LocalClientId}");

                if (nm.IsServer && nm.ConnectedClientsIds != null)
                {
                    _sb.AppendLine($"  clients: {nm.ConnectedClientsIds.Count}");
                }

                if (!string.IsNullOrEmpty(nm.DisconnectReason))
                {
                    _sb.AppendLine($"  <color=orange>lastDisconnectReason: {nm.DisconnectReason}</color>");
                }

                var transport = nm.NetworkConfig != null ? nm.NetworkConfig.NetworkTransport : null;
                _sb.AppendLine($"  transport: {(transport == null ? "<color=red>null</color>" : transport.GetType().Name)}");
            }

            _sb.AppendLine($"Scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");

            return _sb.ToString();
        }
    }
}
