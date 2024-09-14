using System;
using UnityEngine;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
#if UNITASK_INCLUDED
using Cysharp.Threading.Tasks;
#endif

namespace devolfer.Sound
{
    /// <summary>
    /// An extended wrapper of an <see cref="AudioSource"/> that works together with the <see cref="SoundManager"/>. 
    /// </summary>
    [AddComponentMenu("")]
    public class SoundEntity : MonoBehaviour
    {
        /// <summary>
        /// Is the SoundEntity playing?
        /// </summary>
        public bool Playing { get; private set; }

        /// <summary>
        /// Is the SoundEntity paused?
        /// </summary>
        public bool Paused { get; private set; }

        /// <summary>
        /// Is the SoundEntity fading?
        /// </summary>
        public bool Fading { get; private set; }

        /// <summary>
        /// Is the SoundEntity stopping?
        /// </summary>
        public bool Stopping { get; private set; }

        /// <summary>
        /// Did the SoundEntity originate from an external AudioSource?
        /// </summary>
        /// <remarks>This means that the Play method of this entity was initiated via an AudioSource rather than from SoundProperties.</remarks>
        public bool FromExternalAudioSource { get; private set; }

        /// <summary>
        /// The external AudioSource this SoundEntity originated from.
        /// </summary>
        /// <remarks>Check <see cref="FromExternalAudioSource"/> to foresee a null reference.</remarks>
        public AudioSource ExternalAudioSource => _externalAudioSource;

        /// <summary>
        /// The global position of the entity.
        /// </summary>
        /// <remarks>Will be cheaper than calling 'this.transform.position'.</remarks>
        public Vector3 Position => _transform.position;

        /// <summary>
        /// Whether this entity is following the position of another Transform.
        /// </summary>
        public bool HasFollowTarget => _hasFollowTarget;

        /// <summary>
        /// The Transform this entity is following while playing.
        /// </summary>
        /// <remarks>Check <see cref="HasFollowTarget"/> to foresee a null reference.</remarks>
        public Transform FollowTarget => _followTarget;
        
        /// <summary>
        /// The position offset relative to the <see cref="FollowTarget"/>.
        /// </summary>
        public Vector3 FollowTargetOffset => _followTargetOffset;

        private SoundManager _manager;
        private SoundProperties _properties;
        private Transform _transform;
        private AudioSource _audioSource;
        private AudioSource _externalAudioSource;
        
        private bool _hasFollowTarget;
        private Transform _followTarget;
        private Vector3 _followTargetOffset;

        private bool _setup;

        private CancellationTokenSource _playCts;
        private CancellationTokenSource _fadeCts;
        private CancellationTokenSource _stopCts;
        private Func<bool> SourceIsPlayingOrPausedPredicate => () => (_setup && _audioSource.isPlaying) || Paused;
        private Func<bool> PausedPredicate => () => Paused;

        private Coroutine _playRoutine;
        private Coroutine _fadeRoutine;
        private Coroutine _stopRoutine;
        private WaitWhile _waitWhileSourceIsPlayingOrPaused;
        private WaitWhile _waitWhilePaused;

        internal void Setup(SoundManager manager)
        {
            _manager = manager;
            _properties = new SoundProperties(default(AudioClip));
            _transform = transform;
            if (!TryGetComponent(out _audioSource)) _audioSource = gameObject.AddComponent<AudioSource>();

            _waitWhileSourceIsPlayingOrPaused = new WaitWhile(SourceIsPlayingOrPausedPredicate);
            _waitWhilePaused = new WaitWhile(PausedPredicate);

            _setup = true;
        }

        private void LateUpdate()
        {
            if (!_setup) return;
            
            if (_hasFollowTarget && Playing)
            {
                _transform.position = _followTarget.position + _followTargetOffset;
            }
        }

#if UNITASK_INCLUDED
        private void OnDestroy()
        {
            TaskHelper.Cancel(ref _playCts);
            TaskHelper.Cancel(ref _fadeCts);
            TaskHelper.Cancel(ref _stopCts);
        }
#endif

        internal SoundEntity Play(SoundProperties properties,
                                  Transform followTarget = null,
                                  Vector3 position = default,
                                  bool fadeIn = false,
                                  float fadeInDuration = .5f,
                                  Ease fadeInEase = Ease.Linear,
                                  Action onComplete = null)
        {
            SetProperties(properties, followTarget, position);

            _playRoutine = _manager.StartCoroutine(PlayRoutine());

            return this;

            IEnumerator PlayRoutine()
            {
                Playing = true;
                _audioSource.Play();

                if (fadeIn)
                {
                    _audioSource.volume = 0;
                    yield return SoundManager.FadeRoutine(
                        _audioSource,
                        fadeInDuration,
                        properties.Volume,
                        fadeInEase,
                        _waitWhilePaused);
                }

                yield return _waitWhileSourceIsPlayingOrPaused;

                onComplete?.Invoke();

                _manager.Stop(this, false);
                Playing = false;
                _playRoutine = null;
            }
        }

        internal SoundEntity Play(AudioSource audioSource,
                                  Transform followTarget = null,
                                  Vector3 position = default,
                                  bool fadeIn = false,
                                  float fadeInDuration = .5f,
                                  Ease fadeInEase = Ease.Linear,
                                  Action onComplete = null)
        {
            _externalAudioSource = audioSource;
            FromExternalAudioSource = true;
            
            if (audioSource.isPlaying) audioSource.Stop();
            audioSource.enabled = false;

            SoundProperties properties = audioSource;

            return Play(properties, followTarget, position, fadeIn, fadeInDuration, fadeInEase, onComplete);
        }

        internal async
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
            if (cancellationToken == default)
            {
                cancellationToken = TaskHelper.CancelAndRefresh(ref _playCts);
            }
            else
            {
                TaskHelper.CancelAndRefresh(ref _playCts);
                TaskHelper.Link(ref cancellationToken, ref _playCts);
            }

            SetProperties(properties, followTarget, position);

            Playing = true;
            _audioSource.Play();

            if (fadeIn)
            {
                _audioSource.volume = 0;

                await SoundManager.FadeTask(
                    _audioSource,
                    fadeInDuration,
                    properties.Volume,
                    fadeInEase,
                    PausedPredicate,
                    cancellationToken);
            }

#if UNITASK_INCLUDED
            await UniTask.WaitWhile(SourceIsPlayingOrPausedPredicate, cancellationToken: cancellationToken);
#else
            await TaskHelper.WaitWhile(SourceIsPlayingOrPausedPredicate, cancellationToken: cancellationToken);
#endif

            await _manager.StopAsync(this, false, cancellationToken: cancellationToken);

            Playing = false;
        }

        internal
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
            _externalAudioSource = audioSource;
            FromExternalAudioSource = true;
            
            if (audioSource.isPlaying) audioSource.Stop();
            audioSource.enabled = false;

            SoundProperties properties = audioSource;
            
            return PlayAsync(properties, followTarget, position, fadeIn, fadeInDuration, fadeInEase, cancellationToken);
        }

        internal void Pause()
        {
            if (Paused) return;

            Paused = true;
            _audioSource.Pause();
        }

        internal void Resume()
        {
            if (!Paused) return;

            _audioSource.UnPause();
            Paused = false;
        }

        internal void Stop(bool fadeOut = true,
                           float fadeOutDuration = .5f,
                           Ease fadeOutEase = Ease.Linear,
                           Action onComplete = null)
        {
            if (Stopping && fadeOut) return;
            
            if (_stopRoutine != null) _manager.StopCoroutine(_stopRoutine);
            _stopRoutine = null;
            TaskHelper.Cancel(ref _stopCts);
            Stopping = false;

            if (Playing)
            {
                if (_playRoutine != null) _manager.StopCoroutine(_playRoutine);
                _playRoutine = null;
                TaskHelper.Cancel(ref _playCts);
                Playing = false;
            }

            if (Fading)
            {
                if (_fadeRoutine != null) _manager.StopCoroutine(_fadeRoutine);
                _fadeRoutine = null;
                TaskHelper.Cancel(ref _fadeCts);
                Fading = false;
            }

            if (!fadeOut || Paused)
            {
                _audioSource.Stop();
                ResetProperties();

                onComplete?.Invoke();
            }
            else
            {
                _stopRoutine = _manager.StartCoroutine(StopRoutine());
            }

            return;

            IEnumerator StopRoutine()
            {
                Stopping = true;
                
                yield return SoundManager.FadeRoutine(_audioSource, fadeOutDuration, 0, fadeOutEase);

                _audioSource.Stop();
                ResetProperties();

                onComplete?.Invoke();

                Stopping = false;
                _stopRoutine = null;
            }
        }

        internal async
#if UNITASK_INCLUDED
            UniTask
#else
            Task
#endif
            StopAsync(bool fadeOut = true,
                      float fadeOutDuration = .5f,
                      Ease fadeOutEase = Ease.Linear,
                      CancellationToken cancellationToken = default)
        {
            if (Stopping && fadeOut) return;
            
            if (_stopRoutine != null) _manager.StopCoroutine(_stopRoutine);
            _stopRoutine = null;
            TaskHelper.Cancel(ref _stopCts);
            Stopping = false;

            if (Playing)
            {
                if (_playRoutine != null) _manager.StopCoroutine(_playRoutine);
                _playRoutine = null;
                TaskHelper.Cancel(ref _playCts);
                Playing = false;
            }

            if (Fading)
            {
                if (_fadeRoutine != null) _manager.StopCoroutine(_fadeRoutine);
                _fadeRoutine = null;
                TaskHelper.Cancel(ref _fadeCts);
                Fading = false;
            }

            if (!fadeOut || Paused)
            {
                _audioSource.Stop();
                ResetProperties();
            }
            else
            {
                if (cancellationToken == default)
                {
                    cancellationToken = TaskHelper.CancelAndRefresh(ref _stopCts);
                }
                else
                {
                    TaskHelper.CancelAndRefresh(ref _stopCts);
                    TaskHelper.Link(ref cancellationToken, ref _stopCts);
                }

                Stopping = true;

                await SoundManager.FadeTask(
                    _audioSource,
                    fadeOutDuration,
                    0,
                    fadeOutEase,
                    cancellationToken: cancellationToken);

                _audioSource.Stop();
                ResetProperties();

                Stopping = false;
            }
        }

        internal void Fade(float targetVolume, float duration, Ease ease = Ease.Linear, Action onComplete = null)
        {
            if (Fading)
            {
                if (_fadeRoutine != null) _manager.StopCoroutine(_fadeRoutine);
                _fadeRoutine = null;
                TaskHelper.Cancel(ref _fadeCts);
                Fading = false;
            }
            
            _fadeRoutine = _manager.StartCoroutine(FadeRoutine());

            return;

            IEnumerator FadeRoutine()
            {
                Fading = true;
                
                yield return SoundManager.FadeRoutine(_audioSource, duration, targetVolume, ease, _waitWhilePaused);

                onComplete?.Invoke();
                
                Fading = false;
                _fadeRoutine = null;
            }
        }

        internal async
#if UNITASK_INCLUDED
            UniTask
#else
            Task
#endif
            FadeAsync(float targetVolume,
                      float duration,
                      Ease ease = Ease.Linear,
                      CancellationToken cancellationToken = default)
        {
            if (cancellationToken == default)
            {
                cancellationToken = TaskHelper.CancelAndRefresh(ref _fadeCts);
            }
            else
            {
                TaskHelper.CancelAndRefresh(ref _fadeCts);
                TaskHelper.Link(ref cancellationToken, ref _fadeCts);
            }

            Fading = true;

            await SoundManager.FadeTask(
                _audioSource,
                duration,
                targetVolume,
                ease,
                PausedPredicate,
                cancellationToken);

            Fading = false;
        }

        internal void SetProperties(SoundProperties properties, Transform followTarget, Vector3 position)
        {
            _properties = properties;
            _properties.ApplyOn(ref _audioSource);
            
            _followTarget = followTarget;
            _hasFollowTarget = _followTarget != null;

            if (_hasFollowTarget)
            {
                _followTargetOffset = position;
            }
            else
            {
                _transform.position = position;
            }
        }

        private void ResetProperties()
        {
            Paused = false;

            if (_hasFollowTarget)
            {
                _hasFollowTarget = false;
                _followTarget = null;
                _followTargetOffset = default;
            }

            _transform.position = default;

            SoundProperties.ResetOn(ref _audioSource);

            _externalAudioSource = null;
            FromExternalAudioSource = false;
        }
    }
}