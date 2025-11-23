namespace Eitan.SherpaONNXUnity.Runtime
{
    public enum SherpaONNXModuleType
    {

        Undefined,
        // 语音识别 | Automatic Speech Recognition (ASR)
        // 效果：将语音转换为文字（音频 → 文本）
        SpeechRecognition,

        // 语音合成 | Speech Synthesis (Text-to-Speech, TTS)
        // 效果：将文字转换为自然语音（文本 → 音频）
        SpeechSynthesis,

        // 源分离 | Source Separation
        // 效果：分离混合音频中的不同音源（如人声/背景音乐分离）
        SourceSeparation,

        // 说话人识别 | Speaker Identification
        // 效果：识别音频中的说话人身份（从已知说话人库中匹配）
        SpeakerIdentification,

        // 说话人日志 | Speaker Diarization
        // 效果：标记音频中"谁在什么时候说话"（分段标注说话人）
        SpeakerDiarization,

        // 口语语言识别 | Spoken Language Identification
        // 效果：识别语音使用的语言种类（如中/英/法语识别）
        SpokenLanguageIdentification,

        // 音频标记 | Audio Tagging
        // 效果：为音频添加语义标签（如"婴儿哭声"、"玻璃破碎声"）
        AudioTagging,

        // 语音活动检测 | Voice Activity Detection (VAD)
        // 效果：检测音频中是否包含人声（区分语音与静音段）
        VoiceActivityDetection,

        // 关键词唤醒 | Keyword Spotting (KWS)
        // 效果：实时检测特定关键词（如"Hey Siri"）
        KeywordSpotting,

        // 添加标点 | Add Punctuation
        // 效果：为无标点文本添加标点符号（文本后处理）
        AddPunctuation,

        // 语音增强 | Speech Enhancement
        // 效果：提升语音质量（降噪/去混响/清晰化处理）
        SpeechEnhancement,

    }
}
