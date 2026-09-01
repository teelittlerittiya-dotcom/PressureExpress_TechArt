using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;
using PressureExpress.Framework;
using Unity.Netcode;

namespace PressureExpress.Tutorial
{
    [Serializable]
    public class StationPreviewTarget
    {
        public string stationName;
        public string deckLocation;
        public MachineUIType machineType;
        public Transform targetTransform;
        public float holdDuration = 2.0f;
    }

    public class TutorialCameraPreview : MonoBehaviour
    {
        public static TutorialCameraPreview Instance { get; private set; }

        [Header("Preview UI")]
        [SerializeField] private GameObject previewUIContainer;
        [SerializeField] private TextMeshProUGUI stationTitleText;
        [SerializeField] private TextMeshProUGUI stationDeckText;
        [SerializeField] private TextMeshProUGUI skipPromptText;

        [Header("Camera Control")]
        [SerializeField] private Camera targetCamera;
        [SerializeField] private float transitionDuration = 1.0f;
        [SerializeField] private float returnToPlayerDuration = 0.8f;

        [Header("Stations Tour")]
        [SerializeField] private List<StationPreviewTarget> previewTargets = new List<StationPreviewTarget>();

        [Header("Foreground / Occlusion Control")]
        [Tooltip("Foreground GameObject to disable during camera preview so it doesn't block station arrows/UI, and re-enable afterwards.")]
        [SerializeField] private GameObject foregroundObject;
        [Tooltip("Additional GameObjects to disable during camera preview.")]
        [SerializeField] private List<GameObject> additionalObjectsToDisable = new List<GameObject>();

        private bool isPreviewActive = false;
        private bool skipRequested = false;
        private bool advanceRequested = false;
        private PlayerCameraController playerCamController;
        private MainCamController mainCamController;

        private Vector3 fallbackFollowOffset = new Vector3(0f, 3.0f, -11.0f);
        private float fallbackBasePitch = 11.0f;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) Destroy(gameObject);

            if (foregroundObject == null)
            {
                foregroundObject = GameObject.Find("MainShip - 3D/Foreground") ?? GameObject.Find("Foreground");
            }

            if (previewTargets == null || previewTargets.Count == 0)
            {
                InitializeDefaultTargets();
            }
        }

        private void Start()
        {
            StartTourRoutine().Forget();
        }

        private void Update()
        {
            if (isPreviewActive && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Escape)))
            {
                skipRequested = true;
            }

            if (isPreviewActive && Input.GetMouseButtonDown(0))
            {
                advanceRequested = true;
            }
        }

        public void InitializeDefaultTargets()
        {
            previewTargets = new List<StationPreviewTarget>
            {
                new StationPreviewTarget
                {
                    stationName = "FUEL CONVERTER",
                    deckLocation = "Deck 2 - Upper Left Section (Engine Room)",
                    machineType = MachineUIType.FuelConverter,
                    holdDuration = 2.0f
                },
                new StationPreviewTarget
                {
                    stationName = "OXYGEN GENERATOR",
                    deckLocation = "Deck 1 - Lower Right Section (Oxygen Room)",
                    machineType = MachineUIType.OxygenPump,
                    holdDuration = 2.0f
                },
                new StationPreviewTarget
                {
                    stationName = "TEMPERATURE & COOLANT VALVE",
                    deckLocation = "Deck 1 - Mid Right Section (Cooler Room)",
                    machineType = MachineUIType.CoolantGame,
                    holdDuration = 2.0f
                },
                new StationPreviewTarget
                {
                    stationName = "HULL PRESSURE STABILIZER",
                    deckLocation = "Deck 2 - Mid Deck Section (Pressure Room)",
                    machineType = MachineUIType.PressureGame,
                    holdDuration = 2.0f
                },
                new StationPreviewTarget
                {
                    stationName = "BILGE WATER DRAIN PUMP",
                    deckLocation = "Bilge Level - Submarine Keel (Pump Room)",
                    machineType = MachineUIType.WaterPump,
                    holdDuration = 2.0f
                },
                new StationPreviewTarget
                {
                    stationName = "HELM & SONAR RADAR",
                    deckLocation = "Bridge - Command Deck (Control Room)",
                    machineType = MachineUIType.MapNavigation,
                    holdDuration = 2.0f
                }
            };
        }

        private void AutoResolveTargetTransforms()
        {
            var highlights = FindObjectsByType<TutorialWorldHighlight>(FindObjectsSortMode.None);
            var machineMap = new Dictionary<MachineUIType, Transform>();

            foreach (var h in highlights)
            {
                machineMap[h.MachineType] = h.transform;
            }

            foreach (var target in previewTargets)
            {
                if (target.targetTransform == null && machineMap.TryGetValue(target.machineType, out Transform t))
                {
                    target.targetTransform = t;
                }
            }
        }

        private Transform FindPlayerTransform()
        {
            if (NetworkManager.Singleton != null &&
                NetworkManager.Singleton.LocalClient != null &&
                NetworkManager.Singleton.LocalClient.PlayerObject != null)
            {
                return NetworkManager.Singleton.LocalClient.PlayerObject.transform;
            }

            var playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null) return playerObj.transform;

            var controller = FindFirstObjectByType<CharacterController2D>();
            if (controller != null) return controller.transform;

            return null;
        }

        public Vector3 CalculateCameraFraming(Vector3 targetWorldPos, out float targetPitch)
        {
            Vector3 offset = fallbackFollowOffset;
            targetPitch = fallbackBasePitch;

            if (RoomCameraOverride.TryGetOverrideForPosition(targetWorldPos, out RoomCameraOverride roomOverride))
            {
                offset = roomOverride.roomFollowOffset;
                if (roomOverride.overridePitch) targetPitch = roomOverride.roomPitch;
            }

            Vector3 desired = targetWorldPos + Quaternion.Euler(0f, 0f, 0f) * offset;
            desired.y = targetWorldPos.y + offset.y;
            return desired;
        }

        public async UniTaskVoid StartTourRoutine()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            if (targetCamera == null)
            {
                var camObj = GameObject.FindWithTag("MainCamera");
                if (camObj != null) targetCamera = camObj.GetComponent<Camera>();
            }

            if (targetCamera != null)
            {
                playerCamController = targetCamera.GetComponentInParent<PlayerCameraController>();
            }

            mainCamController = FindFirstObjectByType<MainCamController>();

            if (playerCamController != null)
            {
                playerCamController.isControlledExternally = true;
                playerCamController.enabled = false;
            }
            if (mainCamController != null)
            {
                mainCamController.SetExternalControl(true);
            }

            CharacterController2D.LockMovement();
            isPreviewActive = true;
            skipRequested = false;
            advanceRequested = false;

            SetForegroundActive(false);

            // Wait for player to spawn
            float waitDeadline = Time.realtimeSinceStartup + 5f;
            Transform playerT = null;

            while (Time.realtimeSinceStartup < waitDeadline)
            {
                playerT = FindPlayerTransform();
                if (playerT != null) break;
                await UniTask.Yield();
            }

            // Ensure controller is locked even if player script tried to re-acquire
            if (playerCamController == null && targetCamera != null)
            {
                playerCamController = targetCamera.GetComponentInParent<PlayerCameraController>();
            }
            if (mainCamController == null)
            {
                mainCamController = FindFirstObjectByType<MainCamController>();
            }
            if (playerCamController != null)
            {
                playerCamController.isControlledExternally = true;
                playerCamController.enabled = false;
            }
            if (mainCamController != null)
            {
                mainCamController.SetExternalControl(true);
            }

            AutoResolveTargetTransforms();

            if (previewUIContainer != null) previewUIContainer.SetActive(true);

            Transform camTransform = targetCamera != null ? targetCamera.transform : null;

            // Tour across each machine
            for (int i = 0; i < previewTargets.Count; i++)
            {
                if (skipRequested) break;

                var node = previewTargets[i];
                if (node.targetTransform == null) continue;

                if (stationTitleText != null) stationTitleText.text = $"[STATION {i + 1}/{previewTargets.Count}] {node.stationName}";
                if (stationDeckText != null) stationDeckText.text = node.deckLocation;

                if (camTransform != null)
                {
                    Vector3 startPos = camTransform.position;
                    Quaternion startRot = camTransform.rotation;

                    Vector3 targetFraming = CalculateCameraFraming(node.targetTransform.position, out float pitch);
                    Quaternion targetRot = Quaternion.Euler(pitch, 0f, 0f);

                    float elapsed = 0f;
                    float duration = transitionDuration;

                    while (elapsed < duration && !skipRequested)
                    {
                        elapsed += Time.deltaTime;
                        float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                        camTransform.position = Vector3.Lerp(startPos, targetFraming, t);
                        camTransform.rotation = Quaternion.Slerp(startRot, targetRot, t);
                        await UniTask.Yield();
                    }

                    camTransform.position = targetFraming;
                    camTransform.rotation = targetRot;
                }

                // Hold on the station. A mouse click advances early.
                advanceRequested = false;
                float holdTimer = 0f;
                while (holdTimer < node.holdDuration && !skipRequested && !advanceRequested)
                {
                    holdTimer += Time.deltaTime;
                    await UniTask.Yield();
                }
            }

            // Return to player with exact matching room framing
            if (previewUIContainer != null) previewUIContainer.SetActive(false);

            // Resolve the player again in case the network spawn was replaced during the tour.
            playerT = FindPlayerTransform();

            if (camTransform != null && playerT != null)
            {
                Vector3 startPos = camTransform.position;
                Quaternion startRot = camTransform.rotation;

                Vector3 playerFraming = CalculateCameraFraming(playerT.position, out float playerPitch);
                Quaternion playerRot = Quaternion.Euler(playerPitch, 0f, 0f);

                float elapsed = 0f;
                float duration = returnToPlayerDuration;

                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                    camTransform.position = Vector3.Lerp(startPos, playerFraming, t);
                    camTransform.rotation = Quaternion.Slerp(startRot, playerRot, t);
                    await UniTask.Yield();
                }

                camTransform.position = playerFraming;
                camTransform.rotation = playerRot;
            }

            if (playerCamController != null)
            {
                if (playerT != null)
                {
                    playerCamController.SnapToTarget(playerT);
                }
                playerCamController.isControlledExternally = false;
                playerCamController.enabled = true;
            }
            if (mainCamController != null)
            {
                mainCamController.SetExternalControl(false);
            }

            SetForegroundActive(true);
            CharacterController2D.UnlockMovement();
            isPreviewActive = false;
        }

        public void SetForegroundActive(bool active)
        {
            if (foregroundObject != null)
            {
                foregroundObject.SetActive(active);
            }

            if (additionalObjectsToDisable != null)
            {
                for (int i = 0; i < additionalObjectsToDisable.Count; i++)
                {
                    if (additionalObjectsToDisable[i] != null)
                    {
                        additionalObjectsToDisable[i].SetActive(active);
                    }
                }
            }
        }

        private void OnDisable()
        {
            if (isPreviewActive)
            {
                SetForegroundActive(true);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (isPreviewActive)
            {
                SetForegroundActive(true);
            }
        }
    }
}
