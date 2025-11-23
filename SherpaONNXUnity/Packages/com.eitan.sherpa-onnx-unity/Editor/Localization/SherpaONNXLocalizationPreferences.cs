#if UNITY_EDITOR

namespace Eitan.SherpaONNXUnity.Editor.Localization
{
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Persisted preference that stores the desired editor language selection.
    /// Saved under ProjectSettings so it travels with the project.
    /// </summary>
    [FilePath("ProjectSettings/SherpaONNXLocalization.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class SherpaONNXLocalizationPreferences : ScriptableSingleton<SherpaONNXLocalizationPreferences>
    {
        [SerializeField]
        private SherpaONNXEditorLanguage _language = SherpaONNXEditorLanguage.Auto;

        internal SherpaONNXEditorLanguage Language
        {
            get => _language;
            set
            {
                if (_language == value)
                {
                    return;
                }

                _language = value;
                Save(true);
            }
        }
    }
}

#endif
