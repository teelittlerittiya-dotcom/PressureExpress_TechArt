using UnityEngine;

/// <summary>
/// Attach to Main Camera or Camera holder. Automatically toggles underwater cyan/blue screen tint
/// and visual atmospheric effects when the camera enters room water.
/// </summary>
[RequireComponent(typeof(Camera))]
public class UnderwaterCameraEffect : MonoBehaviour
{
    [Header("Underwater Color & Visuals")]
    [SerializeField] private Color underwaterColor = new Color(0.0f, 0.45f, 0.75f, 0.35f);
    [SerializeField] private Color deepUnderwaterColor = new Color(0.02f, 0.15f, 0.35f, 0.65f);
    [SerializeField] private float transitionSpeed = 5f;

    [Header("Underwater Audio Listener Muffling")]
    [SerializeField] private float shallowCutoffFrequency = 3500f;
    [SerializeField] private float deepCutoffFrequency = 1200f;
    [SerializeField] private float maxMuffleDepth = 3.0f;
    [SerializeField] private float normalCutoffFrequency = 22000f;

    [Header("Underwater Loop Sound")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip underwaterLoopClip;

    private Camera cam;
    private Texture2D overlayTexture;
    private float currentAlpha = 0f;
    private Color currentColor;
    private bool isUnderwater;
    private Transform localPlayerTransform;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        CreateOverlayTexture();
    }

    private void FindLocalPlayer()
    {
        if (localPlayerTransform != null) return;

        if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.LocalClient != null && Unity.Netcode.NetworkManager.Singleton.LocalClient.PlayerObject != null)
        {
            localPlayerTransform = Unity.Netcode.NetworkManager.Singleton.LocalClient.PlayerObject.transform;
            return;
        }

        var playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            localPlayerTransform = playerObj.transform;
        }
    }

    private void CreateOverlayTexture()
    {
        overlayTexture = new Texture2D(1, 1);
        overlayTexture.SetPixel(0, 0, Color.white);
        overlayTexture.Apply();
    }

    private void Update()
    {
        FindLocalPlayer();
        // Check player position (head/chest height) if available, otherwise fallback to camera
        Vector3 checkPos = (localPlayerTransform != null) ? (localPlayerTransform.position + Vector3.up * 0.5f) : transform.position;

        bool inWater = RoomWaterVisualizer.TryGetWaterSurfaceY(checkPos, out float surfaceY, out _);
        bool submerged = inWater && (checkPos.y <= surfaceY);
        float currentDepth = submerged ? (surfaceY - checkPos.y) : 0f;

        if (submerged)
        {
            float t = Mathf.Clamp01(currentDepth / 2.5f);
            Color targetColor = Color.Lerp(underwaterColor, deepUnderwaterColor, t);
            currentColor = Color.Lerp(currentColor, targetColor, Time.deltaTime * transitionSpeed);
            currentAlpha = Mathf.Lerp(currentAlpha, targetColor.a, Time.deltaTime * transitionSpeed);
            isUnderwater = true;
        }
        else
        {
            currentAlpha = Mathf.Lerp(currentAlpha, 0f, Time.deltaTime * transitionSpeed * 1.5f);
            if (currentAlpha < 0.01f) isUnderwater = false;
        }

        HandleUnderwaterAudio(submerged, currentDepth);
    }

    private AudioLowPassFilter listenerLowPassFilter;

    private void EnsureAudioListenerFilter()
    {
        if (listenerLowPassFilter != null) return;

        AudioListener listener = GetComponent<AudioListener>();
        if (listener == null) listener = FindFirstObjectByType<AudioListener>();

        if (listener != null)
        {
            listenerLowPassFilter = listener.GetComponent<AudioLowPassFilter>();
            if (listenerLowPassFilter == null)
            {
                listenerLowPassFilter = listener.gameObject.AddComponent<AudioLowPassFilter>();
            }
            listenerLowPassFilter.cutoffFrequency = normalCutoffFrequency;
        }
    }

    private void HandleUnderwaterAudio(bool submerged, float depth)
    {
        EnsureAudioListenerFilter();

        if (listenerLowPassFilter != null)
        {
            float targetCutoff = normalCutoffFrequency;
            if (submerged)
            {
                float depthT = Mathf.Clamp01(depth / maxMuffleDepth);
                targetCutoff = Mathf.Lerp(shallowCutoffFrequency, deepCutoffFrequency, depthT);
            }

            listenerLowPassFilter.cutoffFrequency = Mathf.Lerp(
                listenerLowPassFilter.cutoffFrequency,
                targetCutoff,
                Time.deltaTime * transitionSpeed * 2f
            );
        }

        if (audioSource == null || underwaterLoopClip == null) return;

        if (submerged)
        {
            if (!audioSource.isPlaying)
            {
                audioSource.clip = underwaterLoopClip;
                audioSource.loop = true;
                audioSource.Play();
            }
            float depthT = Mathf.Clamp01(depth / maxMuffleDepth);
            float targetVolume = Mathf.Lerp(0.25f, 0.55f, depthT);
            audioSource.volume = Mathf.Lerp(audioSource.volume, targetVolume, Time.deltaTime * transitionSpeed);
        }
        else if (audioSource.isPlaying)
        {
            audioSource.volume = Mathf.Lerp(audioSource.volume, 0f, Time.deltaTime * transitionSpeed * 2f);
            if (audioSource.volume < 0.02f)
            {
                audioSource.Stop();
            }
        }
    }

    private void OnGUI()
    {
        if (!isUnderwater || currentAlpha <= 0.001f || overlayTexture == null) return;

        Color originalGUIColor = GUI.color;
        GUI.color = new Color(currentColor.r, currentColor.g, currentColor.b, currentAlpha);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), overlayTexture, ScaleMode.StretchToFill);
        GUI.color = originalGUIColor;
    }

    private void OnDestroy()
    {
        if (overlayTexture != null)
        {
            Destroy(overlayTexture);
        }
    }
}
