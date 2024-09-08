using UnityEngine;

namespace devolfer.Sound
{
    [RequireComponent(typeof(AudioSource))]
    public class SoundEmitter : MonoBehaviour
    {
        private AudioSource _source;
        private SoundEntity _entity;
        private Transform _transform;
        private bool Playing => _entity != null && _entity.Playing;

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            _source.enabled = false;
            _transform = transform;
        }

        public void Play()
        {
            Stop();

            _entity = SoundManager.Instance.Play(_source, _transform.position, onPlayEnd: ClearEntity);
        }

        public void Stop()
        {
            if (!Playing) return;

            SoundManager.Instance.Stop(_entity);

            ClearEntity();
        }

        private void ClearEntity() => _entity = null;

        // TODO Remove these after testing
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.P))
            {
                Play();
            }

            if (Input.GetKeyDown(KeyCode.O))
            {
                Stop();
            }

            if (Input.GetKeyDown(KeyCode.L))
            {
                SoundManager.Instance.Play(_source);
            }
        }
    }
}