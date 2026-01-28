using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Linework.SoftOutline
{
    [Serializable]
    public sealed class ShaderResources
    {
        public Shader mask;
        public Shader silhouette;
        public Shader silhouetteInstanced;
        public Shader boxBlur;
        public Shader gaussianBlur;
        public Shader kawaseBlur;
        public Shader dilate;
        public Shader outline;

        public ShaderResources Load()
        {
            mask = Shader.Find(ShaderPath.Mask);
            silhouette = Shader.Find(ShaderPath.Silhouette);
            silhouetteInstanced = Shader.Find(ShaderPath.SilhouetteInstanced);
            boxBlur = Shader.Find(ShaderPath.BoxBlur);
            gaussianBlur = Shader.Find(ShaderPath.GaussianBlur);
            kawaseBlur = Shader.Find(ShaderPath.KawaseBlur);
            dilate = Shader.Find(ShaderPath.Dilate);
            outline = Shader.Find(ShaderPath.Outline);
            return this;
        }
    }
    
    static class ShaderPath
    {
        public const string Mask = "Hidden/Outlines/Soft Outline/Mask";
        public const string Silhouette = "Hidden/Outlines/Soft Outline/Silhouette";
        public const string SilhouetteInstanced = "Hidden/Outlines/Soft Outline/Silhouette Instanced";
        public const string BoxBlur = "Hidden/Outlines/Soft Outline/Box Blur";
        public const string GaussianBlur = "Hidden/Outlines/Soft Outline/Gaussian Blur";
        public const string KawaseBlur = "Hidden/Outlines/Soft Outline/Kawase Blur";
        public const string Dilate = "Hidden/Outlines/Soft Outline/Dilate";
        public const string Outline = "Hidden/Outlines/Soft Outline/Outline";
    }
    
    static class ShaderPass
    {
        public const int Mask = 0;
        public const int Silhouette = 0;
        public const int Blur = 0;
        public const int VerticalBlur = 0;
        public const int HorizontalBlur = 1;
        public const int Outline = 0;
    }

    static class ShaderPassName
    {
        public const string Mask = "Mask (Soft Outline)";
        public const string Silhouette = "Silhouette (Soft Outline)";
        public const string Blur = "Blur (Soft Outline)";
        public const string Outline = "Outline (Soft Outline)";
    }

    static class ShaderPropertyId
    {
        public static readonly int Samples = Shader.PropertyToID("_Samples");
        public static readonly int KernelSize = Shader.PropertyToID("_KernelSize");
        public static readonly int KernelSpread = Shader.PropertyToID("_KernelSpread");
        public static readonly int Offset = Shader.PropertyToID("_offset");
        public static readonly int OutlineHardness = Shader.PropertyToID("_OutlineHardness");
        public static readonly int OutlineIntensity = Shader.PropertyToID("_OutlineIntensity");
        public static readonly int OutlineGap = Shader.PropertyToID("_OutlineGap");
        public static readonly int SilhouetteBuffer = Shader.PropertyToID("_SilhouetteBuffer");
    }
    
    static class ShaderFeature
    {
        public const string AlphaCutout = "ALPHA_CUTOUT";
        public const string HardOutline = "HARD_OUTLINE";
        public const string ScaleWithResolution = "SCALE_WITH_RESOLUTION";
    }
    
    static class Keyword
    {
        public static readonly GlobalKeyword OutlineColor = GlobalKeyword.Create("_OUTLINE_COLOR");
    }

    static class Buffer
    {
        public const string Silhouette = "_SilhouetteBuffer";
        public const string Blur = "_BlurBuffer";
    }
    
    public enum DilationMethod
    {
        Box,
        Gaussian,
        Kawase,
        Dilate
    }
    
    public enum OutlineType
    {
        Soft,
        Hard
    }
    
    public enum SoftOutlineOcclusion
    {
        Always,
        WhenOccluded,
        WhenNotOccluded,
        AsMask
    }
}