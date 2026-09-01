using UnityEngine;

/// <summary>
/// Attach to Player prefabs or Voice Chat AudioSources.
/// Dynamically applies a muffled underwater low-pass filter and subtle bubbling pitch distortion
/// whenever the voice/sound emitter is submerged in room water.
/// </summary>
[DisallowMultipleComponent]
public class UnderwaterVoiceAudioFilter : MonoBehaviour
{
    [Header("Underwater Voice Settings")]
    [Tooltip("Low-pass cutoff frequency when submerged (lower = more muffled voice).")]
    [SerializeField] private float submergedCutoff = 600f;
    [Tooltip("Normal low-pass cutoff frequency when out of water.")]
    [SerializeField] private float normalCutoff = 22000f;
    [Tooltip("Filter transition speed.")]
    [SerializeField] private float transitionSpeed = 8f;

    [Header("Bubbling Pitch Distortion")]
    [Tooltip("Enable subtle underwater voice pitch oscillation for bubbling voice effect.")]
    [SerializeField] private bool enableBubbleDistortion = true;
    [SerializeField] private float bubblePitchDepth = 0.05f;
    [SerializeField] private float bubblePitchSpeed = 12f;

    private AudioSource voiceAudioSource;
    private AudioLowPassFilter lowPassFilter;
    private float defaultPitch = 1f;

    private void Awake()
    {
        voiceAudioSource = GetComponent<AudioSource>();
        EnsureLowPassFilter();

        if (voiceAudioSource != null)
        {
            defaultPitch = voiceAudioSource.pitch;
        }
    }

    private void EnsureLowPassFilter()
    {
        lowPassFilter = GetComponent<AudioLowPassFilter>();
        if (lowPassFilter == null)
        {
            lowPassFilter = gameObject.AddComponent<AudioLowPassFilter>();
        }
        lowPassFilter.cutoffFrequency = normalCutoff;
    }

    private void Update()
    {
        Vector3 pos = transform.position;
        bool inWater = RoomWaterVisualizer.TryGetWaterSurfaceY(pos, out float surfaceY, out _);
        bool submerged = inWater && (pos.y <= surfaceY);

        if (lowPassFilter != null)
        {
            float targetCutoff = submerged ? submergedCutoff : normalCutoff;
            lowPassFilter.cutoffFrequency = Mathf.Lerp(
                lowPassFilter.cutoffFrequency,
                targetCutoff,
                Time.deltaTime * transitionSpeed
            );
        }

        if (voiceAudioSource != null && enableBubbleDistortion)
        {
            if (submerged && voiceAudioSource.isPlaying)
            {
                float pitchModulation = Mathf.Sin(Time.time * bubblePitchSpeed) * bubblePitchDepth;
                voiceAudioSource.pitch = defaultPitch + pitchModulation;
            }
            else if (voiceAudioSource.pitch != defaultPitch)
            {
                voiceAudioSource.pitch = Mathf.Lerp(voiceAudioSource.pitch, defaultPitch, Time.deltaTime * transitionSpeed);
            }
        }
    }
}
