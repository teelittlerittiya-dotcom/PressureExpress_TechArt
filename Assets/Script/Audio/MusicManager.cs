using UnityEngine;

public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; }

    [Header("Audio Clips")]
    [SerializeField] private AudioClip bgm;
    [SerializeField] private AudioClip bgm2;
    [SerializeField] private AudioClip fixingSFX;

    [Header("BGM Settings")]
    [Tooltip("Default volume for the background music (used if no saved preference exists).")]
    [SerializeField, Range(0f, 1f)] private float bgmVolume = 0.5f;

    [Tooltip("Duration in seconds for crossfading between BGM tracks.")]
    [SerializeField] private float crossfadeDuration = 2f;

    [Header("SFX Settings")]
    [Tooltip("Default volume for sound effects (used if no saved preference exists).")]
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 0.7f;

    [Header("Volume Step")]
    [Tooltip("Amount added/removed per Increase/Decrease call.")]
    [SerializeField] private float volumeStep = 0.1f;

    // Runtime references — created in Awake.
    private AudioSource bgmAudioSource;
    private SFXSource bgmSFXSource;

    private AudioSource bgm2AudioSource;
    private SFXSource bgm2SFXSource;

    private AudioSource fixingAudioSource;
    private SFXSource fixingSFXSource;

    // PlayerPrefs keys.
    private const string BGM_VOL_KEY = "BGMVolume";
    private const string SFX_VOL_KEY = "SFXVolume";

    // Crossfade state.
    private bool isCrossfading;
    private float crossfadeTimer;
    private int crossfadeTarget; // 1 = fading to bgm, 2 = fading to bgm2

    // ═══════════════════════════════════════════════════════════════════
    // Lifecycle
    // ═══════════════════════════════════════════════════════════════════

    private void Awake()
    {
        // Singleton.
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        // Restore saved volume (or use Inspector defaults).
        if (PlayerPrefs.HasKey(BGM_VOL_KEY)) bgmVolume = PlayerPrefs.GetFloat(BGM_VOL_KEY);
        if (PlayerPrefs.HasKey(SFX_VOL_KEY)) sfxVolume = PlayerPrefs.GetFloat(SFX_VOL_KEY);

        // --- BGM 1 ---
        bgmAudioSource = CreateLoopingSource();
        bgmSFXSource = CreateSFXSource(bgmAudioSource, bgmVolume);

        // --- BGM 2 ---
        bgm2AudioSource = CreateLoopingSource();
        bgm2SFXSource = CreateSFXSource(bgm2AudioSource, 0f); // Starts silent.

        // --- Fixing SFX ---
        fixingAudioSource = CreateLoopingSource();
        fixingSFXSource = CreateSFXSource(fixingAudioSource, sfxVolume);
    }

    private AudioSource CreateLoopingSource()
    {
        AudioSource src = gameObject.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = true;
        src.spatialBlend = 0f;
        return src;
    }

    private SFXSource CreateSFXSource(AudioSource audioSrc, float volume)
    {
        SFXSource sfx = gameObject.AddComponent<SFXSource>();
        sfx.Initialize(audioSrc);
        sfx.SetBaseVolume(volume);
        return sfx;
    }

    private void Start()
    {
        PlayBothBGM();
    }

    private void Update()
    {
        // --- Fixing SFX (F key) ---
        if (fixingSFX != null && fixingAudioSource != null)
        {
            // กด F ค้าง
            if (Input.GetKey(KeyCode.F))
            {
                if (!fixingAudioSource.isPlaying)
                {
                    fixingAudioSource.clip = fixingSFX;
                    fixingAudioSource.Play();
                }
            }
            // ปล่อย F
            else
            {
                if (fixingAudioSource.isPlaying)
                {
                    fixingAudioSource.Stop();
                }
            }
        }

        // --- Crossfade ---
        if (isCrossfading)
        {
            UpdateCrossfade();
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // BGM Playback API
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>Play only BGM 1 (stops BGM 2 immediately).</summary>
    public void PlayBGM1()
    {
        isCrossfading = false;
        bgmSFXSource.SetBaseVolume(bgmVolume);
        bgm2SFXSource.SetBaseVolume(0f);

        if (bgm != null && !bgmAudioSource.isPlaying)
        {
            bgmAudioSource.clip = bgm;
            bgmAudioSource.Play();
        }
        bgm2AudioSource.Stop();
    }

    /// <summary>Play only BGM 2 (stops BGM 1 immediately).</summary>
    public void PlayBGM2()
    {
        isCrossfading = false;
        bgmSFXSource.SetBaseVolume(0f);
        bgm2SFXSource.SetBaseVolume(bgmVolume);

        if (bgm2 != null && !bgm2AudioSource.isPlaying)
        {
            bgm2AudioSource.clip = bgm2;
            bgm2AudioSource.Play();
        }
        bgmAudioSource.Stop();
    }

    /// <summary>Play both BGM tracks simultaneously at full volume.</summary>
    public void PlayBothBGM()
    {
        isCrossfading = false;
        bgmSFXSource.SetBaseVolume(bgmVolume);
        bgm2SFXSource.SetBaseVolume(bgmVolume);

        if (bgm != null && !bgmAudioSource.isPlaying)
        {
            bgmAudioSource.clip = bgm;
            bgmAudioSource.Play();
        }
        if (bgm2 != null && !bgm2AudioSource.isPlaying)
        {
            bgm2AudioSource.clip = bgm2;
            bgm2AudioSource.Play();
        }
    }

    /// <summary>Stop all BGM tracks immediately.</summary>
    public void StopAllBGM()
    {
        isCrossfading = false;
        bgmAudioSource.Stop();
        bgm2AudioSource.Stop();
    }

    /// <summary>
    /// Smoothly crossfade to the specified BGM track over
    /// <see cref="crossfadeDuration"/> seconds.
    /// </summary>
    /// <param name="targetTrack">1 or 2.</param>
    public void CrossfadeToBGM(int targetTrack)
    {
        CrossfadeToBGM(targetTrack, crossfadeDuration);
    }

    /// <summary>
    /// Smoothly crossfade to the specified BGM track over the given duration.
    /// The outgoing track fades out while the incoming track fades in.
    /// </summary>
    /// <param name="targetTrack">1 or 2.</param>
    /// <param name="duration">Fade duration in seconds.</param>
    public void CrossfadeToBGM(int targetTrack, float duration)
    {
        crossfadeTarget = Mathf.Clamp(targetTrack, 1, 2);
        crossfadeDuration = Mathf.Max(duration, 0.1f);
        crossfadeTimer = 0f;
        isCrossfading = true;

        // Ensure both tracks are playing (the outgoing one will fade to silence).
        if (bgm != null && !bgmAudioSource.isPlaying)
        {
            bgmAudioSource.clip = bgm;
            bgmAudioSource.Play();
        }
        if (bgm2 != null && !bgm2AudioSource.isPlaying)
        {
            bgm2AudioSource.clip = bgm2;
            bgm2AudioSource.Play();
        }
    }

    private void UpdateCrossfade()
    {
        crossfadeTimer += Time.deltaTime;
        float t = Mathf.Clamp01(crossfadeTimer / crossfadeDuration);

        if (crossfadeTarget == 2)
        {
            // Fading: BGM 1 → BGM 2
            bgmSFXSource.SetBaseVolume(bgmVolume * (1f - t));
            bgm2SFXSource.SetBaseVolume(bgmVolume * t);
        }
        else
        {
            // Fading: BGM 2 → BGM 1
            bgmSFXSource.SetBaseVolume(bgmVolume * t);
            bgm2SFXSource.SetBaseVolume(bgmVolume * (1f - t));
        }

        if (t >= 1f)
        {
            isCrossfading = false;

            // Stop the track that's now silent.
            if (crossfadeTarget == 2) bgmAudioSource.Stop();
            else bgm2AudioSource.Stop();
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // Public Volume API  — use these to bind UI Sliders or buttons
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Set the BGM volume directly. Affects whichever tracks are currently active.
    /// Designed to be linked to a UI Slider's <c>OnValueChanged(float)</c> event.
    /// </summary>
    /// <param name="volume">0 = silent, 1 = full volume.</param>
    public void SetBGMVolume(float volume)
    {
        bgmVolume = Mathf.Clamp01(volume);

        // Update active tracks (only the ones currently audible).
        if (!isCrossfading)
        {
            if (bgmAudioSource.isPlaying) bgmSFXSource.SetBaseVolume(bgmVolume);
            if (bgm2AudioSource.isPlaying) bgm2SFXSource.SetBaseVolume(bgmVolume);
        }
        // During crossfade, the UpdateCrossfade loop handles volumes.

        PlayerPrefs.SetFloat(BGM_VOL_KEY, bgmVolume);
    }

    /// <summary>
    /// Set the SFX volume directly. Designed to be linked to a UI Slider's
    /// <c>OnValueChanged(float)</c> event.
    /// </summary>
    /// <param name="volume">0 = silent, 1 = full volume.</param>
    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        if (fixingSFXSource != null) fixingSFXSource.SetBaseVolume(sfxVolume);
        PlayerPrefs.SetFloat(SFX_VOL_KEY, sfxVolume);
    }

    /// <summary>Current BGM volume (0–1).</summary>
    public float GetBGMVolume() => bgmVolume;

    /// <summary>Current SFX volume (0–1).</summary>
    public float GetSFXVolume() => sfxVolume;

    // ─── Convenience: step-based increase / decrease ──────────────────

    public void IncreaseBGMVolume() => SetBGMVolume(bgmVolume + volumeStep);
    public void DecreaseBGMVolume() => SetBGMVolume(bgmVolume - volumeStep);

    public void IncreaseSFXVolume() => SetSFXVolume(sfxVolume + volumeStep);
    public void DecreaseSFXVolume() => SetSFXVolume(sfxVolume - volumeStep);
}
