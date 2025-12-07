#if UNITY_EDITOR

namespace Eitan.SherpaONNXUnity.Editor.Localization
{
    using System;
    using System.Collections.Generic;
    using Eitan.SherpaONNXUnity.Runtime;
    using UnityEngine;

    /// <summary>
    /// JSON-serializable representation of a localization table.
    /// </summary>
    [Serializable]
    internal sealed class SherpaONNXLocalizationTable
    {
        [SerializeField]
        private Entry[] entries = Array.Empty<Entry>();

        internal static Dictionary<string, string> Parse(string json)
        {
            var dict = new Dictionary<string, string>(StringComparer.Ordinal);
            if (string.IsNullOrWhiteSpace(json))
            {
                return dict;
            }

            SherpaONNXLocalizationTable table = null;
            try
            {
                table = JsonUtility.FromJson<SherpaONNXLocalizationTable>(json);
            }
            catch (Exception ex)
            {
                SherpaLog.Error($"Failed to parse SherpaONNX localization JSON: {ex.Message}", category: "Localization");
                return dict;
            }

            if (table?.entries == null)
            {
                return dict;
            }

            foreach (var entry in table.entries)
            {
                if (string.IsNullOrWhiteSpace(entry.key))
                {
                    continue;
                }

                dict[entry.key.Trim()] = entry.value ?? string.Empty;
            }

            return dict;
        }

        [Serializable]
        private struct Entry
        {
            public string key;
            public string value;
        }
    }
}

#endif
