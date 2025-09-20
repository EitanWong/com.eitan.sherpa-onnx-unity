using UnityEditor;
using UnityEngine;
using Eitan.SherpaOnnxUnity.Runtime;

namespace Eitan.SherpaOnnxUnity.Editor
{
    [CustomPropertyDrawer(typeof(KeywordSpotting.KeywordRegistration))]
    internal sealed class KeywordRegistrationDrawer : PropertyDrawer
    {
        private const float HorizontalFieldSpacing = 4f;
        private const float DefaultBoostingScore = 1f;
        private const float DefaultTriggerThreshold = 0.1f;
        private static readonly GUIContent BoostLabel = new("Boost", "Hotword boosting score (> 0). Increase to make the keyword easier to trigger.");
        private static readonly GUIContent ThresholdLabel = new("Threshold", "Minimum acoustic score (0-1). Lower values make the keyword easier to trigger.");

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var keywordProp = property.FindPropertyRelative(nameof(KeywordSpotting.KeywordRegistration.Keyword));
            var boostingProp = property.FindPropertyRelative(nameof(KeywordSpotting.KeywordRegistration.BoostingScore));
            var thresholdProp = property.FindPropertyRelative(nameof(KeywordSpotting.KeywordRegistration.TriggerThreshold));

            if (keywordProp == null || boostingProp == null || thresholdProp == null)
            {
                EditorGUI.LabelField(position, label, new GUIContent("KeywordRegistration fields not found."));
                EditorGUI.EndProperty();
                return;
            }

            if (float.IsNaN(boostingProp.floatValue) || boostingProp.floatValue <= 0f)
            {
                boostingProp.floatValue = DefaultBoostingScore;
            }

            if (float.IsNaN(thresholdProp.floatValue))
            {
                thresholdProp.floatValue = DefaultTriggerThreshold;
            }

            var lineHeight = EditorGUIUtility.singleLineHeight;
            var verticalSpacing = EditorGUIUtility.standardVerticalSpacing;

            var keywordRect = new Rect(position.x, position.y, position.width, lineHeight);
            EditorGUI.PropertyField(keywordRect, keywordProp, label);

            var rowY = keywordRect.y + lineHeight + verticalSpacing;
            var halfWidth = (position.width - HorizontalFieldSpacing) * 0.5f;
            var boostingRect = new Rect(position.x, rowY, halfWidth, lineHeight);
            var thresholdRect = new Rect(position.x + halfWidth + HorizontalFieldSpacing, rowY, halfWidth, lineHeight);

            EditorGUI.showMixedValue = boostingProp.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            var boostValue = EditorGUI.FloatField(boostingRect, BoostLabel, boostingProp.floatValue);
            if (EditorGUI.EndChangeCheck())
            {
                boostingProp.floatValue = boostValue <= 0f ? DefaultBoostingScore : boostValue;
            }

            EditorGUI.showMixedValue = thresholdProp.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            var thresholdValue = EditorGUI.Slider(thresholdRect, ThresholdLabel, thresholdProp.floatValue, 0f, 1f);
            if (EditorGUI.EndChangeCheck())
            {
                thresholdProp.floatValue = Mathf.Clamp01(thresholdValue);
            }

            EditorGUI.showMixedValue = false;
            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight * 2f + EditorGUIUtility.standardVerticalSpacing;
        }
    }
}
