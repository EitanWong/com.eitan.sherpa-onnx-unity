<div align="center">

# 🎙️ SherpaONNXUnity

### Unity Package for Offline Speech AI: ASR, TTS, VAD, Diarization & More

> **中文用户请注意**: 本项目提供中文文档，请查看 [README_zh.md](./README_zh.md) 获取详细的中文说明。

**Language**: [English](./README.md) | [中文](./README_zh.md)

[![OpenUPM](https://img.shields.io/npm/v/com.eitan.sherpa-onnx-unity?label=openupm&registry_uri=https://package.openupm.com&style=flat-square&color=blue)](https://openupm.com/packages/com.eitan.sherpa-onnx-unity/)
[![Downloads](https://img.shields.io/badge/dynamic/json?color=brightgreen&label=downloads&suffix=%2Fmonth&url=https%3A%2F%2Fpackage.openupm.com%2Fdownloads%2Fpoint%2Flast-month%2Fcom.eitan.sherpa-onnx-unity&style=flat-square)](https://openupm.com/packages/com.eitan.sherpa-onnx-unity/)
[![Unity](https://img.shields.io/badge/Unity-2021.3%2B-black?style=flat-square&logo=unity)](https://unity.com/)
[![License](https://img.shields.io/badge/License-Apache_2.0-blue.svg?style=flat-square)](LICENSE.md)

📋 **[View Changelog](./SherpaONNXUnity/Packages/com.eitan.sherpa-onnx-unity/CHANGELOG.md)** | 📊 **Latest: v0.1.3-exp.2** (2026-03-20)

</div>

## 🎬 Demos

Here are some video demonstrations of the features in action.

<div align="center">

| Language          | Demo Video                                                                                                | Language        | Demo Video                                                                                             |
|-------------------|-----------------------------------------------------------------------------------------------------------|-----------------|--------------------------------------------------------------------------------------------------------|
| English & Chinese | <video src="https://github.com/user-attachments/assets/d1df8412-042f-4c66-947a-98fb4784ba2e" width="400" controls>Your browser does not support the video tag.</video> | French          | <video src="https://github.com/user-attachments/assets/0760f2b9-0c0e-4df0-9ed1-86a78c94fd33" width="400" controls>Your browser does not support the video tag.</video>            |
| Japanese          | <video src="https://github.com/user-attachments/assets/ec52b860-e945-4574-b5b3-11cbed741113" width="400" controls>Your browser does not support the video tag.</video>          | Korean          | <video src="https://github.com/user-attachments/assets/6707a16a-c12d-464a-a57e-24b044d87e76" width="400" controls>Your browser does not support the video tag.</video>            |
| Russian           | <video src="https://github.com/user-attachments/assets/9ce70da8-44a9-4d6b-8864-d9e24d535dfa" width="400" controls>Your browser does not support the video tag.</video>            | Sichuan Dialect | <video src="https://github.com/user-attachments/assets/bdfed3a3-efe7-4899-bd8a-8d63bd8a30c8" width="400" controls>Your browser does not support the video tag.</video>  |

</div>

For a more detailed introduction, you can also watch the video on [Bilibili](https://www.bilibili.com/video/BV1E38hz3ETw/?share_source=copy_web&vd_source=06d081c8a7b3c877a41f801ce5915855).

---

## 🆕 What's New in v0.1.3-exp.2 (2026-03-20)

### 🚀 Highlights
- **Upstream Native Runtime Updated to sherpa-onnx v1.12.30**
  Refreshed the bundled native libraries and synchronized the C# native interop layer with the latest upstream APIs used by this package.

- **Speaker Diarization Support Added**
  Added offline speaker diarization support, including a dedicated runtime module, Unity component integration, and a sample demo workflow.

- **Native/API Alignment Improvements**
  Updated the Unity-side native bindings to match the current sherpa-onnx runtime layout and keep feature coverage aligned with upstream.

[📋 **View Full Changelog**](./SherpaONNXUnity/Packages/com.eitan.sherpa-onnx-unity/CHANGELOG.md)

---

## 🚀 Overview

A Unity package that brings **offline automatic speech recognition (ASR)**, **text-to-speech (TTS)**, **voice activity detection (VAD)**, **speaker diarization**, and other speech/audio AI capabilities to the Unity game engine, powered by [sherpa-onnx](https://github.com/k2-fsa/sherpa-onnx).

✨ **Features intelligent automatic model downloading with breakpoint resume support** for seamless setup and 📱 **optimized mobile platform integration** for production-ready deployment.

## 🌟 Key Features

### 🎯 Core Capabilities
- **🔌 Offline Operation** - No internet required after setup
- **⚡ Real-time Processing** - Low-latency speech recognition
- **🗣️ Voice Activity Detection** - Smart speech boundary detection
- **👥 Speaker Diarization** - Separate who spoke when in multi-speaker audio
- **🔊 Speech Enhancement** - GTCRN noise reduction and audio quality improvement
- **👂 Keyword Spotting** - Voice-activated keyword detection, now with custom keyword support.
- **🎤 Text-to-Speech** - High-quality voice synthesis
- **🌍 Spoken Language Identification** - Identify the language of a given audio clip.
- **🎼 Audio Tagging** – Automatic detection and classification of various audio events, such as music, traffic, and environmental sounds
- **🖥️ Cross-platform Support** - Windows, macOS, Linux, Android

### 🤖 Intelligent Model Management
- **🔄 Automatic Downloads** - Models download seamlessly
- **📡 Breakpoint Resume** - Network interruptions handled
- **🔐 Hash Verification** - Integrity verification built-in
- **💾 Smart Caching** - Local storage optimization

### 🛠️ Developer Experience
- **🎮 Unity Native** - Seamless workflow integration
- **📚 Rich Documentation** - Comprehensive examples
- **🔄 Regular Updates** - Latest sherpa-onnx improvements

## 🏗️ Architecture

> Unity-native wrapper around the powerful [sherpa-onnx](https://github.com/k2-fsa/sherpa-onnx) speech processing library.

**Core Components:**
- 📚 **Native Integration** - Cross-platform binaries with mobile optimization
- 🤖 **Smart Model Management** - Background downloads with breakpoint resume
- 🎮 **Unity Components** - MonoBehaviour-based scene integration

## 🚀 Quick Start Guide

### 📋 Prerequisites

**Unity:** 2021.3 LTS+ | **Storage:** ~500MB | **Platforms:** Win/Mac/Linux/Android

### 📦 Installation

> **Note:** This package is currently in an experimental phase and has not yet been published to OpenUPM. Please use the Git URL method for installation.

<details open>
<summary><strong>🎯 Add package from Git URL (Recommended)</strong></summary>

1. In Unity, open **Window → Package Manager**.
2. Click the **+** button in the top-left corner.
3. Select **"Add package from git URL..."**
4. Enter the following URL and click **Add**:
   ```
   https://github.com/EitanWong/com.eitan.sherpa-onnx-unity.git#upm
   ```

</details>

<details>
<summary><strong>🔧 Unity Package Manager (via Scoped Registry - Coming Soon)</strong></summary>

> This method will be available after the official release on OpenUPM.

1. **Edit → Project Settings → Package Manager**
2. Add Scoped Registry:
   - Name: `OpenUPM`
   - URL: `https://package.openupm.com`
   - Scope: `com.eitan.sherpa-onnx-unity`
3. **Window → Package Manager → My Registries**
4. Install **SherpaONNXUnity**

</details>

<details>
<summary><strong>🔗 OpenUPM (CLI - Coming Soon)</strong></summary>

> This method will be available after the official release on OpenUPM.

```bash
openupm add com.eitan.sherpa-onnx-unity
```

</details>

### 💻 Getting Started

**🎯 The fastest way to get started is to import and explore the sample scenes:**

1. Open **Window → Package Manager**
2. Find **SherpaONNXUnity** in **In Project** tab
3. Expand **Samples** section
4. Click **Import** next to "SherpaONNXUnity Sample"

The samples include:
- **Real-time Speech Recognition** - Live microphone input with real-time transcription
- **Voice Activity Detection** - Detect when users start and stop speaking
- **Speaker Diarization** - Analyze multi-speaker recordings and cluster speaker turns
- **Offline Speech Recognition** - Process pre-recorded audio files
- **Speech Enhancement** - Real-time noise reduction with GTCRN models
- **Keyword Spotting** - Voice-activated keyword detection and wake words
- **Spoken Language Identification** - Identify the language from an audio clip.
- **Text-to-Speech Synthesis** - High-quality voice generation
- **Audio Tagging** – Automatic detection and classification of various audio events
- **Zero-Shot Speech Synthesis** – Prompt-driven voice cloning with example prompt assets

Each example includes complete sample code that you can use as a starting point for your own implementation.

**New drop-in component flow (no boilerplate scripting):**
- Add `SherpaMicrophoneInput` to your scene to emit PCM chunks.
- Add a module component (e.g., `SpeechRecognizerComponent`, `AudioTaggingComponent`, `VoiceActivityDetectionComponent`, `SpeakerDiarizationComponent`, `ZeroShotSpeechSynthesisComponent`) and set the `Model Id`.
- Hook UnityEvents (e.g., `TranscriptionReadyEvent`, `ClipReadyEvent`) for results; the component will start capture when the model finishes loading.
- See the bilingual guide at `SherpaONNXUnity/Packages/com.eitan.sherpa-onnx-unity/Documentation~/README.md` for full API/component usage.

### Model Manager

Open the Model Manager window by navigating to **Window → Sherpa ONNX → Model Manager**.

![Model Manager](https://github.com/user-attachments/assets/ce622a7d-0885-406d-9a97-78ea89474731)

With the Model Manager, you can search for all the models supported by sherpa-onnx and download them to your local system with a single click.

#### Runtime settings via code
`SherpaONNXUnityAPI` mirrors all environment/asset knobs so you can react at runtime:

```csharp
SherpaONNXUnityAPI.SetAutoDownloadModels(false);          // mirrors SHERPA_ONNX_AUTO_DOWNLOAD
SherpaONNXUnityAPI.SetFetchLatestManifest(true);          // mirrors SHERPA_ONNX_FETCH_LATEST_MANIFEST
SherpaONNXUnityAPI.SetChecksumCacheDirectory(
    Path.Combine(Application.persistentDataPath, "SherpaCache"));
SherpaONNXUnityAPI.SetChecksumCacheTtlSeconds(0);         // disable caching when distributing offline bundles
```

Use these helpers together with `SetGithubProxy`/`ClearGithubProxy` to control download behavior without touching ScriptableObjects.

## 🛠️ Development

### Building from Source

1. Clone the repository:
   ```bash
   git clone https://github.com/EitanWong/com.eitan.sherpa-onnx-unity.git
   cd com.eitan.sherpa-onnx-unity
   ```

2. Open in Unity 2021.3 LTS or higher

3. Install dependencies via Package Manager

4. Import sample scenes and test functionality

5. Build for your target platform

### Testing

- **Edit Mode Tests**: Unit tests for core functionality
- **Play Mode Tests**: Integration tests with Unity components
- **Platform Tests**: Cross-platform compatibility validation

Run tests via **Window → General → Test Runner**

### Project Structure

```
SherpaONNXUnity/
├── Packages/com.eitan.sherpa-onnx-unity/
│   ├── Runtime/           # Core package code
│   ├── Editor/            # Unity editor extensions
│   ├── Tests/             # Unit and integration tests
│   └── Samples~/          # Example scenes and scripts
├── Assets/Demo/           # Demo project
└── Documentation/         # Additional documentation
```

## 🤝 Contributing

We welcome contributions from the community! Please see our [Contributing Guidelines](CONTRIBUTING.md) for details on:

- 🐛 Reporting bugs
- 💡 Suggesting features
- 🔧 Submitting pull requests
- 📖 Improving documentation

## 📄 License & Legal

### Apache 2.0 License

This project is licensed under the **Apache 2.0 License** - see the [LICENSE.md](LICENSE.md) file for details.

### Attribution

This package is built upon [sherpa-onnx](https://github.com/k2-fsa/sherpa-onnx), an outstanding speech processing library also under the Apache 2.0 License. We extend our gratitude to the k2-fsa team for their excellent work.

### Important License Information

- ✅ **Commercial Use**: Permitted
- ✅ **Modification**: Permitted
- ✅ **Distribution**: Permitted
- ✅ **Private Use**: Permitted
- ❗ **License Notice**: Must be included in redistributions
- ❗ **Copyright Notice**: Must be preserved

**Compliance Note**: When using this package in your projects, ensure you include the license notices for both SherpaONNXUnity and sherpa-onnx in your application's legal documentation.

## 🙏 Acknowledgments

- **[sherpa-onnx](https://github.com/k2-fsa/sherpa-onnx)**: The powerful speech processing engine that makes this Unity integration possible
- **k2-fsa Team**: For creating and maintaining the world-class sherpa-onnx library
- **Unity Community**: For continuous feedback, testing, and contributions
- **Contributors**: All developers who have helped improve this project

## 📞 Support & Community

### Getting Help

-  **Issues**: [GitHub Issues](https://github.com/EitanWong/com.eitan.sherpa-onnx-unity/issues) for bug reports
- 📖 **Wiki**: [Project Wiki](https://github.com/EitanWong/com.eitan.sherpa-onnx-unity/wiki) for detailed guides
- 💡 **Discussions**: [GitHub Discussions](https://github.com/EitanWong/com.eitan.sherpa-onnx-unity/discussions) for questions and ideas

### Stay Updated

- ⭐ **Star** this repository to stay updated
- 👀 **Watch** releases for new versions
- 🐦 **Follow** [@EitanWong](https://github.com/EitanWong) for updates

## 🔗 Links & Resources

| Resource | Link |
|----------|------|
| 📦 **Package Registry** | [OpenUPM](https://openupm.com/packages/com.eitan.sherpa-onnx-unity/) |
| 🏪 **Unity Asset Store** | Coming Soon |
| 📂 **Source Code** | [GitHub](https://github.com/EitanWong/com.eitan.sherpa-onnx-unity) |
| 🤖 **sherpa-onnx** | [Original Project](https://github.com/k2-fsa/sherpa-onnx) |
| 📚 **Documentation** | [Wiki](https://github.com/EitanWong/com.eitan.sherpa-onnx-unity/wiki) |
| 🎯 **Roadmap** | [Project Board](https://github.com/EitanWong/com.eitan.sherpa-onnx-unity/projects) |

---

<div align="center">

**Made with ❤️ by [Eitan](https://github.com/EitanWong)**

*Powered by [sherpa-onnx](https://github.com/k2-fsa/sherpa-onnx) | Inspired by the Unity Community*

</div>
