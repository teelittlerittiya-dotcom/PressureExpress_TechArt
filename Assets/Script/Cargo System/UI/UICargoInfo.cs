using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

[DefaultExecutionOrder(12000)]
[DisallowMultipleComponent]
public class UICargoInfo : MonoBehaviour
{
    private const string OverlaySortingLayer = "UI";
    private const int OverlaySortingOrder = short.MaxValue;
    private const string GraphicOverlayShaderName = "PressureExpress/UI/Cargo Overlay";
    private const string TextOverlayShaderName = "TextMeshPro/Distance Field Overlay";

    [Header("Main UI")]
    [SerializeField] private TextMeshProUGUI cargoNameText;
    [SerializeField] private Transform gridContainer; // จุดที่จะ Spawn Slot ใส่ (ต้องมี Component GridLayoutGroup)
    
    [Header("Prefabs")]
    [SerializeField] private SlotCargoModuleInfo slotPrefab; // ลาก Prefab ที่ทำข้อ 1 มาใส่

    [Header("Always-on-top rendering")]
    [SerializeField] private Shader graphicOverlayShader;
    [SerializeField] private Shader textOverlayShader;

    // เก็บรายการ Slot ที่สร้างขึ้นมา เพื่อใช้อัปเดตค่า
    private readonly List<(SlotCargoModuleInfo slot, System.Type moduleType)> activeSlots = new List<(SlotCargoModuleInfo, System.Type)>();
    private readonly List<Material> overlayMaterials = new List<Material>();
    private Canvas rootCanvas;
    private Transform worldTarget;
    private Vector3 fixedWorldOffset;
    
    // เรียกครั้งเดียวตอนเริ่ม เพื่อสร้าง UI
    public void SetupUI(CargoController controller)
    {
        if (controller == null || controller.cargoItemData == null) return;

        // 1. ตั้งชื่อ Cargo
        if (cargoNameText != null)
        {
            cargoNameText.text = controller.cargoItemData.cargoName;
        }

        // 2. เคลียร์ของเก่า (ถ้ามี)
        if (gridContainer == null) return;

        foreach (Transform child in gridContainer)
        {
            Destroy(child.gameObject);
        }
        activeSlots.Clear();

        // 3. Instantiate Slot ตามจำนวน Module
        foreach (var module in controller.cargoItemData.GetModules())
        {
            if (slotPrefab != null)
            {
                SlotCargoModuleInfo newSlot = Instantiate(slotPrefab, gridContainer);
                // เก็บ Reference ไว้คู่กับ Type ของ Module นั้นๆ
                activeSlots.Add((newSlot, module.GetModuleType()));
                
                // Init ค่าครั้งแรก
                float currentVal = controller.GetCurrentValue(module.GetModuleType());
                newSlot.UpdateSlot(module, currentVal);
            }
        }
    }

    /// <summary>
    /// Keeps the world-space status panel upright at a fixed world offset from the Cargo.
    /// Overlay materials deliberately ignore scene depth so opaque 3D geometry cannot hide it.
    /// </summary>
    public void ConfigureWorldPresentation(Transform target, Vector3 worldOffset)
    {
        worldTarget = target;
        fixedWorldOffset = worldOffset;

        rootCanvas = GetComponent<Canvas>();
        if (rootCanvas == null) rootCanvas = GetComponentInChildren<Canvas>(true);
        if (rootCanvas != null)
        {
            rootCanvas.renderMode = RenderMode.WorldSpace;
            rootCanvas.overrideSorting = true;
            rootCanvas.sortingLayerName = OverlaySortingLayer;
            rootCanvas.sortingOrder = OverlaySortingOrder;
        }

        ApplyOverlayMaterials();
        RefreshWorldPresentation();
    }

    public void RefreshWorldPresentation()
    {
        if (worldTarget == null) return;
        transform.SetPositionAndRotation(worldTarget.position + fixedWorldOffset, Quaternion.identity);
    }

    private void LateUpdate()
    {
        RefreshWorldPresentation();
    }

    private void ApplyOverlayMaterials()
    {
        ReleaseOverlayMaterials();

        Graphic[] graphics = GetComponentsInChildren<Graphic>(true);
        foreach (Graphic graphic in graphics)
        {
            TMP_Text textGraphic = graphic as TMP_Text;
            Material source = textGraphic != null ? textGraphic.fontSharedMaterial : graphic.materialForRendering;
            if (source == null) continue;

            Shader overlayShader = textGraphic != null
                ? textOverlayShader != null ? textOverlayShader : Shader.Find(TextOverlayShaderName)
                : graphicOverlayShader != null ? graphicOverlayShader : Shader.Find(GraphicOverlayShaderName);

            Material overlayMaterial = overlayShader != null ? new Material(overlayShader) : new Material(source);
            if (overlayShader != null)
            {
                overlayMaterial.CopyMatchingPropertiesFromMaterial(source);
                overlayMaterial.shaderKeywords = source.shaderKeywords;
            }

            overlayMaterial.name = $"{source.name} (Cargo UI Overlay)";
            overlayMaterial.hideFlags = HideFlags.HideAndDontSave;
            overlayMaterial.renderQueue = (int)RenderQueue.Overlay;

            SetDepthTestAlways(overlayMaterial, "unity_GUIZTestMode");
            SetDepthTestAlways(overlayMaterial, "_ZTestMode");
            SetDepthTestAlways(overlayMaterial, "_ZTest");

            if (textGraphic != null)
            {
                textGraphic.fontSharedMaterial = overlayMaterial;
            }
            else
            {
                graphic.material = overlayMaterial;
            }

            overlayMaterials.Add(overlayMaterial);
        }
    }

    private static void SetDepthTestAlways(Material material, string propertyName)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetInt(propertyName, (int)CompareFunction.Always);
        }
    }

    private void ReleaseOverlayMaterials()
    {
        foreach (Material material in overlayMaterials)
        {
            if (material == null) continue;
            if (Application.isPlaying) Destroy(material);
            else DestroyImmediate(material);
        }

        overlayMaterials.Clear();
    }

    private void OnDestroy()
    {
        ReleaseOverlayMaterials();
    }

    // เรียกทุก Frame (หรือตอนค่าเปลี่ยน) เพื่ออัปเดตหลอดเลือด
    public void UpdateUIValues(CargoController controller)
    {
        if (controller == null || controller.cargoItemData == null) return;

        foreach (var item in activeSlots)
        {
            // ดึง Module ต้นฉบับ
            var module = controller.cargoItemData.GetModules().Find(m => m.GetModuleType() == item.moduleType);
            if (module != null)
            {
                // ดึงค่าปัจจุบันจาก Controller
                float currentVal = controller.GetCurrentValue(item.moduleType);
                // สั่ง Slot อัปเดต
                item.slot.UpdateSlot(module, currentVal);
            }
        }
    }
}
