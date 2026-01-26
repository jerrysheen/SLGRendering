Shader "Hidden/GaussianBlur"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType"="Opaque" 
            "RenderPipeline" = "UniversalPipeline"
        }
        
        ZWrite Off 
        Cull Off
        ZTest Always

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        
        struct Attributes
        {
            float4 positionOS : POSITION;
            float2 uv : TEXCOORD0;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
        };

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);
        float4 _MainTex_TexelSize;
        float _BlurOffset;

        Varyings Vert(Attributes input)
        {
            Varyings output;
            output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
            output.uv = input.uv;
            return output;
        }

        // 降采样 Pass (Box filter with 4 samples)
        half4 FragDownsample(Varyings input) : SV_Target
        {
            float2 texelSize = _MainTex_TexelSize.xy;
            float4 offset = texelSize.xyxy * float4(-1, -1, 1, 1);
            
            // 4个采样点取平均（box filter）
            half4 result = 0;
            result += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + offset.xy);
            result += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + offset.zy);
            result += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + offset.xw);
            result += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + offset.zw);
            
            return result * 0.25;
        }

        // 升采样 Pass (Tent filter with 9 samples)
        half4 FragUpsample(Varyings input) : SV_Target
        {
            float2 texelSize = _MainTex_TexelSize.xy * _BlurOffset;
            
            // 9-tap tent filter
            half4 result = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * 4.0;
            
            result += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(-texelSize.x, 0)) * 2.0;
            result += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(texelSize.x, 0)) * 2.0;
            result += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(0, -texelSize.y)) * 2.0;
            result += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(0, texelSize.y)) * 2.0;
            
            result += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(-texelSize.x, -texelSize.y));
            result += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(texelSize.x, -texelSize.y));
            result += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(-texelSize.x, texelSize.y));
            result += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(texelSize.x, texelSize.y));
            
            return result / 16.0;
        }

        // Kawase Blur - 单Pass模糊（高质量）
        half4 FragKawaseBlur(Varyings input) : SV_Target
        {
            float2 texelSize = _MainTex_TexelSize.xy;
            float offset = _BlurOffset;
            
            // 中心
            half4 result = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * 0.5;
            
            // 对角线4个采样点
            result += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(offset, offset) * texelSize) * 0.125;
            result += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(-offset, offset) * texelSize) * 0.125;
            result += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(offset, -offset) * texelSize) * 0.125;
            result += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(-offset, -offset) * texelSize) * 0.125;
            
            return result;
        }
        ENDHLSL

        // Pass 0: 降采样
        Pass
        {
            Name "Downsample"
            
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragDownsample
            ENDHLSL
        }

        // Pass 1: 升采样（带模糊）
        Pass
        {
            Name "Upsample"
            
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragUpsample
            ENDHLSL
        }

        // Pass 2: Kawase 模糊（单Pass高质量模糊）
        Pass
        {
            Name "KawaseBlur"
            
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragKawaseBlur
            ENDHLSL
        }
    }
}
