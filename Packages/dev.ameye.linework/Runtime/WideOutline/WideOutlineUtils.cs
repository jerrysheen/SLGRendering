using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Linework.WideOutline
{
    [Serializable]
    public sealed class ShaderResources
    {
        public Shader mask;
        public Shader silhouette;
        public Shader silhouetteInstanced;
        public Shader outline;
        public Shader clear;

        public ShaderResources Load()
        {
            mask = Shader.Find(ShaderPath.Mask);
            silhouette = Shader.Find(ShaderPath.Silhouette);
            silhouetteInstanced = Shader.Find(ShaderPath.SilhouetteInstanced);
            outline = Shader.Find(ShaderPath.Outline);
            clear = Shader.Find(ShaderPath.Clear);
            return this;
        }
    }
    
    static class ShaderPath
    {
        public const string Mask = "Hidden/Outlines/Wide Outline/Mask";
        public const string Silhouette = "Hidden/Outlines/Wide Outline/Silhouette";
        public const string SilhouetteInstanced = "Hidden/Outlines/Wide Outline/Silhouette Instanced";
        public const string Outline = "Hidden/Outlines/Wide Outline/Outline";
        public const string Clear = "Hidden/Clear Stencil";
    }
    
    static class ShaderPass
    {
        public const int Mask = 0;
        public const int Silhouette = 0;
        public const int Information = 0;
        public const int FloodInit = 1;
        public const int FloodJump = 2;
        public const int Outline = 3;
    }
    
    static class ShaderPassName
    {
        public const string Mask = "Mask (Wide Outline)";
        public const string Silhouette = "Silhouette (Wide Outline)";
        public const string Information = "Information (Wide Outline)";
        public const string Flood = "Flood (Wide Outline)";
        public const string Outline = "Outline (Wide Outline)";
    }
    
    static class ShaderPropertyId
    {
        public static readonly int OutlineOccludedColor = Shader.PropertyToID("_OutlineOccludedColor");
        public static readonly int OutlineWidth = Shader.PropertyToID("_OutlineWidth");
        public static readonly int OutlineGap = Shader.PropertyToID("_OutlineGap");
        public static readonly int RenderScale = Shader.PropertyToID("_RenderScale");
        
        public static readonly int AxisWidthId = Shader.PropertyToID("_AxisWidth");
        public static readonly int SilhouetteBuffer = Shader.PropertyToID("_SilhouetteBuffer");
        public static readonly int InformationBuffer = Shader.PropertyToID("_InformationBuffer");
        public static readonly int SilhouetteDepthBuffer = Shader.PropertyToID("_SilhouetteDepthBuffer");
    }
    
    static class ShaderFeature
    {
        public const string AlphaCutout = "ALPHA_CUTOUT";
        public const string CustomDepth = "CUSTOM_DEPTH";
        public const string InformationBuffer = "INFORMATION_BUFFER";
        public const string ScaleWithResolution = "SCALE_WITH_RESOLUTION";
    }
    
    static class Keyword
    {
        public static readonly GlobalKeyword OutlineColor = GlobalKeyword.Create("_OUTLINE_COLOR");
    }
    
    static class Buffer
    {
        public const string Silhouette = "_SilhouetteBuffer";
        public const string SilhouetteDepth = "_SilhouetteDepthBuffer";
        public const string Information = "_InformationBuffer";
        public const string Ping = "_PingBuffer";
        public const string Pong = "_PongBuffer";
    }

    public enum WideOutlineOcclusion
    {
        Always,
        WhenOccluded,
        WhenNotOccluded,
        AsMask
    }
}