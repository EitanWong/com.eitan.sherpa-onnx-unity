# 🧩 Contributing to SherpaOnnxUnity

感谢你对 **SherpaOnnxUnity** 的关注！
本项目致力于让 **Unity** 开发者在本地轻松使用 **离线语音识别（ASR）**、**语音合成（TTS）**、**语音增强（Enhancement）** 与 **语音活动检测（VAD）** 等功能。
我们非常欢迎社区贡献，无论是功能开发、文档完善还是问题反馈。

---

## 📋 目录

- [🧩 Contributing to SherpaOnnxUnity](#-contributing-to-sherpaonnxunity)
  - [📋 目录](#-目录)
  - [🧭 行为准则 (Code of Conduct)](#-行为准则-code-of-conduct)
  - [💡 如何贡献 (Ways to Contribute)](#-如何贡献-ways-to-contribute)
  - [⚙️ 开发环境设置 (Development Setup)](#️-开发环境设置-development-setup)
    - [1️⃣ 克隆仓库](#1️⃣-克隆仓库)
    - [2️⃣ 打开项目](#2️⃣-打开项目)
    - [3️⃣ 安装依赖](#3️⃣-安装依赖)
    - [4️⃣ 运行测试](#4️⃣-运行测试)
  - [🧱 提交规范 (Commit Guidelines)](#-提交规范-commit-guidelines)
    - [常见类型：](#常见类型)
    - [示例：](#示例)
  - [🔄 Pull Request 流程](#-pull-request-流程)
  - [🎨 代码风格规范 (Coding Style)](#-代码风格规范-coding-style)
  - [🧪 测试与验证 (Testing)](#-测试与验证-testing)
  - [📚 文档贡献 (Docs)](#-文档贡献-docs)
  - [🧾 发布说明 (Release Notes)](#-发布说明-release-notes)
  - [💬 联系与支持 (Community)](#-联系与支持-community)
    - [❤️ 致谢](#️-致谢)

---

## 🧭 行为准则 (Code of Conduct)

我们遵循 [Contributor Covenant](https://www.contributor-covenant.org/) 行为准则。
请在贡献前阅读并遵守社区规则，保持尊重、耐心与包容。

---

## 💡 如何贡献 (Ways to Contribute)

你可以通过以下方式帮助项目成长：

| 类型              | 说明                    |
| --------------- | --------------------- |
| 🐛 **提交 Issue** | 反馈 Bug、提出问题或改进建议。     |
| 💡 **功能建议**     | 提出你希望支持的新功能或平台。       |
| 🧑‍💻 **代码贡献**  | 修复 bug、添加功能或优化性能。     |
| 📖 **文档改进**     | 改善 README、Wiki 或示例说明。 |
| 🎨 **Demo 贡献**  | 提交新的 Unity 场景或语音示例。   |

---

## ⚙️ 开发环境设置 (Development Setup)

### 1️⃣ 克隆仓库

```bash
git clone https://github.com/EitanWong/com.eitan.sherpa-onnx-unity.git
cd com.eitan.sherpa-onnx-unity
```

### 2️⃣ 打开项目

在 **Unity 2021.3 LTS 或更高版本** 中打开根目录。

### 3️⃣ 安装依赖

在 Unity 的 **Package Manager** 中确认依赖均已自动安装。
（若未自动导入，可参考 `Packages/manifest.json` 手动恢复。）

### 4️⃣ 运行测试

在 Unity 中打开：

```
Window → General → Test Runner
```

执行所有测试，确保本地环境无误。

---

## 🧱 提交规范 (Commit Guidelines)

请遵循以下 **Commit Message 格式**（符合 Conventional Commits 标准）：

```
<type>(<scope>): <subject>
```

### 常见类型：

| 类型         | 含义            |
| ---------- | ------------- |
| `feat`     | 新功能           |
| `fix`      | 修复缺陷          |
| `docs`     | 文档更新          |
| `style`    | 代码格式调整（不影响逻辑） |
| `refactor` | 重构代码（不改变功能）   |
| `perf`     | 性能优化          |
| `test`     | 测试相关改动        |
| `chore`    | 构建或工具调整       |

### 示例：

```bash
feat(tts): add streaming voice synthesis for Android
fix(downloader): handle retry logic on unstable network
docs(readme): embed new demo video links
```

---

## 🔄 Pull Request 流程

1. **从 `main` 分支创建新分支：**

   ```bash
   git checkout -b feat/your-feature-name
   ```

2. **实现与测试代码。**

3. **确保通过测试与编译。**

4. **提交 PR 前请执行：**

   ```bash
   git pull origin main
   ```

5. **创建 Pull Request：**

   * 标题简洁明了。
   * 描述清楚修改内容和动机。
   * 附上测试截图或示例（如为功能改动）。

---

## 🎨 代码风格规范 (Coding Style)

请保持与项目现有风格一致：

| 语言             | 风格规范                                                                                                                                                                                                |
| -------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **C# (Unity)** | - 遵循 [Microsoft C# Style Guide](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)  <br> - 类名、方法名使用 PascalCase <br> - 变量名使用 camelCase <br> - 常量全大写，下划线分隔 |
| **YAML/JSON**  | 两空格缩进，不使用制表符                                                                                                                                                                                        |
| **Markdown**   | 一级标题 `#`，代码块加语言标识                                                                                                                                                                                   |

---

## 🧪 测试与验证 (Testing)

项目包含三类测试：

| 测试类型         | 说明                           |
| ------------ | ---------------------------- |
| 🧩 Edit Mode | 核心逻辑单元测试                     |
| 🎮 Play Mode | 与 Unity 场景交互测试               |
| 📱 Platform  | 不同平台兼容性验证（Android/iOS/macOS） |

运行方式：

```
Window → General → Test Runner
```

如提交 PR 涉及核心逻辑，请附加相应测试用例。

---

## 📚 文档贡献 (Docs)

* 所有文档均采用 Markdown (`.md`)
* 中文文档放在 `README_zh.md` 或 `Documentation/` 目录下
* 若修改用户指南或 API，请同步更新：

  * `README.md`
  * `Wiki` 页面（可通过 PR 注明）

---

## 🧾 发布说明 (Release Notes)

每次版本发布会更新：

* `CHANGELOG.md`
* `README.md` 中的版本徽章

请不要直接创建 Release；由项目维护者统一发布。

---

## 💬 联系与支持 (Community)

* 🐞 [GitHub Issues](https://github.com/EitanWong/com.eitan.sherpa-onnx-unity/issues) — 报告问题或反馈。
* 💡 [Discussions](https://github.com/EitanWong/com.eitan.sherpa-onnx-unity/discussions) — 提问、建议或分享使用经验。
* 📖 [Wiki](https://github.com/EitanWong/com.eitan.sherpa-onnx-unity/wiki) — 获取详细文档。
* 🧑‍💻 Maintainer: [@EitanWong](https://github.com/EitanWong)

---

### ❤️ 致谢

感谢所有为项目贡献代码、文档与测试的开发者。
你们让 **离线语音智能** 更易于接入每一个 Unity 项目。