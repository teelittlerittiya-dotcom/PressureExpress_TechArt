using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using Cysharp.Threading.Tasks;

public class TestVoiceChat : MonoBehaviour
{
    private const string DEFAULT_VIVOX_CHANNEL = "main-lobby-room";

    public void StartHost()
    {
        StartHostAsync().Forget();
    }

    private async UniTaskVoid StartHostAsync()
    {
        await UniTask.WaitUntil(() => VivoxManager.Instance != null && VivoxManager.Instance.IsInitialized);
        Debug.Log("[NetworkVoiceController] Starting Host...");
        if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.StartHost();
        }
        if (VivoxManager.Instance != null)
        {
            await VivoxManager.Instance.JoinChannelAsync(DEFAULT_VIVOX_CHANNEL);
        }
    }

    public void StartClient()
    {
        StartClientAsync().Forget();
    }

    private async UniTaskVoid StartClientAsync()
    {
        await UniTask.WaitUntil(() => VivoxManager.Instance != null && VivoxManager.Instance.IsInitialized);
        Debug.Log("[NetworkVoiceController] Starting Client...");
        if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.StartClient();
        }
        if (VivoxManager.Instance != null)
        {
            await VivoxManager.Instance.JoinChannelAsync(DEFAULT_VIVOX_CHANNEL);
        }
    }

    public void Shutdown()
    {
        ShutdownAsync().Forget();
    }

    private async UniTaskVoid ShutdownAsync()
    {
        Debug.Log("[NetworkVoiceController] Shutting down network and Vivox connection.");

        if (VivoxManager.Instance != null)
        {
            await VivoxManager.Instance.LeaveCurrentChannelAsync();
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }
    }

    public void LoadScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("[NetworkVoiceController] Provided scene name is null or empty.");
            return;
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
        {
            NetworkManager.Singleton.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }
        else
        {
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }
    }
}