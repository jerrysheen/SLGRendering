using System.Linq;
using Linework.Editor.Common.Utils;
using Linework.WideOutline;
using UnityEditor;
using UnityEngine;

namespace Linework.Editor.WideOutline
{
    [CustomEditor(typeof(Linework.WideOutline.WideOutline))]
    public class WideOutlineEditor : UnityEditor.Editor
    {
        private static class Styles
        {
            public static readonly GUIContent Settings = EditorGUIUtility.TrTextContent("Settings", "The settings for the Wide Outline renderer feature.");
        }

        private SerializedProperty settings;

        private bool initialized;

        private void Initialize()
        {
            settings = serializedObject.FindProperty("settings");
            initialized = true;
        }

        public override void OnInspectorGUI()
        {
            if (!initialized) Initialize();
            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(settings, Styles.Settings);

            if (settings.objectReferenceValue == null)
            {
                if (GUILayout.Button("Create", EditorStyles.miniButton, GUILayout.Width(70.0f)))
                {
                    const string path = "Assets/Wide Outline Settings.asset";

                    var createdSettings = CreateInstance<WideOutlineSettings>();
                    AssetDatabase.CreateAsset(createdSettings, path);
                    AssetDatabase.SaveAssets();
                    EditorUtility.FocusProjectWindow();
                    Selection.activeObject = createdSettings;
                    EditorUtils.OpenInspectorWindow(createdSettings);
                    settings.objectReferenceValue = createdSettings;
                    serializedObject.ApplyModifiedProperties();
                }
            }
            else
            {
                if (GUILayout.Button("Open", EditorStyles.miniButton, GUILayout.Width(70.0f)))
                {
                    EditorUtils.OpenInspectorWindow(settings.objectReferenceValue);
                }
            }
            EditorGUILayout.EndHorizontal();

            if (settings.objectReferenceValue != null && !((WideOutlineSettings) settings.objectReferenceValue).Outlines.Any(outline => outline.IsActive()))
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox("No active outlines present. Effect will not render. Open the settings to add/enable outlines.", MessageType.Warning);
            }
        }
    }
}