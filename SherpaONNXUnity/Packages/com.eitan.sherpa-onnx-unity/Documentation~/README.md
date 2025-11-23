# SherpaONNXUnity Documentation

> Bilingual quick reference for the sherpa-onnx Unity package.

## Table of Contents / 目录
- [English Guide](#english-guide)
  - [1. Installation](#1-installation)
  - [2. Quick Start](#2-quick-start)
  - [3. Modules & APIs](#3-modules--apis)
  - [4. Mono Components](#4-mono-components)
  - [5. Samples](#5-samples)
  - [6. Troubleshooting](#6-troubleshooting)
- [中文指南](#中文指南)
  - [1. 安装](#1-安装)
  - [2. 快速上手](#2-快速上手)
  - [3. 模块与API](#3-模块与api)
  - [4. 拖拽式组件](#4-拖拽式组件)
  - [5. 示例场景](#5-示例场景)
  - [6. 常见问题](#6-常见问题)

---

## English Guide

### 1. Installation
- Unity 2021.3 LTS or newer.
- OpenUPM registry (Package Manager → Project Settings):
  - Name: `OpenUPM`
  - URL: `https://package.openupm.com`
  - Scope: `com.eitan.sherpa-onnx-unity`
  - (Reference image: `images/package-manager-scopes.png`)
- Git URL (experimental channel):
  ```
  https://github.com/EitanWong/com.eitan.sherpa-onnx-unity.git#upm
  ```

### 2. Quick Start
- **Drop-in components (no boilerplate):**
  1) Add `SherpaMicrophoneInput` to emit PCM chunks.  
  2) Add a module component (e.g., `SpeechRecognizerComponent`, `AudioTaggingComponent`, `VoiceActivityDetectionComponent`, `ZeroShotSpeechSynthesisComponent`) and set the `Model Id`.  
  3) Subscribe to UnityEvents (`TranscriptionReadyEvent`, `ClipReadyEvent`, `OnKeywordDetected`, etc.).
  4) Press Play; capture starts when the model finishes loading.
- **Scripting (ASR):**
  ```csharp
  var asr = new SpeechRecognition("your-model-id", 16000);
  var text = await asr.SpeechTranscriptionAsync(samples, sampleRate);
  ```
- **Scripting (Zero-shot TTS):**
  ```csharp
  var tts = new SpeechSynthesis("zipvoice-demo", -1);
  var clip = await tts.GenerateZeroShotAsync(
      "Hello world",
      "Reference prompt text",
      promptClip,
      speed: 1f,
      numSteps: 4);
  ```
- **Model Manager:** Window → Sherpa ONNX → Model Manager. Use `SherpaONNXUnityAPI.SetGithubProxy("https://your-proxy/")` if downloads are slow.

#### Runtime configuration API
These helpers mirror the ScriptableObject/environment keys (e.g., SHERPA_ONNX_AUTO_DOWNLOAD):
```csharp
SherpaONNXUnityAPI.SetAutoDownloadModels(false);
SherpaONNXUnityAPI.SetFetchLatestManifest(true);
SherpaONNXUnityAPI.SetChecksumCacheDirectory(
    Path.Combine(Application.persistentDataPath, "SherpaCache"));
SherpaONNXUnityAPI.SetChecksumCacheTtlSeconds(0);
```

### 3. Modules & APIs
- `SpeechRecognition` — `SpeechTranscriptionAsync(float[] samples, int sampleRate)` supports online/offline models automatically.
- `Punctuation` — `AddPunctuationAsync(string text)` to restore punctuation/casing.
- `KeywordSpotting` — configure keywords in constructor; call `DetectAsync(samples, sampleRate)` and subscribe to `OnKeywordDetected`.
- `AudioTagging` — `TagAsync(samples, sampleRate, topK)` for one-shot or `TagStreamAsync(...)` for rolling windows.
- `SpeechEnhancement` — `EnhanceAsync` / `EnhanceSync` / Span overloads for in-place denoise.
- `SpeechSynthesis` — `GenerateAsync(text, voiceId, speed)`, `GenerateWithProgressCallbackAsync(...)`, `GenerateZeroShotAsync(text, promptText, promptClip, speed, steps)`.
- `SpokenLanguageIdentification` — `IdentifyAsync(float[] samples, int sampleRate)` or `IdentifyAsync(AudioClip clip)`.
- `VoiceActivityDetection` — tune thresholds, call `StreamDetect(float[] samples)`, listen to `OnSpeechSegmentDetected` / `OnSpeakingStateChanged`.
- `SherpaONNXUnityAPI` — set GitHub proxy, clear checksum cache, query model IDs by module type.

### 4. Mono Components
- **Audio Input:** `SherpaMicrophoneInput` (auto-start option, `ChunkReady` event, preferred device, mono downmix).
- **Streaming bases:** `SherpaModuleComponent<T>` (model lifecycle, feedback events), `SherpaAudioStreamingComponent<T>` (binds audio input, auto start/stop capture).
- **Ready-to-use components:** `SpeechRecognizerComponent`, `OfflineSpeechRecognizerComponent`, `VoiceActivityDetectionComponent`, `KeywordSpottingComponent`, `PunctuationComponent`, `AudioTaggingComponent`, `SpeechEnhancementComponent`, `SpeechSynthesizerComponent`, `ZeroShotSpeechSynthesisComponent`, `SpokenLanguageIdentificationComponent`.
- **Editor:** Localized inspectors and menu items under SherpaONNX; model selectors and microphone pickers are available in the Inspector.

### 5. Samples
- `Samples~/Collection/RealtimeSpeechRecognition/RealtimeSpeechRecognition.unity`
- `Samples~/Collection/OfflineSpeechRecognition/OfflineSpeechRecognition.unity`
- `Samples~/Collection/KeywordSpotting/KeywordSpotting.unity`
- `Samples~/Collection/Punctuation/Punctuation.unity`
- `Samples~/Collection/VoiceActivityDetection/VoiceActivityDetection.unity`
- `Samples~/Collection/SpeechEnhancement/SpeechEnhancement.unity`
- `Samples~/Collection/SpeechSynthesis/SpeechSynthesis.unity`
- `Samples~/Collection/SpokenLanguageIdentification/SpokenLanguageIdentification.unity`
- `Samples~/Collection/AudioTagging/AudioTagging.unity`
- `Samples~/Collection/ZeroShotSpeechSynthesis/ZeroShotSpeechSynthesis.unity`

### 6. Troubleshooting
- **Slow or failed downloads:** Configure a proxy via `SherpaONNXUnityAPI.SetGithubProxy(...)` or retry via the Model Manager.
- **Models not found:** Ensure the selected `Model Id` exists in the registry and that download/extract feedback reaches `Success`.
- **No microphone input:** Check OS microphone permission and set `preferredDevice` on `SherpaMicrophoneInput`.
- **High CPU usage:** Lower VAD/speech enhancement thresholds or dispose modules when not in use (`DisposeModule()`).
- **Disable auto-downloads:** Set `SHERPA_ONNX_AUTO_DOWNLOAD=false` or clear `AutoDownloadModels` in the runtime settings asset to require local models (per issue #4).
- **Force manifest refresh or proxy:** Use `SHERPA_ONNX_FETCH_LATEST_MANIFEST=true/false` and `SHERPA_ONNX_GITHUB_PROXY`; clear checksum cache via `SherpaONNXUnityAPI.ClearChecksumCache()`.

---

## 中文指南

### 1. 安装
- 需要 Unity 2021.3 LTS 及以上版本。
- OpenUPM 注册表（Package Manager → Project Settings）：
  - Name: `OpenUPM`
  - URL: `https://package.openupm.com`
  - Scope: `com.eitan.sherpa-onnx-unity`
  - 参考图：`images/package-manager-scopes.png`
- Git URL（实验分支）：
  ```
  https://github.com/EitanWong/com.eitan.sherpa-onnx-unity.git#upm
  ```

### 2. 快速上手
- **拖拽式组件：**
  1) 在场景中添加 `SherpaMicrophoneInput`。  
  2) 添加对应模块组件（如 `SpeechRecognizerComponent`、`AudioTaggingComponent`、`VoiceActivityDetectionComponent`、`ZeroShotSpeechSynthesisComponent`），填写 `Model Id`。  
  3) 订阅 UnityEvents（`TranscriptionReadyEvent`、`ClipReadyEvent`、`OnKeywordDetected` 等）。  
  4) 进入 Play；模型加载完成后自动开始采集。
- **脚本示例（ASR）：**
  ```csharp
  var asr = new SpeechRecognition("your-model-id", 16000);
  var text = await asr.SpeechTranscriptionAsync(samples, sampleRate);
  ```
- **脚本示例（零样本 TTS）：**
  ```csharp
  var tts = new SpeechSynthesis("zipvoice-demo", -1);
  var clip = await tts.GenerateZeroShotAsync(
      "你好，世界",
      "参考提示词",
      promptClip,
      speed: 1f,
      numSteps: 4);
  ```
- **模型管理器：** 菜单 Window → Sherpa ONNX → Model Manager。如下载缓慢，可调用 `SherpaONNXUnityAPI.SetGithubProxy("https://你的代理/")`。

#### 运行时配置 API
这些接口与 ScriptableObject/环境变量保持一致（如 SHERPA_ONNX_AUTO_DOWNLOAD）：
```csharp
SherpaONNXUnityAPI.SetAutoDownloadModels(false);
SherpaONNXUnityAPI.SetFetchLatestManifest(true);
SherpaONNXUnityAPI.SetChecksumCacheDirectory(
    Path.Combine(Application.persistentDataPath, "SherpaCache"));
SherpaONNXUnityAPI.SetChecksumCacheTtlSeconds(0);
```

### 3. 模块与API
- `SpeechRecognition` — `SpeechTranscriptionAsync` 支持在线/离线模型自动切换。
- `Punctuation` — `AddPunctuationAsync` 为识别文本补充标点与大小写。
- `KeywordSpotting` — 构造时配置关键词，调用 `DetectAsync`，监听 `OnKeywordDetected`。
- `AudioTagging` — `TagAsync`（整段）或 `TagStreamAsync`（滑窗）获取 TopK 标签。
- `SpeechEnhancement` — `EnhanceAsync` / `EnhanceSync` / Span 重载，原地降噪。
- `SpeechSynthesis` — `GenerateAsync`、`GenerateWithProgressCallbackAsync`、`GenerateZeroShotAsync`（文本+提示词音频，支持调整速度与步数）。
- `SpokenLanguageIdentification` — `IdentifyAsync(float[]/AudioClip)` 识别语种。
- `VoiceActivityDetection` — 调整阈值，调用 `StreamDetect`，监听 `OnSpeechSegmentDetected` 与 `OnSpeakingStateChanged`。
- `SherpaONNXUnityAPI` — 设置 GitHub 代理、清理校验缓存、按模块类型查询模型 ID。

### 4. 拖拽式组件
- **音频输入：** `SherpaMicrophoneInput`（自动启动、`ChunkReady` 事件、首选设备、单声道混音）。
- **基础类：** `SherpaModuleComponent<T>` 管理模块生命周期与反馈；`SherpaAudioStreamingComponent<T>` 负责音频绑定与自动捕获。
- **可直接使用的组件：** `SpeechRecognizerComponent`、`OfflineSpeechRecognizerComponent`、`VoiceActivityDetectionComponent`、`KeywordSpottingComponent`、`PunctuationComponent`、`AudioTaggingComponent`、`SpeechEnhancementComponent`、`SpeechSynthesizerComponent`、`ZeroShotSpeechSynthesisComponent`、`SpokenLanguageIdentificationComponent`。
- **编辑器：** Inspector 支持中英文，提供模型选择、麦克风选择及快捷菜单。

### 5. 示例场景
- `Samples~/Collection/RealtimeSpeechRecognition/RealtimeSpeechRecognition.unity`
- `Samples~/Collection/OfflineSpeechRecognition/OfflineSpeechRecognition.unity`
- `Samples~/Collection/KeywordSpotting/KeywordSpotting.unity`
- `Samples~/Collection/Punctuation/Punctuation.unity`
- `Samples~/Collection/VoiceActivityDetection/VoiceActivityDetection.unity`
- `Samples~/Collection/SpeechEnhancement/SpeechEnhancement.unity`
- `Samples~/Collection/SpeechSynthesis/SpeechSynthesis.unity`
- `Samples~/Collection/SpokenLanguageIdentification/SpokenLanguageIdentification.unity`
- `Samples~/Collection/AudioTagging/AudioTagging.unity`
- `Samples~/Collection/ZeroShotSpeechSynthesis/ZeroShotSpeechSynthesis.unity`

### 6. 常见问题
- **下载缓慢/失败**：使用代理或通过 Model Manager 重新下载。
- **模型缺失**：确认 `Model Id` 存在并下载成功（查看反馈信息）。
- **麦克风无声**：检查系统权限并在 `SherpaMicrophoneInput` 设置 `preferredDevice`。
- **性能过高**：降低 VAD/降噪阈值，或在不用时调用 `DisposeModule()` 释放模块。
- **关闭自动下载**：设置环境变量 `SHERPA_ONNX_AUTO_DOWNLOAD=false`，或在运行时设置资产中取消勾选 `AutoDownloadModels`，仅使用本地模型（参考 issue #4）。
- **清单刷新/代理**：`SHERPA_ONNX_FETCH_LATEST_MANIFEST=true/false` 控制清单拉取，`SHERPA_ONNX_GITHUB_PROXY` 配置代理，可用 `SherpaONNXUnityAPI.ClearChecksumCache()` 清理校验缓存。
