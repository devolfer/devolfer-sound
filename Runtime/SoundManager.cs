using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace devolfer.Sound
{
    // TODO Fade in/out on play/stop by default (optional)
    // TODO Crossfade between audio sources/entities
    // TODO Audio Mixers handling in general
    public class SoundManager : PersistentSingleton<SoundManager>
    {
        [SerializeField] private int _poolCapacityDefault = 64;

        private ObjectPool<SoundEntity> _pool;
        private HashSet<SoundEntity> _entitiesPlaying;
        private HashSet<SoundEntity> _entitiesPaused;

        public SoundEntity Play(SoundProperties properties,
                                Transform parent = null,
                                Vector3 position = default,
                                Action onPlayStart = null,
                                Action onPlayEnd = null)
        {
            SoundEntity entity = _pool.Get();
            _entitiesPlaying.Add(entity);

            return entity.Play(properties, parent, position, onPlayStart, onPlayEnd);
        }

        public void Pause(SoundEntity entity)
        {
            if (!_entitiesPlaying.Contains(entity)) return;

            _entitiesPlaying.Remove(entity);

            entity.Pause();

            _entitiesPaused.Add(entity);
        }

        public void Resume(SoundEntity entity)
        {
            if (!_entitiesPaused.Contains(entity)) return;

            _entitiesPaused.Remove(entity);

            entity.Resume();

            _entitiesPlaying.Add(entity);
        }
        
        public void Stop(SoundEntity entity)
        {
            if (!_entitiesPlaying.Contains(entity) && !_entitiesPaused.Contains(entity)) return;

            entity.Stop();

            _entitiesPlaying.Remove(entity);
            _pool.Release(entity);
        }

        public void PauseAll()
        {
            foreach (SoundEntity entity in _entitiesPlaying)
            {
                entity.Pause();
                _entitiesPaused.Add(entity);
            }

            _entitiesPlaying.Clear();
        }

        public void ResumeAll()
        {
            foreach (SoundEntity entity in _entitiesPaused)
            {
                entity.Resume();
                _entitiesPlaying.Add(entity);
            }

            _entitiesPaused.Clear();
        }

        public void StopAll()
        {
            foreach (SoundEntity entity in _entitiesPlaying)
            {
                entity.Stop();
                _pool.Release(entity);
            }

            _entitiesPlaying.Clear();

            foreach (SoundEntity entity in _entitiesPaused)
            {
                entity.Stop();
                _pool.Release(entity);
            }

            _entitiesPaused.Clear();
        }

        public void Fade(SoundEntity entity, float duration, float targetVolume, Ease ease = Ease.Linear)
        {
            entity.Fade(duration, targetVolume, ease);
        }

        protected override void Setup()
        {
            base.Setup();

            if (s_instance != this) return;

            _entitiesPlaying = new HashSet<SoundEntity>();
            _entitiesPaused = new HashSet<SoundEntity>();

            CreatePool();
        }

        private void CreatePool()
        {
            _pool = new ObjectPool<SoundEntity>(
                createFunc: () =>
                {
                    GameObject obj = new($"SoundEntity-{_pool.CountAll}");
                    obj.transform.SetParent(transform);
                    SoundEntity entity = obj.AddComponent<SoundEntity>();
                    entity.Setup(this);

                    obj.SetActive(false);

                    return entity;
                },
                actionOnGet: entity => entity.gameObject.SetActive(true),
                actionOnRelease: entity => entity.gameObject.SetActive(false),
                actionOnDestroy: entity => Destroy(entity.gameObject),
                defaultCapacity: _poolCapacityDefault);

            _pool.PreAllocate(_poolCapacityDefault);
        }
        
        public static IEnumerator Fade(AudioSource audioSource,
                                       float duration,
                                       float targetVolume,
                                       Ease ease = Ease.Linear,
                                       WaitWhile waitWhilePredicate = null)
        {
            return Fade(audioSource, duration, targetVolume, EasingFunctions.GetEasingFunction(ease), waitWhilePredicate);
        }

        public static IEnumerator Fade(AudioSource audioSource,
                                       float duration,
                                       float targetVolume,
                                       Func<float, float> easeFunction,
                                       WaitWhile waitWhilePredicate = null)
        {
            targetVolume = Mathf.Clamp01(targetVolume);
            
            if (duration <= 0)
            {
                audioSource.volume = targetVolume;
                yield break;
            }
            
            float deltaTime = 0;
            float startVolume = audioSource.volume;

            while (deltaTime < duration)
            {
                deltaTime += Time.deltaTime;
                audioSource.volume = Mathf.Lerp(startVolume, targetVolume, easeFunction(deltaTime / duration));

                yield return waitWhilePredicate;
            }

            audioSource.volume = targetVolume;
        }
    }

    public class Singleton<T> : MonoBehaviour where T : Component
    {
        [SerializeField] protected HideFlags _hideFlags = HideFlags.None;

        protected static T s_instance;

        public static T Instance
        {
            get
            {
                if (s_instance != null) return s_instance;

                s_instance = FindAnyObjectByType<T>();

                return s_instance != null ?
                    s_instance :
                    s_instance = new GameObject($"{typeof(T).Name}").AddComponent<T>();
            }
        }

        protected virtual void Awake()
        {
            if (!Application.isPlaying) return;

            Setup();
        }

        protected virtual void OnDestroy() => s_instance = null;

        protected virtual void Setup()
        {
            gameObject.hideFlags = _hideFlags;

            s_instance = this as T;
        }
    }

    public class PersistentSingleton<T> : Singleton<T> where T : Component
    {
        [SerializeField] protected bool _autoUnParent = true;

        protected override void Setup()
        {
            if (_autoUnParent) transform.SetParent(null);

            if (s_instance != null)
            {
                if (s_instance != this) Destroy(gameObject);
            }
            else
            {
                base.Setup();

                DontDestroyOnLoad(gameObject);
            }
        }
    }

    internal static class ObjectPoolExtensions
    {
        /// <summary>
        /// Pre-allocates objects from the pool by getting and releasing a desired amount.
        /// </summary>
        /// <param name="capacity">The amount of objects to pre-allocate.</param>
        public static void PreAllocate<T>(this ObjectPool<T> pool, int capacity) where T : class
        {
            T[] preAllocatedT = new T[capacity];

            for (int i = 0; i < capacity; i++) preAllocatedT[i] = pool.Get();
            for (int i = preAllocatedT.Length - 1; i >= 0; i--) pool.Release(preAllocatedT[i]);
        }
    }
}