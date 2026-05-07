<div align="center">

# SherpaONNXUnity

Unity package for offline speech and audio AI, powered by [sherpa-onnx](https://github.com/k2-fsa/sherpa-onnx).

[中文](./README_zh.md)

[![OpenUPM](https://img.shields.io/npm/v/com.eitan.sherpa-onnx-unity?label=openupm&registry_uri=https://package.openupm.com&style=flat-square&color=blue)](https://openupm.com/packages/com.eitan.sherpa-onnx-unity/)
[![Downloads](https://img.shields.io/badge/dynamic/json?color=brightgreen&label=downloads&suffix=%2Fmonth&url=https%3A%2F%2Fpackage.openupm.com%2Fdownloads%2Fpoint%2Flast-month%2Fcom.eitan.sherpa-onnx-unity&style=flat-square)](https://openupm.com/packages/com.eitan.sherpa-onnx-unity/)
[![Unity](https://img.shields.io/badge/Unity-2021.3%2B-black?style=flat-square&logo=unity)](https://unity.com/)
[![License](https://img.shields.io/badge/License-Apache_2.0-blue?style=flat-square)](./SherpaONNXUnity/Packages/com.eitan.sherpa-onnx-unity/LICENSE.md)

**Latest package:** `v0.1.3-exp.4`
**sherpa-onnx native runtime:** `v1.13.0`

[Documentation](./SherpaONNXUnity/Packages/com.eitan.sherpa-onnx-unity/Documentation~/README.md) |
[Changelog](./SherpaONNXUnity/Packages/com.eitan.sherpa-onnx-unity/CHANGELOG.md) |
[OpenUPM](https://openupm.com/packages/com.eitan.sherpa-onnx-unity/)

</div>

---

## Overview

SherpaONNXUnity brings the main sherpa-onnx speech and audio capabilities into Unity through native plugins, model management tools, editor windows, sample scenes, and ready-to-use Mono components.

It is built for offline-first Unity applications that need speech recognition, speech synthesis, speaker analysis, audio understanding, or real-time microphone workflows without depending on a cloud service at runtime.

## What's New in v0.1.3-exp.4

- Updated bundled sherpa-onnx native libraries to `v1.13.0`.
- Added iOS support with device and simulator xcframework slices.
- Improved the SherpaONNXUnity component workflow for Unity scenes and Inspectors.
- Added Source Separation support.
- Added Speaker Diarization support.
- Expanded the Unity integration toward full sherpa-onnx feature coverage.

## Features

|  |  |  |
|---|---|---|
| 🎙️ [Speech recognition](./SherpaONNXUnity/Packages/com.eitan.sherpa-onnx-unity/Documentation~/README.en.md#speech-recognition) | 🔊 [Speech synthesis](./SherpaONNXUnity/Packages/com.eitan.sherpa-onnx-unity/Documentation~/README.en.md#speech-synthesis) | 🎚️ [Source separation](./SherpaONNXUnity/Packages/com.eitan.sherpa-onnx-unity/Documentation~/README.en.md#source-separation) |
| 👤 [Speaker identification](./SherpaONNXUnity/Packages/com.eitan.sherpa-onnx-unity/Documentation~/README.en.md#speaker-identification) | 👥 [Speaker diarization](./SherpaONNXUnity/Packages/com.eitan.sherpa-onnx-unity/Documentation~/README.en.md#speaker-diarization) | ✅ [Speaker verification](./SherpaONNXUnity/Packages/com.eitan.sherpa-onnx-unity/Documentation~/README.en.md#speaker-verification) |
| 🌐 [Spoken language identification](./SherpaONNXUnity/Packages/com.eitan.sherpa-onnx-unity/Documentation~/README.en.md#spoken-language-identification) | 🏷️ [Audio tagging](./SherpaONNXUnity/Packages/com.eitan.sherpa-onnx-unity/Documentation~/README.en.md#audio-tagging) | 📈 [Voice activity detection](./SherpaONNXUnity/Packages/com.eitan.sherpa-onnx-unity/Documentation~/README.en.md#voice-activity-detection) |
| 🔑 [Keyword spotting](./SherpaONNXUnity/Packages/com.eitan.sherpa-onnx-unity/Documentation~/README.en.md#keyword-spotting) | ✍️ [Punctuation](./SherpaONNXUnity/Packages/com.eitan.sherpa-onnx-unity/Documentation~/README.en.md#punctuation) | ✨ [Speech enhancement](./SherpaONNXUnity/Packages/com.eitan.sherpa-onnx-unity/Documentation~/README.en.md#speech-enhancement) |

## Platform Support

| Platform | Architectures |
|---|---|
| Windows | x64 |
| macOS | Apple Silicon / Intel |
| Linux | x64 |
| Android | `arm64-v8a`, `armeabi-v7a`, `x86`, `x86_64` |
| iOS | `ios-arm64`, `ios-arm64_x86_64-simulator` |

For Android production builds, `arm64-v8a` is recommended. Some upstream native initialization paths may still be unstable on `armeabi-v7a` for specific models.

## Demos

| Language | Demo Video | Language | Demo Video |
|---|---|---|---|
| English & Chinese | <video src="https://github.com/user-attachments/assets/d1df8412-042f-4c66-947a-98fb4784ba2e" width="400" controls>Your browser does not support the video tag.</video> | French | <video src="https://github.com/user-attachments/assets/0760f2b9-0c0e-4df0-9ed1-86a78c94fd33" width="400" controls>Your browser does not support the video tag.</video> |
| Japanese | <video src="https://github.com/user-attachments/assets/ec52b860-e945-4574-b5b3-11cbed741113" width="400" controls>Your browser does not support the video tag.</video> | Korean | <video src="https://github.com/user-attachments/assets/6707a16a-c12d-464a-a57e-24b044d87e76" width="400" controls>Your browser does not support the video tag.</video> |
| Russian | <video src="https://github.com/user-attachments/assets/9ce70da8-44a9-4d6b-8864-d9e24d535dfa" width="400" controls>Your browser does not support the video tag.</video> | Sichuan Dialect | <video src="https://github.com/user-attachments/assets/bdfed3a3-efe7-4899-bd8a-8d63bd8a30c8" width="400" controls>Your browser does not support the video tag.</video> |

More introduction: [Bilibili demo](https://www.bilibili.com/video/BV1E38hz3ETw/?share_source=copy_web&vd_source=06d081c8a7b3c877a41f801ce5915855)

## Prerequisites

**Unity:** 2021.3 LTS+ | **Storage:** ~500MB | **Platforms:** Win/Mac/Linux/Android/iOS

## Installation

### Git URL

In Unity, open **Window > Package Manager**, click **+**, choose **Add package from git URL...**, then add:

```text
https://github.com/EitanWong/com.eitan.sherpa-onnx-unity.git#upm
```

### OpenUPM

Add the scoped registry:

```text
Name: OpenUPM
URL: https://package.openupm.com
Scope: com.eitan.sherpa-onnx-unity
```

Then install `com.eitan.sherpa-onnx-unity` from **My Registries**.

## Quick Start

The fastest path is to import the sample package and open a scene under `Samples~/Collection`.

1. Install SherpaONNXUnity.
2. Import **SherpaONNXUnity Sample** from Package Manager.
3. Open a sample scene.
4. Select or download a model through **Window > SherpaONNX > Model Manager**.
5. Press Play.

## Model Manager

SherpaONNXUnity includes an editor Model Manager for browsing supported models, downloading model assets, and preparing local model files for samples or your own scenes.

Open it from:

```text
Window/SherpaONNX/Model Manager
```

![Model Manager](https://github.com/user-attachments/assets/cebbcd15-9361-4e6e-b3ab-d39041fbb61a)

For component usage, runtime configuration, model downloads, custom manifests, and editor tools, see the [package documentation](./SherpaONNXUnity/Packages/com.eitan.sherpa-onnx-unity/Documentation~/README.en.md).

## Repository Layout

```text
SherpaONNXUnity/
├── Packages/com.eitan.sherpa-onnx-unity/
│   ├── Runtime/
│   ├── Editor/
│   ├── Tests/
│   ├── Samples~/
│   └── Documentation~/
├── Assets/Demo/
└── ProjectSettings/
```

## License

SherpaONNXUnity is licensed under the Apache 2.0 License.

This project includes third-party software. See:

- [LICENSE.md](./SherpaONNXUnity/Packages/com.eitan.sherpa-onnx-unity/LICENSE.md)
- [THIRD PARTY NOTICES.md](./SherpaONNXUnity/Packages/com.eitan.sherpa-onnx-unity/THIRD%20PARTY%20NOTICES.md)

## Acknowledgments

SherpaONNXUnity is built on the official [sherpa-onnx](https://github.com/k2-fsa/sherpa-onnx) project. Thanks to the k2-fsa team and contributors for making high-quality offline speech and audio AI available to the community.

## Links

| Resource | Link |
|---|---|
| Package registry | [OpenUPM](https://openupm.com/packages/com.eitan.sherpa-onnx-unity/) |
| Official sherpa-onnx repository | [k2-fsa/sherpa-onnx](https://github.com/k2-fsa/sherpa-onnx) |
| Package documentation | [Documentation~/README.md](./SherpaONNXUnity/Packages/com.eitan.sherpa-onnx-unity/Documentation~/README.md) |
| Changelog | [CHANGELOG.md](./SherpaONNXUnity/Packages/com.eitan.sherpa-onnx-unity/CHANGELOG.md) |
