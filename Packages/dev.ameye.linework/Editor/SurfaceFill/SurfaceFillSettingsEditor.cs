using Linework.Editor.Common.Utils;
using Linework.SurfaceFill;
using UnityEditor;
using UnityEditor.Rendering;

namespace Linework.Editor.SurfaceFill
{
    [CustomEditor(typeof(SurfaceFillSettings))]
    public class SurfaceFillSettingsEditor : UnityEditor.Editor
    {
        private SerializedProperty injectionPoint;
        private SerializedProperty showInSceneView;

        private SerializedProperty fills;
        private EditorList<Fill> fillList;

        private void OnEnable()
        {
            injectionPoint = serializedObject.FindProperty("injectionPoint");
            showInSceneView = serializedObject.FindProperty("showInSceneView");

            fills = serializedObject.FindProperty("fills");
            fillList = new EditorList<Fill>(this, fills, ForceSave, "Add Fill", "No fills added.", "A maximum of 8 fills has been added.", 8);
        }

        private void OnDisable()
        {
            fillList.OnDisable();
        }

        public override void OnInspectorGUI()
        {
            if (fills == null) OnEnable();

            serializedObject.Update();

            EditorGUILayout.LabelField("Surface Fill", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(injectionPoint, EditorUtils.CommonStyles.InjectionPoint);
            EditorGUILayout.PropertyField(showInSceneView, EditorUtils.CommonStyles.ShowInSceneView);
            EditorGUILayout.Space();
            CoreEditorUtils.DrawSplitter();
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.LabelField(EditorUtils.CommonStyles.Fills, EditorStyles.boldLabel);
            fillList.Draw();
        }

        private void ForceSave()
        {
            ((SurfaceFillSettings) target).Changed();
            EditorUtility.SetDirty(target);
        }
    }
}