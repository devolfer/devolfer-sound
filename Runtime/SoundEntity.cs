using System;
using System.Collections;
using UnityEngine;

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

        private Coroutine _playRoutine;
        private Coroutine _fadeRoutine;
        private Coroutine _stopRoutine;
        private WaitWhile _waitWhilePlayingOrPaused;
        private WaitWhile _waitWhilePaused;

        private bool _setup;
        private bool _paused;

        internal void Setup(SoundManager manager)
        {
            _manager = manager;
            _properties = new SoundProperties(default(AudioClip));
            _transform = transform;
            if (!TryGetComponent(out _source)) _source = gameObject.AddComponent<AudioSource>();

            _waitWhilePlayingOrPaused = new WaitWhile(() => Playing || Paused);
            _waitWhilePaused = new WaitWhile(() => Paused);

            _setup = true;
        }

        private void LateUpdate()
        {
            if (_hasFollowTarget && Playing && !_paused)
            {
                _transform.position = _followTarget.position + _followTargetOffset;
            }
        }

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
        }

        internal void Fade(float duration, float targetVolume, Ease ease = Ease.Linear)
        {
            if (!_setup) return;

            if (_fadeRoutine != null) _manager.StopCoroutine(_fadeRoutine);

            _fadeRoutine = _manager.StartCoroutine(FadeRoutine());

            return;

            IEnumerator FadeRoutine()
            {
                yield return SoundManager.Fade(_source, duration, targetVolume, ease, _waitWhilePaused);

                _fadeRoutine = null;
            }
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