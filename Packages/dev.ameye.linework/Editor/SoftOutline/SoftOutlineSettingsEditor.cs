using System;
using Linework.Editor.Common.Utils;
using Linework.SoftOutline;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using Resolution = Linework.Common.Utils.Resolution;

namespace Linework.Editor.SoftOutline
{
    [CustomEditor(typeof(SoftOutlineSettings))]
    public class SoftOutlineSettingsEditor : UnityEditor.Editor
    {
        private SerializedProperty injectionPoint;
        private SerializedProperty showInSceneView;

        private SerializedProperty type;
        private SerializedProperty hardness;
        private SerializedProperty sharedColor;
        private SerializedProperty intensity;
        private SerializedProperty gap;
        private SerializedProperty blendMode;
        private SerializedProperty dilationMethod;
        private SerializedProperty kernelSize;
        private SerializedProperty blurSpread;
        private SerializedProperty blurPasses;
        private SerializedProperty occlusion;
        private SerializedProperty scaleWithResolution;
        private SerializedProperty referenceResolution;
        private SerializedProperty customReferenceResolution;

        private SerializedProperty outlines;
        private EditorList<Outline> outlineList;

        private void OnEnable()
        {
            injectionPoint = serializedObject.FindProperty("injectionPoint");
            showInSceneView = serializedObject.FindProperty("showInSceneView");

            type = serializedObject.FindProperty(nameof(SoftOutlineSettings.type));
            hardness = serializedObject.FindProperty(nameof(SoftOutlineSettings.hardness));
            sharedColor = serializedObject.FindProperty(nameof(SoftOutlineSettings.sharedColor));
            intensity = serializedObject.FindProperty(nameof(SoftOutlineSettings.intensity));
            gap = serializedObject.FindProperty(nameof(SoftOutlineSettings.gap));
            blendMode = serializedObject.FindProperty(nameof(SoftOutlineSettings.blendMode));
            dilationMethod = serializedObject.FindProperty(nameof(SoftOutlineSettings.dilationMethod));
            kernelSize = serializedObject.FindProperty(nameof(SoftOutlineSettings.kernelSize));
            blurSpread = serializedObject.FindProperty(nameof(SoftOutlineSettings.blurSpread));
            blurPasses = serializedObject.FindProperty(nameof(SoftOutlineSettings.blurPasses));
            scaleWithResolution = serializedObject.FindProperty(nameof(SoftOutlineSettings.scaleWithResolution));
            referenceResolution = serializedObject.FindProperty(nameof(SoftOutlineSettings.referenceResolution));
            customReferenceResolution = serializedObject.FindProperty(nameof(SoftOutlineSettings.customResolution));
       
            outlines = serializedObject.FindProperty("outlines");
            outlineList = new EditorList<Outline>(this, outlines, ForceSave, "Add Outline", "No outlines added.");
        }

        private void OnDisable()
        {
            outlineList.OnDisable();
        }

        public override void OnInspectorGUI()
        {
            if (outlines == null) OnEnable();

            serializedObject.Update();
            
            var typeChanged = false;

            EditorGUILayout.LabelField("Soft Outline", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(injectionPoint, EditorUtils.CommonStyles.InjectionPoint);
            EditorGUILayout.PropertyField(showInSceneView, EditorUtils.CommonStyles.ShowInSceneView);
            EditorGUILayout.Space();
            CoreEditorUtils.DrawSplitter();
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.LabelField(EditorUtils.CommonStyles.Outlines, EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(blendMode, EditorUtils.CommonStyles.OutlineBlendMode);
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(type, EditorUtils.CommonStyles.Type);
            typeChanged |= EditorGUI.EndChangeCheck();
            EditorGUI.indentLevel++;
            switch ((OutlineType) type.intValue)
            {
                case OutlineType.Hard:
                    EditorGUILayout.PropertyField(hardness, EditorUtils.CommonStyles.Hardness);
                    EditorGUILayout.PropertyField(sharedColor, EditorUtils.CommonStyles.OutlineColor);
                    break;
                case OutlineType.Soft:
                    EditorGUILayout.PropertyField(intensity, EditorUtils.CommonStyles.Intensity);
                    break;
            }
            EditorGUI.indentLevel--;
            EditorGUILayout.PropertyField(dilationMethod, EditorUtils.CommonStyles.DilationMethod);
            EditorGUI.indentLevel++;
            switch ((DilationMethod) dilationMethod.intValue)
            {
                case DilationMethod.Box:
                    EditorGUILayout.PropertyField(kernelSize, EditorUtils.CommonStyles.KernelSize);
                    if((OutlineType) type.intValue == OutlineType.Hard) EditorGUILayout.PropertyField(gap, EditorUtils.CommonStyles.Gap);
                    break;
                case DilationMethod.Gaussian:
                    EditorGUILayout.PropertyField(kernelSize, EditorUtils.CommonStyles.KernelSize);
                    EditorGUILayout.PropertyField(blurSpread, EditorUtils.CommonStyles.Spread);
                    if((OutlineType) type.intValue == OutlineType.Hard) EditorGUILayout.PropertyField(gap, EditorUtils.CommonStyles.Gap);
                    break;
                case DilationMethod.Kawase:
                    EditorGUILayout.PropertyField(blurPasses, EditorUtils.CommonStyles.Passes);
                    break;
                case DilationMethod.Dilate:
                    EditorGUILayout.PropertyField(kernelSize, EditorUtils.CommonStyles.KernelSize);
                    if((OutlineType) type.intValue == OutlineType.Hard) EditorGUILayout.PropertyField(gap, EditorUtils.CommonStyles.Gap);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(scaleWithResolution, EditorUtils.CommonStyles.ScaleWithResolution);
            if (scaleWithResolution.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(referenceResolution, GUIContent.none);
                if ((Resolution) referenceResolution.intValue == Resolution.Custom) EditorGUILayout.PropertyField(customReferenceResolution, GUIContent.none, GUILayout.Width(100));
                EditorGUILayout.EndHorizontal();
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndHorizontal();
            EditorGUI.indentLevel--;
            EditorGUI.BeginChangeCheck();
            
            if ((OutlineType) type.intValue == OutlineType.Hard)
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox("When using a 'Hard' outline, the color is shared between all outlines.", MessageType.Info);
            }

            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.Space();
            outlineList.Draw();
            
            if (typeChanged)
            {
                ForceSave();
            }
            
            serializedObject.ApplyModifiedProperties();
        }

        private void ForceSave()
        {
            ((SoftOutlineSettings) target).Changed();
            EditorUtility.SetDirty(target);
        }
    }
}