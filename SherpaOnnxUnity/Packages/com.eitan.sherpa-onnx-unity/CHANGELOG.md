# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed
- Updated sherpa-onnx to v1.12.7.
- Simplified platform library dependencies, removing unsupported architectures for Unity.

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