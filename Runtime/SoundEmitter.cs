using UnityEngine;

namespace Devolfer.Sound
{
    /// <summary>
    /// Propagates properties of an <see cref="AudioSource"/> to the <see cref="SoundManager"/> and handles playback via a <see cref="SoundEntity"/>.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class SoundEmitter : MonoBehaviour
    {
        private AudioSource _source;
        private Transform _transform;

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            _source.enabled = false;
            _transform = transform;
        }

        /// <summary>
        /// Plays attached <see cref="AudioSource"/> via the <see cref="SoundManager"/> as a <see cref="SoundEntity"/>.
        /// </summary>
        public void Play()
        {
            SoundManager.Instance.Play(_source, _transform);
        }

        /// <summary>
        /// Pauses attached <see cref="AudioSource"/> via the <see cref="SoundManager"/> as a <see cref="SoundEntity"/>.
        /// </summary>
        public void Pause()
        {
            SoundManager.Instance.Pause(_source);
        }

        /// <summary>
        /// Resumes attached <see cref="AudioSource"/> via the <see cref="SoundManager"/> as a <see cref="SoundEntity"/>.
        /// </summary>
        public void Resume()
        {
            SoundManager.Instance.Resume(_source);
        }

        /// <summary>
        /// Stops attached <see cref="AudioSource"/> via the <see cref="SoundManager"/> as a <see cref="SoundEntity"/>.
        /// </summary>
        public void Stop()
        {
            SoundManager.Instance.Stop(_source);
        }

        /// <summary>
        /// Fades attached <see cref="AudioSource"/> via the <see cref="SoundManager"/> as a <see cref="SoundEntity"/>.
        /// </summary>
        /// <param name="targetVolume">The target volume the fade will reach at the end.</param>
        /// <param name="duration">The duration in seconds the fade will prolong.</param>
        public void Fade(float targetVolume, float duration)
        {
            SoundManager.Instance.Fade(_source, targetVolume, duration);
        }

        /// <summary>
        /// Fades attached <see cref="AudioSource"/> via the <see cref="SoundManager"/> for 2 seconds as a <see cref="SoundEntity"/>.
        /// </summary>
        /// <param name="targetVolume">The target volume the fade will reach at the end.</param>
        public void Fade(float targetVolume)
        {
            SoundManager.Instance.Fade(_source, targetVolume, 2);
        }

        /// <summary>
        /// Fades in attached AudioSource via the <see cref="SoundManager"/> as a <see cref="SoundEntity"/>.
        /// </summary>
        /// <param name="duration">The duration in seconds the fade in will prolong.</param>
        public void FadeIn(float duration)
        {
            SoundManager.Instance.Play(_source, _transform, fadeIn: true, fadeInDuration: duration);
        }

        /// <summary>
        /// Fades out attached AudioSource via the <see cref="SoundManager"/> as a <see cref="SoundEntity"/>.
        /// </summary>
        /// <param name="duration">The duration in seconds the fade out will prolong.</param>
        public void FadeOut(float duration)
        {
            SoundManager.Instance.Stop(_source, fadeOutDuration: duration);
        }
    }
}