// File: Packages/com.eitan.sherpa-onnx-unity/Runtime/Core/Structs/SherpaEnvironment.cs

#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;

namespace Eitan.SherpaONNXUnity.Runtime
{
    /// <summary>
    /// Lightweight in-memory environment store for runtime configuration.
    /// Thread-safe, fast, and easy to extend via EnvKeys partial class.
    /// </summary>
    public static partial class SherpaONNXEnvironment
    {
        // Case-insensitive keys for developer convenience; adjust if you need strict case.
        private static readonly IEqualityComparer<string> _comparer = StringComparer.OrdinalIgnoreCase;
        private static readonly ConcurrentDictionary<string, string> _store =
            new ConcurrentDictionary<string, string>(_comparer);

        /// <summary>
        /// Raised after a value changes (Set or Remove). Subscribe if you need to react to changes.
        /// </summary>
        public static event Action<Change>? Changed;

        /// <summary>Represents a change in the environment.</summary>
        public readonly struct Change
        {
            public enum Kind { Set, Removed }
            public string Key { get; }
            public string? OldValue { get; }
            public string? NewValue { get; }
            public Kind Type { get; }
            internal Change(string key, string? oldValue, string? newValue, Kind type)
            {
                Key = key; OldValue = oldValue; NewValue = newValue; Type = type;
            }
        }

        /// <summary>Sets a string value. Pass null to remove.</summary>
        public static void Set(string key, string? value)
        {
            var k = NormalizeKey(key);
            if (value is null)
            {
                Remove(k);
                return;
            }

            _store.AddOrUpdate(k,
                addValueFactory: _ =>
                {
                    OnChanged(new Change(k, null, value, Change.Kind.Set));
                    return value;
                },
                updateValueFactory: (_, old) =>
                {
                    if (!string.Equals(old, value, StringComparison.Ordinal))
                    {
                        OnChanged(new Change(k, old, value, Change.Kind.Set));
                    }


                    return value;
                });
        }

        /// <summary>Sets a value using invariant culture formatting.</summary>
        public static void Set<T>(string key, T value) where T : IFormattable =>
            Set(key, value.ToString(null, CultureInfo.InvariantCulture));

        /// <summary>Try get raw string value.</summary>
        public static bool TryGet(string key, out string value)
        {
            var k = NormalizeKey(key);
            return _store.TryGetValue(k, out value!);
        }

        /// <summary>Get raw string, or default if not found.</summary>
        public static string Get(string key, string defaultValue = "") =>
            TryGet(key, out var v) ? v : defaultValue;

        /// <summary>Get a bool (accepts: true/false/1/0/yes/no/on/off). Returns default on parse failure.</summary>
        public static bool GetBool(string key, bool @default = false)
        {
            if (!TryGet(key, out var s))
            {
                return @default;
            }


            if (bool.TryParse(s, out var b))
            {
                return b;
            }


            switch (s.Trim().ToLowerInvariant())
            {
                case "1": case "yes": case "y": case "on": return true;
                case "0": case "no": case "n": case "off": return false;
                default: return @default;
            }
        }

        /// <summary>Get an int. Returns default on parse failure.</summary>
        public static int GetInt(string key, int @default = 0) =>
            TryParseInvariant<int>(key, int.TryParse, @default);

        /// <summary>Get a float (Single). Returns default on parse failure.</summary>
        public static float GetFloat(string key, float @default = 0f) =>
            TryParseInvariant<float>(key, float.TryParse, @default);

        /// <summary>Get a double. Returns default on parse failure.</summary>
        public static double GetDouble(string key, double @default = 0d) =>
            TryParseInvariant<double>(key, double.TryParse, @default);

        /// <summary>Get a TimeSpan (supports standard/ISO formats). Returns default on parse failure.</summary>
        public static TimeSpan GetTimeSpan(string key, TimeSpan @default)
        {
            if (!TryGet(key, out var s))
            {
                return @default;
            }
            // Use the simplest and widest-available overload for Unity runtimes
            // to maximize compatibility.

            if (TimeSpan.TryParse(s, out var ts))
            {
                return ts;
            }


            return @default;
        }

        /// <summary>Remove a key. Returns true if it existed.</summary>
        public static bool Remove(string key)
        {
            var k = NormalizeKey(key);
            if (_store.TryRemove(k, out var old))
            {
                OnChanged(new Change(k, old, null, Change.Kind.Removed));
                return true;
            }
            return false;
        }

        /// <summary>Returns true if the key exists.</summary>
        public static bool Contains(string key) => _store.ContainsKey(NormalizeKey(key));

        /// <summary>Remove all keys. Fires a single change notification with key="*".</summary>
        public static void Clear()
        {
            if (_store.IsEmpty)
            {
                return;
            }


            _store.Clear();
            OnChanged(new Change("*", null, null, Change.Kind.Removed));
        }

        /// <summary>Returns a thread-safe snapshot copy of all key/values.</summary>
        public static IReadOnlyDictionary<string, string> Snapshot()
        {
            // Copy to avoid exposing internal state.
            return new Dictionary<string, string>(_store, _comparer);
        }

        /// <summary>Bulk import (replaces existing keys with same name).</summary>
        public static void Load(IDictionary<string, string> data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }


            foreach (var kv in data)
            {
                if (kv.Key is null)
                {
                    continue; // skip invalid
                }


                Set(kv.Key, kv.Value);
            }
        }

        /// <summary>Export all key/values to a new Dictionary.</summary>
        public static Dictionary<string, string> Export() =>
            new Dictionary<string, string>(_store, _comparer);

        // ---------- Helpers ----------

        private static string NormalizeKey(string key)
        {
            if (key is null)
            {
                throw new ArgumentNullException(nameof(key));
            }


            var k = key.Trim();
            if (k.Length == 0)
            {
                throw new ArgumentException("Key must not be empty or whitespace.", nameof(key));
            }


            return k;
        }

        private static T TryParseInvariant<T>(string key, TryParseHandler<T> parser, T @default)
        {
            if (TryGet(key, out var s) &&
                parser(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
            {
                return value;
            }
            return @default;
        }

        private delegate bool TryParseHandler<T>(string s, NumberStyles style, IFormatProvider provider, out T result);

        private static void OnChanged(Change change)
        {
            // Avoid race: copy delegate before invoke.
            var handler = Changed;
            handler?.Invoke(change);
        }

        // ---------- Common Well-Known Keys (extend via partial class) ----------
        /// <summary>
        /// Well-known environment keys. Authors can extend this via partial class in their own files.
        /// </summary>
        public static partial class BuiltinKeys
        {
            // Add your reusable keys here to promote consistency across plugins.
            public const string GithubProxy = "SherpaONNX.GithubProxy";        // e.g., "https://gh-proxy.com/"
            public const string FetchLatestManifest = "SherpaONNX.FetchLatestManifest"; // bool, default true
            public const string AutoDownloadModels = "SherpaONNX.AutoDownloadModels";   // bool, default true
            public const string ChecksumCacheDirectory = "SherpaONNX.ChecksumCacheDirectory"; // optional override path
            public const string ChecksumCacheTtlSeconds = "SherpaONNX.ChecksumCacheTtlSeconds"; // int, default 3600 (1h)
            public const string LoggingEnabled = "SherpaONNX.Logging.Enabled"; // bool, default true
            public const string LoggingLevel = "SherpaONNX.Logging.Level";     // string enum name, default Info
            public const string LoggingTraceStacks = "SherpaONNX.Logging.TraceStacks"; // bool, default true
        }
    }
}
