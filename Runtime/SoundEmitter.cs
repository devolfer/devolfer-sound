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

            _entity = SoundManager.Instance.Play(_source, _transform.position, onPlayEnd: ClearEntity);
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

            if (Input.GetKeyDown(KeyCode.I))
            {
                Pause();
            }

            if (Input.GetKeyDown(KeyCode.U))
            {
                Resume();
            }

            if (Input.GetKeyDown(KeyCode.L))
            {
                SoundManager.Instance.Play(_source);
            }
            
            if (Input.GetKeyDown(KeyCode.K))
            {
                SoundManager.Instance.StopAll();
            }
            
            if (Input.GetKeyDown(KeyCode.J))
            {
                SoundManager.Instance.PauseAll();
            }
            
            if (Input.GetKeyDown(KeyCode.H))
            {
                SoundManager.Instance.ResumeAll();
            }
        }
    }
}