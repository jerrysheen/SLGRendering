Shader "Hidden/Outlines/Soft Outline/Gaussian Blur"
{
    Properties {}

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        ZWrite On
        ZTest Always

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        #pragma multi_compile_local _ SCALE_WITH_RESOLUTION

        #define E 2.71828

        CBUFFER_START(UnityPerMaterial)
            int _KernelSize;
            int _Samples;
            float _KernelSpread;
            float _ReferenceResolution;
            #if UNITY_VERSION < 202300
            float4 _BlitTexture_TexelSize;
            #endif
        CBUFFER_END

        float kernel_weight(int x)
        {
            float variance = _KernelSpread * _KernelSpread;
            return 1 / sqrt(2 * PI * variance) * pow(E, -(x * x) / (2 * variance));
        }
        ENDHLSL

        Pass // 0: VERTICAL BLUR
        {
            Name "VERTICAL BLUR"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag

            float4 frag(Varyings IN) : SV_TARGET
            {
                float4 sum = 0;
                float scale = 1;
                
                #if defined(SCALE_WITH_RESOLUTION)
                scale = 1 * _ScreenParams.y / _ReferenceResolution;
                #endif

                for (float y = 0; y < _Samples; y++) {
                    float weight = kernel_weight(y - _KernelSize);
                    float2 offset = float2(0, y - _KernelSize) * _BlitTexture_TexelSize.xy * scale;
                    sum += weight * SAMPLE_TEXTURE2D(_BlitTexture, sampler_PointClamp, IN.texcoord + offset);
                }

                return sum;
            }
            ENDHLSL
        }

        Pass // 1: HORIZONTAL BLUR
        {
            Name "HORIZONTAL BLUR"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag

            float4 frag(Varyings IN) : SV_TARGET
            {
                float4 sum = 0;
                float scale = 1;
                
                #if defined(SCALE_WITH_RESOLUTION)
                scale = 1 * _ScreenParams.y / _ReferenceResolution;
                #endif

                for (float x = 0; x < _Samples; x++) {
                    float weight = kernel_weight(x - _KernelSize);
                    float2 offset = float2(x - _KernelSize, 0) * _BlitTexture_TexelSize.xy * scale;
                    sum += weight * SAMPLE_TEXTURE2D(_BlitTexture, sampler_PointClamp, IN.texcoord + offset);
                }

                return sum;
            }
            ENDHLSL
        }
    }
}