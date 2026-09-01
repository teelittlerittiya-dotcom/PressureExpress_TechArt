using UnityEngine;

public class MapMoveController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject shipDrivePanel;

    [Header("Target Map")]
    [SerializeField] private MapNetworkMovement mapMovementScript;

    private bool isUsing = false;
    private MapNavigationMachine currentMachine;

    private void Awake()
    {
        if (mapMovementScript == null)
        {
            mapMovementScript = Object.FindFirstObjectByType<MapNetworkMovement>(FindObjectsInactive.Include);
        }
    }

    private Vector2 lastSentInput = Vector2.zero;

    private void Update()
    {
        if (isUsing && mapMovementScript != null)
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
                mapMovementScript.SubmitInputServerRpc(currentInput);
            }
        }
    }

    public void EnterDriveMode(MapNavigationMachine machine)
    {
        currentMachine = machine;
        isUsing = true;
        lastSentInput = Vector2.zero;

        if (mapMovementScript == null)
        {
            mapMovementScript = Object.FindFirstObjectByType<MapNetworkMovement>(FindObjectsInactive.Include);
        }

        if (shipDrivePanel != null)
        {
            shipDrivePanel.SetActive(true);
        }

        if (MainCamController.instance != null)
        {
            MainCamController.instance.SetZoom(MainCamMode.ShipView);
        }
    }

    public void ExitDriveMode()
    {
        isUsing = false;
        currentMachine = null;
        lastSentInput = Vector2.zero;

        if (mapMovementScript != null)
        {
            mapMovementScript.SubmitInputServerRpc(Vector2.zero);
        }

        if (shipDrivePanel != null)
        {
            shipDrivePanel.SetActive(false);
        }

        if (MainCamController.instance != null)
        {
            MainCamController.instance.SetZoom(MainCamMode.CharacterView);
        }
    }

    public void OnUIExitButtonClicked()
    {
        if (currentMachine != null)
        {
            currentMachine.OnExitUIButtonClicked();
        }
    }
}