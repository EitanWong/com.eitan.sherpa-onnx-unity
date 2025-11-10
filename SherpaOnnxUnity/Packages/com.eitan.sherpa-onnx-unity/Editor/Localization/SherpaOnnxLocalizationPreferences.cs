#if UNITY_EDITOR

namespace Eitan.SherpaOnnxUnity.Editor.Localization
{
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Persisted preference that stores the desired editor language selection.
    /// Saved under ProjectSettings so it travels with the project.
    /// </summary>
    [FilePath("ProjectSettings/SherpaOnnxLocalization.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class SherpaOnnxLocalizationPreferences : ScriptableSingleton<SherpaOnnxLocalizationPreferences>
    {
        [SerializeField]
        private SherpaOnnxEditorLanguage _language = SherpaOnnxEditorLanguage.Auto;

        internal SherpaOnnxEditorLanguage Language
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
