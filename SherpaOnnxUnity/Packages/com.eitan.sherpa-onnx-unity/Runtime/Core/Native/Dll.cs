/// Copyright (c)  2023  Xiaomi Corporation (authors: Fangjun Kuang)
/// Copyright (c)  2023 by manyeyes
/// Copyright (c)  2024.5 by 东风破

namespace Eitan.SherpaOnnxUnity.Runtime.Native
{
    internal static class Dll
    {
        // Unity resolves the native library name differently per-platform.
#if (UNITY_IOS || UNITY_TVOS || UNITY_WEBGL) && !UNITY_EDITOR
        public const string Filename = "__Internal";
#elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        public const string Filename = "libsherpa-onnx-c-api";
#elif UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
        public const string Filename = "libsherpa-onnx-c-api";
#else
        public const string Filename = "sherpa-onnx-c-api";
#endif
    }
}
