using UnityEngine;
using UnityEngine.UI;
using TMPro; // ถ้าใช้ TextMeshPro

public class MapNodeSlotUI : MonoBehaviour
{
    [Header("UI References")]
    public Image nodeIcon;
    public TextMeshProUGUI nodeName;

    private MapNode<MapData> mapNodeData;

    [SerializeField] private Sprite[] spriteMapType;
    [SerializeField] private Color32[] spriteMapTypeColor;

    public void Setup(MapNode<MapData> data)
    {
        mapNodeData = data;
        
        nodeName.text = data.Data.name;
        
        switch (data.Data.mapType)
        {
            case MapType.SpawnPoint:
                nodeIcon.color = spriteMapTypeColor[0];
                nodeIcon.sprite = spriteMapType[0];
                break;
            case MapType.Destinaton:
                nodeIcon.color = spriteMapTypeColor[1];
                nodeIcon.sprite = spriteMapType[1];
                break;
            case MapType.Treasure:
                nodeIcon.color = spriteMapTypeColor[2];
                nodeIcon.sprite = spriteMapType[2];
                break;
            case MapType.Danger:
                nodeIcon.color = spriteMapTypeColor[3];
                nodeIcon.sprite = spriteMapType[3];
                break;
            case MapType.Mystery:
                nodeIcon.color = spriteMapTypeColor[4];
                nodeIcon.sprite = spriteMapType[4];
                break;
            case MapType.Blank:
                nodeIcon.color = spriteMapTypeColor[5];
                nodeIcon.sprite = spriteMapType[5];
                break;
            default:
                nodeIcon.color = spriteMapTypeColor[0];
                nodeIcon.sprite = spriteMapType[0];
                break;
        }
    }
}