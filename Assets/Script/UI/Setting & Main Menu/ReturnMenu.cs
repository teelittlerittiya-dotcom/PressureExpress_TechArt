using Cysharp.Threading.Tasks;
using PressureExpress.Network;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Leaving a run. This used to be a bare SceneManager.LoadScene("MainMenu"), which left NGO still
/// listening (the manager is DontDestroyOnLoad), left the Steam lobby alive and joinable with the
/// player still listed in it, and left friends looking at a "Join Game" button pointing at a dead
/// session. All of that now goes through the one teardown path.
/// </summary>
public class ReturnMenu : MonoBehaviour
{
    [SerializeField] private string fallbackMenuScene = "MainMenu";

    public void BackToMenu()
    {
        Time.timeScale = 1f;
        BackToMenuAsync().Forget();
    }

    private async UniTaskVoid BackToMenuAsync()
    {
        SessionService session = SessionService.Instance;

        if (session == null)
        {
            Debug.LogWarning("[ReturnMenu] No SessionService, falling back to a plain scene load.");
            SceneManager.LoadScene(fallbackMenuScene);
            return;
        }

        await session.LeaveSessionAsync();
    }
}
