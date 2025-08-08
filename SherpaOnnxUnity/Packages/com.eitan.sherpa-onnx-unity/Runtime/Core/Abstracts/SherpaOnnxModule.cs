// SherpaOnnxModule.cs (Optimized)

namespace Eitan.SherpaOnnxUnity.Runtime
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Utilities;

    public abstract class SherpaOnnxModule : IDisposable
    {
        protected abstract SherpaOnnxModuleType ModuleType { get; }

        private readonly SynchronizationContext _rootThreadContext;
        protected readonly TaskRunner runner;

        // --- 统一的、线程安全的销毁标志 ---
        protected bool IsDisposed { get; private set; }

        public SherpaOnnxModule(string modelID, int sampleRate = 16000, SherpaOnnxFeedbackReporter reporter = null)
        {
            _rootThreadContext = SynchronizationContext.Current;
            runner = new TaskRunner();

            // --- 订阅应用退出事件，作为安全保障 ---
            UnityEngine.Application.quitting += Dispose;

            bool isMobilePlatform = UnityEngine.Application.isMobilePlatform;

            _ = runner.RunAsync(async (ct) =>
            {
                // 在启动时检查是否已经被销毁 (例如，对象创建后立即被销毁)
                if (IsDisposed)
                {
                    return;
                }
                var reporterAdapter = new SherpaOnnxFeedbackReporter(feedbackArgs =>
                {
                    // 使用 _isDisposed 标志位进行更可靠的检查
                    if (IsDisposed || runner.IsDisposed) { return; }

                    _rootThreadContext.Post(state =>
                    {
                        if (IsDisposed || runner.IsDisposed) { return; }
                        try
                        {
                            reporter?.Report((IFeedback)state);
                        }
                        catch (Exception e)
                        {
                            UnityEngine.Debug.LogException(e);
                        }
                    }, feedbackArgs);
                });

                var metadata = SherpaOnnxModelRegistry.Instance.GetMetadata(modelID);

                try
                {


                    var prepareResult = await SherpaUtils.Prepare.PrepareModelAsync(metadata, reporterAdapter);

                    if (prepareResult)
                    {

                        reporterAdapter?.Report(new PrepareFeedback(metadata, message: $"{ModuleType} model:{modelID} ready to init"));

                        await Initialization(metadata, sampleRate, isMobilePlatform, reporterAdapter, ct);

                        // 初始化成功后再次检查，防止在初始化过程中被销毁
                        if (ct.IsCancellationRequested || IsDisposed)
                        {
                            reporterAdapter?.Report(new CancelFeedback(metadata, message: "Initialization was cancelled or disposed."));
                            return;
                        }

                        reporterAdapter?.Report(new SuccessFeedback(metadata, message: $"{ModuleType} model:{modelID} init success"));
                    }
                    else
                    {
                        throw new Exception($"Model {metadata.modelId} initialization failed\nplease download from url:{metadata.downloadUrl}\nthen uncompress it to {SherpaUtils.Model.GetModuleTypeByModelId(metadata.modelId)} manually.");
                    }

                }
                catch (OperationCanceledException oce)
                {
                    reporterAdapter?.Report(new CancelFeedback(metadata, message: oce.Message));
                }
                catch (Exception ex)
                {
                    reporterAdapter?.Report(new FailedFeedback(metadata, message: ex.Message, exception: ex));
                }
            });
        }

        // --- 实现完整的、标准的 IDisposable 模式 ---
        public void Dispose()
        {
            // 这是供外部调用的标准 Dispose 方法
            Dispose(true);
            // 请求垃圾回收器不要调用终结器（析构函数），因为我们已经手动清理了
            GC.SuppressFinalize(this);
        }

        // 析构函数，作为最后的安全网。它会在对象被GC回收时调用
        ~SherpaOnnxModule()
        {
            // 如果开发者忘记调用Dispose()，此方法会确保非托管资源被释放
            Dispose(false);
        }

        private void Dispose(bool disposing)
        {
            // 防止重复调用，确保幂等性
            if (IsDisposed) { return; }
            IsDisposed = true;

            // 如果是由用户代码调用 (Dispose())，而不是由GC调用
            if (disposing)
            {
                // 1. 取消订阅事件，防止内存泄漏
                UnityEngine.Application.quitting -= Dispose;

                // 2. 销毁托管资源，例如 TaskRunner，它会取消所有正在运行的任务
                runner?.Dispose();
            }

            // 3. 调用子类的清理方法，释放非托管资源 (Native)
            // 无论如何，这一步都需要执行
            OnDestroy();
        }

        /// <summary>
        /// 子类必须实现的初始化逻辑。
        /// </summary>
        protected abstract Task Initialization(SherpaOnnxModelMetadata metadata, int sampleRate, bool isMobilePlatform, SherpaOnnxFeedbackReporter reporter, CancellationToken ct);

        /// <summary>
        /// 子类必须实现的资源清理逻辑。
        /// </summary>
        protected abstract void OnDestroy();
    }
}