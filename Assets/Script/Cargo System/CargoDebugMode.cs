using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
[DefaultExecutionOrder(1000)]
public sealed class CargoDebugMode : MonoBehaviour
{
    [Header("Local Debug Toggle")]
    [FormerlySerializedAs("togglePrototypeStatusKey")]
    [SerializeField] private KeyCode toggleCargoStatusKey = KeyCode.Equals;
    [FormerlySerializedAs("prototypeStatusUIVisible")]
    [SerializeField] private bool cargoStatusUIVisible;

    private CargoController[] allCargos = new CargoController[0];

    public bool CargoStatusUIVisible => cargoStatusUIVisible;

    private void Start()
    {
        RefreshCargoTargets();
        ApplyCargoStatusUI();
    }

    private void Update()
    {
        // KeyCode.Equals is the main keyboard '=' key after number-row 0.
        // KeypadEquals is intentionally not used.
        if (!Input.GetKeyDown(toggleCargoStatusKey)) return;

        cargoStatusUIVisible = !cargoStatusUIVisible;
        RefreshCargoTargets();
        ApplyCargoStatusUI();
    }

    private void RefreshCargoTargets()
    {
        allCargos = FindObjectsByType<CargoController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.InstanceID);
    }

    private void ApplyCargoStatusUI()
    {
        foreach (CargoController cargo in allCargos)
        {
            if (cargo != null) cargo.SetLocalDebugStatusUIVisible(cargoStatusUIVisible);
        }
    }
}
