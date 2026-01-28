using Linework.Editor.Common.Utils;
using Linework.WideOutline;
using UnityEditor;

namespace Linework.Editor.WideOutline
{
    [CustomEditor(typeof(Outline))]
    public class OutlineEditor : UnityEditor.Editor
    {
        private SerializedProperty renderingLayer;
        private SerializedProperty layerMask;
        private SerializedProperty renderQueue;
        private SerializedProperty occlusion;
        private SerializedProperty cullMode;
        private SerializedProperty closedLoop;
        private SerializedProperty alphaCutout, alphaCutoutTexture, alphaCutoutThreshold, alphaCutoutUVTransform;
        private SerializedProperty gpuInstancing;
        private SerializedProperty vertexAnimation;
        private SerializedProperty color;
        private SerializedProperty width;
        private SerializedProperty customDepthEnabled;
        private SerializedProperty disableWidthControl;

        private void OnEnable()
        {
            renderingLayer = serializedObject.FindProperty(nameof(Outline.RenderingLayer));
            layerMask = serializedObject.FindProperty(nameof(Outline.layerMask));
            renderQueue = serializedObject.FindProperty(nameof(Outline.renderQueue));
            occlusion = serializedObject.FindProperty(nameof(Outline.occlusion));
            cullMode = serializedObject.FindProperty(nameof(Outline.cullingMode));
            closedLoop = serializedObject.FindProperty(nameof(Outline.closedLoop));
            alphaCutout = serializedObject.FindProperty(nameof(Outline.alphaCutout));
            alphaCutoutTexture = serializedObject.FindProperty(nameof(Outline.alphaCutoutTexture));
            alphaCutoutThreshold = serializedObject.FindProperty(nameof(Outline.alphaCutoutThreshold));
            alphaCutoutUVTransform = serializedObject.FindProperty(nameof(Outline.alphaCutoutUVTransform));
            gpuInstancing = serializedObject.FindProperty(nameof(Outline.gpuInstancing));
            vertexAnimation = serializedObject.FindProperty(nameof(Outline.vertexAnimation));
            color = serializedObject.FindProperty(nameof(Outline.color));
            width = serializedObject.FindProperty(nameof(Outline.width));
            disableWidthControl = serializedObject.FindProperty("disableWidthControl");
            customDepthEnabled = serializedObject.FindProperty("customDepthEnabled");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Filters", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(renderingLayer, EditorUtils.CommonStyles.OutlineLayer);
            EditorGUILayout.PropertyField(layerMask, EditorUtils.CommonStyles.LayerMask);
            EditorGUILayout.PropertyField(renderQueue, EditorUtils.CommonStyles.RenderQueue);
            EditorGUILayout.Space();
            
            EditorGUILayout.LabelField("Render", EditorStyles.boldLabel);
            if (!customDepthEnabled.boolValue)
            {
                EditorGUILayout.PropertyField(occlusion, EditorUtils.CommonStyles.OutlineOcclusion);
                if ((WideOutlineOcclusion) occlusion.intValue == WideOutlineOcclusion.WhenNotOccluded)
                {
                    EditorGUILayout.PropertyField(closedLoop, EditorUtils.CommonStyles.ClosedLoop);
                }
            }
            EditorGUILayout.PropertyField(cullMode, EditorUtils.CommonStyles.CullMode);
            EditorGUILayout.PropertyField(alphaCutout, EditorUtils.CommonStyles.AlphaCutout);
            if (alphaCutout.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(alphaCutoutTexture, EditorUtils.CommonStyles.AlphaCutoutTexture);
                EditorGUILayout.PropertyField(alphaCutoutThreshold, EditorUtils.CommonStyles.AlphaCutoutThreshold);
                alphaCutoutUVTransform.vector4Value = EditorGUILayout.Vector4Field(EditorUtils.CommonStyles.AlphaCutoutUVTransform, alphaCutoutUVTransform.vector4Value);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.PropertyField(gpuInstancing, EditorUtils.CommonStyles.GpuInstancing);
            if (gpuInstancing.boolValue)
            {
                EditorGUILayout.HelpBox("GPU instancing breaks the SRP Batcher. See the documentation for details.", MessageType.Warning);
            }
            EditorGUILayout.PropertyField(vertexAnimation, EditorUtils.CommonStyles.VertexAnimation);
            if (vertexAnimation.boolValue)
            {
                EditorGUILayout.HelpBox("With vertex animation enabled, the outline color should be set by your object's shader. See the documentation for details.", MessageType.Warning);
            }
            EditorGUILayout.Space();
            
            if ((WideOutlineOcclusion) occlusion.intValue != WideOutlineOcclusion.AsMask)
            {
                EditorGUILayout.LabelField("Outline", EditorStyles.boldLabel);
                using (new EditorGUI.DisabledScope(vertexAnimation.boolValue))
                {
                    EditorGUILayout.PropertyField(color, EditorUtils.CommonStyles.OutlineColor);
                }
                using (new EditorGUI.DisabledScope(disableWidthControl.boolValue))
                {
                    EditorGUILayout.PropertyField(width, EditorUtils.CommonStyles.OutlineWidth);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("The mask mode is used to mask out the other outlines where they are not needed.", MessageType.Info);
            }
            
            EditorGUILayout.Space();
            serializedObject.ApplyModifiedProperties();
        }
    }
}