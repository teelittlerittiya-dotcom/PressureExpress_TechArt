using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class UIMapConnector : MonoBehaviour
{
    [Header("Line Settings")]
    public GameObject linePrefab; 
    public float lineThickness = 5f;
    public Color lineColor = Color.gray;

    private Transform lineParent; 
    private List<GameObject> activeLines = new List<GameObject>();

    public void Initialize(Transform lineRoot)
    {
        if (lineRoot == null)
        {
            Debug.LogError("Initialization failed: lineRoot (ConnectionRoot) is null.");
            return;
        }
        lineParent = lineRoot;
    }

    public void ClearAndRedraw(Dictionary<MapNode<MapData>, MapNodeSlotUI> nodeMap)
    {
        // ... (โค้ดล้างเส้นเดิม) ...
        foreach (var line in activeLines)
        {
            Destroy(line);
        }
        activeLines.Clear();

        if (lineParent == null)
        {
            Debug.LogError("Cannot draw lines: lineParent (ConnectionRoot) is null. Initialize() may have failed.");
            return;
        }
        
        if (nodeMap == null || nodeMap.Count == 0)
        {
            Debug.LogWarning("Node map is empty. No lines to draw.");
            return;
        }
        
        foreach (var kvp in nodeMap)
        {
            MapNode<MapData> parentData = kvp.Key;
            
            // 1. ตรวจสอบ RectTransform ของ Parent UI
            RectTransform parentRect = kvp.Value.GetComponent<RectTransform>();
            if (parentRect == null) 
            {
                Debug.LogWarning($"Skipping node '{parentData.Data.name}': Parent UI is missing RectTransform.");
                continue; 
            }

            foreach (var childNodeData in parentData.Children)
            {
                // 2. ตรวจสอบว่า Child UI ถูกสร้างแล้วหรือไม่ (อยู่ใน Dictionary)
                if (nodeMap.TryGetValue(childNodeData, out MapNodeSlotUI childUI))
                {
                    // 3. ตรวจสอบ RectTransform ของ Child UI
                    RectTransform childRect = childUI.GetComponent<RectTransform>();
                    if (childRect != null)
                    {
                        DrawConnection(parentRect, childRect);
                    }
                    else
                    {
                        Debug.LogWarning($"Skipping connection from '{parentData.Data.name}': Child UI ('{childNodeData.Data.name}') is missing RectTransform.");
                    }
                }
                else
                {
                    // กรณีนี้ไม่ควรเกิดถ้า MapUIDisplayManager ทำงานถูกต้อง
                    Debug.LogWarning($"Skipping connection from '{parentData.Data.name}': Child Node Data ('{childNodeData.Data.name}') UI not found in node map.");
                }
            }
        }
    }
    
    private void DrawConnection(RectTransform parentRect, RectTransform childRect)
    {
        // 4. ตรวจสอบ Line Prefab
        if (linePrefab == null)
        {
            Debug.LogError("Cannot draw line: Line Prefab is null in UIMapConnector. Assign the prefab in the Inspector!");
            return;
        }

        GameObject lineGO = Instantiate(linePrefab, lineParent);
        Image lineImage = lineGO.GetComponent<Image>();
        
        // 5. ตรวจสอบ Image Component
        if (lineImage == null)
        {
            Debug.LogError($"Instantiated line prefab '{linePrefab.name}' is missing the Image component.");
        }
        else
        {
            lineImage.color = lineColor;
        }
        
        RectTransform lineRect = lineGO.GetComponent<RectTransform>();
        if (lineRect == null)
        {
            Debug.LogError($"Instantiated line prefab '{linePrefab.name}' is missing the RectTransform component.");
            Destroy(lineGO);
            return;
        }

        activeLines.Add(lineGO);

        Vector3 startPos = parentRect.position; 
        Vector3 endPos = childRect.position; 
        
        Vector3 direction = endPos - startPos;
        float distance = direction.magnitude;

        // 6. ตรวจสอบระยะทางที่ใกล้เคียงศูนย์
        if (distance < 1f) // ถ้าน้อยกว่า 1 หน่วย (ใกล้เคียง 0 มาก)
        {
             Debug.LogWarning($"Connection distance is near zero ({distance:F2}) between '{parentRect.name}' and '{childRect.name}'. Line may not be visible.");
             // เรายังคงวาดเส้นไว้เผื่อกรณีที่ Layout ยังไม่เสร็จสมบูรณ์
        }
        
        // ... (โค้ดการคำนวณตำแหน่งและการหมุนยังคงเดิม) ...
        Vector3 lineCenterWorld = startPos + (direction / 2f);

        lineRect.position = lineCenterWorld;
        lineRect.localPosition = lineRect.parent.InverseTransformPoint(lineRect.position);

        lineRect.sizeDelta = new Vector2(lineThickness, distance);

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        
        lineRect.localRotation = Quaternion.Euler(0, 0, angle - 90f); 
        lineRect.SetAsFirstSibling();
    }
}