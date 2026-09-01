using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PressureExpress.Tests.EditMode
{
    public sealed class CargoPrototypeEditModeTests
    {
        private const string CargoPrefabPath = "Assets/Prefab/Cargo/CargoController (new).prefab";
        private const string CargoDataPath = "Assets/Data/Cargo/Prototype/Cargo Prototype.asset";
        private const string CargoUiPrefabPath = "Assets/Prefab/Cargo/CargoUI/UICargoInfo.prefab";
        private const string CargoGripPath = "Assets/PhysicsMaterial/CargoGrip.physicMaterial";
        private const string NoFrictionPath = "Assets/PhysicsMaterial/NoFriction.physicMaterial";
        private const string MainLevelPath = "Assets/Scenes/MainLevel.unity";

        [Test]
        public void CargoPrefab_PassesProductionValidator()
        {
            Type validatorType = FindType("PressureExpress.EditorTools.CargoPrototypeValidator");
            MethodInfo validateMethod = validatorType.GetMethod(
                "ValidatePrefab",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(validateMethod, Is.Not.Null);

            object[] arguments = { CargoPrefabPath, null };
            bool passed = (bool)validateMethod.Invoke(null, arguments);
            Assert.That(passed, Is.True, arguments[1] as string);
        }

        [Test]
        public void CargoPolishProfiles_AreAssignedAndPassValidator()
        {
            Type validatorType = FindType("PressureExpress.EditorTools.CargoPolishProfileValidator");
            MethodInfo validateMethod = validatorType.GetMethod(
                "ValidateAll",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(validateMethod, Is.Not.Null);

            object[] arguments = { null };
            bool passed = (bool)validateMethod.Invoke(null, arguments);
            Assert.That(passed, Is.True, arguments[0] as string);
        }

        [Test]
        public void CargoImpactProfiles_IgnoreBelowThresholdAndKeepSquashSubtle()
        {
            string[] profileGuids = AssetDatabase.FindAssets("t:CargoPolishProfile");
            Assert.That(profileGuids, Has.Length.EqualTo(3));

            foreach (string guid in profileGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                UnityEngine.Object profile = AssetDatabase.LoadMainAssetAtPath(path);
                FieldInfo squashField = profile.GetType().GetField("impactSquash", BindingFlags.Public | BindingFlags.Instance);
                object squash = squashField.GetValue(profile);

                float minimumStrength = (float)squash.GetType()
                    .GetField("minimumStrength", BindingFlags.Public | BindingFlags.Instance)
                    .GetValue(squash);
                float fullStrength = (float)squash.GetType()
                    .GetField("fullStrength", BindingFlags.Public | BindingFlags.Instance)
                    .GetValue(squash);
                MethodInfo evaluate = squash.GetType().GetMethod("EvaluateIntensity", BindingFlags.Public | BindingFlags.Instance);
                Assert.That((float)evaluate.Invoke(squash, new object[] { minimumStrength - 0.01f }), Is.EqualTo(0f), path);
                Assert.That((float)evaluate.Invoke(squash, new object[] { minimumStrength }), Is.EqualTo(0f), path);
                Assert.That((float)evaluate.Invoke(squash, new object[] { fullStrength }), Is.EqualTo(1f).Within(0.0001f), path);

                float maxScaleDelta = (float)squash.GetType()
                    .GetField("maxScaleDelta", BindingFlags.Public | BindingFlags.Instance)
                    .GetValue(squash);
                Assert.That(maxScaleDelta, Is.GreaterThan(0f).And.LessThanOrEqualTo(0.055f), path);
            }
        }

        [Test]
        public void CargoData_KeepsSinglePrefabDataDrivenWorkflow()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CargoPrefabPath);
            UnityEngine.Object data = AssetDatabase.LoadMainAssetAtPath(CargoDataPath);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(data, Is.Not.Null);

            Component controller = prefab.GetComponent(FindType("CargoController"));
            Assert.That(controller, Is.Not.Null);

            SerializedObject controllerObject = new SerializedObject(controller);
            SerializedProperty configuredData = controllerObject.FindProperty("cargoItemData");
            Assert.That(configuredData, Is.Not.Null);
            Assert.That(configuredData.objectReferenceValue, Is.SameAs(data));

            SerializedObject dataObject = new SerializedObject(data);
            Assert.That(dataObject.FindProperty("definitionId").stringValue, Is.EqualTo("cargo.prototype.eggs"));
            Assert.That(dataObject.FindProperty("defaultSprite").objectReferenceValue, Is.Not.Null);
            Assert.That(dataObject.FindProperty("autoSizeColliderFromSprite").boolValue, Is.True);
            Assert.That(dataObject.FindProperty("cargoScale").floatValue, Is.GreaterThan(0f));
            Assert.That(dataObject.FindProperty("colliderDepth").floatValue, Is.GreaterThan(0f));
            Assert.That(dataObject.FindProperty("polishProfile").objectReferenceValue, Is.Not.Null);

            PhysicsMaterial physicsMaterial = dataObject.FindProperty("physicsMaterial").objectReferenceValue as PhysicsMaterial;
            Assert.That(physicsMaterial, Is.Not.Null);
            Assert.That(physicsMaterial.dynamicFriction, Is.GreaterThanOrEqualTo(0.5f));
            Assert.That(physicsMaterial.staticFriction, Is.GreaterThanOrEqualTo(0.5f));
            Assert.That(physicsMaterial.frictionCombine, Is.EqualTo(PhysicsMaterialCombine.Maximum));

            SerializedProperty modules = dataObject.FindProperty("modules");
            Assert.That(modules, Is.Not.Null);
            Assert.That(modules.arraySize, Is.EqualTo(4));
            Assert.That(
                Enumerable.Range(0, modules.arraySize)
                    .Select(index => modules.GetArrayElementAtIndex(index).objectReferenceValue)
                    .All(module => module != null),
                Is.True);
        }

        [Test]
        public void CargoProtection_DefaultsToThreeSecondsAndExposesReusableBuffApi()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CargoPrefabPath);
            Assert.That(prefab, Is.Not.Null);

            Component controller = prefab.GetComponent(FindType("CargoController"));
            Assert.That(controller, Is.Not.Null);

            SerializedProperty duration = new SerializedObject(controller)
                .FindProperty("initialInvincibilityDuration");
            Assert.That(duration, Is.Not.Null);
            Assert.That(duration.floatValue, Is.EqualTo(3f).Within(0.0001f));

            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                instance.hideFlags = HideFlags.HideAndDontSave;
                Type controllerType = controller.GetType();
                Component instanceController = instance.GetComponent(controllerType);
                MethodInfo grant = controllerType.GetMethod("GrantInvincibility", BindingFlags.Public | BindingFlags.Instance);
                MethodInfo clear = controllerType.GetMethod("ClearInvincibility", BindingFlags.Public | BindingFlags.Instance);
                PropertyInfo isInvincible = controllerType.GetProperty("IsInvincible");

                Assert.That(grant, Is.Not.Null);
                Assert.That(clear, Is.Not.Null);
                Assert.That(isInvincible, Is.Not.Null);

                grant.Invoke(instanceController, new object[] { 3f });
                Assert.That((bool)isInvincible.GetValue(instanceController), Is.True);

                clear.Invoke(instanceController, null);
                Assert.That((bool)isInvincible.GetValue(instanceController), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void CargoDebugMode_DefaultsToMainKeyboardEqualsAndOffForAllCargo()
        {
            Type debugType = FindType("CargoDebugMode");
            GameObject debugObject = new GameObject("Cargo Debug Mode Test");
            try
            {
                Component debug = debugObject.AddComponent(debugType);
                SerializedObject serialized = new SerializedObject(debug);
                SerializedProperty toggleKey = serialized.FindProperty("toggleCargoStatusKey");
                Assert.That(toggleKey.enumNames[toggleKey.enumValueIndex], Is.EqualTo(nameof(KeyCode.Equals)));
                Assert.That(serialized.FindProperty("cargoStatusUIVisible").boolValue, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(debugObject);
            }
        }

        [Test]
        public void CargoStatusUi_DebugModeGatesHoverVisibility()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CargoPrefabPath);
            Assert.That(prefab, Is.Not.Null);

            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                instance.hideFlags = HideFlags.HideAndDontSave;
                Type controllerType = FindType("CargoController");
                Component controller = instance.GetComponent(controllerType);
                MethodInfo shouldShow = controllerType.GetMethod(
                    "ShouldShowLocalStatusUI",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                MethodInfo setHover = controllerType.GetMethod(
                    "SetLocalPointerHover",
                    BindingFlags.Public | BindingFlags.Instance);
                MethodInfo setDebug = controllerType.GetMethod(
                    "SetLocalDebugStatusUIVisible",
                    BindingFlags.Public | BindingFlags.Instance);

                Assert.That(shouldShow, Is.Not.Null);
                Assert.That(setHover, Is.Not.Null);
                Assert.That(setDebug, Is.Not.Null);

                setHover.Invoke(controller, new object[] { true });
                Assert.That((bool)shouldShow.Invoke(controller, null), Is.False);

                setDebug.Invoke(controller, new object[] { true });
                Assert.That((bool)shouldShow.Invoke(controller, null), Is.True);

                setHover.Invoke(controller, new object[] { false });
                Assert.That((bool)shouldShow.Invoke(controller, null), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void CargoStatusUi_StaysUprightAtFixedWorldOffsetAndRendersAsOverlay()
        {
            GameObject uiPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CargoUiPrefabPath);
            Assert.That(uiPrefab, Is.Not.Null);

            GameObject uiInstance = UnityEngine.Object.Instantiate(uiPrefab);
            GameObject target = new GameObject("Cargo UI Target");
            try
            {
                uiInstance.hideFlags = HideFlags.HideAndDontSave;
                target.hideFlags = HideFlags.HideAndDontSave;

                Canvas canvas = uiInstance.GetComponentInChildren<Canvas>(true);
                Assert.That(canvas, Is.Not.Null);
                Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.WorldSpace));
                Assert.That(canvas.isRootCanvas || canvas.overrideSorting, Is.True);
                Assert.That(canvas.sortingLayerName, Is.EqualTo("UI"));
                Assert.That(canvas.sortingOrder, Is.EqualTo(short.MaxValue));

                Component presenter = uiInstance.GetComponent(FindType("UICargoInfo"));
                MethodInfo configure = presenter.GetType().GetMethod("ConfigureWorldPresentation", BindingFlags.Public | BindingFlags.Instance);
                MethodInfo refresh = presenter.GetType().GetMethod("RefreshWorldPresentation", BindingFlags.Public | BindingFlags.Instance);
                Assert.That(configure, Is.Not.Null);
                Assert.That(refresh, Is.Not.Null);

                Vector3 worldOffset = new Vector3(0.25f, 1.75f, 0f);
                target.transform.SetPositionAndRotation(new Vector3(3f, -2f, -3.24f), Quaternion.Euler(0f, 0f, 62f));
                configure.Invoke(presenter, new object[] { target.transform, worldOffset });

                Assert.That(Vector3.Distance(uiInstance.transform.position, target.transform.position + worldOffset), Is.LessThan(0.0001f));
                Assert.That(Quaternion.Angle(uiInstance.transform.rotation, Quaternion.identity), Is.LessThan(0.001f));

                target.transform.SetPositionAndRotation(new Vector3(-4f, 1f, -3.24f), Quaternion.Euler(0f, 0f, -117f));
                refresh.Invoke(presenter, null);

                Assert.That(Vector3.Distance(uiInstance.transform.position, target.transform.position + worldOffset), Is.LessThan(0.0001f));
                Assert.That(Quaternion.Angle(uiInstance.transform.rotation, Quaternion.identity), Is.LessThan(0.001f));

                Graphic[] graphics = uiInstance.GetComponentsInChildren<Graphic>(true);
                Assert.That(graphics, Is.Not.Empty);
                foreach (Graphic graphic in graphics)
                {
                    Material material = graphic.materialForRendering;
                    Assert.That(material, Is.Not.Null);
                    Assert.That(material.renderQueue, Is.EqualTo((int)RenderQueue.Overlay));
                    string expectedShader = graphic.GetType().FullName?.StartsWith("TMPro.", StringComparison.Ordinal) == true
                        ? "TextMeshPro/Distance Field Overlay"
                        : "PressureExpress/UI/Cargo Overlay";
                    Assert.That(material.shader.name, Is.EqualTo(expectedShader));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(uiInstance);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void CargoGrip_StopsHorizontalSlidingOnNoFrictionShipFloor()
        {
            PhysicsMaterial cargoGrip = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(CargoGripPath);
            PhysicsMaterial noFriction = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(NoFrictionPath);
            Assert.That(cargoGrip, Is.Not.Null);
            Assert.That(noFriction, Is.Not.Null);

            Scene physicsSceneContainer = EditorSceneManager.NewPreviewScene();
            PhysicsScene physicsScene = physicsSceneContainer.GetPhysicsScene();

            try
            {
                GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.name = "NoFriction Ship Floor";
                floor.transform.SetPositionAndRotation(new Vector3(0f, -0.5f, 0f), Quaternion.identity);
                floor.transform.localScale = new Vector3(20f, 1f, 2f);
                floor.GetComponent<Collider>().sharedMaterial = noFriction;
                SceneManager.MoveGameObjectToScene(floor, physicsSceneContainer);

                GameObject cargo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cargo.name = "Cargo Grip Probe";
                cargo.transform.SetPositionAndRotation(new Vector3(0f, 0.51f, 0f), Quaternion.identity);
                cargo.GetComponent<Collider>().sharedMaterial = cargoGrip;
                Rigidbody body = cargo.AddComponent<Rigidbody>();
                body.mass = 1f;
                body.linearDamping = 0.05f;
                body.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
                SceneManager.MoveGameObjectToScene(cargo, physicsSceneContainer);

                Physics.SyncTransforms();
                for (int step = 0; step < 30; step++) physicsScene.Simulate(0.02f);

                float slideStartX = cargo.transform.position.x;
                body.linearVelocity = new Vector3(1.5f, 0f, 0f);
                body.WakeUp();
                for (int step = 0; step < 50; step++) physicsScene.Simulate(0.02f);

                Assert.That(Mathf.Abs(body.linearVelocity.x), Is.LessThan(0.1f));
                Assert.That(Mathf.Abs(cargo.transform.position.x - slideStartX), Is.LessThan(0.5f));
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(physicsSceneContainer);
            }
        }

        [Test]
        public void CargoModules_ClampRangesAndExposeUniqueStableIds()
        {
            UnityEngine.Object data = AssetDatabase.LoadMainAssetAtPath(CargoDataPath);
            SerializedProperty modules = new SerializedObject(data).FindProperty("modules");
            HashSet<byte> ids = new HashSet<byte>();
            UnityEngine.Object temperatureModule = null;

            for (int index = 0; index < modules.arraySize; index++)
            {
                UnityEngine.Object module = modules.GetArrayElementAtIndex(index).objectReferenceValue;
                PropertyInfo idProperty = module.GetType().GetProperty("ModuleId", BindingFlags.Public | BindingFlags.Instance);
                MethodInfo clampMethod = module.GetType().GetMethod("ClampValue", BindingFlags.Public | BindingFlags.Instance);
                MethodInfo minMethod = module.GetType().GetMethod("GetMinValue", BindingFlags.Public | BindingFlags.Instance);
                MethodInfo maxMethod = module.GetType().GetMethod("GetMaxValue", BindingFlags.Public | BindingFlags.Instance);

                Assert.That(idProperty, Is.Not.Null);
                Assert.That(clampMethod, Is.Not.Null);
                Assert.That(minMethod, Is.Not.Null);
                Assert.That(maxMethod, Is.Not.Null);

                byte id = Convert.ToByte(idProperty.GetValue(module));
                Assert.That(ids.Add(id), Is.True, $"Duplicate module id {id}");

                float min = Convert.ToSingle(minMethod.Invoke(module, null));
                float max = Convert.ToSingle(maxMethod.Invoke(module, null));
                Assert.That(Convert.ToSingle(clampMethod.Invoke(module, new object[] { float.NegativeInfinity })), Is.EqualTo(min));
                Assert.That(Convert.ToSingle(clampMethod.Invoke(module, new object[] { float.PositiveInfinity })), Is.EqualTo(max));

                if (module.GetType().Name == "TemperatureModule") temperatureModule = module;
            }

            Assert.That(ids.Count, Is.EqualTo(4));
            Assert.That(temperatureModule, Is.Not.Null);
            Assert.That(
                Convert.ToSingle(temperatureModule.GetType().GetMethod("GetMinValue").Invoke(temperatureModule, null)),
                Is.LessThan(0f));
            Assert.That(
                temperatureModule.GetType().GetMethod("GetStateName").DeclaringType.Name,
                Is.EqualTo("TemperatureModule"));
        }

        [Test]
        public void CargoStageVisuals_UseImpactSpriteAndStatusParticleChannels()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CargoPrefabPath);
            Assert.That(prefab, Is.Not.Null);

            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                instance.hideFlags = HideFlags.HideAndDontSave;
                Type controllerType = FindType("CargoController");
                Component controller = instance.GetComponent(controllerType);
                Assert.That(controller, Is.Not.Null);

                MethodInfo validateConfiguration = controllerType.GetMethod(
                    "ValidateConfiguration",
                    BindingFlags.Public | BindingFlags.Instance);
                Assert.That(validateConfiguration, Is.Not.Null);
                object[] validationArguments = { null };
                bool configurationValid = (bool)validateConfiguration.Invoke(controller, validationArguments);
                Assert.That(configurationValid, Is.True, validationArguments[0] as string);

                Type polishType = FindType("CargoPolishController");
                Component polish = instance.GetComponent(polishType);
                Assert.That(polish, Is.Not.Null);
                MethodInfo applyVisuals = polishType.GetMethod(
                    "ApplyState",
                    BindingFlags.Instance | BindingFlags.Public);
                Assert.That(applyVisuals, Is.Not.Null);
                MethodInfo initializePresentation = polishType.GetMethod(
                    "InitializePresentation",
                    BindingFlags.Instance | BindingFlags.Public);
                Assert.That(initializePresentation, Is.Not.Null);
                initializePresentation.Invoke(polish, null);

                Type runtimeStateType = FindType("CargoRuntimeState");
                Type moduleMaskType = FindType("CargoModuleMask");
                object degraded = Activator.CreateInstance(runtimeStateType);
                runtimeStateType.GetField("Initialized").SetValue(degraded, true);
                runtimeStateType.GetField("ModuleMask").SetValue(
                    degraded,
                    Enum.Parse(moduleMaskType, "Impact, Temperature, Freshness, Pressure"));
                runtimeStateType.GetField("Impact").SetValue(degraded, 50f);
                runtimeStateType.GetField("Temperature").SetValue(degraded, 50f);
                runtimeStateType.GetField("Freshness").SetValue(degraded, 20f);
                runtimeStateType.GetField("Pressure").SetValue(degraded, 0f);
                applyVisuals.Invoke(polish, new object[] { degraded });

                PropertyInfo spriteRendererProperty = controllerType.GetProperty("SpriteRenderer");
                Assert.That(spriteRendererProperty, Is.Not.Null);
                SpriteRenderer renderer = (SpriteRenderer)spriteRendererProperty.GetValue(controller);
                Assert.That(renderer, Is.Not.Null);
                Assert.That(renderer.sprite, Is.Not.Null);
                Assert.That(renderer.sprite.name, Is.EqualTo("Many Opened Eggs_0"));

                Transform anchor = instance.transform.Find("VisualRoot/VFXAnchor");
                Assert.That(anchor, Is.Not.Null);
                string[] activeStageParticles = anchor.Cast<Transform>()
                    .Where(child => child.gameObject.activeInHierarchy)
                    .Select(child => child.name)
                    .ToArray();
                Assert.That(activeStageParticles, Does.Contain("Particle-HeatTemp50(Clone)"));
                Assert.That(activeStageParticles, Does.Contain("Particle-Rotten100(Clone)"));

                MaterialPropertyBlock statusBlock = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(statusBlock);
                int roundWave = Shader.PropertyToID("_RoundWaveStrength");
                int handDrawn = Shader.PropertyToID("_HandDrawnAmount");
                float roundWaveBeforeHover = statusBlock.GetFloat(roundWave);
                float handDrawnBeforeHover = statusBlock.GetFloat(handDrawn);
                Assert.That(roundWaveBeforeHover, Is.GreaterThan(0f));
                Assert.That(handDrawnBeforeHover, Is.GreaterThan(0f));

                controllerType.GetMethod("SetLocalPointerHover", BindingFlags.Public | BindingFlags.Instance)
                    .Invoke(controller, new object[] { true });
                renderer.GetPropertyBlock(statusBlock);
                Assert.That(statusBlock.GetFloat(roundWave), Is.EqualTo(roundWaveBeforeHover).Within(0.0001f));
                Assert.That(statusBlock.GetFloat(handDrawn), Is.EqualTo(handDrawnBeforeHover).Within(0.0001f));

                object normal = degraded;
                runtimeStateType.GetField("Impact").SetValue(normal, 100f);
                runtimeStateType.GetField("Temperature").SetValue(normal, 0f);
                runtimeStateType.GetField("Freshness").SetValue(normal, 100f);
                applyVisuals.Invoke(polish, new object[] { normal });

                Assert.That(renderer.sprite.name, Is.EqualTo("Eggs_0"));
                Assert.That(
                    anchor.Cast<Transform>().Count(child => child.gameObject.activeInHierarchy),
                    Is.EqualTo(0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void CargoHover_UsesPerRendererMaterialProperties()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CargoPrefabPath);
            GameObject first = UnityEngine.Object.Instantiate(prefab);
            GameObject second = UnityEngine.Object.Instantiate(prefab);
            try
            {
                first.hideFlags = HideFlags.HideAndDontSave;
                second.hideFlags = HideFlags.HideAndDontSave;

                Type controllerType = FindType("CargoController");
                Type polishType = FindType("CargoPolishController");
                Component firstController = first.GetComponent(controllerType);
                Component firstPolish = first.GetComponent(polishType);
                Component secondPolish = second.GetComponent(polishType);
                MethodInfo initialize = polishType.GetMethod("InitializePresentation", BindingFlags.Public | BindingFlags.Instance);
                MethodInfo setHover = controllerType.GetMethod("SetLocalPointerHover", BindingFlags.Public | BindingFlags.Instance);
                initialize.Invoke(firstPolish, null);
                initialize.Invoke(secondPolish, null);

                SpriteRenderer firstRenderer = first.transform
                    .Find("VisualRoot/FeedbackRoot")
                    .GetComponent<SpriteRenderer>();
                SpriteRenderer secondRenderer = second.transform
                    .Find("VisualRoot/FeedbackRoot")
                    .GetComponent<SpriteRenderer>();
                Assert.That(firstRenderer.sharedMaterial, Is.SameAs(secondRenderer.sharedMaterial));

                setHover.Invoke(firstController, new object[] { true });
                MaterialPropertyBlock firstBlock = new MaterialPropertyBlock();
                MaterialPropertyBlock secondBlock = new MaterialPropertyBlock();
                firstRenderer.GetPropertyBlock(firstBlock);
                secondRenderer.GetPropertyBlock(secondBlock);

                int outlineAlpha = Shader.PropertyToID("_OutlineAlpha");
                Assert.That(firstBlock.GetFloat(outlineAlpha), Is.EqualTo(1f).Within(0.0001f));
                Assert.That(secondBlock.GetFloat(outlineAlpha), Is.EqualTo(0f).Within(0.0001f));

                Transform feedbackRoot = first.transform.Find("VisualRoot/FeedbackRoot");
                Vector3 authoredScale = feedbackRoot.localScale;
                feedbackRoot.localScale = new Vector3(0.75f, 1.2f, 1f);
                polishType.GetMethod("OnDisable", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(firstPolish, null);
                Assert.That(feedbackRoot.localScale, Is.EqualTo(authoredScale));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(second);
            }
        }

        [Test]
        public void CargoImpactSquash_IsSubtleRestoresExactlyAndDoesNotTouchColliders()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CargoPrefabPath);
            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                instance.hideFlags = HideFlags.HideAndDontSave;
                Type controllerType = FindType("CargoController");
                Type polishType = FindType("CargoPolishController");
                Component controller = instance.GetComponent(controllerType);
                Component polish = instance.GetComponent(polishType);
                Component builder = instance.GetComponent(FindType("CargoColliderBuilder"));
                UnityEngine.Object data = new SerializedObject(controller)
                    .FindProperty("cargoItemData")
                    .objectReferenceValue;

                polishType.GetMethod("InitializePresentation", BindingFlags.Public | BindingFlags.Instance)
                    .Invoke(polish, null);
                MethodInfo rebuild = builder.GetType().GetMethod("Rebuild", BindingFlags.Public | BindingFlags.Instance);
                Assert.That((bool)rebuild.Invoke(builder, new object[] { data }), Is.True);

                Transform feedbackRoot = instance.transform.Find("VisualRoot/FeedbackRoot");
                Transform generatedRoot = instance.transform.Find("GeneratedColliders");
                Collider[] colliders = generatedRoot.GetComponentsInChildren<Collider>(true);
                Assert.That(colliders, Is.Not.Empty);
                Physics.SyncTransforms();

                Vector3 authoredFeedbackScale = feedbackRoot.localScale;
                Vector3 rootScale = instance.transform.localScale;
                Vector3 generatedScale = generatedRoot.localScale;
                Bounds[] colliderBounds = colliders.Select(collider => collider.bounds).ToArray();

                MethodInfo applySquash = polishType.GetMethod(
                    "ApplyImpactSquashDeformation",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                MethodInfo restoreScale = polishType.GetMethod(
                    "RestoreFeedbackScale",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.That(applySquash, Is.Not.Null);
                Assert.That(restoreScale, Is.Not.Null);

                applySquash.Invoke(polish, new object[] { 0.025f, 0.45f });
                Physics.SyncTransforms();

                Assert.That(feedbackRoot.localScale, Is.Not.EqualTo(authoredFeedbackScale));
                Vector3 firstSquashScale = feedbackRoot.localScale;
                applySquash.Invoke(polish, new object[] { 0.025f, 0.45f });
                Assert.That(feedbackRoot.localScale, Is.EqualTo(firstSquashScale), "Repeated impact squash must not accumulate scale.");
                Assert.That(
                    Mathf.Abs(feedbackRoot.localScale.y / authoredFeedbackScale.y - 1f),
                    Is.LessThanOrEqualTo(0.0251f));
                Assert.That(instance.transform.localScale, Is.EqualTo(rootScale));
                Assert.That(generatedRoot.localScale, Is.EqualTo(generatedScale));
                for (int i = 0; i < colliders.Length; i++)
                {
                    Assert.That(colliders[i].bounds.center, Is.EqualTo(colliderBounds[i].center));
                    Assert.That(colliders[i].bounds.size, Is.EqualTo(colliderBounds[i].size));
                }

                restoreScale.Invoke(polish, null);
                Assert.That(feedbackRoot.localScale, Is.EqualTo(authoredFeedbackScale));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void CompoundColliderHit_ResolvesCargoNetworkRoot()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CargoPrefabPath);
            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                instance.hideFlags = HideFlags.HideAndDontSave;
                Component controller = instance.GetComponent(FindType("CargoController"));
                Component builder = instance.GetComponent(FindType("CargoColliderBuilder"));
                UnityEngine.Object data = new SerializedObject(controller)
                    .FindProperty("cargoItemData")
                    .objectReferenceValue;

                Transform visualRoot = instance.transform.Find("VisualRoot");
                SpriteRenderer renderer = visualRoot.GetComponentInChildren<SpriteRenderer>(true);
                renderer.sprite = (Sprite)new SerializedObject(data)
                    .FindProperty("defaultSprite")
                    .objectReferenceValue;

                MethodInfo rebuild = builder.GetType().GetMethod("Rebuild", BindingFlags.Public | BindingFlags.Instance);
                Assert.That((bool)rebuild.Invoke(builder, new object[] { data }), Is.True);

                MeshCollider generatedCollider = instance.GetComponentInChildren<MeshCollider>(true);
                Assert.That(generatedCollider, Is.Not.Null);
                Assert.That(generatedCollider.gameObject, Is.Not.SameAs(instance));

                Type grabControllerType = FindType("CargoGrabController");
                MethodInfo resolve = grabControllerType.GetMethod(
                    "ResolveGrabbableNetworkObject",
                    BindingFlags.NonPublic | BindingFlags.Static);
                Assert.That(resolve, Is.Not.Null);

                Component networkObject = resolve.Invoke(null, new object[] { generatedCollider }) as Component;
                Assert.That(networkObject, Is.Not.Null);
                Assert.That(networkObject.gameObject, Is.SameAs(instance));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void MainLevel_HasNetworkCargoOnSupportedShipFloor()
        {
            Scene scene = SceneManager.GetSceneByPath(MainLevelPath);
            bool openedByTest = !scene.IsValid() || !scene.isLoaded;
            if (openedByTest) scene = EditorSceneManager.OpenScene(MainLevelPath, OpenSceneMode.Additive);

            try
            {
                Type cargoType = FindType("CargoController");
                Type networkObjectType = FindType("Unity.Netcode.NetworkObject");
                Transform cargo = EnumerateSceneTransforms(scene)
                    .FirstOrDefault(transform => transform.GetComponent(cargoType) != null);

                Assert.That(cargo, Is.Not.Null);
                Assert.That(cargo.name, Is.EqualTo("[CARGO PROTOTYPE] Status Eggs"));
                Assert.That(cargo.GetComponent(networkObjectType), Is.Not.Null);
                Assert.That(cargo.localScale, Is.EqualTo(Vector3.one));
                Assert.That(cargo.position.y, Is.EqualTo(-1.4f).Within(0.001f));
                Assert.That(cargo.position.z, Is.EqualTo(-3.24f).Within(0.001f));

                GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(cargo.gameObject);
                Assert.That(source, Is.Not.Null);
                Assert.That(AssetDatabase.GetAssetPath(source), Is.EqualTo(CargoPrefabPath));

                Transform ship = EnumerateSceneTransforms(scene).FirstOrDefault(transform => transform.name == "MainShip - 3D");
                Assert.That(ship, Is.Not.Null);
                foreach (string groupName in new[] { "Floor", "Wall", "BG" })
                {
                    Transform group = ship.Cast<Transform>().FirstOrDefault(child => child.name == groupName);
                    Assert.That(group, Is.Not.Null, $"Missing MainShip/{groupName}");
                    Rigidbody body = group.GetComponent<Rigidbody>();
                    Assert.That(body, Is.Not.Null, $"MainShip/{groupName} needs an isolated 3D body");
                    Assert.That(body.isKinematic, Is.True, $"MainShip/{groupName} must be kinematic");
                }
            }
            finally
            {
                if (openedByTest) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static Type FindType(string fullName)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(candidate => candidate != null);
            Assert.That(type, Is.Not.Null, $"Unable to resolve type {fullName}");
            return type;
        }

        private static IEnumerable<Transform> EnumerateSceneTransforms(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                {
                    yield return transform;
                }
            }
        }
    }
}
