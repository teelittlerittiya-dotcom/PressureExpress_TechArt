using Cysharp.Threading.Tasks;
using MoreMountains.Feedbacks;
using PressureExpress.Network;
using UnityEngine;
using UnityEngine.UIElements;

namespace PressureExpress.UI
{
    /// <summary>
    /// In-game session / pause overlay, bound to SessionPanel.uxml.
    ///
    /// Spawned once by GameBootstrap and kept alive, so no scene needs to contain it. It stays
    /// hidden unless a session is actually running, which is why Escape does nothing in the menu.
    ///
    /// Deliberately does NOT touch Time.timeScale: this is a multiplayer game and freezing local
    /// time while the rest of the crew keeps playing causes far more problems than it solves.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class SessionPanelView : MonoBehaviour
    {
        [Tooltip("SettingsPanel.uxml. Cloned into the settings-host container on demand.")]
        [SerializeField] private VisualTreeAsset settingsTemplate;

        [Header("Feel (all optional)")]
        [SerializeField] private MMF_Player openFeedback;
        [SerializeField] private MMF_Player pressFeedback;

        private UIDocument _document;
        private SettingsController _settings;

        private VisualElement _screen;
        private VisualElement _panel;
        private Label _roomCode;
        private Label _feedback;
        private Button _inviteButton;

        private bool _movementLocked;

        public bool IsOpen { get; private set; }

        private void Awake()
        {
            _document = GetComponent<UIDocument>();
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            VisualElement root = _document != null ? _document.rootVisualElement : null;
            if (root == null)
            {
                Debug.LogError("[SessionPanelView] UIDocument has no root. Is a VisualTreeAsset assigned?");
                return;
            }

            _screen = root.Q<VisualElement>("screen") ?? root;
            _panel = root.Q<VisualElement>("session-panel");
            _roomCode = root.Q<Label>("room-code");
            _feedback = root.Q<Label>("feedback");
            _inviteButton = root.Q<Button>("invite-button");

            _settings = new SettingsController(root.Q<VisualElement>("settings-host"), settingsTemplate);

            root.Q<Button>("close-button")?.RegisterCallback<ClickEvent>(_ => Close());
            root.Q<Button>("resume-button")?.RegisterCallback<ClickEvent>(_ => { Press(); Close(); });
            root.Q<Button>("copy-button")?.RegisterCallback<ClickEvent>(_ => OnCopy());
            root.Q<Button>("invite-button")?.RegisterCallback<ClickEvent>(_ => OnInvite());
            root.Q<Button>("settings-button")?.RegisterCallback<ClickEvent>(_ => { Press(); _settings?.Open(); });
            root.Q<Button>("leave-button")?.RegisterCallback<ClickEvent>(_ => OnLeave());

            HideImmediate();
        }

        private void OnDisable()
        {
            ReleaseUI();
        }

        private void Update()
        {
            if (!TogglePressed()) return;

            // The settings overlay swallows Escape first, so one press backs out one level.
            if (_settings != null && _settings.IsOpen)
            {
                _settings.Close();
                return;
            }

            if (IsOpen)
            {
                Close();
                return;
            }

            SessionService session = SessionService.Instance;
            if (session != null && session.State == SessionState.InSession)
            {
                Open();
            }
        }

        private static bool TogglePressed()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null) return keyboard.escapeKey.wasPressedThisFrame;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Escape);
#else
            return false;
#endif
        }

        #region Open / close

        public void Open()
        {
            if (IsOpen || _screen == null) return;

            IsOpen = true;
            _screen.RemoveFromClassList("hidden");

            Refresh();
            AcquireUI();

            _panel?.RemoveFromClassList("is-open");
            _screen.schedule.Execute(() => _panel?.AddToClassList("is-open")).ExecuteLater(16);

            openFeedback?.PlayFeedbacks();
        }

        public void Close()
        {
            if (!IsOpen) return;

            _settings?.Close();
            IsOpen = false;
            _panel?.RemoveFromClassList("is-open");
            _screen.AddToClassList("hidden");

            ReleaseUI();
        }

        private void HideImmediate()
        {
            ReleaseUI();
            IsOpen = false;
            _screen?.AddToClassList("hidden");
            _panel?.RemoveFromClassList("is-open");
        }

        private void Refresh()
        {
            SessionService session = SessionService.Instance;

            if (_roomCode != null)
            {
                _roomCode.text = session != null && !string.IsNullOrEmpty(session.CurrentCode)
                    ? session.CurrentCode
                    : "------";
            }

            // Steam has no invite overlay to offer in the Editor's loopback mode.
            bool canInvite = session != null && session.Mode == NetworkMode.Steam && session.HasLobby;
            _inviteButton?.EnableInClassList("hidden", !canInvite);

            SetFeedback(string.Empty);
        }

        #endregion

        #region Actions

        private void Press()
        {
            pressFeedback?.PlayFeedbacks();
        }

        private void OnCopy()
        {
            Press();

            SessionService session = SessionService.Instance;
            if (session == null || string.IsNullOrEmpty(session.CurrentCode)) return;

            GUIUtility.systemCopyBuffer = session.CurrentCode;
            SetFeedback("Room code copied.");
        }

        private void OnInvite()
        {
            Press();

            SessionService session = SessionService.Instance;
            if (session == null) return;

            session.OpenInviteOverlay();
            SetFeedback("Opening the Steam invite overlay...");
        }

        private void OnLeave()
        {
            Press();
            LeaveAsync().Forget();
        }

        private async UniTaskVoid LeaveAsync()
        {
            SetFeedback("Leaving...");

            SessionService session = SessionService.Instance;
            if (session == null) return;

            Close();
            Time.timeScale = 1f;
            await session.LeaveSessionAsync();
        }

        private void SetFeedback(string message)
        {
            if (_feedback != null) _feedback.text = message;
        }

        #endregion

        #region Cursor and movement

        private void AcquireUI()
        {
            CursorVisibilityController.OpenUI(this);
            if (_movementLocked) return;

            CharacterController2D.LockMovement();
            _movementLocked = true;
        }

        private void ReleaseUI()
        {
            CursorVisibilityController.CloseUI(this);
            if (!_movementLocked) return;

            CharacterController2D.UnlockMovement();
            _movementLocked = false;
        }

        #endregion
    }
}
