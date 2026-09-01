using UnityEngine;
using Unity.Netcode;
using Unity.Collections;
using Unity.Services.Authentication;
using Unity.Services.Vivox;
using UnityEngine.Rendering.Universal;
using Cysharp.Threading.Tasks;

public class PlayerVoiceController : NetworkBehaviour
{
    public NetworkVariable<FixedString64Bytes> VivoxPlayerId = new NetworkVariable<FixedString64Bytes>(
        default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Header("Speaking Visual")]
    [SerializeField] private PhysicsHeadLure2D physicsHeadLure;
    [Tooltip("Optional 2D speaking indicator. The custom 3D bulb light is intentionally never changed by this script.")]
    [SerializeField] private Light2D lightBulb;
    [SerializeField, Min(0f)] private float idleBulbLightRadius = 0.4f;
    [SerializeField, Min(0f)] private float speakingBulbLightRadius = 1f;

    private VivoxParticipant selfParticipant;
    private bool hasFoundParticipant = false;

    public override void OnNetworkSpawn()
    {
        VivoxPlayerId.OnValueChanged += OnPlayerIdChanged;

        if (GetComponent<VivoxOcclusionHandler>() == null)
        {
            gameObject.AddComponent<VivoxOcclusionHandler>();
        }

        if (IsOwner)
        {
            InitializeOwnerAsync().Forget();
        }
        if (!string.IsNullOrEmpty(VivoxPlayerId.Value.ToString()))
        {
            OnPlayerIdChanged("", VivoxPlayerId.Value);
        }
    }

    private async UniTaskVoid InitializeOwnerAsync()
    {
        await UniTask.WaitUntil(() => VivoxManager.Instance != null && VivoxManager.Instance.IsInitialized);
        if (AuthenticationService.Instance.IsSignedIn)
        {
            string playerId = AuthenticationService.Instance.PlayerId;
            SetVivoxPlayerIdServerRpc(playerId);
        }
    }

    public override void OnNetworkDespawn()
    {
        VivoxPlayerId.OnValueChanged -= OnPlayerIdChanged;
    }

    private void OnPlayerIdChanged(FixedString64Bytes previousValue, FixedString64Bytes newPlayerId)
    {
        gameObject.name = $"Player_{newPlayerId}";
        var vivox = VivoxManager.Instance;
        if (vivox != null && !string.IsNullOrEmpty(newPlayerId.ToString()))
        {
            vivox.ClaimPlayerObject(newPlayerId.ToString(), this.gameObject);
            selfParticipant = vivox.GetParticipant(newPlayerId.ToString());
            if (selfParticipant != null)
            {
                hasFoundParticipant = true;
            }
        }
    }

    private void Update()
    {
        var vivox = VivoxManager.Instance;
        if (IsOwner && vivox != null && vivox.IsInChannel)
        {
            VivoxService.Instance.Set3DPosition(this.gameObject, vivox.CurrentChannelName);
        }

        if (!hasFoundParticipant)
        {
            if (vivox != null && !string.IsNullOrEmpty(VivoxPlayerId.Value.ToString()))
            {
                selfParticipant = vivox.GetParticipant(VivoxPlayerId.Value.ToString());
                if (selfParticipant != null)
                {
                    hasFoundParticipant = true;
                }
            }
            return;
        }

        if (Input.GetKeyDown(KeyCode.U))
        {
            if (selfParticipant != null && !selfParticipant.IsSelf)
            {
                selfParticipant.UnmutePlayerLocally();
                selfParticipant.SetLocalVolume(1);
            }
        }

        UpdateSpeakingVisuals();
    }

    private void UpdateSpeakingVisuals()
    {
        if (selfParticipant == null) return;

        ApplySpeakingVisualState(selfParticipant.SpeechDetected);
    }

    private void ApplySpeakingVisualState(bool isSpeaking)
    {
        if (physicsHeadLure == null)
        {
            physicsHeadLure = GetComponentInChildren<PhysicsHeadLure2D>(true);
        }

        float lightRadius = isSpeaking
            ? speakingBulbLightRadius
            : idleBulbLightRadius;
        HeadLureBulbVisual bulbVisual = physicsHeadLure != null
            ? physicsHeadLure.BulbVisual
            : null;

        bool appliedToPhysicsLure = false;
        if (bulbVisual != null)
        {
            if (bulbVisual.TwoDimensionalLight != null)
            {
                bulbVisual.TwoDimensionalLight.pointLightOuterRadius = lightRadius;
                appliedToPhysicsLure = true;
            }
        }

        if (!appliedToPhysicsLure && lightBulb != null)
        {
            lightBulb.pointLightOuterRadius = lightRadius;
        }
    }

    [Rpc(SendTo.Server)]
    private void SetVivoxPlayerIdServerRpc(string playerId)
    {
        VivoxPlayerId.Value = playerId;
    }
}
