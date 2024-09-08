using UnityEngine;

namespace devolfer.Sound
{
    [RequireComponent(typeof(AudioSource))]
    public class SoundEmitter : MonoBehaviour
    {
        private AudioSource _source;
        private SoundEntity _entity;
        private bool Playing => _entity != null && _entity.Playing;

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            _source.enabled = false;
        }

        public void Play()
        {
            Stop();

            _entity = SoundManager.Instance.Play(_source, onPlayEnd: ClearEntity);
        }

        public void Play2D()
        {
            Stop();

            _entity = SoundManager.Instance.Play(
                new SoundProperties(_source) { SpatialBlend = 0 },
                onPlayEnd: ClearEntity);
        }

        public void Play3D()
        {
            Stop();

            _entity = SoundManager.Instance.PlayAtPoint(
                new SoundProperties(_source) { SpatialBlend = 1 },
                transform.position,
                onPlayEnd: ClearEntity);
        }

        public void Stop()
        {
            if (!Playing) return;

            SoundManager.Instance.Stop(_entity);

            ClearEntity();
        }

        private void ClearEntity() => _entity = null;

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