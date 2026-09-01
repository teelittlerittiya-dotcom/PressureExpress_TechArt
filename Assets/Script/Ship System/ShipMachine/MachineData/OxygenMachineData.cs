using UnityEngine;

[CreateAssetMenu(fileName = "New Machine Data", menuName = "Data/Machine/Oxygen Machine Data")]
public class OxygenMachineData : MachineData
{
    public override void UpgradeMachine()
    {
        base.UpgradeMachine();

        ResourceManager.Instance.maxOxygen.Value = 50f + (machineLevel * 50f);
    }

    // public override void Interact()
    //     base.Interact();
}
