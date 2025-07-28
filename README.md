# SherpaOnnxUnity

A Unity package providing offline speech recognition (ASR), text-to-speech (TTS), and voice activity detection using [sherpa-onnx](https://github.com/k2-fsa/sherpa-onnx). Supports real-time audio processing, speaker diarization, audio enhancement, and cross-platform deployment.

## Features

- **Offline Speech Recognition**: Real-time and batch ASR processing without internet connectivity
- **Text-to-Speech**: High-quality offline TTS synthesis
- **Voice Activity Detection (VAD)**: Detect speech segments in audio streams
- **Speaker Diarization**: Identify and separate different speakers
- **Audio Enhancement**: Noise reduction and audio quality improvement
- **Cross-Platform Support**: Windows, macOS, Linux, Android, and iOS
- **Model Management**: Automatic model downloading, verification, and loading
- **Unity Integration**: Seamless integration with Unity's audio system

## Requirements

- Unity 2021.3 LTS or higher
- Supported platforms: Windows (x86, x64), macOS (Intel, Apple Silicon), Linux (x64, ARM64), Android (ARM64, ARMv7, x86, x64)

## Installation

### Via OpenUPM (Recommended)

1. Add the OpenUPM registry to your project:
   ```bash
   openupm add com.eitan.sherpa-onnx-unity
   ```

2. Or add via Unity Package Manager:
   - Open Unity Package Manager
   - Click the `+` button and select "Add package from git URL"
   - Enter: `https://github.com/EitanWong/com.eitan.sherpa-onnx-unity.git#upm`

### Via Git URL

Add this line to your `Packages/manifest.json`:
```json
{
  "dependencies": {
    "com.eitan.sherpa-onnx-unity": "https://github.com/EitanWong/com.eitan.sherpa-onnx-unity.git#upm"
  }
}
```

### Model Configuration

Models are automatically managed through the `SherpaOnnxModelRegistry`. Supported model types:

- **ASR Models**: Whisper, Zipformer, Paraformer, and more
- **VAD Models**: Silero VAD and similar

## Architecture

### Core Components

- **SherpaOnnxModule**: Base class for all functionality modules
- **SpeechRecognition**: Handles ASR operations
- **VoiceActivityDetection**: VAD functionality

### Assembly Structure

- `Eitan.SherpaOnnxUnity` - Main runtime assembly
- `Eitan.SherpaOnnxUnity.Editor` - Editor tools and extensions
- `Eitan.SherpaOnnxUnity.Tests` - Unit and integration tests
- `Eitan.SherpaOnnxUnity.Samples` - Example implementations

## Platform Support

| Platform | Architecture | Status |
|----------|-------------|---------|
| Windows  | x86, x64    | ✅ Supported |
| macOS    | Intel, Apple Silicon | ✅ Supported |
| Linux    | x64, ARM64  | ✅ Supported |
| Android  | ARM64, ARMv7, x86, x64 | ✅ Supported |
| iOS      | ARM64       | 🚧 In Development |

## Performance Considerations

- **Memory Usage**: Models typically require 100-500MB RAM
- **CPU Usage**: Optimized for real-time processing on modern hardware
- **Storage**: Models range from 50MB to 1GB depending on complexity
- **Latency**: Real-time recognition with <100ms latency on supported hardware

## Troubleshooting

### Common Issues

1. **Model Download Fails**: Check internet connectivity and firewall settings
2. **Audio Not Recognized**: Verify microphone permissions and audio format
3. **Performance Issues**: Ensure adequate RAM and CPU resources
4. **Platform Errors**: Verify native library compatibility

### Debug Logging

Enable detailed logging in the `SherpaOnnxAnchor` component for debugging.

## Contributing

This package is based on the [sherpa-onnx](https://github.com/k2-fsa/sherpa-onnx) library. For issues specific to the Unity integration, please use this repository's issue tracker.

## License

This package is licensed under the MIT License. See [LICENSE.md](LICENSE.md) for details.

## Third-Party Software

This package includes native libraries from sherpa-onnx and ONNX Runtime. See [Third Party Notices.md](Third%20Party%20Notices.md) for complete licensing information.

## Support

- **Documentation**: [Package Documentation](https://github.com/EitanWong/com.eitan.sherpa-onnx-unity#documentation)
- **Issues**: [GitHub Issues](https://github.com/EitanWong/com.eitan.sherpa-onnx-unity/issues)
- **Samples**: Import the sample collection from Package Manager

---

**Keywords**: Unity, Speech Recognition, ASR, TTS, Voice Activity Detection, ONNX, Machine Learning, Real-time Audio, Cross-platform