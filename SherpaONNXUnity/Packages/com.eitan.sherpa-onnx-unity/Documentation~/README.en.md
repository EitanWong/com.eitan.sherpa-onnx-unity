# SherpaONNXUnity Documentation

[中文](./README.zh.md) | [Package README](./README.md)

## Installation

- Unity 2021.3 LTS or newer.
- Package name: `com.eitan.sherpa-onnx-unity`
- Git URL:

```text
https://github.com/EitanWong/com.eitan.sherpa-onnx-unity.git#upm
```

OpenUPM scoped registry:

```text
Name: OpenUPM
URL: https://package.openupm.com
Scope: com.eitan.sherpa-onnx-unity
```

## Quick Start

1. Install the package.
2. Import **SherpaONNXUnity Sample** from Package Manager.
3. Open a scene under `Samples~/Collection`.
4. Select or download a model in `Window/SherpaONNX/Model Manager`.
5. Press Play.

For scene-based workflows, add `SherpaMicrophoneInput` and a module component, set `Model Id`, bind audio input when needed, and subscribe to UnityEvents.

## Editor Tools

| Tool | Menu |
|---|---|
| Model Manager | `Window/SherpaONNX/Model Manager` |
| Profiler | `Window/SherpaONNX/SherpaONNX Profiler` |
| Welcome | `Help/SherpaONNX/Welcome` |
| Component shortcuts | `GameObject/SherpaONNX/...` |

## Feature Guides

### <a id="speech-recognition"></a>Speech Recognition

Transcribes speech to text. Use realtime recognition for microphone streams and offline recognition for recorded audio.

- Components: `RealtimeSpeechRecognizerComponent`, `OfflineSpeechRecognizerComponent`
- API: `SpeechRecognition.TranscribeAsync(...)`, `SpeechRecognition.SpeechTranscriptionAsync(...)`
- Samples: `RealtimeSpeechRecognition`, `OfflineSpeechRecognition`

### <a id="speech-synthesis"></a>Speech Synthesis

Generates speech from text, including standard TTS and prompt-driven zero-shot synthesis.

- Components: `SpeechSynthesizerComponent`, `ZeroShotSpeechSynthesisComponent`
- API: `SpeechSynthesis.GenerateAsync(...)`, `SpeechSynthesis.GenerateZeroShotAsync(...)`
- Samples: `SpeechSynthesis`, `ZeroShotSpeechSynthesis`

### <a id="spoken-language-identification"></a>Spoken Language Identification

Detects the spoken language from an audio clip or sample buffer.

- Component: `SpokenLanguageIdentificationComponent`
- API: `SpokenLanguageIdentification.IdentifyAsync(...)`
- Sample: `SpokenLanguageIdentification`

### <a id="keyword-spotting"></a>Keyword Spotting

Detects wake words or configured keywords from streaming or recorded audio.

- Component: `KeywordSpottingComponent`
- API: `KeywordSpotting.StreamDetect(...)`, `KeywordSpotting.DetectAsync(...)`
- Sample: `KeywordSpotting`

### <a id="punctuation"></a>Punctuation

Restores punctuation and casing for recognized text.

- Component: `PunctuationComponent`
- API: `Punctuation.AddPunctuationAsync(...)`
- Sample: `Punctuation`

### <a id="speaker-identification"></a>Speaker Identification

Identifies or labels speakers through speaker embeddings. Use this with speaker-analysis workflows when you need to associate speech with known speakers.

- APIs: speaker embedding and speaker analysis APIs
- Related component: `SpeakerDiarizationComponent`
- Related sample: `SpeakerDiarization`

### <a id="speaker-diarization"></a>Speaker Diarization

Segments an audio clip by speaker turns and returns speaker-labeled time ranges.

- Component: `SpeakerDiarizationComponent`
- API: `SpeakerDiarization.DiarizeAsync(...)`
- Sample: `SpeakerDiarization`

### <a id="speaker-verification"></a>Speaker Verification

Compares speaker embeddings to verify whether two speech segments are likely from the same speaker.

- APIs: speaker embedding and verification APIs
- Related component: `SpeakerDiarizationComponent`
- Related sample: `SpeakerDiarization`

### <a id="source-separation"></a>Source Separation

Separates mixed audio into stems, such as vocals and accompaniment depending on the selected model.

- Component: `SourceSeparationComponent`
- API: `SourceSeparation.SeparateAsync(...)`
- Sample: `SourceSeparation`

### <a id="audio-tagging"></a>Audio Tagging

Classifies audio events such as music, environmental sounds, or other acoustic classes.

- Component: `AudioTaggingComponent`
- API: `AudioTagging.TagAsync(...)`, `AudioTagging.TagStreamAsync(...)`
- Sample: `AudioTagging`

### <a id="voice-activity-detection"></a>Voice Activity Detection

Detects speech boundaries and speaking state from audio streams.

- Component: `VoiceActivityDetectionComponent`
- API: `VoiceActivityDetection.StreamDetect(...)`, `VoiceActivityDetection.FlushAsync()`
- Sample: `VoiceActivityDetection`

### <a id="speech-enhancement"></a>Speech Enhancement

Denoises and enhances speech audio.

- Component: `SpeechEnhancementComponent`
- API: `SpeechEnhancement.EnhanceAsync(...)`, `SpeechEnhancement.ProcessStreamingAsync(...)`
- Sample: `SpeechEnhancement`

## Runtime Configuration

```csharp
SherpaONNXUnityAPI.SetAutoDownloadModels(false);
SherpaONNXUnityAPI.SetFetchLatestManifest(true);
SherpaONNXUnityAPI.SetDownloadAttemptTimeoutSeconds(600);
SherpaONNXUnityAPI.SetAllowInsecureModelDownload(false);
SherpaONNXUnityAPI.SetForceModelHashValidation(false);
SherpaONNXUnityAPI.SetGithubProxy("https://your-proxy/");
SherpaONNXUnityAPI.ClearChecksumCache();
```

Environment overrides:

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

Call `SherpaONNXUnityAPI.ApplyEnvironmentOverridesFromProcess()` after changing process environment variables at runtime.

## Custom Models

Runtime registration:

```csharp
SherpaONNXUnityAPI.RegisterCustomModel(metadata);
SherpaONNXUnityAPI.RegisterCustomModels(models);
```

Minimum manifest entry:

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

## Platform Notes

- `0.1.3-exp.4` updates sherpa-onnx native libraries to v1.13.0.
- iOS uses xcframework bundles for device and simulator builds.
- Android `arm64-v8a` is recommended for production.
- Android `armeabi-v7a` remains available but may be unstable for some upstream native model paths.
