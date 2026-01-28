using System.Linq;
using Linework.Editor.Common.Utils;
using Linework.SurfaceFill;
using UnityEditor;
using UnityEngine;

namespace Linework.Editor.SurfaceFill
{
    [CustomEditor(typeof(Linework.SurfaceFill.SurfaceFill))]
    public class SurfaceFillEditor : UnityEditor.Editor
    {
        private static class Styles
        {
            public static readonly GUIContent Settings = EditorGUIUtility.TrTextContent("Settings", "The settings for the Surface Fill renderer feature.");
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
                    const string path = "Assets/Surface Fill Settings.asset";

                    var createdSettings = CreateInstance<SurfaceFillSettings>();
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

            if (settings.objectReferenceValue != null && !((SurfaceFillSettings) settings.objectReferenceValue).Fills.Any(fill => fill.IsActive()))
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox("No active fills present. Effect will not render. Open the settings to add/enable fills.", MessageType.Warning);
            }
        }
    }
}