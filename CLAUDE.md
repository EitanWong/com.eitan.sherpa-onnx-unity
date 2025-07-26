# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

SherpaOnnxUnity is a Unity package that provides offline speech recognition (ASR) and related functionality using sherpa-onnx. The project is structured as a Unity Package Manager (UPM) package with native library bindings for cross-platform support.

## Architecture

### Package Structure
- **Main Package**: `/Packages/com.eitan.sherpa-onnx-unity/` - Core package containing runtime, editor, and test code
- **Runtime**: Core speech recognition and voice activity detection modules
- **Editor**: Unity editor extensions and tools
- **Plugins**: Native libraries for Windows, macOS, Linux, and Android platforms
- **Samples**: Example scenes and scripts demonstrating package usage
- **Tests**: Unit and integration tests

### Key Components
- **SherpaOnnxModule**: Abstract base class for all sherpa-onnx functionality
- **SpeechRecognition**: Handles offline and real-time speech recognition
- **VoiceActivityDetection**: Voice activity detection capabilities
- **SherpaOnnxAnchor**: Main integration point for Unity scenes
- **Model Management**: Automatic downloading, verification, and loading of ML models

### Assembly Definitions
- `Eitan.SherpaOnnxUnity` - Main runtime assembly
- `Eitan.SherpaOnnxUnity.Editor` - Editor-only functionality
- `Eitan.SherpaOnnxUnity.Tests` - Test assembly
- `Eitan.SherpaOnnxUnity.Samples` - Sample code assembly
- `Eitan.SherpaOnnxUnity.Demo` - Demo scene assembly

## Development Commands

### Building
Unity projects are typically built through the Unity Editor, but you can also use:
- **Solution Build**: Open `SherpaOnnxUnity.sln` in Visual Studio or use `dotnet build`
- **Unity Command Line**: Use Unity's command line interface for automated builds

### Testing
- **Unity Test Runner**: Run tests through Unity Editor (Window → General → Test Runner)
- **Command Line**: Tests can be run via Unity's command line batch mode

### Package Management
- **OpenUPM**: Package is published to OpenUPM registry
- **Local Development**: Use local package reference in `Packages/manifest.json`

## Important File Locations

### Configuration
- `Packages/com.eitan.sherpa-onnx-unity/package.json` - Package manifest
- `Packages/manifest.json` - Project package dependencies
- `ProjectSettings/` - Unity project settings

### Core Runtime
- `Packages/com.eitan.sherpa-onnx-unity/Runtime/Modules/` - Main speech recognition modules
- `Packages/com.eitan.sherpa-onnx-unity/Runtime/Core/` - Core abstractions and interfaces
- `Packages/com.eitan.sherpa-onnx-unity/Runtime/Utilities/` - Utility classes for file handling, downloads, etc.

### Native Libraries
- `Packages/com.eitan.sherpa-onnx-unity/Plugins/` - Platform-specific native libraries
- Platform folders: Windows/, macOS/, Linux/, Android/ with architecture subfolders

### Examples and Samples
- `Packages/com.eitan.sherpa-onnx-unity/Samples~/Collection/` - Sample scenes and scripts
- `Assets/Demo/` - Demo project assets

## Development Workflow

1. **Package Development**: Make changes in `Packages/com.eitan.sherpa-onnx-unity/`
2. **Testing**: Use Unity Test Runner for both edit-mode and play-mode tests
3. **Demo Testing**: Use scenes in `Assets/Demo/` to test functionality
4. **Native Library Updates**: Update platform-specific libraries in `Plugins/` folders when needed

## Key Dependencies

- **Unity 2021.3 LTS** or higher
- **sherpa-onnx native libraries** - Provided in Plugins folders
- **OpenUPM packages**: com.utilities.buildpipeline
- **Unity packages**: Test Framework, UI System

## Model Management

The package includes automatic model downloading and management:
- Models are downloaded to `StreamingAssets/sherpa-onnx/`
- Model metadata is managed through `SherpaOnnxModelRegistry`
- Hash verification ensures model integrity
- Models support offline speech recognition and voice activity detection