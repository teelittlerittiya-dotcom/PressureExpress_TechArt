using System.Collections.Generic;
using System.Linq;
using System.Text;
using MoreMountains.Feedbacks;
using UnityEditor;
using UnityEngine;

namespace PressureExpress.EditorTools
{
    public static class CargoPolishProfileValidator
    {
        private static readonly string[] RequiredAllIn1Properties =
        {
            "_OutlineColor",
            "_OutlineAlpha",
            "_OutlineGlow",
            "_OutlinePixelWidth",
            "_RoundWaveStrength",
            "_HandDrawnAmount",
            "_HitEffectColor",
            "_HitEffectBlend"
        };

        [MenuItem("Tools/Cargo/Validate Polish Profiles")]
        public static void ValidateFromMenu()
        {
            bool valid = ValidateAll(out string report);
            if (valid) Debug.Log(report);
            else Debug.LogError(report);
        }

        public static bool ValidateAll(out string report)
        {
            List<string> errors = new List<string>();
            List<CargoPolishProfile> profiles = new List<CargoPolishProfile>();
            string[] dataGuids = AssetDatabase.FindAssets("t:CargoItemData");

            foreach (string guid in dataGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                CargoItemData data = AssetDatabase.LoadAssetAtPath<CargoItemData>(path);
                if (data == null) continue;
                if (data.polishProfile == null)
                {
                    errors.Add($"{path}: polishProfile is missing");
                    continue;
                }

                if (!profiles.Contains(data.polishProfile)) profiles.Add(data.polishProfile);
            }

            foreach (CargoPolishProfile profile in profiles)
            {
                ValidateProfile(profile, errors);
            }

            string[] cargoPrefabPaths = AssetDatabase.FindAssets("t:Prefab")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path =>
                {
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    return prefab != null && prefab.GetComponent<CargoController>() != null;
                })
                .ToArray();
            if (cargoPrefabPaths.Length != 1 || cargoPrefabPaths[0] != CargoPrototypeValidator.CargoPrefabPath)
            {
                errors.Add(
                    "Cargo presentation must use exactly one shared prefab. Found: "
                    + string.Join(", ", cargoPrefabPaths));
            }

            StringBuilder builder = new StringBuilder();
            builder.AppendLine($"Cargo polish validator: {(errors.Count == 0 ? "PASS" : "FAIL")}");
            builder.AppendLine($"Cargo data: {dataGuids.Length}, referenced profiles: {profiles.Count}");
            foreach (string error in errors) builder.AppendLine($"  ERROR: {error}");
            report = builder.ToString();
            return errors.Count == 0;
        }

        public static bool ValidateProfile(CargoPolishProfile profile, List<string> errors)
        {
            int initialErrorCount = errors.Count;
            if (profile == null)
            {
                errors.Add("CargoPolishProfile is null");
                return false;
            }

            string path = AssetDatabase.GetAssetPath(profile);
            if (!profile.ValidateProfile(out string error)) errors.Add($"{path}: {error}");

            if (profile.useAllIn1MaterialEffects && profile.spriteMaterialPreset != null)
            {
                Material material = profile.spriteMaterialPreset;
                foreach (string propertyName in RequiredAllIn1Properties)
                {
                    if (!material.HasProperty(propertyName)) errors.Add($"{path}: material is missing {propertyName}");
                }

                RequireKeyword(profile.hover.enabled, material, "OUTBASE_ON", path, errors);
                RequireKeyword(profile.hover.enabled, material, "OUTBASEPIXELPERF_ON", path, errors);
                RequireKeyword(profile.statusMaterial.enabled, material, "DOODLE_ON", path, errors);
                RequireKeyword(profile.statusMaterial.enabled, material, "ROUNDWAVEUV_ON", path, errors);
                RequireKeyword(profile.impactMaterial.enabled, material, "HITEFFECT_ON", path, errors);
            }

            ValidateCue(profile.impact, "Impact", path, errors);
            ValidateCue(profile.pickup, "Pickup", path, errors);
            ValidateCue(profile.release, "Release", path, errors);
            return errors.Count == initialErrorCount;
        }

        private static void ValidateCue(CargoPolishCue cue, string label, string profilePath, List<string> errors)
        {
            if (cue == null || !cue.enabled || cue.feelTemplate == null) return;

            GameObject templateObject = cue.feelTemplate.gameObject;
            if (!PrefabUtility.IsPartOfPrefabAsset(templateObject))
                errors.Add($"{profilePath}/{label}: FEEL template must be a prefab asset");

            if (templateObject.GetComponentInChildren<Collider>(true) != null
                || templateObject.GetComponentInChildren<Rigidbody>(true) != null
                || templateObject.GetComponentInChildren<Collider2D>(true) != null
                || templateObject.GetComponentInChildren<Rigidbody2D>(true) != null)
                errors.Add($"{profilePath}/{label}: FEEL template must be visual-only and cannot contain physics components");

            List<MMF_Feedback> feedbacks = cue.feelTemplate.FeedbacksList;
            if (feedbacks == null || feedbacks.All(feedback => feedback is not MMF_ReferenceHolder))
                errors.Add($"{profilePath}/{label}: FEEL template needs an MMF Reference Holder");

            if (feedbacks != null && feedbacks.Any(feedback => feedback is MMF_Sound))
                errors.Add($"{profilePath}/{label}: use Cargo spatialSfx instead of MMF_Sound");

            if (feedbacks != null && feedbacks.Any(feedback => feedback is MMF_MaterialSetProperty))
                errors.Add($"{profilePath}/{label}: material changes belong in CargoPolishProfile, not FEEL");

            if (feedbacks != null && feedbacks.Any(feedback =>
                    feedback is MMF_SquashAndStretch || feedback is MMF_SquashAndStretchSpring))
                errors.Add($"{profilePath}/{label}: impact squash belongs in CargoPolishProfile, not FEEL");

            if (feedbacks != null)
            {
                foreach (MMF_ParticlesInstantiation particles in feedbacks.OfType<MMF_ParticlesInstantiation>())
                {
                    bool hasMain = particles.ParticlesPrefab != null;
                    bool hasRandom = particles.RandomParticlePrefabs != null
                                     && particles.RandomParticlePrefabs.Any(prefab => prefab != null);
                    if (!hasMain && !hasRandom)
                        errors.Add($"{profilePath}/{label}: particle feedback has no ParticleSystem prefab");
                }
            }
        }

        private static void RequireKeyword(
            bool required,
            Material material,
            string keyword,
            string profilePath,
            List<string> errors)
        {
            if (required && !material.IsKeywordEnabled(keyword))
                errors.Add($"{profilePath}: material preset must enable {keyword}");
        }
    }
}
