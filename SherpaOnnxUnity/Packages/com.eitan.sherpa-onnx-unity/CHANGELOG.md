# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

## [Unreleased]

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
- Enhanced `SherpaOnnxModuleType` enum with `KeywordSpotting` and `SpeechEnhancement` types
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
- Initial release of SherpaOnnxUnity package
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
  - `SherpaOnnxAnchor` main scene component
  - `SherpaOnnxModule` base class system
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

---

## Release Notes Format

### Added
- New features and functionality

### Changed
- Changes in existing functionality

### Deprecated
- Soon-to-be removed features

### Removed
- Removed features

### Fixed
- Bug fixes

### Security
- Security vulnerability fixes