<div align="center">

# SherpaONNXUnity

面向 Unity 的离线语音与音频 AI 能力包，基于 [sherpa-onnx](https://github.com/k2-fsa/sherpa-onnx)。

[English](./README.md)

[![OpenUPM](https://img.shields.io/npm/v/com.eitan.sherpa-onnx-unity?label=openupm&registry_uri=https://package.openupm.com&style=flat-square&color=blue)](https://openupm.com/packages/com.eitan.sherpa-onnx-unity/)
[![Downloads](https://img.shields.io/badge/dynamic/json?color=brightgreen&label=downloads&suffix=%2Fmonth&url=https%3A%2F%2Fpackage.openupm.com%2Fdownloads%2Fpoint%2Flast-month%2Fcom.eitan.sherpa-onnx-unity&style=flat-square)](https://openupm.com/packages/com.eitan.sherpa-onnx-unity/)
[![Unity](https://img.shields.io/badge/Unity-2021.3%2B-black?style=flat-square&logo=unity)](https://unity.com/)
[![License](https://img.shields.io/badge/License-Apache_2.0-blue?style=flat-square)](./SherpaONNXUnity/Packages/com.eitan.sherpa-onnx-unity/LICENSE.md)

**最新包版本：** `v0.1.3-exp.4`
**sherpa-onnx 原生运行时：** `v1.13.0`

[包内文档](./SherpaONNXUnity/Packages/com.eitan.sherpa-onnx-unity/Documentation~/README.md) |
[更新日志](./SherpaONNXUnity/Packages/com.eitan.sherpa-onnx-unity/CHANGELOG.md) |
[OpenUPM](https://openupm.com/packages/com.eitan.sherpa-onnx-unity/)

</div>

---

## 项目概述

SherpaONNXUnity 将 sherpa-onnx 的主要语音与音频能力集成到 Unity，提供原生插件、模型管理工具、编辑器窗口、示例场景和可直接使用的 Mono 组件。

它适合需要离线语音识别、语音合成、说话人分析、音频理解或实时麦克风处理的 Unity 应用。运行时不依赖云端服务。

## v0.1.3-exp.4 更新亮点

- 同步 sherpa-onnx 原生库至 `v1.13.0`。
- 支持 iOS 平台，内置静态原生库。
- 完善 SherpaONNXUnity 在 Unity 场景和 Inspector 中的组件工作流。
- 新增 Source Separation 支持。
- 新增 Speaker Diarization 支持。
- 进一步补齐 sherpa-onnx 在 Unity 中的主要功能覆盖。

## 功能

|  |  |  |
|---|---|---|
| 🎙️ [语音识别](./SherpaONNXUnity/Packages/com.eitan.sherpa-onnx-unity/Documentation~/README.zh.md#speech-recognition) | 🔊 [语音合成](./SherpaONNXUnity/Packages/com.eitan.sherpa-onnx-unity/Documentation~/README.zh.md#speech-synthesis) | 🎚️ [音源分离](./SherpaONNXUnity/Packages/com.eitan.sherpa-onnx-unity/Documentation~/README.zh.md#source-separation) |
| 👤 [说话人识别](./SherpaONNXUnity/Packages/com.eitan.sherpa-onnx-unity/Documentation~/README.zh.md#speaker-identification) | 👥 [说话人分离](./SherpaONNXUnity/Packages/com.eitan.sherpa-onnx-unity/Documentation~/README.zh.md#speaker-diarization) | ✅ [说话人验证](./SherpaONNXUnity/Packages/com.eitan.sherpa-onnx-unity/Documentation~/README.zh.md#speaker-verification) |
| 🌐 [语种识别](./SherpaONNXUnity/Packages/com.eitan.sherpa-onnx-unity/Documentation~/README.zh.md#spoken-language-identification) | 🏷️ [音频标签](./SherpaONNXUnity/Packages/com.eitan.sherpa-onnx-unity/Documentation~/README.zh.md#audio-tagging) | 📈 [语音活动检测](./SherpaONNXUnity/Packages/com.eitan.sherpa-onnx-unity/Documentation~/README.zh.md#voice-activity-detection) |
| 🔑 [关键词检测](./SherpaONNXUnity/Packages/com.eitan.sherpa-onnx-unity/Documentation~/README.zh.md#keyword-spotting) | ✍️ [标点恢复](./SherpaONNXUnity/Packages/com.eitan.sherpa-onnx-unity/Documentation~/README.zh.md#punctuation) | ✨ [语音增强](./SherpaONNXUnity/Packages/com.eitan.sherpa-onnx-unity/Documentation~/README.zh.md#speech-enhancement) |

## 平台支持

| 平台 | 架构 |
|---|---|
| Windows | x64 |
| macOS | Apple Silicon / Intel |
| Linux | x64 |
| Android | `arm64-v8a`、`armeabi-v7a`、`x86`、`x86_64` |
| iOS | `ios-arm64`、`ios-arm64_x86_64-simulator` |

Android 生产环境推荐使用 `arm64-v8a`。`armeabi-v7a` 在部分模型的上游原生初始化路径中仍可能不稳定。

## 演示

| 语言 | 演示视频 | 语言 | 演示视频 |
|---|---|---|---|
| 英语和中文 | <video src="https://github.com/user-attachments/assets/d1df8412-042f-4c66-947a-98fb4784ba2e" width="400" controls>您的浏览器不支持视频标签。</video> | 法语 | <video src="https://github.com/user-attachments/assets/0760f2b9-0c0e-4df0-9ed1-86a78c94fd33" width="400" controls>您的浏览器不支持视频标签。</video> |
| 日语 | <video src="https://github.com/user-attachments/assets/ec52b860-e945-4574-b5b3-11cbed741113" width="400" controls>您的浏览器不支持视频标签。</video> | 韩语 | <video src="https://github.com/user-attachments/assets/6707a16a-c12d-464a-a57e-24b044d87e76" width="400" controls>您的浏览器不支持视频标签。</video> |
| 俄语 | <video src="https://github.com/user-attachments/assets/9ce70da8-44a9-4d6b-8864-d9e24d535dfa" width="400" controls>您的浏览器不支持视频标签。</video> | 四川话 | <video src="https://github.com/user-attachments/assets/bdfed3a3-efe7-4899-bd8a-8d63bd8a30c8" width="400" controls>您的浏览器不支持视频标签。</video> |

更多介绍可以查看 [Bilibili 演示视频](https://www.bilibili.com/video/BV1E38hz3ETw/?share_source=copy_web&vd_source=06d081c8a7b3c877a41f801ce5915855)。

## 前置要求

**Unity：** 2021.3 LTS+ | **存储空间：** 约 500MB | **平台：** Win/Mac/Linux/Android/iOS

## 安装

### Git URL

在 Unity 中打开 **Window > Package Manager**，点击 **+**，选择 **Add package from git URL...**，添加：

```text
https://github.com/EitanWong/com.eitan.sherpa-onnx-unity.git#upm
```

### OpenUPM

添加 scoped registry：

```text
Name: OpenUPM
URL: https://package.openupm.com
Scope: com.eitan.sherpa-onnx-unity
```

然后在 **My Registries** 中安装 `com.eitan.sherpa-onnx-unity`。

## 快速开始

最快的方式是导入示例包，并打开 `Samples~/Collection` 下的示例场景。

1. 安装 SherpaONNXUnity。
2. 在 Package Manager 中导入 **SherpaONNXUnity Sample**。
3. 打开示例场景。
4. 通过 **Window > SherpaONNX > Model Manager** 选择或下载模型。
5. 进入 Play。

## Model Manager

SherpaONNXUnity 提供编辑器内的 Model Manager，用于浏览支持的模型、下载模型资源，并为示例场景或自定义场景准备本地模型文件。

打开路径：

```text
Window/SherpaONNX/Model Manager
```

![Model Manager](https://github.com/user-attachments/assets/64fd2301-fd14-4616-a5ba-dd1a6b1cdc55)

组件用法、运行时配置、模型下载、自定义清单和编辑器工具见 [包内文档](./SherpaONNXUnity/Packages/com.eitan.sherpa-onnx-unity/Documentation~/README.zh.md)。

## 仓库结构

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

## 许可证

SherpaONNXUnity 使用 Apache 2.0 License。

本项目包含第三方软件，详见：

- [LICENSE.md](./SherpaONNXUnity/Packages/com.eitan.sherpa-onnx-unity/LICENSE.md)
- [THIRD PARTY NOTICES.md](./SherpaONNXUnity/Packages/com.eitan.sherpa-onnx-unity/THIRD%20PARTY%20NOTICES.md)

## 致谢

SherpaONNXUnity 基于官方 [sherpa-onnx](https://github.com/k2-fsa/sherpa-onnx) 项目构建。感谢 k2-fsa 团队和所有贡献者，让高质量的离线语音与音频 AI 能力可以被社区使用。

## 链接

| 资源 | 链接 |
|---|---|
| 包注册表 | [OpenUPM](https://openupm.com/packages/com.eitan.sherpa-onnx-unity/) |
| sherpa-onnx 官方仓库 | [k2-fsa/sherpa-onnx](https://github.com/k2-fsa/sherpa-onnx) |
| 包内文档 | [Documentation~/README.md](./SherpaONNXUnity/Packages/com.eitan.sherpa-onnx-unity/Documentation~/README.md) |
| 更新日志 | [CHANGELOG.md](./SherpaONNXUnity/Packages/com.eitan.sherpa-onnx-unity/CHANGELOG.md) |
