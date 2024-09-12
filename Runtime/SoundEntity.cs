using System;
using UnityEngine;
#if UNITASK_INCLUDED
using System.Threading;
using Cysharp.Threading.Tasks;
#else
using System.Collections;
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
        public bool Playing => _setup && _source.isPlaying;

        /// <summary>
        /// Is the SoundEntity paused?
        /// </summary>
        public bool Paused => _setup && _paused;

        private SoundManager _manager;
        private SoundProperties _properties;
        private Transform _transform;
        private AudioSource _source;

        private bool _hasFollowTarget;
        private Transform _followTarget;
        private Vector3 _followTargetOffset;

        private bool _setup;
        private bool _paused;

#if UNITASK_INCLUDED
        private bool _uniPlaying;
        private bool _uniFading;
        private bool _uniStopping;
        private CancellationTokenSource _ctsPlaying;
        private CancellationTokenSource _ctsFading;
        private CancellationTokenSource _ctsStopping;
#else
        private Coroutine _playRoutine;
        private Coroutine _fadeRoutine;
        private Coroutine _stopRoutine;
        private WaitWhile _waitWhilePlayingOrPaused;
        private WaitWhile _waitWhilePaused;
#endif

        internal void Setup(SoundManager manager)
        {
            _manager = manager;
            _properties = new SoundProperties(default(AudioClip));
            _transform = transform;
            if (!TryGetComponent(out _source)) _source = gameObject.AddComponent<AudioSource>();

#if !UNITASK_INCLUDED
            _waitWhilePlayingOrPaused = new WaitWhile(() => Playing || Paused);
            _waitWhilePaused = new WaitWhile(() => Paused);
#endif

            _setup = true;
        }

        private void LateUpdate()
        {
            if (_hasFollowTarget && Playing && !_paused)
            {
                _transform.position = _followTarget.position + _followTargetOffset;
            }
        }

#if UNITASK_INCLUDED
        private void OnDestroy()
        {
            TaskHelper.Kill(ref _ctsPlaying);
            TaskHelper.Kill(ref _ctsFading);
            TaskHelper.Kill(ref _ctsStopping);
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
            _properties = properties;
            _properties.ApplyOn(ref _source);

            if (followTarget != null)
            {
                _hasFollowTarget = true;
                _followTarget = followTarget;
                _followTargetOffset = position;
            }
            else
            {
                _transform.position = position;
            }

#if UNITASK_INCLUDED
            PlayTask(TaskHelper.Refresh(ref _ctsPlaying)).Forget();

            return this;

            async UniTaskVoid PlayTask(CancellationToken cancellationToken)
            {
                _uniPlaying = true;

                _source.Play();

                if (fadeIn)
                {
                    _source.volume = 0;
                    await SoundManager.Fade(
                        _source,
                        fadeInDuration,
                        properties.Volume,
                        fadeInEase,
                        cancellationToken: cancellationToken);
                }

                await UniTask.WaitWhile(() => Playing || Paused, cancellationToken: cancellationToken);

                onComplete?.Invoke();

                _manager.Stop(this, false);

                _uniPlaying = false;
            }
#else
            _playRoutine = _manager.StartCoroutine(PlayRoutine());

            return this;
            
            IEnumerator PlayRoutine()
            {
                _source.Play();

                if (fadeIn)
                {
                    _source.volume = 0;
                    yield return SoundManager.Fade(_source, fadeInDuration, properties.Volume, fadeInEase);
                }

                yield return _waitWhilePlayingOrPaused;

                onComplete?.Invoke();

                _manager.Stop(this, false);

                _playRoutine = null;
            }
#endif
        }

        internal void Pause()
        {
            if (Paused) return;

            _paused = true;
            _source.Pause();
        }

        internal void Resume()
        {
            if (!Paused) return;

            _source.UnPause();
            _paused = false;
        }

        internal void Stop(bool fadeOut = true,
                           float fadeOutDuration = .5f,
                           Ease fadeOutEase = Ease.Linear,
                           Action onComplete = null)
        {
#if UNITASK_INCLUDED
            if (_uniStopping) return;

            if (_uniPlaying)
            {
                TaskHelper.Kill(ref _ctsPlaying);
                _uniPlaying = false;
            }

            if (_uniFading)
            {
                TaskHelper.Kill(ref _ctsFading);
                _uniFading = false;
            }

            if (!fadeOut || Paused)
            {
                _source.Stop();
                ResetEntity();

                onComplete?.Invoke();
            }
            else
            {
                StopTask(TaskHelper.Refresh(ref _ctsStopping)).Forget();
            }

            return;

            async UniTaskVoid StopTask(CancellationToken cancellationToken)
            {
                _uniStopping = true;

                await SoundManager.Fade(_source, fadeOutDuration, 0, fadeOutEase, cancellationToken: cancellationToken);

                _source.Stop();
                ResetEntity();

                onComplete?.Invoke();

                _uniStopping = false;
            }
#else
            if (_stopRoutine != null) return;

            if (_playRoutine != null)
            {
                _manager.StopCoroutine(_playRoutine);
                _playRoutine = null;
            }

            if (_fadeRoutine != null)
            {
                _manager.StopCoroutine(_fadeRoutine);
                _fadeRoutine = null;
            }

            if (!fadeOut || Paused)
            {
                _source.Stop();
                ResetEntity();

                onComplete?.Invoke();
            }
            else
            {
                _stopRoutine = _manager.StartCoroutine(StopRoutine());
            }

            return;

            IEnumerator StopRoutine()
            {
                yield return SoundManager.Fade(_source, fadeOutDuration, 0, fadeOutEase);

                _source.Stop();
                ResetEntity();

                onComplete?.Invoke();

                _stopRoutine = null;
            }
#endif
        }

        internal void Fade(float duration, float targetVolume, Ease ease = Ease.Linear)
        {
            if (!_setup) return;

#if UNITASK_INCLUDED
            FadeTask(TaskHelper.Refresh(ref _ctsFading)).Forget();

            return;

            async UniTaskVoid FadeTask(CancellationToken cancellationToken)
            {
                _uniFading = true;

                await SoundManager.Fade(_source, duration, targetVolume, ease, cancellationToken: cancellationToken);

                _uniFading = false;
            }
#else
            if (_fadeRoutine != null) _manager.StopCoroutine(_fadeRoutine);

            _fadeRoutine = _manager.StartCoroutine(FadeRoutine());

            return;

            IEnumerator FadeRoutine()
            {
                yield return SoundManager.Fade(_source, duration, targetVolume, ease, _waitWhilePaused);

                _fadeRoutine = null;
            }
#endif
        }

        private void ResetEntity()
        {
            _paused = false;

            if (_hasFollowTarget)
            {
                _hasFollowTarget = false;
                _followTarget = null;
                _followTargetOffset = default;
            }

            _transform.position = default;

            SoundProperties.ResetOn(ref _source);
        }
    }
}