namespace Eitan.SherpaONNXUnity.Samples
{
    using Eitan.SherpaONNXUnity.Runtime;
    using UnityEngine;
    using UnityEngine.UI;
    using Stage = Eitan.SherpaONNXUnity.Samples.ModelLoadProgressTracker.Stage;

    /// <summary>
    /// Shared UI helpers and color palette for demo scenes.
    /// 演示场景通用的UI辅助方法与颜色配置。
    /// </summary>
    public static class DemoUIShared
    {
        // 统一颜色配置 / Centralized palette
        public static readonly Color LoadColor = new Color(0.15f, 0.67f, 0.36f);
        public static readonly Color UnloadColor = new Color(0.83f, 0.27f, 0.27f);
        public static readonly Color RecordIdleColor = new Color(0.2f, 0.6f, 0.95f);
        public static readonly Color RecordStopColor = new Color(0.93f, 0.45f, 0.2f);
        public static readonly Color DisabledColor = new Color(0.65f, 0.65f, 0.65f);

        /// <summary>
        /// Set button tint safely. 安全设置按钮颜色。
        /// </summary>
        public static void SetButtonColor(Button button, Color color)
        {
            if (button == null)
            {
                return;
            }

            var image = button.GetComponent<Image>();
            if (image != null)
            {
                image.color = color;
            }
        }

        /// <summary>
        /// Show loading progress and update status text. 展示加载进度并更新状态文本。
        /// </summary>
        public static void ShowLoading(ModelLoadProgressTracker tracker, Text statusText, string message)
        {
            tracker?.Reset();
            tracker?.SetVisible(true);
            tracker?.MarkStageComplete(Stage.Prepare, message);
            tracker?.UpdateStage(Stage.Download, message, 0.35f);
            if (statusText != null)
            {
                statusText.text = message;
            }
        }

        /// <summary>
        /// Hide loading UI and finalize status. 隐藏加载进度并更新完成状态。
        /// </summary>
        public static void ShowLoadingComplete(ModelLoadProgressTracker tracker, Text statusText, string message)
        {
            tracker?.Complete(message);
            tracker?.SetVisible(false);
            if (statusText != null)
            {
                statusText.text = message;
            }
        }

        /// <summary>
        /// Update the shared progress tracker based on structured feedback from the runtime.
        /// </summary>
        public static void UpdateProgressFromFeedback(ModelLoadProgressTracker tracker, Text statusText, SherpaFeedback feedback)
        {
            if (feedback == null)
            {
                return;
            }

            tracker?.SetVisible(true);

            var message = feedback.Message ?? string.Empty;
            statusText?.gameObject.SetActive(true);
            if (statusText != null)
            {
                statusText.text = message;
            }

            switch (feedback)
            {
                case PrepareFeedback prepare:
                    tracker?.UpdateStage(Stage.Prepare, message, 0.05f);
                    break;
                case DownloadFeedback download:
                    tracker?.UpdateStage(Stage.Download, message, download.Progress);
                    break;
                case VerifyFeedback verify:
                    tracker?.UpdateStage(Stage.Verify, message, verify.Progress);
                    break;
                case DecompressFeedback decompress:
                    tracker?.UpdateStage(Stage.Decompress, message, decompress.Progress);
                    break;
                case CleanFeedback clean:
                    tracker?.UpdateStage(Stage.Clean, message, 1f);
                    break;
                case LoadFeedback load:
                    tracker?.UpdateStage(Stage.Load, message, 0.75f);
                    break;
                case SuccessFeedback success:
                    tracker?.Complete(message);
                    tracker?.SetVisible(false);
                    break;
                case FailedFeedback failed:
                    tracker?.UpdateStage(Stage.Load, message, 1f);
                    tracker?.SetVisible(true);
                    break;
                case CancelFeedback cancel:
                    tracker?.UpdateStage(Stage.Load, message, 0f);
                    tracker?.SetVisible(true);
                    break;
            }
        }
    }
}
