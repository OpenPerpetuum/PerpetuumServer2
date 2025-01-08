using Perpetuum.Log;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.Caching;

namespace Perpetuum
{
    public static class ObjectCacheExtensions
    {
        [CanBeNull]
        public static T? Get<T>(this ObjectCache cache, string key, Func<T> valueFactory, TimeSpan? expiration = null)
        {
            object? result = cache.Get(key);

            if (result == null)
            {
                result = valueFactory();

                if (result == null)
                {
                    return default;
                }

                Set(cache, key, result, expiration);
            }

            return (T)result;
        }

        public static void Set(this ObjectCache objectCache, string key, object value, TimeSpan? expiration = null)
        {
            CacheItemPolicy policy = new()
            {
                RemovedCallback = HandleRemovedCacheItem
            };

            if (expiration == null)
            {
                policy.AbsoluteExpiration = ObjectCache.InfiniteAbsoluteExpiration;
            }
            else
            {
                policy.SlidingExpiration = (TimeSpan)expiration;
            }

            objectCache.Set(key, value, policy);

            Logger.Info($"Cache set. Name = {objectCache.Name} ({key} = {value}) expiration = {(expiration == null ? "never" : DateTime.Now.Add(policy.SlidingExpiration).ToString(CultureInfo.InvariantCulture))}");
        }

        [CanBeNull]
        public static T? GetWithAbsoluteExpiration<T>(this ObjectCache cache, string key, Func<T> valueFactory, TimeSpan expiration)
        {
            object? result = cache.Get(key);

            if (result == null)
            {
                result = valueFactory();

                if (result == null)
                {
                    return default;
                }

                SetWithAbsoluteExpiration(cache, key, result, expiration);
            }

            return (T)result;
        }


        private static void SetWithAbsoluteExpiration(this ObjectCache objectCache, string key, object value, TimeSpan expiration)
        {
            CacheItemPolicy policy = new()
            {
                RemovedCallback = HandleRemovedCacheItem,
                AbsoluteExpiration = new DateTimeOffset(DateTime.Now.Add(expiration))
            };

            objectCache.Set(key, value, policy);

            Logger.Info($"Cache set. Name = {objectCache.Name} ({key} = {value}) expiration = {(expiration == null ? "never" : policy.AbsoluteExpiration.ToString(CultureInfo.InvariantCulture))}");
        }

        private static void HandleRemovedCacheItem(CacheEntryRemovedArguments removedArguments)
        {
            IDisposable? disposable = removedArguments.CacheItem.Value as IDisposable;
            disposable?.Dispose();

            Logger.Info($"Cache remove. Name = {removedArguments.Source.Name} ({removedArguments.CacheItem.Key} = {removedArguments.CacheItem.Value}) reason = {removedArguments.RemovedReason}");
        }

        public static void Clear(this ObjectCache cache)
        {
            Debug.Assert(cache != null);

            List<string> list = cache.Select(kvp => kvp.Key).ToList();

            foreach (string? key in list)
            {
                cache.Remove(key);
            }
        }
    }
}
