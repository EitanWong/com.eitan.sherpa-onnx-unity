<div align="center">

# 🎙️ SherpaOnnxUnity

### Unity离线语音识别与语音活动检测包

> **For English users**: This project provides English documentation. Please see [README.md](./README.md) for detailed English instructions.
> 
> **English用户请注意**: 本项目提供英文文档，请查看 [README.md](./README.md) 获取详细的英文说明。

**语言**: [English](./README.md) | [中文](./README_zh.md)

[![OpenUPM](https://img.shields.io/npm/v/com.eitan.sherpa-onnx-unity?label=openupm&registry_uri=https://package.openupm.com&style=flat-square&color=blue)](https://openupm.com/packages/com.eitan.sherpa-onnx-unity/) 
[![Downloads](https://img.shields.io/badge/dynamic/json?color=brightgreen&label=downloads&suffix=%2Fmonth&url=https%3A%2F%2Fpackage.openupm.com%2Fdownloads%2Fpoint%2Flast-month%2Fcom.eitan.sherpa-onnx-unity&style=flat-square)](https://openupm.com/packages/com.eitan.sherpa-onnx-unity/)
[![Unity](https://img.shields.io/badge/Unity-2021.3%2B-black?style=flat-square&logo=unity)](https://unity.com/)
[![License](https://img.shields.io/badge/License-MIT-yellow.svg?style=flat-square)](LICENSE.md)

</div>

## 🎬 演示视频

<div align="center">

[![Unity本地离线语音识别演示](https://img.shields.io/badge/🎥_观看演示-Unity语音识别-blue?style=for-the-badge)](https://www.bilibili.com/video/BV1E38hz3ETw/?share_source=copy_web&vd_source=06d081c8a7b3c877a41f801ce5915855)

*点击上方观看SherpaOnnxUnity实时语音识别演示效果*

</div>

---

## 🚀 项目概述

一个为Unity游戏引擎带来**离线自动语音识别（ASR）**、**文本转语音（TTS）**和**语音活动检测（VAD）**功能的Unity包，基于强大的[sherpa-onnx](https://github.com/k2-fsa/sherpa-onnx)开发。

✨ **具备智能自动模型下载和断点续传功能**，确保无缝设置，同时 📱 **针对移动平台优化集成**，可用于生产环境部署。

## 🌟 主要特性

### 🎯 核心能力
- **🔌 离线运行** - 设置后无需网络连接
- **⚡ 实时处理** - 低延迟语音识别
- **🎯 语音活动检测** - 智能语音边界检测
- **🌍 跨平台支持** - Windows、macOS、Linux、Android

### 🤖 智能模型管理
- **🔄 自动下载** - 模型无缝下载
- **📡 断点续传** - 网络中断处理
- **🔐 哈希验证** - 内置完整性验证
- **💾 智能缓存** - 本地存储优化

### 📱 移动优先设计
- **🏃‍♂️ Android优化** - ARM64/ARMv7/x64原生支持
- **🧠 内存高效** - 智能资源管理
- **🔋 电池优化** - 可配置性能模式
- **📦 生产就绪** - 经过实战测试的部署

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

<details>
<summary><strong>🎯 OpenUPM（推荐）</strong></summary>

```bash
openupm add com.eitan.sherpa-onnx-unity
```

</details>

<details>
<summary><strong>🔧 Unity Package Manager</strong></summary>

1. **Edit → Project Settings → Package Manager**
2. 添加Scoped Registry：
   - Name: `OpenUPM`
   - URL: `https://package.openupm.com`
   - Scope: `com.eitan.sherpa-onnx-unity`
3. **Window → Package Manager → My Registries**
4. 安装 **SherpaOnnxUnity**

</details>

<details>
<summary><strong>🔗 Git URL</strong></summary>

```
https://github.com/EitanWong/com.eitan.sherpa-onnx-unity.git#upm
```

</details>

### 💻 快速上手

**🎯 最快的入门方式是导入并探索示例场景：**

1. 打开 **Window → Package Manager**
2. 在 **In Project** 标签页中找到 **SherpaOnnxUnity**
3. 展开 **Samples** 部分
4. 点击"SherpaOnnxUnity Sample"旁边的 **Import**

示例包含：
- **实时语音识别** - 麦克风实时输入和转录
- **语音活动检测** - 检测用户开始和停止说话
- **离线语音识别** - 处理预录制音频文件
- **高级配置** - 自定义模型选择和移动端优化

每个示例都包含完整的、生产就绪的代码，您可以将其作为自己实现的起点。

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
SherpaOnnxUnity/
├── Packages/com.eitan.sherpa-onnx-unity/
│   ├── Runtime/           # 核心包代码
│   ├── Editor/            # Unity编辑器扩展
│   ├── Plugins/           # 原生库
│   ├── Tests/             # 单元和集成测试
│   └── Samples~/          # 示例场景和脚本
├── Assets/Demo/           # 演示项目
└── Documentation/         # 额外文档
```

## 🤝 贡献

我们欢迎社区贡献！详情请参阅我们的[贡献指南](CONTRIBUTING.md)：

- 🐛 报告错误
- 💡 建议功能
- 🔧 提交拉取请求
- 📖 改进文档

## 📄 许可证和法律

### MIT许可证

本项目基于**MIT许可证**授权 - 详情请参阅[LICENSE.md](LICENSE.md)文件。

### 致谢

本包基于[sherpa-onnx](https://github.com/k2-fsa/sherpa-onnx)构建，这是一个同样基于MIT许可证的优秀语音处理库。我们向k2-fsa团队的出色工作表示感谢。

### 重要许可信息

- ✅ **商业使用**: 允许
- ✅ **修改**: 允许  
- ✅ **分发**: 允许
- ✅ **私人使用**: 允许
- ❗ **许可声明**: 必须包含在再分发中
- ❗ **版权声明**: 必须保留

**合规说明**: 在项目中使用此包时，确保在应用程序的法律文档中包含SherpaOnnxUnity和sherpa-onnx的许可声明。

## 🙏 致谢

- **[sherpa-onnx](https://github.com/k2-fsa/sherpa-onnx)**: 使Unity集成成为可能的强大语音处理引擎
- **k2-fsa团队**: 创建和维护世界级sherpa-onnx库
- **Unity社区**: 持续的反馈、测试和贡献
- **贡献者**: 所有帮助改进此项目的开发者

## 📞 支持和社区

### 获取帮助

- 💬 **Discord**: [加入我们的社区](https://discord.gg/xQgMW9ufN4)获得实时支持
- 🐛 **问题**: [GitHub Issues](https://github.com/EitanWong/com.eitan.sherpa-onnx-unity/issues)用于错误报告
- 📖 **Wiki**: [项目Wiki](https://github.com/EitanWong/com.eitan.sherpa-onnx-unity/wiki)获得详细指南
- 💡 **讨论**: [GitHub Discussions](https://github.com/EitanWong/com.eitan.sherpa-onnx-unity/discussions)用于问题和想法

### 保持更新

- ⭐ **收藏**此仓库以保持更新
- 👀 **关注**发布以获得新版本
- 🐦 **关注** [@EitanWongWong](https://github.com/EitanWongWong)获取更新

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

**由[Eitan](https://github.com/EitanWongWong)用❤️制作**

*基于[sherpa-onnx](https://github.com/k2-fsa/sherpa-onnx) | 受Unity社区启发*

</div>