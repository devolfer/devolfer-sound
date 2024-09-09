using UnityEngine;

namespace devolfer.Sound
{
    [RequireComponent(typeof(AudioSource))]
    public class SoundEmitter : MonoBehaviour
    {
        [SerializeField] private bool _local;

        private AudioSource _source;
        private SoundEntity _entity;
        private Transform _transform;
        private bool Playing => _entity != null && _entity.Playing;
        private bool Paused => _entity != null && _entity.Paused;

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            _source.enabled = false;
            _transform = transform;
        }

        public void Play()
        {
            if (Playing) return;

            if (Paused)
            {
                Resume();
                return;
            }

            _entity = SoundManager.Instance.Play(
                _source,
                _local ? _transform : null,
                _local ? Vector3.zero : _transform.position,
                onComplete: ClearEntity);
        }

        public void Pause()
        {
            if (Playing) SoundManager.Instance.Pause(_entity);
        }

        public void Resume()
        {
            if (Paused) SoundManager.Instance.Resume(_entity);
        }

        public void Stop()
        {
            if (!Playing && !Paused) return;

            SoundManager.Instance.Stop(_entity);

            ClearEntity();
        }

        private void ClearEntity() => _entity = null;
    }
}