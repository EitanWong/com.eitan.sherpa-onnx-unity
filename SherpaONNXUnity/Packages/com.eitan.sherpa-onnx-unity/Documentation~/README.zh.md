# SherpaONNXUnity 文档

[English](./README.en.md) | [文档入口](./README.md)

## 安装

- Unity 2021.3 LTS 或更新版本。
- 包名：`com.eitan.sherpa-onnx-unity`
- Git URL：

```text
https://github.com/EitanWong/com.eitan.sherpa-onnx-unity.git#upm
```

OpenUPM scoped registry：

```text
Name: OpenUPM
URL: https://package.openupm.com
Scope: com.eitan.sherpa-onnx-unity
```

## 快速开始

1. 安装包。
2. 在 Package Manager 中导入 **SherpaONNXUnity Sample**。
3. 打开 `Samples~/Collection` 下的示例场景。
4. 在 `Window/SherpaONNX/Model Manager` 中选择或下载模型。
5. 进入 Play。

如果使用场景组件方式，添加 `SherpaMicrophoneInput` 和对应模块组件，设置 `Model Id`，按需绑定音频输入，并订阅 UnityEvent 获取结果。

## 编辑器工具

| 工具 | 菜单 |
|---|---|
| 模型管理器 | `Window/SherpaONNX/Model Manager` |
| 性能分析器 | `Window/SherpaONNX/SherpaONNX Profiler` |
| 欢迎窗口 | `Help/SherpaONNX/Welcome` |
| 组件快捷创建 | `GameObject/SherpaONNX/...` |

## 功能说明

### <a id="speech-recognition"></a>语音识别

将语音转为文本。实时识别适合麦克风流，离线识别适合录音或音频文件。

- 组件：`RealtimeSpeechRecognizerComponent`、`OfflineSpeechRecognizerComponent`
- API：`SpeechRecognition.TranscribeAsync(...)`、`SpeechRecognition.SpeechTranscriptionAsync(...)`
- 示例：`RealtimeSpeechRecognition`、`OfflineSpeechRecognition`

### <a id="speech-synthesis"></a>语音合成

从文本生成语音，包含常规 TTS 和基于提示音频的零样本语音合成。

- 组件：`SpeechSynthesizerComponent`、`ZeroShotSpeechSynthesisComponent`
- API：`SpeechSynthesis.GenerateAsync(...)`、`SpeechSynthesis.GenerateZeroShotAsync(...)`
- 示例：`SpeechSynthesis`、`ZeroShotSpeechSynthesis`

### <a id="spoken-language-identification"></a>语种识别

从音频片段或采样缓冲区判断语种。

- 组件：`SpokenLanguageIdentificationComponent`
- API：`SpokenLanguageIdentification.IdentifyAsync(...)`
- 示例：`SpokenLanguageIdentification`

### <a id="keyword-spotting"></a>关键词检测

从流式或录制音频中检测唤醒词或配置的关键词。

- 组件：`KeywordSpottingComponent`
- API：`KeywordSpotting.StreamDetect(...)`、`KeywordSpotting.DetectAsync(...)`
- 示例：`KeywordSpotting`

### <a id="punctuation"></a>标点恢复

为识别文本恢复标点和大小写。

- 组件：`PunctuationComponent`
- API：`Punctuation.AddPunctuationAsync(...)`
- 示例：`Punctuation`

### <a id="speaker-identification"></a>说话人识别

通过说话人 embedding 识别或标记说话人，适合需要把语音关联到已知说话人的场景。

- API：speaker embedding 和说话人分析相关 API
- 相关组件：`SpeakerDiarizationComponent`
- 相关示例：`SpeakerDiarization`

### <a id="speaker-diarization"></a>说话人分离

按说话人切分音频，并返回带说话人标签的时间片段。

- 组件：`SpeakerDiarizationComponent`
- API：`SpeakerDiarization.DiarizeAsync(...)`
- 示例：`SpeakerDiarization`

### <a id="speaker-verification"></a>说话人验证

比较说话人 embedding，判断两段语音是否可能来自同一说话人。

- API：speaker embedding 和验证相关 API
- 相关组件：`SpeakerDiarizationComponent`
- 相关示例：`SpeakerDiarization`

### <a id="source-separation"></a>音源分离

将混合音频分离为不同 stem，例如人声和伴奏，具体输出取决于模型。

- 组件：`SourceSeparationComponent`
- API：`SourceSeparation.SeparateAsync(...)`
- 示例：`SourceSeparation`

### <a id="audio-tagging"></a>音频标签

识别音乐、环境声等音频事件类别。

- 组件：`AudioTaggingComponent`
- API：`AudioTagging.TagAsync(...)`、`AudioTagging.TagStreamAsync(...)`
- 示例：`AudioTagging`

### <a id="voice-activity-detection"></a>语音活动检测

检测语音边界和当前说话状态。

- 组件：`VoiceActivityDetectionComponent`
- API：`VoiceActivityDetection.StreamDetect(...)`、`VoiceActivityDetection.FlushAsync()`
- 示例：`VoiceActivityDetection`

### <a id="speech-enhancement"></a>语音增强

对语音音频进行降噪和增强。

- 组件：`SpeechEnhancementComponent`
- API：`SpeechEnhancement.EnhanceAsync(...)`、`SpeechEnhancement.ProcessStreamingAsync(...)`
- 示例：`SpeechEnhancement`

## 运行时配置

```csharp
SherpaONNXUnityAPI.SetAutoDownloadModels(false);
SherpaONNXUnityAPI.SetFetchLatestManifest(true);
SherpaONNXUnityAPI.SetDownloadAttemptTimeoutSeconds(600);
SherpaONNXUnityAPI.SetAllowInsecureModelDownload(false);
SherpaONNXUnityAPI.SetForceModelHashValidation(false);
SherpaONNXUnityAPI.SetGithubProxy("https://your-proxy/");
SherpaONNXUnityAPI.ClearChecksumCache();
```

环境变量：

- `SHERPA_ONNX_FETCH_LATEST_MANIFEST`
- `SHERPA_ONNX_AUTO_DOWNLOAD`
- `SHERPA_ONNX_AUTO_DELETE_CORRUPTED_MODELS`
- `SHERPA_ONNX_DOWNLOAD_ATTEMPT_TIMEOUT_SECONDS`
- `SHERPA_ONNX_ALLOW_INSECURE_MODEL_DOWNLOAD`
- `SHERPA_ONNX_FORCE_MODEL_HASH_VALIDATION`
- `SHERPA_ONNX_GITHUB_PROXY`
- `SHERPA_ONNX_CHECKSUM_CACHE_DIR`
- `SHERPA_ONNX_CHECKSUM_CACHE_TTL_SECONDS`
- `SHERPA_ONNX_LOGGING_ENABLED`
- `SHERPA_ONNX_LOGGING_LEVEL`
- `SHERPA_ONNX_LOGGING_TRACE_STACKS`

运行时修改进程环境变量后，调用 `SherpaONNXUnityAPI.ApplyEnvironmentOverridesFromProcess()` 使其生效。

## 自定义模型

运行时注册：

```csharp
SherpaONNXUnityAPI.RegisterCustomModel(metadata);
SherpaONNXUnityAPI.RegisterCustomModels(models);
```

最小清单条目：

```json
{
  "modelId": "your-model-id",
  "moduleType": 1,
  "moduleTypeHint": "SpeechRecognition",
  "downloadUrl": "https://your.cdn/path/to/model.zip",
  "downloadFileHash": "sha256-hex",
  "modelTypeHint": "",
  "fileBindings": [],
  "numberOfSpeakers": 0,
  "sampleRate": 16000
}
```

## 平台说明

- `0.1.3-exp.4` 同步 sherpa-onnx 原生库至 v1.13.0。
- iOS 使用内置静态原生库，支持 Unity iOS 构建。
- Android 生产环境推荐 `arm64-v8a`。
- Android `armeabi-v7a` 仍可用，但部分上游原生模型路径可能不稳定。
