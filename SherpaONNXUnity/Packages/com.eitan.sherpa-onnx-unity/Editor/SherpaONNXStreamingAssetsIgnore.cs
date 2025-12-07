#if UNITY_EDITOR

namespace Eitan.SherpaONNXUnity.Editor
{

    using System;
    using System.Collections.Generic;
    using System.IO;
    using Eitan.SherpaONNXUnity.Runtime;
    using UnityEditor;
    using UnityEditor.Build;
    using UnityEditor.Build.Reporting;
    using UnityEngine;

    public sealed class SherpaONNXStreamingAssetsIgnore :
        IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public int callbackOrder => 0;

        private static int s_AssetEditDepth = 0; // reentrancy-safe batching
        private const string kSessionMovedKey = "SherpaONNX_AnyMoved"; // session-scoped guard for restore

        // 需要忽略的 StreamingAssets 相对路径（可扩展多个）
        // 例如：Assets/StreamingAssets/<这些子目录>
        private static readonly string[] kIgnoreSubfolders =
        {
        "sherpa-onnx", // 你的默认目标
        // "another-folder", ...
    };

        // 备份根目录与标记文件（用于失败兜底）
        private const string HiddenRootAsset = "Assets/StreamingAssets~";
        private const string ConflictLogPath = "Library/SherpaONNX_ConflictReport.txt";

        // Dynamic hidden root path (Unity may auto-suffix the folder name). Always reuse the actual path.
        private static string GetOrCreateHiddenRootAsset()
        {
            const string desired = "Assets/StreamingAssets~";

            // 1) If the folder already exists on disk, always reuse it, regardless of AssetDatabase state
            if (Directory.Exists(desired))
            {

                return desired.Replace('\\', '/');
            }

            // 2) If AssetDatabase already recognizes it, use it

            if (AssetDatabase.IsValidFolder(desired))
            {

                return desired;
            }

            // 3) Create it via System.IO to avoid AssetDatabase adding numeric suffixes ("~ 1", "~ 2", ...)

            try { Directory.CreateDirectory(desired); } catch { /* ignore IO issues */ }
            // Make sure the editor sees the new folder (even though it's ignored by importer)
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            return desired.Replace('\\', '/');
        }

        private static string MarkerFilePath
            => (GetOrCreateHiddenRootAsset() + "/.pending_restore").Replace('\\', '/');

        private static void BeginAssetBatch()
        {
            if (s_AssetEditDepth++ == 0)
            {
                AssetDatabase.StartAssetEditing();
            }

        }

        private static void EndAssetBatch()
        {
            if (--s_AssetEditDepth <= 0)
            {
                s_AssetEditDepth = 0;
                AssetDatabase.StopAssetEditing();
            }
        }

        #region Build Hooks
        public void OnPreprocessBuild(BuildReport report)
        {
            // 只在“运行时 StreamingAssets 不可写”的平台才忽略
            if (!IsStreamingAssetsReadOnlyAtRuntime(report.summary.platform))
            {
                return;
            }

            EnsureCleanBackupRoot();
            bool movedAny = false;
            try
            {
                BeginAssetBatch();

                foreach (var sub in kIgnoreSubfolders)
                {
                    var src = AssetStreamingSubfolder(sub);
                    if (!AssetDatabase.IsValidFolder(src))
                    {
                        continue;
                    }

                    // Ensure hidden root exists (Unity ignores folders ending with '~')
                    var hiddenRoot = GetOrCreateHiddenRootAsset();
                    EnsureAssetFolder(hiddenRoot);

                    // Conflict guard: if both the source and the backup exist and both have content, warn and only merge (no wholesale move)
                    var existingBackup = FixedBackupAssetPathFor(sub);
                    bool backupHasContent = AssetDatabase.IsValidFolder(existingBackup) && !IsAssetFolderEmpty(existingBackup);
                    bool sourceHasContent = AssetDatabase.IsValidFolder(src) && !IsAssetFolderEmpty(src);
                    if (backupHasContent && sourceHasContent)
                    {
                        SherpaLog.Warning($"[StreamingAssetsIgnore] Both source and backup have content for '{sub}'. Will MERGE into backup to avoid data loss.");
                        AppendConflictLog(sub, src, existingBackup);
                    }

                    var dst = FixedBackupAssetPathFor(sub);
                    MoveOrMergeAssetFolder(src, dst);
                    movedAny = true;
                    SherpaLog.Info($"[StreamingAssetsIgnore] Moved: {src} -> {dst}");
                }
            }
            finally
            {
                EndAssetBatch();
            }

            if (movedAny)
            {
                try { File.WriteAllText(MarkerFilePath, DateTime.UtcNow.ToString("o")); } catch { /* ignore */ }
                SessionState.SetBool(kSessionMovedKey, true);
                var envOv = GetEnvIncludeModelsDesktopOverride();
                if (envOv.HasValue && IsDesktop(report.summary.platform))
                {
                    SherpaLog.Info($"[StreamingAssetsIgnore] CI override SHERPA_INCLUDE_MODELS_DESKTOP={(envOv.Value ? "ON" : "OFF")} (takes precedence over Project Settings).");
                }
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                SherpaLog.Info($"[StreamingAssetsIgnore] Will restore after build. Platform={report.summary.platform}");
            }
            else
            {
                SherpaLog.Info("[StreamingAssetsIgnore] Nothing to move.");
            }
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            // Only attempt restore if we moved something in this session or a marker exists
            if (SessionState.GetBool(kSessionMovedKey, false) || File.Exists(MarkerFilePath))
            {
                TryRestoreAll("post-build");
            }
        }
        #endregion

        #region Auto-restore on script reload / menu
        [InitializeOnLoadMethod]
        private static void AutoRestoreOnReload()
        {
            if (!File.Exists(MarkerFilePath) && !SessionState.GetBool(kSessionMovedKey, false))
            {
                return;
            }

            EditorApplication.update += OneShotTryRestore;
            void OneShotTryRestore()
            {
                EditorApplication.update -= OneShotTryRestore;
                TryRestoreAll("reload");
            }
        }

        // [MenuItem("Tools/StreamingAssets/Restore Ignored Folders")]
        // private static void MenuRestore()
        // {
        //     TryRestoreAll("menu");
        // }

        #endregion

        #region Core restore/move
        private static void TryRestoreAll(string reason)
        {
            if (!File.Exists(MarkerFilePath) && !SessionState.GetBool(kSessionMovedKey, false))
            {
                return;
            }

            bool restoredAny = false;

            try
            {
                BeginAssetBatch();

                // 逐个子目录从固定备份路径还原
                foreach (var sub in kIgnoreSubfolders)
                {
                    var dst = AssetStreamingSubfolder(sub);
                    var bak = FixedBackupAssetPathFor(sub);
                    if (Directory.Exists(bak) || AssetDatabase.IsValidFolder(bak))
                    {
                        MoveOrMergeAssetFolder(bak, dst);
                        SherpaLog.Info($"[StreamingAssetsIgnore] Restored: {bak} -> {dst}");
                        restoredAny = true;
                    }
                }
            }
            finally
            {
                EndAssetBatch();
            }

            // 清理空的备份目录与标记
            CleanupBackupRoot();
            if (restoredAny)
            {
                SessionState.SetBool(kSessionMovedKey, false);
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                SherpaLog.Info($"[StreamingAssetsIgnore] Restore finished ({reason}).");
            }
        }

        private static void MoveOrMergeAssetFolder(string sourceAssetPath, string targetAssetPath)
        {
            // Normalize
            sourceAssetPath = sourceAssetPath.Replace('\\', '/');
            targetAssetPath = targetAssetPath.Replace('\\', '/');

            // Prevent self-move/no-op
            if (string.Equals(sourceAssetPath, targetAssetPath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // If SOURCE is under the ignored hidden root (…/StreamingAssets~), AssetDatabase won't see it.
            // Use filesystem move to bring it back under Assets/StreamingAssets first.
            var hiddenRoot = GetOrCreateHiddenRootAsset();
            bool sourceUnderHiddenRoot = sourceAssetPath.StartsWith(hiddenRoot + "/", StringComparison.Ordinal);
            if (sourceUnderHiddenRoot)
            {
                // Ensure destination parent exists; do not pre-create the final target folder if moving the whole tree.
                var parent = Path.GetDirectoryName(targetAssetPath)?.Replace('\\', '/');
                if (!string.IsNullOrEmpty(parent))
                {
                    EnsureAssetFolder(parent);
                }

                // If an empty target folder already exists, delete it to allow a clean move.
                if (AssetDatabase.IsValidFolder(targetAssetPath) && IsAssetFolderEmpty(targetAssetPath))
                {
                    AssetDatabase.DeleteAsset(targetAssetPath);
                }

                try
                {
                    FileUtil.MoveFileOrDirectory(sourceAssetPath, targetAssetPath);
                    AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                    return;
                }
                catch (Exception ex)
                {
                    SherpaLog.Warning($"[StreamingAssetsIgnore] Filesystem move (restore) failed: {ex.Message}. Will attempt merge.");
                    // Fall through to merge logic below.
                }
            }

            // From here on, source must be a valid AssetDatabase folder.
            if (!AssetDatabase.IsValidFolder(sourceAssetPath))
            {
                return;
            }

            // If target is under the ignored hidden root (…/StreamingAssets~), use filesystem move.
            // AssetDatabase may not recognize ignored folders, causing ValidateMoveAsset/MoveAsset to fail.
            bool targetUnderHiddenRoot = targetAssetPath.Replace('\\', '/').StartsWith(hiddenRoot + "/", StringComparison.Ordinal);
            if (targetUnderHiddenRoot)
            {
                // Ensure parent exists (via IO for '~' folders), but DO NOT pre-create the target folder itself.
                var parent = Path.GetDirectoryName(targetAssetPath)?.Replace('\\', '/');
                if (!string.IsNullOrEmpty(parent))
                {
                    EnsureAssetFolder(parent);
                }

                // If an empty target folder happens to exist, delete it to allow a clean move.

                if (AssetDatabase.IsValidFolder(targetAssetPath) && IsAssetFolderEmpty(targetAssetPath))
                {
                    AssetDatabase.DeleteAsset(targetAssetPath);
                }


                try
                {
                    FileUtil.MoveFileOrDirectory(sourceAssetPath, targetAssetPath);
                    AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                    return;
                }
                catch (Exception ex)
                {
                    SherpaLog.Warning($"[StreamingAssetsIgnore] Filesystem move fallback failed: {ex.Message}. Will MERGE instead. src={sourceAssetPath} dst={targetAssetPath}");
                    // fall through to merge logic below
                }
            }

            // If we can, try a whole-folder move (fast path that preserves GUIDs)
            // IMPORTANT: AssetDatabase.MoveAsset requires that the destination path DOES NOT exist.
            {
                var parent = Path.GetDirectoryName(targetAssetPath)?.Replace('\\', '/');
                var leaf = Path.GetFileName(targetAssetPath);
                if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(leaf))
                {
                    // Ensure parent exists, but DO NOT pre-create the target folder
                    EnsureAssetFolder(parent);

                    // If target folder already exists and is empty, delete it so MoveAsset can succeed
                    if (AssetDatabase.IsValidFolder(targetAssetPath) && IsAssetFolderEmpty(targetAssetPath))
                    {
                        AssetDatabase.DeleteAsset(targetAssetPath);
                    }

                    // Validate the move before attempting it
                    var validate = AssetDatabase.ValidateMoveAsset(sourceAssetPath, targetAssetPath);
                    if (string.IsNullOrEmpty(validate))
                    {
                        var err = AssetDatabase.MoveAsset(sourceAssetPath, targetAssetPath);
                        if (string.IsNullOrEmpty(err))
                        {
                            AssetDatabase.SaveAssets();
                            return;
                        }
                        else
                        {
                            SherpaLog.Warning($"[StreamingAssetsIgnore] MoveAsset fast-path failed: {err}. Will MERGE instead. src={sourceAssetPath} dst={targetAssetPath}");
                        }
                    }
                    else
                    {
                        // ValidateMoveAsset returned a reason it can't move as a whole
                        // e.g., target exists with content, source under VC lock, etc.
                        // We'll fall back to a merge.
                        // (Optional) Debug: SherpaLog.Info($"[StreamingAssetsIgnore] ValidateMoveAsset blocked fast move: {validate}");
                    }
                }
            }

            // Fallback: move children (merge)
            foreach (var subFolder in AssetDatabase.GetSubFolders(sourceAssetPath))
            {
                var to = Path.Combine(targetAssetPath, Path.GetFileName(subFolder)).Replace('\\', '/');
                MoveOrMergeAssetFolder(subFolder, to);
            }

            foreach (var guid in AssetDatabase.FindAssets(string.Empty, new[] { sourceAssetPath }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.IsValidFolder(path))
                {
                    continue; // handled above
                }

                var to = Path.Combine(targetAssetPath, Path.GetFileName(path)).Replace('\\', '/');
                if (path == to)
                {
                    continue;
                }

                var err = AssetDatabase.MoveAsset(path, to);
                if (!string.IsNullOrEmpty(err))
                {
                    // As a last resort, try delete-then-move semantics via FileUtil
                    try { FileUtil.MoveFileOrDirectory(path, to); } catch { /* ignore */ }
                }
            }

            // Delete the now-empty source folder (if empty)
            TryDeleteEmptyAssetFolder(sourceAssetPath);
            AssetDatabase.SaveAssets();
        }
        #endregion

        #region Helpers
        private static bool IsStreamingAssetsReadOnlyAtRuntime(BuildTarget target)
        {
            // Platforms where StreamingAssets is effectively inside a read-only container at runtime
            switch (target)
            {
                case BuildTarget.Android:
                case BuildTarget.iOS:
                case BuildTarget.tvOS:
                case BuildTarget.WebGL:
#if UNITY_2021_2_OR_NEWER
                case BuildTarget.PS4:
                case BuildTarget.PS5:
                case BuildTarget.XboxOne:
                case BuildTarget.GameCoreXboxOne:
                case BuildTarget.GameCoreXboxSeries:
#endif
#if UNITY_SWITCH
            case BuildTarget.Switch:
#endif
                    return true;
            }

            // Desktop (Standalone) — controlled by Project Settings (default: ignore => treat as read-only)
            if (IsDesktop(target))
            {
                bool include = SherpaONNXBuildSettings.Instance.IncludeModelsInDesktopBuild;
                var envOv = GetEnvIncludeModelsDesktopOverride();
                if (envOv.HasValue)
                {
                    include = envOv.Value; // CI override takes precedence
                }


                return !include;
            }

            // Other platforms: treat as writable (do not ignore) by default

            return false;
        }
        private static bool IsDesktop(BuildTarget target)
        {
            switch (target)
            {
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                case BuildTarget.StandaloneOSX:
#if UNITY_2021_2_OR_NEWER
                case BuildTarget.StandaloneLinux64:
#endif
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// •	本地/默认：继续用 Project Settings 的“桌面端是否包含模型”开关。
        // •	CI/Cloud Build：设置环境变量覆盖开关（优先级更高）：
        // •	包含模型：SHERPA_INCLUDE_MODELS_DESKTOP=1
        // •	忽略模型：SHERPA_INCLUDE_MODELS_DESKTOP=0
        /// </summary>
        /// <returns></returns>
        private static bool? GetEnvIncludeModelsDesktopOverride()
        {
            try
            {
                var v = Environment.GetEnvironmentVariable("SHERPA_INCLUDE_MODELS_DESKTOP");
                if (string.IsNullOrEmpty(v))
                {
                    return null;
                }

                v = v.Trim().ToLowerInvariant();
                if (v == "1" || v == "true" || v == "on" || v == "yes")
                {
                    return true;
                }

                if (v == "0" || v == "false" || v == "off" || v == "no")
                {
                    return false;
                }


                return null;
            }
            catch { return null; }
        }

        private static bool IsAssetFolderEmpty(string assetFolder)
            => AssetDatabase.IsValidFolder(assetFolder)
               && AssetDatabase.GetSubFolders(assetFolder).Length == 0
               && AssetDatabase.FindAssets(string.Empty, new[] { assetFolder }).Length == 0;

        private static void AppendConflictLog(string subfolder, string sourceAssetFolder, string backupAssetFolder)
        {
            try
            {
                // Build a simple text report enumerating both sides
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("==== SherpaONNX StreamingAssets Conflict ====");
                sb.AppendLine($"Time(UTC): {DateTime.UtcNow:o}");
                sb.AppendLine($"Subfolder: {subfolder}");
                sb.AppendLine($"Source: {sourceAssetFolder}");
                sb.AppendLine($"Backup: {backupAssetFolder}");
                sb.AppendLine("-- Source contents --");
                foreach (var p in EnumerateAssetFilesRecursively(sourceAssetFolder))
                {
                    sb.AppendLine("  " + p);
                }

                sb.AppendLine("-- Backup contents --");
                foreach (var p in EnumerateAssetFilesRecursively(backupAssetFolder))
                {
                    sb.AppendLine("  " + p);
                }


                sb.AppendLine();

                // Ensure the directory exists (Library usually exists but guard anyway)
                var dir = Path.GetDirectoryName(ConflictLogPath);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }


                File.AppendAllText(ConflictLogPath, sb.ToString());
                SherpaLog.Info($"[StreamingAssetsIgnore] Conflict details appended to: {ConflictLogPath}");
            }
            catch (Exception ex)
            {
                SherpaLog.Warning($"[StreamingAssetsIgnore] Failed to write conflict log: {ex.Message}");
            }
        }

        private static IEnumerable<string> EnumerateAssetFilesRecursively(string rootAssetFolder)
        {
            var results = new List<string>();
            if (!AssetDatabase.IsValidFolder(rootAssetFolder))
            {
                return results;
            }
            // Use AssetDatabase to list under the folder (folders will also appear; filter them out)

            foreach (var guid in AssetDatabase.FindAssets(string.Empty, new[] { rootAssetFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path) && !AssetDatabase.IsValidFolder(path))
                {
                    results.Add(path);
                }

            }
            results.Sort(StringComparer.OrdinalIgnoreCase);
            return results;
        }

        private static string AssetStreamingSubfolder(string sub)
            => ("Assets/StreamingAssets/" + sub.Trim('/')).Replace('\\', '/');

        private static string FixedBackupAssetPathFor(string subfolder)
        {
            var hiddenRoot = GetOrCreateHiddenRootAsset();
            EnsureAssetFolder(hiddenRoot);
            string safe = subfolder.Trim('/').Replace('/', '_').Replace('\\', '_');
            return (hiddenRoot + "/" + safe).Replace('\\', '/');
        }

        private static bool IsBackupFolderFor(string backupAssetPath, string subfolder)
        {
            var name = Path.GetFileName(backupAssetPath);
            var prefix = subfolder.Replace('/', '_').Replace('\\', '_') + "__";
            return name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        private static void EnsureAssetFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder))
            {
                return;
            }

            // If this is an ignored special folder (leaf ends with '~'), create it via System.IO to avoid Unity auto-suffixing

            var leaf = Path.GetFileName(assetFolder);
            var parent = Path.GetDirectoryName(assetFolder)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(leaf) && leaf.EndsWith("~", StringComparison.Ordinal))
            {
                if (!string.IsNullOrEmpty(parent))
                {
                    EnsureAssetFolder(parent);
                }


                if (!Directory.Exists(assetFolder))
                {
                    try { Directory.CreateDirectory(assetFolder); } catch { /* ignore */ }
                    AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                }
                return;
            }

            // Normal AssetDatabase-driven creation for non-ignored folders
            if (string.IsNullOrEmpty(parent))
            {
                return;
            }


            EnsureAssetFolder(parent);
            if (!AssetDatabase.IsValidFolder(assetFolder))
            {
                AssetDatabase.CreateFolder(parent, leaf);
            }

        }


        // Some OSes may drop hidden files like .DS_Store into "~" folders, which
        // AssetDatabase doesn't see. Use an IO-level check to decide true emptiness.
        private static bool IsDirectoryReallyEmptyIO(string assetFolderPath)
        {
            var path = assetFolderPath.Replace('\\', '/');
            if (!Directory.Exists(path))
            {
                return true;
            }
            try
            {
                using (var e = Directory.EnumerateFileSystemEntries(path).GetEnumerator())
                {
                    return !e.MoveNext();
                }
            }
            catch
            {
                return false;
            }
        }

        // Force delete a folder on disk (plus its .meta), then refresh the AssetDatabase.
        private static void ForceDeleteFolderIO(string assetFolderPath)
        {
            var path = assetFolderPath.Replace('\\', '/');
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }
            catch { /* ignore IO issues */ }

            // Also remove the .meta if present
            var meta = path.EndsWith("/") ? (path.TrimEnd('/') + ".meta") : (path + ".meta");
            try
            {
                if (File.Exists(meta))
                {
                    File.Delete(meta);
                }
            }
            catch { /* ignore IO issues */ }

            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        }

        private static void TryDeleteEmptyAssetFolder(string assetFolder)
        {
            if (!AssetDatabase.IsValidFolder(assetFolder))
            {
                return;
            }

            if (AssetDatabase.GetSubFolders(assetFolder).Length > 0)
            {
                return;
            }

            if (AssetDatabase.FindAssets(string.Empty, new[] { assetFolder }).Length > 0)
            {
                return;
            }

            AssetDatabase.DeleteAsset(assetFolder);
        }

        private static void EnsureCleanBackupRoot()
        {
            EnsureAssetFolder(GetOrCreateHiddenRootAsset());
        }

        private static void CleanupBackupRoot()
        {
            var hiddenRoot = GetOrCreateHiddenRootAsset();

            // If the editor doesn't recognize the folder, try IO-level cleanup anyway.
            if (!AssetDatabase.IsValidFolder(hiddenRoot) && !Directory.Exists(hiddenRoot))
            {
                // Nothing to do.
                return;
            }

            // 1) Delete all AssetDatabase-visible children (folders/assets)
            if (AssetDatabase.IsValidFolder(hiddenRoot))
            {
                foreach (var sub in AssetDatabase.GetSubFolders(hiddenRoot))
                {
                    AssetDatabase.DeleteAsset(sub);
                }
            }

            // 2) Delete known non-asset files that AssetDatabase won't see (e.g., marker)
            if (File.Exists(MarkerFilePath))
            {
                try { File.Delete(MarkerFilePath); } catch { /* ignore */ }
            }

            AssetDatabase.SaveAssets();

            // 3) If the folder looks empty to AssetDatabase but still contains hidden files (like .DS_Store),
            // use an IO-level emptiness check.
            bool rootEmptyADB = AssetDatabase.IsValidFolder(hiddenRoot) ? IsAssetFolderEmpty(hiddenRoot) : true;
            bool rootEmptyIO = IsDirectoryReallyEmptyIO(hiddenRoot);
            bool shouldDelete = rootEmptyADB && rootEmptyIO;

            if (shouldDelete)
            {
                bool deleted = false;

                // Prefer AssetDatabase deletion when possible (keeps editor state clean)
                if (AssetDatabase.IsValidFolder(hiddenRoot))
                {
                    deleted = AssetDatabase.DeleteAsset(hiddenRoot);
                }

                // If AssetDatabase couldn't delete (or the folder was not recognized), force delete on disk
                if (!deleted || Directory.Exists(hiddenRoot))
                {
                    ForceDeleteFolderIO(hiddenRoot);
                }
            }
            else
            {
                // The folder still has residual files on disk; remove them forcibly and then delete the folder.
                ForceDeleteFolderIO(hiddenRoot);
            }

            // Final sanity refresh; if something survived, leave a warning to aid diagnosis.
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            if (AssetDatabase.IsValidFolder(hiddenRoot) || Directory.Exists(hiddenRoot))
            {
                SherpaLog.Warning($"[StreamingAssetsIgnore] CleanupBackupRoot: '{hiddenRoot}' could not be fully removed. It may be held open by the OS or contain locked files.");
            }
        }
        #endregion
    }
}
#endif
