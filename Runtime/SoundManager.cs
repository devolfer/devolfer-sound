using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Pool;

#if UNITASK_INCLUDED
using Cysharp.Threading.Tasks;
using DynamicTask = Cysharp.Threading.Tasks.UniTask;
#endif

#if !UNITASK_INCLUDED
using System.Collections;
using System.Threading.Tasks;
using DynamicTask = System.Threading.Tasks.Task;
#endif

namespace Devolfer.Sound
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

        private bool _setup;
        
        private ObjectPool<SoundEntity> _soundEntityPool;

        private Dictionary<SoundEntity, AudioSource> _entitiesPlaying;
        private Dictionary<AudioSource, SoundEntity> _sourcesPlaying;
        private Dictionary<SoundEntity, AudioSource> _entitiesPaused;
        private Dictionary<AudioSource, SoundEntity> _sourcesPaused;
        private Dictionary<SoundEntity, AudioSource> _entitiesStopping;
        private Dictionary<AudioSource, SoundEntity> _sourcesStopping;

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

            SetupIfNeeded();
        }

        private void SetupIfNeeded()
        {
            if (_setup) return;
            
            _setup = true;
            
            SetupSoundEntities();
            SetupMixers();
        }

        private void SetupSoundEntities()
        {
            _entitiesPlaying = new Dictionary<SoundEntity, AudioSource>();
            _sourcesPlaying = new Dictionary<AudioSource, SoundEntity>();
            _entitiesPaused = new Dictionary<SoundEntity, AudioSource>();
            _sourcesPaused = new Dictionary<AudioSource, SoundEntity>();
            _entitiesStopping = new Dictionary<SoundEntity, AudioSource>();
            _sourcesStopping = new Dictionary<AudioSource, SoundEntity>();

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
        /// Plays a sound with the specified <see cref="SoundProperties"/>.
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
            SetupIfNeeded();
            
            SoundEntity entity = _soundEntityPool.Get();

            entity.Play(properties, followTarget, position, fadeIn, fadeInDuration, fadeInEase, onComplete);
            AddPlaying(entity);

            return entity;
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
            SetupIfNeeded();
            
            if (HasPlaying(audioSource, out SoundEntity playingEntity)) return playingEntity;
            
            if (HasPaused(audioSource, out SoundEntity pausedEntity))
            {
                Resume(pausedEntity);
                return pausedEntity;
            }
            
            SoundEntity entity = _soundEntityPool.Get();

            entity.Play(audioSource, followTarget, position, fadeIn, fadeInDuration, fadeInEase, onComplete);
            AddPlaying(entity);

            return entity;
        }

        /// <summary>
        /// Plays a sound with the specified <see cref="AudioClip"/>.
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

        /// <summary>
        /// Asynchronously plays a sound with the specified <see cref="SoundProperties"/>.
        /// </summary>
        /// <param name="entity">The <see cref="SoundEntity"/> used for playback.</param>
        /// <param name="properties">The properties that define the sound.</param>
        /// <param name="followTarget">Optional target the sound will follow while playing.</param>
        /// <param name="position">Either the global position or, when following, the position offset at which the sound is played.</param>
        /// <param name="fadeIn">Optional volume fade in at the start of play.</param>
        /// <param name="fadeInDuration">The duration in seconds the fading in will prolong.</param>
        /// <param name="fadeInEase">The easing applied when fading in.</param>
        /// <param name="cancellationToken">Optional token for cancelling the playback.</param>
        /// <returns>The playback task.</returns>
        public DynamicTask PlayAsync(out SoundEntity entity,
                                     SoundProperties properties,
                                     Transform followTarget = null,
                                     Vector3 position = default,
                                     bool fadeIn = false,
                                     float fadeInDuration = .5f,
                                     Ease fadeInEase = Ease.Linear,
                                     CancellationToken cancellationToken = default)
        {
            SetupIfNeeded();
            
            entity = _soundEntityPool.Get();

            DynamicTask task = entity.PlayAsync(
                properties,
                followTarget,
                position,
                fadeIn,
                fadeInDuration,
                fadeInEase,
                cancellationToken);

            AddPlaying(entity);

            return task;
        }

        /// <summary>
        /// Asynchronously plays a sound with the properties of an <see cref="AudioSource"/>.
        /// </summary>
        /// <param name="entity">The <see cref="SoundEntity"/> used for playback.</param>
        /// <param name="audioSource">The source of which the sound properties will be derived from.</param>
        /// <param name="followTarget">Optional target the sound will follow while playing.</param>
        /// <param name="position">Either the global position or, when following, the position offset at which the sound is played.</param>
        /// <param name="fadeIn">Optional volume fade in at the start of play.</param>
        /// <param name="fadeInDuration">The duration in seconds the fading in will prolong.</param>
        /// <param name="fadeInEase">The easing applied when fading in.</param>
        /// <param name="cancellationToken">Optional token for cancelling the playback.</param>
        /// <returns>The playback task.</returns>
        public DynamicTask PlayAsync(out SoundEntity entity,
                                     AudioSource audioSource,
                                     Transform followTarget = null,
                                     Vector3 position = default,
                                     bool fadeIn = false,
                                     float fadeInDuration = .5f,
                                     Ease fadeInEase = Ease.Linear,
                                     CancellationToken cancellationToken = default)
        {
            SetupIfNeeded();
            
            entity = _soundEntityPool.Get();

            DynamicTask task = entity.PlayAsync(
                audioSource,
                followTarget,
                position,
                fadeIn,
                fadeInDuration,
                fadeInEase,
                cancellationToken);

            AddPlaying(entity);

            return task;
        }

        /// <summary>
        /// Asynchronously plays a sound with the specified <see cref="AudioClip"/>.
        /// </summary>
        /// <param name="entity">The <see cref="SoundEntity"/> used for playback.</param>
        /// <param name="audioClip">The clip to be played.</param>
        /// <param name="followTarget">Optional target the sound will follow while playing.</param>
        /// <param name="position">Either the global position or, when following, the position offset at which the sound is played.</param>
        /// <param name="fadeIn">Optional volume fade in at the start of play.</param>
        /// <param name="fadeInDuration">The duration in seconds the fading in will prolong.</param>
        /// <param name="fadeInEase">The easing applied when fading in.</param>
        /// <param name="cancellationToken">Optional token for cancelling the playback.</param>
        /// <returns>The playback task.</returns>
        public DynamicTask PlayAsync(out SoundEntity entity,
                                     AudioClip audioClip,
                                     Transform followTarget = null,
                                     Vector3 position = default,
                                     bool fadeIn = false,
                                     float fadeInDuration = .5f,
                                     Ease fadeInEase = Ease.Linear,
                                     CancellationToken cancellationToken = default)
        {
            SetupIfNeeded();
            
            entity = _soundEntityPool.Get();

            DynamicTask task = entity.PlayAsync(
                new SoundProperties(audioClip),
                followTarget,
                position,
                fadeIn,
                fadeInDuration,
                fadeInEase,
                cancellationToken);

            AddPlaying(entity);

            return task;
        }

        /// <summary>
        /// Asynchronously plays a sound with the specified <see cref="SoundProperties"/>.
        /// </summary>
        /// <param name="properties">The properties that define the sound.</param>
        /// <param name="followTarget">Optional target the sound will follow while playing.</param>
        /// <param name="position">Either the global position or, when following, the position offset at which the sound is played.</param>
        /// <param name="fadeIn">Optional volume fade in at the start of play.</param>
        /// <param name="fadeInDuration">The duration in seconds the fading in will prolong.</param>
        /// <param name="fadeInEase">The easing applied when fading in.</param>
        /// <param name="cancellationToken">Optional token for cancelling the playback.</param>
        /// <returns>The playback task.</returns>
        public DynamicTask PlayAsync(SoundProperties properties,
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

        /// <summary>
        /// Asynchronously plays a sound with the properties of an <see cref="AudioSource"/>.
        /// </summary>
        /// <param name="audioSource">The source of which the sound properties will be derived from.</param>
        /// <param name="followTarget">Optional target the sound will follow while playing.</param>
        /// <param name="position">Either the global position or, when following, the position offset at which the sound is played.</param>
        /// <param name="fadeIn">Optional volume fade in at the start of play.</param>
        /// <param name="fadeInDuration">The duration in seconds the fading in will prolong.</param>
        /// <param name="fadeInEase">The easing applied when fading in.</param>
        /// <param name="cancellationToken">Optional token for cancelling the playback.</param>
        /// <returns>The playback task.</returns>
        public DynamicTask PlayAsync(AudioSource audioSource,
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
        /// Asynchronously plays a sound with the specified <see cref="AudioClip"/>.
        /// </summary>
        /// <param name="audioClip">The clip to be played.</param>
        /// <param name="followTarget">Optional target the sound will follow while playing.</param>
        /// <param name="position">Either the global position or, when following, the position offset at which the sound is played.</param>
        /// <param name="fadeIn">Optional volume fade in at the start of play.</param>
        /// <param name="fadeInDuration">The duration in seconds the fading in will prolong.</param>
        /// <param name="fadeInEase">The easing applied when fading in.</param>
        /// <param name="cancellationToken">Optional token for cancelling the playback.</param>
        /// <returns>The playback task.</returns>
        public DynamicTask PlayAsync(AudioClip audioClip,
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
        /// Pauses a playing sound handled by the Sound Manager.
        /// </summary>
        /// <param name="entity">The sound entity that is currently playing.</param>
        /// <returns>True, if pausing was successful.</returns>
        /// <remarks>Has no effect if the entity is currently stopping.</remarks>
        public bool Pause(SoundEntity entity)
        {
            SetupIfNeeded();
            
            if (!HasPlaying(entity)) return false;
            if (HasStopping(entity)) return false;

            RemovePlaying(entity);

            entity.Pause();

            AddPaused(entity);

            return true;
        }

        /// <summary>
        /// Pauses a playing sound handled by the Sound Manager.
        /// </summary>
        /// <param name="audioSource">The source of the sound.</param>
        /// <returns>True, if pausing was successful.</returns>
        /// <remarks>Has no effect if the source is currently stopping.</remarks>
        public bool Pause(AudioSource audioSource)
        {
            SetupIfNeeded();
            
            if (HasStopping(audioSource)) return false;
            if (!HasPlaying(audioSource, out SoundEntity entity)) return false;

            RemovePlaying(entity);
            
            entity.Pause();

            AddPaused(entity);
            
            return true;
        }

        /// <summary>
        /// Resumes a paused sound handled by the Sound Manager.
        /// </summary>
        /// <param name="entity">The sound entity that is currently paused.</param>
        /// <returns>True, if resuming was successful.</returns>
        /// <remarks>Has no effect if the entity is currently stopping.</remarks>
        public bool Resume(SoundEntity entity)
        {
            SetupIfNeeded();
            
            if (!HasPaused(entity)) return false;
            if (HasStopping(entity)) return false;

            RemovePaused(entity);

            entity.Resume();

            AddPlaying(entity);
            
            return true;
        }

        /// <summary>
        /// Resumes a paused sound handled by the Sound Manager.
        /// </summary>
        /// <param name="audioSource">The source of the sound.</param>
        /// <returns>True, if resuming was successful.</returns>
        /// <remarks>Has no effect if the source is currently stopping.</remarks>
        public bool Resume(AudioSource audioSource)
        {
            SetupIfNeeded();
            
            if (HasStopping(audioSource)) return false;
            if (!HasPaused(audioSource, out SoundEntity entity)) return false;
            
            RemovePaused(entity);

            entity.Resume();

            AddPlaying(entity);

            return true;
        }

        /// <summary>
        /// Stops playback of a playing/paused sound handled by the Sound Manager.
        /// </summary>
        /// <param name="entity">The sound entity that is either currently playing or paused.</param>
        /// <param name="fadeOut">True by default. Set this to false, if the volume should not fade out when stopping.</param>
        /// <param name="fadeOutDuration">The duration in seconds the fading out will prolong.</param>
        /// <param name="fadeOutEase">The easing applied when fading out.</param>
        /// <param name="onComplete">Optional callback once sound completes stopping.</param>
        /// <remarks>Paused sounds will be stopped without fade out regardless.</remarks>
        public void Stop(SoundEntity entity,
                         bool fadeOut = true,
                         float fadeOutDuration = .5f,
                         Ease fadeOutEase = Ease.Linear,
                         Action onComplete = null)
        {
            SetupIfNeeded();
            
            bool playingEntity = HasPlaying(entity);
            bool pausedEntity = HasPaused(entity);

            if (!playingEntity && !pausedEntity) return;

            AddStopping(entity);
            entity.Stop(fadeOut, fadeOutDuration, fadeOutEase, OnStopComplete);
            
            return;

            void OnStopComplete()
            {
                if (playingEntity) RemovePlaying(entity);
                if (pausedEntity) RemovePaused(entity);
                RemoveStopping(entity);

                _soundEntityPool.Release(entity);

                onComplete?.Invoke();
            }
        }

        /// <summary>
        /// Stops playback of a playing/paused sound handled by the Sound Manager.
        /// </summary>
        /// <param name="audioSource">The source that is either currently playing or paused.</param>
        /// <param name="fadeOut">True by default. Set this to false, if the volume should not fade out when stopping.</param>
        /// <param name="fadeOutDuration">The duration in seconds the fading out will prolong.</param>
        /// <param name="fadeOutEase">The easing applied when fading out.</param>
        /// <param name="onComplete">Optional callback once sound completes stopping.</param>
        /// <remarks>Paused sounds will be stopped without fade out regardless.</remarks>
        public void Stop(AudioSource audioSource,
                         bool fadeOut = true,
                         float fadeOutDuration = .5f,
                         Ease fadeOutEase = Ease.Linear,
                         Action onComplete = null)
        {
            SetupIfNeeded();
            
            bool playingAudioSource = HasPlaying(audioSource, out SoundEntity entityPlaying);
            bool pausedAudioSource = HasPaused(audioSource, out SoundEntity entityPaused);

            if (!playingAudioSource && !pausedAudioSource) return;

            SoundEntity entity = entityPlaying != null ? entityPlaying : entityPaused;
            AddStopping(entity);
            entity.Stop(fadeOut, fadeOutDuration, fadeOutEase, OnStopComplete);
            
            return;

            void OnStopComplete()
            {
                if (playingAudioSource) RemovePlaying(entity);
                if (pausedAudioSource) RemovePaused(entity);
                RemoveStopping(entity);
                
                _soundEntityPool.Release(entity);

                onComplete?.Invoke();
            }
        }

        /// <summary>
        /// Asynchronously stops playback of a playing/paused sound handled by the Sound Manager.
        /// </summary>
        /// <param name="entity">The sound entity that is either currently playing or paused.</param>
        /// <param name="fadeOut">True by default. Set this to false, if the volume should not fade out when stopping.</param>
        /// <param name="fadeOutDuration">The duration in seconds the fading out will prolong.</param>
        /// <param name="fadeOutEase">The easing applied when fading out.</param>
        /// <param name="cancellationToken">Optional token for cancelling the stopping.</param>
        /// <returns>The stopping task.</returns>
        /// <remarks>Paused sounds will be stopped without fade out regardless.</remarks>
        public async DynamicTask StopAsync(SoundEntity entity,
                                           bool fadeOut = true,
                                           float fadeOutDuration = .5f,
                                           Ease fadeOutEase = Ease.Linear,
                                           CancellationToken cancellationToken = default)
        {
            SetupIfNeeded();
            
            bool playingEntity = HasPlaying(entity);
            bool pausedEntity = HasPaused(entity);

            if (!playingEntity && !pausedEntity) return;

            AddStopping(entity);
            
            await entity.StopAsync(fadeOut, fadeOutDuration, fadeOutEase, cancellationToken);

            if (playingEntity) RemovePlaying(entity);
            if (pausedEntity) RemovePaused(entity);
            RemoveStopping(entity);

            _soundEntityPool.Release(entity);
        }

        /// <summary>
        /// Asynchronously stops playback of a playing/paused sound handled by the Sound Manager.
        /// </summary>
        /// <param name="audioSource">The source that is either currently playing or paused.</param>
        /// <param name="fadeOut">True by default. Set this to false, if the volume should not fade out when stopping.</param>
        /// <param name="fadeOutDuration">The duration in seconds the fading out will prolong.</param>
        /// <param name="fadeOutEase">The easing applied when fading out.</param>
        /// <param name="cancellationToken">Optional token for cancelling the stopping.</param>
        /// <returns>The stopping task.</returns>
        /// <remarks>Paused sounds will be stopped without fade out regardless.</remarks>
        public async DynamicTask StopAsync(AudioSource audioSource,
                                           bool fadeOut = true,
                                           float fadeOutDuration = .5f,
                                           Ease fadeOutEase = Ease.Linear,
                                           CancellationToken cancellationToken = default)
        {
            SetupIfNeeded();
            
            bool playingAudioSource = HasPlaying(audioSource, out SoundEntity entityPlaying);
            bool pausedAudioSource = HasPaused(audioSource, out SoundEntity entityPaused);

            if (!playingAudioSource && !pausedAudioSource) return;

            SoundEntity entity = entityPlaying != null ? entityPlaying : entityPaused;
            AddStopping(entity);
            
            await entity.StopAsync(fadeOut, fadeOutDuration, fadeOutEase, cancellationToken);

            if (playingAudioSource) RemovePlaying(entity);
            if (pausedAudioSource) RemovePaused(entity);
            RemoveStopping(entity);
            
            _soundEntityPool.Release(entity);
        }

        /// <summary>
        /// Pauses all currently playing sounds handled by the Sound Manager.
        /// </summary>
        /// <remarks>Has no effect on sounds currently stopping.</remarks>
        public bool PauseAll()
        {
            SetupIfNeeded();

            bool successful = false;
            
            foreach ((SoundEntity entity, AudioSource _) in _entitiesPlaying)
            {
                if (HasStopping(entity)) continue;
                
                entity.Pause();
                AddPaused(entity);
                successful = true;
            }
            
            ClearPlayingEntities();
            
            return successful;
        }

        /// <summary>
        /// Resumes all currently paused sounds handled by the Sound Manager.
        /// </summary>
        public bool ResumeAll()
        {
            SetupIfNeeded();
            
            bool successful = false;
            
            foreach ((SoundEntity entity, AudioSource _) in _entitiesPaused)
            {
                if (HasStopping(entity)) continue;
                
                entity.Resume();
                AddPlaying(entity);
                successful = true;
            }
            
            ClearPausedEntities();
            
            return successful;
        }

        /// <summary>
        /// Stops all currently playing/paused sounds handled by the Sound Manager.
        /// </summary>
        /// <param name="fadeOut">True by default. Set this to false, if the volumes should not fade out when stopping.</param>
        /// <param name="fadeOutDuration">The duration in seconds the fading out will prolong.</param>
        /// <param name="fadeOutEase">The easing applied when fading out.</param>
        public void StopAll(bool fadeOut = true,
                            float fadeOutDuration = 1,
                            Ease fadeOutEase = Ease.Linear)
        {
            SetupIfNeeded();
            
            foreach ((SoundEntity entity, AudioSource _) in _entitiesPlaying)
            {
                Stop(entity, fadeOut, fadeOutDuration, fadeOutEase);
            }

            foreach ((SoundEntity entity, AudioSource _) in _entitiesPaused)
            {
                entity.Stop(false);
                _soundEntityPool.Release(entity);
            }

            ClearPausedEntities();
        }

        /// <summary>
        /// Asynchronously stops all currently playing/paused sounds handled by the Sound Manager.
        /// </summary>
        /// <param name="fadeOut">True by default. Set this to false, if the volumes should not fade out when stopping.</param>
        /// <param name="fadeOutDuration">The duration in seconds the fading out will prolong.</param>
        /// <param name="fadeOutEase">The easing applied when fading out.</param>
        /// <param name="cancellationToken">Optional token for cancelling the stopping.</param>
        /// <returns>The stopping task.</returns>
        public async DynamicTask StopAllAsync(bool fadeOut = true,
                                              float fadeOutDuration = 1,
                                              Ease fadeOutEase = Ease.Linear,
                                              CancellationToken cancellationToken = default)
        {
            SetupIfNeeded();
            
            List<DynamicTask> stopTasks = new();

            foreach ((SoundEntity entity, AudioSource _) in _entitiesPlaying)
            {
                stopTasks.Add(StopAsync(entity, fadeOut, fadeOutDuration, fadeOutEase, cancellationToken));
            }

            foreach ((SoundEntity entity, AudioSource _) in _entitiesPaused)
            {
                stopTasks.Add(entity.StopAsync(false, cancellationToken: cancellationToken));
                _soundEntityPool.Release(entity);
            }

            ClearPausedEntities();

            await DynamicTask.WhenAll(stopTasks);
        }

        /// <summary>
        /// Sets the properties of a playing/paused sound.
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
            SetupIfNeeded();
            
            if (properties == null) return;

            entity.SetProperties(properties, followTarget, position);
        }

        /// <summary>
        /// Sets the properties of a playing/paused AudioSource that is handled by the Sound Manager.
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
            SetupIfNeeded();
            
            if (properties == null) return;

            if (HasPaused(audioSource, out SoundEntity entityPaused))
            {
                entityPaused.SetProperties(properties, followTarget, position);
                return;
            }

            if (HasPlaying(audioSource, out SoundEntity entityPlaying))
            {
                entityPlaying.SetProperties(properties, followTarget, position);
            }
        }

        /// <summary>
        /// Fades the volume of a playing sound handled by the Sound Manager.
        /// </summary>
        /// <param name="entity">The sound entity that is currently playing or paused.</param>
        /// <param name="targetVolume">The target volume reached at the end of the fade.</param>
        /// <param name="duration">The duration in seconds the fade will prolong.</param>
        /// <param name="ease">The easing applied when fading.</param>
        /// <param name="onComplete">Optional callback once sound completes fading.</param>
        /// <remarks>Has no effect on sounds currently stopping.</remarks>
        public void Fade(SoundEntity entity,
                         float targetVolume,
                         float duration,
                         Ease ease = Ease.Linear,
                         Action onComplete = null)
        {
            SetupIfNeeded();
            
            if (!HasPlaying(entity)) return;
            if (HasStopping(entity)) return;

            if (HasPaused(entity)) Resume(entity);

            entity.Fade(targetVolume, duration, ease, onComplete);
        }

        /// <summary>
        /// Fades the volume of a playing sound handled by the Sound Manager.
        /// </summary>
        /// <param name="audioSource">The source that is currently playing or paused.</param>
        /// <param name="targetVolume">The target volume reached at the end of the fade.</param>
        /// <param name="duration">The duration in seconds the fade will prolong.</param>
        /// <param name="ease">The easing applied when fading.</param>
        /// <param name="onComplete">Optional callback once sound completes fading.</param>
        /// <remarks>Has no effect on sounds currently stopping.</remarks>
        public void Fade(AudioSource audioSource,
                         float targetVolume,
                         float duration,
                         Ease ease = Ease.Linear,
                         Action onComplete = null)
        {
            SetupIfNeeded();
            
            if (!HasPlaying(audioSource, out SoundEntity entityPlaying)) return;
            if (HasStopping(audioSource)) return;

            if (HasPaused(audioSource, out SoundEntity entityPaused)) Resume(entityPaused);

            entityPlaying.Fade(targetVolume, duration, ease, onComplete);
        }

        /// <summary>
        /// Asynchronously fades the volume of a playing sound handled by the Sound Manager.
        /// </summary>
        /// <param name="entity">The sound entity that is currently playing or paused.</param>
        /// <param name="targetVolume">The target volume reached at the end of the fade.</param>
        /// <param name="duration">The duration in seconds the fade will prolong.</param>
        /// <param name="ease">The easing applied when fading.</param>
        /// <param name="cancellationToken">Optional token for cancelling the fading.</param>
        /// <returns>The fading task.</returns>
        /// <remarks>Has no effect on sounds currently stopping.</remarks>
        public DynamicTask FadeAsync(SoundEntity entity,
                                     float targetVolume,
                                     float duration,
                                     Ease ease = Ease.Linear,
                                     CancellationToken cancellationToken = default)
        {
            SetupIfNeeded();
            
            if (HasStopping(entity)) return default;

            if (HasPaused(entity)) Resume(entity);

            return entity.FadeAsync(targetVolume, duration, ease, cancellationToken);
        }

        /// <summary>
        /// Asynchronously fades the volume of a playing sound handled by the Sound Manager.
        /// </summary>
        /// <param name="audioSource">The source that is currently playing or paused.</param>
        /// <param name="targetVolume">The target volume reached at the end of the fade.</param>
        /// <param name="duration">The duration in seconds the fade will prolong.</param>
        /// <param name="ease">The easing applied when fading.</param>
        /// <param name="cancellationToken">Optional token for cancelling the fading.</param>
        /// <returns>The fading task.</returns>
        /// <remarks>Has no effect on sounds currently stopping.</remarks>
        public DynamicTask FadeAsync(AudioSource audioSource,
                                     float targetVolume,
                                     float duration,
                                     Ease ease = Ease.Linear,
                                     CancellationToken cancellationToken = default)
        {
            SetupIfNeeded();
            
            if (!HasPlaying(audioSource, out SoundEntity entityPlaying)) return default;
            if (HasStopping(audioSource)) return default;

            if (HasPaused(audioSource, out SoundEntity entityPaused)) Resume(entityPaused);

            return entityPlaying.FadeAsync(targetVolume, duration, ease, cancellationToken);
        }

        /// <summary>
        /// Linearly cross-fades a playing sound handled by the Sound Manager and a new sound. The fading out sound will be stopped at the end.
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

        /// <summary>
        /// Linearly cross-fades a playing sound handled by the Sound Manager and a new sound. The fading out sound will be stopped at the end.
        /// </summary>
        /// <param name="duration">The duration in seconds the cross-fade will prolong.</param>
        /// <param name="fadeOutAudioSource">The source that will fade out and stop.</param>
        /// <param name="fadeInAudioSource">The source that defines the newly played sound.</param>
        /// <param name="followTarget">Optional target the new sound will follow while playing.</param>
        /// <param name="fadeInPosition">Either the global position or, when following, the position offset at which the new sound is played.</param>
        /// <param name="onComplete">Optional callback once sounds complete cross-fading.</param>
        /// <returns>The new <see cref="SoundEntity"/> fading in.</returns>
        /// <remarks>Simultaneously call Stop and Play methods for finer cross-fading control instead.</remarks>
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

        /// <summary>
        /// Asynchronously and linearly cross-fades a playing sound handled by the Sound Manager and a new sound. The fading out sound will be stopped at the end.
        /// </summary>
        /// <param name="entity">The <see cref="SoundEntity"/> used for playback of the new sound.</param>
        /// <param name="duration">The duration in seconds the cross-fade will prolong.</param>
        /// <param name="fadeOutEntity">The sound entity that will fade out and stop.</param>
        /// <param name="fadeInProperties">The properties that define the newly played sound.</param>
        /// <param name="followTarget">Optional target the new sound will follow while playing.</param>
        /// <param name="fadeInPosition">Either the global position or, when following, the position offset at which the new sound is played.</param>
        /// <param name="cancellationToken">Optional token for cancelling the cross-fading.</param>
        /// <returns>The cross-fading task.</returns>
        /// <remarks>Simultaneously call Stop and Play methods for finer cross-fading control instead.</remarks>
        public DynamicTask CrossFadeAsync(out SoundEntity entity,
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

        /// <summary>
        /// Asynchronously and linearly cross-fades a playing sound handled by the Sound Manager and a new sound. The fading out sound will be stopped at the end.
        /// </summary>
        /// <param name="duration">The duration in seconds the cross-fade will prolong.</param>
        /// <param name="fadeOutEntity">The sound entity that will fade out and stop.</param>
        /// <param name="fadeInProperties">The properties that define the newly played sound.</param>
        /// <param name="followTarget">Optional target the new sound will follow while playing.</param>
        /// <param name="fadeInPosition">Either the global position or, when following, the position offset at which the new sound is played.</param>
        /// <param name="cancellationToken">Optional token for cancelling the cross-fading.</param>
        /// <returns>The cross-fading task.</returns>
        /// <remarks>Simultaneously call Stop and Play methods for finer cross-fading control instead.</remarks>
        public DynamicTask CrossFadeAsync(float duration,
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

        /// <summary>
        /// Asynchronously and linearly cross-fades a playing sound handled by the Sound Manager and a new sound. The fading out sound will be stopped at the end.
        /// </summary>
        /// <param name="entity">The <see cref="SoundEntity"/> used for playback of the new sound.</param>
        /// <param name="duration">The duration in seconds the cross-fade will prolong.</param>
        /// <param name="fadeOutAudioSource">The source that will fade out and stop.</param>
        /// <param name="fadeInAudioSource">The source that defines the newly played sound.</param>
        /// <param name="followTarget">Optional target the new sound will follow while playing.</param>
        /// <param name="fadeInPosition">Either the global position or, when following, the position offset at which the new sound is played.</param>
        /// <param name="cancellationToken">Optional token for cancelling the cross-fading.</param>
        /// <returns>The cross-fading task.</returns>
        /// <remarks>Simultaneously call Stop and Play methods for finer cross-fading control instead.</remarks>
        public DynamicTask CrossFadeAsync(out SoundEntity entity,
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

        /// <summary>
        /// Asynchronously and linearly cross-fades a playing sound handled by the Sound Manager and a new sound. The fading out sound will be stopped at the end.
        /// </summary>
        /// <param name="duration">The duration in seconds the cross-fade will prolong.</param>
        /// <param name="fadeOutAudioSource">The source that will fade out and stop.</param>
        /// <param name="fadeInAudioSource">The source that defines the newly played sound.</param>
        /// <param name="followTarget">Optional target the new sound will follow while playing.</param>
        /// <param name="fadeInPosition">Either the global position or, when following, the position offset at which the new sound is played.</param>
        /// <param name="cancellationToken">Optional token for cancelling the cross-fading.</param>
        /// <returns>The cross-fading task.</returns>
        /// <remarks>Simultaneously call Stop and Play methods for finer cross-fading control instead.</remarks>
        public DynamicTask CrossFadeAsync(float duration,
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

        internal static DynamicTask FadeTask(AudioSource audioSource,
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

        private static async DynamicTask FadeTask(AudioSource audioSource,
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
        
        private static void AddToDictionaries(SoundEntity entity,
                                              ref Dictionary<SoundEntity, AudioSource> entityDictionary,
                                              ref Dictionary<AudioSource, SoundEntity> sourceDictionary)
        {
            AudioSource source = entity.FromExternalAudioSource ? entity.ExternalAudioSource : entity.AudioSource;

            entityDictionary.TryAdd(entity, source);
            sourceDictionary.TryAdd(source, entity);
        }

        private static void RemoveFromDictionaries(SoundEntity entity,
                                                   ref Dictionary<SoundEntity, AudioSource> entityDictionary,
                                                   ref Dictionary<AudioSource, SoundEntity> sourceDictionary)
        {
            AudioSource source = entity.FromExternalAudioSource ? entity.ExternalAudioSource : entity.AudioSource;

            entityDictionary.Remove(entity);
            sourceDictionary.Remove(source);
        }
        
        private void AddPlaying(SoundEntity entity)
        {
            AddToDictionaries(entity, ref _entitiesPlaying, ref _sourcesPlaying);
        }

        private void RemovePlaying(SoundEntity entity)
        {
            RemoveFromDictionaries(entity, ref _entitiesPlaying, ref _sourcesPlaying);
        }

        private void AddPaused(SoundEntity entity)
        {
            AddToDictionaries(entity, ref _entitiesPaused, ref _sourcesPaused);
        }

        private void RemovePaused(SoundEntity entity)
        {
            RemoveFromDictionaries(entity, ref _entitiesPaused, ref _sourcesPaused);
        }

        private void AddStopping(SoundEntity entity)
        {
            AddToDictionaries(entity, ref _entitiesStopping, ref _sourcesStopping);
        }

        private void RemoveStopping(SoundEntity entity)
        {
            RemoveFromDictionaries(entity, ref _entitiesStopping, ref _sourcesStopping);
        }

        private bool HasPlaying(SoundEntity entity) => _entitiesPlaying.ContainsKey(entity);
        
        private bool HasPlaying(AudioSource source, out SoundEntity entity)
        {
            return _sourcesPlaying.TryGetValue(source, out entity);
        }

        private bool HasPlaying(AudioSource source) => _sourcesPlaying.ContainsKey(source);

        private bool HasPaused(SoundEntity entity) => _entitiesPaused.ContainsKey(entity);
        
        private bool HasPaused(AudioSource source, out SoundEntity entity)
        {
            return _sourcesPaused.TryGetValue(source, out entity);
        }
        private bool HasPaused(AudioSource source) => _sourcesPaused.ContainsKey(source);
        
        private bool HasStopping(SoundEntity entity) => _entitiesStopping.ContainsKey(entity);
        
        private bool HasStopping(AudioSource source, out SoundEntity entity)
        {
            return _sourcesStopping.TryGetValue(source, out entity);
        }
        
        private bool HasStopping(AudioSource source) => _sourcesStopping.ContainsKey(source);

        private void ClearPlayingEntities()
        {
            _entitiesPlaying.Clear();
            _sourcesPlaying.Clear();
        }
        
        private void ClearPausedEntities()
        {
            _entitiesPaused.Clear();
            _sourcesPaused.Clear();
        }
        
        private void ClearStoppingEntities()
        {
            _entitiesStopping.Clear();
            _sourcesStopping.Clear();
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
            SetupIfNeeded();

            string exposedParameter = group.ExposedParameter;
            
            if (string.IsNullOrWhiteSpace(exposedParameter))
            {
                Debug.LogError(
                    $"You are trying to register a {nameof(MixerVolumeGroup)} with an empty exposed parameter. " +
                    "\nThis is not allowed.");
                
                return;
            }
            
            if (!group.AudioMixer.HasParameter(exposedParameter))
            {
                Debug.LogError(
                    $"You are trying to register a {nameof(MixerVolumeGroup)} with the exposed parameter " +
                    $"'{exposedParameter}' for the Audio Mixer '{group.AudioMixer.name}'. " +
                    "\nPlease expose the necessary parameter via the Editor or check your spelling.");
                
                return;
            }

            if (_mixerVolumeGroups.TryAdd(exposedParameter, group)) return;
            
            Debug.LogWarning(
                $"You are trying to register a {nameof(MixerVolumeGroup)} with the exposed parameter " +
                $"'{exposedParameter}' for the Audio Mixer '{group.AudioMixer.name}'. " +
                "\nThe parameter was either already registered or you were trying to register it with multiple Audio Mixers." +
                "\nIt is not allowed to use the same exposed parameter for different Audio Mixers.");
        }

        /// <summary>
        /// Unregisters a <see cref="MixerVolumeGroup"/> from the internal dictionary.
        /// </summary>
        /// <param name="group">The group to be unregistered.</param>
        public void UnregisterMixerVolumeGroup(MixerVolumeGroup group)
        {
            SetupIfNeeded();
            
            _mixerVolumeGroups.Remove(group.ExposedParameter);
        }

        /// <summary>
        /// Sets the volume for an Audio Mixer Group.
        /// </summary>
        /// <param name="exposedParameter">The exposed parameter with which to access the group, e.g. 'VolumeMusic'.</param>
        /// <param name="value">The volumes' new value.</param>
        /// <returns>True, if group with exposedParameter is registered.</returns>
        /// <remarks>Changing a volume stops any ongoing volume fades applied in the mixer.</remarks>
        public bool SetMixerGroupVolume(string exposedParameter, float value)
        {
            if (!MixerVolumeGroupRegistered(exposedParameter, out MixerVolumeGroup mixerVolumeGroup)) return false;

            StopMixerFading(exposedParameter);

            mixerVolumeGroup.Set(value);

            return true;
        }

        /// <summary>
        /// Increases the volume of an Audio Mixer Group incrementally.
        /// </summary>
        /// <param name="exposedParameter">The exposed parameter with which to access the group, e.g. 'VolumeMusic'.</param>
        /// <returns>True, if group with exposedParameter is registered.</returns>
        /// <remarks>Has no effect if no Volume Segments are defined in the <see cref="MixerVolumeGroup"/>.</remarks>
        public bool IncreaseMixerGroupVolume(string exposedParameter)
        {
            if (!MixerVolumeGroupRegistered(exposedParameter, out MixerVolumeGroup mixerVolumeGroup)) return false;

            StopMixerFading(exposedParameter);

            mixerVolumeGroup.Increase();

            return true;
        }

        /// <summary>
        /// Decreases the volume of an Audio Mixer Group incrementally.
        /// </summary>
        /// <param name="exposedParameter">The exposed parameter with which to access the group, e.g. 'VolumeMusic'.</param>
        /// <returns>True, if group with exposedParameter is registered.</returns>
        /// <remarks>Has no effect if no Volume Segments are defined in the <see cref="MixerVolumeGroup"/>.</remarks>
        public bool DecreaseMixerGroupVolume(string exposedParameter)
        {
            if (!MixerVolumeGroupRegistered(exposedParameter, out MixerVolumeGroup mixerVolumeGroup)) return false;

            StopMixerFading(exposedParameter);

            mixerVolumeGroup.Decrease();

            return true;
        }

        /// <summary>
        /// Mutes/Un-mutes the volume of an Audio Mixer Group by setting the volume to 0 or reapplying the previously stored value.
        /// </summary>
        /// <param name="exposedParameter">The exposed parameter with which to access the group, e.g. 'VolumeMusic'.</param>
        /// <param name="value">True = muted, False = unmuted.</param>
        /// <returns>True, if group with exposedParameter is registered.</returns>
        public bool MuteMixerGroupVolume(string exposedParameter, bool value)
        {
            if (!MixerVolumeGroupRegistered(exposedParameter, out MixerVolumeGroup mixerVolumeGroup)) return false;

            StopMixerFading(exposedParameter);

            mixerVolumeGroup.Mute(value);

            return true;
        }

        /// <summary>
        /// Fades the volume of an Audio Mixer Group.
        /// </summary>
        /// <param name="exposedParameter">The exposed parameter with which to access the group, e.g. 'VolumeMusic'.</param>
        /// <param name="targetVolume">The target volume reached at the end of the fade.</param>
        /// <param name="duration">The duration in seconds the fade will prolong.</param>
        /// <param name="ease">The easing applied when fading.</param>
        /// <param name="onComplete">Optional callback once mixer completes fading.</param>
        /// <returns>True, if group with exposedParameter is registered.</returns>
        public bool FadeMixerGroupVolume(string exposedParameter,
                                         float targetVolume,
                                         float duration,
                                         Ease ease = Ease.Linear,
                                         Action onComplete = null)
        {
            if (!MixerVolumeGroupRegistered(exposedParameter, out MixerVolumeGroup mixerVolumeGroup)) return false;

            StopMixerFading(exposedParameter);

#if UNITASK_INCLUDED
            CancellationTokenSource cts = new();
            _mixerFadeCancellationTokenSources.TryAdd(exposedParameter, cts);

            DoFadeTask(cts.Token).Forget();

            return true;

            async UniTaskVoid DoFadeTask(CancellationToken cancellationToken)
            {
                await FadeMixerTask(mixerVolumeGroup, duration, targetVolume, ease, cancellationToken);

                onComplete?.Invoke();

                _mixerFadeCancellationTokenSources.Remove(exposedParameter);
            }
#else
            _mixerFadeRoutines.TryAdd(exposedParameter, StartCoroutine(DoFadeRoutine()));

            return true;

            IEnumerator DoFadeRoutine()
            {
                yield return FadeMixerRoutine(mixerVolumeGroup, duration, targetVolume, ease);

                onComplete?.Invoke();

                _mixerFadeRoutines.Remove(exposedParameter);
            }
#endif
        }

        /// <summary>
        /// Asynchronously fades the volume of an Audio Mixer Group.
        /// </summary>
        /// <param name="exposedParameter">The exposed parameter with which to access the group, e.g. 'VolumeMusic'.</param>
        /// <param name="targetVolume">The target volume reached at the end of the fade.</param>
        /// <param name="duration">The duration in seconds the fade will prolong.</param>
        /// <param name="ease">The easing applied when fading.</param>
        /// <param name="cancellationToken">Optional token for cancelling the fading.</param>
        public async DynamicTask FadeMixerGroupVolumeAsync(string exposedParameter,
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
        /// <returns>True, if both fadeOutExposedParameter and fadeInExposedParameter are registered.</returns>
        public bool CrossFadeMixerGroupVolumes(string fadeOutExposedParameter,
                                               string fadeInExposedParameter,
                                               float duration,
                                               Action onComplete = null)
        {
            bool bothRegistered = MixerVolumeGroupRegistered(fadeOutExposedParameter, out MixerVolumeGroup _) &&
                                  MixerVolumeGroupRegistered(fadeInExposedParameter, out MixerVolumeGroup _);

            if (!bothRegistered) return false;
            
            FadeMixerGroupVolume(fadeOutExposedParameter, 0, duration);
            FadeMixerGroupVolume(fadeInExposedParameter, 1, duration, onComplete: onComplete);

            return true;
        }

        /// <summary>
        /// Asynchronously and linearly cross-fades the volume of two Audio Mixer Groups.
        /// </summary>
        /// <param name="fadeOutExposedParameter">The exposed parameter with which to access the group fading out, e.g. 'VolumeSFX'.</param>
        /// <param name="fadeInExposedParameter">The exposed parameter with which to access the group fading in, e.g. 'VolumeMusic'.</param>
        /// <param name="duration">The duration in seconds the cross-fade will prolong.</param>
        /// <param name="cancellationToken">Optional token for cancelling the fading.</param>
        public DynamicTask CrossFadeMixerGroupVolumesAsync(string fadeOutExposedParameter,
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

        private static DynamicTask FadeMixerTask(MixerVolumeGroup mixerVolumeGroup,
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

        private static async DynamicTask FadeMixerTask(MixerVolumeGroup mixerVolumeGroup,
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
            SetupIfNeeded();
            
            if (_mixerVolumeGroups.TryGetValue(exposedParameter, out mixerVolumeGroup)) return true;

            Debug.LogError($"There is no {nameof(MixerVolumeGroup)} for {exposedParameter} registered.");
            return false;
        }

        private void StopMixerFading(string exposedParameter)
        {
            SetupIfNeeded();
            
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
}