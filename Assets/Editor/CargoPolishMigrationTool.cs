using System;
using System.Collections.Generic;
using System.IO;
using AllIn1SpriteShader;
using MoreMountains.Feedbacks;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace PressureExpress.EditorTools
{
    public static class CargoPolishMigrationTool
    {
        private const string PolishFolder = "Assets/Data/Cargo/Polish";
        private const string FeelFolder = PolishFolder + "/FEEL";
        private const string AllIn1MaterialPath = PolishFolder + "/Cargo AllIn1 Lit Polish.mat";
        private const string NeutralProfilePath = PolishFolder + "/Neutral Cargo Polish.asset";
        private const string SoftProfilePath = PolishFolder + "/Soft Cargo Polish.asset";
        private const string ExplosiveProfilePath = PolishFolder + "/Explosive Cargo Polish.asset";
        private const string SoftFeelPath = FeelFolder + "/Cargo Soft Impact FEEL.prefab";
        private const string ExplosiveFeelPath = FeelFolder + "/Cargo Explosive Impact FEEL.prefab";
        private const string NeutralFeelPath = FeelFolder + "/Cargo Neutral Impact FEEL.prefab";

        [MenuItem("Tools/Cargo/Setup Polish Pilot")]
        public static void SetupPolishPilot()
        {
            EnsureFolder(PolishFolder);
            EnsureFolder(FeelFolder);

            Material allIn1Material = CreateAllIn1Material();
            MMF_Player neutralFeel = CreateFeelTemplate(NeutralFeelPath, "Assets/Simple FX Kit/Prefabs/Spheres Explode.prefab");
            MMF_Player softFeel = CreateFeelTemplate(SoftFeelPath, "Assets/Simple FX Kit/Prefabs/Squares Explode.prefab");
            MMF_Player explosiveFeel = CreateFeelTemplate(ExplosiveFeelPath, "Assets/Simple FX Kit/Prefabs/Explosion Fire.prefab");
            neutralFeel = RemoveLegacySquashFeedback(NeutralFeelPath);
            softFeel = RemoveLegacySquashFeedback(SoftFeelPath);
            explosiveFeel = RemoveLegacySquashFeedback(ExplosiveFeelPath);

            CargoPolishProfile neutral = CreateNeutralProfile(neutralFeel);
            CargoPolishProfile soft = CreateSoftProfile(allIn1Material, softFeel);
            CargoPolishProfile explosive = CreateExplosiveProfile(allIn1Material, explosiveFeel);

            AssignProfiles(neutral, soft, explosive);
            MigrateSharedCargoPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!CargoPolishProfileValidator.ValidateAll(out string profileReport))
                throw new InvalidOperationException(profileReport);
            if (!CargoPrototypeValidator.ValidatePrefab(CargoPrototypeValidator.CargoPrefabPath, out string prefabReport))
                throw new InvalidOperationException(prefabReport);

            Debug.Log($"Cargo polish pilot setup complete.\n{profileReport}\n{prefabReport}");
        }

        [MenuItem("Tools/Cargo/Apply Conservative Polish Tuning")]
        public static void ApplyConservativePolishTuning()
        {
            CargoPolishProfile neutral = RequireProfile(NeutralProfilePath);
            CargoPolishProfile soft = RequireProfile(SoftProfilePath);
            CargoPolishProfile explosive = RequireProfile(ExplosiveProfilePath);

            MMF_Player neutralFeel = RemoveLegacySquashFeedback(NeutralFeelPath);
            MMF_Player softFeel = RemoveLegacySquashFeedback(SoftFeelPath);
            MMF_Player explosiveFeel = RemoveLegacySquashFeedback(ExplosiveFeelPath);
            if (neutral.impact.feelTemplate != null) neutral.impact.feelTemplate = neutralFeel;
            if (soft.impact.feelTemplate != null) soft.impact.feelTemplate = softFeel;
            if (explosive.impact.feelTemplate != null) explosive.impact.feelTemplate = explosiveFeel;

            ApplyNeutralVisualTuning(neutral);
            ApplySoftVisualTuning(soft);
            ApplyExplosiveVisualTuning(explosive);
            EditorUtility.SetDirty(neutral);
            EditorUtility.SetDirty(soft);
            EditorUtility.SetDirty(explosive);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!CargoPolishProfileValidator.ValidateAll(out string report))
                throw new InvalidOperationException(report);
            Debug.Log($"Cargo conservative polish tuning complete.\n{report}");
        }

        private static Material CreateAllIn1Material()
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(AllIn1MaterialPath);
            if (existing != null) return existing;

            Shader shader = Shader.Find("AllIn1SpriteShader/AllIn1SpriteShaderLit");
            if (shader == null) throw new InvalidOperationException("AllIn1 Sprite Shader Lit was not found.");

            Material material = new Material(shader) { name = "Cargo AllIn1 Lit Polish" };
            foreach (string keyword in new[]
                     {
                         "OUTBASE_ON",
                         "OUTBASEPIXELPERF_ON",
                         "HITEFFECT_ON",
                         "DOODLE_ON",
                         "ROUNDWAVEUV_ON"
                     })
            {
                material.EnableKeyword(keyword);
            }

            material.SetFloat("_OutlineAlpha", 0f);
            material.SetFloat("_OutlinePixelWidth", 1f);
            material.SetFloat("_HitEffectBlend", 0f);
            material.SetFloat("_HandDrawnAmount", 0f);
            material.SetFloat("_RoundWaveStrength", 0f);
            AssetDatabase.CreateAsset(material, AllIn1MaterialPath);
            return material;
        }

        private static CargoPolishProfile CreateNeutralProfile(MMF_Player feelTemplate)
        {
            CargoPolishProfile profile = LoadOrCreateProfile(NeutralProfilePath, out bool created);
            if (created)
            {
                profile.spriteMaterialPreset = AssetDatabase.LoadAssetAtPath<Material>("Assets/Shaders/Sprite-Lit-Posterized.mat");
                profile.useAllIn1MaterialEffects = false;
                profile.pickup.enabled = false;
                profile.release.enabled = false;
                ApplyNeutralVisualTuning(profile);
            }

            // Upgrade only the migration-created empty cue. Once a template is present,
            // rerunning this tool leaves any designer tuning untouched.
            if (profile.impact.feelTemplate == null)
            {
                ConfigureImpactCue(
                    profile.impact,
                    feelTemplate,
                    "Assets/Audio/egg-cracking.wav",
                    0.5f,
                    10f,
                    1f);
            }
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static CargoPolishProfile CreateSoftProfile(Material material, MMF_Player feelTemplate)
        {
            CargoPolishProfile profile = LoadOrCreateProfile(SoftProfilePath, out bool created);
            if (!created) return profile;

            profile.spriteMaterialPreset = material;
            profile.useAllIn1MaterialEffects = true;
            ApplySoftVisualTuning(profile);
            ConfigureImpactCue(
                profile.impact,
                feelTemplate,
                "Assets/Audio/egg-cracking.wav",
                0.5f,
                10f,
                0.8f);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static CargoPolishProfile CreateExplosiveProfile(Material material, MMF_Player feelTemplate)
        {
            CargoPolishProfile profile = LoadOrCreateProfile(ExplosiveProfilePath, out bool created);
            if (!created) return profile;

            profile.spriteMaterialPreset = material;
            profile.useAllIn1MaterialEffects = true;
            ApplyExplosiveVisualTuning(profile);
            ConfigureImpactCue(
                profile.impact,
                feelTemplate,
                "Assets/Feel/NiceVibrations/HapticSamples/Impacts/CarCrash1.wav",
                0.75f,
                14f,
                1f);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static void ConfigureImpactCue(
            CargoPolishCue cue,
            MMF_Player feelTemplate,
            string sfxPath,
            float minimumStrength,
            float fullStrength,
            float volume)
        {
            cue.enabled = true;
            cue.feelTemplate = feelTemplate;
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(sfxPath);
            cue.spatialSfx = clip == null ? Array.Empty<AudioClip>() : new[] { clip };
            cue.minimumStrength = minimumStrength;
            cue.fullStrength = fullStrength;
            cue.volume = volume;
            cue.intensityRemap = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        }

        private static CargoPolishProfile LoadOrCreateProfile(string path, out bool created)
        {
            CargoPolishProfile profile = AssetDatabase.LoadAssetAtPath<CargoPolishProfile>(path);
            created = profile == null;
            if (!created) return profile;

            profile = ScriptableObject.CreateInstance<CargoPolishProfile>();
            profile.name = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(profile, path);
            return profile;
        }

        private static MMF_Player CreateFeelTemplate(string path, string particlePrefabPath)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing.GetComponent<MMF_Player>();

            GameObject template = new GameObject(Path.GetFileNameWithoutExtension(path));
            try
            {
                MMF_Player player = template.AddComponent<MMF_Player>();
                player.FeedbacksList = new List<MMF_Feedback>();

                MMF_ReferenceHolder holder = (MMF_ReferenceHolder)player.AddFeedback(typeof(MMF_ReferenceHolder));
                holder.ForceReferenceOnAll = true;

                GameObject particleObject = AssetDatabase.LoadAssetAtPath<GameObject>(particlePrefabPath);
                ParticleSystem particle = particleObject != null
                    ? particleObject.GetComponentInChildren<ParticleSystem>(true)
                    : null;
                if (particle == null) throw new InvalidOperationException($"Particle prefab missing: {particlePrefabPath}");

                MMF_ParticlesInstantiation particles =
                    (MMF_ParticlesInstantiation)player.AddFeedback(typeof(MMF_ParticlesInstantiation));
                particles.ParticlesPrefab = particle;
                particles.Mode = MMF_ParticlesInstantiation.Modes.Pool;
                particles.ObjectPoolSize = 4;
                particles.MutualizePools = true;
                particles.PositionMode = MMF_ParticlesInstantiation.PositionModes.Script;
                particles.NestParticles = false;
                particles.DeclaredDuration = Mathf.Max(0.1f, particle.main.duration);

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(template, path);
                return prefab.GetComponent<MMF_Player>();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(template);
            }
        }

        private static MMF_Player RemoveLegacySquashFeedback(string path)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                MMF_Player player = root.GetComponent<MMF_Player>();
                if (player == null) throw new InvalidOperationException($"FEEL template has no MMF_Player: {path}");

                int removed = player.FeedbacksList.RemoveAll(feedback =>
                    feedback is MMF_SquashAndStretch || feedback is MMF_SquashAndStretchSpring);
                if (removed > 0)
                {
                    EditorUtility.SetDirty(player);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            return prefab != null ? prefab.GetComponent<MMF_Player>() : null;
        }

        private static CargoPolishProfile RequireProfile(string path)
        {
            CargoPolishProfile profile = AssetDatabase.LoadAssetAtPath<CargoPolishProfile>(path);
            if (profile == null) throw new InvalidOperationException($"Cargo polish profile missing: {path}");
            return profile;
        }

        private static void ApplyNeutralVisualTuning(CargoPolishProfile profile)
        {
            profile.hover.outlineAlpha = 1f;
            profile.hover.outlineGlow = 1.5f;
            ConfigureImpactSquash(profile, 0.2f, 3f, 0.035f, 0.11f, 0.4f);
        }

        private static void ApplySoftVisualTuning(CargoPolishProfile profile)
        {
            profile.hover.outlineColor = new Color(1f, 0.9f, 0.25f, 1f);
            profile.hover.outlineAlpha = 1f;
            profile.hover.outlinePixelWidth = 1;
            profile.hover.outlineGlow = 1.5f;
            profile.statusMaterial.heatRoundWaveStrength = AnimationCurve.Linear(0f, 0f, 1f, 0.05f);
            profile.statusMaterial.coldHandDrawnAmount = AnimationCurve.Linear(0f, 0f, 1f, 0.6f);
            profile.statusMaterial.lowPressureRoundWaveStrength = AnimationCurve.Linear(0f, 0f, 1f, 0f);
            profile.statusMaterial.staleHandDrawnAmount = AnimationCurve.Linear(0f, 0f, 1f, 0.2f);
            profile.impactMaterial.color = new Color(1f, 0.88f, 0.75f, 1f);
            profile.impactMaterial.duration = 0.08f;
            profile.impactMaterial.blendOverTime = CreateImpactBlendCurve(0.08f);
            ConfigureImpactSquash(profile, 0.03f, 0.5f, 0.055f, 0.15f, 0.55f);
        }

        private static void ApplyExplosiveVisualTuning(CargoPolishProfile profile)
        {
            profile.hover.outlineColor = new Color(1f, 0.45f, 0.15f, 1f);
            profile.hover.outlineAlpha = 1f;
            profile.hover.outlinePixelWidth = 2;
            profile.hover.outlineGlow = 1.5f;
            profile.statusMaterial.heatRoundWaveStrength = AnimationCurve.Linear(0f, 0f, 1f, 0.07f);
            profile.statusMaterial.coldHandDrawnAmount = AnimationCurve.Linear(0f, 0f, 1f, 0.35f);
            profile.statusMaterial.lowPressureRoundWaveStrength = AnimationCurve.Linear(0f, 0f, 1f, 0.03f);
            profile.statusMaterial.staleHandDrawnAmount = AnimationCurve.Linear(0f, 0f, 1f, 0f);
            profile.impactMaterial.color = new Color(1f, 0.45f, 0.25f, 1f);
            profile.impactMaterial.duration = 0.09f;
            profile.impactMaterial.blendOverTime = CreateImpactBlendCurve(0.1f);
            ConfigureImpactSquash(profile, 1f, 10f, 0.025f, 0.1f, 0.35f);
        }

        private static void ConfigureImpactSquash(
            CargoPolishProfile profile,
            float minimumStrength,
            float fullStrength,
            float maxScaleDelta,
            float duration,
            float horizontalCompensation)
        {
            profile.impactSquash ??= new CargoImpactSquashSettings();
            profile.impactSquash.enabled = true;
            profile.impactSquash.minimumStrength = minimumStrength;
            profile.impactSquash.fullStrength = fullStrength;
            profile.impactSquash.strengthResponse = AnimationCurve.Linear(0f, 0f, 1f, 1f);
            profile.impactSquash.maxScaleDelta = maxScaleDelta;
            profile.impactSquash.duration = duration;
            profile.impactSquash.horizontalCompensation = horizontalCompensation;
            profile.impactSquash.deformationOverTime = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.15f, 1f),
                new Keyframe(0.55f, -0.2f),
                new Keyframe(1f, 0f));
        }

        private static AnimationCurve CreateImpactBlendCurve(float peak)
        {
            return new AnimationCurve(
                new Keyframe(0f, peak),
                new Keyframe(1f, 0f));
        }

        private static void AssignProfiles(
            CargoPolishProfile neutral,
            CargoPolishProfile soft,
            CargoPolishProfile explosive)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:CargoItemData"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                CargoItemData data = AssetDatabase.LoadAssetAtPath<CargoItemData>(path);
                if (data == null || data.polishProfile != null) continue;

                string searchable = (path + " " + data.cargoName).ToLowerInvariant();
                data.polishProfile = searchable.Contains("egg")
                    ? soft
                    : searchable.Contains("nuke")
                        ? explosive
                        : neutral;
                EditorUtility.SetDirty(data);
            }
        }

        private static void MigrateSharedCargoPrefab()
        {
            string path = CargoPrototypeValidator.CargoPrefabPath;
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                CargoController controller = root.GetComponent<CargoController>();
                Transform visualRoot = root.transform.Find("VisualRoot");
                Transform vfxAnchor = visualRoot != null ? visualRoot.Find("VFXAnchor") : null;
                Transform uiAnchor = root.transform.Find("UIAnchor");
                CargoProximitySensor proximity = root.GetComponentInChildren<CargoProximitySensor>(true);
                CargoColliderBuilder colliderBuilder = root.GetComponent<CargoColliderBuilder>();
                if (controller == null || visualRoot == null || vfxAnchor == null || uiAnchor == null
                    || proximity == null || colliderBuilder == null)
                {
                    throw new InvalidOperationException("Cargo prefab hierarchy is incomplete before polish migration.");
                }

                Transform feedbackRoot = visualRoot.Find("FeedbackRoot");
                if (feedbackRoot == null)
                {
                    GameObject feedbackObject = new GameObject("FeedbackRoot");
                    feedbackRoot = feedbackObject.transform;
                    feedbackRoot.SetParent(visualRoot, false);
                }

                SpriteRenderer renderer = feedbackRoot.GetComponent<SpriteRenderer>();
                if (renderer == null)
                {
                    SpriteRenderer oldRenderer = visualRoot.GetComponent<SpriteRenderer>();
                    if (oldRenderer == null) oldRenderer = visualRoot.GetComponentInChildren<SpriteRenderer>(true);
                    if (oldRenderer == null) throw new InvalidOperationException("Cargo SpriteRenderer is missing.");

                    ComponentUtility.CopyComponent(oldRenderer);
                    ComponentUtility.PasteComponentAsNew(feedbackRoot.gameObject);
                    renderer = feedbackRoot.GetComponent<SpriteRenderer>();
                    UnityEngine.Object.DestroyImmediate(oldRenderer, true);
                }

                SpritePropertiesSync sync = feedbackRoot.GetComponent<SpritePropertiesSync>();
                if (sync == null) sync = feedbackRoot.gameObject.AddComponent<SpritePropertiesSync>();
                sync.spr = renderer;

                CargoPolishController polish = root.GetComponent<CargoPolishController>();
                if (polish == null) polish = root.AddComponent<CargoPolishController>();
                polish.ConfigureReferences(renderer, feedbackRoot, vfxAnchor);

                controller.ConfigureReferences(
                    visualRoot,
                    renderer,
                    vfxAnchor,
                    uiAnchor,
                    proximity,
                    colliderBuilder);
                colliderBuilder.ConfigureReferences(
                    renderer,
                    colliderBuilder.GeneratedColliderRoot,
                    colliderBuilder.ProximityTrigger);

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void EnsureFolder(string path)
        {
            string[] segments = path.Split('/');
            string current = segments[0];
            for (int i = 1; i < segments.Length; i++)
            {
                string next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, segments[i]);
                current = next;
            }
        }
    }
}
