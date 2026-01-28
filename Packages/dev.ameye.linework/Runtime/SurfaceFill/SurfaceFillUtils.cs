using System;
using UnityEngine;

namespace Linework.SurfaceFill
{
    [Serializable]
    public sealed class ShaderResources
    {
        public Shader mask;
        public Shader fill;

        public ShaderResources Load()
        {
            mask = Shader.Find(ShaderPath.Mask);
            fill = Shader.Find(ShaderPath.Fill);
            return this;
        }
    }
    
    static class ShaderPath
    {
        public const string Mask = "Hidden/Outlines/Surface Fill/Mask";
        public const string Fill = "Hidden/Outlines/Surface Fill/Fill";
    }

    static class ShaderPass
    {
        public const int Mask = 0;
    }
    
    static class ShaderPassName
    {
        public const string Mask = "Mask (Surface Fill)";
        public const string Fill = "Fill (Surface Fill)";
    }
    
    static class ShaderPropertyId
    {
        public static readonly int PrimaryColor = Shader.PropertyToID("_PrimaryColor");
        public static readonly int SecondaryColor = Shader.PropertyToID("_SecondaryColor");
        public static readonly int FrequencyX = Shader.PropertyToID("_FrequencyX");
        public static readonly int FrequencyY = Shader.PropertyToID("_FrequencyY");
        public static readonly int Density = Shader.PropertyToID("_Density");
        public static readonly int Rotation = Shader.PropertyToID("_Rotation");
        public static readonly int Direction = Shader.PropertyToID("_Direction");
        public static readonly int Offset = Shader.PropertyToID("_Offset");
        public static readonly int Speed = Shader.PropertyToID("_Speed");
        public static readonly int Scale = Shader.PropertyToID("_Scale");
        public static readonly int Texture = Shader.PropertyToID("_Texture");
        public static readonly int Softness = Shader.PropertyToID("_Softness");
        public static readonly int Width = Shader.PropertyToID("_Width");
        public static readonly int Power = Shader.PropertyToID("_Power");
    }

    static class ShaderFeature
    {
        public const string AlphaCutout = "ALPHA_CUTOUT";
        
        public const string ChannelR = "CHANNEL_R";
        public const string ChannelG = "CHANNEL_G";
        public const string ChannelB = "CHANNEL_B";
        public const string ChannelA = "CHANNEL_A";
        
        public const string PatternSolid = "_PATTERN_SOLID";
        public const string PatternCheckerboard = "_PATTERN_CHECKERBOARD";
        public const string PatternDots = "_PATTERN_DOTS";
        public const string PatternStripes = "_PATTERN_STRIPES";
        public const string PatternSquares= "_PATTERN_SQUARES";
        public const string PatternGlow = "_PATTERN_GLOW";
        public const string PatternTexture = "_PATTERN_TEXTURE";
    }
    
    public enum Pattern
    {
        Solid,
        Checkerboard,
        Dots,
        Stripes,
        Squares,
        Glow,
        Texture
    }
    
    public enum DebugStage
    {
        None,
        Mask
    }
}