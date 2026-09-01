using Cysharp.Threading.Tasks;
using MoreMountains.Feedbacks;
using PressureExpress.Network;
using UnityEngine;
using UnityEngine.UIElements;

namespace PressureExpress.UI
{
    /// <summary>
    /// UI Toolkit main menu, bound to MainMenu.uxml.
    ///
    /// Replaces the uGUI MainUI. Managers live in the Bootstrap scene and cannot hold scene
    /// references, so this reaches for SessionService.Instance rather than being wired to it.
    ///
    /// The MMF_Player fields are optional hooks: the USS transitions already animate hover, press
    /// and the panel entrance on their own, and Feel layers on top of that when assigned. Feel's
    /// MMF_UIToolkit* feedbacks can target any element here by name (host-button, menu-panel,
    /// title, status) or by class.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class MainMenuView : MonoBehaviour
    {
        [Tooltip("SettingsPanel.uxml. Cloned into the settings-host container on demand.")]
        [SerializeField] private VisualTreeAsset settingsTemplate;

        [Header("Feel (all optional)")]
        [SerializeField] private MMF_Player introFeedback;
        [SerializeField] private MMF_Player pressFeedback;
        [SerializeField] private MMF_Player errorFeedback;

        private UIDocument _document;
        private SessionService _session;
        private SettingsController _settings;

        private VisualElement _panel;
        private Button _hostButton;
        private Button _joinButton;
        private Button _settingsButton;
        private Button _quitButton;
        private TextField _joinCode;
        private Label _status;
        private Label _localBanner;

        private bool _busy;

        private void Awake()
        {
            _document = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            CursorVisibilityController.OpenUI(this);

            VisualElement root = _document != null ? _document.rootVisualElement : null;
            if (root == null)
            {
                Debug.LogError("[MainMenuView] UIDocument has no root. Is a VisualTreeAsset assigned?");
                return;
            }

            _panel = root.Q<VisualElement>("menu-panel");
            _hostButton = root.Q<Button>("host-button");
            _joinButton = root.Q<Button>("join-button");
            _settingsButton = root.Q<Button>("settings-button");
            _quitButton = root.Q<Button>("quit-button");
            _joinCode = root.Q<TextField>("join-code");
            _status = root.Q<Label>("status");
            _localBanner = root.Q<Label>("local-banner");

            _settings = new SettingsController(root.Q<VisualElement>("settings-host"), settingsTemplate);

            _hostButton?.RegisterCallback<ClickEvent>(OnHostClicked);
            _joinButton?.RegisterCallback<ClickEvent>(OnJoinClicked);
            _settingsButton?.RegisterCallback<ClickEvent>(OnSettingsClicked);
            _quitButton?.RegisterCallback<ClickEvent>(OnQuitClicked);
            _joinCode?.RegisterValueChangedCallback(OnCodeTyped);

            _session = SessionService.Instance;
            if (_session != null)
            {
                _session.StateChanged += OnSessionStateChanged;
                _session.StatusChanged += ShowStatus;
            }

            ApplyAvailability();
            PlayIntro();
        }

        private void OnDisable()
        {
            CursorVisibilityController.CloseUI(this);

            _hostButton?.UnregisterCallback<ClickEvent>(OnHostClicked);
            _joinButton?.UnregisterCallback<ClickEvent>(OnJoinClicked);
            _settingsButton?.UnregisterCallback<ClickEvent>(OnSettingsClicked);
            _quitButton?.UnregisterCallback<ClickEvent>(OnQuitClicked);
            _joinCode?.UnregisterValueChangedCallback(OnCodeTyped);

            if (_session != null)
            {
                _session.StateChanged -= OnSessionStateChanged;
                _session.StatusChanged -= ShowStatus;
            }
        }

        private void PlayIntro()
        {
            if (_panel == null) return;

            // Deferred a frame so the .enter -> .is-open transition has something to animate from.
            _panel.RemoveFromClassList("is-open");
            _panel.schedule.Execute(() => _panel.AddToClassList("is-open")).ExecuteLater(16);

            introFeedback?.PlayFeedbacks();
        }

        /// <summary>
        /// Decides whether multiplayer is usable at all. In a build with Steam down we disable
        /// rather than quietly starting a loopback host nobody could ever reach.
        /// </summary>
        private void ApplyAvailability()
        {
            if (_session == null)
            {
                SetInteractable(false);
                ShowStatus("Network services missing. Enter play mode from the Bootstrap scene.", StatusKind.Error);
                return;
            }

            _localBanner?.EnableInClassList("hidden", _session.Mode != NetworkMode.LocalLoopback);

            SteamService steam = SteamService.Instance;
            if (_session.Mode == NetworkMode.Steam && (steam == null || !steam.IsReady))
            {
                SetInteractable(false);
                ShowStatus("Steam is not running. Please launch the game through Steam.", StatusKind.Error);
                return;
            }

            SetInteractable(true);
            ShowStatus(_session.Mode == NetworkMode.LocalLoopback
                ? "Local mode — UnityTransport on 127.0.0.1."
                : string.Empty);
        }

        #region Input

        /// <summary>Keeps the field showing exactly what will be sent: uppercase, no separators.</summary>
        private void OnCodeTyped(ChangeEvent<string> evt)
        {
            string cleaned = RoomCode.Normalize(evt.newValue);
            if (cleaned.Length > RoomCode.Length)
            {
                cleaned = cleaned.Substring(0, RoomCode.Length);
            }

            if (cleaned != evt.newValue)
            {
                _joinCode.SetValueWithoutNotify(cleaned);
            }
        }

        private void OnHostClicked(ClickEvent evt)
        {
            if (_busy || _session == null) return;

            pressFeedback?.PlayFeedbacks();
            HostAsync().Forget();
        }

        private async UniTaskVoid HostAsync()
        {
            BeginBusy("Creating room...");

            SessionResult result = await _session.HostAsync();

            EndBusy(result);
            // On success the session loads the game scene and this menu goes away.
        }

        private void OnJoinClicked(ClickEvent evt)
        {
            if (_busy || _session == null) return;

            pressFeedback?.PlayFeedbacks();
            JoinAsync().Forget();
        }

        private async UniTaskVoid JoinAsync()
        {
            string typed = _joinCode != null ? _joinCode.value : string.Empty;

            BeginBusy("Searching for room...");

            SessionResult result = await _session.JoinByCodeAsync(typed);

            EndBusy(result);
        }

        private void OnSettingsClicked(ClickEvent evt)
        {
            pressFeedback?.PlayFeedbacks();
            _settings?.Open();
        }

        private void OnQuitClicked(ClickEvent evt)
        {
            pressFeedback?.PlayFeedbacks();

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        #endregion

        #region State

        private void BeginBusy(string message)
        {
            _busy = true;
            SetInteractable(false);
            ShowStatus(message, StatusKind.Busy);
        }

        private void EndBusy(SessionResult result)
        {
            _busy = false;

            if (result == SessionResult.Success)
            {
                ShowStatus(result.ToMessage(), StatusKind.Good);
                return;
            }

            ShowStatus(result.ToMessage(), StatusKind.Error);
            errorFeedback?.PlayFeedbacks();
            ApplyAvailability();
        }

        private void OnSessionStateChanged(SessionState state)
        {
            switch (state)
            {
                case SessionState.Hosting:
                    ShowStatus("Creating room...", StatusKind.Busy);
                    break;
                case SessionState.Searching:
                    ShowStatus("Searching for room...", StatusKind.Busy);
                    break;
                case SessionState.Connecting:
                    ShowStatus("Connecting to host...", StatusKind.Busy);
                    break;
                case SessionState.Idle:
                    if (!_busy) ApplyAvailability();
                    break;
            }
        }

        private void SetInteractable(bool value)
        {
            _hostButton?.SetEnabled(value);
            _joinButton?.SetEnabled(value);
            _joinCode?.SetEnabled(value);
        }

        private enum StatusKind { Neutral, Busy, Good, Error }

        private void ShowStatus(string message)
        {
            ShowStatus(message, StatusKind.Neutral);
        }

        private void ShowStatus(string message, StatusKind kind)
        {
            if (_status == null)
            {
                if (!string.IsNullOrEmpty(message)) Debug.Log($"[MainMenuView] {message}");
                return;
            }

            _status.text = message;
            _status.EnableInClassList("status--busy", kind == StatusKind.Busy);
            _status.EnableInClassList("status--good", kind == StatusKind.Good);
            _status.EnableInClassList("status--error", kind == StatusKind.Error);
        }

        #endregion
    }
}
