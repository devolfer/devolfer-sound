using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Pool;
#if UNITASK_INCLUDED
using Cysharp.Threading.Tasks;
#endif
#if !UNITASK_INCLUDED
using System.Collections;
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

        private ObjectPool<SoundEntity> _soundEntityPool;

        private HashSet<SoundEntity> _entitiesPlaying;
        private HashSet<SoundEntity> _entitiesPaused;
        private HashSet<SoundEntity> _entitiesStopping;

        private Dictionary<AudioSource, SoundEntity> _audioSourcesPlaying;
        private Dictionary<AudioSource, SoundEntity> _audioSourcesPaused;
        private Dictionary<AudioSource, SoundEntity> _audioSourcesStopping;

        private Dictionary<string, MixerVolumeGroup> _mixerVolumeGroups;
        private Dictionary<string, CancellationTokenSource> _mixerFadeCancellationTokenSources;
#if !UNITASK_INCLUDED
        private Dictionary<string, Coroutine> _mixerFadeRoutines;
#endif

        #region Setup

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
            _audioSourcesPlaying = new Dictionary<AudioSource, SoundEntity>();
            _audioSourcesPaused = new Dictionary<AudioSource, SoundEntity>();
            _audioSourcesStopping = new Dictionary<AudioSource, SoundEntity>();

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
            _mixerFadeCancellationTokenSources = new Dictionary<string, CancellationTokenSource>();
#if !UNITASK_INCLUDED
            _mixerFadeRoutines = new Dictionary<string, Coroutine>();
#endif

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

        #endregion

        #region Entity

        /// <summary>
        /// Plays a sound with the specified sound properties.
        /// </summary>
        /// <param name="properties">The properties that define the sound.</param>
        /// <param name="followTarget">Optional target the sound will follow while playing.</param>
        /// <param name="position">Either the global position or, when following, the position offset at which the sound is played.</param>
        /// <param name="fadeIn">Optional volume fade in at the start of play.</param>
        /// <param name="fadeInDuration">The duration in seconds the fading in will prolong.</param>
        /// <param name="fadeInEase">The easing applied when fading in.</param>
        /// <param name="onComplete">Optional callback once sound completes playing (not applicable for looping sounds).</param>
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
        /// Plays a sound with the properties of an <see cref="AudioSource"/>.
        /// </summary>
        /// <param name="audioSource">The source of which the sound properties will be derived from.</param>
        /// <param name="followTarget">Optional target the sound will follow while playing.</param>
        /// <param name="position">Either the global position or, when following, the position offset at which the sound is played.</param>
        /// <param name="fadeIn">Optional volume fade in at the start of play.</param>
        /// <param name="fadeInDuration">The duration in seconds the fading in will prolong.</param>
        /// <param name="fadeInEase">The easing applied when fading in.</param>
        /// <param name="onComplete">Optional callback once sound completes playing (not applicable for looping sounds).</param>
        /// <returns>The <see cref="SoundEntity"/> used for playback.</returns>
        /// <remarks>The original <see cref="AudioSource"/> will be disabled.</remarks>
        public SoundEntity Play(AudioSource audioSource,
                                Transform followTarget = null,
                                Vector3 position = default,
                                bool fadeIn = false,
                                float fadeInDuration = .5f,
                                Ease fadeInEase = Ease.Linear,
                                Action onComplete = null)
        {
            SoundEntity entity = _soundEntityPool.Get();
            _entitiesPlaying.Add(entity);
            _audioSourcesPlaying.TryAdd(audioSource, entity);

            return entity.Play(audioSource, followTarget, position, fadeIn, fadeInDuration, fadeInEase, onComplete);
        }

        /// <summary>
        /// Plays a sound with the given <see cref="AudioClip"/>.
        /// </summary>
        /// <param name="audioClip">The clip to be played.</param>
        /// <param name="followTarget">Optional target the sound will follow while playing.</param>
        /// <param name="position">Either the global position or, when following, the position offset at which the sound is played.</param>
        /// <param name="fadeIn">Optional volume fade in at the start of play.</param>
        /// <param name="fadeInDuration">The duration in seconds the fading in will prolong.</param>
        /// <param name="fadeInEase">The easing applied when fading in.</param>
        /// <param name="onComplete">Optional callback once sound completes playing (not applicable for looping sounds).</param>
        /// <returns>The <see cref="SoundEntity"/> used for playback.</returns>
        public SoundEntity Play(AudioClip audioClip,
                                Transform followTarget = null,
                                Vector3 position = default,
                                bool fadeIn = false,
                                float fadeInDuration = .5f,
                                Ease fadeInEase = Ease.Linear,
                                Action onComplete = null)
        {
            return Play(
                new SoundProperties(audioClip),
                followTarget,
                position,
                fadeIn,
                fadeInDuration,
                fadeInEase,
                onComplete);
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
            PlayAsync(out SoundEntity entity,
                      AudioSource audioSource,
                      Transform followTarget = null,
                      Vector3 position = default,
                      bool fadeIn = false,
                      float fadeInDuration = .5f,
                      Ease fadeInEase = Ease.Linear,
                      CancellationToken cancellationToken = default)
        {
            entity = _soundEntityPool.Get();
            _entitiesPlaying.Add(entity);
            _audioSourcesPlaying.TryAdd(audioSource, entity);

            return entity.PlayAsync(
                audioSource,
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
                      AudioClip audioClip,
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
                new SoundProperties(audioClip),
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

        public
#if UNITASK_INCLUDED
            UniTask
#else
            Task
#endif
            PlayAsync(AudioClip audioClip,
                      Transform followTarget = null,
                      Vector3 position = default,
                      bool fadeIn = false,
                      float fadeInDuration = .5f,
                      Ease fadeInEase = Ease.Linear,
                      CancellationToken cancellationToken = default)
        {
            return PlayAsync(
                out _,
                new SoundProperties(audioClip),
                followTarget,
                position,
                fadeIn,
                fadeInDuration,
                fadeInEase,
                cancellationToken);
        }

        /// <summary>
        /// Pauses a playing sound.
        /// </summary>
        /// <param name="entity">The sound entity that is currently playing.</param>
        /// <remarks>Has no effect if the entity is currently stopping.</remarks>
        public void Pause(SoundEntity entity)
        {
            if (!_entitiesPlaying.Contains(entity)) return;
            if (_entitiesStopping.Contains(entity)) return;

            _entitiesPlaying.Remove(entity);

            entity.Pause();

            _entitiesPaused.Add(entity);
        }

        public void Pause(AudioSource audioSource)
        {
            if (_audioSourcesStopping.ContainsKey(audioSource)) return;
            if (!_audioSourcesPlaying.Remove(audioSource, out SoundEntity entity)) return;

            entity.Pause();

            _audioSourcesPaused.TryAdd(audioSource, entity);
        }

        /// <summary>
        /// Resumes a paused sound.
        /// </summary>
        /// <param name="entity">The sound entity that is currently paused.</param>
        /// <remarks>Has no effect if the entity is currently stopping.</remarks>
        public void Resume(SoundEntity entity)
        {
            if (!_entitiesPaused.Contains(entity)) return;
            if (_entitiesStopping.Contains(entity)) return;

            _entitiesPaused.Remove(entity);

            entity.Resume();

            _entitiesPlaying.Add(entity);
        }

        public void Resume(AudioSource audioSource)
        {
            if (_audioSourcesStopping.ContainsKey(audioSource)) return;
            if (!_audioSourcesPaused.Remove(audioSource, out SoundEntity entity)) return;

            entity.Resume();

            _audioSourcesPlaying.TryAdd(audioSource, entity);
        }

        /// <summary>
        /// Stops playback of a playing/paused sound.
        /// </summary>
        /// <param name="entity">The sound entity that is either currently playing or paused.</param>
        /// <param name="fadeOut">True by default. Set this to false, if the volume should not fade out when stopping.</param>
        /// <param name="fadeOutDuration">The duration in seconds the fading out will prolong.</param>
        /// <param name="fadeOutEase">The easing applied when fading out.</param>
        /// <param name="onComplete">Optional callback once sound completes stopping.</param>
        /// <remarks>Paused entities will be stopped without fade out regardless.</remarks>
        public void Stop(SoundEntity entity,
                         bool fadeOut = true,
                         float fadeOutDuration = .5f,
                         Ease fadeOutEase = Ease.Linear,
                         Action onComplete = null)
        {
            bool playingEntity = _entitiesPlaying.Contains(entity);
            bool pausedEntity = _entitiesPaused.Contains(entity);

            if (!playingEntity && !pausedEntity) return;

            _entitiesStopping.Add(entity);
            entity.Stop(fadeOut, fadeOutDuration, fadeOutEase, OnStopComplete);
            return;

            void OnStopComplete()
            {
                if (playingEntity) _entitiesPlaying.Remove(entity);
                if (pausedEntity) _entitiesPaused.Remove(entity);
                _entitiesStopping.Remove(entity);

                _soundEntityPool.Release(entity);

                onComplete?.Invoke();
            }
        }

        public void Stop(AudioSource audioSource,
                         bool fadeOut = true,
                         float fadeOutDuration = .5f,
                         Ease fadeOutEase = Ease.Linear,
                         Action onComplete = null)
        {
            bool playingAudioSource = _audioSourcesPlaying.Remove(audioSource, out SoundEntity entityPlaying);
            bool pausedAudioSource = _audioSourcesPaused.Remove(audioSource, out SoundEntity entityPaused);

            if (!playingAudioSource && !pausedAudioSource) return;

            SoundEntity entityToBeStopped = entityPlaying != null ? entityPlaying : entityPaused;
            _audioSourcesStopping.TryAdd(audioSource, entityToBeStopped);
            entityToBeStopped.Stop(fadeOut, fadeOutDuration, fadeOutEase, OnStopComplete);
            return;

            void OnStopComplete()
            {
                _audioSourcesStopping.Remove(audioSource);
                _soundEntityPool.Release(entityToBeStopped);

                onComplete?.Invoke();
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

            if (playingEntity) _entitiesPlaying.Remove(entity);
            if (pausedEntity) _entitiesPaused.Remove(entity);
            _entitiesStopping.Remove(entity);

            _soundEntityPool.Release(entity);
        }

        public async
#if UNITASK_INCLUDED
            UniTask
#else
            Task
#endif
            StopAsync(AudioSource audioSource,
                      bool fadeOut = true,
                      float fadeOutDuration = .5f,
                      Ease fadeOutEase = Ease.Linear,
                      CancellationToken cancellationToken = default)
        {
            bool playingAudioSource = _audioSourcesPlaying.Remove(audioSource, out SoundEntity entityPlaying);
            bool pausedAudioSource = _audioSourcesPaused.Remove(audioSource, out SoundEntity entityPaused);

            if (!playingAudioSource && !pausedAudioSource) return;

            SoundEntity entityToBeStopped = entityPlaying != null ? entityPlaying : entityPaused;
            _audioSourcesStopping.TryAdd(audioSource, entityToBeStopped);
            await entityToBeStopped.StopAsync(fadeOut, fadeOutDuration, fadeOutEase, cancellationToken);

            _audioSourcesStopping.Remove(audioSource);
            _soundEntityPool.Release(entityToBeStopped);
        }

        /// <summary>
        /// Pauses all currently playing sounds handled by the Sound Manager.
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

            foreach ((AudioSource audioSource, SoundEntity entity) in _audioSourcesPlaying)
            {
                if (_audioSourcesStopping.ContainsKey(audioSource)) continue;

                entity.Pause();
                _audioSourcesPaused.TryAdd(audioSource, entity);
            }

            _audioSourcesPlaying.Clear();
        }

        /// <summary>
        /// Resumes all currently paused sounds handled by the Sound Manager.
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

            foreach ((AudioSource audioSource, SoundEntity entity) in _audioSourcesPaused)
            {
                if (_audioSourcesStopping.ContainsKey(audioSource)) continue;

                entity.Resume();
                _audioSourcesPlaying.TryAdd(audioSource, entity);
            }

            _audioSourcesPaused.Clear();
        }

        /// <summary>
        /// Stops all currently playing and paused sounds handled by the Sound Manager.
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

            foreach ((AudioSource _, SoundEntity entity) in _audioSourcesPlaying)
            {
                Stop(entity, fadeOut, fadeOutDuration, fadeOutEase);
            }

            foreach ((AudioSource _, SoundEntity entity) in _audioSourcesPaused)
            {
                entity.Stop(false);
                _soundEntityPool.Release(entity);
            }

            _audioSourcesPaused.Clear();
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

            foreach ((AudioSource _, SoundEntity entity) in _audioSourcesPlaying)
            {
                stopTasks.Add(StopAsync(entity, fadeOut, fadeOutDuration, fadeOutEase, cancellationToken));
            }

            foreach ((AudioSource _, SoundEntity entity) in _audioSourcesPaused)
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
        /// Sets the properties of a playing or paused sound.
        /// </summary>
        /// <param name="entity">The entity whose properties will be changed.</param>
        /// <param name="properties">The new properties.</param>
        /// <param name="followTarget">The target the sound will follow while playing (none if null).</param>
        /// <param name="position">Either the global position or, when following, the position offset of the sound.</param>
        /// <remarks>Will change ALL properties, the followTarget and the position.
        /// Be sure to retrieve the original properties (e.g. via copy constructor), if you only want to change certain properties.</remarks>
        public void Set(SoundEntity entity,
                        SoundProperties properties,
                        Transform followTarget,
                        Vector3 position)
        {
            if (properties == null) return;

            entity.SetProperties(properties, followTarget, position);
        }

        /// <summary>
        /// Sets the properties of a playing or paused AudioSource that is handled by this SoundManager.
        /// </summary>
        /// <param name="audioSource">The source used to initiate playback via the manager.</param>
        /// <param name="properties">The new properties.</param>
        /// <param name="followTarget">The target the sound will follow while playing (none if null).</param>
        /// <param name="position">Either the global position or, when following, the position offset of the sound.</param>
        /// <remarks>Will change ALL properties, the followTarget and the position.
        /// Be sure to retrieve the original properties (e.g. via copy constructor), if you only want to change certain properties.</remarks>
        public void Set(AudioSource audioSource,
                        SoundProperties properties,
                        Transform followTarget,
                        Vector3 position)
        {
            if (properties == null) return;

            if (_audioSourcesPaused.TryGetValue(audioSource, out SoundEntity entityPaused))
            {
                entityPaused.SetProperties(properties, followTarget, position);
                return;
            }

            if (_audioSourcesPlaying.TryGetValue(audioSource, out SoundEntity entityPlaying))
            {
                entityPlaying.SetProperties(properties, followTarget, position);
            }
        }

        /// <summary>
        /// Fades the volume of a playing sound.
        /// </summary>
        /// <param name="entity">The sound entity that is currently playing or paused.</param>
        /// <param name="targetVolume">The target volume reached at the end of the fade.</param>
        /// <param name="duration">The duration in seconds the fade will prolong.</param>
        /// <param name="ease">The easing applied when fading.</param>
        /// <param name="onComplete">Optional callback once sound completes fading.</param>
        /// <remarks>Has no effect on entities currently stopping.</remarks>
        public void Fade(SoundEntity entity,
                         float targetVolume,
                         float duration,
                         Ease ease = Ease.Linear,
                         Action onComplete = null)
        {
            if (!_entitiesPlaying.Contains(entity)) return;
            if (_entitiesStopping.Contains(entity)) return;

            if (_entitiesPaused.Contains(entity)) Resume(entity);

            entity.Fade(targetVolume, duration, ease, onComplete);
        }

        public void Fade(AudioSource audioSource,
                         float targetVolume,
                         float duration,
                         Ease ease = Ease.Linear,
                         Action onComplete = null)
        {
            if (!_audioSourcesPlaying.TryGetValue(audioSource, out SoundEntity entityPlaying)) return;
            if (_audioSourcesStopping.ContainsKey(audioSource)) return;

            if (_audioSourcesPaused.TryGetValue(audioSource, out SoundEntity entityPaused)) Resume(entityPaused);

            entityPlaying.Fade(targetVolume, duration, ease, onComplete);
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

            return entity.FadeAsync(targetVolume, duration, ease, cancellationToken);
        }

        public
#if UNITASK_INCLUDED
            UniTask
#else
            Task
#endif
            FadeAsync(AudioSource audioSource,
                      float targetVolume,
                      float duration,
                      Ease ease = Ease.Linear,
                      CancellationToken cancellationToken = default)
        {
            if (!_audioSourcesPlaying.TryGetValue(audioSource, out SoundEntity entityPlaying)) return default;
            if (_audioSourcesStopping.ContainsKey(audioSource)) return default;

            if (_audioSourcesPaused.TryGetValue(audioSource, out SoundEntity entityPaused)) Resume(entityPaused);

            return entityPlaying.FadeAsync(targetVolume, duration, ease, cancellationToken);
        }

        /// <summary>
        /// Linearly cross-fades a playing sound entity and a new sound. The fading out sound entity will be stopped at the end.
        /// </summary>
        /// <param name="duration">The duration in seconds the cross-fade will prolong.</param>
        /// <param name="fadeOutEntity">The sound entity that will fade out and stop.</param>
        /// <param name="fadeInProperties">The properties that define the newly played sound.</param>
        /// <param name="followTarget">Optional target the new sound will follow while playing.</param>
        /// <param name="fadeInPosition">Either the global position or, when following, the position offset at which the new sound is played.</param>
        /// <param name="onComplete">Optional callback once sounds complete cross-fading.</param>
        /// <returns>The new <see cref="SoundEntity"/> fading in.</returns>
        /// <remarks>Simultaneously call Stop and Play methods for finer cross-fading control instead.</remarks>
        public SoundEntity CrossFade(float duration,
                                     SoundEntity fadeOutEntity,
                                     SoundProperties fadeInProperties,
                                     Transform followTarget = null,
                                     Vector3 fadeInPosition = default,
                                     Action onComplete = null)
        {
            Stop(fadeOutEntity, fadeOutDuration: duration);

            return Play(fadeInProperties, followTarget, fadeInPosition, true, duration, onComplete: onComplete);
        }

        public SoundEntity CrossFade(float duration,
                                     AudioSource fadeOutAudioSource,
                                     AudioSource fadeInAudioSource,
                                     Transform followTarget = null,
                                     Vector3 fadeInPosition = default,
                                     Action onComplete = null)
        {
            Stop(fadeOutAudioSource, fadeOutDuration: duration);

            return Play(fadeInAudioSource, followTarget, fadeInPosition, true, duration, onComplete: onComplete);
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

        public
#if UNITASK_INCLUDED
            UniTask
#else
            Task
#endif
            CrossFadeAsync(float duration,
                           SoundEntity fadeOutEntity,
                           SoundProperties fadeInProperties,
                           Transform followTarget = null,
                           Vector3 fadeInPosition = default,
                           CancellationToken cancellationToken = default)
        {
            _ = StopAsync(fadeOutEntity, fadeOutDuration: duration, cancellationToken: cancellationToken);

            return PlayAsync(
                out _,
                fadeInProperties,
                followTarget,
                fadeInPosition,
                true,
                duration,
                cancellationToken: cancellationToken);
        }

        public
#if UNITASK_INCLUDED
            UniTask
#else
            Task
#endif
            CrossFadeAsync(out SoundEntity entity,
                           float duration,
                           AudioSource fadeOutAudioSource,
                           AudioSource fadeInAudioSource,
                           Transform followTarget = null,
                           Vector3 fadeInPosition = default,
                           CancellationToken cancellationToken = default)
        {
            _ = StopAsync(fadeOutAudioSource, fadeOutDuration: duration, cancellationToken: cancellationToken);

            return PlayAsync(
                out entity,
                fadeInAudioSource,
                followTarget,
                fadeInPosition,
                true,
                duration,
                cancellationToken: cancellationToken);
        }

        public
#if UNITASK_INCLUDED
            UniTask
#else
            Task
#endif
            CrossFadeAsync(float duration,
                           AudioSource fadeOutAudioSource,
                           AudioSource fadeInAudioSource,
                           Transform followTarget = null,
                           Vector3 fadeInPosition = default,
                           CancellationToken cancellationToken = default)
        {
            _ = StopAsync(fadeOutAudioSource, fadeOutDuration: duration, cancellationToken: cancellationToken);

            return PlayAsync(
                out _,
                fadeInAudioSource,
                followTarget,
                fadeInPosition,
                true,
                duration,
                cancellationToken: cancellationToken);
        }

#if !UNITASK_INCLUDED
        internal static IEnumerator FadeRoutine(AudioSource audioSource,
                                                float duration,
                                                float targetVolume,
                                                Ease ease = Ease.Linear,
                                                WaitWhile waitWhilePredicate = null)
        {
            return FadeRoutine(
                audioSource,
                duration,
                targetVolume,
                EasingFunctions.GetEasingFunction(ease),
                waitWhilePredicate);
        }

        private static IEnumerator FadeRoutine(AudioSource audioSource,
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
#endif

        internal static
#if UNITASK_INCLUDED
            UniTask
#else
            Task
#endif
            FadeTask(AudioSource audioSource,
                     float duration,
                     float targetVolume,
                     Ease ease = Ease.Linear,
                     Func<bool> waitWhilePredicate = default,
                     CancellationToken cancellationToken = default)
        {
            return FadeTask(
                audioSource,
                duration,
                targetVolume,
                EasingFunctions.GetEasingFunction(ease),
                waitWhilePredicate,
                cancellationToken);
        }

        private static async
#if UNITASK_INCLUDED
            UniTask
#else
            Task
#endif
            FadeTask(AudioSource audioSource,
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
            if (!group.AudioMixer.HasParameter(group.ExposedParameter))
            {
                Debug.LogError(
                    $"You are trying to register a {nameof(MixerVolumeGroup)} with the non-existing exposed parameter " +
                    $"{group.ExposedParameter} for the Audio Mixer {group.AudioMixer}. " +
                    "Please add the necessary exposed parameter in the Audio Mixer in the Editor.");
                return;
            }

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

            StopMixerFading(exposedParameter);

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

            StopMixerFading(exposedParameter);

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

            StopMixerFading(exposedParameter);

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

            StopMixerFading(exposedParameter);

            mixerVolumeGroup.Mute(value);
        }

        /// <summary>
        /// Fades the volume of an Audio Mixer Group.
        /// </summary>
        /// <param name="exposedParameter">The exposed parameter with which to access the group, e.g. 'VolumeMusic'.</param>
        /// <param name="targetVolume">The target volume reached at the end of the fade.</param>
        /// <param name="duration">The duration in seconds the fade will prolong.</param>
        /// <param name="ease">The easing applied when fading.</param>
        /// <param name="onComplete">Optional callback once mixer completes fading.</param>
        public void FadeMixerGroupVolume(string exposedParameter,
                                         float targetVolume,
                                         float duration,
                                         Ease ease = Ease.Linear,
                                         Action onComplete = null)
        {
            if (!MixerVolumeGroupRegistered(exposedParameter, out MixerVolumeGroup mixerVolumeGroup)) return;

            StopMixerFading(exposedParameter);

#if UNITASK_INCLUDED
            CancellationTokenSource cts = new();
            _mixerFadeCancellationTokenSources.TryAdd(exposedParameter, cts);

            DoFadeTask(cts.Token).Forget();

            return;

            async UniTaskVoid DoFadeTask(CancellationToken cancellationToken)
            {
                await FadeMixerTask(mixerVolumeGroup, duration, targetVolume, ease, cancellationToken);

                onComplete?.Invoke();

                _mixerFadeCancellationTokenSources.Remove(exposedParameter);
            }
#else
            _mixerFadeRoutines.TryAdd(exposedParameter, StartCoroutine(DoFadeRoutine()));

            return;

            IEnumerator DoFadeRoutine()
            {
                yield return FadeMixerRoutine(mixerVolumeGroup, duration, targetVolume, ease);

                onComplete?.Invoke();

                _mixerFadeRoutines.Remove(exposedParameter);
            }
#endif
        }

        public async
#if UNITASK_INCLUDED
            UniTask
#else
            Task
#endif
            FadeMixerGroupVolumeAsync(string exposedParameter,
                                      float targetVolume,
                                      float duration,
                                      Ease ease = Ease.Linear,
                                      CancellationToken cancellationToken = default)
        {
            if (!MixerVolumeGroupRegistered(exposedParameter, out MixerVolumeGroup mixerVolumeGroup)) return;

            StopMixerFading(exposedParameter);

            CancellationTokenSource cts = new();
            TaskHelper.Link(ref cancellationToken, ref cts);

            _mixerFadeCancellationTokenSources.TryAdd(exposedParameter, cts);

            await FadeMixerTask(mixerVolumeGroup, duration, targetVolume, ease, cancellationToken);

            _mixerFadeCancellationTokenSources.Remove(exposedParameter);
        }

        /// <summary>
        /// Linearly cross-fades the volume of two Audio Mixer Groups.
        /// </summary>
        /// <param name="fadeOutExposedParameter">The exposed parameter with which to access the group fading out, e.g. 'VolumeSFX'.</param>
        /// <param name="fadeInExposedParameter">The exposed parameter with which to access the group fading in, e.g. 'VolumeMusic'.</param>
        /// <param name="duration">The duration in seconds the cross-fade will prolong.</param>
        /// <param name="onComplete">Optional callback once mixer completes cross-fading.</param>
        public void CrossFadeMixerGroupVolumes(string fadeOutExposedParameter,
                                               string fadeInExposedParameter,
                                               float duration,
                                               Action onComplete = null)
        {
            FadeMixerGroupVolume(fadeOutExposedParameter, 0, duration);
            FadeMixerGroupVolume(fadeInExposedParameter, 1, duration, onComplete: onComplete);
        }

        public
#if UNITASK_INCLUDED
            UniTask
#else
            Task
#endif
            CrossFadeMixerGroupVolumesAsync(string fadeOutExposedParameter,
                                            string fadeInExposedParameter,
                                            float duration,
                                            CancellationToken cancellationToken = default)
        {
            _ = FadeMixerGroupVolumeAsync(fadeOutExposedParameter, 0, duration, cancellationToken: cancellationToken);

            return FadeMixerGroupVolumeAsync(fadeInExposedParameter, 1, duration, cancellationToken: cancellationToken);
        }

#if !UNITASK_INCLUDED
        private static IEnumerator FadeMixerRoutine(MixerVolumeGroup mixerVolumeGroup,
                                                    float duration,
                                                    float targetVolume,
                                                    Ease ease)
        {
            return FadeMixerRoutine(mixerVolumeGroup, duration, targetVolume, EasingFunctions.GetEasingFunction(ease));
        }

        private static IEnumerator FadeMixerRoutine(MixerVolumeGroup mixerVolumeGroup,
                                                    float duration,
                                                    float targetVolume,
                                                    Func<float, float> easeFunction)
        {
            targetVolume = Mathf.Clamp01(targetVolume);

            if (duration <= 0)
            {
                mixerVolumeGroup.Set(targetVolume);
                yield break;
            }

            float deltaTime = 0;
            float startVolume = mixerVolumeGroup.VolumeCurrent;

            while (deltaTime < duration)
            {
                deltaTime += Time.deltaTime;
                mixerVolumeGroup.Set(Mathf.Lerp(startVolume, targetVolume, easeFunction(deltaTime / duration)));

                yield return null;
            }

            mixerVolumeGroup.Set(targetVolume);
        }
#endif

        private static
#if UNITASK_INCLUDED
            UniTask
#else
            Task
#endif
            FadeMixerTask(MixerVolumeGroup mixerVolumeGroup,
                          float duration,
                          float targetVolume,
                          Ease ease,
                          CancellationToken cancellationToken = default)
        {
            return FadeMixerTask(
                mixerVolumeGroup,
                duration,
                targetVolume,
                EasingFunctions.GetEasingFunction(ease),
                cancellationToken);
        }

        private static async
#if UNITASK_INCLUDED
            UniTask
#else
            Task
#endif
            FadeMixerTask(MixerVolumeGroup mixerVolumeGroup,
                          float duration,
                          float targetVolume,
                          Func<float, float> easeFunction,
                          CancellationToken cancellationToken = default)
        {
            targetVolume = Mathf.Clamp01(targetVolume);

            if (duration <= 0)
            {
                mixerVolumeGroup.Set(targetVolume);
                return;
            }

            float deltaTime = 0;
            float startVolume = mixerVolumeGroup.VolumeCurrent;

            while (deltaTime < duration)
            {
                deltaTime += Time.deltaTime;
                mixerVolumeGroup.Set(Mathf.Lerp(startVolume, targetVolume, easeFunction(deltaTime / duration)));

#if UNITASK_INCLUDED
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken: cancellationToken);
#else
                await Task.Yield();
#endif
            }

            mixerVolumeGroup.Set(targetVolume);
        }

        private bool MixerVolumeGroupRegistered(string exposedParameter, out MixerVolumeGroup mixerVolumeGroup)
        {
            if (_mixerVolumeGroups.TryGetValue(exposedParameter, out mixerVolumeGroup)) return true;

            Debug.LogError($"There is no {nameof(MixerVolumeGroup)} for {exposedParameter} registered.");
            return false;
        }

        private void StopMixerFading(string exposedParameter)
        {
#if !UNITASK_INCLUDED
            if (_mixerFadeRoutines.Remove(exposedParameter, out Coroutine fadeRoutine))
            {
                StopCoroutine(fadeRoutine);
            }
#endif

            if (_mixerFadeCancellationTokenSources.Remove(exposedParameter, out CancellationTokenSource cts))
            {
                TaskHelper.Cancel(ref cts);
            }
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

        internal static CancellationTokenSource Link(ref CancellationToken externalCancellationToken,
                                                     ref CancellationTokenSource cancellationTokenSource)
        {
            return cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
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