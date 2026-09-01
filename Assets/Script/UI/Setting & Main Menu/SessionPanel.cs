using Cysharp.Threading.Tasks;
using PressureExpress.Network;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// In-game session / pause panel: room code, copy, Steam invite, resume, leave.
///
/// This is where invites live now that the flow goes MainMenu -> MainLevel with no waiting room.
/// Put it on your hand-made pause menu canvas and assign whichever fields you actually have.
///
/// Deliberately does NOT touch Time.timeScale: this is a multiplayer game and freezing local time
/// while the rest of the crew keeps playing causes more problems than it solves.
/// </summary>
public class SessionPanel : MonoBehaviour
{
    [Header("Panel")]
    [Tooltip("The object shown/hidden by Escape. Leave empty to use this GameObject.")]
    [SerializeField] private GameObject panelRoot;

    [Header("Room code")]
    [SerializeField] private TextMeshProUGUI roomCodeText;
    [SerializeField] private Button copyButton;

    [Header("Actions")]
    [SerializeField] private Button inviteButton;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button leaveButton;
    [SerializeField] private TextMeshProUGUI feedbackText;

    [Header("Behaviour")]
    [SerializeField] private bool toggleWithEscape = true;

    private bool _movementLocked;

    public bool IsOpen => Root != null && Root.activeSelf;

    private GameObject Root => panelRoot != null ? panelRoot : gameObject;

    private void Awake()
    {
        if (copyButton != null) copyButton.onClick.AddListener(OnCopy);
        if (inviteButton != null) inviteButton.onClick.AddListener(OnInvite);
        if (resumeButton != null) resumeButton.onClick.AddListener(Close);
        if (leaveButton != null) leaveButton.onClick.AddListener(OnLeave);

        Root.SetActive(false);
    }

    private void OnDestroy()
    {
        ReleaseUI();

        if (copyButton != null) copyButton.onClick.RemoveListener(OnCopy);
        if (inviteButton != null) inviteButton.onClick.RemoveListener(OnInvite);
        if (resumeButton != null) resumeButton.onClick.RemoveListener(Close);
        if (leaveButton != null) leaveButton.onClick.RemoveListener(OnLeave);
    }

    private void OnDisable()
    {
        ReleaseUI();
    }

    private void Update()
    {
        if (!toggleWithEscape || !TogglePressed()) return;

        if (IsOpen)
        {
            Close();
            return;
        }

        // Only openable during an actual session, so Escape does nothing in the menu.
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
        if (IsOpen) return;

        Root.SetActive(true);
        Refresh();
        AcquireUI();
    }

    public void Close()
    {
        Root.SetActive(false);
        ReleaseUI();
    }

    public void Refresh()
    {
        SessionService session = SessionService.Instance;

        if (roomCodeText != null)
        {
            roomCodeText.text = session != null && !string.IsNullOrEmpty(session.CurrentCode)
                ? session.CurrentCode
                : "------";
        }

        // Steam has no invite overlay to offer in the Editor's local mode.
        if (inviteButton != null)
        {
            bool canInvite = session != null && session.Mode == NetworkMode.Steam && session.HasLobby;
            inviteButton.gameObject.SetActive(canInvite);
        }

        SetFeedback(string.Empty);
    }

    #endregion

    #region Actions

    private void OnCopy()
    {
        SessionService session = SessionService.Instance;
        if (session == null || string.IsNullOrEmpty(session.CurrentCode)) return;

        GUIUtility.systemCopyBuffer = session.CurrentCode;
        SetFeedback("Room code copied.");
    }

    private void OnInvite()
    {
        SessionService session = SessionService.Instance;
        if (session == null) return;

        session.OpenInviteOverlay();
        SetFeedback("Opening the Steam invite overlay...");
    }

    private void OnLeave()
    {
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
        if (feedbackText != null) feedbackText.text = message;
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
