using UnityEngine;

namespace devolfer.Sound
{
    /// <summary>
    /// Propagates properties of an <see cref="AudioSource"/> to the <see cref="SoundManager"/> and handles playback via a <see cref="SoundEntity"/>.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class SoundEmitter : MonoBehaviour
    {
        /// <summary>
        /// Is there an entity playing right now?
        /// </summary>
        public bool Playing => _entity != null && _entity.Playing;
        
        /// <summary>
        /// Is there an entity paused right now?
        /// </summary>
        public bool Paused => _entity != null && _entity.Paused;

        private AudioSource _source;
        private SoundEntity _entity;
        private Transform _transform;

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            _source.enabled = false;
            _transform = transform;
        }

        /// <summary>
        /// Plays attached AudioSource via the <see cref="SoundManager"/> as a <see cref="SoundEntity"/>.
        /// </summary>
        public void Play()
        {
            if (Playing) return;

            if (Paused)
            {
                Resume();
                return;
            }

            _entity = SoundManager.Instance.Play(_source, _transform, onComplete: ClearEntity);
        }

        /// <summary>
        /// Pauses attached AudioSource via the <see cref="SoundManager"/> as a <see cref="SoundEntity"/>.
        /// </summary>
        public void Pause()
        {
            if (Playing) SoundManager.Instance.Pause(_entity);
        }

        /// <summary>
        /// Resumes attached AudioSource via the <see cref="SoundManager"/> as a <see cref="SoundEntity"/>.
        /// </summary>
        public void Resume()
        {
            if (Paused) SoundManager.Instance.Resume(_entity);
        }

        /// <summary>
        /// Stops attached AudioSource via the <see cref="SoundManager"/> as a <see cref="SoundEntity"/>.
        /// </summary>
        public void Stop()
        {
            if (!Playing && !Paused) return;

            SoundManager.Instance.Stop(_entity);

            ClearEntity();
        }

        /// <summary>
        /// Fades attached AudioSource via the <see cref="SoundManager"/> for 2 seconds as a <see cref="SoundEntity"/>.
        /// </summary>
        /// <param name="targetVolume">The target volume the fade will reach at the end.</param>
        public void Fade(float targetVolume)
        {
            if (Playing) return;

            if (Paused) Resume();

            SoundManager.Instance.Fade(_entity, targetVolume, 2);
        }

        /// <summary>
        /// Fades in attached AudioSource via the <see cref="SoundManager"/> as a <see cref="SoundEntity"/>.
        /// </summary>
        /// <param name="duration">The duration in seconds the fade in will prolong.</param>
        public void FadeIn(float duration)
        {
            if (Playing || Paused) return;

            _entity = SoundManager.Instance.Play(
                _source,
                _transform,
                fadeIn: true,
                fadeInDuration: duration,
                onComplete: ClearEntity);
        }

        /// <summary>
        /// Fades out attached AudioSource via the <see cref="SoundManager"/> as a <see cref="SoundEntity"/>.
        /// </summary>
        /// <param name="duration">The duration in seconds the fade out will prolong.</param>
        public void FadeOut(float duration)
        {
            if (!Playing && !Paused) return;

            SoundManager.Instance.Stop(_entity, fadeOutDuration: duration);

            ClearEntity();
        }

        private void ClearEntity() => _entity = null;
    }
}