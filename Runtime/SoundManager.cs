using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace devolfer.Sound
{
    // TODO FadeIn/Out in general
    // TODO Crossfade between audio sources/entities
    // TODO Play overload with AudioSource parameter
    // TODO Audio Mixers handling in general
    public class SoundManager : PersistentSingleton<SoundManager>
    {
        private ObjectPool<SoundEntity> _pool;
        private const int PoolCapacityDefault = 64;

        private HashSet<SoundEntity> _entitiesPlaying;

        public SoundEntity Play(SoundProperties properties,
                                Action onPlayStart = null,
                                Action onPlayEnd = null)
        {
            SoundEntity entity = _pool.Get();
            _entitiesPlaying.Add(entity);
            
            return entity.Play(properties, onPlayStart, onPlayEnd);
        }

        public SoundEntity PlayAtPoint(SoundProperties properties,
                                       Vector3 worldPosition,
                                       Action onPlayStart = null,
                                       Action onPlayEnd = null)
        {
            SoundEntity entity = _pool.Get();
            _entitiesPlaying.Add(entity);
            
            return entity.PlayAtPoint(properties, worldPosition, onPlayStart, onPlayEnd);
        }

        // TODO Stop with fade out by default?
        public void Stop(SoundEntity entity)
        {
            if (!_entitiesPlaying.Contains(entity)) return;

            entity.Stop();
            
            _entitiesPlaying.Remove(entity);
            _pool.Release(entity);
        }

        protected override void Setup()
        {
            base.Setup();

            if (s_instance != this) return;

            _entitiesPlaying = new HashSet<SoundEntity>();
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
                defaultCapacity: PoolCapacityDefault);

            _pool.PreAllocate(PoolCapacityDefault);
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