using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Linework.EdgeDetection
{
    [Serializable]
    public sealed class ShaderResources
    {
        public Shader section;
        public Shader sectionMask;
        public Shader outline;

        public ShaderResources Load()
        {
            section = Shader.Find(ShaderPath.Section);
            sectionMask = Shader.Find(ShaderPath.SectionMask);
            outline = Shader.Find(ShaderPath.Outline);
            return this;
        }
    }
    
    static class ShaderPath
    {
        public const string Outline = "Hidden/Outlines/Edge Detection/Outline";
        public const string Section = "Hidden/Outlines/Edge Detection/Section";
        public const string SectionMask = "Hidden/Outlines/Edge Detection/Section Mask";
    }

    static class Keyword
    {
        public static readonly GlobalKeyword ScreenSpaceOcclusion = GlobalKeyword.Create("_SCREEN_SPACE_OCCLUSION");
        public static readonly GlobalKeyword SectionPass = GlobalKeyword.Create("_SECTION_PASS");
    }

    static class ShaderPassName
    {
        public const string Section = "Section (Edge Detection)";
        public const string Outline = "Outline (Edge Detection)";
    }
    
    static class ShaderPropertyId
    {
        // Line appearance.
        public static readonly int BackgroundColor = Shader.PropertyToID("_BackgroundColor");
        public static readonly int OutlineColorShadow = Shader.PropertyToID("_OutlineColorShadow");
        public static readonly int FillColor = Shader.PropertyToID("_FillColor");
        public static readonly int OutlineThickness = Shader.PropertyToID("_OutlineThickness");
        public static readonly int DistanceScaleStart = Shader.PropertyToID("_DistanceScaleStart");
        public static readonly int DistanceScaleDistance = Shader.PropertyToID("_DistanceScaleDistance");
        public static readonly int DistanceScaleMin = Shader.PropertyToID("_DistanceScaleMin");
        public static readonly int ReferenceResolution = Shader.PropertyToID("_ReferenceResolution");
        public static readonly int DistanceFadeStart = Shader.PropertyToID("_DistanceFadeStart");
        public static readonly int DistanceFadeDistance = Shader.PropertyToID("_DistanceFadeDistance");
        public static readonly int DistanceFadeColor = Shader.PropertyToID("_DistanceFadeColor");
        public static readonly int HeightFadeStart = Shader.PropertyToID("_HeightFadeStart");
        public static readonly int HeightFadeDistance = Shader.PropertyToID("_HeightFadeDistance");
        public static readonly int HeightFadeColor = Shader.PropertyToID("_HeightFadeColor");
        
        // Distortion.
        public static readonly int DistortionTexture = Shader.PropertyToID("_DistortionTexture");
        public static readonly int DistortionScale = Shader.PropertyToID("_DistortionScale");
        public static readonly int DistortionThicknessInfluence = Shader.PropertyToID("_DistortionThicknessInfluence");
        public static readonly int DistortionStrength = Shader.PropertyToID("_DistortionStrength");
        public static readonly int DistortionStepRate = Shader.PropertyToID("_DistortionStepRate");
        
        // Break up.
        public static readonly int BreakupScale = Shader.PropertyToID("_BreakUpScale");
        public static readonly int BreakupAmount = Shader.PropertyToID("_BreakUpAmount");
        
        // Edge detection.
        public static readonly int DepthSensitivity = Shader.PropertyToID("_DepthSensitivity");
        public static readonly int DepthDistanceModulation = Shader.PropertyToID("_DepthDistanceModulation");
        public static readonly int GrazingAngleMaskPower = Shader.PropertyToID("_GrazingAngleMaskPower");
        public static readonly int GrazingAngleMaskHardness = Shader.PropertyToID("_GrazingAngleMaskHardness");
        public static readonly int NormalSensitivity = Shader.PropertyToID("_NormalSensitivity");
        public static readonly int LuminanceSensitivity = Shader.PropertyToID("_LuminanceSensitivity");
        public static readonly int CameraSectioningTexture = Shader.PropertyToID("_CameraSectioningTexture");
  
        // Section map.
        public static readonly int SectionTexture = Shader.PropertyToID("_SectionTexture");
    }

    static class Buffer
    {
        public const string Section = "_SectionBuffer";
    }
    
    [Flags]
    public enum DiscontinuityInput
    {
        None = 0,
        Depth = 1 << 0,
        Normals = 1 << 1,
        Luminance = 1 << 2,
        Sections = 1 << 3,
        All = ~0,
    }
    
    [Flags]
    public enum DebugSectionsChannels
    {
        Nothing = 0,
        R = 1 << 0,
        G = 1 << 1,
        B = 1 << 2,
        All = ~0,
    }
    
    [Flags]
    public enum MaskInfluence
    {
        Nothing = 0,
        Depth = 1 << 0,
        Normals = 1 << 1,
        Luminance = 1 << 2,
        All = ~0,
    }
    
    public enum DebugView
    {
        None,
        [InspectorName("Depth")]
        Depth,
        [InspectorName("Normals")]
        Normals,
        [InspectorName("Luminance")]
        Luminance,
        [InspectorName("Sections")]
        Sections
    }
    
    static class ShaderFeature
    {
        public const string DepthDiscontinuity = "DEPTH";
        public const string NormalDiscontinuity = "NORMALS";
        public const string LuminanceDiscontinuity = "LUMINANCE";
        public const string SectionDiscontinuity = "SECTIONS";

        public const string TextureUV0 = "TEXTURE_UV_SET_UV0";
        public const string TextureUV1 = "TEXTURE_UV_SET_UV1";
        public const string TextureUV2 = "TEXTURE_UV_SET_UV2";
        public const string TextureUV3 = "TEXTURE_UV_SET_UV3";
        
        public const string VertexColorChannelR = "VERTEX_COLOR_CHANNEL_R";
        public const string VertexColorChannelG = "VERTEX_COLOR_CHANNEL_G";
        public const string VertexColorChannelB = "VERTEX_COLOR_CHANNEL_B";
        public const string VertexColorChannelA = "VERTEX_COLOR_CHANNEL_A";
        
        public const string TextureChannelR = "TEXTURE_CHANNEL_R";
        public const string TextureChannelG = "TEXTURE_CHANNEL_G";
        public const string TextureChannelB = "TEXTURE_CHANNEL_B";
        public const string TextureChannelA = "TEXTURE_CHANNEL_A";

        public const string OperatorCross = "OPERATOR_CROSS";
        public const string OperatorSobel = "OPERATOR_SOBEL";
        public const string OperatorCircular = "OPERATOR_CIRCULAR";

        public const string Distortion = "DISTORTION";
        public const string Breakup = "BREAKUP";

        public const string DebugDepth = "DEBUG_DEPTH";
        public const string DebugNormals = "DEBUG_NORMALS";
        public const string DebugLuminance = "DEBUG_LUMINANCE";
        public const string DebugSections = "DEBUG_SECTIONS";
        public const string DebugSectionsPerceptual = "DEBUG_SECTIONS_PERCEPTUAL";
        public const string OverrideShadow = "OVERRIDE_SHADOW";
        public const string ScaleWithDistance = "SCALE_WITH_DISTANCE";
        public const string ScaleWithResolution = "SCALE_WITH_RESOLUTION";
        public const string FadeByDistance = "FADE_BY_DISTANCE";
        public const string FadeByHeight = "FADE_BY_HEIGHT";
        public const string DepthMask = "DEPTH_MASK";
        public const string NormalsMask = "NORMALS_MASK";
        public const string LuminanceMask = "LUMINANCE_MASK";
        public const string Fill = "FILL";
        
        public const string DebugSectionsR = "DEBUG_SECTIONS_R";
        public const string DebugSectionsG = "DEBUG_SECTIONS_G";
        public const string DebugSectionsB = "DEBUG_SECTIONS_B";

        public const string ObjectId = "OBJECT_ID";
        public const string Particles = "PARTICLES";
        public const string InputVertexColor = "INPUT_VERTEX_COLOR";
        public const string InputTexture = "INPUT_TEXTURE";
    }
    
    public enum SectionMapInput
    {
        [InspectorName("Solid Color")]
        None,
        [InspectorName("Vertex Color")]
        VertexColors,
        [InspectorName("Section Texture")]
        SectionTexture,
        [InspectorName("Custom")]
        Custom
    }
    
    public enum Kernel
    {
        RobertsCross,
        Sobel,
        [InspectorName("Circular (sections only)")]
        Circular
    }
    
    public enum UVSet
    {
        UV0,
        UV1,
        UV2,
        UV3
    }
    
    public enum Resolution
    {
        [InspectorName("480px")]
        _480,
        [InspectorName("720px")]
        _720,
        [InspectorName("1080px")]
        _1080,
        [InspectorName("Custom")]
        Custom
    }

    public enum SectionMapPrecision
    {
        [InspectorName("8-bit")]
        Bits8,
        [InspectorName("16-bit")]
        Bits16
    }
}