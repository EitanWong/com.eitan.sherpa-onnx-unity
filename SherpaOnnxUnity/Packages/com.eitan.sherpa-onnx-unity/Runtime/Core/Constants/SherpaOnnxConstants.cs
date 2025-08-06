using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Eitan.SherpaOnnxUnity.Runtime.Constants
{
    public partial class SherpaOnnxConstants
    {

        public const string RootDirectoryName = "sherpa-onnx";
        public const string ManifestFileName = "manifest.json";

        public const string ModelRootDirectoryName = "models";

        public const string githubProxyUrl = "https://gh-proxy.com/";


        private static string GetModelDownloadUrl(string modelId)
        {
            var sherpaModelType = Utilities.SherpaUtils.Model.GetModuleTypeByModelId(modelId);
            var typeName = string.Empty;
            switch (sherpaModelType)
            {
                case SherpaOnnxModuleType.SpeechRecognition:
                    typeName = "asr-models";
                    break;
                case SherpaOnnxModuleType.VoiceActivityDetection:
                    typeName = "asr-models"; // i know it's weird but it's work.
                    break;
                case SherpaOnnxModuleType.SpeechSynthesis:
                    typeName = "tts-models";
                    break;
                case SherpaOnnxModuleType.KeywordSpotting:
                    typeName = "kws-models";
                    break;
                case SherpaOnnxModuleType.SpeechEnhancement:
                    typeName = "speech-enhancement-models";
                    break;
            }

            return $"{githubProxyUrl}https://github.com/k2-fsa/sherpa-onnx/releases/download/{typeName}/{modelId}.tar.bz2";
        }



        /// <summary>
        /// 使用 JsonUtility 生成兼容的默认 manifest 内容。
        /// </summary>
        /// <returns>一个 JSON 字符串。</returns>
        public static string GetDefaultManifestContent()
        {
            var manifest = new SherpaOnnxModelManifest();

            AddToManifest(manifest, SherpaOnnxConstants.Models.ASR_MODELS_METADATA_TABLES, SherpaOnnxModuleType.SpeechRecognition);
            AddToManifest(manifest, SherpaOnnxConstants.Models.VAD_MODELS_METADATA_TABLES, SherpaOnnxModuleType.VoiceActivityDetection);
            AddToManifest(manifest, SherpaOnnxConstants.Models.TTS_MODELS_METADATA_TABLES, SherpaOnnxModuleType.SpeechSynthesis);
            AddToManifest(manifest, SherpaOnnxConstants.Models.KWS_MODELS_METADATA_TABLES, SherpaOnnxModuleType.KeywordSpotting);
            AddToManifest(manifest, SherpaOnnxConstants.Models.SPEECH_ENHANCEMENT_MODELS_METADATA_TABLES, SherpaOnnxModuleType.SpeechEnhancement);
            
            // 使用 JsonUtility 进行序列化，'true' 表示格式化输出（带缩进，易读）
            return JsonUtility.ToJson(manifest, true);
        }

        private static void AddToManifest(SherpaOnnxModelManifest manifest, SherpaOnnxModelMetadata[] modelMetadataList, SherpaOnnxModuleType moduleType)
        {

            foreach (var modelConfig in modelMetadataList)
            {
                if (string.IsNullOrEmpty(modelConfig.downloadUrl))
                {
                    modelConfig.downloadUrl = GetModelDownloadUrl(modelConfig.modelId);
                }
                
                modelConfig.moduleType = moduleType;
                manifest.models.Add(modelConfig);
            }

        }

    }
}