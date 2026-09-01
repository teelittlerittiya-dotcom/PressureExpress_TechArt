using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using Febucci.TextAnimatorForUnity;
using Febucci.TextAnimatorForUnity.TextMeshPro;
using PressureExpress.Framework;
using PressureExpress.Tutorial;
using PressureExpress.Tutorial.UI;
using UnityEditor.SceneManagement;

namespace PressureExpress.EditorScripts
{
    public static class TutorialSceneSetup
    {
        [MenuItem("PressureExpress/Setup Tutorial Scene")]
        public static void SetupTutorialScene()
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.name != "Tutorial")
            {
                Debug.LogWarning("[TutorialSceneSetup] Loading Tutorial scene...");
                EditorSceneManager.OpenScene("Assets/Scenes/Development/Tutorial.unity");
            }

            Debug.Log("[TutorialSceneSetup] Starting Enhanced Tutorial Scene Setup...");

            var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Font-New/Jersey25-Regular.asset");

            // 1. Setup Exit Beacon Prefab
            GameObject exitPrefab = SetupExitBeaconPrefab();

            // Ensure Manager GameObjects are active so CanvasManager works
            activeScene = EditorSceneManager.GetActiveScene();
            foreach (var root in activeScene.GetRootGameObjects())
            {
                if (root.name == "[MANAGER] NavigationGameManager")
                {
                    root.SetActive(true);
                    var ngm = root.GetComponent<NavigationGameManager>();
                    if (ngm != null) ngm.enabled = false;
                }
                else if (root.name == "[MANAGER] MapGen")
                {
                    root.SetActive(true);
                    var mg = root.GetComponent<MapGenerate>();
                    if (mg != null) mg.enabled = false;
                    var mts = root.GetComponent<MapTestScript>();
                    if (mts != null) mts.enabled = false;
                }
                else if (root.name == "MapTileGen")
                {
                    root.SetActive(true);
                }
            }

            // 2. Setup [MANAGER] TutorialManager
            GameObject tutorialManagerObj = GameObject.Find("[MANAGER] TutorialManager");
            if (tutorialManagerObj == null)
            {
                tutorialManagerObj = new GameObject("[MANAGER] TutorialManager");
                Undo.RegisterCreatedObjectUndo(tutorialManagerObj, "Create [MANAGER] TutorialManager");
            }

            var tutorialManager = tutorialManagerObj.GetComponent<TutorialManager>();
            if (tutorialManager == null) tutorialManager = tutorialManagerObj.AddComponent<TutorialManager>();

            // Obstacle & Exit Spawn Points
            Transform obstacleSpawn = tutorialManagerObj.transform.Find("ObstacleSpawnPoint");
            if (obstacleSpawn == null)
            {
                GameObject sp = new GameObject("ObstacleSpawnPoint");
                sp.transform.SetParent(tutorialManagerObj.transform);
                sp.transform.position = new Vector3(35f, 0f, 0f);
                obstacleSpawn = sp.transform;
            }

            Transform exitSpawn = tutorialManagerObj.transform.Find("ExitSpawnPoint");
            if (exitSpawn == null)
            {
                GameObject sp = new GameObject("ExitSpawnPoint");
                sp.transform.SetParent(tutorialManagerObj.transform);
                sp.transform.position = new Vector3(70f, 0f, 0f);
                exitSpawn = sp.transform;
            }

            var tmSO = new SerializedObject(tutorialManager);
            var sonarObj = GameObject.Find("[Machine] Map Navigation");
            if (sonarObj != null) tmSO.FindProperty("sonarMachineObject").objectReferenceValue = sonarObj;

            var stonePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/MapLevel-Grid/Stone.prefab");
            if (stonePrefab != null) tmSO.FindProperty("tutorialObstaclePrefab").objectReferenceValue = stonePrefab;
            tmSO.FindProperty("obstacleSpawnPoint").objectReferenceValue = obstacleSpawn;

            if (exitPrefab != null) tmSO.FindProperty("tutorialExitPrefab").objectReferenceValue = exitPrefab;
            tmSO.FindProperty("exitSpawnPoint").objectReferenceValue = exitSpawn;
            tmSO.ApplyModifiedProperties();

            // 3. Setup Camera Preview Tour Component & UI
            SetupCameraPreview(tutorialManagerObj, fontAsset);

            // 4. Setup World Highlights & Floating Badges on Machines
            SetupMachineHighlights(fontAsset);

            // 5. Setup Task Tracker UI Canvas
            SetupTaskTrackerUI(fontAsset);

            // 6. Setup Multi-Page Dialogue Overlay UI Canvas
            SetupDialogueOverlayUI(fontAsset);

            // 7. Setup Fuel Converter Audio
            SetupFuelConverterAudio();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();

            Debug.Log("[TutorialSceneSetup] Enhanced Tutorial Scene Setup Complete & Saved!");
        }

        private static void SetupFuelConverterAudio()
        {
            var fuelMachines = Object.FindObjectsByType<FuelConverterMachine>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var fuelLoopClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Machine/Temp-Wheel.mp3");
            var fuelCompleteClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Machine/Fuel-Recharge.mp3");
            var buttonClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Machine/UI-Button.mp3");

            foreach (var fm in fuelMachines)
            {
                var fmSO = new SerializedObject(fm);
                if (fuelLoopClip != null) fmSO.FindProperty("fuelConvertLoopClip").objectReferenceValue = fuelLoopClip;
                if (fuelCompleteClip != null) fmSO.FindProperty("conversionCompleteClip").objectReferenceValue = fuelCompleteClip;
                if (buttonClip != null) fmSO.FindProperty("buttonClickClip").objectReferenceValue = buttonClip;
                fmSO.ApplyModifiedProperties();
            }
        }

        private static GameObject SetupExitBeaconPrefab()
        {
            string prefabPath = "Assets/Prefab/Tutorial/TutorialExitBeacon.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existing != null) return existing;

            if (!AssetDatabase.IsValidFolder("Assets/Prefab/Tutorial"))
            {
                AssetDatabase.CreateFolder("Assets/Prefab", "Tutorial");
            }

            var baseExitPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/MapLevel-Grid/Exit.prefab");
            GameObject instance;
            if (baseExitPrefab != null)
            {
                instance = (GameObject)PrefabUtility.InstantiatePrefab(baseExitPrefab);
            }
            else
            {
                instance = GameObject.CreatePrimitive(PrimitiveType.Cube);
                instance.name = "TutorialExitBeacon";
            }

            var legacyExit = instance.GetComponent<ExitPoint>();
            if (legacyExit != null) Object.DestroyImmediate(legacyExit, true);

            var beacon = instance.GetComponent<TutorialExitBeacon>();
            if (beacon == null) beacon = instance.AddComponent<TutorialExitBeacon>();

            var col = instance.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            Object.DestroyImmediate(instance);
            return savedPrefab;
        }

        private static void SetupCameraPreview(GameObject managerObj, TMP_FontAsset fontAsset)
        {
            var preview = managerObj.GetComponent<TutorialCameraPreview>();
            if (preview == null) preview = managerObj.AddComponent<TutorialCameraPreview>();

            // Setup Preview UI Overlay Canvas
            GameObject previewCanvasObj = GameObject.Find("[UI] Tutorial Camera Preview");
            if (previewCanvasObj == null)
            {
                previewCanvasObj = new GameObject("[UI] Tutorial Camera Preview");
                var canvas = previewCanvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 300;

                var scaler = previewCanvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;

                previewCanvasObj.AddComponent<GraphicRaycaster>();
            }

            Transform containerT = previewCanvasObj.transform.Find("PreviewContainer");
            GameObject containerObj;
            if (containerT == null)
            {
                containerObj = new GameObject("PreviewContainer");
                containerObj.transform.SetParent(previewCanvasObj.transform, false);
                var rt = containerObj.AddComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.sizeDelta = Vector2.zero;
            }
            else
            {
                containerObj = containerT.gameObject;
            }

            // Top Header Banner
            Transform bannerT = containerObj.transform.Find("StationBanner");
            GameObject bannerObj;
            TextMeshProUGUI titleTxt;
            TextMeshProUGUI deckTxt;

            if (bannerT == null)
            {
                bannerObj = new GameObject("StationBanner");
                bannerObj.transform.SetParent(containerObj.transform, false);
                var rt = bannerObj.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 1f);
                rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0f, -40f);
                rt.sizeDelta = new Vector2(650f, 90f);

                var img = bannerObj.AddComponent<Image>();
                img.color = new Color(0.04f, 0.08f, 0.15f, 0.92f);

                // Title
                GameObject tObj = new GameObject("StationTitle");
                tObj.transform.SetParent(bannerObj.transform, false);
                var tRT = tObj.AddComponent<RectTransform>();
                tRT.anchorMin = new Vector2(0, 0.5f);
                tRT.anchorMax = new Vector2(1, 1);
                tRT.pivot = new Vector2(0.5f, 0.5f);
                tRT.offsetMin = new Vector2(10, 0);
                tRT.offsetMax = new Vector2(-10, -8);

                titleTxt = tObj.AddComponent<TextMeshProUGUI>();
                if (fontAsset != null) titleTxt.font = fontAsset;
                titleTxt.fontSize = 28;
                titleTxt.fontStyle = FontStyles.Bold;
                titleTxt.color = new Color(0.3f, 1f, 0.5f, 1f);
                titleTxt.alignment = TextAlignmentOptions.Center;
                titleTxt.text = "[STATION 1/6] FUEL CONVERTER";

                // Deck Location
                GameObject dObj = new GameObject("DeckLocation");
                dObj.transform.SetParent(bannerObj.transform, false);
                var dRT = dObj.AddComponent<RectTransform>();
                dRT.anchorMin = new Vector2(0, 0);
                dRT.anchorMax = new Vector2(1, 0.5f);
                dRT.pivot = new Vector2(0.5f, 0.5f);
                dRT.offsetMin = new Vector2(10, 5);
                dRT.offsetMax = new Vector2(-10, 0);

                deckTxt = dObj.AddComponent<TextMeshProUGUI>();
                if (fontAsset != null) deckTxt.font = fontAsset;
                deckTxt.fontSize = 18;
                deckTxt.color = new Color(0.8f, 0.9f, 1f, 0.85f);
                deckTxt.alignment = TextAlignmentOptions.Center;
                deckTxt.text = "Deck 2 - Upper Left Section";
            }
            else
            {
                bannerObj = bannerT.gameObject;
                titleTxt = bannerT.Find("StationTitle")?.GetComponent<TextMeshProUGUI>();
                deckTxt = bannerT.Find("DeckLocation")?.GetComponent<TextMeshProUGUI>();
            }

            // Skip Prompt (Bottom-Center)
            Transform skipT = containerObj.transform.Find("SkipPrompt");
            TextMeshProUGUI skipTxt;
            if (skipT == null)
            {
                GameObject sObj = new GameObject("SkipPrompt");
                sObj.transform.SetParent(containerObj.transform, false);
                var rt = sObj.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0f);
                rt.anchorMax = new Vector2(0.5f, 0f);
                rt.pivot = new Vector2(0.5f, 0f);
                rt.anchoredPosition = new Vector2(0f, 30f);
                rt.sizeDelta = new Vector2(500f, 40f);

                skipTxt = sObj.AddComponent<TextMeshProUGUI>();
                if (fontAsset != null) skipTxt.font = fontAsset;
                skipTxt.fontSize = 20;
                skipTxt.color = new Color(1f, 1f, 1f, 0.75f);
                skipTxt.alignment = TextAlignmentOptions.Center;
                skipTxt.text = "Press <color=#4ef>SPACE</color> to Skip Tour";
            }
            else
            {
                skipTxt = skipT.GetComponent<TextMeshProUGUI>();
            }

            var previewSO = new SerializedObject(preview);
            previewSO.FindProperty("previewUIContainer").objectReferenceValue = containerObj;
            previewSO.FindProperty("stationTitleText").objectReferenceValue = titleTxt;
            previewSO.FindProperty("stationDeckText").objectReferenceValue = deckTxt;
            previewSO.FindProperty("skipPromptText").objectReferenceValue = skipTxt;

            var fgObj = GameObject.Find("MainShip - 3D/Foreground") ?? GameObject.Find("Foreground");
            if (fgObj != null)
            {
                previewSO.FindProperty("foregroundObject").objectReferenceValue = fgObj;
            }

            var targetsProp = previewSO.FindProperty("previewTargets");
            targetsProp.ClearArray();

            var targetConfigs = new (string name, string deck, MachineUIType type, string path)[]
            {
                ("FUEL CONVERTER", "Deck 2 - Upper Left Section (Engine Room)", MachineUIType.FuelConverter, "MainShip - 3D/---------Machine-------------/FuelMachine/FuelMachine"),
                ("OXYGEN GENERATOR", "Deck 1 - Lower Right Section (Oxygen Room)", MachineUIType.OxygenPump, "MainShip - 3D/---------Machine-------------/OxygenMachine/OxygenMachine"),
                ("TEMPERATURE & COOLANT VALVE", "Deck 1 - Mid Right Section (Cooler Room)", MachineUIType.CoolantGame, "MainShip - 3D/---------Machine-------------/TemperatureMachine/CoolantMachine"),
                ("HULL PRESSURE STABILIZER", "Deck 2 - Mid Deck Section (Pressure Room)", MachineUIType.PressureGame, "MainShip - 3D/---------Machine-------------/PressureMachine/[script] PressureMachine"),
                ("BILGE WATER DRAIN PUMP", "Bilge Level - Submarine Keel (Pump Room)", MachineUIType.WaterPump, "MainShip - 3D/---------Machine-------------/PumpMachine/PumpMachine"),
                ("HELM & SONAR RADAR", "Bridge - Command Deck (Control Room)", MachineUIType.MapNavigation, "MainShip - 3D/---------Machine-------------/[Machine] Map Navigation")
            };

            for (int i = 0; i < targetConfigs.Length; i++)
            {
                var cfg = targetConfigs[i];
                targetsProp.InsertArrayElementAtIndex(i);
                var elem = targetsProp.GetArrayElementAtIndex(i);
                elem.FindPropertyRelative("stationName").stringValue = cfg.name;
                elem.FindPropertyRelative("deckLocation").stringValue = cfg.deck;
                elem.FindPropertyRelative("machineType").enumValueIndex = (int)cfg.type;
                elem.FindPropertyRelative("holdDuration").floatValue = 2.0f;

                var go = GameObject.Find(cfg.path);
                if (go != null)
                {
                    elem.FindPropertyRelative("targetTransform").objectReferenceValue = go.transform;
                }
            }

            previewSO.ApplyModifiedProperties();

            containerObj.SetActive(false);
        }

        private static void SetupMachineHighlights(TMP_FontAsset fontAsset)
        {
            var machines = new (string path, MachineUIType type, string badge, float height)[]
            {
                ("MainShip - 3D/---------Machine-------------/FuelMachine/FuelMachine", MachineUIType.FuelConverter, "[E] FUEL", 2.2f),
                ("MainShip - 3D/---------Machine-------------/PumpMachine/PumpMachine", MachineUIType.WaterPump, "[E] PUMP", 2.2f),
                ("MainShip - 3D/---------Machine-------------/TemperatureMachine/CoolantMachine", MachineUIType.CoolantGame, "[E] COOLANT", 2.2f),
                ("MainShip - 3D/---------Machine-------------/PressureMachine/[script] PressureMachine", MachineUIType.PressureGame, "[E] PRESSURE", 2.2f),
                ("MainShip - 3D/---------Machine-------------/OxygenMachine/OxygenMachine", MachineUIType.OxygenPump, "[E] O2 PUMP", 2.4f),
                ("MainShip - 3D/---------Machine-------------/[Machine] Map Navigation", MachineUIType.MapNavigation, "[E] SONAR", 2.2f)
            };

            foreach (var item in machines)
            {
                var go = GameObject.Find(item.path);
                if (go == null) continue;

                var highlight = go.GetComponent<TutorialWorldHighlight>();
                if (highlight == null) highlight = go.AddComponent<TutorialWorldHighlight>();

                var so = new SerializedObject(highlight);
                so.FindProperty("machineType").enumValueIndex = (int)item.type;

                var sr = go.GetComponentInChildren<SpriteRenderer>();
                if (sr != null)
                {
                    so.FindProperty("targetSpriteRenderer").objectReferenceValue = sr;
                }

                // Setup World-Space Badge Marker
                Transform markerT = go.transform.Find("FloatingMarker");
                if (markerT != null) Object.DestroyImmediate(markerT.gameObject);

                GameObject markerObj = new GameObject("FloatingMarker");
                markerObj.transform.SetParent(go.transform, false);
                markerObj.transform.localPosition = new Vector3(0f, item.height, 0f);
                markerObj.transform.localRotation = Quaternion.identity;
                markerObj.transform.localScale = new Vector3(0.015f, 0.015f, 0.015f);

                var canvas = markerObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.sortingOrder = 50;

                var rt = markerObj.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(160f, 90f);

                // Badge Container
                GameObject badgeObj = new GameObject("Badge");
                badgeObj.transform.SetParent(markerObj.transform, false);
                var bRT = badgeObj.AddComponent<RectTransform>();
                bRT.anchorMin = new Vector2(0.5f, 1f);
                bRT.anchorMax = new Vector2(0.5f, 1f);
                bRT.pivot = new Vector2(0.5f, 1f);
                bRT.anchoredPosition = Vector2.zero;
                bRT.sizeDelta = new Vector2(150f, 40f);

                var bImg = badgeObj.AddComponent<Image>();
                bImg.color = new Color(0.05f, 0.12f, 0.08f, 0.92f);

                // Text
                GameObject lblObj = new GameObject("Label");
                lblObj.transform.SetParent(badgeObj.transform, false);
                var lRT = lblObj.AddComponent<RectTransform>();
                lRT.anchorMin = Vector2.zero;
                lRT.anchorMax = Vector2.one;
                lRT.sizeDelta = Vector2.zero;
                var lbl = lblObj.AddComponent<TextMeshProUGUI>();
                if (fontAsset != null) lbl.font = fontAsset;
                lbl.fontSize = 26;
                lbl.fontStyle = FontStyles.Bold;
                lbl.color = new Color(0.2f, 1f, 0.4f, 1f);
                lbl.alignment = TextAlignmentOptions.Center;
                lbl.text = item.badge;

                // Downward Arrow
                GameObject arrowObj = new GameObject("Arrow");
                arrowObj.transform.SetParent(markerObj.transform, false);
                var aRT = arrowObj.AddComponent<RectTransform>();
                aRT.anchorMin = new Vector2(0.5f, 0f);
                aRT.anchorMax = new Vector2(0.5f, 0f);
                aRT.pivot = new Vector2(0.5f, 0f);
                aRT.anchoredPosition = new Vector2(0f, 5f);
                aRT.sizeDelta = new Vector2(40f, 35f);

                var arrowTxt = arrowObj.AddComponent<TextMeshProUGUI>();
                if (fontAsset != null) arrowTxt.font = fontAsset;
                arrowTxt.fontSize = 36;
                arrowTxt.fontStyle = FontStyles.Bold;
                arrowTxt.color = new Color(0.2f, 1f, 0.4f, 1f);
                arrowTxt.alignment = TextAlignmentOptions.Center;
                arrowTxt.text = "▼";

                so.FindProperty("floatingMarker").objectReferenceValue = markerObj;
                so.ApplyModifiedProperties();
            }
        }

        private static void SetupTaskTrackerUI(TMP_FontAsset fontAsset)
        {
            GameObject canvasObj = GameObject.Find("[UI] Tutorial Task Tracker");
            if (canvasObj == null)
            {
                canvasObj = new GameObject("[UI] Tutorial Task Tracker");
                var canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;

                var scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;

                canvasObj.AddComponent<GraphicRaycaster>();
            }

            Transform panelT = canvasObj.transform.Find("TaskTrackerPanel");
            GameObject panelObj;
            if (panelT == null)
            {
                panelObj = new GameObject("TaskTrackerPanel");
                panelObj.transform.SetParent(canvasObj.transform, false);
                var rt = panelObj.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(1, 1);
                rt.anchorMax = new Vector2(1, 1);
                rt.pivot = new Vector2(1, 1);
                rt.anchoredPosition = new Vector2(-25, -25);
                rt.sizeDelta = new Vector2(400, 310);

                var bgImage = panelObj.AddComponent<Image>();
                bgImage.color = new Color(0.05f, 0.08f, 0.14f, 0.88f);
            }
            else
            {
                panelObj = panelT.gameObject;
            }

            Transform headerT = panelObj.transform.Find("HeaderTitle");
            TextMeshProUGUI headerText;
            if (headerT == null)
            {
                GameObject hObj = new GameObject("HeaderTitle");
                hObj.transform.SetParent(panelObj.transform, false);
                var rt = hObj.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(0, 1);
                rt.anchorMax = new Vector2(1, 1);
                rt.pivot = new Vector2(0.5f, 1);
                rt.anchoredPosition = new Vector2(0, -12);
                rt.sizeDelta = new Vector2(-20, 30);

                headerText = hObj.AddComponent<TextMeshProUGUI>();
                if (fontAsset != null) headerText.font = fontAsset;
                headerText.fontSize = 24;
                headerText.fontStyle = FontStyles.Bold;
                headerText.color = new Color(1f, 0.85f, 0.2f, 1f);
                headerText.alignment = TextAlignmentOptions.Center;
                headerText.text = "TUTORIAL: SHIP STATIONS";
            }
            else
            {
                headerText = headerT.GetComponent<TextMeshProUGUI>();
            }

            Transform counterT = panelObj.transform.Find("ProgressCounter");
            TextMeshProUGUI counterText;
            if (counterT == null)
            {
                GameObject cObj = new GameObject("ProgressCounter");
                cObj.transform.SetParent(panelObj.transform, false);
                var rt = cObj.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(0, 1);
                rt.anchorMax = new Vector2(1, 1);
                rt.pivot = new Vector2(0.5f, 1);
                rt.anchoredPosition = new Vector2(0, -42);
                rt.sizeDelta = new Vector2(-20, 24);

                counterText = cObj.AddComponent<TextMeshProUGUI>();
                if (fontAsset != null) counterText.font = fontAsset;
                counterText.fontSize = 18;
                counterText.color = new Color(0.4f, 0.9f, 1f, 0.9f);
                counterText.alignment = TextAlignmentOptions.Center;
                counterText.text = "Stations Done: 0/5";
            }
            else
            {
                counterText = counterT.GetComponent<TextMeshProUGUI>();
            }

            Transform rowsContainerT = panelObj.transform.Find("RowsContainer");
            GameObject rowsContainer;
            if (rowsContainerT == null)
            {
                rowsContainer = new GameObject("RowsContainer");
                rowsContainer.transform.SetParent(panelObj.transform, false);
                var rt = rowsContainer.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(0, 0);
                rt.anchorMax = new Vector2(1, 1);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.offsetMin = new Vector2(15, 15);
                rt.offsetMax = new Vector2(-15, -70);

                var vlg = rowsContainer.AddComponent<VerticalLayoutGroup>();
                vlg.spacing = 6;
                vlg.childControlWidth = true;
                vlg.childControlHeight = false;
                vlg.childForceExpandWidth = true;
                vlg.childForceExpandHeight = false;
            }
            else
            {
                rowsContainer = rowsContainerT.gameObject;
                var vlg = rowsContainer.GetComponent<VerticalLayoutGroup>();
                if (vlg != null)
                {
                    vlg.spacing = 6;
                    vlg.childControlWidth = true;
                    vlg.childControlHeight = false;
                    vlg.childForceExpandWidth = true;
                    vlg.childForceExpandHeight = false;
                }
            }

            var trackerUI = canvasObj.GetComponent<TutorialTaskTrackerUI>();
            if (trackerUI == null) trackerUI = canvasObj.AddComponent<TutorialTaskTrackerUI>();

            var taskDefinitions = new (MachineUIType type, string name, string label)[]
            {
                (MachineUIType.FuelConverter, "Row_Fuel", "1. Fuel Converter: Hold [SPACE] to recharge"),
                (MachineUIType.OxygenPump, "Row_Oxygen", "2. Oxygen Generator: Pump oxygen"),
                (MachineUIType.CoolantGame, "Row_Coolant", "3. Temperature Valve: Regulate heat"),
                (MachineUIType.PressureGame, "Row_Pressure", "4. Pressure Stabilizer: Hit target zone"),
                (MachineUIType.WaterPump, "Row_WaterPump", "5. Bilge Pump: Drain room water"),
                (MachineUIType.MapNavigation, "Row_Sonar", "6. Sonar & Helm: Steer to exit")
            };

            var trackerSO = new SerializedObject(trackerUI);
            trackerSO.FindProperty("headerTitleText").objectReferenceValue = headerText;
            trackerSO.FindProperty("progressCounterText").objectReferenceValue = counterText;

            var coinClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Feel/NiceVibrations/HapticSamples/Objects/Coins2.wav") ?? AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Machine/UI-Button.mp3");
            if (coinClip != null)
            {
                trackerSO.FindProperty("taskCompleteClip").objectReferenceValue = coinClip;
            }

            var rowsProp = trackerSO.FindProperty("taskRows");
            rowsProp.ClearArray();

            for (int i = 0; i < taskDefinitions.Length; i++)
            {
                var def = taskDefinitions[i];
                Transform rowT = rowsContainer.transform.Find(def.name);
                GameObject rowObj;
                Image checkImg;
                TextMeshProUGUI rowTxt;

                if (rowT == null)
                {
                    rowObj = new GameObject(def.name);
                    rowObj.transform.SetParent(rowsContainer.transform, false);
                    var rt = rowObj.AddComponent<RectTransform>();
                    rt.sizeDelta = new Vector2(0, 26);

                    var hlg = rowObj.AddComponent<HorizontalLayoutGroup>();
                    hlg.spacing = 8;
                    hlg.childControlWidth = false;
                    hlg.childControlHeight = false;
                    hlg.childForceExpandWidth = false;
                    hlg.childForceExpandHeight = false;
                    hlg.childAlignment = TextAnchor.MiddleLeft;

                    var le = rowObj.AddComponent<LayoutElement>();
                    le.minHeight = 26;
                    le.preferredHeight = 26;
                    le.flexibleHeight = 0;

                    GameObject iconObj = new GameObject("Checkmark");
                    iconObj.transform.SetParent(rowObj.transform, false);
                    var iconRT = iconObj.AddComponent<RectTransform>();
                    iconRT.sizeDelta = new Vector2(22, 22);
                    checkImg = iconObj.AddComponent<Image>();
                    checkImg.color = new Color(0.6f, 0.6f, 0.6f, 0.7f);

                    var iconLE = iconObj.AddComponent<LayoutElement>();
                    iconLE.minWidth = 22;
                    iconLE.minHeight = 22;
                    iconLE.preferredWidth = 22;
                    iconLE.preferredHeight = 22;
                    iconLE.flexibleWidth = 0;
                    iconLE.flexibleHeight = 0;

                    GameObject txtObj = new GameObject("Label");
                    txtObj.transform.SetParent(rowObj.transform, false);
                    var txtRT = txtObj.AddComponent<RectTransform>();
                    txtRT.sizeDelta = new Vector2(330, 24);
                    rowTxt = txtObj.AddComponent<TextMeshProUGUI>();
                    if (fontAsset != null) rowTxt.font = fontAsset;
                    rowTxt.fontSize = 17;
                    rowTxt.color = Color.white;
                    rowTxt.alignment = TextAlignmentOptions.Left;
                    rowTxt.text = def.label;
                }
                else
                {
                    rowObj = rowT.gameObject;
                    var rt = rowObj.GetComponent<RectTransform>();
                    if (rt != null) rt.sizeDelta = new Vector2(0, 26);

                    var hlg = rowObj.GetComponent<HorizontalLayoutGroup>();
                    if (hlg != null)
                    {
                        hlg.spacing = 8;
                        hlg.childControlWidth = false;
                        hlg.childControlHeight = false;
                        hlg.childForceExpandWidth = false;
                        hlg.childForceExpandHeight = false;
                        hlg.childAlignment = TextAnchor.MiddleLeft;
                    }

                    var le = rowObj.GetComponent<LayoutElement>() ?? rowObj.AddComponent<LayoutElement>();
                    le.minHeight = 26;
                    le.preferredHeight = 26;
                    le.flexibleHeight = 0;

                    var iconT = rowT.Find("Checkmark");
                    if (iconT != null)
                    {
                        var iconRT = iconT.GetComponent<RectTransform>();
                        if (iconRT != null) iconRT.sizeDelta = new Vector2(22, 22);
                        var iconLE = iconT.GetComponent<LayoutElement>() ?? iconT.gameObject.AddComponent<LayoutElement>();
                        iconLE.minWidth = 22;
                        iconLE.minHeight = 22;
                        iconLE.preferredWidth = 22;
                        iconLE.preferredHeight = 22;
                        iconLE.flexibleWidth = 0;
                        iconLE.flexibleHeight = 0;
                    }

                    checkImg = rowT.Find("Checkmark")?.GetComponent<Image>();
                    rowTxt = rowT.Find("Label")?.GetComponent<TextMeshProUGUI>();
                    if (rowTxt != null && fontAsset != null) rowTxt.font = fontAsset;
                }

                rowsProp.InsertArrayElementAtIndex(i);
                var elem = rowsProp.GetArrayElementAtIndex(i);
                elem.FindPropertyRelative("machineType").enumValueIndex = (int)def.type;
                elem.FindPropertyRelative("rowContainer").objectReferenceValue = rowObj;
                elem.FindPropertyRelative("checkmarkImage").objectReferenceValue = checkImg;
                elem.FindPropertyRelative("taskText").objectReferenceValue = rowTxt;
                elem.FindPropertyRelative("defaultText").stringValue = def.label;
                elem.FindPropertyRelative("completedText").stringValue = def.label + " [DONE]";
            }

            var mmf = panelObj.GetComponent<MMF_Player>();
            if (mmf == null) mmf = panelObj.AddComponent<MMF_Player>();
            trackerSO.FindProperty("trackerPanel").objectReferenceValue = panelObj;
            trackerSO.FindProperty("taskCompletedFeedback").objectReferenceValue = mmf;
            trackerSO.ApplyModifiedProperties();
        }

        private static void SetupDialogueOverlayUI(TMP_FontAsset fontAsset)
        {
            GameObject canvasObj = GameObject.Find("[UI] Tutorial Dialogue Overlay");
            if (canvasObj == null)
            {
                canvasObj = new GameObject("[UI] Tutorial Dialogue Overlay");
                var canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 200;

                var scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;

                canvasObj.AddComponent<GraphicRaycaster>();
            }

            Transform panelT = canvasObj.transform.Find("DialoguePanel");
            GameObject panelObj;
            if (panelT == null)
            {
                panelObj = new GameObject("DialoguePanel");
                panelObj.transform.SetParent(canvasObj.transform, false);
                var rt = panelObj.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0);
                rt.anchorMax = new Vector2(0.5f, 0);
                rt.pivot = new Vector2(0.5f, 0);
                rt.anchoredPosition = new Vector2(0, 30);
                rt.sizeDelta = new Vector2(820, 165);

                var bgImage = panelObj.AddComponent<Image>();
                bgImage.color = new Color(0.04f, 0.07f, 0.12f, 0.96f);
            }
            else
            {
                panelObj = panelT.gameObject;
                var rt = panelObj.GetComponent<RectTransform>();
                rt.anchoredPosition = new Vector2(0, 30);
                rt.sizeDelta = new Vector2(820, 165);
            }

            // Title
            Transform titleT = panelObj.transform.Find("TitleText");
            TextMeshProUGUI titleTxt;
            if (titleT == null)
            {
                GameObject tObj = new GameObject("TitleText");
                tObj.transform.SetParent(panelObj.transform, false);
                var rt = tObj.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(0, 1);
                rt.anchorMax = new Vector2(1, 1);
                rt.pivot = new Vector2(0, 1);
                rt.anchoredPosition = new Vector2(25, -12);
                rt.sizeDelta = new Vector2(-200, 26);

                titleTxt = tObj.AddComponent<TextMeshProUGUI>();
                if (fontAsset != null) titleTxt.font = fontAsset;
                titleTxt.fontSize = 22;
                titleTxt.fontStyle = FontStyles.Bold;
                titleTxt.color = new Color(0.3f, 1f, 0.5f, 1f);
                titleTxt.text = "MACHINE INSTRUCTIONS";
            }
            else
            {
                titleTxt = titleT.GetComponent<TextMeshProUGUI>();
                if (fontAsset != null) titleTxt.font = fontAsset;
                var rt = titleT.GetComponent<RectTransform>();
                rt.anchoredPosition = new Vector2(25, -12);
                rt.sizeDelta = new Vector2(-200, 26);
            }

            // Page Indicator
            Transform pageT = panelObj.transform.Find("PageIndicator");
            TextMeshProUGUI pageTxt;
            if (pageT == null)
            {
                GameObject pObj = new GameObject("PageIndicator");
                pObj.transform.SetParent(panelObj.transform, false);
                var rt = pObj.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(1, 1);
                rt.anchorMax = new Vector2(1, 1);
                rt.pivot = new Vector2(1, 1);
                rt.anchoredPosition = new Vector2(-55, -14);
                rt.sizeDelta = new Vector2(60, 24);

                pageTxt = pObj.AddComponent<TextMeshProUGUI>();
                if (fontAsset != null) pageTxt.font = fontAsset;
                pageTxt.fontSize = 18;
                pageTxt.color = new Color(0.7f, 0.85f, 1f, 0.8f);
                pageTxt.alignment = TextAlignmentOptions.Right;
                pageTxt.text = "1 / 3";
            }
            else
            {
                pageTxt = pageT.GetComponent<TextMeshProUGUI>();
                if (fontAsset != null) pageTxt.font = fontAsset;
                var rt = pageT.GetComponent<RectTransform>();
                rt.anchoredPosition = new Vector2(-55, -14);
                rt.sizeDelta = new Vector2(60, 24);
            }

            // Close Button (Top-Right "X")
            Transform closeT = panelObj.transform.Find("CloseButton");
            Button closeBtn;
            if (closeT == null)
            {
                GameObject cObj = new GameObject("CloseButton");
                cObj.transform.SetParent(panelObj.transform, false);
                var rt = cObj.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(1, 1);
                rt.anchorMax = new Vector2(1, 1);
                rt.pivot = new Vector2(1, 1);
                rt.anchoredPosition = new Vector2(-15, -12);
                rt.sizeDelta = new Vector2(26, 26);

                var img = cObj.AddComponent<Image>();
                img.color = new Color(0.6f, 0.2f, 0.2f, 0.9f);
                closeBtn = cObj.AddComponent<Button>();

                GameObject lblObj = new GameObject("Text");
                lblObj.transform.SetParent(cObj.transform, false);
                var lblRT = lblObj.AddComponent<RectTransform>();
                lblRT.anchorMin = Vector2.zero;
                lblRT.anchorMax = Vector2.one;
                lblRT.sizeDelta = Vector2.zero;
                var lbl = lblObj.AddComponent<TextMeshProUGUI>();
                if (fontAsset != null) lbl.font = fontAsset;
                lbl.fontSize = 18;
                lbl.fontStyle = FontStyles.Bold;
                lbl.alignment = TextAlignmentOptions.Center;
                lbl.text = "X";
            }
            else
            {
                closeBtn = closeT.GetComponent<Button>();
                var rt = closeT.GetComponent<RectTransform>();
                rt.anchoredPosition = new Vector2(-15, -12);
                rt.sizeDelta = new Vector2(26, 26);
                var lbl = closeT.GetComponentInChildren<TextMeshProUGUI>();
                if (lbl != null)
                {
                    if (fontAsset != null) lbl.font = fontAsset;
                    lbl.text = "X";
                }
            }

            // Dialogue Text
            Transform dlgTextT = panelObj.transform.Find("DialogueText");
            TextMeshProUGUI dlgDialogueText;
            TextAnimator_TMP dlgTextAnimator;
            TypewriterComponent dlgTypewriter;

            if (dlgTextT == null)
            {
                GameObject txtObj = new GameObject("DialogueText");
                txtObj.transform.SetParent(panelObj.transform, false);
                var rt = txtObj.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(0, 0);
                rt.anchorMax = new Vector2(1, 1);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.offsetMin = new Vector2(25, 48);
                rt.offsetMax = new Vector2(-25, -48);

                dlgDialogueText = txtObj.AddComponent<TextMeshProUGUI>();
                if (fontAsset != null) dlgDialogueText.font = fontAsset;
                dlgDialogueText.fontSize = 20;
                dlgDialogueText.color = Color.white;
                dlgDialogueText.alignment = TextAlignmentOptions.Left;
                dlgDialogueText.enableWordWrapping = true;
                dlgDialogueText.richText = true;

                dlgTextAnimator = txtObj.AddComponent<TextAnimator_TMP>();
                dlgTypewriter = txtObj.AddComponent<TypewriterComponent>();
            }
            else
            {
                dlgDialogueText = dlgTextT.GetComponent<TextMeshProUGUI>();
                if (fontAsset != null) dlgDialogueText.font = fontAsset;
                dlgTextAnimator = dlgTextT.GetComponent<TextAnimator_TMP>();
                dlgTypewriter = dlgTextT.GetComponent<TypewriterComponent>();
                var rt = dlgTextT.GetComponent<RectTransform>();
                rt.offsetMin = new Vector2(25, 48);
                rt.offsetMax = new Vector2(-25, -48);
            }

            // Next Button (Bottom-Right)
            Transform nextT = panelObj.transform.Find("NextButton");
            Button nextBtn;
            if (nextT == null)
            {
                GameObject nObj = new GameObject("NextButton");
                nObj.transform.SetParent(panelObj.transform, false);
                var rt = nObj.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(1, 0);
                rt.anchorMax = new Vector2(1, 0);
                rt.pivot = new Vector2(1, 0);
                rt.anchoredPosition = new Vector2(-15, 12);
                rt.sizeDelta = new Vector2(90, 30);

                var img = nObj.AddComponent<Image>();
                img.color = new Color(0.2f, 0.5f, 0.85f, 0.95f);
                nextBtn = nObj.AddComponent<Button>();

                GameObject lblObj = new GameObject("Text");
                lblObj.transform.SetParent(nObj.transform, false);
                var lblRT = lblObj.AddComponent<RectTransform>();
                lblRT.anchorMin = Vector2.zero;
                lblRT.anchorMax = Vector2.one;
                lblRT.sizeDelta = Vector2.zero;
                var lbl = lblObj.AddComponent<TextMeshProUGUI>();
                if (fontAsset != null) lbl.font = fontAsset;
                lbl.fontSize = 16;
                lbl.fontStyle = FontStyles.Bold;
                lbl.alignment = TextAlignmentOptions.Center;
                lbl.text = "NEXT >";
            }
            else
            {
                nextBtn = nextT.GetComponent<Button>();
                var rt = nextT.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(1, 0);
                rt.anchorMax = new Vector2(1, 0);
                rt.pivot = new Vector2(1, 0);
                rt.anchoredPosition = new Vector2(-15, 12);
                rt.sizeDelta = new Vector2(90, 30);
            }

            // Prev Button (Bottom-Right, to the left of Next)
            Transform prevT = panelObj.transform.Find("PrevButton");
            Button prevBtn;
            if (prevT == null)
            {
                GameObject pObj = new GameObject("PrevButton");
                pObj.transform.SetParent(panelObj.transform, false);
                var rt = pObj.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(1, 0);
                rt.anchorMax = new Vector2(1, 0);
                rt.pivot = new Vector2(1, 0);
                rt.anchoredPosition = new Vector2(-115, 12);
                rt.sizeDelta = new Vector2(90, 30);

                var img = pObj.AddComponent<Image>();
                img.color = new Color(0.3f, 0.35f, 0.45f, 0.95f);
                prevBtn = pObj.AddComponent<Button>();

                GameObject lblObj = new GameObject("Text");
                lblObj.transform.SetParent(pObj.transform, false);
                var lblRT = lblObj.AddComponent<RectTransform>();
                lblRT.anchorMin = Vector2.zero;
                lblRT.anchorMax = Vector2.one;
                lblRT.sizeDelta = Vector2.zero;
                var lbl = lblObj.AddComponent<TextMeshProUGUI>();
                if (fontAsset != null) lbl.font = fontAsset;
                lbl.fontSize = 16;
                lbl.fontStyle = FontStyles.Bold;
                lbl.alignment = TextAlignmentOptions.Center;
                lbl.text = "< PREV";
            }
            else
            {
                prevBtn = prevT.GetComponent<Button>();
                var rt = prevT.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(1, 0);
                rt.anchorMax = new Vector2(1, 0);
                rt.pivot = new Vector2(1, 0);
                rt.anchoredPosition = new Vector2(-115, 12);
                rt.sizeDelta = new Vector2(90, 30);
            }

            // Dismiss Button ("GOT IT ✓" on last step, occupying same spot as Next)
            Transform dismissT = panelObj.transform.Find("DismissButton");
            Button dismissBtn;
            if (dismissT == null)
            {
                GameObject dObj = new GameObject("DismissButton");
                dObj.transform.SetParent(panelObj.transform, false);
                var rt = dObj.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(1, 0);
                rt.anchorMax = new Vector2(1, 0);
                rt.pivot = new Vector2(1, 0);
                rt.anchoredPosition = new Vector2(-15, 12);
                rt.sizeDelta = new Vector2(90, 30);

                var img = dObj.AddComponent<Image>();
                img.color = new Color(0.18f, 0.65f, 0.32f, 0.95f);
                dismissBtn = dObj.AddComponent<Button>();

                GameObject lblObj = new GameObject("Text");
                lblObj.transform.SetParent(dObj.transform, false);
                var lblRT = lblObj.AddComponent<RectTransform>();
                lblRT.anchorMin = Vector2.zero;
                lblRT.anchorMax = Vector2.one;
                lblRT.sizeDelta = Vector2.zero;
                var lbl = lblObj.AddComponent<TextMeshProUGUI>();
                if (fontAsset != null) lbl.font = fontAsset;
                lbl.fontSize = 16;
                lbl.fontStyle = FontStyles.Bold;
                lbl.alignment = TextAlignmentOptions.Center;
                lbl.text = "GOT IT";
            }
            else
            {
                dismissBtn = dismissT.GetComponent<Button>();
                var rt = dismissT.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(1, 0);
                rt.anchorMax = new Vector2(1, 0);
                rt.pivot = new Vector2(1, 0);
                rt.anchoredPosition = new Vector2(-15, 12);
                rt.sizeDelta = new Vector2(90, 30);
            }

            // Highlight Pointer (Floating Bouncing Arrow Indicator)
            Transform frameT = canvasObj.transform.Find("HighlightFrame");
            GameObject frameObj;
            Image frameImg;
            if (frameT == null)
            {
                frameObj = new GameObject("HighlightFrame");
                frameObj.transform.SetParent(canvasObj.transform, false);
                var rt = frameObj.AddComponent<RectTransform>();
                rt.sizeDelta = new Vector2(60, 60);

                frameImg = frameObj.AddComponent<Image>();
                frameImg.color = Color.clear;
                frameImg.raycastTarget = false;

                GameObject arrowObj = new GameObject("Arrow");
                arrowObj.transform.SetParent(frameObj.transform, false);
                var aRT = arrowObj.AddComponent<RectTransform>();
                aRT.anchorMin = Vector2.zero;
                aRT.anchorMax = Vector2.one;
                aRT.sizeDelta = Vector2.zero;

                var arrowTxt = arrowObj.AddComponent<TextMeshProUGUI>();
                if (fontAsset != null) arrowTxt.font = fontAsset;
                arrowTxt.fontSize = 42;
                arrowTxt.fontStyle = FontStyles.Bold;
                arrowTxt.color = new Color(0.2f, 1f, 0.4f, 1f);
                arrowTxt.alignment = TextAlignmentOptions.Center;
                arrowTxt.raycastTarget = false;
                arrowTxt.text = "▼";

                frameObj.SetActive(false);
            }
            else
            {
                frameObj = frameT.gameObject;
                var rt = frameObj.GetComponent<RectTransform>();
                if (rt != null) rt.sizeDelta = new Vector2(60, 60);

                frameImg = frameObj.GetComponent<Image>();
                if (frameImg != null)
                {
                    frameImg.color = Color.clear;
                    frameImg.enabled = false;
                }

                var arrowTxt = frameObj.GetComponentInChildren<TextMeshProUGUI>();
                if (arrowTxt == null)
                {
                    GameObject arrowObj = new GameObject("Arrow");
                    arrowObj.transform.SetParent(frameObj.transform, false);
                    var aRT = arrowObj.AddComponent<RectTransform>();
                    aRT.anchorMin = Vector2.zero;
                    aRT.anchorMax = Vector2.one;
                    aRT.sizeDelta = Vector2.zero;

                    arrowTxt = arrowObj.AddComponent<TextMeshProUGUI>();
                    if (fontAsset != null) arrowTxt.font = fontAsset;
                    arrowTxt.fontSize = 44;
                    arrowTxt.fontStyle = FontStyles.Bold;
                    arrowTxt.color = new Color(1f, 0.9f, 0.2f, 1f);
                    arrowTxt.alignment = TextAlignmentOptions.Center;
                    arrowTxt.raycastTarget = false;
                    arrowTxt.text = "▼";
                }
                else
                {
                    if (fontAsset != null) arrowTxt.font = fontAsset;
                    arrowTxt.fontSize = 44;
                    arrowTxt.fontStyle = FontStyles.Bold;
                    arrowTxt.color = new Color(1f, 0.9f, 0.2f, 1f);
                    arrowTxt.alignment = TextAlignmentOptions.Center;
                    arrowTxt.raycastTarget = false;
                    arrowTxt.text = "▼";
                }
            }

            var overlay = canvasObj.GetComponent<TutorialMinigameOverlay>();
            if (overlay == null) overlay = canvasObj.AddComponent<TutorialMinigameOverlay>();

            var overlaySO = new SerializedObject(overlay);
            overlaySO.FindProperty("dialoguePanel").objectReferenceValue = panelObj;
            overlaySO.FindProperty("titleText").objectReferenceValue = titleTxt;
            overlaySO.FindProperty("dialogueText").objectReferenceValue = dlgDialogueText;
            overlaySO.FindProperty("pageIndicatorText").objectReferenceValue = pageTxt;
            overlaySO.FindProperty("typewriter").objectReferenceValue = dlgTypewriter;
            overlaySO.FindProperty("textAnimator").objectReferenceValue = dlgTextAnimator;
            overlaySO.FindProperty("nextButton").objectReferenceValue = nextBtn;
            overlaySO.FindProperty("prevButton").objectReferenceValue = prevBtn;
            overlaySO.FindProperty("dismissButton").objectReferenceValue = dismissBtn;
            overlaySO.FindProperty("closeButton").objectReferenceValue = closeBtn;
            overlaySO.FindProperty("highlightFrame").objectReferenceValue = frameObj.GetComponent<RectTransform>();
            overlaySO.FindProperty("highlightImage").objectReferenceValue = frameImg;
            overlaySO.ApplyModifiedProperties();

            overlay.InitializeDefaultGuides();

            panelObj.SetActive(false);
        }
    }
}
