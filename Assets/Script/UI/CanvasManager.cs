using System;
using System.Collections.Generic;
using UnityEngine;
using PressureExpress.Framework;

public class CanvasManager : MonoBehaviour
{
    [Serializable]
    public struct MachineUIEntry
    {
        public MachineUIType uiType;
        public GameObject uiPrefab;
    }

    private static CanvasManager instance;
    public static CanvasManager Instance => instance ?? ServiceLocator.Get<CanvasManager>();

    [Header("UI Container")]
    [SerializeField] private Transform uiContainer;

    [Header("Machine UI Registry")]
    [SerializeField] private List<MachineUIEntry> machineUIPrefabs = new List<MachineUIEntry>();
    private readonly Dictionary<MachineUIType, GameObject> prefabLookup = new Dictionary<MachineUIType, GameObject>();

    [Header("UI State")]
    [SerializeField] private GameObject currentActiveUI;
    private MachineInstance activeMachine;

    [Header("Cursor Settings")]
    [SerializeField] private bool manageCursor = true;

    private void Awake()
    {
        CharacterController2D.canMove = true;

        if (instance == null)
        {
            instance = this;
            ServiceLocator.Register(this);
            InitializeLookup();
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        CursorVisibilityController.CloseUI(this);
        CharacterController2D.canMove = true;

        if (instance == this)
        {
            ServiceLocator.Unregister<CanvasManager>(this);
            instance = null;
        }
    }

    private void InitializeLookup()
    {
        prefabLookup.Clear();
        foreach (var entry in machineUIPrefabs)
        {
            if (entry.uiType != MachineUIType.None && entry.uiPrefab != null)
            {
                prefabLookup[entry.uiType] = entry.uiPrefab;
            }
        }
    }

    public GameObject CurrentActiveUI => currentActiveUI;
    public Transform UiContainer => uiContainer;

    public static event Action<MachineUIType, GameObject> OnMachineUIOpened;
    public static event Action OnMachineUIClosed;

    public GameObject OpenMachineUI(MachineUIType type, MachineInstance sourceMachine = null, Transform parentOverride = null)
    {
        if (type == MachineUIType.None) return null;

        // Lookup prefab
        if (prefabLookup.Count == 0 && machineUIPrefabs.Count > 0)
        {
            InitializeLookup();
        }

        if (!prefabLookup.TryGetValue(type, out var prefab) || prefab == null)
        {
            Debug.LogWarning($"[CanvasManager] No UI prefab registered for MachineUIType.{type}");
            return null;
        }

        GameObject uiInstance = OpenMachineUIInternal(prefab, sourceMachine, parentOverride);
        if (uiInstance != null)
        {
            OnMachineUIOpened?.Invoke(type, uiInstance);
        }
        return uiInstance;
    }

    public GameObject OpenMachineUI(GameObject uiPrefab, Transform parentOverride = null)
    {
        return OpenMachineUIInternal(uiPrefab, null, parentOverride);
    }

    private GameObject OpenMachineUIInternal(GameObject uiPrefab, MachineInstance sourceMachine, Transform parentOverride)
    {
        if (uiPrefab == null) return null;

        // Destroy previous active UI if present
        if (currentActiveUI != null)
        {
            CloseCurrentUIInternal(notifyMachine: false);
        }

        Transform targetParent = parentOverride != null ? parentOverride : uiContainer;
        if (targetParent != null)
        {
            currentActiveUI = Instantiate(uiPrefab, targetParent);
        }
        else
        {
            currentActiveUI = Instantiate(uiPrefab);
        }
        currentActiveUI.SetActive(true);

        activeMachine = sourceMachine;

        CharacterController2D.LockMovement();

        if (manageCursor)
        {
            CursorVisibilityController.OpenUI(this);
        }

        return currentActiveUI;
    }

    public void CloseMachineUI(GameObject uiInstance = null)
    {
        if (uiInstance != null && currentActiveUI != null && uiInstance != currentActiveUI) return;
        CloseCurrentUIInternal(notifyMachine: true);
    }

    public void CloseCurrentUI()
    {
        CloseCurrentUIInternal(notifyMachine: true);
    }

    private void CloseCurrentUIInternal(bool notifyMachine)
    {
        if (currentActiveUI != null)
        {
            Destroy(currentActiveUI);
            currentActiveUI = null;
            CharacterController2D.UnlockMovement();
        }

        if (manageCursor)
        {
            CursorVisibilityController.CloseUI(this);
        }

        var machineToExit = activeMachine;
        activeMachine = null;

        OnMachineUIClosed?.Invoke();

        if (notifyMachine && machineToExit != null)
        {
            machineToExit.OnExitUIButtonClicked();
        }
    }

    private void Update()
    {
        if (currentActiveUI != null && Input.GetKeyDown(KeyCode.Escape))
        {
            var minigame = currentActiveUI.GetComponentInChildren<MinigameBaseUI>();
            if (minigame != null && minigame.IsTutorialOpen)
            {
                minigame.ToggleTutorial();
                return;
            }
            CloseCurrentUI();
        }
    }
}
