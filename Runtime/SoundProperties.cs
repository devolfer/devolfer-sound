using System;
using UnityEngine;
using UnityEngine.Audio;

namespace devolfer.Sound
{
    [Serializable]
    public class SoundProperties
    {
        public AudioClip Clip;
        public AudioMixerGroup OutputAudioMixerGroup;
        public bool Mute;
        public bool BypassEffects;
        public bool BypassListenerEffects;
        public bool BypassReverbZones;
        public bool PlayOnAwake;
        public bool Loop;
        [Range(0, 256)] public int Priority; // 0 = High, 256 = Low
        [Range(0, 1)] public float Volume;
        [Range(-3, 3)] public float Pitch;
        [Range(-1, 1)] public float PanStereo;   // -1 = Left, 1 = Right
        [Range(0, 1)] public float SpatialBlend; // 0 = 2D, 1 = 3D
        [Range(0, 1.1f)] public float ReverbZoneMix;

        // 3D Sound Settings
        [Range(0, 5)] public float DopplerLevel;
        [Range(0, 360)] public float Spread;
        public AudioRolloffMode RolloffMode;
        public float MinDistance;
        public float MaxDistance;

        public SoundProperties()
        {
            Clip = null;
            OutputAudioMixerGroup = null;
            Mute = false;
            BypassEffects = false;
            BypassListenerEffects = false;
            BypassReverbZones = false;
            PlayOnAwake = true;
            Loop = false;
            Priority = 128;
            Volume = 1;
            Pitch = 1;
            PanStereo = 0;
            SpatialBlend = 0;
            ReverbZoneMix = 1;
            DopplerLevel = 1;
            Spread = 0;
            RolloffMode = AudioRolloffMode.Logarithmic;
            MinDistance = 1;
            MaxDistance = 500;
        }

        public SoundProperties(SoundProperties properties)
        {
            Clip = properties.Clip;
            OutputAudioMixerGroup = properties.OutputAudioMixerGroup;
            Mute = properties.Mute;
            BypassEffects = properties.BypassEffects;
            BypassListenerEffects = properties.BypassListenerEffects;
            BypassReverbZones = properties.BypassReverbZones;
            PlayOnAwake = properties.PlayOnAwake;
            Loop = properties.Loop;
            Priority = properties.Priority;
            Volume = properties.Volume;
            Pitch = properties.Pitch;
            PanStereo = properties.PanStereo;
            SpatialBlend = properties.SpatialBlend;
            ReverbZoneMix = properties.ReverbZoneMix;
            DopplerLevel = properties.DopplerLevel;
            Spread = properties.Spread;
            RolloffMode = properties.RolloffMode;
            MinDistance = properties.MinDistance;
            MaxDistance = properties.MaxDistance;
        }

        public void ApplyOn(ref AudioSource audioSource)
        {
            audioSource.clip = Clip;
            audioSource.outputAudioMixerGroup = OutputAudioMixerGroup;
            audioSource.mute = Mute;
            audioSource.bypassEffects = BypassEffects;
            audioSource.bypassListenerEffects = BypassListenerEffects;
            audioSource.bypassReverbZones = BypassReverbZones;
            audioSource.playOnAwake = PlayOnAwake;
            audioSource.loop = Loop;
            audioSource.priority = Priority;
            audioSource.volume = Volume;
            audioSource.pitch = Pitch;
            audioSource.panStereo = PanStereo;
            audioSource.spatialBlend = SpatialBlend;
            audioSource.reverbZoneMix = ReverbZoneMix;
            audioSource.dopplerLevel = DopplerLevel;
            audioSource.spread = Spread;
            audioSource.rolloffMode = RolloffMode;
            audioSource.minDistance = MinDistance;
            audioSource.maxDistance = MaxDistance;
        }

        public void ResetOn(ref AudioSource audioSource)
        {
            audioSource.clip = null;
            audioSource.outputAudioMixerGroup = null;
            audioSource.mute = false;
            audioSource.bypassEffects = false;
            audioSource.bypassListenerEffects = false;
            audioSource.bypassReverbZones = false;
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.priority = 128;
            audioSource.volume = 1f;
            audioSource.pitch = 1f;
            audioSource.panStereo = 0f;
            audioSource.spatialBlend = 1f;
            audioSource.reverbZoneMix = 1f;
            audioSource.dopplerLevel = 1f;
            audioSource.spread = 0f;
            audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            audioSource.minDistance = 1f;
            audioSource.maxDistance = 500f;
        }

        public static implicit operator SoundProperties(AudioSource audioSource) =>
            new()
            {
                Clip = audioSource.clip,
                OutputAudioMixerGroup = audioSource.outputAudioMixerGroup,
                Mute = audioSource.mute,
                BypassEffects = audioSource.bypassEffects,
                BypassListenerEffects = audioSource.bypassListenerEffects,
                BypassReverbZones = audioSource.bypassReverbZones,
                PlayOnAwake = audioSource.playOnAwake,
                Loop = audioSource.loop,
                Priority = audioSource.priority,
                Volume = audioSource.volume,
                Pitch = audioSource.pitch,
                PanStereo = audioSource.panStereo,
                SpatialBlend = audioSource.spatialBlend,
                ReverbZoneMix = audioSource.reverbZoneMix,
                DopplerLevel = audioSource.dopplerLevel,
                Spread = audioSource.spread,
                RolloffMode = audioSource.rolloffMode,
                MinDistance = audioSource.minDistance,
                MaxDistance = audioSource.maxDistance
            };
    }
}