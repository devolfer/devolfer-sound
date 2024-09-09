using System;
using System.Collections;
using UnityEngine;

namespace devolfer.Sound
{
    [AddComponentMenu("")]
    public class SoundEntity : MonoBehaviour
    {
        public bool Playing => _setup && _source.isPlaying;
        public bool Paused => _setup && _paused;

        private SoundManager _manager;
        private SoundProperties _properties;
        private Transform _transform;
        private AudioSource _source;

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
            _properties = new SoundProperties();
            _transform = transform;
            if (!TryGetComponent(out _source)) _source = gameObject.AddComponent<AudioSource>();

            _playRoutine = null;
            _fadeRoutine = null;
            _waitWhilePlayingOrPaused = new WaitWhile(() => Playing || Paused);
            _waitWhilePaused = new WaitWhile(() => Paused);

            _setup = true;
        }

        internal SoundEntity Play(SoundProperties properties,
                                  Transform parent = null,
                                  Vector3 position = default,
                                  Action onPlayStart = null,
                                  Action onPlayEnd = null)
        {
            _properties = properties;
            _properties.ApplyOn(ref _source);

            if (parent != null)
            {
                _transform.SetParent(parent, false);
                _transform.localPosition = position;
            }
            else
            {
                _transform.position = position;
            }

            _playRoutine = _manager.StartCoroutine(PlayRoutine());

            return this;

            IEnumerator PlayRoutine()
            {
                onPlayStart?.Invoke();

                _source.Play();

                yield return _waitWhilePlayingOrPaused;

                onPlayEnd?.Invoke();

                _manager.Stop(this, false);

                _playRoutine = null;
            }
        }

        internal void Pause()
        {
            _paused = true;
            _source.Pause();
        }

        internal void Resume()
        {
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

            if (_transform.parent != _manager.transform)
            {
                _transform.SetParent(_manager.transform, false);
                _transform.localPosition = default;
            }
            else
            {
                _transform.position = default;
            }

            _properties.ResetOn(ref _source);
        }
    }
}