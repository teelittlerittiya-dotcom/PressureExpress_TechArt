using System;
using System.Linq;
using MoreMountains.Feedbacks;
using UnityEngine;

[Serializable]
public readonly struct CargoImpactPresentationEvent
{
    public CargoImpactPresentationEvent(float strength, Vector3 contactPoint)
    {
        Strength = Mathf.Max(0f, strength);
        ContactPoint = contactPoint;
    }

    public float Strength { get; }
    public Vector3 ContactPoint { get; }
}

[Serializable]
public sealed class CargoHoverPolishSettings
{
    public bool enabled = true;
    public Color outlineColor = new Color(1f, 0.9f, 0.25f, 1f);
    [Range(0f, 1f)] public float outlineAlpha = 0.25f;
    [Range(1, 8)] public int outlinePixelWidth = 1;
    [Range(1f, 10f)] public float outlineGlow = 1.5f;
}

[Serializable]
public sealed class CargoStatusMaterialSettings
{
    public bool enabled = true;

    [Tooltip("Temperature above ideal -> AllIn1 round-wave strength.")]
    public AnimationCurve heatRoundWaveStrength = AnimationCurve.Linear(0f, 0f, 1f, 0.7f);

    [Tooltip("Temperature below ideal -> AllIn1 hand-drawn amount.")]
    public AnimationCurve coldHandDrawnAmount = AnimationCurve.Linear(0f, 0f, 1f, 10f);

    [Tooltip("Low pressure severity -> extra round-wave strength.")]
    public AnimationCurve lowPressureRoundWaveStrength = AnimationCurve.Linear(0f, 0f, 1f, 0f);

    [Tooltip("Low freshness severity -> extra hand-drawn amount.")]
    public AnimationCurve staleHandDrawnAmount = AnimationCurve.Linear(0f, 0f, 1f, 0f);
}

[Serializable]
public sealed class CargoImpactMaterialSettings
{
    public bool enabled = true;
    public Color color = Color.white;
    [Min(0.01f)] public float duration = 0.12f;
    public AnimationCurve blendOverTime = new AnimationCurve(
        new Keyframe(0f, 1f),
        new Keyframe(1f, 0f));
}

[Serializable]
public sealed class CargoImpactSquashSettings
{
    public bool enabled = true;
    [Tooltip("Impacts at or below this presentation strength do not squash.")]
    [Min(0f)] public float minimumStrength = 0.05f;
    [Tooltip("Presentation strength that reaches the maximum deformation.")]
    [Min(0.01f)] public float fullStrength = 0.5f;
    public AnimationCurve strengthResponse = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    [Tooltip("Maximum visual-only scale deformation at full impact strength.")]
    [Range(0f, 0.1f)] public float maxScaleDelta = 0.025f;
    [Min(0.01f)] public float duration = 0.11f;
    [Range(0f, 1f)] public float horizontalCompensation = 0.5f;
    public AnimationCurve deformationOverTime = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.15f, 1f),
        new Keyframe(0.55f, -0.2f),
        new Keyframe(1f, 0f));

    public float EvaluateIntensity(float strength)
    {
        if (!enabled || strength <= minimumStrength) return 0f;
        float range = Mathf.Max(0.01f, fullStrength - minimumStrength);
        float normalized = Mathf.Clamp01((strength - minimumStrength) / range);
        return Mathf.Clamp01(strengthResponse == null ? normalized : strengthResponse.Evaluate(normalized));
    }
}

[Serializable]
public sealed class CargoPolishCue
{
    public bool enabled;
    [Tooltip("Prefab component containing the FEEL sequence. Keep world SFX and material edits outside this sequence.")]
    public MMF_Player feelTemplate;
    public AudioClip[] spatialSfx = Array.Empty<AudioClip>();
    [Min(0f)] public float minimumStrength = 0.5f;
    [Min(0.01f)] public float fullStrength = 10f;
    [Range(0f, 1f)] public float volume = 1f;
    public AnimationCurve intensityRemap = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    public float EvaluateIntensity(float strength)
    {
        if (!enabled) return 0f;
        float range = Mathf.Max(0.01f, fullStrength - minimumStrength);
        float normalized = Mathf.Clamp01((strength - minimumStrength) / range);
        return Mathf.Clamp01(intensityRemap == null ? normalized : intensityRemap.Evaluate(normalized));
    }

    public AudioClip PickSfx()
    {
        if (spatialSfx == null || spatialSfx.Length == 0) return null;
        AudioClip[] valid = spatialSfx.Where(clip => clip != null).ToArray();
        return valid.Length == 0 ? null : valid[UnityEngine.Random.Range(0, valid.Length)];
    }

    public bool ValidateCue(string label, out string error)
    {
        if (!enabled)
        {
            error = null;
            return true;
        }

        if (fullStrength <= minimumStrength)
        {
            error = $"{label}: fullStrength must be greater than minimumStrength.";
            return false;
        }

        if (intensityRemap == null)
        {
            error = $"{label}: intensityRemap is missing.";
            return false;
        }

        if (feelTemplate == null && (spatialSfx == null || spatialSfx.All(clip => clip == null)))
        {
            error = $"{label}: enabled cue has no FEEL template or spatial SFX.";
            return false;
        }

        if (spatialSfx != null && spatialSfx.Any(clip => clip == null))
        {
            error = $"{label}: spatialSfx contains a null entry.";
            return false;
        }

        error = null;
        return true;
    }
}

[CreateAssetMenu(menuName = "Cargo/Polish Profile", fileName = "CargoPolishProfile")]
public sealed class CargoPolishProfile : ScriptableObject
{
    [Header("Material Preset")]
    [Tooltip("Shared preset. Runtime values are applied per SpriteRenderer with a MaterialPropertyBlock.")]
    public Material spriteMaterialPreset;
    public bool useAllIn1MaterialEffects;

    [Header("Local Hover")]
    public CargoHoverPolishSettings hover = new CargoHoverPolishSettings();

    [Header("Status -> Material")]
    public CargoStatusMaterialSettings statusMaterial = new CargoStatusMaterialSettings();

    [Header("Impact Material Pulse")]
    public CargoImpactMaterialSettings impactMaterial = new CargoImpactMaterialSettings();

    [Header("Impact Visual Squash")]
    public CargoImpactSquashSettings impactSquash = new CargoImpactSquashSettings();

    [Header("Semantic Event Cues")]
    public CargoPolishCue impact = new CargoPolishCue { enabled = false };
    public CargoPolishCue pickup = new CargoPolishCue { enabled = false, minimumStrength = 0f, fullStrength = 1f };
    public CargoPolishCue release = new CargoPolishCue { enabled = false, minimumStrength = 0f, fullStrength = 1f };

    public bool ValidateProfile(out string error)
    {
        if (spriteMaterialPreset == null)
        {
            error = $"{name}: spriteMaterialPreset is missing.";
            return false;
        }

        if (hover == null || statusMaterial == null || impactMaterial == null || impactSquash == null
            || impact == null || pickup == null || release == null)
        {
            error = $"{name}: one or more polish setting groups are missing.";
            return false;
        }

        if (statusMaterial.heatRoundWaveStrength == null
            || statusMaterial.coldHandDrawnAmount == null
            || statusMaterial.lowPressureRoundWaveStrength == null
            || statusMaterial.staleHandDrawnAmount == null)
        {
            error = $"{name}: one or more status material curves are missing.";
            return false;
        }

        if (impactMaterial.blendOverTime == null || impactMaterial.duration <= 0f)
        {
            error = $"{name}: impact material pulse is invalid.";
            return false;
        }

        if (impactSquash.enabled
            && (impactSquash.strengthResponse == null
                || impactSquash.deformationOverTime == null
                || impactSquash.fullStrength <= impactSquash.minimumStrength
                || impactSquash.duration <= 0f
                || impactSquash.maxScaleDelta < 0f
                || impactSquash.maxScaleDelta > 0.1f
                || impactSquash.horizontalCompensation < 0f
                || impactSquash.horizontalCompensation > 1f))
        {
            error = $"{name}: impact squash settings are invalid.";
            return false;
        }

        if (useAllIn1MaterialEffects
            && (spriteMaterialPreset.shader == null
                || !spriteMaterialPreset.shader.name.StartsWith("AllIn1SpriteShader/", StringComparison.Ordinal)))
        {
            error = $"{name}: material effects require an AllIn1 Sprite Shader material preset.";
            return false;
        }

        if (!impact.ValidateCue("Impact", out error)
            || !pickup.ValidateCue("Pickup", out error)
            || !release.ValidateCue("Release", out error))
        {
            return false;
        }

        error = null;
        return true;
    }
}
