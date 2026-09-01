using System.Collections.Generic;
using PressureExpress.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Settings panel, bound to the hand-made settings UI.
///
/// The three original field names are unchanged so the references already wired in MainMenu.unity
/// stay connected.
///
/// Two bugs fixed here. The widgets had no OnValueChanged entries in the scene and this class never
/// added listeners, so changing a setting did nothing at all. And Start read PlayerPrefs into the
/// widgets without ever applying them, so a saved value was ignored on launch — that now happens
/// once in GameBootstrap via DisplaySettings.Apply().
/// </summary>
public class SettingsMenu : MonoBehaviour
{
    public Slider volumeSlider;
    public Toggle fullscreenToggle;
    public TMP_Dropdown resolutionDropdown;

    [Header("Optional — voice devices")]
    public TMP_Dropdown inputDeviceDropdown;
    public TMP_Dropdown outputDeviceDropdown;

    private List<Resolution> resolutions = new List<Resolution>();

    private void Start()
    {
        // MainMenu can be opened directly without GameBootstrap. Apply saved display settings here
        // so volume, fullscreen, and resolution are restored in every launch path.
        DisplaySettings.Apply();

        BuildResolutionDropdown();

        if (volumeSlider != null)
        {
            volumeSlider.SetValueWithoutNotify(DisplaySettings.Volume);
            volumeSlider.onValueChanged.AddListener(SetVolume);
        }

        if (fullscreenToggle != null)
        {
            fullscreenToggle.SetIsOnWithoutNotify(Screen.fullScreen);
            fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
        }

        BuildVoiceDropdowns();
    }

    private void OnDestroy()
    {
        if (volumeSlider != null) volumeSlider.onValueChanged.RemoveListener(SetVolume);
        if (fullscreenToggle != null) fullscreenToggle.onValueChanged.RemoveListener(SetFullscreen);
        if (resolutionDropdown != null) resolutionDropdown.onValueChanged.RemoveListener(SetResolution);
    }

    private void OnEnable()
    {
        // Devices can be plugged in while the game is running, and Vivox may not have been logged
        // in yet the first time this panel was built.
        BuildVoiceDropdowns();
    }

    private void BuildResolutionDropdown()
    {
        if (resolutionDropdown == null) return;

        resolutions = DisplaySettings.BuildResolutionList();

        var options = new List<string>(resolutions.Count);
        foreach (Resolution res in resolutions)
        {
            options.Add($"{res.width} x {res.height}");
        }

        resolutionDropdown.ClearOptions();
        resolutionDropdown.AddOptions(options);

        int saved = DisplaySettings.SavedResolutionIndex;
        int index = saved >= 0 && saved < resolutions.Count
            ? saved
            : DisplaySettings.CurrentResolutionIndex(resolutions);

        resolutionDropdown.SetValueWithoutNotify(index);
        resolutionDropdown.RefreshShownValue();

        resolutionDropdown.onValueChanged.RemoveListener(SetResolution);
        resolutionDropdown.onValueChanged.AddListener(SetResolution);
    }

    private void BuildVoiceDropdowns()
    {
        if (inputDeviceDropdown == null && outputDeviceDropdown == null) return;

        VivoxAudioHandler audio = VivoxManager.Instance != null
            ? VivoxManager.Instance.GetComponent<VivoxAudioHandler>()
            : null;

        bool available = audio != null && VivoxManager.Instance.IsInitialized;

        if (inputDeviceDropdown != null) inputDeviceDropdown.interactable = available;
        if (outputDeviceDropdown != null) outputDeviceDropdown.interactable = available;

        if (!available) return;

        audio.RefreshDevices();

        Bind(inputDeviceDropdown, audio.InputDeviceNames, audio.ActiveInputIndex, audio.SelectInputDevice);
        Bind(outputDeviceDropdown, audio.OutputDeviceNames, audio.ActiveOutputIndex, audio.SelectOutputDevice);
    }

    private static void Bind(TMP_Dropdown dropdown, IReadOnlyList<string> names, int activeIndex,
                             UnityEngine.Events.UnityAction<int> onSelected)
    {
        if (dropdown == null) return;

        dropdown.onValueChanged.RemoveListener(onSelected);
        dropdown.ClearOptions();
        dropdown.AddOptions(new List<string>(names));

        if (names.Count > 0)
        {
            dropdown.SetValueWithoutNotify(activeIndex >= 0 && activeIndex < names.Count ? activeIndex : 0);
            dropdown.RefreshShownValue();
        }

        dropdown.onValueChanged.AddListener(onSelected);
    }

    // Kept public so existing or future inspector OnValueChanged wiring still works.
    public void SetVolume(float volume) => DisplaySettings.SetVolume(volume);

    public void SetFullscreen(bool isFullscreen) => DisplaySettings.SetFullscreen(isFullscreen);

    public void SetResolution(int resolutionIndex) => DisplaySettings.SetResolution(resolutionIndex);
}
