using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Vivox;
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using PressureExpress.Framework;

public class VivoxManager : MonoBehaviour
{
    private static VivoxManager instance;
    public static VivoxManager Instance => instance ?? ServiceLocator.Get<VivoxManager>();

    public string CurrentChannelName => _currentChannelName;
    public bool IsInitialized { get; private set; } = false;
    public bool IsInChannel { get; private set; } = false;

    private VivoxAudioHandler audioHandler;
    private string _currentChannelName;
    private readonly Dictionary<string, VivoxParticipant> _participants = new Dictionary<string, VivoxParticipant>();
    private readonly Dictionary<string, GameObject> _pendingPlayers = new Dictionary<string, GameObject>();

    private void Awake()
    {
        audioHandler = GetComponent<VivoxAudioHandler>();
        if (instance == null)
        {
            instance = this;
            ServiceLocator.Register(this);
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        InitializeAndLoginAsync().Forget();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            ServiceLocator.Unregister<VivoxManager>(this);
            instance = null;
        }
        UnbindVivoxEvents();
    }

    public async UniTask InitializeAndLoginAsync()
    {
        if (IsInitialized) return;
        try
        {
            // Routed through the shared bootstrap so this cannot race AnalyticManager's own
            // UnityServices initialisation, which used to run concurrently with this one.
            if (!await UnityServicesBootstrap.EnsureSignedInAsync())
            {
                Debug.LogWarning("Vivox: UnityServices sign-in unavailable, voice chat disabled.");
                IsInitialized = false;
                return;
            }

            await VivoxService.Instance.InitializeAsync();
            LoginOptions options = new LoginOptions();
            await VivoxService.Instance.LoginAsync(options);
            BindVivoxEvents();
            Debug.Log("Vivox Initialized and Logged In Successfully!");
            if (audioHandler != null)
            {
                audioHandler.InitializeDeviceUI();
            }
            IsInitialized = true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Vivox Initialization or Login Failed: {e.Message}");
            IsInitialized = false;
        }
    }

    public async UniTask JoinChannelAsync(string lobbyId)
    {
        if (!VivoxService.Instance.IsLoggedIn)
        {
            Debug.LogWarning("Cannot join channel, Vivox is not logged in.");
            return;
        }
        string channelName = $"lobby-{lobbyId}";
        _currentChannelName = channelName;
        Debug.Log($"Attempting to join Vivox positional channel: {channelName}");
        try
        {
            var positionalChannelProperties = new Channel3DProperties(
                audibleDistance: 20,
                conversationalDistance: 2,
                audioFadeIntensityByDistanceaudio: 1.0f,
                audioFadeModel: AudioFadeModel.InverseByDistance
            );
            await VivoxService.Instance.JoinPositionalChannelAsync(channelName, ChatCapability.AudioOnly, positionalChannelProperties);
            Debug.Log($"Successfully joined Vivox positional channel: {channelName}");
            IsInChannel = true;
            await VivoxService.Instance.SetChannelTransmissionModeAsync(TransmissionMode.All);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Failed to join Vivox channel {channelName}: {e.Message}");
            IsInChannel = false;
        }
    }

    public async UniTask LeaveCurrentChannelAsync()
    {
        if (string.IsNullOrEmpty(_currentChannelName))
        {
            return;
        }
        Debug.Log($"Leaving Vivox channel: {_currentChannelName}");
        IsInChannel = false;
        try
        {
            await VivoxService.Instance.LeaveChannelAsync(_currentChannelName);
            _currentChannelName = null;
            _participants.Clear();
            _pendingPlayers.Clear();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Failed to leave Vivox channel: {e.Message}");
        }
    }

    public void ClaimPlayerObject(string playerId, GameObject playerObject)
    {
        if (string.IsNullOrEmpty(playerId)) return;
        if (!_participants.ContainsKey(playerId))
        {
            _pendingPlayers[playerId] = playerObject;
        }
    }

    private void BindVivoxEvents()
    {
        VivoxService.Instance.ParticipantAddedToChannel += OnParticipantAdded;
        VivoxService.Instance.ParticipantRemovedFromChannel += OnParticipantRemoved;
    }

    private void UnbindVivoxEvents()
    {
        if (VivoxService.Instance != null)
        {
            VivoxService.Instance.ParticipantAddedToChannel -= OnParticipantAdded;
            VivoxService.Instance.ParticipantRemovedFromChannel -= OnParticipantRemoved;
        }
    }

    private void OnParticipantAdded(VivoxParticipant participant)
    {
        _participants[participant.PlayerId] = participant;
        if (_pendingPlayers.ContainsKey(participant.PlayerId))
        {
            _pendingPlayers.Remove(participant.PlayerId);
        }
    }

    private void OnParticipantRemoved(VivoxParticipant participant)
    {
        if (_participants.ContainsKey(participant.PlayerId))
        {
            _participants.Remove(participant.PlayerId);
        }
        if (_pendingPlayers.ContainsKey(participant.PlayerId))
        {
            _pendingPlayers.Remove(participant.PlayerId);
        }
    }

    public VivoxParticipant GetParticipant(string playerId)
    {
        if (!string.IsNullOrEmpty(playerId) && _participants.TryGetValue(playerId, out var participant))
        {
            return participant;
        }
        return null;
    }
}