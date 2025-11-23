namespace Eitan.SherpaONNXUnity.Samples
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.UI;

    /// <summary>
    /// Lightweight helper to keep model loading progress consistent across demos.
    /// Assumes Prepare → Download → Clean → Verify → Decompress → Load.
    /// </summary>
    public sealed class ModelLoadProgressTracker
    {
        public enum Stage
        {
            Prepare,
            Download,
            Clean,
            Verify,
            Decompress,
            Load,
        }

        private readonly UI.EasyProgressBar _progressBar;
        private readonly Text _progressValueText;
        private readonly Text _messageText;
        private readonly Dictionary<Stage, float> _stageProgress = new();
        private const int StageCount = 6;

        public ModelLoadProgressTracker(
            UI.EasyProgressBar progressBar,
            Text progressValueText,
            Text messageText)
        {
            _progressBar = progressBar;
            _progressValueText = progressValueText;
            _messageText = messageText;

            foreach (Stage stage in Enum.GetValues(typeof(Stage)))
            {
                _stageProgress[stage] = 0f;
            }

            UpdateDisplay(0f, string.Empty);
            SetVisible(false);
        }

        public void SetVisible(bool isVisible)
        {
            if (_progressBar != null)
            {
                _progressBar.gameObject.SetActive(isVisible);
            }

            if (_messageText != null)
            {
                _messageText.gameObject.SetActive(isVisible);
            }
        }

        public void Reset()
        {
            foreach (Stage stage in Enum.GetValues(typeof(Stage)))
            {
                _stageProgress[stage] = 0f;
            }

            UpdateDisplay(0f, string.Empty);
        }

        public void UpdateStage(Stage stage, string message, float? stageProgress = null)
        {
            SetVisible(true);

            if (stageProgress.HasValue)
            {
                var clamped = Mathf.Clamp01(stageProgress.Value);
                if (_stageProgress[stage] < clamped)
                {
                    _stageProgress[stage] = clamped;
                }
            }

            UpdateDisplay(CalculateTotalProgress(), message);
        }

        public void MarkStageComplete(Stage stage, string message)
        {
            UpdateStage(stage, message, 1f);
        }

        public void Complete(string message)
        {
            foreach (Stage stage in Enum.GetValues(typeof(Stage)))
            {
                _stageProgress[stage] = 1f;
            }

            UpdateDisplay(1f, message);
        }

        private float CalculateTotalProgress()
        {
            float total = 0f;
            foreach (Stage stage in Enum.GetValues(typeof(Stage)))
            {
                total += _stageProgress[stage];
            }

            return total / StageCount;
        }

        private void UpdateDisplay(float totalProgress, string message)
        {
            totalProgress = Mathf.Clamp01(totalProgress);
            if (_progressBar != null)
            {
                _progressBar.FillAmount = totalProgress;
            }

            if (_progressValueText != null)
            {
                _progressValueText.text = $"{totalProgress * 100f:F0}%";
            }

            if (_messageText != null)
            {
                _messageText.text = message ?? string.Empty;
            }
        }
    }
}
