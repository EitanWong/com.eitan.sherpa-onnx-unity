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
            const string timeoutKey = "SHERPA_ONNX_DOWNLOAD_ATTEMPT_TIMEOUT_SECONDS";
            const string allowInsecureKey = "SHERPA_ONNX_ALLOW_INSECURE_MODEL_DOWNLOAD";
            const string forceHashKey = "SHERPA_ONNX_FORCE_MODEL_HASH_VALIDATION";

            var prevAuto = Environment.GetEnvironmentVariable(autoDownloadKey);
            var prevFetch = Environment.GetEnvironmentVariable(fetchLatestKey);
            var prevLevel = Environment.GetEnvironmentVariable(loggingLevelKey);
            var prevTimeout = Environment.GetEnvironmentVariable(timeoutKey);
            var prevAllowInsecure = Environment.GetEnvironmentVariable(allowInsecureKey);
            var prevForceHash = Environment.GetEnvironmentVariable(forceHashKey);

            try
            {
                Environment.SetEnvironmentVariable(autoDownloadKey, "false");
                Environment.SetEnvironmentVariable(fetchLatestKey, "false");
                Environment.SetEnvironmentVariable(loggingLevelKey, "Warning");
                Environment.SetEnvironmentVariable(timeoutKey, "1200");
                Environment.SetEnvironmentVariable(allowInsecureKey, "true");
                Environment.SetEnvironmentVariable(forceHashKey, "true");

                SherpaONNXUnityAPI.ApplyEnvironmentOverridesFromProcess();

                Assert.IsFalse(SherpaONNXEnvironment.GetBool(SherpaONNXEnvironment.BuiltinKeys.AutoDownloadModels, @default: true));
                Assert.IsFalse(SherpaONNXEnvironment.GetBool(SherpaONNXEnvironment.BuiltinKeys.FetchLatestManifest, @default: true));
                Assert.AreEqual("Warning", SherpaONNXEnvironment.Get(SherpaONNXEnvironment.BuiltinKeys.LoggingLevel));
                Assert.AreEqual(1200, SherpaONNXEnvironment.GetInt(SherpaONNXEnvironment.BuiltinKeys.DownloadAttemptTimeoutSeconds, @default: 600));
                Assert.IsTrue(SherpaONNXEnvironment.GetBool(SherpaONNXEnvironment.BuiltinKeys.AllowInsecureModelDownload, @default: false));
                Assert.IsTrue(SherpaONNXEnvironment.GetBool(SherpaONNXEnvironment.BuiltinKeys.ForceModelHashValidation, @default: false));
            }
            finally
            {
                Environment.SetEnvironmentVariable(autoDownloadKey, prevAuto);
                Environment.SetEnvironmentVariable(fetchLatestKey, prevFetch);
                Environment.SetEnvironmentVariable(loggingLevelKey, prevLevel);
                Environment.SetEnvironmentVariable(timeoutKey, prevTimeout);
                Environment.SetEnvironmentVariable(allowInsecureKey, prevAllowInsecure);
                Environment.SetEnvironmentVariable(forceHashKey, prevForceHash);
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

        [Test]
        public async Task Prepare_MissingHash_AllowsWhenNotForced()
        {
            SherpaUtils.Prepare.EnsureUnityThreadInfrastructure();

            var prevForceHash = SherpaONNXEnvironment.Get(SherpaONNXEnvironment.BuiltinKeys.ForceModelHashValidation);
            var prevFetchLatest = SherpaONNXEnvironment.Get(SherpaONNXEnvironment.BuiltinKeys.FetchLatestManifest);
            var prevAutoDownload = SherpaONNXEnvironment.Get(SherpaONNXEnvironment.BuiltinKeys.AutoDownloadModels);

            try
            {
                SherpaONNXEnvironment.Set(SherpaONNXEnvironment.BuiltinKeys.ForceModelHashValidation, bool.FalseString);
                SherpaONNXEnvironment.Set(SherpaONNXEnvironment.BuiltinKeys.FetchLatestManifest, bool.FalseString);
                SherpaONNXEnvironment.Set(SherpaONNXEnvironment.BuiltinKeys.AutoDownloadModels, bool.FalseString);

                var metadata = new SherpaONNXModelMetadata
                {
                    modelId = "zipformer-test-missing-hash-nonstrict",
                    downloadUrl = "https://example.com/zipformer-test-missing-hash-nonstrict.zip",
                    downloadFileHash = string.Empty
                };

                var result = await SherpaUtils.Prepare.PrepareAndLoadModelWithResultAsync(
                    metadata,
                    reporter: null,
                    cancellationToken: CancellationToken.None);

                Assert.IsFalse(result.Success);
                Assert.AreEqual(PrepareErrorCode.AutoDownloadDisabled, result.ErrorCode);
            }
            finally
            {
                RestoreEnvironmentValue(SherpaONNXEnvironment.BuiltinKeys.ForceModelHashValidation, prevForceHash);
                RestoreEnvironmentValue(SherpaONNXEnvironment.BuiltinKeys.FetchLatestManifest, prevFetchLatest);
                RestoreEnvironmentValue(SherpaONNXEnvironment.BuiltinKeys.AutoDownloadModels, prevAutoDownload);
            }
        }

        [Test]
        public async Task Prepare_MissingHash_FailsWhenForced()
        {
            SherpaUtils.Prepare.EnsureUnityThreadInfrastructure();

            var prevForceHash = SherpaONNXEnvironment.Get(SherpaONNXEnvironment.BuiltinKeys.ForceModelHashValidation);
            var prevFetchLatest = SherpaONNXEnvironment.Get(SherpaONNXEnvironment.BuiltinKeys.FetchLatestManifest);
            var prevAutoDownload = SherpaONNXEnvironment.Get(SherpaONNXEnvironment.BuiltinKeys.AutoDownloadModels);

            try
            {
                SherpaONNXEnvironment.Set(SherpaONNXEnvironment.BuiltinKeys.ForceModelHashValidation, bool.TrueString);
                SherpaONNXEnvironment.Set(SherpaONNXEnvironment.BuiltinKeys.FetchLatestManifest, bool.FalseString);
                SherpaONNXEnvironment.Set(SherpaONNXEnvironment.BuiltinKeys.AutoDownloadModels, bool.FalseString);

                var metadata = new SherpaONNXModelMetadata
                {
                    modelId = "zipformer-test-missing-hash-strict",
                    downloadUrl = "https://example.com/zipformer-test-missing-hash-strict.zip",
                    downloadFileHash = string.Empty
                };

                var result = await SherpaUtils.Prepare.PrepareAndLoadModelWithResultAsync(
                    metadata,
                    reporter: null,
                    cancellationToken: CancellationToken.None);

                Assert.IsFalse(result.Success);
                Assert.AreEqual(PrepareErrorCode.HashMissing, result.ErrorCode);
            }
            finally
            {
                RestoreEnvironmentValue(SherpaONNXEnvironment.BuiltinKeys.ForceModelHashValidation, prevForceHash);
                RestoreEnvironmentValue(SherpaONNXEnvironment.BuiltinKeys.FetchLatestManifest, prevFetchLatest);
                RestoreEnvironmentValue(SherpaONNXEnvironment.BuiltinKeys.AutoDownloadModels, prevAutoDownload);
            }
        }

        private static void RestoreEnvironmentValue(string key, string value)
        {
            if (value == null)
            {
                SherpaONNXEnvironment.Remove(key);
                return;
            }

            SherpaONNXEnvironment.Set(key, value);
        }
    }
}
