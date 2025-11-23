/// Copyright (c)  2023  Xiaomi Corporation (authors: Fangjun Kuang)
/// Copyright (c)  2023 by manyeyes
/// Copyright (c)  2024.5 by 东风破

namespace Eitan.SherpaONNXUnity.Runtime.Native
{
        internal static class Dll
        {
#if (UNITY_IOS || UNITY_TVOS || UNITY_WEBGL) && !UNITY_EDITOR
                 public const string Filename = "__Internal"; // iOS/tvOS/WebGL
#else
                public const string Filename = "sherpa-onnx-c-api"; // Android, macOS, Linux, etc.
#endif
        }
}
