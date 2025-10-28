# 🧩 Contributing to SherpaOnnxUnity

Thank you for your interest in contributing to **SherpaOnnxUnity** — a Unity package for **offline ASR, TTS, VAD,** and **speech enhancement** powered by [sherpa-onnx](https://github.com/k2-fsa/sherpa-onnx).  
We welcome all forms of contribution — from reporting bugs to improving documentation or implementing new features.

---

## 📜 Table of Contents
- [🧩 Contributing to SherpaOnnxUnity](#-contributing-to-sherpaonnxunity)
  - [📜 Table of Contents](#-table-of-contents)
  - [🧭 Code of Conduct](#-code-of-conduct)
  - [💡 How to Contribute](#-how-to-contribute)
  - [⚙️ Development Setup](#️-development-setup)
    - [1. Clone the repository](#1-clone-the-repository)
    - [2. Open in Unity](#2-open-in-unity)
    - [3. Install dependencies](#3-install-dependencies)
    - [4. Run tests](#4-run-tests)
  - [🧱 Commit Guidelines](#-commit-guidelines)
    - [Common types:](#common-types)
    - [Examples](#examples)
  - [🔄 Pull Request Process](#-pull-request-process)
  - [🎨 Coding Style](#-coding-style)
    - [C# conventions](#c-conventions)
  - [🧪 Testing](#-testing)
  - [📚 Documentation](#-documentation)
  - [🧾 Release Notes](#-release-notes)
  - [💬 Community](#-community)
  - [❤️ Acknowledgments](#️-acknowledgments)

---

## 🧭 Code of Conduct
Please adhere to the [Contributor Covenant](https://www.contributor-covenant.org/) Code of Conduct.  
We expect everyone to contribute respectfully and constructively.

---

## 💡 How to Contribute

| Type | Description |
|------|--------------|
| 🐛 **Bug Reports** | Help us identify and fix issues. |
| 💡 **Feature Requests** | Suggest new features or enhancements. |
| 🧑‍💻 **Code Contributions** | Submit patches or improvements. |
| 📖 **Documentation** | Improve or translate docs and examples. |
| 🎨 **Demos & Samples** | Add or improve example scenes. |

---

## ⚙️ Development Setup

### 1. Clone the repository
```bash
git clone https://github.com/EitanWong/com.eitan.sherpa-onnx-unity.git
cd com.eitan.sherpa-onnx-unity
````

### 2. Open in Unity

Use **Unity 2021.3 LTS or later** to open the project.

### 3. Install dependencies

All dependencies are managed via Unity’s **Package Manager**.
If missing, reimport them from `Packages/manifest.json`.

### 4. Run tests

In Unity:
`Window → General → Test Runner → Run All`

---

## 🧱 Commit Guidelines

Follow the **Conventional Commits** format:

```
<type>(<scope>): <subject>
```

### Common types:

| Type       | Meaning                                 |
| ---------- | --------------------------------------- |
| `feat`     | Add a new feature                       |
| `fix`      | Fix a bug                               |
| `docs`     | Documentation changes                   |
| `style`    | Formatting or style-only changes        |
| `refactor` | Code refactor without changing behavior |
| `perf`     | Performance improvements                |
| `test`     | Add or modify tests                     |
| `chore`    | Build or tooling updates                |

### Examples

```bash
feat(tts): add streaming speech synthesis support
fix(downloader): handle retry on unstable connections
docs(readme): update sample usage section
```

---

## 🔄 Pull Request Process

1. **Create a new branch:**

   ```bash
   git checkout -b feat/my-feature
   ```

2. **Make your changes** and **run tests**.

3. **Rebase** before submitting:

   ```bash
   git pull origin main
   ```

4. **Open a Pull Request**:

   * Use a clear title.
   * Explain the motivation and changes.
   * Include screenshots or logs if applicable.

---

## 🎨 Coding Style

| Language        | Style Guide                                                                                                              |
| --------------- | ------------------------------------------------------------------------------------------------------------------------ |
| **C#**          | [Microsoft C# Style Guide](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions) |
| **YAML / JSON** | 2-space indentation, no tabs                                                                                             |
| **Markdown**    | Use proper heading levels and fenced code blocks                                                                         |

### C# conventions

* **PascalCase** for class and method names
* **camelCase** for local variables and fields
* **ALL_CAPS** for constants
* Use explicit access modifiers
* Avoid magic numbers — use constants or enums

---

## 🧪 Testing

Tests are categorized as follows:

| Type         | Description                   |
| ------------ | ----------------------------- |
| 🧩 Edit Mode | Core logic unit tests         |
| 🎮 Play Mode | Scene integration tests       |
| 📱 Platform  | Device-specific compatibility |

Run all tests via Unity’s **Test Runner** before submitting a PR.

---

## 📚 Documentation

All documentation is written in Markdown (`.md`).
You can help by:

* Improving clarity in `README.md` and `README_zh.md`
* Adding tutorials in `Documentation/`
* Keeping code examples up to date

---

## 🧾 Release Notes

Each release updates:

* `CHANGELOG.md`
* `README.md` version badge

Please do **not** manually publish new releases — this is handled by maintainers.

---

## 💬 Community

* 🐞 **Issues:** [GitHub Issues](https://github.com/EitanWong/com.eitan.sherpa-onnx-unity/issues)
* 💡 **Discussions:** [GitHub Discussions](https://github.com/EitanWong/com.eitan.sherpa-onnx-unity/discussions)
* 📖 **Wiki:** [Project Wiki](https://github.com/EitanWong/com.eitan.sherpa-onnx-unity/wiki)

Maintainer: [@EitanWong](https://github.com/EitanWong)

---

## ❤️ Acknowledgments

Thanks to all contributors who make this project better.
Special thanks to the [k2-fsa/sherpa-onnx](https://github.com/k2-fsa/sherpa-onnx) team for their excellent work enabling offline speech AI in Unity.