using UnityEngine;
using UnityEngine.UI;
using PressureExpress.Framework;

public class ShipDriveMinigameUI : MinigameBaseUI, IUpdateable
{
    [Header("Machine Binding")]
    public MapNavigationMachine machine;

    [Header("Map Movement Target")]
    public MapNetworkMovement mapMovement;

    [Header("Sonar System Binding")]
    public AdvancedSonarSystem sonarSystem;
    public SonarUIController sonarUI;

    [Header("Map Node Display Binding")]
    public MapUIDisplayManager mapUIDisplay;

    [Header("Panel Navigation")]
    public GameObject sonarPanel;
    public GameObject mapPanel;
    public Button toggleViewButton;
    public TMPro.TextMeshProUGUI toggleButtonText;

    private bool isShowingMap = false;

    [Header("Extra UI Controls")]
    public Button exitButton;

    protected override void Awake()
    {
        base.Awake();
        ValidateReferences();

        if (exitButton != null)
        {
            exitButton.onClick.AddListener(OnClickClose);
        }

        if (toggleViewButton != null)
        {
            toggleViewButton.onClick.AddListener(ToggleView);
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (UpdateManager.Instance != null)
        {
            UpdateManager.Instance.RegisterUpdateable(this);
        }

        if (mapMovement == null)
        {
            mapMovement = UnityEngine.Object.FindFirstObjectByType<MapNetworkMovement>(FindObjectsInactive.Include);
        }

        if (sonarSystem == null)
        {
            sonarSystem = UnityEngine.Object.FindFirstObjectByType<AdvancedSonarSystem>(FindObjectsInactive.Include);
        }

        if (sonarUI == null)
        {
            sonarUI = GetComponentInChildren<SonarUIController>(true);
        }

        if (sonarSystem != null && sonarUI != null)
        {
            sonarSystem.uiController = sonarUI;
        }

        if (mapUIDisplay == null)
        {
            mapUIDisplay = GetComponentInChildren<MapUIDisplayManager>(true);
        }

        // Auto-find panels if unassigned
        if (sonarPanel == null && sonarUI != null)
        {
            sonarPanel = sonarUI.gameObject;
        }

        if (mapPanel == null && mapUIDisplay != null)
        {
            ScrollRect scroll = mapUIDisplay.GetComponentInParent<ScrollRect>();
            if (scroll != null) mapPanel = scroll.gameObject;
            else if (mapUIDisplay.layersContainer != null && mapUIDisplay.layersContainer.parent != null)
            {
                mapPanel = mapUIDisplay.layersContainer.parent.gameObject;
            }
        }

        // Auto-find toggle button if unassigned
        if (toggleViewButton == null)
        {
            Transform navT = transform.Find("[Parent] PANEL_DRIVE/[Grid] grid/[Button] BT_NAV");
            if (navT != null) toggleViewButton = navT.GetComponent<Button>();
        }

        if (toggleButtonText == null && toggleViewButton != null)
        {
            toggleButtonText = toggleViewButton.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
        }

        if (toggleViewButton != null)
        {
            toggleViewButton.onClick.RemoveListener(ToggleView);
            toggleViewButton.onClick.AddListener(ToggleView);
        }

        ShowSonarPanel();

        if (MainCamController.instance != null)
        {
            MainCamController.instance.SetZoom(MainCamMode.ShipView);
        }
    }

    public void ToggleView()
    {
        isShowingMap = !isShowingMap;
        if (isShowingMap)
        {
            ShowMapPanel();
        }
        else
        {
            ShowSonarPanel();
        }
    }

    public void ShowMapPanel()
    {
        isShowingMap = true;
        if (sonarPanel != null) sonarPanel.SetActive(false);
        if (mapPanel != null) mapPanel.SetActive(true);

        if (toggleButtonText != null)
        {
            toggleButtonText.text = "RADAR";
        }

        if (mapUIDisplay != null)
        {
            if (mapUIDisplay.mapNodeManager == null)
            {
                mapUIDisplay.mapNodeManager = UnityEngine.Object.FindFirstObjectByType<MapNodeManager>(FindObjectsInactive.Include);
            }
            if (mapUIDisplay.mapNodeManager != null)
            {
                mapUIDisplay.DisplayMap();
            }
        }
    }

    public void ShowSonarPanel()
    {
        isShowingMap = false;
        if (mapPanel != null) mapPanel.SetActive(false);
        if (sonarPanel != null) sonarPanel.SetActive(true);

        if (toggleButtonText != null)
        {
            toggleButtonText.text = "MAP";
        }
    }

    private void OnDisable()
    {
        if (UpdateManager.Instance != null)
        {
            UpdateManager.Instance.UnregisterUpdateable(this);
        }

        if (sonarSystem != null && sonarSystem.uiController == sonarUI)
        {
            sonarSystem.uiController = null;
        }

        if (mapMovement != null)
        {
            mapMovement.SubmitInputServerRpc(Vector2.zero);
        }
        lastSentInput = Vector2.zero;

        if (MainCamController.instance != null)
        {
            MainCamController.instance.SetZoom(MainCamMode.CharacterView);
        }
    }

    private Vector2 lastSentInput = Vector2.zero;

    public void OnUpdate()
    {
        if (IsTutorialOpen)
        {
            if (mapMovement != null && lastSentInput != Vector2.zero)
            {
                lastSentInput = Vector2.zero;
                mapMovement.SubmitInputServerRpc(Vector2.zero);
            }
            return;
        }

        if (mapMovement != null)
        {
            float inputX = 0f;
            float inputY = 0f;

            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) inputY += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) inputY -= 1f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) inputX -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) inputX += 1f;

            Vector2 currentInput = new Vector2(inputX, inputY);
            if (currentInput != lastSentInput)
            {
                lastSentInput = currentInput;
                mapMovement.SubmitInputServerRpc(currentInput);

                if (currentInput != Vector2.zero)
                {
                    PressureExpress.Tutorial.TutorialManager.Instance?.ReportMachineCompleted(MachineUIType.MapNavigation);
                }
            }
        }
    }

    protected override void OnMinigameUpdate() { }

    public void OnClickClose()
    {
        if (machine != null)
        {
            machine.OnExitUIButtonClicked();
        }
        else
        {
            RequestClose();
        }
    }

    private void ValidateReferences()
    {
        if (closeButton == null && exitButton == null)
            Debug.LogWarning($"[{nameof(ShipDriveMinigameUI)}] No close/exit button bound on '{gameObject.name}'.", this);
    }
}
