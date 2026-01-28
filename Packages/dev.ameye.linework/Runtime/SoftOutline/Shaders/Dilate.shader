Shader "Hidden/Outlines/Soft Outline/Dilate"
{
    Properties {}

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
        }

        Cull Off
        ZWrite Off
        ZTest Always

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        #pragma multi_compile_local _ SCALE_WITH_RESOLUTION
        
        CBUFFER_START(UnityPerMaterial)
            float _KernelSize;
            float _ReferenceResolution;
            #if UNITY_VERSION < 202300
            float4 _BlitTexture_TexelSize;
            #endif
        CBUFFER_END
        ENDHLSL

        Pass // HORIZONTAL DILATE
        {
            Name "HORIZONTAL DILATE"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag

            float4 frag(Varyings IN) : SV_Target
            {
                float sum = 0;
                int shortestActivePixelDistance = _KernelSize + 1;
                float3 nearestActivePixelColor = float3(0, 0, 0);

                float scale = 1;
                
                #if defined(SCALE_WITH_RESOLUTION)
                scale = 1 * _ScreenParams.y / _ReferenceResolution;
                #endif

                for (int x = -_KernelSize; x <= _KernelSize; x++) {
                    float2 offset = float2(x, 0) * _BlitTexture_TexelSize.xy * scale;
                    float4 sample = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, IN.texcoord + offset);

                    int pixelDistance = abs(x);
                    float falloff = 1.0f - pixelDistance / _KernelSize;
                    sum += sample.a * falloff;

                    if (pixelDistance < shortestActivePixelDistance && sample.a >= 1.0) {
                        shortestActivePixelDistance = pixelDistance;
                        nearestActivePixelColor = sample.xyz;
                    }
                }

                return float4(nearestActivePixelColor, 1 - saturate(shortestActivePixelDistance / _KernelSize));
            }
            ENDHLSL
        }

        Pass // VERTICAL DILATE
        {
            Name "VERTICAL DILATE"

            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag

            float4 frag(Varyings IN) : SV_Target
            {
                float sum = 0;
                float3 brightestActivePixelColor = float3(0, 0, 0);
                float brightestWeightedAlpha = 0;

                float scale = 1;
                
                #if defined(SCALE_WITH_RESOLUTION)
                scale = 1 * _ScreenParams.y / _ReferenceResolution;
                #endif

                for (int y = -_KernelSize; y <= _KernelSize; y++) {
                    float2 offset = float2(0, y) * _BlitTexture_TexelSize.xy * scale;
                    float4 sample = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, IN.texcoord + offset);

                    int pixelDistance = abs(y);
                    float falloff = 1.0f - pixelDistance / _KernelSize;
                    float weightedValue = sample.a * falloff;
                    sum += weightedValue;

                    if (weightedValue > brightestWeightedAlpha) {
                        brightestWeightedAlpha = weightedValue;
                        brightestActivePixelColor = sample.xyz;
                    }
                }

                return float4(brightestActivePixelColor, Smoothstep01(brightestWeightedAlpha));
            }
            ENDHLSL
        }
    }
}