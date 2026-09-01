using System;
using System.Collections.Generic;
using PressureExpress.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace PressureExpress.UI
{
    /// <summary>
    /// Binds SettingsPanel.uxml. A plain class rather than a MonoBehaviour so the same settings UI
    /// can be hosted by both the main menu and the in-game session panel without duplicating markup
    /// or components.
    ///
    /// Also fixes a real bug in the old SettingsMenu: it read PlayerPrefs into the widgets on Start
    /// but never actually applied them, so your saved volume, fullscreen and resolution were
    /// silently ignored on every launch. <see cref="ApplySavedSettings"/> is now called from
    /// GameBootstrap before any UI exists.
    /// </summary>
    public class SettingsController
    {
        private readonly VisualElement _host;
        private readonly VisualTreeAsset _template;

        private VisualElement _panel;
        private Slider _volume;
        private Toggle _fullscreen;
        private DropdownField _resolution;
        private DropdownField _inputDevice;
        private DropdownField _outputDevice;
        private Label _voiceStatus;

        private List<Resolution> _resolutions = new List<Resolution>();
        private VivoxAudioHandler _audio;

        public bool IsOpen { get; private set; }
        public event Action Closed;

        public SettingsController(VisualElement host, VisualTreeAsset template)
        {
            _host = host;
            _template = template;
        }

        /// <summary>
        /// Applies persisted display/audio settings. Call once at boot, before any menu exists.
        /// </summary>
        public static void ApplySavedSettings()
        {
            DisplaySettings.Apply();
        }

        public void Open()
        {
            if (_host == null || _template == null)
            {
                Debug.LogWarning("[SettingsController] No host container or settings template assigned.");
                return;
            }

            if (_panel == null)
            {
                Build();
            }

            _host.RemoveFromClassList("hidden");
            IsOpen = true;

            Refresh();

            // One frame later so the transition from .enter actually runs.
            _panel.RemoveFromClassList("is-open");
            _host.schedule.Execute(() => _panel?.AddToClassList("is-open")).ExecuteLater(16);
        }

        public void Close()
        {
            if (!IsOpen) return;

            IsOpen = false;
            _panel?.RemoveFromClassList("is-open");
            _host.AddToClassList("hidden");
            Closed?.Invoke();
        }

        private void Build()
        {
            _template.CloneTree(_host);
            _panel = _host.Q<VisualElement>("settings-panel");

            if (_panel == null)
            {
                Debug.LogError("[SettingsController] SettingsPanel.uxml has no element named 'settings-panel'.");
                return;
            }

            _volume = _panel.Q<Slider>("volume-slider");
            _fullscreen = _panel.Q<Toggle>("fullscreen-toggle");
            _resolution = _panel.Q<DropdownField>("resolution-dropdown");
            _inputDevice = _panel.Q<DropdownField>("input-device-dropdown");
            _outputDevice = _panel.Q<DropdownField>("output-device-dropdown");
            _voiceStatus = _panel.Q<Label>("voice-status");

            _panel.Q<Button>("settings-close-button")?.RegisterCallback<ClickEvent>(_ => Close());
            _panel.Q<Button>("settings-done-button")?.RegisterCallback<ClickEvent>(_ => Close());

            if (_volume != null) _volume.RegisterValueChangedCallback(OnVolumeChanged);
            if (_fullscreen != null) _fullscreen.RegisterValueChangedCallback(OnFullscreenChanged);
            if (_resolution != null) _resolution.RegisterValueChangedCallback(OnResolutionChanged);
            if (_inputDevice != null) _inputDevice.RegisterValueChangedCallback(OnInputDeviceChanged);
            if (_outputDevice != null) _outputDevice.RegisterValueChangedCallback(OnOutputDeviceChanged);
        }

        private void Refresh()
        {
            _resolutions = DisplaySettings.BuildResolutionList();

            if (_volume != null)
            {
                _volume.SetValueWithoutNotify(DisplaySettings.Volume);
            }

            if (_fullscreen != null)
            {
                _fullscreen.SetValueWithoutNotify(Screen.fullScreen);
            }

            if (_resolution != null)
            {
                var choices = new List<string>(_resolutions.Count);
                int current = 0;

                for (int i = 0; i < _resolutions.Count; i++)
                {
                    choices.Add($"{_resolutions[i].width} x {_resolutions[i].height}");

                    if (_resolutions[i].width == Screen.width && _resolutions[i].height == Screen.height)
                    {
                        current = i;
                    }
                }

                _resolution.choices = choices;
                _resolution.SetValueWithoutNotify(choices.Count > 0 ? choices[current] : string.Empty);
            }

            RefreshVoiceDevices();
        }

        private void RefreshVoiceDevices()
        {
            _audio = ResolveAudioHandler();

            bool available = _audio != null && VivoxManager.Instance != null && VivoxManager.Instance.IsInitialized;

            if (_inputDevice != null) _inputDevice.SetEnabled(available);
            if (_outputDevice != null) _outputDevice.SetEnabled(available);

            if (!available)
            {
                SetVoiceStatus("Voice chat is unavailable.");
                return;
            }

            _audio.RefreshDevices();
            SetVoiceStatus(string.Empty);

            BindDeviceDropdown(_inputDevice, _audio.InputDeviceNames, _audio.ActiveInputIndex);
            BindDeviceDropdown(_outputDevice, _audio.OutputDeviceNames, _audio.ActiveOutputIndex);
        }

        private static void BindDeviceDropdown(DropdownField field, IReadOnlyList<string> names, int activeIndex)
        {
            if (field == null) return;

            var choices = new List<string>(names);
            field.choices = choices;

            if (choices.Count == 0)
            {
                field.SetValueWithoutNotify(string.Empty);
                field.SetEnabled(false);
                return;
            }

            int index = activeIndex >= 0 && activeIndex < choices.Count ? activeIndex : 0;
            field.SetValueWithoutNotify(choices[index]);
        }

        private VivoxAudioHandler ResolveAudioHandler()
        {
            if (_audio != null) return _audio;
            if (VivoxManager.Instance == null) return null;

            return VivoxManager.Instance.GetComponent<VivoxAudioHandler>();
        }

        private void SetVoiceStatus(string message)
        {
            if (_voiceStatus == null) return;

            _voiceStatus.text = message;
            _voiceStatus.EnableInClassList("status--error", !string.IsNullOrEmpty(message));
        }

        #region Handlers

        private void OnVolumeChanged(ChangeEvent<float> evt)
        {
            DisplaySettings.SetVolume(evt.newValue);
        }

        private void OnFullscreenChanged(ChangeEvent<bool> evt)
        {
            DisplaySettings.SetFullscreen(evt.newValue);
        }

        private void OnResolutionChanged(ChangeEvent<string> evt)
        {
            int index = _resolution.choices.IndexOf(evt.newValue);
            if (index < 0 || index >= _resolutions.Count) return;

            DisplaySettings.SetResolution(index);
        }

        private void OnInputDeviceChanged(ChangeEvent<string> evt)
        {
            if (_audio == null) return;

            int index = _inputDevice.choices.IndexOf(evt.newValue);
            if (index >= 0) _audio.SelectInputDevice(index);
        }

        private void OnOutputDeviceChanged(ChangeEvent<string> evt)
        {
            if (_audio == null) return;

            int index = _outputDevice.choices.IndexOf(evt.newValue);
            if (index >= 0) _audio.SelectOutputDevice(index);
        }

        #endregion

    }
}
