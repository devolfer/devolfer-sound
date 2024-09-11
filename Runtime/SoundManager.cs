using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Pool;

namespace devolfer.Sound
{
    public class SoundManager : PersistentSingleton<SoundManager>
    {
        [Space]
        [Tooltip("The number of entities pre-allocated by the sound entity pool." +
                 "\n\nIdeally set this to the expected number of maximum simultaneously playing sounds.")]
        [SerializeField] private int _soundEntityPoolCapacityDefault = 64;
        
        [Space]
        [Tooltip("Add any Audio Mixer groups you wish here, that the Sound Manager can change the respective volumes of." +
                 "\n\nIf none are provided, the default Audio Mixer and groups bundled with the package will be used.")]
        [SerializeField] private MixerVolumeGroup[] _mixerVolumeGroupsDefault;

        private HashSet<SoundEntity> _entitiesPlaying;
        private HashSet<SoundEntity> _entitiesPaused;
        private HashSet<SoundEntity> _entitiesStopping;
        
        private ObjectPool<SoundEntity> _soundEntityPool;

        private Dictionary<string, MixerVolumeGroup> _mixerVolumeGroups;
        private Dictionary<string, Coroutine> _mixerFadeRoutines;

        protected override void Setup()
        {
            base.Setup();

            if (s_instance != this) return;

            SetupSoundEntities();
            SetupMixers();
        }

        private void SetupSoundEntities()
        {
            _entitiesPlaying = new HashSet<SoundEntity>();
            _entitiesPaused = new HashSet<SoundEntity>();
            _entitiesStopping = new HashSet<SoundEntity>();

            _soundEntityPool = new ObjectPool<SoundEntity>(
                createFunc: () =>
                {
                    GameObject obj = new($"SoundEntity-{_soundEntityPool.CountAll}");
                    obj.transform.SetParent(transform);
                    SoundEntity entity = obj.AddComponent<SoundEntity>();
                    entity.Setup(this);

                    obj.SetActive(false);

                    return entity;
                },
                actionOnGet: entity => entity.gameObject.SetActive(true),
                actionOnRelease: entity => entity.gameObject.SetActive(false),
                actionOnDestroy: entity => Destroy(entity.gameObject),
                defaultCapacity: _soundEntityPoolCapacityDefault);

            _soundEntityPool.PreAllocate(_soundEntityPoolCapacityDefault);
        }

        private void SetupMixers()
        {
            _mixerVolumeGroups = new Dictionary<string, MixerVolumeGroup>();
            _mixerFadeRoutines = new Dictionary<string, Coroutine>();

            if (_mixerVolumeGroupsDefault == null || _mixerVolumeGroupsDefault.Length == 0)
            {
                AudioMixer audioMixerDefault = Resources.Load<AudioMixer>("AudioMixerDefault");

                _mixerVolumeGroupsDefault = new MixerVolumeGroup[3];
                _mixerVolumeGroupsDefault[0] = new MixerVolumeGroup(audioMixerDefault, "VolumeMaster", 10);
                _mixerVolumeGroupsDefault[1] = new MixerVolumeGroup(audioMixerDefault, "VolumeMusic", 10);
                _mixerVolumeGroupsDefault[2] = new MixerVolumeGroup(audioMixerDefault, "VolumeSFX", 10);
            }

            foreach (MixerVolumeGroup group in _mixerVolumeGroupsDefault)
            {
                RegisterMixerVolumeGroup(group);
                group.Refresh();
            }
        }

        #region Entity

        public SoundEntity Play(SoundProperties properties,
                                Transform parent = null,
                                Vector3 position = default,
                                bool fadeIn = false,
                                float fadeInDuration = .5f,
                                Ease fadeInEase = Ease.Linear,
                                Action onComplete = null)
        {
            SoundEntity entity = _soundEntityPool.Get();
            _entitiesPlaying.Add(entity);

            return entity.Play(properties, parent, position, fadeIn, fadeInDuration, fadeInEase, onComplete);
        }

        public void Pause(SoundEntity entity)
        {
            if (!_entitiesPlaying.Contains(entity)) return;
            if (_entitiesStopping.Contains(entity)) return;

            _entitiesPlaying.Remove(entity);

            entity.Pause();

            _entitiesPaused.Add(entity);
        }

        public void Resume(SoundEntity entity)
        {
            if (!_entitiesPaused.Contains(entity)) return;
            if (_entitiesStopping.Contains(entity)) return;

            _entitiesPaused.Remove(entity);

            entity.Resume();

            _entitiesPlaying.Add(entity);
        }

        public void Stop(SoundEntity entity,
                         bool fadeOut = true,
                         float fadeOutDuration = .5f,
                         Ease fadeOutEase = Ease.Linear)
        {
            bool playingEntity = _entitiesPlaying.Contains(entity);
            bool pausedEntity = _entitiesPaused.Contains(entity);

            if (!playingEntity && !pausedEntity) return;

            _entitiesStopping.Add(entity);
            entity.Stop(fadeOut, fadeOutDuration, fadeOutEase, onComplete: ReleaseEntity);
            return;

            void ReleaseEntity()
            {
                if (playingEntity) _entitiesPlaying.Remove(entity);
                if (pausedEntity) _entitiesPaused.Remove(entity);
                _entitiesStopping.Remove(entity);

                _soundEntityPool.Release(entity);
            }
        }

        public void PauseAll()
        {
            foreach (SoundEntity entity in _entitiesPlaying)
            {
                if (_entitiesStopping.Contains(entity)) continue;

                entity.Pause();
                _entitiesPaused.Add(entity);
            }

            _entitiesPlaying.Clear();
        }

        public void ResumeAll()
        {
            foreach (SoundEntity entity in _entitiesPaused)
            {
                if (_entitiesStopping.Contains(entity)) continue;

                entity.Resume();
                _entitiesPlaying.Add(entity);
            }

            _entitiesPaused.Clear();
        }

        public void StopAll(bool fadeOut = true,
                            float fadeOutDuration = 1,
                            Ease fadeOutEase = Ease.Linear)
        {
            foreach (SoundEntity entity in _entitiesPlaying) Stop(entity, fadeOut, fadeOutDuration, fadeOutEase);

            foreach (SoundEntity entity in _entitiesPaused)
            {
                entity.Stop(false);
                _soundEntityPool.Release(entity);
            }

            _entitiesPaused.Clear();
        }

        public void Fade(SoundEntity entity, float duration, float targetVolume, Ease ease = Ease.Linear)
        {
            if (_entitiesStopping.Contains(entity)) return;

            if (_entitiesPaused.Contains(entity)) Resume(entity);

            entity.Fade(duration, targetVolume, ease);
        }

        public SoundEntity CrossFade(float duration,
                                     SoundEntity fadeOutEntity,
                                     SoundProperties fadeInProperties,
                                     Transform fadeInParent = null,
                                     Vector3 fadeInPosition = default)
        {
            Stop(fadeOutEntity, fadeOutDuration: duration);

            return Play(fadeInProperties, fadeInParent, fadeInPosition, true, duration);
        }

        internal static IEnumerator Fade(AudioSource audioSource,
                                         float duration,
                                         float targetVolume,
                                         Ease ease = Ease.Linear,
                                         WaitWhile waitWhilePredicate = null)
        {
            return Fade(
                audioSource,
                duration,
                targetVolume,
                EasingFunctions.GetEasingFunction(ease),
                waitWhilePredicate);
        }

        internal static IEnumerator Fade(AudioSource audioSource,
                                         float duration,
                                         float targetVolume,
                                         Func<float, float> easeFunction,
                                         WaitWhile waitWhilePredicate = null)
        {
            targetVolume = Mathf.Clamp01(targetVolume);

            if (duration <= 0)
            {
                audioSource.volume = targetVolume;
                yield break;
            }

            float deltaTime = 0;
            float startVolume = audioSource.volume;

            while (deltaTime < duration)
            {
                deltaTime += Time.deltaTime;
                audioSource.volume = Mathf.Lerp(startVolume, targetVolume, easeFunction(deltaTime / duration));

                yield return waitWhilePredicate;
            }

            audioSource.volume = targetVolume;
        }

        #endregion

        #region Mixer

        public void RegisterMixerVolumeGroup(MixerVolumeGroup group)
        {
            _mixerVolumeGroups.TryAdd(group.ExposedParameter, group);
        }

        public void UnRegisterMixerVolumeGroup(MixerVolumeGroup group)
        {
            _mixerVolumeGroups.Remove(group.ExposedParameter);
        }

        public void SetMixerGroupVolume(string exposedParameter, float value)
        {
            if (!MixerVolumeGroupRegistered(exposedParameter, out MixerVolumeGroup mixerVolumeGroup)) return;
            
            StopMixerFadeRoutine(exposedParameter);
            
            mixerVolumeGroup.Set(value);
        }

        public void IncreaseMixerGroupVolume(string exposedParameter)
        {
            if (!MixerVolumeGroupRegistered(exposedParameter, out MixerVolumeGroup mixerVolumeGroup)) return;
            
            StopMixerFadeRoutine(exposedParameter);
            
            mixerVolumeGroup.Increase();
        }

        public void DecreaseMixerGroupVolume(string exposedParameter)
        {
            if (!MixerVolumeGroupRegistered(exposedParameter, out MixerVolumeGroup mixerVolumeGroup)) return;
            
            StopMixerFadeRoutine(exposedParameter);
            
            mixerVolumeGroup.Decrease();
        }

        public void MuteMixerGroupVolume(string exposedParameter, bool value)
        {
            if (!MixerVolumeGroupRegistered(exposedParameter, out MixerVolumeGroup mixerVolumeGroup)) return;
            
            StopMixerFadeRoutine(exposedParameter);
            
            mixerVolumeGroup.Mute(value);
        }

        public void FadeMixerGroupVolume(string exposedParameter, float duration, float targetVolume, Ease ease)
        {
            if (!MixerVolumeGroupRegistered(exposedParameter, out MixerVolumeGroup mixerVolumeGroup)) return;
            
            StopMixerFadeRoutine(exposedParameter);
            
            _mixerFadeRoutines.TryAdd(exposedParameter, StartCoroutine(FadeRoutine()));
            
            return;
            
            IEnumerator FadeRoutine()
            {
                yield return mixerVolumeGroup.Fade(duration, targetVolume, ease);
                
                _mixerFadeRoutines.Remove(exposedParameter);
            }
        }

        public void CrossFadeMixerGroupVolumes(string fadeOutExposedParameter,
                                               string fadeInExposedParameter,
                                               float duration)
        {
            FadeMixerGroupVolume(fadeOutExposedParameter, duration, 0, Ease.Linear);
            FadeMixerGroupVolume(fadeInExposedParameter, duration, 1, Ease.Linear);
        }

        private bool MixerVolumeGroupRegistered(string exposedParameter, out MixerVolumeGroup mixerVolumeGroup)
        {
            if (_mixerVolumeGroups.TryGetValue(exposedParameter, out mixerVolumeGroup)) return true;

            Debug.LogError($"There is no {nameof(MixerVolumeGroup)} for {exposedParameter} registered.");
            return false;
        }
        
        private void StopMixerFadeRoutine(string exposedParameter)
        {
            if (!_mixerFadeRoutines.TryGetValue(exposedParameter, out Coroutine fadeRoutine)) return;
            
            StopCoroutine(fadeRoutine);
            _mixerFadeRoutines.Remove(exposedParameter);
        }
        
        #endregion
    }

    public class Singleton<T> : MonoBehaviour where T : Component
    {
        [Tooltip("Optionally override the gameobjects' hide flags, if you want the gameobject e.g. not to be shown in the hierarchy." +
                 "\n\nBe careful with the not saving options, as you will have to manage deleting manually yourself then!")]
        [SerializeField] protected HideFlags _hideFlags = HideFlags.None;

        protected static T s_instance;

        public static T Instance
        {
            get
            {
                if (s_instance != null) return s_instance;

                s_instance = FindAnyObjectByType<T>();

                return s_instance != null ?
                    s_instance :
                    s_instance = new GameObject($"{typeof(T).Name}").AddComponent<T>();
            }
        }

        protected virtual void Awake()
        {
            if (!Application.isPlaying) return;

            Setup();
        }

        protected virtual void OnDestroy() => s_instance = null;

        protected virtual void Setup()
        {
            gameObject.hideFlags = _hideFlags;

            s_instance = this as T;
        }
    }

    public class PersistentSingleton<T> : Singleton<T> where T : Component
    {
        protected override void Setup()
        {
            transform.SetParent(null);

            if (s_instance != null)
            {
                if (s_instance != this) Destroy(gameObject);
            }
            else
            {
                base.Setup();

                DontDestroyOnLoad(gameObject);
            }
        }
    }

    internal static class ObjectPoolExtensions
    {
        /// <summary>
        /// Pre-allocates objects from the pool by getting and releasing a desired amount.
        /// </summary>
        /// <param name="capacity">The amount of objects to pre-allocate.</param>
        public static void PreAllocate<T>(this ObjectPool<T> pool, int capacity) where T : class
        {
            T[] preAllocatedT = new T[capacity];

            for (int i = 0; i < capacity; i++) preAllocatedT[i] = pool.Get();
            for (int i = preAllocatedT.Length - 1; i >= 0; i--) pool.Release(preAllocatedT[i]);
        }
    }
}