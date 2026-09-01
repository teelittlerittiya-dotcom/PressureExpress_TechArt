using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SonarUIController : MonoBehaviour
{
    [Header("UI References")]
    public RectTransform radarUIPanel;
    public GameObject blipPrefab;
    public RectTransform pingRingUI;
    [Header("Pool Settings")]
    public int poolSize = 500;
    public float blipDisplayTime = 2.5f;
    private Queue<GameObject> blipPool = new Queue<GameObject>();
    private void Start()
    {
        for (int i = 0; i < poolSize; i++)
        {
            GameObject blip = Instantiate(blipPrefab, radarUIPanel);
            blip.SetActive(false);
            blipPool.Enqueue(blip);
        }
    }
    public void PlayPingAnimation(float scanDuration)
    {
        if (pingRingUI != null)
        {
            StartCoroutine(AnimatePingRing(scanDuration));
        }
    }
    private IEnumerator AnimatePingRing(float duration)
    {
        float timer = 0f;
        pingRingUI.gameObject.SetActive(true);
        pingRingUI.localScale = Vector3.one;
        float maxUIRes = Mathf.Max(radarUIPanel.rect.width, radarUIPanel.rect.height);
        Vector2 targetSize = new Vector2(maxUIRes, maxUIRes);
        pingRingUI.sizeDelta = Vector2.zero;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            pingRingUI.sizeDelta = Vector2.Lerp(Vector2.zero, targetSize, timer / duration);
            yield return null;
        }
        pingRingUI.gameObject.SetActive(false);
    }
    public void DrawBlip(Vector3 worldHitPoint, Transform scannerTransform, float maxRadius, float delay)
    {
        if (blipPool.Count == 0) return;
        GameObject activeBlip = blipPool.Dequeue();
        Vector3 localPos = scannerTransform.InverseTransformPoint(worldHitPoint);
        float uiScaleFactor = Mathf.Min(radarUIPanel.rect.width, radarUIPanel.rect.height) / (maxRadius * 2f);
        Vector2 finalUIPosition = new Vector2(localPos.x, localPos.y) * uiScaleFactor;
        RectTransform blipRect = activeBlip.GetComponent<RectTransform>();
        blipRect.anchoredPosition = finalUIPosition;
        activeBlip.SetActive(true);
        activeBlip.GetComponent<SonarBlip>().Initialize(this, blipDisplayTime, delay);
    }

    public void ReturnBlipToPool(GameObject blip)
    {
        blip.SetActive(false);
        blipPool.Enqueue(blip);
    }
}