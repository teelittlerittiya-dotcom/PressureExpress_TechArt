using UnityEngine;
using TMPro;
using Unity.Services.Vivox;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Audio device selection for Vivox.
///
/// Exposes a plain data API (names + indices) so any UI layer can drive it. The legacy uGUI
/// dropdowns still work if assigned, but they are optional now: the UI Toolkit settings panel
/// binds to the API instead.
/// </summary>
public class VivoxAudioHandler : MonoBehaviour
{
    [Header("Legacy uGUI device selection (optional)")]
    [Tooltip("Optional. Leave empty when using the UI Toolkit settings panel.")]
    public TMP_Dropdown inputDeviceDropdown;
    public TMP_Dropdown outputDeviceDropdown;

    private readonly List<VivoxInputDevice> availableInputDevices = new List<VivoxInputDevice>();
    private readonly List<VivoxOutputDevice> availableOutputDevices = new List<VivoxOutputDevice>();

    private bool isMuted;

    /// <summary>Raised after the device lists change, so open UI can rebind.</summary>
    public event Action DevicesRefreshed;

    public bool IsMuted => isMuted;

    public IReadOnlyList<string> InputDeviceNames =>
        availableInputDevices.Select(d => d.DeviceName).ToList();

    public IReadOnlyList<string> OutputDeviceNames =>
        availableOutputDevices.Select(d => d.DeviceName).ToList();

    public int ActiveInputIndex { get; private set; } = -1;
    public int ActiveOutputIndex { get; private set; } = -1;

    /// <summary>
    /// Called by VivoxManager once login succeeds. Must never throw: VivoxManager calls this
    /// inside the try block that sets IsInitialized, so an exception here would leave voice chat
    /// permanently reported as uninitialised.
    /// </summary>
    public void InitializeDeviceUI()
    {
        try
        {
            RefreshDevices();

            if (inputDeviceDropdown != null)
            {
                inputDeviceDropdown.onValueChanged.RemoveListener(SelectInputDevice);
                inputDeviceDropdown.ClearOptions();
                inputDeviceDropdown.AddOptions(InputDeviceNames.ToList());
                if (ActiveInputIndex >= 0) inputDeviceDropdown.SetValueWithoutNotify(ActiveInputIndex);
                inputDeviceDropdown.onValueChanged.AddListener(SelectInputDevice);
            }

            if (outputDeviceDropdown != null)
            {
                outputDeviceDropdown.onValueChanged.RemoveListener(SelectOutputDevice);
                outputDeviceDropdown.ClearOptions();
                outputDeviceDropdown.AddOptions(OutputDeviceNames.ToList());
                if (ActiveOutputIndex >= 0) outputDeviceDropdown.SetValueWithoutNotify(ActiveOutputIndex);
                outputDeviceDropdown.onValueChanged.AddListener(SelectOutputDevice);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[VivoxAudioHandler] Device UI setup failed: {e.Message}");
        }
    }

    /// <summary>Re-reads the device lists from Vivox. Safe to call before Vivox is logged in.</summary>
    public void RefreshDevices()
    {
        availableInputDevices.Clear();
        availableOutputDevices.Clear();
        ActiveInputIndex = -1;
        ActiveOutputIndex = -1;

        try
        {
            var vivox = VivoxService.Instance;
            if (vivox == null) return;

            availableInputDevices.AddRange(vivox.AvailableInputDevices);
            availableOutputDevices.AddRange(vivox.AvailableOutputDevices);

            VivoxInputDevice activeInput = vivox.ActiveInputDevice;
            if (activeInput != null)
            {
                ActiveInputIndex = availableInputDevices.FindIndex(d => d.DeviceName == activeInput.DeviceName);
            }

            VivoxOutputDevice activeOutput = vivox.ActiveOutputDevice;
            if (activeOutput != null)
            {
                ActiveOutputIndex = availableOutputDevices.FindIndex(d => d.DeviceName == activeOutput.DeviceName);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[VivoxAudioHandler] Could not read audio devices: {e.Message}");
        }

        DevicesRefreshed?.Invoke();
    }

    public void SelectInputDevice(int index)
    {
        if (index < 0 || index >= availableInputDevices.Count) return;

        VivoxInputDevice device = availableInputDevices[index];
        ActiveInputIndex = index;
        Debug.Log($"[VivoxAudioHandler] Input device -> {device.DeviceName}");

        try
        {
            VivoxService.Instance.SetActiveInputDeviceAsync(device);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[VivoxAudioHandler] Could not set input device: {e.Message}");
        }
    }

    public void SelectOutputDevice(int index)
    {
        if (index < 0 || index >= availableOutputDevices.Count) return;

        VivoxOutputDevice device = availableOutputDevices[index];
        ActiveOutputIndex = index;
        Debug.Log($"[VivoxAudioHandler] Output device -> {device.DeviceName}");

        try
        {
            VivoxService.Instance.SetActiveOutputDeviceAsync(device);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[VivoxAudioHandler] Could not set output device: {e.Message}");
        }
    }

    public void SetMuted(bool muted)
    {
        isMuted = muted;

        try
        {
            if (isMuted) VivoxService.Instance.MuteInputDevice();
            else VivoxService.Instance.UnmuteInputDevice();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[VivoxAudioHandler] Could not change mute state: {e.Message}");
            return;
        }

        Debug.Log($"[VivoxAudioHandler] Local player muted: {isMuted}");
    }

    public void ToggleMute()
    {
        SetMuted(!isMuted);
    }
}
