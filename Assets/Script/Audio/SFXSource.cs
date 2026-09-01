using UnityEngine;


[RequireComponent(typeof(AudioSource))]
public class SFXSource : MonoBehaviour
{
    // ─── Configuration ────────────────────────────────────────────────
    [Tooltip("The volume this source plays at when the listener is right next to it " +
             "and there is no occlusion.  Acts as the maximum volume ceiling.")]
    [SerializeField, Range(0f, 1f)] private float baseVolume = 1f;

    /// <summary>The unmodified volume ceiling for this source.</summary>
    public float BaseVolume => baseVolume;

    // ─── Runtime ──────────────────────────────────────────────────────
    private AudioSource audioSource;
    private AudioLowPassFilter lowPassFilter;

    // ═══════════════════════════════════════════════════════════════════
    // Lifecycle
    // ═══════════════════════════════════════════════════════════════════

    private void Awake()
    {
        // Auto-detect only if no explicit source was injected via Initialize().
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        SetupLowPassFilter();
    }

    /// <summary>
    /// Explicitly assign which <see cref="AudioSource"/> this SFXSource controls.
    /// Call this right after <c>AddComponent&lt;SFXSource&gt;()</c> when there are
    /// multiple AudioSources on the same GameObject.
    /// </summary>
    public void Initialize(AudioSource source)
    {
        audioSource = source;
        audioSource.spatialBlend = 0f;
        SetupLowPassFilter();
    }

    private void SetupLowPassFilter()
    {
        if (audioSource == null) return;

        // Ensure we have a low-pass filter for muffling support.
        lowPassFilter = GetComponent<AudioLowPassFilter>();
        if (lowPassFilter == null)
            lowPassFilter = gameObject.AddComponent<AudioLowPassFilter>();

        // Start with the filter fully open (no muffling).
        lowPassFilter.cutoffFrequency = 22000f;

        // We manage volume ourselves; keep spatialBlend at 2D.
        audioSource.spatialBlend = 0f;
    }

    private void OnEnable()
    {
        if (SpatialAudioManager.Instance != null)
            SpatialAudioManager.Instance.Register(this);
    }

    private void OnDisable()
    {
        if (SpatialAudioManager.Instance != null)
            SpatialAudioManager.Instance.Unregister(this);
    }

    // ═══════════════════════════════════════════════════════════════════
    // API called by SpatialAudioManager
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Set the final computed volume on the underlying AudioSource.
    /// </summary>
    public void ApplyVolume(float volume)
    {
        if (audioSource != null)
            audioSource.volume = Mathf.Clamp01(volume);
    }

    /// <summary>
    /// Smoothly lerp the low-pass cutoff towards <paramref name="targetCutoff"/>.
    /// </summary>
    public void ApplyLowPass(float targetCutoff, float lerpSpeed)
    {
        if (lowPassFilter != null)
        {
            lowPassFilter.cutoffFrequency = Mathf.Lerp(
                lowPassFilter.cutoffFrequency,
                targetCutoff,
                Time.deltaTime * lerpSpeed);
        }
    }

    /// <summary>
    /// Allows external code (e.g. <see cref="SpatialAudioManager.PlaySFXAtPosition"/>)
    /// to override the base volume at runtime.
    /// Also immediately applies the volume to the AudioSource so the change
    /// is audible right away — SpatialAudioManager will refine it with
    /// distance/occlusion on its next update cycle.
    /// </summary>
    public void SetBaseVolume(float volume)
    {
        baseVolume = Mathf.Clamp01(volume);
        ApplyVolume(baseVolume);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Convenience helpers
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>Play a one-shot clip through this source (spatial + occlusion applied).</summary>
    public void PlayOneShot(AudioClip clip, float volumeScale = 1f)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip, volumeScale);
    }

    /// <summary>Play the assigned clip (spatial + occlusion applied).</summary>
    public void Play()
    {
        if (audioSource != null)
            audioSource.Play();
    }

    /// <summary>Stop the current clip.</summary>
    public void Stop()
    {
        if (audioSource != null)
            audioSource.Stop();
    }

    /// <summary>Whether the underlying AudioSource is currently playing.</summary>
    public bool IsPlaying => audioSource != null && audioSource.isPlaying;
}
