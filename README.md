<div align="center">

# 🎙️ SherpaOnnxUnity

### Unity Package for Offline Speech Recognition & Voice Activity Detection

> **中文用户请注意**: 本项目提供中文文档，请查看 [README_zh.md](./README_zh.md) 获取详细的中文说明。
> 
> **For Chinese users**: This project provides Chinese documentation. Please see [README_zh.md](./README_zh.md) for detailed Chinese instructions.

**Language**: [English](./README.md) | [中文](./README_zh.md)

[![OpenUPM](https://img.shields.io/npm/v/com.eitan.sherpa-onnx-unity?label=openupm&registry_uri=https://package.openupm.com&style=flat-square&color=blue)](https://openupm.com/packages/com.eitan.sherpa-onnx-unity/) 
[![Downloads](https://img.shields.io/badge/dynamic/json?color=brightgreen&label=downloads&suffix=%2Fmonth&url=https%3A%2F%2Fpackage.openupm.com%2Fdownloads%2Fpoint%2Flast-month%2Fcom.eitan.sherpa-onnx-unity&style=flat-square)](https://openupm.com/packages/com.eitan.sherpa-onnx-unity/)
[![Unity](https://img.shields.io/badge/Unity-2021.3%2B-black?style=flat-square&logo=unity)](https://unity.com/)
[![License](https://img.shields.io/badge/License-MIT-yellow.svg?style=flat-square)](LICENSE.md)

📋 **[View Changelog](./Packages/com.eitan.sherpa-onnx-unity/CHANGELOG.md)** | 📊 **Latest: v0.1.1-exp.1** (2025-01-08)

</div>

## 🎬 Demo Video

<div align="center">

[![Unity Local Offline Speech Recognition Demo](https://img.shields.io/badge/🎥_Watch_Demo-Unity_Speech_Recognition-blue?style=for-the-badge)](https://www.bilibili.com/video/BV1E38hz3ETw/?share_source=copy_web&vd_source=06d081c8a7b3c877a41f801ce5915855)

*Click above to see SherpaOnnxUnity in action with real-time speech recognition*

</div>

---

## 🆕 What's New in v0.1.1-exp.1 (2025-08-06)

### 🎯 New Modules
- **🔊 Speech Enhancement** - GTCRN-powered noise reduction with real-time processing
- **👂 Keyword Spotting** - Voice-activated keyword detection for voice commands
- **🎛️ Interactive Demos** - Complete examples with model management and UI controls

### ⚡ Key Improvements
- **Enhanced Model Registry** - Automatic downloads with hash verification
- **Better Thread Safety** - Improved concurrent processing architecture

[📋 **View Full Changelog**](./SherpaOnnxUnity/Packages/com.eitan.sherpa-onnx-unity/CHANGELOG.md)

---

## 🚀 Overview

A Unity package that brings **offline automatic speech recognition (ASR)**, **text-to-speech (TTS)**, and **voice activity detection (VAD)** capabilities to the Unity game engine, powered by [sherpa-onnx](https://github.com/k2-fsa/sherpa-onnx).

✨ **Features intelligent automatic model downloading with breakpoint resume support** for seamless setup and 📱 **optimized mobile platform integration** for production-ready deployment.

## 🌟 Key Features

### 🎯 Core Capabilities
- **🔌 Offline Operation** - No internet required after setup
- **⚡ Real-time Processing** - Low-latency speech recognition
- **🎯 Voice Activity Detection** - Smart speech boundary detection
- **🔊 Speech Enhancement** - GTCRN noise reduction and audio quality improvement
- **👂 Keyword Spotting** - Voice-activated keyword detection and wake words
- **🎤 Text-to-Speech** - High-quality voice synthesis
- **🌍 Cross-platform Support** - Windows, macOS, Linux, Android

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

<details>
<summary><strong>🎯 OpenUPM (Recommended)</strong></summary>

```bash
openupm add com.eitan.sherpa-onnx-unity
```

</details>

<details>
<summary><strong>🔧 Unity Package Manager</strong></summary>

1. **Edit → Project Settings → Package Manager**
2. Add Scoped Registry:
   - Name: `OpenUPM`
   - URL: `https://package.openupm.com`
   - Scope: `com.eitan.sherpa-onnx-unity`
3. **Window → Package Manager → My Registries**
4. Install **SherpaOnnxUnity**

</details>

<details>
<summary><strong>🔗 Git URL</strong></summary>

```
https://github.com/EitanWong/com.eitan.sherpa-onnx-unity.git#upm
```

</details>

### 💻 Getting Started

**🎯 The fastest way to get started is to import and explore the sample scenes:**

1. Open **Window → Package Manager**
2. Find **SherpaOnnxUnity** in **In Project** tab
3. Expand **Samples** section
4. Click **Import** next to "SherpaOnnxUnity Sample"

The samples include:
- **Real-time Speech Recognition** - Live microphone input with real-time transcription
- **Voice Activity Detection** - Detect when users start and stop speaking
- **Offline Speech Recognition** - Process pre-recorded audio files
- **Speech Enhancement** - Real-time noise reduction with GTCRN models
- **Keyword Spotting** - Voice-activated keyword detection and wake words
- **Text-to-Speech Synthesis** - High-quality voice generation
- **Advanced Configuration** - Custom model selection and mobile optimization

Each sample contains complete, production-ready code that you can use as a starting point for your own implementation.

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
SherpaOnnxUnity/
├── Packages/com.eitan.sherpa-onnx-unity/
│   ├── Runtime/           # Core package code
│   ├── Editor/            # Unity editor extensions
│   ├── Plugins/           # Native libraries
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

### MIT License

This project is licensed under the **MIT License** - see the [LICENSE.md](LICENSE.md) file for details.

### Attribution

This package is built upon [sherpa-onnx](https://github.com/k2-fsa/sherpa-onnx), an outstanding speech processing library also under the MIT License. We extend our gratitude to the k2-fsa team for their excellent work.

### Important License Information

- ✅ **Commercial Use**: Permitted
- ✅ **Modification**: Permitted  
- ✅ **Distribution**: Permitted
- ✅ **Private Use**: Permitted
- ❗ **License Notice**: Must be included in redistributions
- ❗ **Copyright Notice**: Must be preserved

**Compliance Note**: When using this package in your projects, ensure you include the license notices for both SherpaOnnxUnity and sherpa-onnx in your application's legal documentation.

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