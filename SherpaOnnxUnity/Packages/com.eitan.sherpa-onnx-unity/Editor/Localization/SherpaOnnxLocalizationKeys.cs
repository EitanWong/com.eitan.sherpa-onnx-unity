#if UNITY_EDITOR

namespace Eitan.SherpaONNXUnity.Editor.Localization
{
    /// <summary>
    /// Centralized string keys so we can avoid inline literals throughout the editors.
    /// </summary>
    internal static class SherpaONNXL10n
    {
        internal static class Common
        {
            internal const string FilterAll = "common.filter.all";
            internal const string LabelCategory = "common.label.category";
            internal const string LabelLanguage = "common.label.language";
            internal const string ButtonClear = "common.button.clear";
            internal const string ButtonRefresh = "common.button.refresh";
            internal const string ButtonRescan = "common.button.rescan";
            internal const string ButtonCancel = "common.button.cancel";
            internal const string TooltipClearFilters = "common.tooltip.clearFilters";
            internal const string TooltipRefresh = "common.tooltip.refresh";
            internal const string TooltipRescan = "common.tooltip.rescan";
            internal const string TooltipEntryRescan = "common.tooltip.entryRescan";
            internal const string TooltipReveal = "common.tooltip.reveal";
            internal const string LanguageOther = "common.language.other";
            internal const string LanguageChinese = "common.language.chinese";
            internal const string LanguageCantonese = "common.language.cantonese";
            internal const string LanguageEnglish = "common.language.english";
            internal const string LanguageJapanese = "common.language.japanese";
            internal const string LanguageKorean = "common.language.korean";
            internal const string LanguageThai = "common.language.thai";
            internal const string LanguageVietnamese = "common.language.vietnamese";
            internal const string LanguageRussian = "common.language.russian";
            internal const string LanguageFrench = "common.language.french";
            internal const string LanguageSpanish = "common.language.spanish";
            internal const string LanguageGerman = "common.language.german";
            internal const string LanguageDutch = "common.language.dutch";
            internal const string LanguageDanish = "common.language.danish";
            internal const string LanguageCzech = "common.language.czech";
            internal const string LanguageCatalan = "common.language.catalan";
            internal const string LanguageArabic = "common.language.arabic";
            internal const string LanguageItalian = "common.language.italian";
            internal const string LanguagePortuguese = "common.language.portuguese";
            internal const string LanguageTurkish = "common.language.turkish";
            internal const string LanguagePolish = "common.language.polish";
            internal const string LanguageSwedish = "common.language.swedish";
            internal const string LanguageNorwegian = "common.language.norwegian";
            internal const string LanguageIndonesian = "common.language.indonesian";
            internal const string LanguageMalay = "common.language.malay";
            internal const string LanguageHindi = "common.language.hindi";
            internal const string LanguageUrdu = "common.language.urdu";
            internal const string LanguagePersian = "common.language.persian";
            internal const string LanguageHebrew = "common.language.hebrew";
        }

        internal static class Settings
        {
            internal const string BuildTitle = "settings.build.title";
            internal const string IncludeModelsLabel = "settings.includeModels.label";
            internal const string IncludeModelsTooltip = "settings.includeModels.tooltip";
            internal const string IncludeModelsHelp = "settings.includeModels.help";
            internal const string RuntimeDefaultsTitle = "settings.runtimeDefaults.title";
            internal const string LanguageLabel = "settings.language.label";
            internal const string LanguageTooltip = "settings.language.tooltip";
            internal const string RuntimeHelp = "settings.runtimeDefaults.help";
            internal const string RuntimeHelpMissing = "settings.runtimeDefaults.helpMissing";

            internal const string FetchLatestLabel = "settings.fetchLatest.label";
            internal const string FetchLatestTooltip = "settings.fetchLatest.tooltip";
            internal const string AutoDownloadLabel = "settings.autoDownload.label";
            internal const string AutoDownloadTooltip = "settings.autoDownload.tooltip";
            internal const string CacheDirectoryLabel = "settings.cacheDirectory.label";
            internal const string CacheDirectoryTooltip = "settings.cacheDirectory.tooltip";
            internal const string CacheTtlLabel = "settings.cacheTtl.label";
            internal const string CacheTtlTooltip = "settings.cacheTtl.tooltip";
            internal const string CacheClearButton = "settings.cacheClear.button";
            internal const string CacheClearTooltip = "settings.cacheClear.tooltip";
            internal const string CacheClearSuccess = "settings.cacheClear.success";
            internal const string CacheClearEmpty = "settings.cacheClear.empty";
            internal const string CacheClearError = "settings.cacheClear.error";
        }

        internal static class KeywordDrawer
        {
            internal const string BoostLabel = "keyword.boost.label";
            internal const string BoostTooltip = "keyword.boost.tooltip";
            internal const string ThresholdLabel = "keyword.threshold.label";
            internal const string ThresholdTooltip = "keyword.threshold.tooltip";
            internal const string ErrorMissingFields = "keyword.error.missingFields";
        }

        internal static class Models
        {
            internal const string WindowTitle = "models.window.title";
            internal const string Header = "models.header";
            internal const string LoadingRemote = "models.loading.remote";
            internal const string LoadingGeneric = "models.loading.generic";
            internal const string NotificationManifestLoadFailed = "models.notification.manifestLoadFailed";
            internal const string NotificationManifestReloadFailed = "models.notification.manifestReloadFailed";
            internal const string ToolbarMore = "models.toolbar.more";
            internal const string FiltersEmpty = "models.filters.none";
            internal const string NotificationCopiedName = "models.notification.copiedName";
            internal const string NotificationCopiedUrl = "models.notification.copiedUrl";
            internal const string NotificationNoUrl = "models.notification.noUrl";
            internal const string NotificationDeleted = "models.notification.deleted";
            internal const string NotificationDeleteFailed = "models.notification.deleteFailed";
            internal const string NotificationDownloadFailed = "models.notification.downloadFailed";
            internal const string NotificationCanceled = "models.notification.canceled";

            internal const string LabelLanguageList = "models.label.languageList";
            internal const string LabelCategoryPrefix = "models.label.categoryPrefix";
            internal const string LabelLanguagePrefix = "models.label.languagePrefix";
            internal const string LabelUrlPrefix = "models.label.urlPrefix";
            internal const string LabelActiveDownloads = "models.label.activeDownloads";

            internal const string ButtonCopyName = "models.button.copyName";
            internal const string TooltipCopyName = "models.tooltip.copyName";
            internal const string ButtonCopyUrl = "models.button.copyUrl";
            internal const string TooltipCopyUrl = "models.tooltip.copyUrl";
            internal const string ButtonReveal = "models.button.reveal";
            internal const string ButtonDownload = "models.button.download";
            internal const string ButtonRedownload = "models.button.redownload";
            internal const string TooltipDownload = "models.tooltip.download";
            internal const string TooltipRedownload = "models.tooltip.redownload";
            internal const string TooltipNoUrl = "models.tooltip.noUrl";
            internal const string ButtonDelete = "models.button.delete";
            internal const string TooltipDelete = "models.tooltip.delete";
            internal const string DialogDeleteTitle = "models.dialog.delete.title";
            internal const string DialogDeleteMessage = "models.dialog.delete.message";
            internal const string DialogDeleteConfirm = "models.dialog.delete.confirm";
            internal const string DialogDeleteCancel = "models.dialog.delete.cancel";
            internal const string CategoryUndefined = "models.category.undefined";
            internal const string CategorySpeechRecognition = "models.category.speechRecognition";
            internal const string CategorySpeechSynthesis = "models.category.speechSynthesis";
            internal const string CategorySourceSeparation = "models.category.sourceSeparation";
            internal const string CategorySpeakerIdentification = "models.category.speakerIdentification";
            internal const string CategorySpeakerDiarization = "models.category.speakerDiarization";
            internal const string CategorySpokenLanguageId = "models.category.spokenLanguageIdentification";
            internal const string CategoryAudioTagging = "models.category.audioTagging";
            internal const string CategoryVad = "models.category.vad";
            internal const string CategoryKeywordSpotting = "models.category.keywordSpotting";
            internal const string CategoryAddPunctuation = "models.category.addPunctuation";
            internal const string CategorySpeechEnhancement = "models.category.speechEnhancement";

            internal const string HelpNoMatches = "models.help.noMatches";

            internal const string StatusWorking = "models.status.working";
            internal const string StatusCancelByUser = "models.status.cancelByUser";
            internal const string StatusWindowClosed = "models.status.windowClosed";
            internal const string StatusCanceled = "models.status.canceled";
            internal const string StatusVerifyFailed = "models.status.verifyFailed";
            internal const string StatusChecking = "models.status.checking";
            internal const string StatusDownloaded = "models.status.downloaded";
            internal const string StatusNotDownloaded = "models.status.notDownloaded";
            internal const string StatusDownloadPhase = "models.status.phase.download";
            internal const string StatusInstallPhase = "models.status.phase.install";
            internal const string StatusVerifyPhase = "models.status.phase.verify";
            internal const string StatusStarting = "models.status.starting";
            internal const string StatusPreparingInstall = "models.status.preparingInstall";
            internal const string StatusExtracting = "models.status.extracting";
            internal const string StatusExtractionFailed = "models.status.extractionFailed";
            internal const string StatusInstallSkipped = "models.status.installSkipped";
            internal const string StatusInstallError = "models.status.installError";
            internal const string StatusInstallFailed = "models.status.installFailed";
            internal const string StatusVerifying = "models.status.verifying";
            internal const string StatusCompleted = "models.status.completed";
            internal const string StatusVerifyFailedMessage = "models.status.verifyFailedMessage";
            internal const string StatusVerifyFailedRetry = "models.status.verifyFailedRetry";
            internal const string StatusVerifyFailedRetryMessage = "models.status.verifyFailedRetryMessage";
            internal const string StatusDownloadFailed = "models.status.downloadFailed";
            internal const string StatusDownloadProgress = "models.status.downloadProgress";
            internal const string StatusSaveError = "models.status.saveError";
            internal const string StatusGenericError = "models.status.genericError";
            internal const string StatusUnknown = "models.status.unknown";
            internal const string StatusDownloadedWithCheck = "models.status.downloadedWithCheck";
        }

        internal static class Inspectors
        {
            internal static class Common
            {
                internal const string SectionModelSettings = "inspectors.common.section.modelSettings";
                internal const string SectionEvents = "inspectors.common.section.events";
                internal const string SectionLifecycleEvents = "inspectors.common.section.lifecycleEvents";
                internal const string SectionAudioInput = "inspectors.common.section.audioInput";
                internal const string FieldModelId = "inspectors.common.field.modelId";
                internal const string FieldSampleRate = "inspectors.common.field.sampleRate";
                internal const string FieldLoadOnAwake = "inspectors.common.field.loadOnAwake";
                internal const string FieldDisposeOnDestroy = "inspectors.common.field.disposeOnDestroy";
                internal const string FieldLogFeedback = "inspectors.common.field.logFeedback";
                internal const string FieldInputSource = "inspectors.common.field.inputSource";
                internal const string FieldAutoBind = "inspectors.common.field.autoBind";
                internal const string FieldDeduplicate = "inspectors.common.field.deduplicate";
                internal const string EventInitialized = "inspectors.common.event.initialized";
                internal const string EventFeedback = "inspectors.common.event.feedback";
                internal const string ButtonSelectInput = "inspectors.common.button.selectInput";
                internal const string HelpAssignInput = "inspectors.common.help.assignInput";
                internal const string HelpInputLivesOnSource = "inspectors.common.help.inputLivesOnSource";
                internal const string WarningSampleRateRange = "inspectors.common.warning.sampleRateRange";
                internal const string HelpSampleRateIgnored = "inspectors.common.help.sampleRateIgnored";
                internal const string HelpPlaymodeRequired = "inspectors.common.help.playmodeRequired";
            }

            internal static class ModelSelector
            {
                internal const string ButtonPick = "inspectors.modelSelector.button.pick";
                internal const string ButtonPickCount = "inspectors.modelSelector.button.pickCount";
                internal const string ButtonFetching = "inspectors.modelSelector.button.fetching";
                internal const string StatusLoading = "inspectors.modelSelector.status.loading";
                internal const string StatusError = "inspectors.modelSelector.status.error";
                internal const string StatusEmpty = "inspectors.modelSelector.status.empty";
                internal const string MenuClear = "inspectors.modelSelector.menu.clear";
                internal const string TooltipRefresh = "inspectors.modelSelector.tooltip.refresh";
            }

            internal static class SpeechRecognizer
            {
                internal const string EventTranscriptionReady = "inspectors.speechRecognizer.event.transcriptionReady";
            }

            internal static class SpeechEnhancer
            {
                internal const string SectionEnhancement = "inspectors.speechEnhancer.section.enhancement";
                internal const string FieldTargetAudioSource = "inspectors.speechEnhancer.field.targetAudioSource";
                internal const string FieldClipReference = "inspectors.speechEnhancer.field.clipReference";
                internal const string FieldEnhanceOnEnable = "inspectors.speechEnhancer.field.enhanceOnEnable";
                internal const string FieldDuplicateClip = "inspectors.speechEnhancer.field.duplicateClip";
                internal const string HelpAssignClip = "inspectors.speechEnhancer.help.assignClip";
                internal const string ButtonEnhanceNow = "inspectors.speechEnhancer.button.enhanceNow";
                internal const string EventClipEnhanced = "inspectors.speechEnhancer.event.clipEnhanced";
                internal const string EventEnhancementFailed = "inspectors.speechEnhancer.event.enhancementFailed";
            }

            internal static class SpeechSynthesizer
            {
                internal const string SectionSynthesis = "inspectors.speechSynth.section.synthesis";
                internal const string SectionPreview = "inspectors.speechSynth.section.preview";
                internal const string FieldOutputAudioSource = "inspectors.speechSynth.field.outputAudioSource";
                internal const string FieldAutoplay = "inspectors.speechSynth.field.autoplay";
                internal const string FieldVoiceId = "inspectors.speechSynth.field.voiceId";
                internal const string FieldSpeechRate = "inspectors.speechSynth.field.speechRate";
                internal const string ButtonSynthesizePreview = "inspectors.speechSynth.button.synthesizePreview";
                internal const string EventStarted = "inspectors.speechSynth.event.started";
                internal const string EventClipReady = "inspectors.speechSynth.event.clipReady";
                internal const string EventFailed = "inspectors.speechSynth.event.failed";
                internal const string PreviewDefaultText = "inspectors.speechSynth.preview.defaultText";
            }

            internal static class Microphone
            {
                internal const string SectionCapture = "inspectors.microphone.section.capture";
                internal const string FieldAutoStart = "inspectors.microphone.field.autoStart";
                internal const string FieldSampleRate = "inspectors.microphone.field.sampleRate";
                internal const string FieldChunkDuration = "inspectors.microphone.field.chunkDuration";
                internal const string FieldBufferLength = "inspectors.microphone.field.bufferLength";
                internal const string FieldDownmix = "inspectors.microphone.field.downmix";
                internal const string FieldDevice = "inspectors.microphone.field.device";
                internal const string HelpNoDevices = "inspectors.microphone.help.noDevices";
                internal const string ButtonStart = "inspectors.microphone.button.start";
                internal const string ButtonStop = "inspectors.microphone.button.stop";
                internal const string EventChunkReady = "inspectors.microphone.event.chunkReady";
                internal const string EventRecordingState = "inspectors.microphone.event.recordingState";
            }

            internal static class VoiceActivityDetection
            {
                internal const string SectionDetector = "inspectors.vad.section.detector";
                internal const string FieldThreshold = "inspectors.vad.field.threshold";
                internal const string FieldMinSilence = "inspectors.vad.field.minSilence";
                internal const string FieldMinSpeech = "inspectors.vad.field.minSpeech";
                internal const string FieldMaxSpeech = "inspectors.vad.field.maxSpeech";
                internal const string FieldLeadingPadding = "inspectors.vad.field.leadingPadding";
                internal const string ButtonFlush = "inspectors.vad.button.flush";
                internal const string EventSegment = "inspectors.vad.event.segment";
                internal const string EventSpeaking = "inspectors.vad.event.speaking";
                internal const string WarningSampleRateMismatch = "inspectors.vad.warning.sampleRateMismatch";
            }

            internal static class OfflineAsr
            {
                internal const string SectionVad = "inspectors.offlineAsr.section.vad";
                internal const string FieldVadSource = "inspectors.offlineAsr.field.vadSource";
                internal const string HelpAssignVad = "inspectors.offlineAsr.help.assignVad";
                internal const string ButtonSelectVad = "inspectors.offlineAsr.button.selectVad";
                internal const string EventTranscriptReady = "inspectors.offlineAsr.event.transcriptReady";
                internal const string EventFailed = "inspectors.offlineAsr.event.failed";
            }

            internal static class KeywordSpotting
            {
                internal const string SectionKeywords = "inspectors.keyword.section.keywords";
                internal const string FieldScore = "inspectors.keyword.field.score";
                internal const string FieldThreshold = "inspectors.keyword.field.threshold";
                internal const string FieldCustomKeywords = "inspectors.keyword.field.custom";
                internal const string EventDetected = "inspectors.keyword.event.detected";
            }

            internal static class Punctuation
            {
                internal const string SectionPreview = "inspectors.punctuation.section.preview";
                internal const string FieldInputText = "inspectors.punctuation.field.inputText";
                internal const string ButtonRun = "inspectors.punctuation.button.run";
                internal const string EventReady = "inspectors.punctuation.event.ready";
                internal const string EventFailed = "inspectors.punctuation.event.failed";
            }

            internal static class SpokenLanguageIdentification
            {
                internal const string SectionClip = "inspectors.sli.section.clip";
                internal const string FieldClip = "inspectors.sli.field.clip";
                internal const string FieldIdentifyOnStart = "inspectors.sli.field.identifyOnStart";
                internal const string ButtonIdentify = "inspectors.sli.button.identify";
                internal const string EventIdentified = "inspectors.sli.event.identified";
                internal const string EventFailed = "inspectors.sli.event.failed";
            }
        }
    }
}

#endif
