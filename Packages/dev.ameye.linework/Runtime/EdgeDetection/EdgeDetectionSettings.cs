#if !UNITY_6000_0_OR_NEWER
using Linework.Common.Attributes;
#endif
using System;
using System.Collections.Generic;
using Linework.Common.Utils;
using UnityEngine;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
using ShaderKeywordFilter = UnityEditor.ShaderKeywordFilter;
#endif

namespace Linework.EdgeDetection
{
    [CreateAssetMenu(fileName = "Edge Detection Settings", menuName = "Linework/Edge Detection Settings")]
    [Icon("Packages/dev.ameye.linework/Editor/Common/Icons/d_EdgeDetection.png")]
    public class EdgeDetectionSettings : ScriptableObject
    {
        internal Action OnSettingsChanged;

        [SerializeField] private InjectionPoint injectionPoint = InjectionPoint.AfterRenderingPostProcessing;
        [SerializeField] private bool showInSceneView = true;
        
        // Debugging
        [SerializeField] private DebugView debugView;
        public DebugSectionsChannels debugSectionsChannels = DebugSectionsChannels.R | DebugSectionsChannels.G | DebugSectionsChannels.B;
#if UNITY_EDITOR
        [ShaderKeywordFilter.SelectIf(true, overridePriority: true, keywordNames: ShaderFeature.DebugSectionsPerceptual)]
#endif
        public bool debugPerceptualSections;
        public DiscontinuityInput discontinuityInput = DiscontinuityInput.Depth | DiscontinuityInput.Normals | DiscontinuityInput.Luminance | DiscontinuityInput.Sections;
        [Range(0.0f, 1.0f)] public float depthSensitivity = 1.0f;
        [Range(0.0f, 1.0f)] public float depthDistanceModulation = 0.4f;
        [Range(0.0f, 1.0f)] public float grazingAngleMaskPower = 0.2f;
        [Range(1.0f, 30.0f)] public float grazingAngleMaskHardness = 1.0f;
        [Range(0.0f, 1.0f)] public float normalSensitivity = 0.4f;
        [Range(0.0f, 1.0f)] public float luminanceSensitivity = 0.3f;
        
        // Section map.
        public bool objectId = true;
        public bool particles = false;
#if UNITY_EDITOR
        [ShaderKeywordFilter.RemoveIf(SectionMapInput.None, keywordNames: new[] { ShaderFeature.InputTexture, ShaderFeature.InputVertexColor, ShaderFeature.VertexColorChannelR,  ShaderFeature.VertexColorChannelG, ShaderFeature.VertexColorChannelB, ShaderFeature.VertexColorChannelA, ShaderFeature.TextureChannelR, ShaderFeature.TextureChannelG, ShaderFeature.TextureChannelB, ShaderFeature.TextureChannelA })]
        [ShaderKeywordFilter.SelectIf(SectionMapInput.SectionTexture, keywordNames: new[] { ShaderFeature.InputTexture })]
        [ShaderKeywordFilter.SelectIf(SectionMapInput.VertexColors, keywordNames: new[] { ShaderFeature.InputVertexColor })]
        [ShaderKeywordFilter.RemoveIf(SectionMapInput.Custom, keywordNames: new[] { ShaderFeature.InputTexture, ShaderFeature.InputVertexColor, ShaderFeature.VertexColorChannelR,  ShaderFeature.VertexColorChannelG, ShaderFeature.VertexColorChannelB, ShaderFeature.VertexColorChannelA, ShaderFeature.TextureChannelR, ShaderFeature.TextureChannelG, ShaderFeature.TextureChannelB, ShaderFeature.TextureChannelA })]
#endif
        public SectionMapInput sectionMapInput = SectionMapInput.None;
        public Texture2D sectionTexture;
        public UVSet sectionTextureUvSet;
        public Channel sectionTextureChannel;
        public Channel vertexColorChannel;
        public SectionMapPrecision sectionMapPrecision = SectionMapPrecision.Bits16;
        [Range(0, 256)] public int sectionMapClearValue = 1;
        public OutlineRenderQueue sectionRenderQueue = OutlineRenderQueue.Opaque;
        public List<SectionPass> additionalSectionPasses = new();
#if UNITY_6000_0_OR_NEWER
        public RenderingLayerMask SectionRenderingLayer = RenderingLayerMask.defaultRenderingLayerMask;
#else
        [RenderingLayerMask]
        public uint SectionRenderingLayer = 1;
#endif
#if UNITY_6000_0_OR_NEWER
        public RenderingLayerMask SectionMaskRenderingLayer = 0;
#else
        [RenderingLayerMask]
        public uint SectionMaskRenderingLayer = 0;
#endif
#if UNITY_EDITOR
        [ShaderKeywordFilter.SelectIf(MaskInfluence.Depth,     keywordNames: ShaderFeature.DepthMask)]
        [ShaderKeywordFilter.SelectIf(MaskInfluence.Normals,   keywordNames: ShaderFeature.NormalsMask)]
        [ShaderKeywordFilter.SelectIf(MaskInfluence.Luminance, keywordNames: ShaderFeature.LuminanceMask)]
#endif
        public MaskInfluence maskInfluence = MaskInfluence.Depth | MaskInfluence.Normals | MaskInfluence.Luminance;
        
        // Sampling.
#if UNITY_EDITOR
        [ShaderKeywordFilter.SelectIf(Kernel.RobertsCross, keywordNames: new[] { ShaderFeature.OperatorCross })]
        [ShaderKeywordFilter.SelectIf(Kernel.Sobel,        keywordNames: new[] { ShaderFeature.OperatorSobel })]
        [ShaderKeywordFilter.SelectIf(Kernel.Circular,     keywordNames: new[] { ShaderFeature.OperatorCircular })]
#endif
        public Kernel kernel = Kernel.RobertsCross;
        [Range(0, 15)] public int outlineThickness = 3;
#if UNITY_EDITOR
        [ShaderKeywordFilter.SelectIf(true, overridePriority: true, keywordNames: ShaderFeature.ScaleWithDistance)]
#endif
        public bool scaleWithDistance;
        [Range(0.0f, 200.0f)] public float distanceScaleStart = 100.0f;
        [Range(0.1f, 100.0f)] public float distanceScaleDistance = 10.0f;
        [Range(0.1f, 1.0f)] public float distanceScaleMin = 0.1f;
#if UNITY_EDITOR
        [ShaderKeywordFilter.SelectIf(true, overridePriority: true, keywordNames: ShaderFeature.ScaleWithResolution)]
#endif
        public bool scaleWithResolution;
        public Resolution referenceResolution;
        public float customResolution;
        
        // Distortion.
#if UNITY_EDITOR
        [ShaderKeywordFilter.SelectIf(true, overridePriority: true, keywordNames: ShaderFeature.Distortion)]
#endif
        public bool distortEdges = false;
        [Range(0, 60)] public int distortionStepRate;
        public Texture3D distortionTexture;
        [Range(0.0f, 0.2f)] public float distortionScale = 0.2f;
        [Range(0.0f, 1.0f)] public float distortionStrength = 0.1f;
        [Range(0.0f, 10.0f)] public float distortionThicknessInfluence = 0.0f;
        
        // Break up.
#if UNITY_EDITOR
        [ShaderKeywordFilter.SelectIf(true, overridePriority: true, keywordNames: ShaderFeature.Breakup)]
#endif
        public bool breakUpEdges = false;
        [Range(0.0f, 20.0f)] public float breakUpNoiseScale = 10.0f;
        [Range(0.0f, 1.0f)] public float breakUpNoiseAmount = 0.0f;
        
        // Colors.
        [ColorUsage(true, true)] public Color backgroundColor = Color.clear;
        [ColorUsage(true, true)] public Color outlineColor = Color.black;
#if UNITY_EDITOR
        [ShaderKeywordFilter.SelectIf(true, overridePriority: true, keywordNames: ShaderFeature.OverrideShadow)]
#endif
        public bool overrideColorInShadow;
        [ColorUsage(true, true)] public Color outlineColorShadow = Color.white;
#if UNITY_EDITOR
        [ShaderKeywordFilter.SelectIf(true, overridePriority: true, keywordNames: ShaderFeature.Fill)]
#endif
        public bool fill;
        [ColorUsage(true, true)] public Color fillColor = Color.black;
#if UNITY_EDITOR
        [ShaderKeywordFilter.SelectIf(true, overridePriority: true, keywordNames: ShaderFeature.FadeByDistance)]
#endif
        public bool fadeByDistance;
        [ColorUsage(true, true)] public Color distanceFadeColor = Color.clear;
        [Range(0.0f, 200.0f)] public float distanceFadeStart = 100.0f;
        [Range(0.1f, 20.0f)] public float distanceFadeDistance = 10.0f;
#if UNITY_EDITOR
        [ShaderKeywordFilter.SelectIf(true, overridePriority: true, keywordNames: ShaderFeature.FadeByHeight)]
#endif
        public bool fadeByHeight;
        [ColorUsage(true, true)] public Color heightFadeColor = Color.clear;
        [Range(0.0f, 2.0f)] public float heightFadeStart = 1.0f;
        [Range(0.01f, 2.0f)] public float heightFadeDistance = 0.5f;
        public BlendingMode blendMode;
        
        public InjectionPoint InjectionPoint => injectionPoint;
        public bool ShowInSceneView => showInSceneView;
        public DebugView DebugView => debugView;

        public bool showSectionMapSection;
        public bool showDiscontinuitySection;
        public bool showOutlineSection;

        private void OnValidate()
        {
#if UNITY_EDITOR
            if (Application.isPlaying)
                return;
            OnSettingsChanged?.Invoke();
#endif
        }

        private void OnDestroy()
        {
            OnSettingsChanged = null;
        }

#if UNITY_EDITOR
        private class OnDestroyProcessor : AssetModificationProcessor
        {
            private static readonly Type Type = typeof(EdgeDetectionSettings);
            private const string FileEnding = ".asset";

            public static AssetDeleteResult OnWillDeleteAsset(string path, RemoveAssetOptions _)
            {
                if (!path.EndsWith(FileEnding))
                    return AssetDeleteResult.DidNotDelete;

                var assetType = AssetDatabase.GetMainAssetTypeAtPath(path);
                if (assetType == null || assetType != Type && !assetType.IsSubclassOf(Type)) return AssetDeleteResult.DidNotDelete;
                var asset = AssetDatabase.LoadAssetAtPath<EdgeDetectionSettings>(path);
                asset.OnDestroy();

                return AssetDeleteResult.DidNotDelete;
            }
        }
#endif
    }
}