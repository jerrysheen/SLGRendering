#ifndef JUMPFLOOD_INCLUDED
#define JUMPFLOOD_INCLUDED

#ifndef SHADERGRAPH_PREVIEW
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
#endif

#define SNORM16_MAX_FLOAT_MINUS_EPSILON ((float)(32768-2) / (float)(32768-1))
#define FLOOD_ENCODE_OFFSET float2(1.0, SNORM16_MAX_FLOAT_MINUS_EPSILON)
#define FLOOD_ENCODE_SCALE float2(2.0, 1.0 + SNORM16_MAX_FLOAT_MINUS_EPSILON)

void DecodePosition_float(
    int2 UV,
    int2 Size,
    UnityTexture2D Source,
    out float2 NearestPosition)
{
    float2 encodedPos = Source.Load(int3(UV, 0)).rg;

    if (encodedPos.y == -1) {
        NearestPosition = float2(0, 0);
        return;
    }

    NearestPosition = (encodedPos + FLOOD_ENCODE_OFFSET) * abs(Size) / FLOOD_ENCODE_SCALE;
}


#endif