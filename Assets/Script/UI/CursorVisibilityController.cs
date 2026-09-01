using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Keeps the OS cursor visible for menus/open UI and hidden during cursor-driven gameplay.
/// Requests are keyed by their owner so nested UI can open/close without restoring stale state.
/// </summary>
public static class CursorVisibilityController
{
    private static readonly HashSet<int> gameplayOwners = new HashSet<int>();
    private static readonly HashSet<int> uiOwners = new HashSet<int>();
    private static CursorVisibilityDriver driver;
    private static bool leftAltHeld;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        gameplayOwners.Clear();
        uiOwners.Clear();
        driver = null;
        leftAltHeld = false;
        Refresh();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InstallDriver()
    {
        if (driver != null) return;

        GameObject driverObject = new GameObject("[Cursor Visibility]");
        driverObject.hideFlags = HideFlags.HideAndDontSave;
        Object.DontDestroyOnLoad(driverObject);
        driver = driverObject.AddComponent<CursorVisibilityDriver>();
        Refresh();
    }

    public static void EnterGameplay(Object owner)
    {
        if (!TryGetOwnerId(owner, out int ownerId)) return;
        gameplayOwners.Add(ownerId);
        Refresh();
    }

    public static void ExitGameplay(Object owner)
    {
        if (!TryGetOwnerId(owner, out int ownerId)) return;
        gameplayOwners.Remove(ownerId);
        Refresh();
    }

    public static void OpenUI(Object owner)
    {
        if (!TryGetOwnerId(owner, out int ownerId)) return;
        uiOwners.Add(ownerId);
        Refresh();
    }

    public static void CloseUI(Object owner)
    {
        if (!TryGetOwnerId(owner, out int ownerId)) return;
        uiOwners.Remove(ownerId);
        Refresh();
    }

    private static bool TryGetOwnerId(Object owner, out int ownerId)
    {
        ownerId = 0;
        if (owner == null) return false;

        ownerId = owner.GetInstanceID();
        return true;
    }

    private static void Refresh()
    {
        if (!Application.isPlaying) return;

        bool gameplayCursorActive = gameplayOwners.Count > 0 && uiOwners.Count == 0;
        bool shouldShow = !gameplayCursorActive || leftAltHeld;
        Cursor.lockState = shouldShow ? CursorLockMode.None : CursorLockMode.Confined;
        Cursor.visible = shouldShow;
    }

    private static void PollTemporaryCursorKey()
    {
        bool isHeld = Input.GetKey(KeyCode.LeftAlt);
        if (isHeld == leftAltHeld) return;

        leftAltHeld = isHeld;
        Refresh();
    }

    private static void RestoreSystemCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private sealed class CursorVisibilityDriver : MonoBehaviour
    {
        private void Update()
        {
            PollTemporaryCursorKey();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus) Refresh();
        }

        private void OnDestroy()
        {
            if (driver != this) return;

            driver = null;
            RestoreSystemCursor();
        }
    }
}
