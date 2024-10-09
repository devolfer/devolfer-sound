using System;
using UnityEngine;
using UnityEngine.Events;

namespace Devolfer.Sound
{
    public class SoundVolumeMixer : MonoBehaviour
    {
        [Tooltip(
            "Add the Audio Mixer Group you wish here, that the Sound Manager can change the respective volume of.")]
        [SerializeField] private MixerVolumeGroup _mixerVolumeGroup;

        [Space]
        [SerializeField] private FadeConfiguration _fadeConfiguration;

        private bool _registered;

        private void Awake()
        {
            RegisterIfNeeded();
        }

        private void OnDestroy()
        {
            UnregisterIfNeeded();
        }

        /// <summary>
        /// Sets volume of set <see cref="MixerVolumeGroup"/> via the <see cref="SoundManager"/>.
        /// </summary>
        /// <param name="volume">The volumes' new value.</param>
        public void Set(float volume)
        {
            RegisterIfNeeded();

            SoundManager.Instance.SetMixerGroupVolume(_mixerVolumeGroup.ExposedParameter, volume);
        }

        /// <summary>
        /// Incrementally increases volume of set <see cref="MixerVolumeGroup"/> via the <see cref="SoundManager"/>.
        /// </summary>
        public void Increase()
        {
            RegisterIfNeeded();

            SoundManager.Instance.IncreaseMixerGroupVolume(_mixerVolumeGroup.ExposedParameter);
        }

        /// <summary>
        /// Decrementally decreases volume of set <see cref="MixerVolumeGroup"/> via the <see cref="SoundManager"/>.
        /// </summary>
        public void Decrease()
        {
            RegisterIfNeeded();

            SoundManager.Instance.DecreaseMixerGroupVolume(_mixerVolumeGroup.ExposedParameter);
        }

        /// <summary>
        /// Mutes/Un-mutes volume of set <see cref="MixerVolumeGroup"/> via the <see cref="SoundManager"/>.
        /// </summary>
        /// <param name="muted">True = muted, False = unmuted.</param>
        public void Mute(bool muted)
        {
            RegisterIfNeeded();

            SoundManager.Instance.MuteMixerGroupVolume(_mixerVolumeGroup.ExposedParameter, muted);
        }

        /// <summary>
        /// Fades volume of set <see cref="MixerVolumeGroup"/> via the <see cref="SoundManager"/>.
        /// </summary>
        /// <param name="targetVolume">The target volume reached at the end of the fade.</param>
        public void Fade(float targetVolume)
        {
            RegisterIfNeeded();

            SoundManager.Instance.FadeMixerGroupVolume(
                _mixerVolumeGroup.ExposedParameter,
                targetVolume,
                _fadeConfiguration.FadeDuration,
                ease: _fadeConfiguration.FadeEase,
                onComplete: _fadeConfiguration.OnComplete.Invoke);
        }

        private void RegisterIfNeeded()
        {
            if (_registered) return;

            _registered = true;
            SoundManager.Instance.RegisterMixerVolumeGroup(_mixerVolumeGroup);
        }

        private void UnregisterIfNeeded()
        {
            if (!_registered) return;

            SoundManager.Instance.UnregisterMixerVolumeGroup(_mixerVolumeGroup);
        }

        [Serializable]
        private class FadeConfiguration
        {
            [Tooltip("The duration in seconds the fade will take.")]
            public float FadeDuration = 1f;

            [Tooltip("The easing applied when fading.")]
            public Ease FadeEase = Ease.Linear;

            [Space]
            [Tooltip("Event invoked once volume completes fading.")]
            public UnityEvent OnComplete;
        }
    }
}