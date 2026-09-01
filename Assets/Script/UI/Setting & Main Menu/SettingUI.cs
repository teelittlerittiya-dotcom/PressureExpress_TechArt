using UnityEngine;

public class SettingUI : MonoBehaviour
{
    [SerializeField] private GameObject pauseMenuUI;
    private bool isPaused;

    private void Start()
    {
        if (pauseMenuUI != null)
        {
            pauseMenuUI.SetActive(false);
        }
        isPaused = false;
    }

    private void OnDestroy()
    {
        CursorVisibilityController.CloseUI(this);
    }

    public void Pause()
    {
        if (pauseMenuUI == null) return;

        isPaused = !isPaused;
        pauseMenuUI.SetActive(isPaused);

        if (isPaused)
        {
            CursorVisibilityController.OpenUI(this);
        }
        else
        {
            CursorVisibilityController.CloseUI(this);
        }
    }
}
