using System;
using System.Collections.Generic;
using UnityEngine;

namespace XFramework
{
    [DisallowMultipleComponent]
    [AddComponentMenu("XFramework/Cache Pool")]
    public sealed partial class CachePool : XFrameworkComponent
    {
        private readonly Dictionary<Type, CacheCollection> _cacheCollections = new();

        /// <summary>
        /// 缓存集合的数量（也是缓存池中的类型数量）
        /// </summary>
        public int CacheCollectionCount => _cacheCollections.Count;

        internal override int Priority
        {
            get => XFrameworkConstant.ComponentPriority.CachePool;
        }

        internal override void Clear()
        {
            base.Clear();

            foreach (CacheCollection cacheCollection in _cacheCollections.Values)
            {
                cacheCollection.DiscardAll();
            }
            _cacheCollections.Clear();
        }

        public CacheCollectionInfo[] GetAllCacheCollectionInfos()
        {
            CacheCollectionInfo[] infos = new CacheCollectionInfo[_cacheCollections.Count];
            int index = 0;
            foreach (CacheCollection cacheCollection in _cacheCollections.Values)
            {
                infos[index++] = new CacheCollectionInfo
                (
                    cacheCollection.CacheType,
                    cacheCollection.UnusedCount,
                    cacheCollection.UsingCount,
                    cacheCollection.SpawnedCount,
                    cacheCollection.UnspawnedCount,
                    cacheCollection.CreatedCount,
                    cacheCollection.DiscardedCount
                );
            }
            return infos;
        }

        /// <summary>
        /// 获取一个指定类型的缓存
        /// </summary>
        /// <param name="type">要获取的缓存类型</param>
        /// <returns>得到的缓存</returns>
        public ICache Spawn(Type type)
        {
            CheckTypeCompilance(type);
            return GetCacheableCollection(type).Spawn();
        }

        /// <summary>
        /// 获取一个指定类型的缓存
        /// </summary>
        /// <typeparam name="T">要获取的缓存类型</typeparam>
        /// <returns>得到的缓存</returns>
        public T Spawn<T>() where T : class, ICache, new()
        {
            return GetCacheableCollection(typeof(T)).Spawn() as T;
        }

        /// <summary>
        /// 放入一个缓存
        /// </summary>
        /// <param name="cache">要放入的缓存</param>
        public void Unspawn(ICache cache)
        {
            if (cache == null)
            {
                throw new ArgumentNullException(nameof(cache), "Unspawn failed. ICacheable is null.");
            }
            Type cacheableType = cache.GetType();
            CheckTypeCompilance(cacheableType);

            GetCacheableCollection(cacheableType).Unspawn(cache);
        }

        /// <summary>
        /// 指定类型的缓存预留指定数量
        /// </summary>
        /// <param name="type">要预留的缓存类型</param>
        /// <param name="count">要预留的数量</param>
        public void Reserve(Type type, int count)
        {
            CheckTypeCompilance(type);
            GetCacheableCollection(type).Reserve(count);
        }

        /// <summary>
        /// 指定类型的缓存预留指定数量
        /// </summary>
        /// <typeparam name="T">要预留的缓存类型</typeparam>
        /// <param name="count">要预留的数量</param>
        public void Reserve<T>(int count) where T : class, ICache, new()
        {
            GetCacheableCollection(typeof(T)).Reserve(count);
        }

        /// <summary>
        /// 指定类型的缓存丢弃指定数量
        /// </summary>
        /// <param name="type">要丢弃的缓存类型</param>
        /// <param name="count">要丢弃的数量</param>
        public void Discard(Type type, int count)
        {
            CheckTypeCompilance(type);
            GetCacheableCollection(type).Discard(count);
        }

        /// <summary>
        /// 指定类型的缓存丢弃指定数量
        /// </summary>
        /// <typeparam name="T">要丢弃的缓存类型</typeparam>
        /// <param name="count">要丢弃的数量</param>
        public void Discard<T>(int count) where T : class, ICache, new()
        {
            GetCacheableCollection(typeof(T)).Discard(count);
        }

        /// <summary>
        /// 丢弃指定类型的所有缓存
        /// </summary>
        /// <param name="type">要丢弃的缓存类型</param>
        public void DiscardAll(Type type)
        {
            CheckTypeCompilance(type);
            GetCacheableCollection(type).DiscardAll();
        }

        public void DiscardAll<T>() where T : class, ICache, new()
        {
            GetCacheableCollection(typeof(T)).DiscardAll();
        }

        private void CheckTypeCompilance(Type type)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type), "Check type compilance failed. Type is null.");
            }
            if (!type.IsClass || type.IsAbstract)
            {
                throw new ArgumentException("Check type compilance failed. Type must be a non-abstract class.", nameof(type));
            }
            if (!typeof(ICache).IsAssignableFrom(type))
            {
                throw new ArgumentException("Check type compilance failed. Type is not a ICacheable type.", nameof(type));
            }
#endif
        }

        private CacheCollection GetCacheableCollection(Type type)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type), "Get GetCacheableCollection failed. Type is null.");
            }

            if (!_cacheCollections.TryGetValue(type, out CacheCollection cacheableCollection))
            {
                cacheableCollection = new CacheCollection(type);
                _cacheCollections.Add(type, cacheableCollection);
            }
            return cacheableCollection;
        }
    }
}