using UnityEngine;
using TMPro;

public class WaypointUIElement : MonoBehaviour
{
    [SerializeField] private RectTransform rectTransform;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI distanceText;

    private void Awake()
    {
        if (rectTransform == null) rectTransform = GetComponent<RectTransform>();
        if (nameText == null)
        {
            var texts = GetComponentsInChildren<TextMeshProUGUI>(true);
            if (texts.Length > 0) nameText = texts[0];
            if (texts.Length > 1) distanceText = texts[1];
        }
    }

    public void UpdateUI(Vector2 anchoredPosition, string name, float distance, bool isClamped)
    {
        if (rectTransform != null) rectTransform.anchoredPosition = anchoredPosition;

        if (nameText != null)
        {
            nameText.text = !string.IsNullOrEmpty(name) ? name : "EXIT BEACON";
            nameText.color = isClamped ? new Color(1f, 0.85f, 0.2f, 1f) : Color.white;
            nameText.gameObject.SetActive(true);
        }

        if (distanceText != null)
        {
            distanceText.text = $"{distance:F0}m";
            distanceText.gameObject.SetActive(true);
        }
    }
}