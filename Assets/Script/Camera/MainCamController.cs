using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public enum MainCamMode
{
    CharacterView,
    ShipView
}

[Serializable]
public struct MainCamZoomSetting
{
    public MainCamMode cameraMode;
    public float targetCamSize;
    public float xOffset; // เพิ่มตัวแปรมารับค่าใน Inspector
    public float yOffset;
}

public class MainCamController : MonoBehaviour
{
    [SerializeField] private Camera mainCam;
    [HideInInspector] public bool isControlledExternally;
    
    private float targetCamSize;
    private Vector3 targetLocalPos; 

    [SerializeField] private MainCamMode defaultCamMode = MainCamMode.CharacterView;
    [SerializeField] private float zoomSpeed = 5f;
    [SerializeField] private float moveSpeed = 5f; 
    [Space] public List<MainCamZoomSetting> zoomSettings;
    
    private float currentCamSize = 0;
    public static MainCamController instance;

    private void Awake()
    {
        if (instance == null) instance = this;
        else Destroy(this.gameObject);
    }

    private void Start()
    {
        if (mainCam == null) mainCam = Camera.main;
        if (mainCam == null) return;

        SetZoom(defaultCamMode);

        currentCamSize = targetCamSize;
        mainCam.orthographicSize = currentCamSize;
        mainCam.transform.localPosition = targetLocalPos;
    }

    private void LateUpdate()
    {
        if (mainCam == null || isControlledExternally) return;

        if (Mathf.Abs(currentCamSize - targetCamSize) > Mathf.Epsilon)
        {
            currentCamSize = Mathf.MoveTowards(currentCamSize, targetCamSize, zoomSpeed * Time.deltaTime);
            mainCam.orthographicSize = currentCamSize;
        }

        if (Vector3.Distance(mainCam.transform.localPosition, targetLocalPos) > 0.001f)
        {
            mainCam.transform.localPosition = Vector3.MoveTowards(
                mainCam.transform.localPosition, 
                targetLocalPos, 
                moveSpeed * Time.deltaTime
            );
        }
    }

    public void SetExternalControl(bool controlled)
    {
        isControlledExternally = controlled;

        if (!controlled && mainCam != null)
        {
            // Keep the preview's final camera position when normal camera control resumes.
            targetLocalPos = mainCam.transform.localPosition;
            currentCamSize = mainCam.orthographicSize;
            targetCamSize = currentCamSize;
        }
    }
    
    public void SetZoom(MainCamMode camMode)
    {
        foreach (var setting in zoomSettings)
        {
            if (setting.cameraMode == camMode)
            {
                targetCamSize = setting.targetCamSize;

                float currentZ = mainCam.transform.localPosition.z;
                targetLocalPos = new Vector3(setting.xOffset, setting.yOffset, currentZ);

                return;
            }
        }
        Debug.LogWarning($"MainCamController: not found {camMode}");
    }
}
