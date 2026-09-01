using UnityEngine;
using UnityEngine.UI;

public class VolumeSetting : MonoBehaviour
{
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider SFXSlider;

    private void Start()
    {
        // Initialize sliders from MusicManager's saved/default values.
        if (MusicManager.Instance != null)
        {
            musicSlider.value = MusicManager.Instance.GetBGMVolume();
            SFXSlider.value = MusicManager.Instance.GetSFXVolume();
        }

        // Listen for slider changes.
        musicSlider.onValueChanged.AddListener(SetMusicVolume);
        SFXSlider.onValueChanged.AddListener(SetSFXVolume);
    }

    public void SetMusicVolume(float volume)
    {
        if (MusicManager.Instance != null)
            MusicManager.Instance.SetBGMVolume(volume);
    }

    public void SetSFXVolume(float volume)
    {
        if (MusicManager.Instance != null)
            MusicManager.Instance.SetSFXVolume(volume);
    }
}
