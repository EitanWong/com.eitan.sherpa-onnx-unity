namespace Eitan.SherpaONNXUnity.Runtime
{
    public enum PrepareErrorCode
    {
        None = 0,
        Cancelled = 1,
        MetadataMissing = 2,
        ModelIdMissing = 3,
        AutoDownloadDisabled = 4,
        DownloadUrlMissing = 5,
        DownloadUrlInvalid = 6,
        DownloadInsecureRejected = 7,
        DownloadFailed = 8,
        DownloadUnauthorized = 9,
        DownloadForbidden = 10,
        DownloadNotFound = 11,
        DownloadRateLimited = 12,
        DownloadTimeout = 13,
        DownloadClientError = 14,
        DownloadServerError = 15,
        DownloadConnectionError = 16,
        DownloadProtocolError = 17,
        DownloadDataProcessingError = 18,
        HashMissing = 19,
        VerificationFailed = 20,
        ExtractionFailed = 21,
        InsufficientDiskSpace = 22,
        IoError = 23,
        UnexpectedError = 24
    }
}
