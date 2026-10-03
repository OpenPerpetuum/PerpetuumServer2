using System;
using System.Collections.Generic;

namespace Perpetuum.CliClient
{
    public static class ClientDictionaryExtensions
    {
        public static bool GetBool(this IDictionary<string, object>? dict, string key, bool defaultValue = false)
        {
            if (dict == null || !dict.TryGetValue(key, out var val) || val == null)
            {
                return defaultValue;
            }

            if (val is bool b) return b;
            if (val is int i) return i != 0;
            if (val is long l) return l != 0;
            if (val is byte by) return by != 0;
            if (val is short sh) return sh != 0;
            if (val is string s)
            {
                if (bool.TryParse(s, out var sb)) return sb;
                if (int.TryParse(s, out var si)) return si != 0;
            }

            try
            {
                return Convert.ToBoolean(val);
            }
            catch
            {
                return defaultValue;
            }
        }

        public static int GetInt(this IDictionary<string, object>? dict, string key, int defaultValue = 0)
        {
            if (dict == null || !dict.TryGetValue(key, out var val) || val == null)
            {
                return defaultValue;
            }

            if (val is int i) return i;
            if (val is long l) return (int)l;
            if (val is short sh) return sh;
            if (val is byte b) return b;
            if (val is double d) return (int)d;
            if (val is float f) return (int)f;
            if (val is string s && int.TryParse(s, out var si)) return si;

            try
            {
                return Convert.ToInt32(val);
            }
            catch
            {
                return defaultValue;
            }
        }

        public static long GetLong(this IDictionary<string, object>? dict, string key, long defaultValue = 0)
        {
            if (dict == null || !dict.TryGetValue(key, out var val) || val == null)
            {
                return defaultValue;
            }

            if (val is long l) return l;
            if (val is int i) return i;
            if (val is short sh) return sh;
            if (val is byte b) return b;
            if (val is double d) return (long)d;
            if (val is float f) return (long)f;
            if (val is string s && long.TryParse(s, out var sl)) return sl;

            try
            {
                return Convert.ToInt64(val);
            }
            catch
            {
                return defaultValue;
            }
        }

        public static double GetDouble(this IDictionary<string, object>? dict, string key, double defaultValue = 0.0)
        {
            if (dict == null || !dict.TryGetValue(key, out var val) || val == null)
            {
                return defaultValue;
            }

            if (val is double d) return d;
            if (val is float f) return f;
            if (val is int i) return i;
            if (val is long l) return l;
            if (val is short sh) return sh;
            if (val is byte b) return b;
            if (val is string s && double.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var sd)) return sd;

            try
            {
                return Convert.ToDouble(val, System.Globalization.CultureInfo.InvariantCulture);
            }
            catch
            {
                return defaultValue;
            }
        }

        public static string GetString(this IDictionary<string, object>? dict, string key, string defaultValue = "")
        {
            if (dict == null || !dict.TryGetValue(key, out var val) || val == null)
            {
                return defaultValue;
            }

            return val.ToString() ?? defaultValue;
        }

        public static DateTime? GetDateTime(this IDictionary<string, object>? dict, string key)
        {
            if (dict == null || !dict.TryGetValue(key, out var val) || val == null)
            {
                return null;
            }

            if (val is DateTime dt) return dt;
            if (val is string s && DateTime.TryParse(s, out var dts)) return dts;

            return null;
        }

        public static IDictionary<string, object>? GetDictionary(this IDictionary<string, object>? dict, string key)
        {
            if (dict == null || !dict.TryGetValue(key, out var val) || val == null)
            {
                return null;
            }

            return val as IDictionary<string, object>;
        }
    }
}
