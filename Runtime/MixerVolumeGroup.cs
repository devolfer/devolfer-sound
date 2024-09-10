using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

namespace devolfer.Sound
{
    [Serializable]
    public class MixerVolumeGroup
    {
        [SerializeField] private AudioMixer _audioMixer;
        [SerializeField] private string _exposedParameter;
        [SerializeField, Min(1)] private int _volumeSegments;

        public string ExposedParameter => _exposedParameter;

        public float VolumeCurrent => _volumeCurrent;
        public int VolumeSegments => _volumeSegments;
        public bool Muted => _muted;

        private float _volumeCurrent = 1;
        private float _volumePrevious = 1;
        private bool _muted;

        internal MixerVolumeGroup(AudioMixer audioMixer,
                                  string exposedParameter,
                                  int volumeSegments)
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
            float segmentStep = 1f / _volumeSegments;
            float volumeCurrentRounded = Mathf.Round(_volumeCurrent / segmentStep) * segmentStep;
            Set(volumeCurrentRounded + segmentStep);
        }

        internal void Decrease()
        {
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
        public static bool TrySetVolume(this AudioMixer mixer, string exposedParameter, ref float value)
        {
            value = Mathf.Clamp01(value);
            float decibel = value != 0 ? Mathf.Log10(value) * 20 : -80;

            return mixer.SetFloat(exposedParameter, decibel);
        }

        public static bool TryGetVolume(this AudioMixer mixer, string exposedParameter, out float value)
        {
            value = 0;

            if (!mixer.GetFloat(exposedParameter, out float decibel)) return false;

            value = decibel > -80 ? Mathf.Pow(10, decibel / 20) : 0;

            return true;
        }

        public static bool HasParameter(this AudioMixer mixer, string exposedParameter)
        {
            return mixer.GetFloat(exposedParameter, out float _);
        }
    }
}