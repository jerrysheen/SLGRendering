using Linework.EdgeDetection;
using Linework.Editor.Common.Utils;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditorInternal;
using UnityEngine;
using Resolution = Linework.EdgeDetection.Resolution;

namespace Linework.Editor.EdgeDetection
{
    [CustomEditor(typeof(EdgeDetectionSettings))]
    public class EdgeDetectionSettingsEditor : UnityEditor.Editor
    {
        private SerializedProperty injectionPoint;
        private SerializedProperty showInSceneView;

        // Debugging.
        private SerializedProperty debugView;
        private SerializedProperty debugSectionsChannels;
        private SerializedProperty debugPerceptualSections;

        // Section map.
        private SerializedProperty sectionMapFormat;
        private SerializedProperty sectionMapClearValue;
        private SerializedProperty sectionRenderingLayer;
        private SerializedProperty maskRenderingLayer;
        private SerializedProperty maskInfluence;
        private SerializedProperty objectId;
        private SerializedProperty particles;
        private SerializedProperty sectionMapInput;
        private SerializedProperty sectionTexture;
        private SerializedProperty sectionTextureUvSet;
        private SerializedProperty vertexColorChannel;
        private SerializedProperty additionalSectionPasses;
        private ReorderableList additionalSectionPassesList;
        private SerializedProperty sectionRenderQueue;

        // Discontinuity.
        private SerializedProperty discontinuityInput;
        private SerializedProperty depthSensitivity;
        private SerializedProperty depthDistanceModulation;
        private SerializedProperty grazingAngleMaskPower;
        private SerializedProperty grazingAngleMaskHardness;
        private SerializedProperty normalSensitivity;
        private SerializedProperty luminanceSensitivity;

        // Distortion.
        private SerializedProperty distortEdges;
        private SerializedProperty distortionTexture;
        private SerializedProperty distortionStepRate;
        private SerializedProperty distortionScale;
        private SerializedProperty distortionStrength;
        private SerializedProperty distortionThicknessInfluence;

        // Break up.
        private SerializedProperty breakUpEdges;
        private SerializedProperty breakUpScale;
        private SerializedProperty breakUpAmount;

        // Outline.
        private SerializedProperty kernel;
        private SerializedProperty outlineThickness;
        private SerializedProperty scaleWithDistance;
        private SerializedProperty distanceScaleStart;
        private SerializedProperty distanceScaleDistance;
        private SerializedProperty distanceScaleMin;
        private SerializedProperty scaleWithResolution;
        private SerializedProperty referenceResolution;
        private SerializedProperty customReferenceResolution;
        private SerializedProperty backgroundColor;
        private SerializedProperty outlineColor;
        private SerializedProperty overrideColorInShadow;
        private SerializedProperty outlineColorShadow;
        private SerializedProperty fill;
        private SerializedProperty fillColor;
        private SerializedProperty fadeByDistance;
        private SerializedProperty distanceFadeStart;
        private SerializedProperty distanceFadeDistance;
        private SerializedProperty distanceFadeColor;
        private SerializedProperty fadeByHeight;
        private SerializedProperty heightFadeStart;
        private SerializedProperty heightFadeDistance;
        private SerializedProperty heightFadeColor;
        private SerializedProperty blendMode;

        private SerializedProperty showSectionMapSection, showDiscontinuitySection, showOutlineSection;

        private void OnEnable()
        {
            showSectionMapSection = serializedObject.FindProperty(nameof(EdgeDetectionSettings.showSectionMapSection));
            showDiscontinuitySection = serializedObject.FindProperty(nameof(EdgeDetectionSettings.showDiscontinuitySection));
            showOutlineSection = serializedObject.FindProperty(nameof(EdgeDetectionSettings.showOutlineSection));

            injectionPoint = serializedObject.FindProperty("injectionPoint");
            showInSceneView = serializedObject.FindProperty("showInSceneView");

            // Debugging.
            debugView = serializedObject.FindProperty("debugView");
            debugSectionsChannels = serializedObject.FindProperty(nameof(EdgeDetectionSettings.debugSectionsChannels));
            debugPerceptualSections = serializedObject.FindProperty(nameof(EdgeDetectionSettings.debugPerceptualSections));

            // Section map.
            sectionMapFormat = serializedObject.FindProperty(nameof(EdgeDetectionSettings.sectionMapPrecision));
            sectionMapClearValue = serializedObject.FindProperty(nameof(EdgeDetectionSettings.sectionMapClearValue));
            sectionRenderingLayer = serializedObject.FindProperty(nameof(EdgeDetectionSettings.SectionRenderingLayer));
            maskRenderingLayer = serializedObject.FindProperty(nameof(EdgeDetectionSettings.SectionMaskRenderingLayer));
            maskInfluence = serializedObject.FindProperty(nameof(EdgeDetectionSettings.maskInfluence));
            objectId = serializedObject.FindProperty(nameof(EdgeDetectionSettings.objectId));
            particles = serializedObject.FindProperty(nameof(EdgeDetectionSettings.particles));
            sectionMapInput = serializedObject.FindProperty(nameof(EdgeDetectionSettings.sectionMapInput));
            sectionTexture = serializedObject.FindProperty(nameof(EdgeDetectionSettings.sectionTexture));
            sectionTextureUvSet = serializedObject.FindProperty(nameof(EdgeDetectionSettings.sectionTextureUvSet));
            vertexColorChannel = serializedObject.FindProperty(nameof(EdgeDetectionSettings.vertexColorChannel));
            additionalSectionPasses = serializedObject.FindProperty(nameof(EdgeDetectionSettings.additionalSectionPasses));
            additionalSectionPassesList = new ReorderableList(serializedObject, additionalSectionPasses, true, true, true, true)
            {
                drawHeaderCallback = rect => { EditorGUI.LabelField(rect, "Additional Section Passes"); },
                drawElementCallback = (rect, index, _, _) =>
                {
                    var element = additionalSectionPasses.GetArrayElementAtIndex(index);
                    DrawOverride(rect, element);
                },
                elementHeightCallback = _ => GetElementHeight()
            };
            sectionRenderQueue = serializedObject.FindProperty(nameof(EdgeDetectionSettings.sectionRenderQueue));

            // Discontinuity.
            discontinuityInput = serializedObject.FindProperty(nameof(EdgeDetectionSettings.discontinuityInput));
            depthSensitivity = serializedObject.FindProperty(nameof(EdgeDetectionSettings.depthSensitivity));
            depthDistanceModulation = serializedObject.FindProperty(nameof(EdgeDetectionSettings.depthDistanceModulation));
            grazingAngleMaskPower = serializedObject.FindProperty(nameof(EdgeDetectionSettings.grazingAngleMaskPower));
            grazingAngleMaskHardness = serializedObject.FindProperty(nameof(EdgeDetectionSettings.grazingAngleMaskHardness));
            normalSensitivity = serializedObject.FindProperty(nameof(EdgeDetectionSettings.normalSensitivity));
            luminanceSensitivity = serializedObject.FindProperty(nameof(EdgeDetectionSettings.luminanceSensitivity));

            // Distortion.
            distortEdges = serializedObject.FindProperty(nameof(EdgeDetectionSettings.distortEdges));
            distortionTexture = serializedObject.FindProperty(nameof(EdgeDetectionSettings.distortionTexture));
            distortionScale = serializedObject.FindProperty(nameof(EdgeDetectionSettings.distortionScale));
            distortionStrength = serializedObject.FindProperty(nameof(EdgeDetectionSettings.distortionStrength));
            distortionStepRate = serializedObject.FindProperty(nameof(EdgeDetectionSettings.distortionStepRate));
            distortionThicknessInfluence = serializedObject.FindProperty(nameof(EdgeDetectionSettings.distortionThicknessInfluence));

            // Break up.
            breakUpEdges = serializedObject.FindProperty(nameof(EdgeDetectionSettings.breakUpEdges));
            breakUpAmount = serializedObject.FindProperty(nameof(EdgeDetectionSettings.breakUpNoiseAmount));
            breakUpScale = serializedObject.FindProperty(nameof(EdgeDetectionSettings.breakUpNoiseScale));

            // Outline.
            kernel = serializedObject.FindProperty(nameof(EdgeDetectionSettings.kernel));
            outlineThickness = serializedObject.FindProperty(nameof(EdgeDetectionSettings.outlineThickness));
            scaleWithDistance = serializedObject.FindProperty(nameof(EdgeDetectionSettings.scaleWithDistance));
            distanceScaleStart = serializedObject.FindProperty(nameof(EdgeDetectionSettings.distanceScaleStart));
            distanceScaleDistance = serializedObject.FindProperty(nameof(EdgeDetectionSettings.distanceScaleDistance));
            distanceScaleMin = serializedObject.FindProperty(nameof(EdgeDetectionSettings.distanceScaleMin));
            scaleWithResolution = serializedObject.FindProperty(nameof(EdgeDetectionSettings.scaleWithResolution));
            referenceResolution = serializedObject.FindProperty(nameof(EdgeDetectionSettings.referenceResolution));
            customReferenceResolution = serializedObject.FindProperty(nameof(EdgeDetectionSettings.customResolution));
            backgroundColor = serializedObject.FindProperty(nameof(EdgeDetectionSettings.backgroundColor));
            outlineColor = serializedObject.FindProperty(nameof(EdgeDetectionSettings.outlineColor));
            overrideColorInShadow = serializedObject.FindProperty(nameof(EdgeDetectionSettings.overrideColorInShadow));
            outlineColorShadow = serializedObject.FindProperty(nameof(EdgeDetectionSettings.outlineColorShadow));
            fill = serializedObject.FindProperty(nameof(EdgeDetectionSettings.fill));
            fillColor = serializedObject.FindProperty(nameof(EdgeDetectionSettings.fillColor));
            fadeByDistance = serializedObject.FindProperty(nameof(EdgeDetectionSettings.fadeByDistance));
            distanceFadeStart = serializedObject.FindProperty(nameof(EdgeDetectionSettings.distanceFadeStart));
            distanceFadeDistance = serializedObject.FindProperty(nameof(EdgeDetectionSettings.distanceFadeDistance));
            distanceFadeColor = serializedObject.FindProperty(nameof(EdgeDetectionSettings.distanceFadeColor));
            fadeByHeight = serializedObject.FindProperty(nameof(EdgeDetectionSettings.fadeByHeight));
            heightFadeStart = serializedObject.FindProperty(nameof(EdgeDetectionSettings.heightFadeStart));
            heightFadeDistance = serializedObject.FindProperty(nameof(EdgeDetectionSettings.heightFadeDistance));
            heightFadeColor = serializedObject.FindProperty(nameof(EdgeDetectionSettings.heightFadeColor));
            blendMode = serializedObject.FindProperty(nameof(EdgeDetectionSettings.blendMode));
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Edge Detection", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(injectionPoint, EditorUtils.CommonStyles.InjectionPoint);
            EditorGUILayout.PropertyField(showInSceneView, EditorUtils.CommonStyles.ShowInSceneView);
            EditorGUILayout.PropertyField(debugView, EditorUtils.CommonStyles.DebugStage);
            switch ((DebugView) debugView.intValue)
            {
                case DebugView.None:
                    break;
                case DebugView.Depth:
                    if (!((DiscontinuityInput) discontinuityInput.intValue).HasFlag(DiscontinuityInput.Depth))
                    {
                        EditorGUILayout.HelpBox("Depth is not configured as a source. No edges will be detected based on scene depth.", MessageType.Warning);
                    }
                    break;
                case DebugView.Normals:
                    if (!((DiscontinuityInput) discontinuityInput.intValue).HasFlag(DiscontinuityInput.Normals))
                    {
                        EditorGUILayout.HelpBox("Normals are not configured as a source. No edges will be detected based on scene normals.", MessageType.Warning);
                    }
                    break;
                case DebugView.Luminance:
                    if (!((DiscontinuityInput) discontinuityInput.intValue).HasFlag(DiscontinuityInput.Luminance))
                    {
                        EditorGUILayout.HelpBox("Luminance is not configured as a source. No edges will be detected based on scene luminance.", MessageType.Warning);
                    }
                    break;
                case DebugView.Sections:
                    if (!((DiscontinuityInput) discontinuityInput.intValue).HasFlag(DiscontinuityInput.Sections))
                    {
                        EditorGUILayout.HelpBox("Sections are not configured as a source. No edges will be detected based on section map.", MessageType.Warning);
                    }
                    else
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.PropertyField(debugSectionsChannels, EditorUtils.CommonStyles.DebugSectionsChannels);
                        EditorGUILayout.PropertyField(debugPerceptualSections, EditorUtils.CommonStyles.DebugPerceptualSections);
                        EditorGUILayout.HelpBox("Blue = mask, Green = fill", MessageType.Info);
                        if (debugPerceptualSections.boolValue)
                        {
                            EditorGUILayout.HelpBox("When using perceptual mode, only the R channel will be visualized", MessageType.Info);
                        }
                        EditorGUI.indentLevel--;
                    }
                    break;
            }
            EditorGUILayout.Space();
            CoreEditorUtils.DrawSplitter();
            serializedObject.ApplyModifiedProperties();

            EditorUtils.SectionGUI("Section Map", showSectionMapSection, () =>
            {
                if (!((DiscontinuityInput) discontinuityInput.intValue).HasFlag(DiscontinuityInput.Sections) &&
                    maskRenderingLayer.intValue == 0)
                {
                    EditorGUILayout.HelpBox("The section map will not be generated since sections are not marked as an edge detection source and no rendering layers are being masked/excluded.", MessageType.Info);
                    EditorGUILayout.Space();
                }
                else
                { EditorGUILayout.HelpBox("The section map is being generated according to your current settings.", MessageType.Info);
                    EditorGUILayout.Space();
                    
                }
                EditorGUILayout.PropertyField(sectionMapFormat, EditorUtils.CommonStyles.SectionMapFormat);
                EditorGUILayout.PropertyField(sectionMapClearValue, EditorUtils.CommonStyles.SectionMapClearValue);
                EditorGUILayout.PropertyField(sectionRenderingLayer, EditorUtils.CommonStyles.SectionLayer);
                EditorGUILayout.PropertyField(sectionRenderQueue, EditorUtils.CommonStyles.SectionRenderQueue);
                EditorGUILayout.PropertyField(sectionMapInput, EditorUtils.CommonStyles.SectionMapInput);
                EditorGUI.indentLevel++;
                if ((SectionMapInput) sectionMapInput.intValue == SectionMapInput.VertexColors)
                {
                    EditorGUILayout.PropertyField(vertexColorChannel, EditorUtils.CommonStyles.VertexColorChannel);
                }

                if ((SectionMapInput) sectionMapInput.intValue == SectionMapInput.SectionTexture)
                {
                    EditorGUILayout.PropertyField(sectionTexture, EditorUtils.CommonStyles.SectionTexture);
                    EditorGUILayout.PropertyField(sectionTextureUvSet, EditorUtils.CommonStyles.SectionTextureUVSet);
                    EditorGUILayout.PropertyField(vertexColorChannel, EditorUtils.CommonStyles.SectionTextureChannel);
                }
                EditorGUI.indentLevel--;

                using (new EditorGUI.DisabledScope((SectionMapInput) sectionMapInput.intValue == SectionMapInput.Custom))
                {
                    EditorGUILayout.PropertyField(objectId, EditorUtils.CommonStyles.ObjectId);
                    EditorGUILayout.PropertyField(particles, EditorUtils.CommonStyles.Particles);
                }
                EditorGUILayout.Space();
                if ((SectionMapInput) sectionMapInput.intValue == SectionMapInput.Custom)
                {
                    const string keywordMessage = "Custom Section Map: Use the _SECTION_PASS keyword to render directly to the section map.";
                    EditorGUILayout.HelpBox(keywordMessage, MessageType.Info);
                }

                EditorGUILayout.Space();
                additionalSectionPassesList.DoLayoutList();
            }, serializedObject);

            EditorUtils.SectionGUI("Edge Detection", showDiscontinuitySection, () =>
            {
                EditorGUI.BeginChangeCheck();
                var discontinuityInputValue = (DiscontinuityInput) discontinuityInput.intValue;
                discontinuityInputValue = (DiscontinuityInput) EditorGUILayout.EnumFlagsField(EditorUtils.CommonStyles.DiscontinuityInput, discontinuityInputValue);
                discontinuityInput.intValue = (int) discontinuityInputValue;
                if (EditorGUI.EndChangeCheck())
                {
                    discontinuityInput.intValue = (int) discontinuityInputValue;
                    discontinuityInput.serializedObject.ApplyModifiedProperties();
                }

                // Exclusions
                var hasSections = ((DiscontinuityInput) discontinuityInput.intValue).HasFlag(DiscontinuityInput.Sections);
                if (hasSections)
                {
                    EditorGUILayout.HelpBox("When sections are enabled as a discontinuity source, they can not be used to mask out other discontinuity sources.", MessageType.Info);
                }
                using (new EditorGUI.DisabledScope(hasSections))
                {
                    EditorGUILayout.PropertyField(maskRenderingLayer, EditorUtils.CommonStyles.MaskLayer);
                    EditorGUI.indentLevel++;
                    var maskInfluenceValue = (MaskInfluence) maskInfluence.intValue;
                    maskInfluenceValue = (MaskInfluence) EditorGUILayout.EnumFlagsField(EditorUtils.CommonStyles.MaskInfluence, maskInfluenceValue);
                    maskInfluence.intValue = (int) maskInfluenceValue;
                    EditorGUI.indentLevel--;
                    EditorGUILayout.Space();
                }

                using (new EditorGUI.DisabledScope(!discontinuityInputValue.HasFlag(DiscontinuityInput.Depth)))
                {
                    EditorGUILayout.LabelField("Depth", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(depthSensitivity, EditorUtils.CommonStyles.Sensitivity);
                    EditorGUILayout.PropertyField(depthDistanceModulation, EditorUtils.CommonStyles.DepthDistanceModulation);
                    EditorGUILayout.PropertyField(grazingAngleMaskPower, EditorUtils.CommonStyles.GrazingAngleMaskPower);
                    EditorGUILayout.PropertyField(grazingAngleMaskHardness, EditorUtils.CommonStyles.GrazingAngleMaskHardness);
                }
                EditorGUILayout.Space();

                using (new EditorGUI.DisabledScope(!discontinuityInputValue.HasFlag(DiscontinuityInput.Normals)))
                {
                    EditorGUILayout.LabelField("Normals", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(normalSensitivity, EditorUtils.CommonStyles.Sensitivity);
                }
                EditorGUILayout.Space();

                using (new EditorGUI.DisabledScope(!discontinuityInputValue.HasFlag(DiscontinuityInput.Luminance)))
                {
                    EditorGUILayout.LabelField("Luminance", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(luminanceSensitivity, EditorUtils.CommonStyles.Sensitivity);
                }
                EditorGUILayout.Space();
            }, serializedObject);

            EditorUtils.SectionGUI("Outline", showOutlineSection, () =>
            {
                EditorGUILayout.LabelField("Sampling", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(kernel, EditorUtils.CommonStyles.Kernel);
                if ((Kernel) kernel.intValue == Kernel.Circular)
                {
                    EditorGUILayout.HelpBox("The circular kernel will only pick up on differences within the section map.", MessageType.Info);
                }
                EditorGUILayout.PropertyField(outlineThickness, EditorUtils.CommonStyles.OutlineThickness);
                EditorGUILayout.PropertyField(scaleWithDistance, EditorUtils.CommonStyles.ScaleWithDistance);
                if (scaleWithDistance.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(distanceScaleStart, EditorUtils.CommonStyles.DistanceScaleStart);
                    EditorGUILayout.PropertyField(distanceScaleDistance, EditorUtils.CommonStyles.DistanceScaleDistance);
                    EditorGUILayout.PropertyField(distanceScaleMin, EditorUtils.CommonStyles.DistanceScaleMin);
                    EditorGUI.indentLevel--;
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

                EditorGUILayout.Space();

                EditorGUILayout.LabelField("Hand-drawn Effects (experimental)", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(distortEdges, EditorUtils.CommonStyles.Distort);
                if (distortEdges.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.HelpBox("Line distortion requires a 3D noise texture. You can use the one included in Packages/Linework/Runtime/EdgeDetection/Textures.",
                        MessageType.Info);
                    EditorGUILayout.PropertyField(distortionTexture, EditorUtils.CommonStyles.DistortionTexture);
                    EditorGUILayout.PropertyField(distortionScale, EditorUtils.CommonStyles.DistortionScale);
                    EditorGUILayout.PropertyField(distortionStrength, EditorUtils.CommonStyles.DistortionStrength);
                    EditorGUILayout.PropertyField(distortionStepRate, EditorUtils.CommonStyles.DistortionStepRate);
                    EditorGUILayout.PropertyField(distortionThicknessInfluence, EditorUtils.CommonStyles.DistortionThicknessInfluence);
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.PropertyField(breakUpEdges, EditorUtils.CommonStyles.BreakUp);
                if (breakUpEdges.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(breakUpAmount, EditorUtils.CommonStyles.BreakUpAmount);
                    EditorGUILayout.PropertyField(breakUpScale, EditorUtils.CommonStyles.BreakUpScale);
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.Space();

                EditorGUILayout.LabelField("Colors", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(outlineColor, EditorUtils.CommonStyles.EdgeColor);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(overrideColorInShadow, EditorUtils.CommonStyles.OverrideShadow);
                if (overrideColorInShadow.boolValue) EditorGUILayout.PropertyField(outlineColorShadow, GUIContent.none);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.PropertyField(backgroundColor, EditorUtils.CommonStyles.BackgroundColor);
                EditorGUILayout.PropertyField(fill, EditorUtils.CommonStyles.Fill);
                if (fill.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(fillColor, EditorUtils.CommonStyles.OutlineFillColor);
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.PropertyField(fadeByDistance, EditorUtils.CommonStyles.FadeByDistance);
                if (fadeByDistance.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(distanceFadeStart, EditorUtils.CommonStyles.FadeStart);
                    EditorGUILayout.PropertyField(distanceFadeDistance, EditorUtils.CommonStyles.FadeDistance);
                    EditorGUILayout.PropertyField(distanceFadeColor, EditorUtils.CommonStyles.FadeColor);
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.PropertyField(fadeByHeight, EditorUtils.CommonStyles.FadeByHeight);
                if (fadeByHeight.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(heightFadeStart, EditorUtils.CommonStyles.FadeStart);
                    EditorGUILayout.PropertyField(heightFadeDistance, EditorUtils.CommonStyles.FadeDistance);
                    EditorGUILayout.PropertyField(heightFadeColor, EditorUtils.CommonStyles.FadeColor);
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.PropertyField(blendMode, EditorUtils.CommonStyles.OutlineBlendMode);
                EditorGUILayout.Space();
            }, serializedObject);

            serializedObject.ApplyModifiedProperties();
        }

        private static void DrawOverride(Rect rect, SerializedProperty element)
        {
            var renderingLayerProperty = element.FindPropertyRelative(nameof(SectionPass.RenderingLayer));
            var materialProperty = element.FindPropertyRelative(nameof(SectionPass.customSectionMaterial));

            var renderingLayerWidth = rect.width * 0.4f;
            var materialWidth = rect.width * 0.6f;

            var renderingLayerRect = new Rect(rect.x, rect.y, renderingLayerWidth, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(renderingLayerRect, renderingLayerProperty, GUIContent.none);

            var materialRect = new Rect(rect.x + renderingLayerWidth + 5, rect.y, materialWidth, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(materialRect, materialProperty, GUIContent.none);
        }

        private static float GetElementHeight()
        {
            return EditorGUIUtility.singleLineHeight + 4;
        }
    }
}