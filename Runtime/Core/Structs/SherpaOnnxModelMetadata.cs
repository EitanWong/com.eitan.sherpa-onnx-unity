// File: Eitan.SherpaOnnxUnity.Runtime/ModelMetadata.cs
using System.Collections.Generic;

namespace Eitan.SherpaOnnxUnity.Runtime
{
    // 这个类代表 JSON 文件中的一个模型条目
    [System.Serializable]
    public class SherpaOnnxModelMetadata
    {
        public string modelId; // modelId 模型的id 例如sherpa-onnx-streaming-zipformer-en-2023-02-21
        public SherpaOnnxModuleType moduleType;
        public string downloadUrl;
        public string downloadFileHash;
        public string[] modelFileNames;
        public string[] modelFileHashes;
    }

    // 这个类代表整个 JSON 文件的根结构
    [System.Serializable]
    public class SherpaOnnxModelManifest
    {
        public List<SherpaOnnxModelMetadata> models = new List<SherpaOnnxModelMetadata>();
    }
}