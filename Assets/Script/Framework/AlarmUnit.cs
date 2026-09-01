using UnityEngine;

namespace PressureExpress.Framework
{
    public class AlarmUnit
    {
        private readonly AudioSource audioSource;
        private readonly AudioClip clip;
        private readonly float volume;
        private bool isActive;

        public bool IsActive => isActive;

        public AlarmUnit(GameObject owner, AudioClip clip, float volume)
        {
            this.clip = clip;
            this.volume = volume;

            if (clip != null && owner != null)
            {
                audioSource = owner.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.loop = true;
                audioSource.spatialBlend = 0f; // 2D global sound
                audioSource.volume = volume;
            }
        }

        public void Start()
        {
            if (isActive) return;
            isActive = true;
            if (audioSource != null && clip != null)
            {
                audioSource.clip = clip;
                audioSource.Play();
            }
        }

        public void Stop()
        {
            if (!isActive) return;
            isActive = false;
            if (audioSource != null && audioSource.isPlaying)
            {
                audioSource.Stop();
            }
        }
    }
}
