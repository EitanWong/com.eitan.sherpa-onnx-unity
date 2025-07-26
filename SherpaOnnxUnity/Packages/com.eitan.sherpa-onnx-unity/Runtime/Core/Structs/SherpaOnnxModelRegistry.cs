using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
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

        private SherpaOnnxModelManifest _manifest;
        private Task _initializationTask;

        public bool IsInitialized => _initializationTask != null && _initializationTask.IsCompletedSuccessfully;

        private SherpaOnnxModelRegistry() { }


        private async Task InitializeInternalAsync()
        {
            string jsonContent = await ReadManifestFileAsync();

            if (!string.IsNullOrEmpty(jsonContent))
            {
                try
                {
                    _manifest = JsonUtility.FromJson<SherpaOnnxModelManifest>(jsonContent);
                    PopulateDictionaryFromManifest(_manifest);
                    // Debug.Log($"Successfully loaded {_modelData.Count} model(s) from manifest.");
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Failed to parse model manifest with JsonUtility: {ex.Message}.");
                }
            }
            else
            {
                Debug.LogError($"FATAL: Failed to read model manifest. The sherpa-onnx model cannot function without it, please create it in StreamingAssets");
            }
        }

        private void PopulateDictionaryFromManifest(SherpaOnnxModelManifest manifest)
        {
            _modelData.Clear();
            if (manifest == null || manifest.models == null)
            {
                return;
            }
            foreach (var metadata in manifest.models)
            {
                if (!string.IsNullOrEmpty(metadata.modelId) && !_modelData.ContainsKey(metadata.modelId))
                {
                    _modelData.Add(metadata.modelId, metadata);
                }
                else
                {
                    Debug.LogWarning($"Duplicate or invalid modelId found in manifest: '{metadata.modelId}'. Entry skipped.");
                }
            }
        }

        private async Task<string> ReadManifestFileAsync()
        {

            string directoryPath = Path.Combine(Application.streamingAssetsPath, SherpaOnnxConstants.RootDirectoryName);
            string manifestPath = Path.Combine(directoryPath, SherpaOnnxConstants.ManifestFileName);

#if !UNITY_ANDROID && !UNITY_IOS && !UNITY_WEBGL
            if (!File.Exists(manifestPath))
            {

                string defaultJson = SherpaOnnxConstants.GetDefaultManifestContent();
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }
                await File.WriteAllTextAsync(manifestPath, defaultJson);
            }

            if (File.Exists(manifestPath))
            {
                return await File.ReadAllTextAsync(manifestPath);
            }

#else
            using (UnityWebRequest www = UnityWebRequest.Get(manifestPath))
            {
                var operation = www.SendWebRequest();
                while (!operation.isDone) await Task.Yield();
                return www.result == UnityWebRequest.Result.Success ? www.downloadHandler.text : null;
            }
#endif

            return null;
        }

        public async Task<SherpaOnnxModelMetadata> GetMetadataAsync(string modelId)
        {
            if (_initializationTask == null)
            {
                _initializationTask = InitializeInternalAsync();
            }
            await _initializationTask;

            if (_modelData.TryGetValue(modelId, out var metadata))
            {
                //select all modelFiles add fullPath
                for (int i = 0; i < metadata.modelFileNames.Length; i++)
                {
                    metadata.modelFileNames[i] = SherpaPathResolver.GetModelFilePath(modelId, metadata.modelFileNames[i]);
                }

                return metadata;
            }

            Debug.LogError($"Metadata for modelId '{modelId}' not found in the manifest.");
            return null;
        }
        
        
        public async Task<SherpaOnnxModelManifest> GetManifestAsync()
        {
            if (_initializationTask == null)
            {
                _initializationTask = InitializeInternalAsync();
            }
            await _initializationTask;
            return _manifest;
        }
    }
}