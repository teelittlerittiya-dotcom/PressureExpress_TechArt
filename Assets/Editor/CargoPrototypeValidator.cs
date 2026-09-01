using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;

namespace PressureExpress.EditorTools
{
    /// <summary>
    /// EditMode contract validator for the generic 2.5D Cargo prefab.
    /// It is intentionally callable from Unity MCP/eval as well as the Editor menu.
    /// </summary>
    public static class CargoPrototypeValidator
    {
        public const string CargoPrefabPath = "Assets/Prefab/Cargo/CargoController (new).prefab";
        public const string NetworkPrefabListPath = "Assets/DefaultNetworkPrefabs.asset";

        [MenuItem("Tools/Cargo/Validate 2.5D Cargo Prototype")]
        public static void ValidateFromMenu()
        {
            bool valid = ValidatePrefab(CargoPrefabPath, out string report);
            if (valid) Debug.Log(report);
            else Debug.LogError(report);
        }

        public static bool ValidatePrefab(string prefabPath, out string report)
        {
            List<string> errors = new List<string>();
            List<string> notes = new List<string>();
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                report = $"Cargo validator: prefab not found at {prefabPath}";
                return false;
            }

            ValidateRoot(prefab, errors, notes);
            ValidateHierarchy(prefab, errors, notes);
            ValidateCargoStatusUi(prefab, errors, notes);
            ValidateLayers(prefab, errors, notes);
            ValidateNetworking(prefab, errors, notes);
            ValidateRuntimeColliderBuild(prefab, errors, notes);
            ValidateNetworkRegistration(prefab, errors, notes);

            StringBuilder builder = new StringBuilder();
            builder.AppendLine($"Cargo validator: {(errors.Count == 0 ? "PASS" : "FAIL")}");
            builder.AppendLine($"Prefab: {prefabPath}");
            foreach (string note in notes) builder.AppendLine($"  OK: {note}");
            foreach (string error in errors) builder.AppendLine($"  ERROR: {error}");
            report = builder.ToString();
            return errors.Count == 0;
        }

        private static void ValidateRoot(GameObject prefab, List<string> errors, List<string> notes)
        {
            if ((prefab.transform.localScale - Vector3.one).sqrMagnitude > 0.000001f)
                errors.Add("physics/network root scale is not Vector3.one");
            else
                notes.Add("root scale is Vector3.one");

            if (prefab.GetComponentsInChildren<Rigidbody2D>(true).Length > 0
                || prefab.GetComponentsInChildren<Collider2D>(true).Length > 0
                || prefab.GetComponentsInChildren<Joint2D>(true).Length > 0)
                errors.Add("2D physics component remains in the Cargo prefab hierarchy");
            else
                notes.Add("no Rigidbody2D/Collider2D/Joint2D remains");

            Rigidbody body = prefab.GetComponent<Rigidbody>();
            if (body == null)
            {
                errors.Add("root Rigidbody is missing");
            }
            else
            {
                RigidbodyConstraints required = RigidbodyConstraints.FreezePositionZ
                    | RigidbodyConstraints.FreezeRotationX
                    | RigidbodyConstraints.FreezeRotationY;
                if ((body.constraints & required) != required) errors.Add("Rigidbody 2.5D constraints are incomplete");
                if ((body.constraints & RigidbodyConstraints.FreezeRotationZ) != 0) errors.Add("Rotation Z is incorrectly frozen");
                if (!body.useGravity || body.interpolation != RigidbodyInterpolation.Interpolate)
                    errors.Add("Rigidbody gravity/interpolation settings are invalid");
                if (body.collisionDetectionMode != CollisionDetectionMode.ContinuousDynamic)
                    errors.Add("Rigidbody collision detection must be Continuous Dynamic");
                notes.Add("3D Rigidbody exists");
            }

            CargoController controller = prefab.GetComponent<CargoController>();
            if (controller == null) errors.Add("CargoController is missing");
            else if (!controller.ValidateConfiguration(out string error)) errors.Add($"CargoController configuration: {error}");
            else notes.Add("CargoController data/references are valid");
        }

        private static void ValidateHierarchy(GameObject prefab, List<string> errors, List<string> notes)
        {
            Transform visualRoot = prefab.transform.Find("VisualRoot");
            Transform uiAnchor = prefab.transform.Find("UIAnchor");
            Transform proximity = prefab.transform.Find("ProximityTrigger");
            Transform generated = prefab.transform.Find("GeneratedColliders");

            if (visualRoot == null) errors.Add("VisualRoot is missing");
            if (uiAnchor == null) errors.Add("UIAnchor is missing");
            if (proximity == null) errors.Add("ProximityTrigger is missing");
            if (generated == null) errors.Add("GeneratedColliders root is missing");

            if (visualRoot != null)
            {
                if (visualRoot.GetComponentInChildren<SpriteRenderer>(true) == null) errors.Add("SpriteRenderer is not below VisualRoot");
                if (visualRoot.Find("VFXAnchor") == null) errors.Add("VFXAnchor is missing below VisualRoot");
                Transform feedbackRoot = visualRoot.Find("FeedbackRoot");
                if (feedbackRoot == null)
                {
                    errors.Add("FeedbackRoot is missing below VisualRoot");
                }
                else if (feedbackRoot.GetComponentInChildren<Collider>(true) != null
                         || feedbackRoot.GetComponentInChildren<Rigidbody>(true) != null
                         || feedbackRoot.GetComponentInChildren<Collider2D>(true) != null
                         || feedbackRoot.GetComponentInChildren<Rigidbody2D>(true) != null)
                {
                    errors.Add("FeedbackRoot must remain visual-only and cannot contain physics components");
                }
            }

            CargoPolishController polishController = prefab.GetComponent<CargoPolishController>();
            if (polishController == null) errors.Add("CargoPolishController is missing on the root");
            else if (!polishController.ValidateConfiguration(out string polishError))
                errors.Add($"CargoPolishController configuration: {polishError}");

            if (proximity != null)
            {
                BoxCollider trigger = proximity.GetComponent<BoxCollider>();
                if (trigger == null || !trigger.isTrigger) errors.Add("ProximityTrigger must have an isTrigger BoxCollider");
                if (proximity.GetComponent<CargoProximitySensor>() == null) errors.Add("CargoProximitySensor is missing");
            }

            if (errors.Count == 0) notes.Add("required visual/collider/UI hierarchy exists");
        }

        private static void ValidateCargoStatusUi(GameObject prefab, List<string> errors, List<string> notes)
        {
            CargoController controller = prefab.GetComponent<CargoController>();
            if (controller == null) return;

            SerializedProperty uiPrefabProperty = new SerializedObject(controller).FindProperty("uiCargoInfoPrefab");
            UICargoInfo uiPrefab = uiPrefabProperty?.objectReferenceValue as UICargoInfo;
            if (uiPrefab == null)
            {
                errors.Add("Cargo status UI prefab reference is missing");
                return;
            }

            Canvas canvas = uiPrefab.GetComponentInChildren<Canvas>(true);
            if (canvas == null)
            {
                errors.Add("Cargo status UI prefab has no Canvas");
                return;
            }

            if (canvas.renderMode != RenderMode.WorldSpace)
                errors.Add("Cargo status UI Canvas must remain World Space");
            if ((!canvas.isRootCanvas && !canvas.overrideSorting)
                || canvas.sortingLayerName != "UI"
                || canvas.sortingOrder != short.MaxValue)
                errors.Add("Cargo status UI Canvas must use root/override sorting on UI at maximum order");
            else
                notes.Add("Cargo status UI uses maximum overlay sorting");

            SerializedObject serializedUi = new SerializedObject(uiPrefab);
            Shader graphicShader = serializedUi.FindProperty("graphicOverlayShader")?.objectReferenceValue as Shader;
            Shader textShader = serializedUi.FindProperty("textOverlayShader")?.objectReferenceValue as Shader;
            if (graphicShader == null || graphicShader.name != "PressureExpress/UI/Cargo Overlay")
                errors.Add("Cargo status UI graphic overlay shader is missing or invalid");
            if (textShader == null || textShader.name != "TextMeshPro/Distance Field Overlay")
                errors.Add("Cargo status UI TextMeshPro overlay shader is missing or invalid");
            if (graphicShader != null && textShader != null)
                notes.Add("Cargo status graphics and text use depth-independent overlay shaders");
        }

        private static void ValidateLayers(GameObject prefab, List<string> errors, List<string> notes)
        {
            int cargoLayer = prefab.layer;
            if (LayerMask.LayerToName(cargoLayer) != "Object")
                errors.Add("Cargo root must use the Object layer");

            string[] requiredCollisionLayers =
            {
                "Default",
                "Ground",
                "GroundForObject",
                "Ladder",
                "Object",
                "Door"
            };

            foreach (string layerName in requiredCollisionLayers)
            {
                int layer = LayerMask.NameToLayer(layerName);
                if (layer < 0)
                {
                    errors.Add($"required physics layer is missing: {layerName}");
                    continue;
                }

                if (Physics.GetIgnoreLayerCollision(cargoLayer, layer))
                    errors.Add($"Object layer unexpectedly ignores {layerName}");
            }

            int playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer >= 0 && !Physics.GetIgnoreLayerCollision(cargoLayer, playerLayer))
                errors.Add("Object/Player collision should remain ignored; Cargo uses the hand layer for interaction");

            if (errors.Count == 0)
                notes.Add("Cargo/floor/wall/door/hand collision matrix is valid; Player body ignore is intentional");
        }

        private static void ValidateNetworking(GameObject prefab, List<string> errors, List<string> notes)
        {
            if (prefab.GetComponent<NetworkObject>() == null) errors.Add("NetworkObject is missing");

            NetworkTransform networkTransform = prefab.GetComponent<NetworkTransform>();
            if (networkTransform == null)
            {
                errors.Add("server-authoritative NetworkTransform is missing");
            }
            else
            {
                if (networkTransform.GetType() != typeof(NetworkTransform)) errors.Add("Cargo uses a derived/client-authoritative NetworkTransform");
                if (networkTransform.AuthorityMode != NetworkTransform.AuthorityModes.Server) errors.Add("NetworkTransform is not server authoritative");
                if (!networkTransform.SyncPositionX || !networkTransform.SyncPositionY || networkTransform.SyncPositionZ)
                    errors.Add("NetworkTransform must sync Position X/Y only");
                if (networkTransform.SyncRotAngleX || networkTransform.SyncRotAngleY || !networkTransform.SyncRotAngleZ)
                    errors.Add("NetworkTransform must sync Rotation Z only");
                if (networkTransform.SyncScaleX || networkTransform.SyncScaleY || networkTransform.SyncScaleZ)
                    errors.Add("NetworkTransform must not sync root scale");
            }

            NetworkRigidbody networkRigidbody = prefab.GetComponent<NetworkRigidbody>();
            if (networkRigidbody == null) errors.Add("3D NetworkRigidbody is missing");
            else if (!networkRigidbody.AutoUpdateKinematicState) errors.Add("NetworkRigidbody must manage authority kinematic state");

            if (prefab.GetComponent<NetworkRigidbody2D>() != null) errors.Add("NetworkRigidbody2D remains on Cargo");
            if (errors.Count == 0) notes.Add("server-authoritative network components and axes are valid");
        }

        private static void ValidateRuntimeColliderBuild(GameObject prefab, List<string> errors, List<string> notes)
        {
            GameObject instance = null;
            try
            {
                instance = Object.Instantiate(prefab);
                instance.name = "CargoValidatorInstance";
                instance.hideFlags = HideFlags.HideAndDontSave;
                CargoController controller = instance.GetComponent<CargoController>();
                CargoColliderBuilder colliderBuilder = instance.GetComponent<CargoColliderBuilder>();

                if (controller == null || colliderBuilder == null || controller.cargoItemData == null)
                {
                    errors.Add("cannot execute collider build because controller/builder/data is missing");
                    return;
                }

                PhysicsMaterial cargoMaterial = controller.cargoItemData.physicsMaterial;
                if (cargoMaterial == null)
                {
                    errors.Add("CargoItemData PhysicsMaterial is missing");
                }
                else if (cargoMaterial.dynamicFriction < 0.5f
                         || cargoMaterial.staticFriction < 0.5f
                         || cargoMaterial.frictionCombine != PhysicsMaterialCombine.Maximum)
                {
                    errors.Add("Cargo PhysicsMaterial needs ground grip and Maximum friction combine");
                }
                else
                {
                    notes.Add($"Cargo ground grip is explicit ({cargoMaterial.name})");
                }

                SpriteRenderer renderer = instance.transform.Find("VisualRoot")?.GetComponentInChildren<SpriteRenderer>(true);
                if (renderer != null) renderer.sprite = controller.cargoItemData.defaultSprite;
                if (!colliderBuilder.Rebuild(controller.cargoItemData))
                {
                    errors.Add("Sprite Physics Shape could not generate a 3D collider");
                    return;
                }

                Collider[] solids = instance.transform.Find("GeneratedColliders")?.GetComponentsInChildren<Collider>(true);
                if (solids == null || solids.Length == 0 || solids.Any(collider => collider.isTrigger || collider.bounds.size.z <= 0f))
                    errors.Add("generated solid colliders are missing, triggers, or have zero depth");
                else if (solids.Any(collider => collider.sharedMaterial != controller.cargoItemData.physicsMaterial))
                    errors.Add("generated solid collider PhysicsMaterial does not match CargoItemData");
                else
                    notes.Add($"Sprite Physics Shape generates {solids.Length} convex 3D collider prism(s)");
            }
            finally
            {
                if (instance != null) Object.DestroyImmediate(instance);
            }
        }

        private static void ValidateNetworkRegistration(GameObject prefab, List<string> errors, List<string> notes)
        {
            Object listAsset = AssetDatabase.LoadMainAssetAtPath(NetworkPrefabListPath);
            if (listAsset == null)
            {
                errors.Add($"Network prefab list missing at {NetworkPrefabListPath}");
                return;
            }

            SerializedObject serializedList = new SerializedObject(listAsset);
            SerializedProperty list = serializedList.FindProperty("List");
            bool found = false;
            if (list != null && list.isArray)
            {
                for (int i = 0; i < list.arraySize; i++)
                {
                    SerializedProperty entry = list.GetArrayElementAtIndex(i);
                    SerializedProperty prefabProperty = entry.FindPropertyRelative("Prefab");
                    if (prefabProperty != null && prefabProperty.objectReferenceValue == prefab)
                    {
                        found = true;
                        break;
                    }
                }
            }

            if (!found) errors.Add("Cargo prefab is not registered in DefaultNetworkPrefabs");
            else notes.Add("Cargo prefab is registered in DefaultNetworkPrefabs");
        }
    }
}
