using System;
using System.Collections;
using UnityEngine;

namespace devolfer.Sound
{
    [AddComponentMenu("")]
    public class SoundEntity : MonoBehaviour
    {
        public bool Playing => _setup && _audioSource.isPlaying;

        private SoundManager _manager;
        private SoundProperties _properties;
        private Transform _transform;
        private AudioSource _audioSource;

        private Coroutine _playRoutine;
        private WaitWhile _waitWhilePlaying;

        private bool _setup;

        internal void Setup(SoundManager manager)
        {
            _manager = manager;
            _properties = new SoundProperties();
            _transform = transform;
            if (!TryGetComponent(out _audioSource)) _audioSource = gameObject.AddComponent<AudioSource>();

            _playRoutine = null;
            _waitWhilePlaying = new WaitWhile(() => Playing);

            _setup = true;
        }

        internal SoundEntity Play(SoundProperties properties,
                                  Vector3 position = default,
                                  Action onPlayStart = null,
                                  Action onPlayEnd = null)
        {
            _properties = properties;
            _properties.ApplyOn(ref _audioSource);

            _transform.position = position;

            _playRoutine = _manager.StartCoroutine(PlayRoutine());

            return this;

            IEnumerator PlayRoutine()
            {
                onPlayStart?.Invoke();

                _audioSource.Play();

                yield return _waitWhilePlaying;

                onPlayEnd?.Invoke();

                _manager.Stop(this);

                _playRoutine = null;
            }
        }

        internal void Stop()
        {
            if (_playRoutine != null)
            {
                _manager.StopCoroutine(_playRoutine);
                _playRoutine = null;
            }

            _audioSource.Stop();
            _transform.position = default;

            _properties.ResetOn(ref _audioSource);
        }
    }
}