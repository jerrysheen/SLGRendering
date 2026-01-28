#ifndef WIDE_OUTLINE_INCLUDED
#define WIDE_OUTLINE_INCLUDED

#ifndef SHADERGRAPH_PREVIEW
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"

#endif

#define SNORM16_MAX_FLOAT_MINUS_EPSILON ((float)(32768-2) / (float)(32768-1))
#define FLOOD_ENCODE_OFFSET float2(1.0, SNORM16_MAX_FLOAT_MINUS_EPSILON)
#define FLOOD_ENCODE_SCALE float2(2.0, 1.0 + SNORM16_MAX_FLOAT_MINUS_EPSILON)

float _OutlineWidth;


TEXTURE2D(_BlitTexture);
SAMPLER(sampler_BlitTexture);
float4 _BlitTexture_TexelSize;

TEXTURE2D(_SilhouetteBuffer);
SAMPLER(sampler_SilhouetteBuffer);
float4 _SilhouetteBuffer_TexelSize;


void WideOutline_float(
    float2 ScreenPosition,
    out half Outline,
    out half RelativeDistance,
    out float4 Color)
{
    float2 positionCS = ScreenPosition * _ScreenParams.xy;

    // Integer pixel position.
    int2 uvInt = int2(positionCS);

    // Load encoded position.
    float2 EncodedPosition = _BlitTexture.Load(int3(uvInt, 0)).rg;
    
    float2 NearestPosition = 0;
    // Early out if null position.
    if (EncodedPosition.y == -1)
    {
        NearestPosition = float2(FLT_INF, FLT_INF);
        return;
    }

    // Decode nearest position.
    NearestPosition = (EncodedPosition + FLOOD_ENCODE_OFFSET) * abs(_ScreenParams.xy) / FLOOD_ENCODE_SCALE;

    // Current pixel position.
    float2 CurrentPosition = positionCS;

    // Distance in pixels to the closest position.
    half pixelDistance = length(NearestPosition - CurrentPosition);

    // Calculate outline.
    // + 1.0 is because encoded nearest position is half a pixel inset
    // not + 0.5 because we want the anti-aliased edge to be aligned between pixels
    // distance is already in pixels, so this is already perfectly anti-aliased!
    Outline = _OutlineWidth - pixelDistance + 1.0;

    // Calculate relative distance.
    RelativeDistance = pixelDistance / _OutlineWidth;

    Color = half4(SAMPLE_TEXTURE2D(_SilhouetteBuffer, sampler_SilhouetteBuffer, NearestPosition / _ScreenParams.xy).rgb, Outline);
}

#endif
