using UnityEngine;
using System.Collections.Generic;

public class SonarWaypointUIController : MonoBehaviour
{
    [SerializeField] private Transform playerTransform;
    [SerializeField] private RectTransform radarUIPanel;
    [SerializeField] private WaypointUIElement waypointUIPrefab;
    [SerializeField] private float radarWorldRadius = 50f;

    [Header("UI Settings")]
    [SerializeField] private float edgePadding = 20f;

    private List<WaypointUIElement> uiPool = new List<WaypointUIElement>();

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
    }

    private void ResolveReferences()
    {
        if (playerTransform == null)
        {
            AdvancedSonarSystem sonar = UnityEngine.Object.FindFirstObjectByType<AdvancedSonarSystem>(FindObjectsInactive.Include);
            if (sonar != null)
            {
                playerTransform = sonar.transform;
                radarWorldRadius = sonar.activeRadius;
            }
            else
            {
                SubmarineCollision sub = UnityEngine.Object.FindFirstObjectByType<SubmarineCollision>(FindObjectsInactive.Include);
                if (sub != null)
                {
                    playerTransform = sub.transform;
                }
                else if (SubmarineManager.Instance != null)
                {
                    playerTransform = SubmarineManager.Instance.transform;
                }
                else
                {
                    GameObject playerObj = GameObject.FindWithTag("Player");
                    if (playerObj != null) playerTransform = playerObj.transform;
                }
            }
        }

        if (radarUIPanel == null)
        {
            SonarUIController sonarUI = GetComponentInParent<SonarUIController>();
            if (sonarUI == null) sonarUI = GetComponentInChildren<SonarUIController>();
            if (sonarUI != null && sonarUI.radarUIPanel != null)
            {
                radarUIPanel = sonarUI.radarUIPanel;
            }
            else
            {
                radarUIPanel = GetComponent<RectTransform>();
            }
        }
    }

    private float retryTimer = 0f;

    private void Update()
    {
        if (playerTransform == null || radarUIPanel == null)
        {
            retryTimer += Time.deltaTime;
            if (retryTimer >= 1f)
            {
                retryTimer = 0f;
                ResolveReferences();
            }
            if (playerTransform == null || radarUIPanel == null) return;
        }

        UpdateWaypoints();
    }

    private void UpdateWaypoints()
    {
        if (radarUIPanel == null || waypointUIPrefab == null) return;

        RadarWaypoint.ActiveWaypoints.RemoveAll(w => w == null);
        int waypointCount = RadarWaypoint.ActiveWaypoints.Count;

        while (uiPool.Count < waypointCount)
        {
            WaypointUIElement newUI = Instantiate(waypointUIPrefab, radarUIPanel);
            uiPool.Add(newUI);
        }

        float halfWidth = (radarUIPanel.rect.width / 2f) - edgePadding;
        float halfHeight = (radarUIPanel.rect.height / 2f) - edgePadding;
        if (halfWidth <= 0f || halfHeight <= 0f) return;
        float maxUIRadius = Mathf.Min(radarUIPanel.rect.width, radarUIPanel.rect.height) / 2f;

        for (int i = 0; i < uiPool.Count; i++)
        {
            if (i < waypointCount)
            {
                RadarWaypoint target = RadarWaypoint.ActiveWaypoints[i];
                if (target == null)
                {
                    uiPool[i].gameObject.SetActive(false);
                    continue;
                }

                uiPool[i].gameObject.SetActive(true);

                Vector3 localOffset = playerTransform.InverseTransformPoint(target.transform.position);
                Vector2 worldOffset = new Vector2(localOffset.x, localOffset.y);
                float actualDistance = worldOffset.magnitude;

                float uiScaleFactor = maxUIRadius / (radarWorldRadius > 0f ? radarWorldRadius : 50f);
                Vector2 rawUIPosition = worldOffset * uiScaleFactor;

                bool isClamped = false;
                if (Mathf.Abs(rawUIPosition.x) > halfWidth || Mathf.Abs(rawUIPosition.y) > halfHeight)
                {
                    isClamped = true;

                    float overflowMax = Mathf.Max(Mathf.Abs(rawUIPosition.x) / halfWidth, Mathf.Abs(rawUIPosition.y) / halfHeight);
                    rawUIPosition /= overflowMax;
                }

                uiPool[i].UpdateUI(rawUIPosition, target.WaypointName, actualDistance, isClamped);
            }
            else
            {
                uiPool[i].gameObject.SetActive(false);
            }
        }
    }
}