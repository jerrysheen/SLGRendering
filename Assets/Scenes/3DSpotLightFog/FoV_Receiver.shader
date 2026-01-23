Shader "Custom/FoV_Receiver_URP"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
        _Color ("Visible Color", Color) = (1,1,1,1)
        _FogColor ("Fog Color", Color) = (0,0,0,1)
    }
    SubShader
    {
        Tags 
        { 
            "RenderType"="Transparent" 
            "Queue"="Transparent" 
            "RenderPipeline" = "UniversalPipeline" // 标记为 URP
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite On

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            // 引入 URP 核心库
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0; // 新增：纹理坐标
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD1;
                float2 uv : TEXCOORD0; // 新增：传递 UV
            };

            // URP SRP Batcher 兼容 (将属性放入 CBUFFER)
            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _FogColor;
                // _MainTex_ST 不需要显式声明，如果用到 Tiling/Offset 可以加
            CBUFFER_END

            // 纹理与采样器定义
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            // 全局变量 (由 C# 传入，通常不放入 UnityPerMaterial)
            float4x4 _FoVShadowVP;
            // 定义阴影图纹理和采样器
            TEXTURE2D(_FoVShadowMap);
            SAMPLER(sampler_FoVShadowMap); 
            
            float3 _FoVLightPos;
            float _FoVRange;

            Varyings vert (Attributes input)
            {
                Varyings output;
                // URP: 模型空间 -> 裁剪空间
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                // URP: 模型空间 -> 世界空间
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag (Varyings input) : SV_Target
            {
                // --- 1. 采样 MainTex ---
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);

                // --- 2. 原有的 FoV 计算逻辑 ---

                // 把世界坐标投影到 阴影图的 UV 空间
                float4 shadowClip = mul(_FoVShadowVP, float4(input.positionWS, 1.0));
                
                // 透视除法
                float2 shadowUV = shadowClip.xy / shadowClip.w;
                // NDC (-1~1) -> UV (0~1)
                shadowUV = shadowUV * 0.5 + 0.5;

                // 检查越界
                if(shadowUV.x < 0 || shadowUV.x > 1 || shadowUV.y < 0 || shadowUV.y > 1)
                    return _FogColor; // 越界直接显示迷雾色

                // 采样阴影图 (RFloat 格式通常只取 R)
                // URP 中采样全局纹理
                float closestDist = SAMPLE_TEXTURE2D(_FoVShadowMap, sampler_FoVShadowMap, shadowUV).r;

                // 计算当前距离
                float currentDist = distance(input.positionWS, _FoVLightPos) / _FoVRange;

                // 比较 (Bias)
                float bias = 0.001;
                float visibility = (currentDist - bias) < closestDist ? 1.0 : 0.0;
                
                // 剔除背后投影
                if (shadowClip.w < 0) visibility = 0;

                // --- 3. 混合结果 ---
                // 可见区域颜色 = 纹理 * _Color
                // 不可见区域 = _FogColor
                half4 finalVisibleColor = texColor * _Color;
                
                return lerp(_FogColor, finalVisibleColor, visibility);
            }
            ENDHLSL
        }
    }
}