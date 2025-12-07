using System;

namespace Eitan.SherpaONNXUnity.Runtime.Utilities
{
    /// <summary>
    /// Snapshot of TaskRunner activity for diagnostics and profiling.
    /// </summary>
    public readonly struct TaskRunnerMetrics
    {
        public TaskRunnerMetrics(int activeTasks, int totalStarted, int completed, double averageDurationMs, double lastDurationMs)
        {
            ActiveTasks = activeTasks;
            TotalStarted = totalStarted;
            Completed = completed;
            AverageDurationMs = averageDurationMs;
            LastDurationMs = lastDurationMs;
        }

        public int ActiveTasks { get; }
        public int TotalStarted { get; }
        public int Completed { get; }
        public double AverageDurationMs { get; }
        public double LastDurationMs { get; }
    }
}
