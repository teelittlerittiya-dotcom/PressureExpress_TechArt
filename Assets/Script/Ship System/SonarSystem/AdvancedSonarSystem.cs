using Cysharp.Threading.Tasks;
using System.Threading.Tasks;
using UnityEngine;

public class AdvancedSonarSystem : MonoBehaviour
{
    [Header("Ref")]
    public SonarUIController uiController;

    [Header("Active Sonar")]
    public float activeRadius = 50f;
    public int rayCount = 360;
    public LayerMask obstacleLayer;
    public float scanDuration = 1.5f;

    public float activeScanInterval = 2.0f;

    [Header("Passive Sonar")]
    public float passiveRadius = 60f;
    public float passiveScanInterval = 0.5f;
    public LayerMask noiseLayer;

    [Header("Sonar Ping Audio")]
    [Tooltip("Sound played on each active sonar ping.")]
    [SerializeField] private AudioClip pingClip;

    [Tooltip("Volume of the sonar ping SFX.")]
    [SerializeField, Range(0f, 1f)] private float pingVolume = 0.85f;

    private Collider[] noiseHits = new Collider[20];

    private float passiveTimer = 0f;

    private bool isAutoScanning = false;
    private float activeTimer = 0f;
    private AudioSource pingAudioSource;

    private float retryTimer = 0f;

    private void Awake()
    {
        if (uiController == null)
        {
            uiController = Object.FindFirstObjectByType<SonarUIController>(FindObjectsInactive.Include);
        }
    }

    private void Start()
    {
        pingAudioSource = gameObject.AddComponent<AudioSource>();
        pingAudioSource.playOnAwake = false;
        pingAudioSource.loop = false;
        pingAudioSource.spatialBlend = 0f;
        pingAudioSource.volume = pingVolume;
    }

    private void Update()
    {
        if (uiController == null)
        {
            retryTimer += Time.deltaTime;
            if (retryTimer >= 1f)
            {
                retryTimer = 0f;
                uiController = Object.FindFirstObjectByType<SonarUIController>(FindObjectsInactive.Include);
            }
        }

        if (uiController == null || !uiController.isActiveAndEnabled) return;

        passiveTimer += Time.deltaTime;
        if (passiveTimer >= passiveScanInterval)
        {
            ListenForPassiveNoise();
            passiveTimer = 0f;
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            isAutoScanning = !isAutoScanning; 

            if (isAutoScanning)
            {
                FireActiveSonar();
                activeTimer = 0f;
            }
        }
        if (isAutoScanning)
        {
            activeTimer += Time.deltaTime;
            if (activeTimer >= activeScanInterval)
            {
               FireActiveSonar();
                activeTimer = 0f;
            }
        }
    }

    public void FireActiveSonar()
    {
        if (uiController == null) return;
        PlayPingLocal();
        uiController.PlayPingAnimation(scanDuration);
        float angleStep = 360f / rayCount;

        for (int i = 0; i < rayCount; i++)
        {
            float currentAngle = i * angleStep;
            float rad = currentAngle * Mathf.Deg2Rad;
            Vector3 direction = new Vector3(Mathf.Sin(rad), Mathf.Cos(rad), 0f);
            Debug.DrawRay(transform.position, direction * activeRadius, Color.yellow, 2f);

            // Single-hit Physics.Raycast, not RaycastNonAlloc with a 1-element buffer:
            // NonAlloc results are explicitly unordered, and "closest hit" is only
            // guaranteed when the buffer is NOT full - with length 1 it always is, so the
            // sonar could paint the far obstacle instead of the near one.
            if (Physics.Raycast(transform.position, direction, out RaycastHit hit, activeRadius, obstacleLayer))
            {
                Vector3 hitPoint = hit.point;
                float distance = Vector3.Distance(transform.position, hitPoint);
                float delayTime = (distance / activeRadius) * scanDuration;
                Debug.DrawLine(transform.position, hitPoint, Color.red, 2f);
                uiController.DrawBlip(hitPoint, transform, activeRadius, delayTime);
            }
        }
    }

    private void ListenForPassiveNoise()
    {
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, passiveRadius, noiseHits, noiseLayer, QueryTriggerInteraction.Collide);
        for (int i = 0; i < hitCount; i++)
        {
            Collider col = noiseHits[i];
            if (col != null && col.TryGetComponent(out NoiseSource source))
            {
                float distance = Vector3.Distance(transform.position, col.transform.position);
                if (distance <= source.noiseLevel)
                {
                    uiController.DrawBlip(col.transform.position, transform, passiveRadius, 0f);
                }
            }
        }
    }

    /// <summary>
    /// Plays the sonar ping clip locally (2D, no network delay).
    /// </summary>
    private void PlayPingLocal()
    {
        if (pingClip != null && pingAudioSource != null)
        {
            pingAudioSource.PlayOneShot(pingClip, pingVolume);
        }
    }
}