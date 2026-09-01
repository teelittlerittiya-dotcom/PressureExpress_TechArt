using UnityEngine;
using UnityEngine.UI;
using System.Collections;

[RequireComponent(typeof(Image))]
public class SonarBlip : MonoBehaviour
{
    private SonarUIController poolManager;
    private Image blipImage;
    private Color originalColor;
    private bool isActiveBlip = false;

    private void Awake()
    {
        blipImage = GetComponent<Image>();
        originalColor = blipImage.color;
    }

    public void Initialize(SonarUIController manager, float displayTime, float delayTime)
    {
        poolManager = manager;
        isActiveBlip = true; 

        blipImage.color = new Color(originalColor.r, originalColor.g, originalColor.b, 0f);
        StartCoroutine(WaitAndFade(displayTime, delayTime));
    }

    private IEnumerator WaitAndFade(float duration, float delay)
    {
        yield return new WaitForSeconds(delay);

        blipImage.color = originalColor;
        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            float alpha = Mathf.Lerp(originalColor.a, 0f, timer / duration);
            blipImage.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
            yield return null;
        }
        ReturnToPool();
    }
    private void OnDisable()
    {
        if (isActiveBlip)
        {
            ReturnToPool();
        }
    }

    private void ReturnToPool()
    {
        if (!isActiveBlip) return;

        isActiveBlip = false;
        blipImage.color = new Color(originalColor.r, originalColor.g, originalColor.b, 0f);

        if (poolManager != null)
        {
            poolManager.ReturnBlipToPool(this.gameObject);
        }
    }
}