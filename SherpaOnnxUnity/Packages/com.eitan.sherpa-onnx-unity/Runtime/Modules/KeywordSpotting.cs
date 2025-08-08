using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SherpaOnnx;

namespace Eitan.SherpaOnnxUnity.Runtime
{
    public sealed class KeywordSpotting : SherpaOnnxModule
    {
        public event Action<string> OnKeywordDetected;
        
        private KeywordSpotter _keywordSpotter;
        private OnlineStream _stream;
        private readonly ConcurrentQueue<float> _audioQueue = new();
        private readonly object _lockObject = new();
        private volatile bool _isDetecting;
        private int _sampleRate;
        private readonly float _keywordsScore;
        private readonly float _keywordsThreshold;
        
        protected override SherpaOnnxModuleType ModuleType => SherpaOnnxModuleType.KeywordSpotting;
        
        //TODO: 支持自定义唤醒词功能 具体要参考https://k2-fsa.github.io/sherpa/onnx/kws/index.html#what-is-open-vocabulary-keyword-spotting 进行实现，用pinyin库将文字转为拼音kk，英文的情况也要根据文档描述设计转换的算法
        public KeywordSpotting(string modelID, int sampleRate = 16000, float keywordsScore = 2.0f, float keywordsThreshold = 0.25f, string[] customKeywords = null, SherpaOnnxFeedbackReporter reporter = null)
            : base(modelID, sampleRate, reporter)
        {
            _keywordsScore = keywordsScore;
            _keywordsThreshold = keywordsThreshold;
        }
        
        protected override async Task Initialization(SherpaOnnxModelMetadata metadata, int sampleRate, bool isMobilePlatform, SherpaOnnxFeedbackReporter reporter, CancellationToken ct)
        {
            try
            {
                reporter?.Report(new LoadFeedback(metadata, message: $"Start Loading: {metadata.modelId}"));
                
                var config = await CreateKeywordSpotterConfig(metadata, sampleRate, isMobilePlatform, reporter, ct);
                
                await runner.RunAsync(cancellationToken =>
                {
                    try
                    {
                        reporter?.Report(new LoadFeedback(metadata, message: $"Loading KWS model: {metadata.modelId}"));
                        _keywordSpotter = new KeywordSpotter(config);
                        _stream = _keywordSpotter.CreateStream();
                        
                        if (_keywordSpotter == null || _stream == null)
                        {
                            throw new Exception($"Failed to initialize KWS model: {metadata.modelId}");
                        }
                        
                        reporter?.Report(new LoadFeedback(metadata, message: $"KWS model loaded successfully: {metadata.modelId}"));
                    }
                    catch (Exception ex)
                    {
                        reporter?.Report(new FailedFeedback(metadata, message: ex.Message, exception: ex));
                        throw;
                    }
                });
            }
            catch (Exception ex)
            {
                reporter?.Report(new FailedFeedback(metadata, ex.Message, exception: ex));
                throw;
            }
        }
        
        private Task<KeywordSpotterConfig> CreateKeywordSpotterConfig(SherpaOnnxModelMetadata metadata, int sampleRate, bool isMobilePlatform, SherpaOnnxFeedbackReporter reporter, CancellationToken ct)
        {
            _sampleRate = sampleRate;
            
            var config = new KeywordSpotterConfig
            {
                FeatConfig = { SampleRate = sampleRate, FeatureDim = 80 },
                ModelConfig = {
                    Provider = "cpu",
                    NumThreads = 1,
                    Debug = 0
                },
                KeywordsScore = _keywordsScore,
                KeywordsThreshold = _keywordsThreshold
            };
            
            var int8QuantKeyword = isMobilePlatform ? "int8" : null;
            
            config.ModelConfig.Transducer.Encoder = metadata.GetModelFilePathByKeywords("encoder","99", int8QuantKeyword)?.First();
            config.ModelConfig.Transducer.Decoder = metadata.GetModelFilePathByKeywords("decoder","99", int8QuantKeyword)?.First();
            config.ModelConfig.Transducer.Joiner = metadata.GetModelFilePathByKeywords("joiner","99", int8QuantKeyword)?.First();
            config.ModelConfig.Tokens = metadata.GetModelFilePathByKeywords("tokens.txt")?.First();
            config.KeywordsFile = metadata.GetModelFilePathByKeywords("keywords.txt")?.First();
            
            return Task.FromResult(config);
        }
        
        public void StreamDetect(ReadOnlySpan<float> samples)
        {
            if (IsDisposed || _keywordSpotter == null || _stream == null || samples.Length == 0)
            {
                return;
            }


            for (int i = 0; i < samples.Length; i++)
            {
                _audioQueue.Enqueue(samples[i]);
            }
            
            if (!_isDetecting)
            {
                _isDetecting = true;
                _ = runner.RunAsync(ProcessAudioQueue);
            }
        }
        
        private Task ProcessAudioQueue(CancellationToken ct)
        {
            if (IsDisposed)
            {
                return Task.CompletedTask;
            }


            const int batchSize = 3200;
            float[] batch = ArrayPool<float>.Shared.Rent(batchSize);
            
            try
            {
                while (!_audioQueue.IsEmpty && !ct.IsCancellationRequested)
                {
                    int count = 0;
                    while (count < batchSize && _audioQueue.TryDequeue(out float sample))
                    {
                        batch[count++] = sample;
                    }
                    
                    if (count > 0)
                    {
                        ProcessAudioChunk(batch.AsSpan(0, count));
                    }
                }
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                UnityEngine.Debug.LogException(ex);
            }
            finally
            {
                ArrayPool<float>.Shared.Return(batch);
                _isDetecting = false;
            }
            
            return Task.CompletedTask;
        }
        
        private void ProcessAudioChunk(ReadOnlySpan<float> samples)
        {
            lock (_lockObject)
            {
                if (IsDisposed || _stream == null)
                {
                    return;
                }


                _stream.AcceptWaveform(_sampleRate, samples.ToArray());
                
                while (_keywordSpotter.IsReady(_stream))
                {
                    _keywordSpotter.Decode(_stream);
                    var result = _keywordSpotter.GetResult(_stream);
                    
                    if (!string.IsNullOrEmpty(result.Keyword))
                    {
                        _keywordSpotter.Reset(_stream);
                        OnKeywordDetected?.Invoke(result.Keyword);
                    }
                }
            }
        }
        
        public async Task<string> DetectAsync(float[] samples, CancellationToken? ct = null)
        {
            if (_keywordSpotter == null || _stream == null)
            {
                throw new InvalidOperationException("KeywordSpotting is not initialized or has been disposed. Please ensure it is loaded successfully before detecting keywords.");
            }
            
            return await runner.RunAsync((cancellationToken) =>
            {
                string detectedKeyword = string.Empty;
                
                lock (_lockObject)
                {
                    if (IsDisposed || _stream == null)
                    {

                        return Task.FromResult(string.Empty);
                    }


                    _stream.AcceptWaveform(_sampleRate, samples);
                    
                    while (_keywordSpotter.IsReady(_stream))
                    {
                        _keywordSpotter.Decode(_stream);
                        var result = _keywordSpotter.GetResult(_stream);
                        
                        if (!string.IsNullOrEmpty(result.Keyword))
                        {
                            _keywordSpotter.Reset(_stream);
                            detectedKeyword = result.Keyword;
                            break;
                        }
                    }
                }
                
                return Task.FromResult(detectedKeyword);
            }, cancellationToken: ct ?? CancellationToken.None);
        }
        
        public string DetectSync(float[] samples)
        {
            if (_keywordSpotter == null || _stream == null || IsDisposed)
            {

                return string.Empty;
            }


            lock (_lockObject)
            {
                if (IsDisposed || _stream == null)
                {
                    return string.Empty;
                }


                _stream.AcceptWaveform(_sampleRate, samples);
                
                while (_keywordSpotter.IsReady(_stream))
                {
                    _keywordSpotter.Decode(_stream);
                    var result = _keywordSpotter.GetResult(_stream);
                    
                    if (!string.IsNullOrEmpty(result.Keyword))
                    {
                        _keywordSpotter.Reset(_stream);
                        return result.Keyword;
                    }
                }
                
                return string.Empty;
            }
        }
        
        protected override void OnDestroy()
        {
            lock (_lockObject)
            {
                _stream?.Dispose();
                _stream = null;
                _keywordSpotter?.Dispose();
                _keywordSpotter = null;
            }
        }
    }
}
