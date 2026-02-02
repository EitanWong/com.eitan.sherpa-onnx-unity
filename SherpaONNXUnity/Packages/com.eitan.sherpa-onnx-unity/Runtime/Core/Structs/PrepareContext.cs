namespace Eitan.SherpaONNXUnity.Runtime
{
    public sealed class PrepareContext
    {
        public PrepareContext(
            SherpaONNXModelMetadata metadata,
            string moduleDirectory,
            string modelDirectory,
            string downloadFilePath,
            string downloadFileName,
            bool isCompressed)
        {
            Metadata = metadata;
            ModuleDirectory = moduleDirectory;
            ModelDirectory = modelDirectory;
            DownloadFilePath = downloadFilePath;
            DownloadFileName = downloadFileName;
            IsCompressed = isCompressed;
        }

        public SherpaONNXModelMetadata Metadata { get; }
        public string ModuleDirectory { get; }
        public string ModelDirectory { get; }
        public string DownloadFilePath { get; }
        public string DownloadFileName { get; }
        public bool IsCompressed { get; }
    }
}
