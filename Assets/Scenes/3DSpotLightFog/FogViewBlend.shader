Shader "Hidden/FogViewBlend"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {} // 兼容旧版 Blit
        _MinVisibility ("Min Visibility (Shadow Brightness)", Range(0, 1)) = 0.2
    }
    SubShader
    {
        Tags 
        { 
            "RenderType"="Opaque" 
            "RenderPipeline" = "UniversalPipeline"
        }
        
        LOD 100
        ZWrite Off 
        Cull Off
        Blend One Zero

        Pass
        {
            Name "FogViewBlend"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            // 定义 Attributes 以匹配 Vert 函数
            struct Attributes
            {
            #if SHADER_API_GLES
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            #else
                uint vertexID     : SV_VertexID;
            #endif
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 texcoord   : TEXCOORD0;
            };

            // URP SRP Batcher 兼容
            CBUFFER_START(UnityPerMaterial)
                float _MinVisibility;
            CBUFFER_END
            
            // URP Blit 传参
            float4 _BlitScaleBias;
            TEXTURE2D(_BlitTexture); // 场景颜色纹理
            SAMPLER(sampler_BlitTexture);

            // Fog View Mask 纹理 (RenderFeature 设置的全局纹理)
            TEXTURE2D(_3DFogViewTexture);
            SAMPLER(sampler_3DFogViewTexture);

            // 顶点着色器
            Varyings Vert(Attributes input)
            {
                Varyings output;

            #if SHADER_API_GLES
                float4 pos = input.positionOS;
                float2 uv  = input.uv;
            #else
                // URP 全屏三角形
                float4 pos = GetFullScreenTriangleVertexPosition(input.vertexID);
                float2 uv  = GetFullScreenTriangleTexCoord(input.vertexID);
            #endif

                output.positionCS = pos;
                // 处理 Blit 的缩放和偏移
                output.texcoord   = uv * _BlitScaleBias.xy + _BlitScaleBias.zw;
                return output;
            }

            // 片元着色器
            half4 Frag(Varyings input) : SV_Target
            {
                // 1. 采样场景颜色
                half4 sceneColor = SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, input.texcoord);
                // 2. 采样雾视图遮罩 (R8 单通道纹理，只取 .r 或 .x)
                float fogMask = SAMPLE_TEXTURE2D(_3DFogViewTexture, sampler_3DFogViewTexture, input.texcoord).r;
                
                // 3. 计算可见度
                // fogMask = 1: 完全可见 (visibility = 1)
                // fogMask = 0: 最小可见度 (visibility = _MinVisibility)
                float visibility = lerp(_MinVisibility, 1.0, fogMask);
                
                // 4. 应用可见度到场景颜色
                half4 finalColor = sceneColor * visibility;
                
                return finalColor;
            }
            ENDHLSL
        }
    }
}
