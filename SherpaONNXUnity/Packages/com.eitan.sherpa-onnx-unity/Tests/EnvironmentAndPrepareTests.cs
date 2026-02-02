using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using Eitan.SherpaONNXUnity.Runtime;
using Eitan.SherpaONNXUnity.Runtime.Utilities;

namespace Eitan.SherpaONNXUnity.Tests
{
    public class EnvironmentAndPrepareTests
    {
        [Test]
        public void EnvironmentOverrides_ApplyToRuntimeStore()
        {
            const string autoDownloadKey = "SHERPA_ONNX_AUTO_DOWNLOAD";
            const string fetchLatestKey = "SHERPA_ONNX_FETCH_LATEST_MANIFEST";
            const string loggingLevelKey = "SHERPA_ONNX_LOGGING_LEVEL";

            var prevAuto = Environment.GetEnvironmentVariable(autoDownloadKey);
            var prevFetch = Environment.GetEnvironmentVariable(fetchLatestKey);
            var prevLevel = Environment.GetEnvironmentVariable(loggingLevelKey);

            try
            {
                Environment.SetEnvironmentVariable(autoDownloadKey, "false");
                Environment.SetEnvironmentVariable(fetchLatestKey, "false");
                Environment.SetEnvironmentVariable(loggingLevelKey, "Warning");

                SherpaONNXUnityAPI.ApplyEnvironmentOverridesFromProcess();

                Assert.IsFalse(SherpaONNXEnvironment.GetBool(SherpaONNXEnvironment.BuiltinKeys.AutoDownloadModels, @default: true));
                Assert.IsFalse(SherpaONNXEnvironment.GetBool(SherpaONNXEnvironment.BuiltinKeys.FetchLatestManifest, @default: true));
                Assert.AreEqual("Warning", SherpaONNXEnvironment.Get(SherpaONNXEnvironment.BuiltinKeys.LoggingLevel));
            }
            finally
            {
                Environment.SetEnvironmentVariable(autoDownloadKey, prevAuto);
                Environment.SetEnvironmentVariable(fetchLatestKey, prevFetch);
                Environment.SetEnvironmentVariable(loggingLevelKey, prevLevel);
            }
        }

        [Test]
        public async Task Prepare_RemovesModelDirectory_OnDownloadFailure()
        {
            SherpaUtils.Prepare.EnsureUnityThreadInfrastructure();

            SherpaONNXEnvironment.Set(SherpaONNXEnvironment.BuiltinKeys.AutoDownloadModels, bool.TrueString);
            SherpaONNXEnvironment.Set(SherpaONNXEnvironment.BuiltinKeys.AutoDeleteCorruptedModels, bool.TrueString);

            var metadata = new SherpaONNXModelMetadata
            {
                modelId = "zipformer-test-model-unity-ci",
                downloadUrl = "https://example.invalid/zipformer-test-model-unity-ci.zip",
                downloadFileHash = "deadbeef"
            };

            var downloadPath = SherpaUtils.Prepare.ResolveDownloadFilePath(
                metadata,
                out _,
                out var modelDirectory,
                out _,
                out _);

            if (Directory.Exists(modelDirectory))
            {
                Directory.Delete(modelDirectory, true);
            }
            if (File.Exists(downloadPath))
            {
                File.Delete(downloadPath);
            }

            var result = await SherpaUtils.Prepare.PrepareAndLoadModelWithResultAsync(
                metadata,
                reporter: null,
                cancellationToken: CancellationToken.None);

            Assert.IsFalse(result.Success);
            Assert.IsTrue(
                result.ErrorCode == PrepareErrorCode.DownloadConnectionError ||
                result.ErrorCode == PrepareErrorCode.DownloadFailed ||
                result.ErrorCode == PrepareErrorCode.DownloadServerError ||
                result.ErrorCode == PrepareErrorCode.DownloadClientError,
                $"Unexpected error code: {result.ErrorCode}");
            Assert.IsFalse(Directory.Exists(modelDirectory), $"Model directory was not cleaned: {modelDirectory}");
            Assert.IsFalse(File.Exists(downloadPath), $"Download artifact was not cleaned: {downloadPath}");
        }
    }
}
