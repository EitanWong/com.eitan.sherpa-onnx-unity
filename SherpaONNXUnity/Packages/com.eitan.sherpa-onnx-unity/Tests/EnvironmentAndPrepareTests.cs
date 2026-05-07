using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
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

        [Test]
        public async Task Prepare_HashMismatch_AllowsWhenNotForced()
        {
            var prevForceHash = SherpaONNXEnvironment.Get(SherpaONNXEnvironment.BuiltinKeys.ForceModelHashValidation);
            var prevFetchLatest = SherpaONNXEnvironment.Get(SherpaONNXEnvironment.BuiltinKeys.FetchLatestManifest);
            var prevAutoDownload = SherpaONNXEnvironment.Get(SherpaONNXEnvironment.BuiltinKeys.AutoDownloadModels);
            var prevAutoDelete = SherpaONNXEnvironment.Get(SherpaONNXEnvironment.BuiltinKeys.AutoDeleteCorruptedModels);

            var metadata = new SherpaONNXModelMetadata
            {
                modelId = "zipformer-test-hash-mismatch-nonstrict",
                moduleType = SherpaONNXModuleType.SpeechRecognition,
                downloadUrl = "https://example.invalid/zipformer-test-hash-mismatch-nonstrict.zip",
                downloadFileHash = "deadbeef"
            };

            SherpaUtils.Prepare.ResolveDownloadFilePath(
                metadata,
                out _,
                out var modelDirectory,
                out var downloadFileName,
                out _);
            var downloadPath = SherpaUtils.Prepare.ResolveDownloadFilePath(
                metadata,
                out _,
                out _,
                out _,
                out _);

            try
            {
                SherpaONNXEnvironment.Set(SherpaONNXEnvironment.BuiltinKeys.ForceModelHashValidation, bool.FalseString);
                SherpaONNXEnvironment.Set(SherpaONNXEnvironment.BuiltinKeys.FetchLatestManifest, bool.FalseString);
                SherpaONNXEnvironment.Set(SherpaONNXEnvironment.BuiltinKeys.AutoDownloadModels, bool.TrueString);
                SherpaONNXEnvironment.Set(SherpaONNXEnvironment.BuiltinKeys.AutoDeleteCorruptedModels, bool.TrueString);

                CleanupPath(modelDirectory);
                CleanupPath(downloadPath);

                Directory.CreateDirectory(Path.GetDirectoryName(downloadPath));
                CreateTestArchive(downloadPath, downloadFileName);

                var result = await SherpaUtils.Prepare.PrepareAndLoadModelWithResultAsync(
                    metadata,
                    reporter: null,
                    cancellationToken: CancellationToken.None);

                Assert.IsTrue(result.Success, $"Prepare should succeed when hash validation is not forced. Error: {result.ErrorCode}");
                Assert.AreEqual(PrepareErrorCode.None, result.ErrorCode);
                Assert.IsTrue(Directory.Exists(modelDirectory));
            }
            finally
            {
                CleanupPath(modelDirectory);
                CleanupPath(downloadPath);
                RestoreEnvironmentValue(SherpaONNXEnvironment.BuiltinKeys.ForceModelHashValidation, prevForceHash);
                RestoreEnvironmentValue(SherpaONNXEnvironment.BuiltinKeys.FetchLatestManifest, prevFetchLatest);
                RestoreEnvironmentValue(SherpaONNXEnvironment.BuiltinKeys.AutoDownloadModels, prevAutoDownload);
                RestoreEnvironmentValue(SherpaONNXEnvironment.BuiltinKeys.AutoDeleteCorruptedModels, prevAutoDelete);
            }
        }

        [Test]
        public void Prepare_HashMismatch_IsRejectedWhenForced()
        {
            var prevForceHash = SherpaONNXEnvironment.Get(SherpaONNXEnvironment.BuiltinKeys.ForceModelHashValidation);

            try
            {
                SherpaONNXEnvironment.Set(SherpaONNXEnvironment.BuiltinKeys.ForceModelHashValidation, bool.TrueString);

                var metadata = new SherpaONNXModelMetadata { modelId = "zipformer-test-hash-mismatch-strict" };
                var verificationResult = new FileVerificationEventArgs(
                    "/tmp/fake.zip",
                    FileVerificationStatus.HashMismatch,
                    progress: 1f,
                    calculatedHash: "actual",
                    expectedHash: "expected",
                    message: "Expected hash: expected, Actual hash: actual");

                var method = typeof(SherpaUtils.Prepare).GetMethod("ShouldAcceptVerificationResult", BindingFlags.NonPublic | BindingFlags.Static);
                Assert.IsNotNull(method, "Could not locate ShouldAcceptVerificationResult via reflection.");

                var accepted = (bool)method.Invoke(null, new object[] { metadata, "/tmp/fake.zip", "download archive", verificationResult, null });
                Assert.IsFalse(accepted);
            }
            finally
            {
                RestoreEnvironmentValue(SherpaONNXEnvironment.BuiltinKeys.ForceModelHashValidation, prevForceHash);
            }
        }

        [Test]
        public async Task Prepare_VerifiesOrtModelDirectory_AsDownloaded()
        {
            var metadata = new SherpaONNXModelMetadata
            {
                modelId = "sherpa-onnx-moonshine-ort-prepare-test",
                moduleType = SherpaONNXModuleType.SpeechRecognition,
                downloadUrl = string.Empty
            };

            SherpaUtils.Prepare.ResolveDownloadFilePath(
                metadata,
                out _,
                out var modelDirectory,
                out _,
                out _);

            try
            {
                if (Directory.Exists(modelDirectory))
                {
                    Directory.Delete(modelDirectory, true);
                }

                Directory.CreateDirectory(modelDirectory);
                File.WriteAllBytes(Path.Combine(modelDirectory, "encoder_model.ort"), new byte[] { 0x01, 0x02, 0x03, 0x04 });
                File.WriteAllText(Path.Combine(modelDirectory, "tokens.txt"), "a 1");

                var downloaded = await SherpaUtils.Prepare.CheckIsModelDownloadedAsync(metadata, CancellationToken.None);

                Assert.IsTrue(downloaded);
            }
            finally
            {
                if (Directory.Exists(modelDirectory))
                {
                    Directory.Delete(modelDirectory, true);
                }
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

        private static void CreateTestArchive(string archivePath, string archiveName)
        {
            using var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create);
            var folderName = Path.GetFileNameWithoutExtension(archiveName);

            var modelEntry = archive.CreateEntry($"{folderName}/encoder.onnx");
            using (var writer = new StreamWriter(modelEntry.Open()))
            {
                writer.Write("fake-model-content");
            }

            var tokensEntry = archive.CreateEntry($"{folderName}/tokens.txt");
            using (var writer = new StreamWriter(tokensEntry.Open()))
            {
                writer.Write("a 1");
            }
        }

        private static void CleanupPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }

            if (File.Exists(path))
            {
                File.Delete(path);
            }

            var cachePath = path + ".sha256";
            if (File.Exists(cachePath))
            {
                File.Delete(cachePath);
            }
        }
    }
}
