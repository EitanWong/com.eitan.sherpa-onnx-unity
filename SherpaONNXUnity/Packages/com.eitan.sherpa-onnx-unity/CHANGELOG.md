# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http.keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.1.2-exp.3] - 2025-12-08

### Changed
- Restructured the overall codebase, refactoring several module implementations to improve readability, maintainability, and architectural robustness.
- Optimized performance across core components to reduce overhead and improve runtime efficiency.
- Updated the native Sherpa-ONNX dependency to v1.12.19, aligning with the latest upstream changes for enhanced compatibility and feature support.
- Improved editor window responsiveness with better rendering performance and smoother interaction flow.
- Refined the editor UI for a cleaner and more polished user experience.
### Added
- Introduced the SherpaONNX Profiler editor tool, enabling real-time performance monitoring and activity tracking for all Sherpa ONNX modules.
- Integrated a new logging system within the profiler to trace module behavior and assist with debugging, diagnostics, and performance analysis.

## [0.1.2-exp.2] - 2025-11-23

### Added
- Editor localization (English/Chinese) plus tailored inspectors and menu entries for every SherpaONNX Mono component.
- Drop-in MonoBehaviour components for ASR, VAD, punctuation, keyword spotting, audio tagging, speech enhancement, TTS, and zero-shot TTS, with shared microphone input streaming.
- New sample scenes for **Audio Tagging** and **Zero-Shot Speech Synthesis**, including prompt assets, progress UI, and updated demo scripts.
- `SherpaONNXUnityAPI` exposes runtime toggles (`SetAutoDownloadModels`, `SetFetchLatestManifest`, checksum cache helpers) so developers can apply the issue #4 recommendation directly from code.

### Changed
- Reorganized native plugins under `Runtime/Plugins` and refreshed sherpa-onnx c-api binaries across Android (ARMv7/ARM64/x86/x64), Windows x64, Linux x64, macOS, and iOS while trimming obsolete x86 DLLs.
- Expanded model constants/resolvers and added `ModelFileResolver` helpers so modules can prepare newer ASR/TTS/audio-tagging families with adaptive threading and clearer feedback.
- Updated editor tooling (model manager, settings provider, runtime settings utility) with localization and more robust streaming transcription deduplication.

### Fixed
- Hardened microphone chunking and streaming queues in demo scripts and the speech recognizer component to avoid duplicate transcripts and race conditions during teardown.
- Unified decompression helper and progress tracking to reduce stalls when preparing models from downloads.

## [0.1.2-exp.1] - 2025-10-28

### Added
- Integrated **Audio Tagging** module from Sherpa-ONNX for sound event recognition.
- Released Unity demo scene showcasing **zero-shot TTS** via ZipVoice (experimental feature).

### Changed
- Supplemented model metadata for various modules to enhance compatibility.
- Optimized decompression process; default extraction remains via SharpZipLib for cross-platform support.

### Fixed
- Resolved IL2CPP build issues by upgrading Sherpa-ONNX to v1.12.15 and restructuring native plugin directories. (https://github.com/EitanWong/com.eitan.sherpa-onnx-unity/issues/2)

### Known Issues
- **ZipVoice TTS Demo**:
  - Chinese prompts may produce intermittent white noise or distorted audio.
  - English prompts may fail during synthesis setup, leading to crashes.
  - Suspected cause: Inconsistent handling of eSpeak-NG data or voice selection in the native layer.
  - Note: This feature is **not production-ready** and is provided for evaluation purposes only.

## [0.1.1-exp.3] - 2025-10-17

### Changed
- Used a self-optimized and compiled sherpa-onnx.dll .NET library to enhance P/Invoke security and compatibility with the iOS platform. (https://github.com/EitanWong/sherpa-onnx/tree/unity)
- **License**: The project license has been changed from MIT to **Apache 2.0**.
- **Model Management Overhaul**:
  - The model registry (`SherpaONNXModelRegistry`) is now fully asynchronous, fetching the latest model list from GitHub releases instead of relying on a local, static manifest. This ensures access to the newest models without package updates.
  - The model downloader (`SherpaFileDownloader`) has been completely rewritten for improved robustness, featuring chunked downloading, automatic retry logic, network health checks, and adaptive concurrency.
  - Simplified `SherpaONNXModelMetadata`, removing the need to declare individual model files. The system now dynamically detects files after extraction.
- **iOS Integration**: Renamed iOS native libraries from `*.a` to `lib*.a` to align with standard conventions, improving compatibility with build systems.

### Added
- Added the functionality to fetch the model list from sherpa-onnx online by default.
- **Automatic English Casing**: ASR results in English are now automatically converted to proper sentence case (e.g., "hello world" becomes "Hello world."). This includes smart handling of proper nouns, acronyms (like "USA"), and contractions (like "it's").
- **Adaptive Performance**: Introduced `ThreadingUtils` to automatically adjust the number of threads used by ONNX models based on the device's CPU cores, memory, and platform (mobile/desktop), optimizing performance and power consumption.

### Fixed
- Fixed a bug that caused the offline speech recognition to crash on Android. (https://github.com/EitanWong/com.eitan.sherpa-onnx-unity/issues/1)
- **Microphone Lifecycle**: Improved microphone handling in demo scenes to ensure proper cleanup and prevent resource leaks when the application quits or the scene is destroyed.
- **Model Initialization**: Refactored module initialization to be more robust, providing clearer success/failure feedback and ensuring that native handles are correctly managed.

### Improved
- Improved the stability of model loading by using reflection to confirm that the model has been loaded.
- **Editor UX**:
  - The "SherpaONNX Models" editor window now uses a high-performance virtualized scroll list, allowing it to display thousands of models without freezing or slowing down.
  - A loading spinner is now displayed while fetching the model manifest, providing better user feedback.
  - Language filtering is now more intelligent, supporting multi-language models and providing a cleaner UI.
- **Demo Scenes**: All demo scenes now fetch the model list asynchronously and display a "Fetching..." message to the user, improving the initial user experience.

### Known Issues
- There is a problem with the official native library of sherpa-onnx on the iOS platform, which causes crashes. Waiting for subsequent updates to fix it.

## [0.1.1-exp.2] - 2025-09-21

### Added
- **SpokenLanguageIdentification Module** - Identifies the language from a list of candidates in a given audio clip.
  - Supports both streaming and batch processing.
  - Includes a demo scene for interactive testing.
- **Custom Keyword Support** - Added functionality to the `KeywordSpotting` module to support custom keywords.
  - Currently available for Chinese language models.

### Changed
- Updated sherpa-onnx to [v1.12.14](https://github.com/k2-fsa/sherpa-onnx/releases/tag/v1.12.14).

## [0.1.1-exp.1] - 2025-08-06

### Added
- **SpeechEnhancement Module** - Complete noise reduction system using GTCRN models
  - In-place audio processing for zero-GC design and optimal performance
  - Real-time streaming and batch processing support
  - Multiple processing methods: synchronous, asynchronous, and Span-based
  - Support for float arrays, Span<float>, and buffer segments
  - Thread-safe processing with proper resource management
  - GTCRN model integration with hash verification

- **KeywordSpotting Module** - Voice-activated keyword detection system
  - Event-driven keyword detection with `OnKeywordDetected` event
  - Stream-based processing with concurrent audio queue
  - Both streaming and batch detection methods
  - Thread-safe processing with ArrayPool optimization
  - Support for Chinese and English keyword models
  - Real-time audio processing with background thread management

- **Comprehensive Demo Applications**
  - **SpeechEnhancementExample**: Interactive demo with real-time enhancement
    - Model dropdown selection with automatic registry integration
    - Real-time recording with performance monitoring
    - Enhancement comparison toggle for A/B testing
    - Automatic playback after recording completion
    - UI state management with proper visibility controls
  - **KeywordSpottingExample**: Voice activation demo with keyword detection

- **Model Registry Enhancements**
  - Added GTCRN speech enhancement model constants with hash verification
  - Added keyword spotting model metadata tables for Chinese and English
  - Enhanced model type detection for new module types
  - Improved model utility functions for better identification

### Changed
- Updated sherpa-onnx to v1.12.7
- Simplified platform library dependencies, removing unsupported architectures for Unity
- Enhanced `SherpaONNXModuleType` enum with `KeywordSpotting` and `SpeechEnhancement` types
- Improved model download URL generation for new module types
- Enhanced `UnityLogger` with better error handling and disposal safety

### Technical Improvements
- **Performance Optimizations**
  - Internal bool variables for real-time audio processing instead of UI component access
  - Zero-allocation processing with in-place array modifications
  - Thread-safe concurrent processing with proper locking mechanisms
  - Optimized UI updates with conditional visibility management
- **Architecture Enhancements**
  - Extended model registry with proper module type filtering
  - Better error handling and resource management across all modules
  - Improved thread safety with concurrent audio processing
- **Code Quality**
  - Enhanced documentation with comprehensive XML comments
  - Better separation of concerns in UI and processing logic
  - Improved resource disposal patterns

## [0.1.0-exp.1] - 2025-07-28

### Added
- Initial release of SherpaONNXUnity package
- Offline speech recognition (ASR) functionality using sherpa-onnx
- Text-to-speech (TTS) synthesis capabilities
- Voice Activity Detection (VAD) module
- Speaker diarization support
- Audio enhancement features
- Cross-platform native library support:
  - Windows (x86, x64)
  - macOS (Intel, Apple Silicon)
  - Linux (x64, ARM64)
  - Android (ARM64, ARMv7, x86, x64)
- Automatic model management system with download and verification
- Unity integration components:
  - `SherpaONNXAnchor` main scene component
  - `SherpaONNXModule` base class system
  - `SpeechRecognition` module
  - `VoiceActivityDetection` module
- Sample collection with example scenes and scripts
- Assembly definitions for runtime, editor, tests, and samples
- Model registry system for automated model handling
- Real-time audio processing with low latency
- Batch processing capabilities for audio files
- Unity Test Framework integration
- Editor tools and extensions
- OpenUPM package registry support

### Technical Details
- Unity 2021.3 LTS minimum requirement
- Native sherpa-onnx library integration
- ONNX Runtime dependency
- Streaming audio processing pipeline
- Thread-safe audio buffer management
- Automatic memory management for models
- Hash-based model integrity verification
- StreamingAssets integration for model storage

### Documentation
- Comprehensive README with quick start guide
- Code examples for common use cases
- Architecture documentation
- Platform compatibility matrix
- Performance guidelines
- Troubleshooting section

### Known Issues
- iOS platform support is in development
- Large model files may require significant download time on slow connections
- Memory usage scales with model complexity
