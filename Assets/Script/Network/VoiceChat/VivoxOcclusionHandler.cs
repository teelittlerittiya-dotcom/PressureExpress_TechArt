using Unity.Netcode;
using Unity.Services.Vivox;
using UnityEngine;

public class VivoxOcclusionHandler : MonoBehaviour
{
    private Transform listenerTransform;
    private AudioLowPassFilter lowPassFilter;

    public LayerMask obstacleLayer;

    public float occludedCutoffFrequency = 2500f;
    private float defaultCutoffFrequency = 22000f;

    private VivoxParticipant _participant; 
    private SpriteRenderer _spriteRenderer; 

    [System.Obsolete]
    void Start()
    {
        lowPassFilter = GetComponent<AudioLowPassFilter>();
        if (lowPassFilter == null)
        {
            this.enabled = false;
            return;
        }

        var localPlayer = FindLocalPlayerObject();
        if (localPlayer != null)
        {
            listenerTransform = localPlayer.transform;
        }
        _spriteRenderer = GetComponent<SpriteRenderer>();
        var playerId = GetComponent<PlayerVoiceController>().VivoxPlayerId.Value.ToString();
    }

    [Header("Underwater Voice Muffling")]
    public float underwaterCutoffFrequency = 550f;
    public float lerpSpeed = 8f;

    [System.Obsolete]
    void Update()
    {
        if (listenerTransform == null)
        {
            if (NetworkManager.Singleton != null &&
                NetworkManager.Singleton.LocalClient != null &&
                NetworkManager.Singleton.LocalClient.PlayerObject != null)
            {
                listenerTransform = NetworkManager.Singleton.LocalClient.PlayerObject.transform;
            }
            return;
        }

        if (lowPassFilter == null) return;

        Vector3 direction = (listenerTransform.position - transform.position).normalized;
        float distance = Vector3.Distance(transform.position, listenerTransform.position);

        RaycastHit hit;
        bool didHit = Physics.Raycast(transform.position, direction, out hit, distance, obstacleLayer);

        // Check if speaker or listener is submerged underwater
        bool speakerUnderwater = RoomWaterVisualizer.TryGetWaterSurfaceY(transform.position, out float speakerWaterY, out _) && (transform.position.y <= speakerWaterY);
        bool listenerUnderwater = RoomWaterVisualizer.TryGetWaterSurfaceY(listenerTransform.position, out float listenerWaterY, out _) && (listenerTransform.position.y <= listenerWaterY);

        float targetCutoff = didHit ? occludedCutoffFrequency : defaultCutoffFrequency;

        if (speakerUnderwater || listenerUnderwater)
        {
            float speakerDepth = speakerUnderwater ? (speakerWaterY - transform.position.y) : 0f;
            float listenerDepth = listenerUnderwater ? (listenerWaterY - listenerTransform.position.y) : 0f;
            float depthT = Mathf.Clamp01(Mathf.Max(speakerDepth, listenerDepth) / 3.0f);
            float waterCutoff = Mathf.Lerp(3500f, 1200f, depthT);
            targetCutoff = Mathf.Min(targetCutoff, waterCutoff);
        }

        lowPassFilter.cutoffFrequency = Mathf.Lerp(lowPassFilter.cutoffFrequency, targetCutoff, Time.deltaTime * lerpSpeed);
    }

    [System.Obsolete]
    private GameObject FindLocalPlayerObject()
    {
        var players = FindObjectsOfType<PlayerVoiceController>();
        foreach (var player in players)
        {
            if (player.IsOwner)
            {
                return player.gameObject;
            }
        }
        return null;
    }
}