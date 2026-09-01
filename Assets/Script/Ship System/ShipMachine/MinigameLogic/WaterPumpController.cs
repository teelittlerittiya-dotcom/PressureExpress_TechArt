using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Self-contained water pump controller with local button-click audio.
/// Place this on the pump machine GameObject and wire the UI buttons to
/// <see cref="OnPumpInPressed"/> and <see cref="OnPumpOutPressed"/>.
///
/// The click sound plays instantly on the calling client (no network delay).
/// The actual pump action is forwarded to <see cref="SubmarineManager"/>
/// on the server via RPC.
/// </summary>
public class WaterPumpController : NetworkBehaviour
{
    // ─── Audio ─────────────────────────────────────────────────────────
    [Header("Button Press Audio")]
    [Tooltip("Short click / button-press clip played instantly when the player " +
             "presses either Pump In or Pump Out.")]
    [SerializeField] private AudioClip buttonClickClip;

    [Tooltip("Volume of the button-click SFX.")]
    [SerializeField, Range(0f, 1f)] private float clickVolume = 0.9f;

    // ─── Pump Settings ─────────────────────────────────────────────────
    [Header("Pump Settings")]
    [Tooltip("Amount of water moved per button press (Pump In = fill ballast, " +
             "Pump Out = drain leaked water).")]
    [SerializeField] private float pumpAmount = 5f;

    // ─── Runtime ───────────────────────────────────────────────────────
    private AudioSource clickAudioSource;

    private void Awake()
    {
        // Create a 2D AudioSource for the instant button-click feedback.
        // NOT spatialized — it's direct UI feedback for the pressing player.
        clickAudioSource = gameObject.AddComponent<AudioSource>();
        clickAudioSource.playOnAwake = false;
        clickAudioSource.loop = false;
        clickAudioSource.spatialBlend = 0f;
        clickAudioSource.volume = clickVolume;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Public API — wire these to your UI Button.onClick events
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Call from the "Pump In" UI button.
    /// Plays a click sound locally, then asks the server to fill ballast.
    /// </summary>
    public void OnPumpInPressed()
    {
        PlayClickLocal();
        PumpInServerRpc(pumpAmount);
    }

    /// <summary>
    /// Call from the "Pump Out" UI button.
    /// Plays a click sound locally, then asks the server to drain water.
    /// </summary>
    public void OnPumpOutPressed()
    {
        PlayClickLocal();
        PumpOutServerRpc(pumpAmount);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Audio
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Plays the button-click clip locally (2D, no network round-trip).
    /// </summary>
    private void PlayClickLocal()
    {
        if (buttonClickClip != null && clickAudioSource != null)
        {
            clickAudioSource.PlayOneShot(buttonClickClip, clickVolume);
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // Server RPCs
    // ═══════════════════════════════════════════════════════════════════

    [Rpc(SendTo.Server)]
    private void PumpInServerRpc(float amount)
    {
        if (SubmarineManager.Instance != null)
        {
            SubmarineManager.Instance.AdjustBallast(amount);
        }
    }

    [Rpc(SendTo.Server)]
    private void PumpOutServerRpc(float amount)
    {
        if (SubmarineManager.Instance != null)
        {
            SubmarineManager.Instance.ReduceGlobalWater(amount);
        }
    }
}
