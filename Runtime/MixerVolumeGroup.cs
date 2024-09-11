using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

namespace devolfer.Sound
{
    /// <summary>
    /// Provides various functionality for handling the volume of an <see cref="AudioMixerGroup"/>.
    /// </summary>
    [Serializable]
    public class MixerVolumeGroup
    {
        [Tooltip("The Audio Mixer of the group.")]
        [SerializeField] private AudioMixer _audioMixer;

        [Tooltip(
            "The name of the exposed parameter of the group, e.g. 'VolumeMusic'" +
            "\n\nYou will have to create an Audio Mixer Group and expose a parameter for it in the Editor yourself. " +
            "Check any online-guides, if unsure how to do this.")]
        [SerializeField] private string _exposedParameter;

        [Tooltip(
            "The amount of segments the volume range of 0.0 - 1.0 will be split into. " +
            "This will allow increasing/decreasing the volume in steps (e.g. 10 segments = 0.1 steps)." +
            "\n\nSetting the volume to a specific value is of course still possible.")]
        [SerializeField, Min(1)] private int _volumeSegments;

        /// <summary>
        /// The name that exposes the Audio Mixer Groups' volume and allows manipulating it.
        /// </summary>
        public string ExposedParameter => _exposedParameter;

        /// <summary>
        /// The current volume from 0.0 to 1.0.
        /// </summary>
        public float VolumeCurrent => _volumeCurrent;
        
        /// <summary>
        /// The current dB from -80 to 0.
        /// </summary>
        public float DecibelCurrent => _volumeCurrent != 0 ? Mathf.Log10(_volumeCurrent) * 20 : -80;
        
        /// <summary>
        /// The amount of volume segments the range 0.0 to 1.0 is split into.
        /// </summary>
        public int VolumeSegments => _volumeSegments;
        
        /// <summary>
        /// Is the group muted?
        /// </summary>
        public bool Muted => _muted;

        private float _volumeCurrent = 1;
        private float _volumePrevious = 1;
        private bool _muted;

        /// <param name="audioMixer">The associated Audio Mixer of the group.</param>
        /// <param name="exposedParameter">The name that exposes the Audio Mixer Groups' volume and allows manipulating it.</param>
        /// <param name="volumeSegments">Splits the volume range into the specified amount of segments and allows setting volume in steps.</param>
        public MixerVolumeGroup(AudioMixer audioMixer, string exposedParameter, int volumeSegments)
        {
            _audioMixer = audioMixer;
            _exposedParameter = exposedParameter;
            _volumeSegments = Mathf.Max(volumeSegments, 1);

            if (Application.isPlaying) Refresh();
        }

        internal void Set(float volume)
        {
            if (!_audioMixer.TrySetVolume(_exposedParameter, ref volume))
            {
                Debug.LogError(
                    $"Exposed Parameter {_exposedParameter} not found in {nameof(AudioMixer)} {_audioMixer}");
                return;
            }

            _volumePrevious = _volumeCurrent;
            _volumeCurrent = volume;

            _muted = _volumeCurrent == 0;
        }

        internal void Increase()
        {
            if (_volumeSegments <= 1) return;

            float segmentStep = 1f / _volumeSegments;
            float volumeCurrentRounded = Mathf.Round(_volumeCurrent / segmentStep) * segmentStep;
            Set(volumeCurrentRounded + segmentStep);
        }

        internal void Decrease()
        {
            if (_volumeSegments <= 1) return;

            float segmentStep = 1f / _volumeSegments;
            float volumeCurrentRounded = Mathf.Round(_volumeCurrent / segmentStep) * segmentStep;
            Set(volumeCurrentRounded - segmentStep);
        }

        internal void Mute(bool value)
        {
            if (_muted == value) return;

            _muted = value;

            Set(_muted ? 0 : _volumePrevious);
        }

        internal IEnumerator Fade(float duration, float targetVolume, Ease ease)
        {
            return Fade(duration, targetVolume, EasingFunctions.GetEasingFunction(ease));
        }

        internal IEnumerator Fade(float duration, float targetVolume, Func<float, float> easeFunction)
        {
            if (!_audioMixer.HasParameter(_exposedParameter))
            {
                Debug.LogError(
                    $"Exposed Parameter {_exposedParameter} not found in {nameof(AudioMixer)} {_audioMixer}");
                yield break;
            }

            targetVolume = Mathf.Clamp01(targetVolume);

            if (duration <= 0)
            {
                Set(targetVolume);
                yield break;
            }

            float deltaTime = 0;
            _audioMixer.TryGetVolume(_exposedParameter, out float startVolume);

            while (deltaTime < duration)
            {
                deltaTime += Time.deltaTime;
                Set(Mathf.Lerp(startVolume, targetVolume, easeFunction(deltaTime / duration)));

                yield return null;
            }

            Set(targetVolume);
        }

        internal void Refresh()
        {
            if (_audioMixer.TryGetVolume(_exposedParameter, out _volumeCurrent))
            {
                _volumePrevious = _volumeCurrent;
                _muted = _volumeCurrent == 0;
            }
            else
            {
                Debug.LogError(
                    $"Exposed Parameter {_exposedParameter} not found in {nameof(AudioMixer)} {_audioMixer}");
            }
        }
    }

    internal static class AudioMixerExtensions
    {
        internal static bool TrySetVolume(this AudioMixer mixer, string exposedParameter, ref float value)
        {
            value = Mathf.Clamp01(value);
            float decibel = value != 0 ? Mathf.Log10(value) * 20 : -80;

            return mixer.SetFloat(exposedParameter, decibel);
        }

        internal static bool TryGetVolume(this AudioMixer mixer, string exposedParameter, out float value)
        {
            value = 0;

            if (!mixer.GetFloat(exposedParameter, out float decibel)) return false;

            value = decibel > -80 ? Mathf.Pow(10, decibel / 20) : 0;

            return true;
        }

        internal static bool HasParameter(this AudioMixer mixer, string exposedParameter)
        {
            return mixer.GetFloat(exposedParameter, out float _);
        }
    }
}