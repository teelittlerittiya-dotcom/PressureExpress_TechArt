using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Serialization;

public class MachinePrefabs : InteractablePrefab
{
    [SerializeField] MachineUI machineUI; //ชั่วคราว เดี๋ยวย้ายไปไว้ที่ singleton later na #############
    public MachineData machineData;
    protected override void OnEnable()
    {
        base.OnEnable();
        if(machineData!=null) Init(machineData);
    }


    public void Init(MachineData newMachineData)
    {
        ToggleInfoUI(false);
        
        machineData = newMachineData.Clone();
        if (interactableUI == null)
        {
            interactableUI = Instantiate(machineUI, this.transform.position, quaternion.identity);
            //fix this
            interactableUI.GetComponent<MachineUI>().Init(machineData, this.transform, UI_YOffset);
            interactableUI.transform.localScale = new Vector2(UI_Scale, UI_Scale);
            interactableUI.UpdateUI();
        }

    }

    public override void ToggleInfoUI(bool setActive)
    {
        base.ToggleInfoUI(setActive);
    }
}

