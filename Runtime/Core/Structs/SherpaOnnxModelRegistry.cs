using System;
using System.Collections.Generic;
using Eitan.SherpaOnnxUnity.Runtime.Constants;
using Eitan.SherpaOnnxUnity.Runtime.Utilities;
using UnityEngine;

namespace Eitan.SherpaOnnxUnity.Runtime
{
    public class SherpaOnnxModelRegistry
    {
        private static readonly SherpaOnnxModelRegistry _instance = new SherpaOnnxModelRegistry();
        public static SherpaOnnxModelRegistry Instance => _instance;

        private readonly Dictionary<string, SherpaOnnxModelMetadata> _modelData = new Dictionary<string, SherpaOnnxModelMetadata>();
        private readonly HashSet<string> _resolvedModelIds = new HashSet<string>();

        private SherpaOnnxModelManifest _manifest;

        public bool IsInitialized { get; private set; }

        private SherpaOnnxModelRegistry() { }


        /// <summary>
        /// Initialize the registry from the default manifest once. Safe to call multiple times.
        /// </summary>
        private void InitializeInternal()
        {
            if (IsInitialized)
            {
                return;
            }


            try
            {
                _manifest = SherpaOnnxConstants.GetDefaultManifest();
                _resolvedModelIds.Clear();
                PopulateDictionaryFromManifest(_manifest);
                IsInitialized = true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to parse model manifest with JsonUtility: {ex.Message}.");
                IsInitialized = false;
            }
        }

        private void PopulateDictionaryFromManifest(SherpaOnnxModelManifest manifest)
        {
            _modelData.Clear();
            if (manifest?.models == null || manifest.models.Count == 0)
            {
                return;
            }

            foreach (var metadata in manifest.models)
            {
                if (string.IsNullOrWhiteSpace(metadata.modelId))
                {
                    Debug.LogWarning("Encountered a model entry with an empty modelId. Entry skipped.");
                    continue;
                }

                if (!_modelData.ContainsKey(metadata.modelId))
                {
                    _modelData.Add(metadata.modelId, metadata);
                }
                else
                {
                    Debug.LogWarning($"Duplicate modelId in manifest: '{metadata.modelId}'. Entry skipped.");
                }
            }
        }

//         private async Task<string> ReadManifestFileAsync()
//         {

//             string directoryPath = Path.Combine(Application.streamingAssetsPath, SherpaOnnxConstants.RootDirectoryName);
//             string manifestPath = Path.Combine(directoryPath, SherpaOnnxConstants.ManifestFileName);

// #if (!UNITY_ANDROID && !UNITY_IOS && !UNITY_WEBGL)
//             if (!File.Exists(manifestPath))
//             {
//                 string defaultJson = SherpaOnnxConstants.GetDefaultManifestContent();
//                 if (!Directory.Exists(directoryPath))
//                 {
//                     Directory.CreateDirectory(directoryPath);
//                 }
//                 await File.WriteAllTextAsync(manifestPath, defaultJson);
//             }

//             if (File.Exists(manifestPath))
//             {
//                 return await File.ReadAllTextAsync(manifestPath);
//             }
//             return null;
// #else
//             using (UnityWebRequest www = UnityWebRequest.Get(manifestPath))
//             {
//                 var operation = www.SendWebRequest();
//                 while (!operation.isDone)
//                 {
//                     await Task.Yield();
//                 }
                
//                 return www.result == UnityWebRequest.Result.Success ? www.downloadHandler.text : null;
//             }

// #endif
//         }

        /// <summary>
        /// Get metadata for a specific modelId. Resolves model file names to absolute paths on first access.
        /// </summary>
        public SherpaOnnxModelMetadata GetMetadata(string modelId)
        {
            if (!IsInitialized)
            {
                InitializeInternal();
            }

            if (_modelData.TryGetValue(modelId, out var metadata))
            {
                // Resolve model file names to absolute paths only once per modelId
                if (!_resolvedModelIds.Contains(modelId))
                {
                    for (int i = 0; i < metadata.modelFileNames.Length; i++)
                    {
                        metadata.modelFileNames[i] = SherpaPathResolver.GetModelFilePath(modelId, metadata.modelFileNames[i]);
                    }
                    _resolvedModelIds.Add(modelId);
                }

                return metadata;
            }

            Debug.LogError($"Metadata for modelId '{modelId}' not found in the manifest.");
            return null;
        }
        
        
        /// <summary>
        /// Get the loaded manifest. Triggers lazy initialization if necessary.
        /// </summary>
        public SherpaOnnxModelManifest GetManifest()
        {
            if (!IsInitialized)
            {
                InitializeInternal();
            }
            return _manifest;
        }
    }
}