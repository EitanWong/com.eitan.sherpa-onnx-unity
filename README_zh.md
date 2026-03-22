<div align="center">

# 🎙️ SherpaONNXUnity

### Unity 离线语音 AI 能力包：ASR、TTS、VAD、说话人分离等

> **For English users**: This project provides English documentation. Please see [README.md](./README.md) for detailed English instructions.

**语言**: [English](./README.md) | [中文](./README_zh.md)

[![OpenUPM](https://img.shields.io/npm/v/com.eitan.sherpa-onnx-unity?label=openupm&registry_uri=https://package.openupm.com&style=flat-square&color=blue)](https://openupm.com/packages/com.eitan.sherpa-onnx-unity/)
[![Downloads](https://img.shields.io/badge/dynamic/json?color=brightgreen&label=downloads&suffix=%2Fmonth&url=https%3A%2F%2Fpackage.openupm.com%2Fdownloads%2Fpoint%2Flast-month%2Fcom.eitan.sherpa-onnx-unity&style=flat-square)](https://openupm.com/packages/com.eitan.sherpa-onnx-unity/)
[![Unity](https://img.shields.io/badge/Unity-2021.3%2B-black?style=flat-square&logo=unity)](https://unity.com/)
[![License](https://img.shields.io/badge/License-Apache_2.0-blue.svg?style=flat-square)](LICENSE.md)

📋 **[查看更新日志](./SherpaONNXUnity/Packages/com.eitan.sherpa-onnx-unity/CHANGELOG.md)** | 📊 **当前最新包版本: v0.1.3-exp.3**

</div>

## 🎬 演示

这里是一些功能的视频演示。

<div align="center">

| 语言       | 演示视频                                                                                                  | 语言   | 演示视频                                                                                               |
|------------|-----------------------------------------------------------------------------------------------------------|--------|--------------------------------------------------------------------------------------------------------|
| 英语和中文 | <video src="https://github.com/user-attachments/assets/d1df8412-042f-4c66-947a-98fb4784ba2e" width="400" controls>您的浏览器不支持视频标签。</video> | 法语   | <video src="https://github.com/user-attachments/assets/0760f2b9-0c0e-4df0-9ed1-86a78c94fd33" width="400" controls>您的浏览器不支持视频标签。</video>            |
| 日语       | <video src="https://github.com/user-attachments/assets/ec52b860-e945-4574-b5b3-11cbed741113" width="400" controls>您的浏览器不支持视频标签。</video>          | 韩语   | <video src="https://github.com/user-attachments/assets/6707a16a-c12d-464a-a57e-24b044d87e76" width="400" controls>您的浏览器不支持视频标签。</video>            |
| 俄语       | <video src="https://github.com/user-attachments/assets/9ce70da8-44a9-4d6b-8864-d9e24d535dfa" width="400" controls>您的浏览器不支持视频标签。</video>            | 四川话 | <video src="https://github.com/user-attachments/assets/bdfed3a3-efe7-4899-bd8a-8d63bd8a30c8" width="400" controls>您的浏览器不支持视频标签。</video>  |

</div>

如果想观看更详细的介绍，您也可以在 [Bilibili](https://www.bilibili.com/video/BV1E38hz3ETw/?share_source=copy_web&vd_source=06d081c8a7b3c877a41f801ce5915855) 上观看视频。

---

## 🆕 v0.1.3-exp.3 更新内容 (2026-03-25)

### 🚀 本次更新亮点
- **官方 sherpa-onnx Native 运行时升级到 v1.12.32**
  全平台（Android、Windows、Linux、macOS、iOS）原生库已同步刷新至最新版本。

### 📱 Android 说明
- 生产环境仍建议优先使用 `arm64-v8a`。
- `armeabi-v7a` 仍然允许使用，但某些上游 native create/init 路径在部分模型或模块上可能依旧不稳定。

### 已知问题
- 在 Android `armeabi-v7a`（32 位）上，某些 sherpa-onnx / ONNX Runtime 的 create 或初始化路径，针对特定模型或模块仍可能发生崩溃。
- 当前 Unity 封装会为这些 32 位路径输出统一的运行时风险提示，但不会强制阻止初始化。
- 生产环境建议优先选择 `arm64-v8a`。

[📋 **查看完整更新日志**](./SherpaONNXUnity/Packages/com.eitan.sherpa-onnx-unity/CHANGELOG.md)

---

## 🚀 项目概述

一个为 Unity 游戏引擎带来**离线自动语音识别（ASR）**、**文本转语音（TTS）**、**语音活动检测（VAD）**、**说话人分离（Speaker Diarization）**等能力的 Unity 包，基于强大的 [sherpa-onnx](https://github.com/k2-fsa/sherpa-onnx) 开发。

✨ **具备智能自动模型下载和断点续传功能**，确保无缝设置，同时 📱 **针对移动平台优化集成**，可用于生产环境部署。

## 🌟 主要特性

### 🎯 核心能力
- **🔌 离线运行** - 设置后无需网络连接
- **⚡ 实时处理** - 低延迟语音识别
- **🗣️ 语音活动检测** - 智能语音边界检测
- **👥 说话人分离** - 对多人音频进行说话人聚类，分析“谁在什么时候说话”
- **🔊 语音增强** - GTCRN噪声消除与音质改善
- **👂 关键词检测** - 语音激活的关键词识别，现已支持自定义关键词。
- **🎤 文本转语音** - 高质量语音合成
- **🌍 语种识别** - 识别给定音频片段的语言。
- **🎼 音频标签** - 自动检测和分类各种音频事件，如音乐、交通和环境声音
- **🖥️ 跨平台支持** - Windows、macOS、Linux、Android

### 🤖 智能模型管理
- **🔄 自动下载** - 模型无缝下载
- **📡 断点续传** - 网络中断处理
- **🔐 哈希验证** - 内置完整性验证
- **💾 智能缓存** - 本地存储优化

### 🛠️ 开发者体验
- **🎮 Unity原生** - 无缝工作流集成
- **📚 丰富文档** - 全面的示例
- **🔄 定期更新** - 最新sherpa-onnx改进

## 🏗️ 架构设计

> 基于强大的[sherpa-onnx](https://github.com/k2-fsa/sherpa-onnx)语音处理库的Unity原生封装。

**核心组件：**
- 📚 **原生集成** - 跨平台二进制文件，移动端优化
- 🤖 **智能模型管理** - 后台下载和断点续传
- 🎮 **Unity组件** - 基于MonoBehaviour的场景集成

## 🚀 快速入门

### 📋 前置要求

**Unity:** 2021.3 LTS+ | **存储空间:** 约500MB | **平台:** Win/Mac/Linux/Android

### 📦 安装方式

> **请注意:** 本包目前处于实验阶段，尚未发布到 OpenUPM。请使用 Git URL 方式进行安装。

<details open>
<summary><strong>🎯 通过 Git URL 添加包（推荐）</strong></summary>

1. 在 Unity 编辑器中，打开 **Window → Package Manager**。
2. 点击左上角的 **+** 按钮。
3. 选择 **"Add package from git URL..."**
4. 输入以下 URL 并点击 **Add**:
   ```
   https://github.com/EitanWong/com.eitan.sherpa-onnx-unity.git#upm
   ```

</details>

<details>
<summary><strong>🔧 Unity Package Manager (通过 Scoped Registry - 即将支持)</strong></summary>

> 该方法将在包正式发布到 OpenUPM 后可用。

1. **Edit → Project Settings → Package Manager**
2. 添加 Scoped Registry:
   - Name: `OpenUPM`
   - URL: `https://package.openupm.com`
   - Scope: `com.eitan.sherpa-onnx-unity`
3. **Window → Package Manager → My Registries**
4. 安装 **SherpaONNXUnity**

</details>

<details>
<summary><strong>🔗 OpenUPM (命令行 - 即将支持)</strong></summary>

> 该方法将在包正式发布到 OpenUPM 后可用。

```bash
openupm add com.eitan.sherpa-onnx-unity
```

</details>

### 💻 快速上手

**🎯 最快的入门方式是导入并探索示例场景：**

1. 打开 **Window → Package Manager**
2. 在 **In Project** 标签页中找到 **SherpaONNXUnity**
3. 展开 **Samples** 部分
4. 点击"SherpaONNXUnity Sample"旁边的 **Import**

示例包含：
- **实时语音识别** - 麦克风实时输入和转录
- **语音活动检测** - 检测用户开始和停止说话
- **说话人分离** - 分析多人录音并聚类说话人片段
- **离线语音识别** - 处理预录制音频文件
- **语音增强** - 使用GTCRN模型进行实时降噪
- **关键词检测** - 语音激活的关键词检测和唤醒词
- **语种识别** - 从音频片段中识别语言
- **文本转语音合成** - 高质量语音生成
- **音频标签** - 自动检测和分类各种音频事件
- **零样本语音合成** - 基于提示词的声音克隆，附带示例提示词资产

每个示例都包含完整的、示例代码，您可以将其作为自己实现的起点。

**新的拖拽式组件流程（无需样板代码）：**
- 在场景中添加 `SherpaMicrophoneInput`，用于产生 PCM 数据。
- 添加对应模块组件（如 `SpeechRecognizerComponent`、`AudioTaggingComponent`、`VoiceActivityDetectionComponent`、`SpeakerDiarizationComponent`、`ZeroShotSpeechSynthesisComponent`），并设置 `Model Id`。
- 订阅 UnityEvents（如 `TranscriptionReadyEvent`、`ClipReadyEvent`）获取结果；组件会在模型加载完成后自动启动采集。
- 更完整的 API/组件用法见 `SherpaONNXUnity/Packages/com.eitan.sherpa-onnx-unity/Documentation~/README.md` 双语指南。

### 模型管理器

通过 **Window → Sherpa ONNX → Model Manager** 打开模型管理器窗口

![模型管理器](https://github.com/user-attachments/assets/ce622a7d-0885-406d-9a97-78ea89474731)

通过模型管理器，你可以搜索所有sherpa-onnx支持的模型，并提供一键下载到本地的功能。

#### 代码层面的运行时设置
`SherpaONNXUnityAPI` 暴露了与环境变量一致的接口，便于运行时动态调整：

```csharp
SherpaONNXUnityAPI.SetAutoDownloadModels(false);      // 对应 SHERPA_ONNX_AUTO_DOWNLOAD
SherpaONNXUnityAPI.SetFetchLatestManifest(true);      // 对应 SHERPA_ONNX_FETCH_LATEST_MANIFEST
SherpaONNXUnityAPI.SetChecksumCacheDirectory(
    Path.Combine(Application.persistentDataPath, "SherpaCache"));
SherpaONNXUnityAPI.SetChecksumCacheTtlSeconds(0);     // 分发离线包时可关闭缓存
```

结合 `SetGithubProxy` / `ClearGithubProxy`，可在不修改 ScriptableObject 的情况下控制下载行为。

## 🛠️ 开发

### 从源码构建

1. 克隆仓库：
   ```bash
   git clone https://github.com/EitanWong/com.eitan.sherpa-onnx-unity.git
   cd com.eitan.sherpa-onnx-unity
   ```

2. 在Unity 2021.3 LTS或更高版本中打开

3. 通过Package Manager安装依赖

4. 导入示例场景并测试功能

5. 为目标平台构建

### 测试

- **编辑模式测试**: 核心功能的单元测试
- **播放模式测试**: Unity组件的集成测试
- **平台测试**: 跨平台兼容性验证

通过 **Window → General → Test Runner** 运行测试

### 项目结构

```
SherpaONNXUnity/
├── Packages/com.eitan.sherpa-onnx-unity/
│   ├── Runtime/           # 核心包代码
│   ├── Editor/            # Unity编辑器扩展
│   ├── Tests/             # 单元和集成测试
│   └── Samples~/          # 示例场景和脚本
├── Assets/Demo/           # 演示项目
└── Documentation/         # 额外文档
```

## 🤝 贡献

我们欢迎社区贡献！详情请参阅我们的[贡献指南](CONTRIBUTING_zh.md)：

- 🐛 报告错误
- 💡 建议功能
- 🔧 提交拉取请求
- 📖 改进文档

## 📄 许可证和法律

### Apache 2.0 许可证

本项目基于**Apache 2.0 许可证**授权 - 详情请参阅[LICENSE.md](LICENSE.md)文件。

### 致谢

本包基于[sherpa-onnx](https://github.com/k2-fsa/sherpa-onnx)构建，这是一个同样基于Apache 2.0 许可证的优秀语音处理库。我们向k2-fsa团队的出色工作表示感谢。

### 重要许可信息

- ✅ **商业使用**: 允许
- ✅ **修改**: 允许
- ✅ **分发**: 允许
- ✅ **私人使用**: 允许
- ❗ **许可声明**: 必须包含在再分发中
- ❗ **版权声明**: 必须保留

**合规说明**: 在项目中使用此包时，确保在应用程序的法律文档中包含SherpaONNXUnity和sherpa-onnx的许可声明。

## 🙏 致谢

- **[sherpa-onnx](https://github.com/k2-fsa/sherpa-onnx)**: 使Unity集成成为可能的强大语音处理引擎
- **k2-fsa团队**: 创建和维护世界级sherpa-onnx库
- **Unity社区**: 持续的反馈、测试和贡献
- **贡献者**: 所有帮助改进此项目的开发者

## 📞 支持和社区

### 获取帮助

- 🐛 **问题**: [GitHub Issues](https://github.com/EitanWong/com.eitan.sherpa-onnx-unity/issues)用于错误报告
- 📖 **Wiki**: [项目Wiki](https://github.com/EitanWong/com.eitan.sherpa-onnx-unity/wiki)获得详细指南
- 💡 **讨论**: [GitHub Discussions](https://github.com/EitanWong/com.eitan.sherpa-onnx-unity/discussions)用于问题和想法

### 保持更新

- ⭐ **收藏**此仓库以保持更新
- 👀 **关注**发布以获得新版本
- 🐦 **关注** [@EitanWong](https://github.com/EitanWong)获取更新

## 🔗 链接和资源

| 资源 | 链接 |
|----------|------|
| 📦 **包注册表** | [OpenUPM](https://openupm.com/packages/com.eitan.sherpa-onnx-unity/) |
| 🏪 **Unity Asset Store** | 即将推出 |
| 📂 **源代码** | [GitHub](https://github.com/EitanWong/com.eitan.sherpa-onnx-unity) |
| 🤖 **sherpa-onnx** | [原始项目](https://github.com/k2-fsa/sherpa-onnx) |
| 📚 **文档** | [Wiki](https://github.com/EitanWong/com.eitan.sherpa-onnx-unity/wiki) |
| 🎯 **路线图** | [项目看板](https://github.com/EitanWong/com.eitan.sherpa-onnx-unity/projects) |

---

<div align="center">

**由[Eitan](https://github.com/EitanWong)用❤️制作**

*基于[sherpa-onnx](https://github.com/k2-fsa/sherpa-onnx) | 受Unity社区启发*

</div>
