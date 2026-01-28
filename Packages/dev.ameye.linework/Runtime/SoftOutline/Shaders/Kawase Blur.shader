Shader "Hidden/Outlines/Soft Outline/Kawase Blur"
{
    Properties {}

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        ZWrite Off
        ZTest Always

        Pass // 0: BLUR
        {
            Name "BLUR"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #pragma multi_compile_local _ SCALE_WITH_RESOLUTION
            
            CBUFFER_START(UnityPerMaterial)
                #if UNITY_VERSION < 202300
                float4 _BlitTexture_TexelSize;
                #endif
                SAMPLER(sampler_BlitTexture);
                float _offset;
                float _ReferenceResolution;
            CBUFFER_END

            half4 frag(Varyings IN) : SV_TARGET
            {
                float scale = 1;
                
                #if defined(SCALE_WITH_RESOLUTION)
                scale = 1 * _ScreenParams.y / _ReferenceResolution;
                #endif
                
                float2 texelSize = _BlitTexture_TexelSize.xy * scale;

                half4 value = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, IN.texcoord);
                value += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, IN.texcoord + float2(_offset, _offset) * texelSize) * 0.2;
                value += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, IN.texcoord + float2(_offset, -_offset) * texelSize) * 0.2;
                value += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, IN.texcoord + float2(-_offset, _offset) * texelSize) * 0.2;
                value += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, IN.texcoord + float2(-_offset, -_offset) * texelSize) * 0.2;

                return value;
            }
            ENDHLSL
        }
    }
}