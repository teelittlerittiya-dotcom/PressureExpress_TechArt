using System.Collections;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CargoController))]
[RequireComponent(typeof(ParticleManager))]
public sealed class CargoPolishController : MonoBehaviour
{
    private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    private static readonly int OutlineAlphaId = Shader.PropertyToID("_OutlineAlpha");
    private static readonly int OutlineGlowId = Shader.PropertyToID("_OutlineGlow");
    private static readonly int OutlinePixelWidthId = Shader.PropertyToID("_OutlinePixelWidth");
    private static readonly int RoundWaveStrengthId = Shader.PropertyToID("_RoundWaveStrength");
    private static readonly int HandDrawnAmountId = Shader.PropertyToID("_HandDrawnAmount");
    private static readonly int HitEffectColorId = Shader.PropertyToID("_HitEffectColor");
    private static readonly int HitEffectBlendId = Shader.PropertyToID("_HitEffectBlend");

    [Header("Shared Cargo Presentation")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Transform feedbackRoot;
    [SerializeField] private Transform vfxAnchor;

    private readonly Dictionary<MMF_Player, MMF_Player> runtimeFeedbacks = new Dictionary<MMF_Player, MMF_Player>();
    private CargoController cargoController;
    private ParticleManager particleManager;
    private CargoPolishProfile activeProfile;
    private MaterialPropertyBlock propertyBlock;
    private Coroutine impactPulseCoroutine;
    private Coroutine impactSquashCoroutine;
    private bool isLocalHovering;
    private float roundWaveStrength;
    private float handDrawnAmount;
    private float hitEffectBlend;
    private bool eventsSubscribed;
    private bool feedbackTransformCached;
    private Vector3 feedbackBaseLocalPosition;
    private Quaternion feedbackBaseLocalRotation;
    private Vector3 feedbackBaseLocalScale;

    public Transform FeedbackRoot => feedbackRoot;
    public Transform VfxAnchor => vfxAnchor;

    private bool ShouldPresent => cargoController == null || !cargoController.IsSpawned || cargoController.IsClient;

    private void Awake()
    {
        CacheReferences();
    }

    private void OnEnable()
    {
        CacheReferences();
        SubscribeEvents();
        if (cargoController != null && cargoController.IsInitialized) InitializePresentation();
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
        StopImpactMaterialPulse();
        StopImpactSquashAndRestore();

        foreach (MMF_Player player in runtimeFeedbacks.Values)
        {
            if (player != null) player.StopFeedbacks();
        }

        isLocalHovering = false;
        hitEffectBlend = 0f;
        ApplyMaterialProperties();
        RestoreFeedbackTransform();
        particleManager?.ClearStateParticles();
    }

    private void OnDestroy()
    {
        foreach (MMF_Player player in runtimeFeedbacks.Values)
        {
            if (player == null) continue;
            if (Application.isPlaying) Destroy(player.gameObject);
            else DestroyImmediate(player.gameObject);
        }
        runtimeFeedbacks.Clear();
    }

    public void ConfigureReferences(SpriteRenderer renderer, Transform newFeedbackRoot, Transform newVfxAnchor)
    {
        if (feedbackRoot != newFeedbackRoot) feedbackTransformCached = false;
        spriteRenderer = renderer;
        feedbackRoot = newFeedbackRoot;
        vfxAnchor = newVfxAnchor;
        CacheReferences();
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheReferences();
        if (cargoController == null || particleManager == null)
        {
            error = "CargoController or ParticleManager is missing.";
            return false;
        }

        if (spriteRenderer == null || feedbackRoot == null || vfxAnchor == null)
        {
            error = "SpriteRenderer, FeedbackRoot or VFXAnchor reference is missing.";
            return false;
        }

        if (feedbackRoot.GetComponentInChildren<Collider>(true) != null
            || feedbackRoot.GetComponentInChildren<Rigidbody>(true) != null
            || feedbackRoot.GetComponentInChildren<Collider2D>(true) != null
            || feedbackRoot.GetComponentInChildren<Rigidbody2D>(true) != null)
        {
            error = "FeedbackRoot must be visual-only and cannot contain physics components.";
            return false;
        }

        CargoItemData data = cargoController.cargoItemData;
        if (data == null || data.polishProfile == null)
        {
            error = "CargoItemData or CargoPolishProfile is missing.";
            return false;
        }

        return data.polishProfile.ValidateProfile(out error);
    }

    public void InitializePresentation()
    {
        CacheReferences();
        SubscribeEvents();
        if (!ShouldPresent || cargoController == null || cargoController.cargoItemData == null) return;

        CargoPolishProfile nextProfile = cargoController.cargoItemData.polishProfile;
        if (nextProfile == null) return;

        if (activeProfile != nextProfile)
        {
            StopImpactMaterialPulse();
            StopImpactSquashAndRestore();
            activeProfile = nextProfile;
            if (spriteRenderer != null) spriteRenderer.sharedMaterial = activeProfile.spriteMaterialPreset;
            roundWaveStrength = 0f;
            handDrawnAmount = 0f;
            hitEffectBlend = 0f;
        }

        particleManager?.SetEffectAnchor(vfxAnchor);
        isLocalHovering = cargoController.IsLocalPointerHovering;
        ApplyState(cargoController.CurrentRuntimeState);
    }

    public void ApplyState(CargoRuntimeState state)
    {
        if (!ShouldPresent) return;
        if (activeProfile == null) InitializePresentationProfileOnly();
        if (activeProfile == null || spriteRenderer == null || cargoController?.cargoItemData == null) return;

        ApplyStageSpriteAndParticles(state);
        EvaluateStatusMaterial(state);
        ApplyMaterialProperties();
    }

    public void PlayPickup(Vector3 worldPosition)
    {
        if (!ShouldPresent || activeProfile == null) return;
        PlayCue(activeProfile.pickup, worldPosition, 1f);
    }

    public void PlayRelease(Vector3 worldPosition)
    {
        if (!ShouldPresent || activeProfile == null) return;
        PlayCue(activeProfile.release, worldPosition, 1f);
    }

    private void CacheReferences()
    {
        if (cargoController == null) cargoController = GetComponent<CargoController>();
        if (particleManager == null) particleManager = GetComponent<ParticleManager>();
        if (spriteRenderer == null && cargoController != null) spriteRenderer = cargoController.SpriteRenderer;

        Transform visualRoot = cargoController != null ? cargoController.VisualRoot : transform.Find("VisualRoot");
        if (feedbackRoot == null && visualRoot != null) feedbackRoot = visualRoot.Find("FeedbackRoot");
        if (vfxAnchor == null && visualRoot != null) vfxAnchor = visualRoot.Find("VFXAnchor");
        if (feedbackRoot != null && !feedbackTransformCached)
        {
            feedbackBaseLocalPosition = feedbackRoot.localPosition;
            feedbackBaseLocalRotation = feedbackRoot.localRotation;
            feedbackBaseLocalScale = feedbackRoot.localScale;
            feedbackTransformCached = true;
        }
        propertyBlock ??= new MaterialPropertyBlock();
    }

    private void RestoreFeedbackTransform()
    {
        if (!feedbackTransformCached || feedbackRoot == null) return;
        feedbackRoot.SetLocalPositionAndRotation(feedbackBaseLocalPosition, feedbackBaseLocalRotation);
        feedbackRoot.localScale = feedbackBaseLocalScale;
    }

    private void RestoreFeedbackScale()
    {
        if (!feedbackTransformCached || feedbackRoot == null) return;
        feedbackRoot.localScale = feedbackBaseLocalScale;
    }

    private void SubscribeEvents()
    {
        if (eventsSubscribed || cargoController == null) return;
        cargoController.RuntimeStateChanged += ApplyState;
        cargoController.LocalPointerHoverChanged += SetLocalHover;
        cargoController.ImpactPresentationRequested += PlayImpact;
        eventsSubscribed = true;
    }

    private void UnsubscribeEvents()
    {
        if (!eventsSubscribed || cargoController == null) return;
        cargoController.RuntimeStateChanged -= ApplyState;
        cargoController.LocalPointerHoverChanged -= SetLocalHover;
        cargoController.ImpactPresentationRequested -= PlayImpact;
        eventsSubscribed = false;
    }

    private void InitializePresentationProfileOnly()
    {
        CacheReferences();
        if (cargoController?.cargoItemData == null) return;
        activeProfile = cargoController.cargoItemData.polishProfile;
        if (activeProfile != null && spriteRenderer != null)
        {
            spriteRenderer.sharedMaterial = activeProfile.spriteMaterialPreset;
        }
        particleManager?.SetEffectAnchor(vfxAnchor);
    }

    private void ApplyStageSpriteAndParticles(CargoRuntimeState state)
    {
        CargoItemData data = cargoController.cargoItemData;
        if (!state.Initialized)
        {
            spriteRenderer.sprite = data.defaultSprite;
            if (particleManager != null)
            {
                foreach (CargoModule module in data.GetModules())
                {
                    if (module != null) particleManager.UpdateStateParticle(module.GetModuleType(), null);
                }
            }
            return;
        }

        Sprite selectedSprite = data.defaultSprite;
        CargoModuleId[] visualPriority = { CargoModuleId.Impact, CargoModuleId.Pressure };

        foreach (CargoModuleId id in visualPriority)
        {
            CargoModule module = data.GetModule(id);
            if (module == null || !state.Has(id)) continue;
            Sprite stageSprite = module.GetSprite(state.Get(id));
            if (stageSprite == null) continue;
            selectedSprite = stageSprite;
            break;
        }

        spriteRenderer.sprite = selectedSprite;
        if (particleManager == null) return;

        foreach (CargoModule module in data.GetModules())
        {
            if (module == null) continue;
            CargoModuleId id = CargoModuleUtility.FromModule(module);
            if (UsesSpriteVisual(id))
            {
                particleManager.UpdateStateParticle(module.GetModuleType(), null);
                continue;
            }

            float value = state.Has(id) ? state.Get(id) : module.GetMaxValue();
            particleManager.UpdateStateParticle(module.GetModuleType(), module.GetParticlePrefab(value));
        }
    }

    private void EvaluateStatusMaterial(CargoRuntimeState state)
    {
        roundWaveStrength = 0f;
        handDrawnAmount = 0f;
        if (!activeProfile.useAllIn1MaterialEffects || !activeProfile.statusMaterial.enabled) return;

        CargoStatusMaterialSettings settings = activeProfile.statusMaterial;
        CargoItemData data = cargoController.cargoItemData;

        if (state.Has(CargoModuleId.Temperature)
            && data.GetModule(CargoModuleId.Temperature) is TemperatureModule temperature)
        {
            float value = state.Get(CargoModuleId.Temperature);
            if (value > temperature.idealTemp)
            {
                float heat = Mathf.InverseLerp(temperature.idealTemp, temperature.GetMaxValue(), value);
                roundWaveStrength = Mathf.Max(roundWaveStrength, settings.heatRoundWaveStrength.Evaluate(heat));
            }
            else if (value < temperature.idealTemp)
            {
                float cold = Mathf.InverseLerp(temperature.idealTemp, temperature.GetMinValue(), value);
                handDrawnAmount = Mathf.Max(handDrawnAmount, settings.coldHandDrawnAmount.Evaluate(cold));
            }
        }

        ApplyLowValueResponse(state, data, CargoModuleId.Pressure, settings.lowPressureRoundWaveStrength, ref roundWaveStrength);
        ApplyLowValueResponse(state, data, CargoModuleId.Freshness, settings.staleHandDrawnAmount, ref handDrawnAmount);
    }

    private static void ApplyLowValueResponse(
        CargoRuntimeState state,
        CargoItemData data,
        CargoModuleId id,
        AnimationCurve curve,
        ref float output)
    {
        CargoModule module = data.GetModule(id);
        if (module == null || !state.Has(id) || curve == null) return;
        float severity = 1f - module.GetNormalizedValue(state.Get(id));
        output = Mathf.Max(output, curve.Evaluate(Mathf.Clamp01(severity)));
    }

    private void SetLocalHover(bool hovering)
    {
        isLocalHovering = hovering;
        ApplyMaterialProperties();
    }

    private void PlayImpact(CargoImpactPresentationEvent impactEvent)
    {
        if (!ShouldPresent) return;
        if (activeProfile == null) InitializePresentationProfileOnly();
        if (activeProfile == null) return;

        float cueIntensity = activeProfile.impact.EvaluateIntensity(impactEvent.Strength);
        float squashIntensity = activeProfile.impactSquash?.EvaluateIntensity(impactEvent.Strength) ?? 0f;
        if (cueIntensity <= 0f && squashIntensity <= 0f) return;

        if (squashIntensity > 0f) PlayImpactSquash(squashIntensity);
        if (cueIntensity <= 0f) return;
        PlayCue(activeProfile.impact, impactEvent.ContactPoint, impactEvent.Strength);
        if (!activeProfile.useAllIn1MaterialEffects || !activeProfile.impactMaterial.enabled) return;

        StopImpactMaterialPulse();
        impactPulseCoroutine = StartCoroutine(PlayImpactMaterialPulse(cueIntensity));
    }

    private void PlayImpactSquash(float intensity)
    {
        CargoImpactSquashSettings settings = activeProfile.impactSquash;
        if (settings == null
            || !settings.enabled
            || settings.maxScaleDelta <= 0f
            || settings.deformationOverTime == null
            || feedbackRoot == null) return;

        StopImpactSquashAndRestore();
        impactSquashCoroutine = StartCoroutine(PlayImpactSquashAnimation(settings, intensity));
    }

    private IEnumerator PlayImpactSquashAnimation(CargoImpactSquashSettings settings, float intensity)
    {
        float duration = Mathf.Max(0.01f, settings.duration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float normalizedTime = Mathf.Clamp01(elapsed / duration);
            float response = settings.deformationOverTime.Evaluate(normalizedTime);
            float deformation = response * settings.maxScaleDelta * Mathf.Clamp01(intensity);
            ApplyImpactSquashDeformation(deformation, settings.horizontalCompensation);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        RestoreFeedbackScale();
        impactSquashCoroutine = null;
    }

    private void ApplyImpactSquashDeformation(float deformation, float horizontalCompensation)
    {
        if (!feedbackTransformCached || feedbackRoot == null) return;

        float safeDeformation = Mathf.Clamp(deformation, -0.05f, 0.1f);
        float compensation = Mathf.Clamp01(horizontalCompensation);
        Vector3 scaleMultiplier = new Vector3(
            1f + safeDeformation * compensation,
            1f - safeDeformation,
            1f);
        feedbackRoot.localScale = Vector3.Scale(feedbackBaseLocalScale, scaleMultiplier);
    }

    private void StopImpactSquashAndRestore()
    {
        if (impactSquashCoroutine != null)
        {
            StopCoroutine(impactSquashCoroutine);
            impactSquashCoroutine = null;
        }
        RestoreFeedbackScale();
    }

    private void PlayCue(CargoPolishCue cue, Vector3 worldPosition, float strength)
    {
        if (cue == null || !cue.enabled) return;
        float intensity = cue.EvaluateIntensity(strength);
        if (intensity <= 0f) return;

        MMF_Player feedback = ResolveRuntimeFeedback(cue.feelTemplate);
        if (feedback != null) feedback.PlayFeedbacks(worldPosition, intensity);

        AudioClip clip = cue.PickSfx();
        if (clip != null && SpatialAudioManager.Instance != null)
        {
            SpatialAudioManager.Instance.PlaySFXAtPosition(clip, worldPosition, cue.volume * intensity);
        }
    }

    private MMF_Player ResolveRuntimeFeedback(MMF_Player template)
    {
        if (template == null || feedbackRoot == null) return null;
        if (runtimeFeedbacks.TryGetValue(template, out MMF_Player cached) && cached != null) return cached;

        GameObject instance = Instantiate(template.gameObject, feedbackRoot);
        instance.name = $"{template.gameObject.name} (Runtime)";
        instance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        MMF_Player player = instance.GetComponent<MMF_Player>();
        if (player == null)
        {
            Destroy(instance);
            return null;
        }

        MMF_ReferenceHolder holder = null;
        foreach (MMF_Feedback feedback in player.FeedbacksList)
        {
            if (feedback is not MMF_ReferenceHolder candidate) continue;
            holder = candidate;
            break;
        }

        if (holder == null)
        {
            Debug.LogError($"{activeProfile.name}: FEEL template {template.name} needs an MMF Reference Holder.", activeProfile);
            Destroy(instance);
            return null;
        }

        holder.GameObjectReference = feedbackRoot.gameObject;
        holder.ForceReferenceOnAll = true;
        player.Initialization(true);
        runtimeFeedbacks.Add(template, player);
        return player;
    }

    private IEnumerator PlayImpactMaterialPulse(float intensity)
    {
        float duration = Mathf.Max(0.01f, activeProfile.impactMaterial.duration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float normalizedTime = Mathf.Clamp01(elapsed / duration);
            hitEffectBlend = Mathf.Clamp01(activeProfile.impactMaterial.blendOverTime.Evaluate(normalizedTime) * intensity);
            ApplyMaterialProperties();
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        hitEffectBlend = 0f;
        ApplyMaterialProperties();
        impactPulseCoroutine = null;
    }

    private void StopImpactMaterialPulse()
    {
        if (impactPulseCoroutine != null)
        {
            StopCoroutine(impactPulseCoroutine);
            impactPulseCoroutine = null;
        }
        hitEffectBlend = 0f;
    }

    private void ApplyMaterialProperties()
    {
        if (spriteRenderer == null || activeProfile == null || !activeProfile.useAllIn1MaterialEffects) return;

        propertyBlock ??= new MaterialPropertyBlock();
        spriteRenderer.GetPropertyBlock(propertyBlock);

        CargoHoverPolishSettings hover = activeProfile.hover;
        bool showOutline = hover.enabled && isLocalHovering;
        propertyBlock.SetColor(OutlineColorId, hover.outlineColor);
        propertyBlock.SetFloat(OutlineAlphaId, showOutline ? Mathf.Clamp01(hover.outlineAlpha) : 0f);
        propertyBlock.SetFloat(OutlineGlowId, Mathf.Clamp(hover.outlineGlow, 1f, 100f));
        propertyBlock.SetFloat(OutlinePixelWidthId, Mathf.Clamp(hover.outlinePixelWidth, 1, 8));
        propertyBlock.SetFloat(RoundWaveStrengthId, Mathf.Clamp01(roundWaveStrength));
        propertyBlock.SetFloat(HandDrawnAmountId, Mathf.Clamp(handDrawnAmount, 0f, 20f));
        propertyBlock.SetColor(HitEffectColorId, activeProfile.impactMaterial.color);
        propertyBlock.SetFloat(HitEffectBlendId, hitEffectBlend);
        spriteRenderer.SetPropertyBlock(propertyBlock);
    }

    private static bool UsesSpriteVisual(CargoModuleId id)
    {
        return id == CargoModuleId.Impact || id == CargoModuleId.Pressure;
    }
}
