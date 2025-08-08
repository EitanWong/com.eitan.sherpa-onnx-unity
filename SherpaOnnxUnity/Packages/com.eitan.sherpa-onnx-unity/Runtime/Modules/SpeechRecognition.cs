// SpeechRecognition.cs (Refactored and Optimized)

namespace Eitan.SherpaOnnxUnity.Runtime
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Eitan.SherpaOnnxUnity.Runtime.Utilities;
    using SherpaOnnx;


    public class SpeechRecognition : SherpaOnnxModule
    {
        private OnlineRecognizer _onlineRecognizer;
        private OnlineStream _onlineStream;
        private OfflineRecognizer _offlineRecognizer;
        
        private SpeechRecognitionModelType _modelType;
        private readonly object _lockObject = new object();
        private bool _isOnlineModel;

        protected override SherpaOnnxModuleType ModuleType => SherpaOnnxModuleType.SpeechRecognition;

        public SpeechRecognition(string modelID, int sampleRate = 16000, SherpaOnnxFeedbackReporter reporter = null)
            : base(modelID, sampleRate, reporter)
        {
        }

        protected override async Task Initialization(SherpaOnnxModelMetadata metadata, int sampleRate, bool isMobilePlatform, SherpaOnnxFeedbackReporter reporter, CancellationToken ct)
        {
            try
            {
                reporter?.Report(new LoadFeedback(metadata, message: $"Start Loading: {metadata.modelId}"));
                
                _isOnlineModel = SherpaUtils.Model.IsOnlineModel(metadata.modelId);
                _modelType = SherpaUtils.Model.GetSpeechRecognitionModelType(metadata.modelId);

                if (_isOnlineModel)
                {
                    await LoadOnlineModelAsync(metadata, sampleRate, isMobilePlatform, reporter, ct);
                }
                else
                {
                    await LoadOfflineModelAsync(metadata, sampleRate, isMobilePlatform, reporter, ct);
                }
            }
            catch (Exception ex)
            {
                reporter?.Report(new FailedFeedback(metadata, ex.Message, exception: ex));
                throw;
            }
        }

        private async Task LoadOnlineModelAsync(SherpaOnnxModelMetadata metadata, int sampleRate, bool isMobilePlatform, SherpaOnnxFeedbackReporter reporter, CancellationToken ct)
        {
            var config = CreateOnlineRecognizerConfig(metadata, sampleRate, isMobilePlatform);
            
            await runner.RunAsync(cancellationToken =>
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, cancellationToken);
                linkedCts.Token.ThrowIfCancellationRequested();

                if (IsDisposed) { return; }

                _onlineRecognizer = new OnlineRecognizer(config);
                _onlineStream = _onlineRecognizer.CreateStream();
                reporter?.Report(new LoadFeedback(metadata, message: $"Loaded online model: {metadata.modelId}"));
            });
        }

        private async Task LoadOfflineModelAsync(SherpaOnnxModelMetadata metadata, int sampleRate, bool isMobilePlatform, SherpaOnnxFeedbackReporter reporter, CancellationToken ct)
        {
            var config = CreateOfflineRecognizerConfig(metadata, sampleRate, isMobilePlatform);
            
            await runner.RunAsync(cancellationToken =>
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, cancellationToken);
                linkedCts.Token.ThrowIfCancellationRequested();

                if (IsDisposed) { return; }

                _offlineRecognizer = new OfflineRecognizer(config);
                reporter?.Report(new LoadFeedback(metadata, message: $"Loaded offline model: {metadata.modelId}"));
            });
        }

        private OnlineRecognizerConfig CreateOnlineRecognizerConfig(SherpaOnnxModelMetadata metadata, int sampleRate, bool isMobilePlatform)
        {
            var config = new OnlineRecognizerConfig
            {
                FeatConfig = { SampleRate = sampleRate, FeatureDim = 80 },
                ModelConfig = { 
                    Tokens = metadata.GetModelFilePathByKeywords("tokens").First(),
                    Provider = "cpu",
                    NumThreads = 1,
                    Debug = 0
                },
                MaxActivePaths = 4,
                EnableEndpoint = 1,
                Rule1MinTrailingSilence = 2.7f,
                Rule2MinTrailingSilence = 1.2f,
                Rule3MinUtteranceLength = 20f
            };

            var int8QuantKeywords = isMobilePlatform ? "int8" : null;
            
            switch (_modelType)
            {
                case SpeechRecognitionModelType.Online_Paraformer:
                    config.DecodingMethod = "greedy_search";
                    config.ModelConfig.Paraformer.Encoder = metadata.GetModelFilePathByKeywords("encoder", int8QuantKeywords)?.First();
                    config.ModelConfig.Paraformer.Decoder = metadata.GetModelFilePathByKeywords("decoder", int8QuantKeywords)?.First();
                    break;
                    
                case SpeechRecognitionModelType.Online_Transducer:
                    config.DecodingMethod = isMobilePlatform ? "greedy_search" : "modified_beam_search";
                    config.ModelConfig.Transducer.Encoder = metadata.GetModelFilePathByKeywords("encoder", int8QuantKeywords)?.First();
                    config.ModelConfig.Transducer.Decoder = metadata.GetModelFilePathByKeywords("decoder", int8QuantKeywords)?.First();
                    config.ModelConfig.Transducer.Joiner = metadata.GetModelFilePathByKeywords("joiner", int8QuantKeywords)?.First();
                    break;
                    
                default:
                    throw new NotSupportedException($"Unsupported online model type: {_modelType}");
            }

            return config;
        }

        private OfflineRecognizerConfig CreateOfflineRecognizerConfig(SherpaOnnxModelMetadata metadata, int sampleRate, bool isMobilePlatform)
        {
            var config = new OfflineRecognizerConfig
            {
                FeatConfig = { SampleRate = sampleRate, FeatureDim = 80 },
                ModelConfig = { 
                    Tokens = metadata.GetModelFilePathByKeywords("tokens").First(),
                    Debug = 0,
                    ModelType = string.Empty
                },
                DecodingMethod = "greedy_search",
                MaxActivePaths = 4,
                HotwordsScore = 1.5f,
                RuleFsts = string.Empty
            };

            var int8QuantKeywords = isMobilePlatform ? "int8" : null;
            var hotwordsFile = metadata.GetModelFilePathByKeywords("hotwords", int8QuantKeywords).First();
            if (!string.IsNullOrEmpty(hotwordsFile))
            {
                config.HotwordsFile = hotwordsFile;
            }

            
            switch (_modelType)
            {
                case SpeechRecognitionModelType.Offline_Transducer:
                    config.ModelConfig.Transducer.Encoder = metadata.GetModelFilePathByKeywords("encoder", int8QuantKeywords).First();
                    config.ModelConfig.Transducer.Decoder = metadata.GetModelFilePathByKeywords("decoder", int8QuantKeywords).First();
                    config.ModelConfig.Transducer.Joiner = metadata.GetModelFilePathByKeywords("joiner", int8QuantKeywords).First();
                    break;

                case SpeechRecognitionModelType.Offline_Paraformer:
                    config.ModelConfig.Paraformer.Model = metadata.GetModelFilePathByKeywords("model", int8QuantKeywords).First();
                    break;

                case SpeechRecognitionModelType.Offline_ZipformerCtc:
                    config.ModelConfig.ZipformerCtc.Model = metadata.GetModelFilePathByKeywords("model", int8QuantKeywords).First();
                    break;

                case SpeechRecognitionModelType.Offline_Nemo_Ctc:
                    config.ModelConfig.NeMoCtc.Model = metadata.GetModelFilePathByKeywords("model", int8QuantKeywords).First();
                    break;

                case SpeechRecognitionModelType.Dolphin:
                    config.ModelConfig.Dolphin.Model = metadata.GetModelFilePathByKeywords("model", int8QuantKeywords).First();
                    break;

                case SpeechRecognitionModelType.TeleSpeech:
                    config.ModelConfig.TeleSpeechCtc = metadata.GetModelFilePathByKeywords("model", int8QuantKeywords).First();
                    break;

                case SpeechRecognitionModelType.Whisper:
                    config.ModelConfig.Whisper.Encoder = metadata.GetModelFilePathByKeywords("encoder", int8QuantKeywords).First();
                    config.ModelConfig.Whisper.Decoder = metadata.GetModelFilePathByKeywords("decoder", int8QuantKeywords).First();
                    config.ModelConfig.Whisper.Language = string.Empty;
                    config.ModelConfig.Whisper.Task = "transcribe";
                    break;

                case SpeechRecognitionModelType.Tdnn:
                    config.ModelConfig.Tdnn.Model = metadata.GetModelFilePathByKeywords("tdnn", int8QuantKeywords).First();
                    break;

                case SpeechRecognitionModelType.SenseVoice:
                    config.ModelConfig.SenseVoice.Model = metadata.GetModelFilePathByKeywords("model", int8QuantKeywords).First();
                    config.ModelConfig.SenseVoice.UseInverseTextNormalization = 1;
                    break;

                case SpeechRecognitionModelType.Moonshine:
                    config.ModelConfig.Moonshine.Preprocessor = metadata.GetModelFilePathByKeywords("preprocess", int8QuantKeywords).First();
                    config.ModelConfig.Moonshine.Encoder = metadata.GetModelFilePathByKeywords("encode", int8QuantKeywords).First();
                    config.ModelConfig.Moonshine.UncachedDecoder = metadata.GetModelFilePathByKeywords("uncached_decode", int8QuantKeywords).First();
                    config.ModelConfig.Moonshine.CachedDecoder = metadata.GetModelFilePathByKeywords("cached_decode", int8QuantKeywords).First();
                    break;

                case SpeechRecognitionModelType.FireRedAsr:
                    config.ModelConfig.FireRedAsr.Encoder = metadata.GetModelFilePathByKeywords("encoder", int8QuantKeywords).First();
                    config.ModelConfig.FireRedAsr.Decoder = metadata.GetModelFilePathByKeywords("decoder", int8QuantKeywords).First();
                    break;

                default:
                    throw new NotSupportedException($"Unsupported offline model type: {_modelType}");
            }
            return config;
        }

        public Task<string> SpeechTranscriptionAsync(float[] audioSamplesFrame, int sampleRate, CancellationToken cancellationToken = default)
        {
            if (IsDisposed || audioSamplesFrame == null || audioSamplesFrame.Length == 0 || runner.IsDisposed)
            {
                return Task.FromResult(string.Empty);
            }

            return _isOnlineModel ? 
                ProcessOnlineTranscriptionAsync(audioSamplesFrame, sampleRate, cancellationToken) :
                ProcessOfflineTranscriptionAsync(audioSamplesFrame, sampleRate, cancellationToken);
        }

        private Task<string> ProcessOnlineTranscriptionAsync(float[] audioSamplesFrame, int sampleRate, CancellationToken cancellationToken)
        {
            if (_onlineRecognizer == null || _onlineStream == null)
            {
                return Task.FromResult(string.Empty);
            }

            lock (_lockObject)
            {
                if (IsDisposed || _onlineStream == null) { return Task.FromResult(string.Empty); }
                
                _onlineStream.AcceptWaveform(sampleRate, audioSamplesFrame);
            }

            return runner.RunAsync<string>(ct =>
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, cancellationToken);
                var combinedCt = linkedCts.Token;

                if (IsDisposed || _onlineRecognizer == null || _onlineStream == null)
                {
                    return Task.FromResult(string.Empty);
                }

                lock (_lockObject)
                {
                    if (IsDisposed || _onlineStream == null) { return Task.FromResult(string.Empty); }
                    
                    DecodeOnlineStream(combinedCt);
                    var result = _onlineRecognizer.GetResult(_onlineStream);
                    
                    if (_onlineRecognizer.IsEndpoint(_onlineStream))
                    {
                        HandleEndpointDetection(sampleRate, combinedCt);
                        result = _onlineRecognizer.GetResult(_onlineStream);
                        _onlineRecognizer.Reset(_onlineStream);
                    }
                    
                    return Task.FromResult(result?.Text ?? string.Empty);
                }
            });
        }

        private Task<string> ProcessOfflineTranscriptionAsync(float[] audioSamplesFrame, int sampleRate, CancellationToken cancellationToken)
        {
            if (_offlineRecognizer == null)
            {
                return Task.FromResult(string.Empty);
            }

            return runner.RunAsync<string>(ct =>
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, cancellationToken);
                var combinedCt = linkedCts.Token;

                if (IsDisposed || _offlineRecognizer == null)
                {
                    return Task.FromResult(string.Empty);
                }

                // Create new offline stream for each transcription
                string result = string.Empty;
                using (var offlineStream = _offlineRecognizer.CreateStream())
                {
                    offlineStream.AcceptWaveform(sampleRate, audioSamplesFrame);
                    combinedCt.ThrowIfCancellationRequested();
                    _offlineRecognizer.Decode(offlineStream);
                    result = offlineStream.Result.Text;
                }
                return Task.FromResult(result);
            });
        }

        private void DecodeOnlineStream(CancellationToken cancellationToken)
        {
            while (!IsDisposed && _onlineRecognizer != null && _onlineStream != null && _onlineRecognizer.IsReady(_onlineStream))
            {
                cancellationToken.ThrowIfCancellationRequested();
                _onlineRecognizer.Decode(_onlineStream);
            }
        }

        private void HandleEndpointDetection(int sampleRate, CancellationToken cancellationToken)
        {
            if (IsDisposed || _onlineStream == null) { return; }
            
            // Add tail padding to ensure final words are processed
            var tailPadding = new float[sampleRate]; // 1 second of silence
            _onlineStream.AcceptWaveform(sampleRate, tailPadding);
            
            DecodeOnlineStream(cancellationToken);
        }

        protected override void OnDestroy()
        {
            lock (_lockObject)
            {
                _onlineStream?.Dispose();
                _onlineRecognizer?.Dispose();
                _offlineRecognizer?.Dispose();

                _onlineStream = null;
                _onlineRecognizer = null;
                _offlineRecognizer = null;
            }
        }
    }
}