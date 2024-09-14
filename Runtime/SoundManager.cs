using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Pool;
#if UNITASK_INCLUDED
using Cysharp.Threading.Tasks;
#endif

namespace devolfer.Sound
{
    /// <summary>
    /// Handles sound playback and volume mixing.
    /// </summary>
    public class SoundManager : PersistentSingleton<SoundManager>
    {
        [Space]
        [Tooltip(
            "The number of entities pre-allocated by the sound entity pool." +
            "\n\nIdeally set this to the expected number of maximum simultaneously playing sounds.")]
        [SerializeField] private int _soundEntityPoolCapacityDefault = 64;

        [Space]
        [Tooltip(
            "Add any Audio Mixer Group you wish here, that the Sound Manager can change the respective volume of." +
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

        /// <summary>
        /// Plays a sound with the specified properties.
        /// </summary>
        /// <param name="properties">The properties that define the sound.</param>
        /// <param name="followTarget">Optional target the sound will follow while playing.</param>
        /// <param name="position">Either the global position or, when following, the position offset at which the sound is played.</param>
        /// <param name="fadeIn">Optional volume fade in at the start of play.</param>
        /// <param name="fadeInDuration">The duration in seconds the fading in will prolong.</param>
        /// <param name="fadeInEase">The easing applied when fading in.</param>
        /// <param name="onComplete">Optional callback once sound completes playing (not applicable for looped sounds).</param>
        /// <returns>The <see cref="SoundEntity"/> used for playback.</returns>
        public SoundEntity Play(SoundProperties properties,
                                Transform followTarget = null,
                                Vector3 position = default,
                                bool fadeIn = false,
                                float fadeInDuration = .5f,
                                Ease fadeInEase = Ease.Linear,
                                Action onComplete = null)
        {
            SoundEntity entity = _soundEntityPool.Get();
            _entitiesPlaying.Add(entity);

            return entity.Play(properties, followTarget, position, fadeIn, fadeInDuration, fadeInEase, onComplete);
        }

        /// <summary>
        /// Plays a sound with the properties of an <see cref="AudioSource"/> and automatically disables it.
        /// </summary>
        /// <param name="audioSource">The source of which the sound properties will be derived from.</param>
        /// <param name="followTarget">Optional target the sound will follow while playing.</param>
        /// <param name="position">Either the global position or, when following, the position offset at which the sound is played.</param>
        /// <param name="fadeIn">Optional volume fade in at the start of play.</param>
        /// <param name="fadeInDuration">The duration in seconds the fading in will prolong.</param>
        /// <param name="fadeInEase">The easing applied when fading in.</param>
        /// <param name="onComplete">Optional callback once sound completes playing (not applicable for looped sounds).</param>
        /// <returns>The <see cref="SoundEntity"/> used for playback.</returns>
        public SoundEntity Play(AudioSource audioSource,
                                Transform followTarget = null,
                                Vector3 position = default,
                                bool fadeIn = false,
                                float fadeInDuration = .5f,
                                Ease fadeInEase = Ease.Linear,
                                Action onComplete = null)
        {
            if (audioSource.isPlaying) audioSource.Stop();
            audioSource.enabled = false;

            SoundProperties properties = audioSource;

            return Play(properties, followTarget, position, fadeIn, fadeInDuration, fadeInEase, onComplete);
        }

        public
#if UNITASK_INCLUDED
            UniTask
#else
            Task
#endif
            PlayAsync(out SoundEntity entity,
                      SoundProperties properties,
                      Transform followTarget = null,
                      Vector3 position = default,
                      bool fadeIn = false,
                      float fadeInDuration = .5f,
                      Ease fadeInEase = Ease.Linear,
                      CancellationToken cancellationToken = default)
        {
            entity = _soundEntityPool.Get();
            _entitiesPlaying.Add(entity);

            return entity.PlayAsync(
                properties,
                followTarget,
                position,
                fadeIn,
                fadeInDuration,
                fadeInEase,
                cancellationToken);
        }

        public
#if UNITASK_INCLUDED
            UniTask
#else
            Task
#endif
            PlayAsync(SoundProperties properties,
                      Transform followTarget = null,
                      Vector3 position = default,
                      bool fadeIn = false,
                      float fadeInDuration = .5f,
                      Ease fadeInEase = Ease.Linear,
                      CancellationToken cancellationToken = default)
        {
            return PlayAsync(
                out _,
                properties,
                followTarget,
                position,
                fadeIn,
                fadeInDuration,
                fadeInEase,
                cancellationToken);
        }

        public
#if UNITASK_INCLUDED
            UniTask
#else
            Task
#endif
            PlayAsync(out SoundEntity entity,
                      AudioSource audioSource,
                      Transform followTarget = null,
                      Vector3 position = default,
                      bool fadeIn = false,
                      float fadeInDuration = .5f,
                      Ease fadeInEase = Ease.Linear,
                      CancellationToken cancellationToken = default)
        {
            if (audioSource.isPlaying) audioSource.Stop();
            audioSource.enabled = false;

            SoundProperties properties = audioSource;

            return PlayAsync(
                out entity,
                properties,
                followTarget,
                position,
                fadeIn,
                fadeInDuration,
                fadeInEase,
                cancellationToken);
        }

        public
#if UNITASK_INCLUDED
            UniTask
#else
            Task
#endif
            PlayAsync(AudioSource audioSource,
                      Transform followTarget = null,
                      Vector3 position = default,
                      bool fadeIn = false,
                      float fadeInDuration = .5f,
                      Ease fadeInEase = Ease.Linear,
                      CancellationToken cancellationToken = default)
        {
            return PlayAsync(
                out _,
                audioSource,
                followTarget,
                position,
                fadeIn,
                fadeInDuration,
                fadeInEase,
                cancellationToken);
        }

        /// <summary>
        /// Pauses sound playback.
        /// </summary>
        /// <param name="entity">The sound entity that is currently playing.</param>
        /// <remarks>Has no effect if the entity is not playing or currently stopping.</remarks>
        public void Pause(SoundEntity entity)
        {
            if (!_entitiesPlaying.Contains(entity)) return;
            if (_entitiesStopping.Contains(entity)) return;

            _entitiesPlaying.Remove(entity);

            entity.Pause();

            _entitiesPaused.Add(entity);
        }

        /// <summary>
        /// Resumes sound playback.
        /// </summary>
        /// <param name="entity">The sound entity that is currently paused.</param>
        /// <remarks>Has no effect if the entity is not paused or currently stopping.</remarks>
        public void Resume(SoundEntity entity)
        {
            if (!_entitiesPaused.Contains(entity)) return;
            if (_entitiesStopping.Contains(entity)) return;

            _entitiesPaused.Remove(entity);

            entity.Resume();

            _entitiesPlaying.Add(entity);
        }

        /// <summary>
        /// Stops sound playback.
        /// </summary>
        /// <param name="entity">The sound entity that is either currently playing or paused.</param>
        /// <param name="fadeOut">True by default. Set this to false, if the volume should not fade out when stopping.</param>
        /// <param name="fadeOutDuration">The duration in seconds the fading out will prolong.</param>
        /// <param name="fadeOutEase">The easing applied when fading out.</param>
        /// <remarks>Paused entities will be stopped without fade out regardless.</remarks>
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

        public async
#if UNITASK_INCLUDED
            UniTask
#else
            Task
#endif
            StopAsync(SoundEntity entity,
                      bool fadeOut = true,
                      float fadeOutDuration = .5f,
                      Ease fadeOutEase = Ease.Linear,
                      CancellationToken cancellationToken = default)
        {
            bool playingEntity = _entitiesPlaying.Contains(entity);
            bool pausedEntity = _entitiesPaused.Contains(entity);

            if (!playingEntity && !pausedEntity) return;

            _entitiesStopping.Add(entity);
            await entity.StopAsync(fadeOut, fadeOutDuration, fadeOutEase, cancellationToken);

            ReleaseEntity();

            return;

            void ReleaseEntity()
            {
                if (playingEntity) _entitiesPlaying.Remove(entity);
                if (pausedEntity) _entitiesPaused.Remove(entity);
                _entitiesStopping.Remove(entity);

                _soundEntityPool.Release(entity);
            }
        }

        /// <summary>
        /// Pauses all currently playing sound entities.
        /// </summary>
        /// <remarks>Has no effect on entities currently stopping.</remarks>
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

        /// <summary>
        /// Resumes all currently paused sound entities.
        /// </summary>
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

        /// <summary>
        /// Stops all currently playing and paused sound entities.
        /// </summary>
        /// <param name="fadeOut">True by default. Set this to false, if the volumes should not fade out when stopping.</param>
        /// <param name="fadeOutDuration">The duration in seconds the fading out will prolong.</param>
        /// <param name="fadeOutEase">The easing applied when fading out.</param>
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

        public async
#if UNITASK_INCLUDED
            UniTask
#else
            Task
#endif
            StopAllAsync(bool fadeOut = true,
                         float fadeOutDuration = 1,
                         Ease fadeOutEase = Ease.Linear,
                         CancellationToken cancellationToken = default)
        {
#if UNITASK_INCLUDED
            List<UniTask> stopTasks = new();
#else
            List<Task> stopTasks = new();
#endif

            foreach (SoundEntity entity in _entitiesPlaying)
            {
                stopTasks.Add(StopAsync(entity, fadeOut, fadeOutDuration, fadeOutEase, cancellationToken));
            }

            foreach (SoundEntity entity in _entitiesPaused)
            {
                stopTasks.Add(entity.StopAsync(false, cancellationToken: cancellationToken));
                _soundEntityPool.Release(entity);
            }

            _entitiesPaused.Clear();

#if UNITASK_INCLUDED
            await UniTask.WhenAll(stopTasks);
#else
            await Task.WhenAll(stopTasks);
#endif
        }

        /// <summary>
        /// Fades sound volume.
        /// </summary>
        /// <param name="entity">The sound entity that is currently playing or paused.</param>
        /// <param name="targetVolume">The target volume reached at the end of the fade.</param>
        /// <param name="duration">The duration in seconds the fade will prolong.</param>
        /// <param name="ease">The easing applied when fading.</param>
        /// <remarks>Has no effect on entities currently stopping.</remarks>
        public void Fade(SoundEntity entity, float targetVolume, float duration, Ease ease = Ease.Linear)
        {
            if (_entitiesStopping.Contains(entity)) return;

            if (_entitiesPaused.Contains(entity)) Resume(entity);

            entity.Fade(duration, targetVolume, ease);
        }

        public
#if UNITASK_INCLUDED
            UniTask
#else
            Task
#endif
            FadeAsync(SoundEntity entity,
                      float targetVolume,
                      float duration,
                      Ease ease = Ease.Linear,
                      CancellationToken cancellationToken = default)
        {
            if (_entitiesStopping.Contains(entity)) return default;

            if (_entitiesPaused.Contains(entity)) Resume(entity);

            return entity.FadeAsync(duration, targetVolume, ease, cancellationToken);
        }

        /// <summary>
        /// Linearly cross-fades a playing sound entity and a new sound. The fading out sound entity will be stopped at the end.
        /// </summary>
        /// <param name="duration">The duration in seconds the cross-fade will prolong.</param>
        /// <param name="fadeOutEntity">The sound entity that will fade out and stop.</param>
        /// <param name="fadeInProperties">The properties that define the newly played sound.</param>
        /// <param name="followTarget">Optional target the new sound will follow while playing.</param>
        /// <param name="fadeInPosition">Either the global position or, when following, the position offset at which the new sound is played.</param>
        /// <returns>The new <see cref="SoundEntity"/> fading in.</returns>
        /// <remarks>Simultaneously call Stop and Play methods for finer cross-fading control instead.</remarks>
        public SoundEntity CrossFade(float duration,
                                     SoundEntity fadeOutEntity,
                                     SoundProperties fadeInProperties,
                                     Transform followTarget = null,
                                     Vector3 fadeInPosition = default)
        {
            Stop(fadeOutEntity, fadeOutDuration: duration);

            return Play(fadeInProperties, followTarget, fadeInPosition, true, duration);
        }

        public
#if UNITASK_INCLUDED
            UniTask
#else
            Task
#endif
            CrossFadeAsync(out SoundEntity entity,
                           float duration,
                           SoundEntity fadeOutEntity,
                           SoundProperties fadeInProperties,
                           Transform followTarget = null,
                           Vector3 fadeInPosition = default,
                           CancellationToken cancellationToken = default)
        {
            _ = StopAsync(fadeOutEntity, fadeOutDuration: duration, cancellationToken: cancellationToken);

            return PlayAsync(
                out entity,
                fadeInProperties,
                followTarget,
                fadeInPosition,
                true,
                duration,
                cancellationToken: cancellationToken);
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

        internal static
#if UNITASK_INCLUDED
            UniTask
#else
            Task
#endif
            FadeAsync(AudioSource audioSource,
                      float duration,
                      float targetVolume,
                      Ease ease = Ease.Linear,
                      Func<bool> waitWhilePredicate = default,
                      CancellationToken cancellationToken = default)
        {
            return FadeAsync(
                audioSource,
                duration,
                targetVolume,
                EasingFunctions.GetEasingFunction(ease),
                waitWhilePredicate,
                cancellationToken);
        }

        internal static async
#if UNITASK_INCLUDED
            UniTask
#else
            Task
#endif
            FadeAsync(AudioSource audioSource,
                      float duration,
                      float targetVolume,
                      Func<float, float> easeFunction,
                      Func<bool> waitWhilePredicate = default,
                      CancellationToken cancellationToken = default)
        {
            targetVolume = Mathf.Clamp01(targetVolume);

            if (duration <= 0)
            {
                audioSource.volume = targetVolume;
                return;
            }

            float deltaTime = 0;
            float startVolume = audioSource.volume;

            while (deltaTime < duration)
            {
                deltaTime += Time.deltaTime;
                audioSource.volume = Mathf.Lerp(startVolume, targetVolume, easeFunction(deltaTime / duration));

                if (waitWhilePredicate != default)
                {
#if UNITASK_INCLUDED
                    await UniTask.WaitWhile(waitWhilePredicate, cancellationToken: cancellationToken);
#else
                    await TaskHelper.WaitWhile(waitWhilePredicate, cancellationToken);
#endif
                }
                else
                {
#if UNITASK_INCLUDED
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken: cancellationToken);
#else
                    await Task.Yield();
#endif
                }
            }

            audioSource.volume = targetVolume;
        }

        #endregion

        #region Mixer

        /// <summary>
        /// Registers a <see cref="MixerVolumeGroup"/> in the internal dictionary.
        /// </summary>
        /// <param name="group">The group to be registered.</param>
        /// <remarks>Once registered, grants access through various methods like <see cref="SetMixerGroupVolume"/> or <see cref="FadeMixerGroupVolume"/>.</remarks>
        public void RegisterMixerVolumeGroup(MixerVolumeGroup group)
        {
            _mixerVolumeGroups.TryAdd(group.ExposedParameter, group);
        }

        /// <summary>
        /// Unregisters a <see cref="MixerVolumeGroup"/> from the internal dictionary.
        /// </summary>
        /// <param name="group">The group to be unregistered.</param>
        public void UnregisterMixerVolumeGroup(MixerVolumeGroup group)
        {
            _mixerVolumeGroups.Remove(group.ExposedParameter);
        }

        /// <summary>
        /// Sets the volume for an Audio Mixer Group.
        /// </summary>
        /// <param name="exposedParameter">The exposed parameter with which to access the group, e.g. 'VolumeMusic'.</param>
        /// <param name="value">The volumes' new value.</param>
        /// <remarks>Changing a volume stops any ongoing volume fades applied in the mixer.</remarks>
        public void SetMixerGroupVolume(string exposedParameter, float value)
        {
            if (!MixerVolumeGroupRegistered(exposedParameter, out MixerVolumeGroup mixerVolumeGroup)) return;

            StopMixerFadeRoutine(exposedParameter);

            mixerVolumeGroup.Set(value);
        }

        /// <summary>
        /// Increases the volume of an Audio Mixer Group incrementally.
        /// </summary>
        /// <param name="exposedParameter">The exposed parameter with which to access the group, e.g. 'VolumeMusic'.</param>
        /// <remarks>Has no effect if no Volume Segments are defined in the <see cref="MixerVolumeGroup"/>.</remarks>
        public void IncreaseMixerGroupVolume(string exposedParameter)
        {
            if (!MixerVolumeGroupRegistered(exposedParameter, out MixerVolumeGroup mixerVolumeGroup)) return;

            StopMixerFadeRoutine(exposedParameter);

            mixerVolumeGroup.Increase();
        }

        /// <summary>
        /// Decreases the volume of an Audio Mixer Group incrementally.
        /// </summary>
        /// <param name="exposedParameter">The exposed parameter with which to access the group, e.g. 'VolumeMusic'.</param>
        /// <remarks>Has no effect if no Volume Segments are defined in the <see cref="MixerVolumeGroup"/>.</remarks>
        public void DecreaseMixerGroupVolume(string exposedParameter)
        {
            if (!MixerVolumeGroupRegistered(exposedParameter, out MixerVolumeGroup mixerVolumeGroup)) return;

            StopMixerFadeRoutine(exposedParameter);

            mixerVolumeGroup.Decrease();
        }

        /// <summary>
        /// Mutes/Un-mutes the volume of an Audio Mixer Group by setting the volume to 0 or reapplying the previously stored value.
        /// </summary>
        /// <param name="exposedParameter">The exposed parameter with which to access the group, e.g. 'VolumeMusic'.</param>
        /// <param name="value">True = muted, False = unmuted.</param>
        public void MuteMixerGroupVolume(string exposedParameter, bool value)
        {
            if (!MixerVolumeGroupRegistered(exposedParameter, out MixerVolumeGroup mixerVolumeGroup)) return;

            StopMixerFadeRoutine(exposedParameter);

            mixerVolumeGroup.Mute(value);
        }

        /// <summary>
        /// Fades the volume of an Audio Mixer Group.
        /// </summary>
        /// <param name="exposedParameter">The exposed parameter with which to access the group, e.g. 'VolumeMusic'.</param>
        /// <param name="targetVolume">The target volume reached at the end of the fade.</param>
        /// <param name="duration">The duration in seconds the fade will prolong.</param>
        /// <param name="ease">The easing applied when fading.</param>
        public void FadeMixerGroupVolume(string exposedParameter, float targetVolume, float duration, Ease ease)
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

        /// <summary>
        /// Linearly cross-fades the volume of two Audio Mixer Groups.
        /// </summary>
        /// <param name="fadeOutExposedParameter">The exposed parameter with which to access the group fading out, e.g. 'VolumeSFX'.</param>
        /// <param name="fadeInExposedParameter">The exposed parameter with which to access the group fading in, e.g. 'VolumeMusic'.</param>
        /// <param name="duration">The duration in seconds the cross-fade will prolong.</param>
        public void CrossFadeMixerGroupVolumes(string fadeOutExposedParameter,
                                               string fadeInExposedParameter,
                                               float duration)
        {
            FadeMixerGroupVolume(fadeOutExposedParameter, 0, duration, Ease.Linear);
            FadeMixerGroupVolume(fadeInExposedParameter, 1, duration, Ease.Linear);
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
        [Tooltip(
            "Optionally override the gameobjects' hide flags, if you want the gameobject e.g. not to be shown in the hierarchy." +
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
        internal static void PreAllocate<T>(this ObjectPool<T> pool, int capacity) where T : class
        {
            T[] preAllocatedT = new T[capacity];

            for (int i = 0; i < capacity; i++) preAllocatedT[i] = pool.Get();
            for (int i = preAllocatedT.Length - 1; i >= 0; i--) pool.Release(preAllocatedT[i]);
        }
    }

    internal static class TaskHelper
    {
        internal static async Task WaitWhile(Func<bool> waitWhilePredicate,
                                             CancellationToken cancellationToken = default)
        {
            while (waitWhilePredicate())
            {
                if (cancellationToken.IsCancellationRequested) return;

                await Task.Yield();
            }
        }

        internal static void Link(ref CancellationToken externalCancellationToken,
                                  ref CancellationTokenSource cancellationTokenSource)
        {
            cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                externalCancellationToken,
                cancellationTokenSource.Token);
        }

        internal static CancellationToken CancelAndRefresh(ref CancellationTokenSource cancellationTokenSource)
        {
            Cancel(ref cancellationTokenSource);

            cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Token.ThrowIfCancellationRequested();

            return cancellationTokenSource.Token;
        }

        internal static void Cancel(ref CancellationTokenSource cancellationTokenSource)
        {
            cancellationTokenSource?.Cancel();
            cancellationTokenSource?.Dispose();
            cancellationTokenSource = null;
        }
    }
}