using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Cysharp.Threading.Tasks;
using PressureExpress.Framework;

namespace PressureExpress.Tutorial
{
    public enum TutorialPhase
    {
        InternalMachines = 0,
        SonarStation = 1,
        SteerToExit = 2,
        Complete = 3
    }

    public class TutorialManager : MonoBehaviour
    {
        public static TutorialManager Instance { get; private set; }

        [Header("Tutorial State")]
        [SerializeField] private TutorialPhase currentPhase = TutorialPhase.InternalMachines;
        public TutorialPhase CurrentPhase => currentPhase;

        [Header("Machines Checklist")]
        public List<MachineUIType> pendingMachines = new List<MachineUIType>
        {
            MachineUIType.FuelConverter,
            MachineUIType.OxygenPump,
            MachineUIType.CoolantGame,
            MachineUIType.PressureGame,
            MachineUIType.WaterPump
        };

        private readonly HashSet<MachineUIType> completedMachines = new HashSet<MachineUIType>();

        [Header("Scene References")]
        [SerializeField] private GameObject sonarMachineObject;
        [SerializeField] private GameObject tutorialObstaclePrefab;
        [SerializeField] private Transform obstacleSpawnPoint;
        [SerializeField] private GameObject tutorialExitPrefab;
        [SerializeField] private Transform exitSpawnPoint;

        [Header("Tutorial Water Settings")]
        [SerializeField] private float tutorialPumpRoomWater = 75f;
        [SerializeField] private float tutorialBallastWater = 50f;

        private RoomMarker pumpRoomMarker;

        public event Action<MachineUIType> OnMachineTaskCompleted;
        public event Action<TutorialPhase> OnPhaseChanged;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            DisableProceduralMapGenerators();
        }

        private void Start()
        {
            EnsureLocalNetworkHost().Forget();
        }

        private void Update()
        {
            if (!IsMachineCompleted(MachineUIType.WaterPump))
            {
                EnsurePumpRoomHasWater();
            }

            if (SubmarineManager.Instance != null && SubmarineManager.Instance.isTutorialMode)
            {
                if (Mathf.Abs(SubmarineManager.Instance.ballastWater.Value - 50f) > 0.1f)
                {
                    SubmarineManager.Instance.ballastWater.Value = 50f;
                }
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void DisableProceduralMapGenerators()
        {
            var navGame = FindFirstObjectByType<NavigationGameManager>();
            if (navGame != null)
            {
                navGame.enabled = false;
                Debug.Log("[TutorialManager] Disabled NavigationGameManager script for tutorial.");
            }

            var mapTest = FindFirstObjectByType<MapTestScript>();
            if (mapTest != null)
            {
                mapTest.enabled = false;
                Debug.Log("[TutorialManager] Disabled MapTestScript for tutorial.");
            }

            var mapGen = FindFirstObjectByType<MapGenerate>();
            if (mapGen != null)
            {
                mapGen.enabled = false;
                Debug.Log("[TutorialManager] Disabled MapGenerate for tutorial.");
            }
        }

        private async UniTaskVoid EnsureLocalNetworkHost()
        {
            await UniTask.Yield();

            if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsListening)
            {
                Debug.Log("[TutorialManager] Starting local Host mode for single-player tutorial.");
                NetworkManager.Singleton.StartHost();
            }

            // Wait for SubmarineManager and rooms to be ready
            await UniTask.Delay(500);
            InitializeTutorialSubmarine();
        }

        private void InitializeTutorialSubmarine()
        {
            if (SubmarineManager.Instance != null)
            {
                SubmarineManager.Instance.isTutorialMode = true;
                SubmarineManager.Instance.submarineOxygen.Value = 100f;
                SubmarineManager.Instance.submarineTemperature.Value = 25f;
                SubmarineManager.Instance.submarinePressure.Value = 0f;
                SubmarineManager.Instance.ballastWater.Value = tutorialBallastWater;

                foreach (var room in SubmarineManager.Instance.allRooms)
                {
                    if (room != null)
                    {
                        room.currentTemp.Value = 25f;
                    }
                }
            }

            if (FuelSystemManager.Instance != null)
            {
                FuelSystemManager.Instance.currentFuelLevel.Value = 100f;
            }

            FindAndFillPumpRoom();
        }

        private void FindAndFillPumpRoom()
        {
            if (pumpRoomMarker == null)
            {
                var rooms = FindObjectsByType<RoomMarker>(FindObjectsSortMode.None);
                foreach (var r in rooms)
                {
                    if (r.gameObject.name.IndexOf("Pump", StringComparison.OrdinalIgnoreCase) >= 0 && !r.isBallastTank)
                    {
                        pumpRoomMarker = r;
                        break;
                    }
                }
            }

            if (pumpRoomMarker != null)
            {
                pumpRoomMarker.currentWater.Value = tutorialPumpRoomWater;
                Debug.Log($"[TutorialManager] Filled {pumpRoomMarker.gameObject.name} with {tutorialPumpRoomWater}L water for tutorial.");
            }
        }

        private bool pumpRoomSearchFailed = false;

        private void EnsurePumpRoomHasWater()
        {
            if (pumpRoomMarker == null)
            {
                if (pumpRoomSearchFailed) return;
                FindAndFillPumpRoom();
                if (pumpRoomMarker == null) pumpRoomSearchFailed = true;
                return;
            }

            if (pumpRoomMarker.currentWater.Value <= 5f && !IsMachineCompleted(MachineUIType.WaterPump))
            {
                pumpRoomMarker.currentWater.Value = tutorialPumpRoomWater;
            }
        }

        public bool IsMachineCompleted(MachineUIType type)
        {
            return completedMachines.Contains(type);
        }

        public void ReportMachineCompleted(MachineUIType type)
        {
            if (completedMachines.Contains(type)) return;

            completedMachines.Add(type);
            Debug.Log($"[TutorialManager] Machine task completed: {type}");
            OnMachineTaskCompleted?.Invoke(type);

            if (currentPhase == TutorialPhase.InternalMachines && AreAllInternalMachinesCompleted())
            {
                AdvanceToSonarPhase();
            }
            else if (currentPhase == TutorialPhase.SonarStation && type == MachineUIType.MapNavigation)
            {
                AdvanceToExitPhase();
            }
        }

        public bool AreAllInternalMachinesCompleted()
        {
            foreach (var m in pendingMachines)
            {
                if (!completedMachines.Contains(m)) return false;
            }
            return true;
        }

        public void AdvanceToSonarPhase()
        {
            currentPhase = TutorialPhase.SonarStation;
            Debug.Log("[TutorialManager] All internal machines complete! Advancing to Sonar Navigation phase.");
            OnPhaseChanged?.Invoke(currentPhase);

            Transform mapMovementT = FindFirstObjectByType<MapNetworkMovement>()?.transform;

            // Spawn obstacle in map path for practice
            if (tutorialObstaclePrefab != null)
            {
                Vector3 spawnPos = obstacleSpawnPoint != null ? obstacleSpawnPoint.position : new Vector3(35f, 0f, 0f);
                Quaternion spawnRot = obstacleSpawnPoint != null ? obstacleSpawnPoint.rotation : Quaternion.identity;
                GameObject obs = Instantiate(tutorialObstaclePrefab, spawnPos, spawnRot, mapMovementT);
                int obsLayer = LayerMask.NameToLayer("MapObstacle");
                if (obsLayer >= 0)
                {
                    obs.layer = obsLayer;
                    foreach (Transform child in obs.transform) child.gameObject.layer = obsLayer;
                }
            }
        }

        public void AdvanceToExitPhase()
        {
            currentPhase = TutorialPhase.SteerToExit;
            Debug.Log("[TutorialManager] Sonar tutorial complete! Advancing to Exit phase.");
            OnPhaseChanged?.Invoke(currentPhase);

            Transform mapMovementT = FindFirstObjectByType<MapNetworkMovement>()?.transform;

            // Spawn Exit Point ahead of ship
            if (tutorialExitPrefab != null)
            {
                Vector3 spawnPos = exitSpawnPoint != null ? exitSpawnPoint.position : new Vector3(60f, 0f, 0f);
                Quaternion spawnRot = exitSpawnPoint != null ? exitSpawnPoint.rotation : Quaternion.identity;
                GameObject exitObj = Instantiate(tutorialExitPrefab, spawnPos, spawnRot, mapMovementT);
                
                var waypoint = exitObj.GetComponent<RadarWaypoint>();
                if (waypoint == null) waypoint = exitObj.AddComponent<RadarWaypoint>();
                waypoint.Setup("EXIT BEACON");
            }
        }

        public void FinishTutorial()
        {
            currentPhase = TutorialPhase.Complete;
            Debug.Log("[TutorialManager] Tutorial completed successfully!");
            OnPhaseChanged?.Invoke(currentPhase);
        }
    }
}
