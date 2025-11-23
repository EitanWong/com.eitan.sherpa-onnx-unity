// Editor helper: Packages/com.eitan.sherpa-onnx-unity/Editor/Mono/Inspector/SherpaModelSelectorUI.cs

namespace Eitan.Sherpa.Onnx.Unity.Editor.Mono.Inspector
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Eitan.SherpaONNXUnity.Editor.Localization;
    using Eitan.SherpaONNXUnity.Runtime;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Small utility that hydrates model metadata for a module type and exposes
    /// a dropdown/popup picker that writes the selection back into a string property.
    /// </summary>
    internal sealed class SherpaModelSelectorUI : IDisposable
    {
        private static readonly GUIContent RefreshIcon = EditorGUIUtility.IconContent("Refresh");

        private readonly SherpaONNXModuleType moduleType;
        private readonly Action repaintRequest;
        private readonly List<SherpaONNXModelMetadata> models = new List<SherpaONNXModelMetadata>();

        private CancellationTokenSource loadCts;
        private bool isLoading;
        private string loadError;

        public SherpaModelSelectorUI(SherpaONNXModuleType moduleType, Action repaintRequest)
        {
            this.moduleType = moduleType;
            this.repaintRequest = repaintRequest;
        }

        public void Dispose()
        {
            CancelPendingLoad();
        }

        public void Refresh()
        {
            CancelPendingLoad();
            loadCts = new CancellationTokenSource();
            _ = LoadAsync(loadCts.Token);
        }

        private void CancelPendingLoad()
        {
            if (loadCts == null)
            {
                return;
            }

            loadCts.Cancel();
            loadCts.Dispose();
            loadCts = null;
        }

        private async Task LoadAsync(CancellationToken token)
        {
            isLoading = true;
            loadError = null;
            repaintRequest?.Invoke();

            try
            {
                var manifest = await SherpaONNXModelRegistry.Instance.GetManifestAsync(moduleType, token).ConfigureAwait(true);

                models.Clear();
                if (manifest?.models != null)
                {
                    models.AddRange(
                        manifest.models
                            .Where(m => m != null && !string.IsNullOrWhiteSpace(m.modelId))
                            .OrderBy(m => m.modelId, StringComparer.OrdinalIgnoreCase));
                }
            }
            catch (OperationCanceledException)
            {
                // Silent cancellation.
            }
            catch (Exception ex)
            {
                loadError = ex.Message;
            }
            finally
            {
                isLoading = false;
                repaintRequest?.Invoke();
            }
        }

        public void DrawModelField(SerializedProperty modelIdProperty, GUIContent label = null)
        {
            if (modelIdProperty == null)
            {
                return;
            }

            label ??= SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.Common.FieldModelId, "Model ID");

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(modelIdProperty, label);
                DrawPickerButtons(modelIdProperty);
            }

            DrawStatusMessage();
        }

        private void DrawPickerButtons(SerializedProperty modelIdProperty)
        {
            using (new EditorGUI.DisabledScope(isLoading))
            {
                string dropdownLabel;
                if (models.Count == 0)
                {
                    dropdownLabel = isLoading
                        ? SherpaInspectorContent.Text(SherpaONNXL10n.Inspectors.ModelSelector.ButtonFetching, "Fetching…")
                        : SherpaInspectorContent.Text(SherpaONNXL10n.Inspectors.ModelSelector.ButtonPick, "Pick");
                }
                else
                {
                    dropdownLabel = string.Format(
                        SherpaInspectorContent.Text(SherpaONNXL10n.Inspectors.ModelSelector.ButtonPickCount, "Pick ({0})"),
                        models.Count);
                }

                if (EditorGUILayout.DropdownButton(new GUIContent(dropdownLabel), FocusType.Passive, EditorStyles.popup, GUILayout.MaxWidth(120f)))
                {
                    if (models.Count == 0)
                    {
                        Refresh();
                    }
                    else
                    {
                        ShowMenu(modelIdProperty);
                    }
                }
            }

            var refreshContent = RefreshIcon ?? new GUIContent("↻");
            refreshContent.tooltip = SherpaInspectorContent.Text(SherpaONNXL10n.Inspectors.ModelSelector.TooltipRefresh, "Refresh model list");
            if (GUILayout.Button(refreshContent, EditorStyles.miniButton, GUILayout.Width(28f)))
            {
                Refresh();
            }
        }

        private void DrawStatusMessage()
        {
            if (isLoading)
            {
                EditorGUILayout.HelpBox(
                    SherpaInspectorContent.Text(SherpaONNXL10n.Inspectors.ModelSelector.StatusLoading, "Loading model manifest…"),
                    MessageType.None);
                return;
            }

            if (!string.IsNullOrEmpty(loadError))
            {
                var message = string.Format(
                    SherpaInspectorContent.Text(SherpaONNXL10n.Inspectors.ModelSelector.StatusError, "Failed to fetch models: {0}"),
                    loadError);
                EditorGUILayout.HelpBox(message, MessageType.Warning);
                return;
            }

            if (models.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    SherpaInspectorContent.Text(SherpaONNXL10n.Inspectors.ModelSelector.StatusEmpty, "No models found for this module type yet. Try refreshing."),
                    MessageType.Info);
            }
        }

        private void ShowMenu(SerializedProperty modelIdProperty)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent(SherpaInspectorContent.Text(SherpaONNXL10n.Inspectors.ModelSelector.MenuClear, "Clear")), string.IsNullOrWhiteSpace(modelIdProperty.stringValue), () =>
            {
                modelIdProperty.stringValue = string.Empty;
                modelIdProperty.serializedObject.ApplyModifiedProperties();
            });
            menu.AddSeparator(string.Empty);

            foreach (var model in models)
            {
                var label = BuildDisplayLabel(model);
                var isCurrent = string.Equals(modelIdProperty.stringValue, model.modelId, StringComparison.OrdinalIgnoreCase);
                menu.AddItem(new GUIContent(label), isCurrent, () =>
                {
                    modelIdProperty.stringValue = model.modelId;
                    modelIdProperty.serializedObject.ApplyModifiedProperties();
                });
            }

            menu.ShowAsContext();
        }

        private static string BuildDisplayLabel(SherpaONNXModelMetadata metadata)
        {
            if (metadata == null || string.IsNullOrWhiteSpace(metadata.modelId))
            {
                return "<invalid>";
            }

            var sr = metadata.sampleRate > 0 ? $"{metadata.sampleRate / 1000f:0.#} kHz" : "unknown rate";
            return $"{metadata.modelId}  ({sr})";
        }
    }
}
