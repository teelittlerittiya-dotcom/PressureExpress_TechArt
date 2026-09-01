using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;


using PressureExpress.Framework;

public class SpatialAudioManager : MonoBehaviour, IUpdateable
{
    public static SpatialAudioManager Instance { get; private set; }

    // ─── Configuration ────────────────────────────────────────────────
    [Header("Distance Attenuation")]
    [Tooltip("Distance at which the SFX is at full volume.")]
    [SerializeField] private float minDistance = 2f;

    [Tooltip("Distance at which the SFX fades to zero.")]
    [SerializeField] private float maxDistance = 25f;

    [Tooltip("Rolloff curve between minDistance and maxDistance.\n" +
             "1 = linear, 2 = quadratic (more realistic), etc.")]
    [SerializeField] private float rolloffExponent = 2f;

    [Header("Occlusion")]
    [Tooltip("Volume multiplier when the sound passes through a closed door.\n" +
             "0 = fully blocked, 0.1 = heavily muffled.")]
    [SerializeField, Range(0f, 1f)] private float closedDoorAttenuation = 0f;

    [Tooltip("Low-pass cutoff frequency applied to occluded sources.\n" +
             "Lower values = more muffled.  Set to 22000 to disable.")]
    [SerializeField] private float occludedLowPassCutoff = 350f;

    [Tooltip("Low-pass cutoff for non-occluded (clear) sources.")]
    [SerializeField] private float clearLowPassCutoff = 22000f;

    [Tooltip("How quickly the low-pass filter lerps to its target (per second).")]
    [SerializeField] private float lowPassLerpSpeed = 8f;

    [Header("Performance")]
    [Tooltip("Seconds between spatial audio updates. Lower = smoother but costlier.")]
    [SerializeField] private float updateInterval = 0.1f;

    // ─── Runtime State ────────────────────────────────────────────────
    private readonly List<SFXSource> registeredSources = new List<SFXSource>();
    private Transform localPlayerTransform;
    private float updateTimer;

    // Cached references from SubmarineManager to avoid per-frame lookups.
    private List<RoomMarker> allRooms;
    private List<DoorConnection> allDoors;
    private bool roomGraphReady;

    // BFS helpers — reused every update to avoid GC.
    private readonly HashSet<RoomMarker> visited = new HashSet<RoomMarker>();
    private readonly Queue<RoomMarker> frontier = new Queue<RoomMarker>();
    private readonly Dictionary<RoomMarker, int> closedDoorsOnPath = new Dictionary<RoomMarker, int>();

    // Lookup: room → doors that touch it.
    private readonly Dictionary<RoomMarker, List<DoorConnection>> roomToDoors =
        new Dictionary<RoomMarker, List<DoorConnection>>();

    // ═══════════════════════════════════════════════════════════════════
    // Lifecycle
    // ═══════════════════════════════════════════════════════════════════

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    private void Start()
    {
        UpdateManager.Instance.RegisterUpdateable(this);
    }

    public void OnUpdate()
    {
        // Lazy-find the local player once it exists.
        if (localPlayerTransform == null)
        {
            TryFindLocalPlayer();
            return; // Nothing to process yet.
        }

        // Lazy-build the room graph once SubmarineManager is ready.
        if (!roomGraphReady)
        {
            TryBuildRoomGraph();
        }

        updateTimer -= Time.deltaTime;
        if (updateTimer <= 0f)
        {
            updateTimer = updateInterval;
            UpdateAllSources();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (UpdateManager.Instance != null)
        {
            UpdateManager.Instance.UnregisterUpdateable(this);
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // Registration (called by SFXSource)
    // ═══════════════════════════════════════════════════════════════════

    public void Register(SFXSource source)
    {
        if (!registeredSources.Contains(source))
            registeredSources.Add(source);
    }

    public void Unregister(SFXSource source)
    {
        registeredSources.Remove(source);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Initialisation helpers
    // ═══════════════════════════════════════════════════════════════════

    private void TryFindLocalPlayer()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null && NetworkManager.Singleton.LocalClient.PlayerObject != null)
        {
            localPlayerTransform = NetworkManager.Singleton.LocalClient.PlayerObject.transform;
            return;
        }

        foreach (var netObj in FindObjectsByType<NetworkObject>(FindObjectsSortMode.None))
        {
            if (netObj != null && netObj.IsOwner && netObj.CompareTag("Player"))
            {
                localPlayerTransform = netObj.transform;
                return;
            }
        }
    }

    private void TryBuildRoomGraph()
    {
        if (SubmarineManager.Instance == null) return;

        allRooms = SubmarineManager.Instance.allRooms;
        allDoors = new List<DoorConnection>(
            FindObjectsByType<DoorConnection>(FindObjectsSortMode.None));

        if (allRooms == null || allRooms.Count == 0 || allDoors.Count == 0)
            return;

        // Build adjacency: room → list of doors.
        roomToDoors.Clear();
        foreach (var door in allDoors)
        {
            if (door.roomA != null)
            {
                if (!roomToDoors.ContainsKey(door.roomA))
                    roomToDoors[door.roomA] = new List<DoorConnection>();
                roomToDoors[door.roomA].Add(door);
            }
            if (door.roomB != null)
            {
                if (!roomToDoors.ContainsKey(door.roomB))
                    roomToDoors[door.roomB] = new List<DoorConnection>();
                roomToDoors[door.roomB].Add(door);
            }
        }

        roomGraphReady = true;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Per-update processing
    // ═══════════════════════════════════════════════════════════════════

    private void UpdateAllSources()
    {
        if (localPlayerTransform == null) return;

        Vector3 listenerPos = localPlayerTransform.position;

        for (int i = registeredSources.Count - 1; i >= 0; i--)
        {
            SFXSource src = registeredSources[i];
            if (src == null) { registeredSources.RemoveAt(i); continue; }

            Vector3 srcPos = src.transform.position;
            float distance = Vector3.Distance(listenerPos, srcPos);

            // --- Distance attenuation ---
            float distVolume = CalculateDistanceVolume(distance);

            // --- Occlusion ---
            float occlusionMultiplier = 1f;
            bool isOccluded = false;

            if (roomGraphReady)
            {
                OcclusionResult occlusion = EvaluateOcclusion(listenerPos, srcPos);
                occlusionMultiplier = occlusion.volumeMultiplier;
                isOccluded = occlusion.isOccluded;
            }

            // Apply final volume.
            float finalVolume = src.BaseVolume * distVolume * occlusionMultiplier;
            src.ApplyVolume(finalVolume);

            // Apply low-pass filter for muffling effect (occlusion + underwater depth checks)
            bool srcUnderwater = RoomWaterVisualizer.TryGetWaterSurfaceY(srcPos, out float srcSurfaceY, out _) && (srcPos.y <= srcSurfaceY);
            bool listenerUnderwater = RoomWaterVisualizer.TryGetWaterSurfaceY(listenerPos, out float listenerSurfaceY, out _) && (listenerPos.y <= listenerSurfaceY);

            float targetCutoff = isOccluded ? occludedLowPassCutoff : clearLowPassCutoff;
            if (srcUnderwater || listenerUnderwater)
            {
                float srcDepth = srcUnderwater ? (srcSurfaceY - srcPos.y) : 0f;
                float listenerDepth = listenerUnderwater ? (listenerSurfaceY - listenerPos.y) : 0f;
                float depthT = Mathf.Clamp01(Mathf.Max(srcDepth, listenerDepth) / 3.0f);
                float waterCutoff = Mathf.Lerp(3500f, 1200f, depthT);
                targetCutoff = Mathf.Min(targetCutoff, waterCutoff);
            }

            src.ApplyLowPass(targetCutoff, lowPassLerpSpeed);
        }
    }

    /// <summary>
    /// Attempt to play a one-shot SFX at a world position with full spatial
    /// processing.  Useful for fire-and-forget sounds (e.g. impacts).
    /// </summary>
    public void PlaySFXAtPosition(AudioClip clip, Vector3 worldPos, float volume = 1f)
    {
        if (clip == null) return;

        // Create a temporary GameObject with SFXSource.
        GameObject go = new GameObject("SFX_OneShot_" + clip.name);
        go.transform.position = worldPos;

        AudioSource audioSource = go.AddComponent<AudioSource>();
        audioSource.clip = clip;
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f; // We handle spatialization ourselves.

        SFXSource sfx = go.AddComponent<SFXSource>();
        sfx.SetBaseVolume(volume);

        audioSource.Play();

        // Self-destruct after clip finishes.
        Destroy(go, clip.length + 0.1f);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Distance Attenuation
    // ═══════════════════════════════════════════════════════════════════

    private float CalculateDistanceVolume(float distance)
    {
        if (distance <= minDistance) return 1f;
        if (distance >= maxDistance) return 0f;

        // Normalise to [0, 1] then apply rolloff curve.
        float t = (distance - minDistance) / (maxDistance - minDistance);
        return Mathf.Pow(1f - t, rolloffExponent);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Room / Door Occlusion
    // ═══════════════════════════════════════════════════════════════════

    private struct OcclusionResult
    {
        public float volumeMultiplier;
        public bool isOccluded;
    }

    /// <summary>
    /// Determine how much a sound is occluded.
    ///
    /// Algorithm:
    ///   1. Find the room the listener is in and the room the source is in.
    ///   2. If they are in the same room → no occlusion.
    ///   3. BFS from the listener's room through open doors.
    ///      Track the number of *closed* doors on the best path.
    ///   4. Each closed door on the path multiplies the volume by
    ///      <see cref="closedDoorAttenuation"/>.
    ///   5. If the source's room is unreachable → full occlusion.
    /// </summary>
    private OcclusionResult EvaluateOcclusion(Vector3 listenerPos, Vector3 sourcePos)
    {
        RoomMarker listenerRoom = GetRoomAtPosition(listenerPos);
        RoomMarker sourceRoom = GetRoomAtPosition(sourcePos);

        // If either position isn't inside a known room, fall back to no occlusion
        // (distance attenuation alone will handle it).
        if (listenerRoom == null || sourceRoom == null)
            return new OcclusionResult { volumeMultiplier = 1f, isOccluded = false };

        // Same room — fully audible.
        if (listenerRoom == sourceRoom)
            return new OcclusionResult { volumeMultiplier = 1f, isOccluded = false };

        // BFS through the room graph, tracking closed-door count.
        int closedDoors = BFSClosedDoorCount(listenerRoom, sourceRoom);

        if (closedDoors < 0)
        {
            // Unreachable — fully blocked.
            return new OcclusionResult { volumeMultiplier = 0f, isOccluded = true };
        }

        if (closedDoors == 0)
        {
            // Reachable through open doors only — no occlusion.
            return new OcclusionResult { volumeMultiplier = 1f, isOccluded = false };
        }

        // Partially occluded — each closed door attenuates.
        float multiplier = Mathf.Pow(closedDoorAttenuation, closedDoors);
        return new OcclusionResult
        {
            volumeMultiplier = multiplier,
            isOccluded = multiplier < 0.5f
        };
    }

    /// <summary>
    /// BFS from <paramref name="start"/> to <paramref name="goal"/>.
    /// Returns the minimum number of *closed* doors along the best path,
    /// or -1 if the goal room is unreachable.
    ///
    /// "Best" = fewest closed doors (open doors cost 0, closed doors cost 1).
    /// This is effectively a 0-1 BFS (deque-based).
    /// </summary>
    private int BFSClosedDoorCount(RoomMarker start, RoomMarker goal)
    {
        visited.Clear();
        frontier.Clear();
        closedDoorsOnPath.Clear();

        frontier.Enqueue(start);
        closedDoorsOnPath[start] = 0;

        while (frontier.Count > 0)
        {
            RoomMarker current = frontier.Dequeue();

            if (visited.Contains(current)) continue;
            visited.Add(current);

            if (current == goal)
                return closedDoorsOnPath[current];

            if (!roomToDoors.ContainsKey(current)) continue;

            foreach (var door in roomToDoors[current])
            {
                RoomMarker neighbour = door.GetOtherRoom(current);
                if (neighbour == null || visited.Contains(neighbour)) continue;

                int cost = closedDoorsOnPath[current] + (door.isOpen.Value ? 0 : 1);

                // Only update if this path is cheaper.
                if (!closedDoorsOnPath.ContainsKey(neighbour) ||
                    cost < closedDoorsOnPath[neighbour])
                {
                    closedDoorsOnPath[neighbour] = cost;
                    frontier.Enqueue(neighbour);
                }
            }
        }

        return -1; // Unreachable.
    }

    /// <summary>
    /// Determine which <see cref="RoomMarker"/> a world-space position falls inside,
    /// using room.ContainsPoint or collider bounds of each room.
    /// </summary>
    private RoomMarker GetRoomAtPosition(Vector3 worldPos)
    {
        if (allRooms == null) return null;

        foreach (var room in allRooms)
        {
            if (room == null) continue;
            if (room.ContainsPoint(worldPos))
                return room;
        }

        return null;
    }
}
