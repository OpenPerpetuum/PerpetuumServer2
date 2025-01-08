using System.Collections;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;

namespace Perpetuum
{
    public static class DictionaryExtensions
    {
        public static void Add<TK, TV>(this ConcurrentDictionary<TK, TV> d, TK key, TV value)
        {
            Debug.Assert(!d.ContainsKey(key), "key already exists! (" + key + ")");
            d[key] = value;
        }

        /// <summary>
        /// Safe remove from a concurrent dictionary
        /// </summary>
        public static bool Remove<TK, TV>(this ConcurrentDictionary<TK, TV> d, TK key, out TV value)
        {
            if (d == null)
            {
                value = default;
                return false;
            }

            if (d.TryRemove(key, out value))
            {
                return true;
            }

            SpinWait sw = new();
            while (d.ContainsKey(key))
            {
                if (d.TryRemove(key, out value))
                {
                    return true;
                }

                sw.SpinOnce();
            }

            value = default;
            return false;
        }

        public static bool Remove<TK, TV>(this ConcurrentDictionary<TK, TV> d, TK key)
        {
            return Remove(d, key, out _);
        }

        public static IEnumerable<TV> GetNonBlockingValues<TK, TV>(this IEnumerable<KeyValuePair<TK, TV>> enumerable)
        {
            return enumerable.Select(kvp => kvp.Value);
        }

        public static void AddRange<TK, TV>(this IDictionary<TK, TV> source, IEnumerable<KeyValuePair<TK, TV>> collection)
        {
            if (collection == null)
            {
                return;
            }

            foreach (KeyValuePair<TK, TV> kvp in collection)
            {
                source[kvp.Key] = kvp.Value;
            }
        }

        public static void RemoveRange<TK, TV>(this IDictionary<TK, TV> source, IEnumerable<TK> collection)
        {
            if (collection == null)
            {
                return;
            }

            using IEnumerator<TK> e = collection.GetEnumerator();
            while (e.MoveNext())
            {
                source.Remove(e.Current);
            }
        }

        public static T GetValue<T>(this IDictionary<string, object> dictionary, string key)
        {
            return (T)dictionary[key];
        }

        public static TV GetOrDefault<TK, TV>(this IDictionary<TK, TV> dictionary, TK key, TV defaultValue = default)
        {
            if (dictionary == null)
            {
                return defaultValue;
            }

            if (!dictionary.TryGetValue(key, out TV result))
            {
                result = defaultValue;
            }
            return result;
        }

        public static T? GetOrDefault<T>(this IDictionary<string, object> dictionary, string key, T defaultValue = default)
        {
            return dictionary == null ? default : !dictionary.TryGetValue(key, out object value) ? defaultValue : (T)value;
        }


        public static T? GetOrDefault<T>(this IDictionary<string, object> dictionary, string key, Func<T> valueFactory)
        {
            return dictionary == null
                ? default
                : !dictionary.TryGetValue(key, out object value) ? valueFactory == null ? default : valueFactory() : (T)value;
        }

        public static bool TryGetValue<TK, TV, T>(this IDictionary<TK, TV> dictionary, TK key, out T value) where T : TV
        {
            if (dictionary == null)
            {
                value = default;
                return false;
            }
            if (!dictionary.TryGetValue(key, out TV currValue))
            {
                value = default;
                return false;
            }
            value = (T)currValue;
            return true;
        }

        public static bool TryAdd<TK, TV>(this IDictionary<TK, TV> dictionary, TK key, TV value)
        {
            if (dictionary == null)
            {
                return false;
            }

            if (dictionary.ContainsKey(key))
            {
                return false;
            }

            dictionary.Add(key, value);
            return true;
        }

        public static TV GetOrAdd<TK, TV>(this IDictionary<TK, TV> dictionary, TK key) where TV : new()
        {
            return dictionary is ConcurrentDictionary<TK, TV> concurrentDictionary ? concurrentDictionary.GetOrAdd(key, () => new TV()) : dictionary.GetOrAdd(key, () => new TV());
        }

        public static TV GetOrAdd<TK, TV>(this IDictionary<TK, TV> dictionary, TK key, Func<TV> creator)
        {
            if (!dictionary.TryGetValue(key, out TV value))
            {
                value = creator();
                dictionary.Add(key, value);
            }
            return value;
        }

        public static bool Compare<TK, TV>(this IDictionary<TK, TV> left, Dictionary<TK, TV> right)
        {
            if (left == null && right == null)
            {
                return true;
            }

            if (left == null || right == null)
            {
                return false;
            }

            if (left.Count != right.Count)
            {
                return false;
            }

            foreach (KeyValuePair<TK, TV> leftKvP in left)
            {
                if (!right.TryGetValue(leftKvP.Key, out TV rightValue))
                {
                    return false;
                }

                if (!Equals(leftKvP.Value, rightValue))
                {
                    return false;
                }
            }
            return true;
        }

        public static void AddOrUpdate<TK, TV>(this IDictionary<TK, TV> dictionary, TK key, TV newValue, Func<TV, TV> updateFunc)
        {
            if (dictionary == null)
            {
                return;
            }

            if (!dictionary.TryGetValue(key, out TV currentValue))
            {
                dictionary.Add(key, newValue);
                return;
            }

            dictionary[key] = updateFunc(currentValue);
        }

        public static void AddOrUpdate<TK, TV>(this Dictionary<TK, TV> dictionary, TK key, Func<TV> newValueFunc, Func<TV, TV> updateFunc)
        {
            if (!dictionary.TryGetValue(key, out TV currentValue))
            {
                TV? newValue = newValueFunc();
                dictionary.Add(key, newValue);
                return;
            }

            dictionary[key] = updateFunc(currentValue);
        }

        public static string ToInsertString(this IEnumerable<KeyValuePair<string, object>> dictionary, string tableName, string? exceptKey = null)
        {
            List<string> listKeys = [];
            List<object> listValues = [];

            foreach (KeyValuePair<string, object> pair in dictionary)
            {
                if (exceptKey != null && pair.Key == exceptKey)
                {
                    continue;
                }

                listKeys.Add(pair.Key);
                listValues.Add(pair.Value);

            }

            return "insert " + tableName + " (" + listKeys.ArrayToString() + ") values (" + listValues.ArrayToValueString() + ")";
        }

        public static IEnumerable<TValue> GetValues<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, IEnumerable<TKey> keys)
        {
            foreach (TKey? index in keys)
            {
                if (dictionary.TryGetValue(index, out TValue item))
                {
                    yield return item;
                }
            }
        }

        public static string ToDebugString<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>> dictionary)
        {
            if (dictionary == null)
            {
                return string.Empty;
            }

            StringBuilder builder = new();

            builder.Append('{');

            bool first = true;

            foreach (KeyValuePair<TKey, TValue> kvp in dictionary)
            {
                if (first)
                {
                    first = false;
                }
                else
                {
                    builder.Append(',');
                }

                builder.Append(kvp.Key + "=" + kvp.Value);
            }

            builder.Append('}');

            return builder.ToString();
        }

        public static IDictionary<TK, TV> ToReadOnlyDictionary<TK, TV>(this IDictionary<TK, TV> dictionary)
        {
            return new ReadOnlyDictionary<TK, TV>(dictionary);
        }

        public static IDictionary<string, object>? ToDictionary(this IDictionary dictionary)
        {
            if (dictionary == null)
            {
                return null;
            }

            Dictionary<string, object> result = [];

            foreach (DictionaryEntry entry in dictionary)
            {
                result[entry.Key.ToString()] = entry.Value;
            }

            return result;
        }


    }
}