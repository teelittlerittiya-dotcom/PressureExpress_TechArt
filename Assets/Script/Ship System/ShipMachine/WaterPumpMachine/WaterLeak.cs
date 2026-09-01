using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class WaterLeak : NetworkBehaviour
{
    [Header("Interact Settings")]
    public float requiredHoldTime = 5f;
    public KeyCode interactKey = KeyCode.F;

    [Header("UI Feedback")]
    public GameObject progressCanvas;
    [SerializeField] GameObject interactCanvas;
    public Slider holdProgressBar;

    private float currentHoldTime = 0f;
    private bool isLocalPlayerInRange = false;
    private bool isFixing = false;

    private void Start()
    {
        if (NetworkHelper.IsOffline)
        {
            if (WaterSystemManager.Instance != null)
            {
                WaterSystemManager.Instance.AddLeak();
            }
        }

        if (progressCanvas != null) progressCanvas.SetActive(false);
        if (interactCanvas != null) interactCanvas.SetActive(false);
        if (holdProgressBar != null) holdProgressBar.value = 0f;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            if (WaterSystemManager.Instance != null)
            {
                WaterSystemManager.Instance.AddLeak();
            }
        }

        if (progressCanvas != null) progressCanvas.SetActive(false);
        if (interactCanvas != null) interactCanvas.SetActive(false);
        if (holdProgressBar != null) holdProgressBar.value = 0f;
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            if (WaterSystemManager.Instance != null)
            {
                WaterSystemManager.Instance.RemoveLeak();
            }
        }
    }

    private void OnDestroy()
    {
        if (NetworkHelper.IsOffline)
        {
            if (WaterSystemManager.Instance != null)
            {
                WaterSystemManager.Instance.RemoveLeak();
            }
        }
    }

    private void Update()
    {
        bool isSpawnedOrOffline = NetworkHelper.IsOffline ? true : IsSpawned;
        if (!isSpawnedOrOffline || !isLocalPlayerInRange || isFixing) return;

        if (Input.GetKey(interactKey))
        {
            currentHoldTime += Time.deltaTime;

            if (holdProgressBar != null)
            {
                holdProgressBar.value = currentHoldTime / requiredHoldTime;
            }

            if (currentHoldTime >= requiredHoldTime)
            {
                isFixing = true;
                if (NetworkHelper.IsListening)
                {
                    FixLeakServerRpc();
                }
                else
                {
                    Destroy(gameObject);
                }
            }
        }
        else
        {
            currentHoldTime = 0f;
            if (holdProgressBar != null) holdProgressBar.value = 0f;
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void FixLeakServerRpc()
    {
        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn(true);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter(Collider collision)
    {
        if (NetworkHelper.IsLocalPlayer(collision))
        {
            isLocalPlayerInRange = true;
            if (progressCanvas != null) progressCanvas.SetActive(true);
            if (interactCanvas != null) interactCanvas.SetActive(true);
        }
    }

    private void OnTriggerExit(Collider collision)
    {
        if (NetworkHelper.IsLocalPlayer(collision))
        {
            isLocalPlayerInRange = false;
            currentHoldTime = 0f;

            if (progressCanvas != null) progressCanvas.SetActive(false);
            if (interactCanvas != null) interactCanvas.SetActive(false);
            if (holdProgressBar != null) holdProgressBar.value = 0f;
        }
    }
}