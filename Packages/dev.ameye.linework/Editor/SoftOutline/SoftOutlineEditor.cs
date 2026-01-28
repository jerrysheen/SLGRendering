using System.Linq;
using Linework.Editor.Common.Utils;
using Linework.SoftOutline;
using UnityEditor;
using UnityEngine;

namespace Linework.Editor.SoftOutline
{
    [CustomEditor(typeof(Linework.SoftOutline.SoftOutline))]
    public class SoftOutlineEditor : UnityEditor.Editor
    {
        private static class Styles
        {
            public static readonly GUIContent Settings = EditorGUIUtility.TrTextContent("Settings", "The settings for the Soft Outline renderer feature.");
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
                    const string path = "Assets/Soft Outline Settings.asset";

                    var createdSettings = CreateInstance<SoftOutlineSettings>();
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
            
            if (settings.objectReferenceValue != null && !((SoftOutlineSettings) settings.objectReferenceValue).Outlines.Any(outline => outline.IsActive()))
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox("No active outlines present. Effect will not render. Open the settings to add/enable outlines.", MessageType.Warning);
            }
        }
    }
}