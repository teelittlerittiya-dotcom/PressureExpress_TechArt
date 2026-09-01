using UnityEngine;

/// <summary>
/// Owns the padded Cargo hover volume generated from CargoItemData. Local pointer hover is
/// resolved by CargoGrabController so a reach-clamped Hand cannot reveal UI from far away.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public sealed class CargoProximitySensor : MonoBehaviour
{
    [SerializeField] private CargoController cargo;

    public CargoController Cargo => cargo;

    public void Configure(CargoController owner)
    {
        cargo = owner;
    }

    private void Awake()
    {
        if (cargo == null) cargo = GetComponentInParent<CargoController>();
        BoxCollider trigger = GetComponent<BoxCollider>();
        trigger.isTrigger = true;
    }
}
