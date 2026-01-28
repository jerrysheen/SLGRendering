Shader "Hidden/Outlines/Soft Outline/Box Blur"
{
    Properties {}

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
        }

        Cull Off
        ZWrite On
        ZTest Always

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        #pragma multi_compile_local _ SCALE_WITH_RESOLUTION

        CBUFFER_START(UnityPerMaterial)
            int _KernelSize;
            int _Samples;
            float _ReferenceResolution;
            #if UNITY_VERSION < 202300
            float4 _BlitTexture_TexelSize;
            #endif
        CBUFFER_END
        ENDHLSL

        Pass // 0: VERTICAL BLUR
        {
            Name "VERTICAL BLUR"

            Stencil
            {
                Ref 1
                Comp Always
                Pass Replace
                Fail Replace
            }

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
                    float2 offset = float2(0, y - _KernelSize) * _BlitTexture_TexelSize.xy * scale;
                    sum += SAMPLE_TEXTURE2D(_BlitTexture, sampler_PointClamp, IN.texcoord + offset);
                }

                return float4(sum / _Samples);
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
                    float2 offset = float2(x - _KernelSize, 0) * _BlitTexture_TexelSize.xy * scale;
                    sum += SAMPLE_TEXTURE2D(_BlitTexture, sampler_PointClamp, IN.texcoord + offset);
                }

                return float4(sum / _Samples);
            }
            ENDHLSL
        }
    }
}