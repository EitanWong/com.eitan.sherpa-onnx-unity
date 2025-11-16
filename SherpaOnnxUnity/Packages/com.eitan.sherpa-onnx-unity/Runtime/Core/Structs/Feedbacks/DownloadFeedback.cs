using System;

namespace Eitan.SherpaONNXUnity.Runtime
{

    public class DownloadFeedback : ProgressFeedback
    {

        public DownloadFeedback(SherpaONNXModelMetadata metadata, string filePath, long downloadedBytes, long totalBytes, double speedBytesPerSecond, Exception exception = null) : base(metadata, $"Downloading from ({metadata.downloadUrl})\n{(downloadedBytes / 1024.0 / 1024.0):F2} MB / {(totalBytes / 1024.0 / 1024.0):F2} MB - {FormatSpeed(speedBytesPerSecond)}", filePath, exception: exception)
        {
            this.Url = metadata.downloadUrl;
            this.DownloadedBytes = downloadedBytes;
            this.TotalBytes = totalBytes;
            this.SpeedBytesPerSecond = speedBytesPerSecond;
            //auto calculate progress
            Progress = (float)downloadedBytes / totalBytes;

            //auto calculate the EstimatedTimeRemaining
            if (speedBytesPerSecond > 0 && downloadedBytes < totalBytes)
            {
                long remainingBytes = totalBytes - downloadedBytes;
                EstimatedTimeRemaining = TimeSpan.FromSeconds(remainingBytes / speedBytesPerSecond);
            }
            base.Message = $"{base.Message}\n(RemainingTime: {FormatTimeSeconds(EstimatedTimeRemaining.TotalSeconds)})";

        }
        public string Url { get; }

        public long DownloadedBytes { get; }
        public long TotalBytes { get; }
        public double SpeedBytesPerSecond { get; }
        public System.TimeSpan EstimatedTimeRemaining { get; }

        public override void Accept(ISherpaFeedbackHandler handler) => handler.OnFeedback(this);


        /// <summary>
        /// Format file size for display
        /// </summary>
        public static string FormatFileSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            decimal number = bytes;

            while (Math.Round(number / 1024) >= 1)
            {
                number /= 1024;
                counter++;
            }

            return $"{number:n1} {suffixes[counter]}";
        }



        /// <summary>
        /// Format speed for display
        /// </summary>
        public static string FormatSpeed(double bytesPerSecond)
        {
            return FormatFileSize((long)bytesPerSecond) + "/s";
        }

        /// <summary>
        /// format time seconds to day hour minute seconE
        /// </summary>
        /// <param name="seconds"></param>
        /// <returns></returns> <summary>
        public static string FormatTimeSeconds(double totalSeconds)
        {

            if (totalSeconds < 0) return "N/A";

            int days = (int)(totalSeconds / 86400);
            totalSeconds %= 86400;
            int hours = (int)(totalSeconds / 3600);
            totalSeconds %= 3600;
            int minutes = (int)(totalSeconds / 60);
            int seconds = (int)(totalSeconds % 60);

            if (days > 0)
            {
                return $"{days}d {hours}h {minutes}m {seconds}s";
            }
            else if (hours > 0)
            {
                return $"{hours}h {minutes}m {seconds}s";
            }
            else if (minutes > 0)
            {
                return $"{minutes}m {seconds}s";
            }
            else
            {
                return $"{seconds}s";
            }
        }


    }

}
