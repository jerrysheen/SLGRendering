Shader "Hidden/PolarShadowGen_URP"
{
    Properties
    {
        // 这里的 Property 主要是为了调试看，实际由 C# 驱动
        _MainColor ("Debug Color", Color) = (1,0,0,1)
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "RenderPipeline" = "UniversalPipeline" 
        }

        Pass
        {
            Name "ShadowGen"
            Tags { "LightMode" = "UniversalForward" }

            // --- 关键修改 1: 渲染状态 ---
            Cull Off
            ZWrite Off           // 不需要写深度
            ZTest Always         // 永远渲染，不被剔除
            
            // --- 关键修改 2: 混合模式 ---
            BlendOp Min          // 取最小值：min(新像素, 旧像素)
            Blend One One        // 因子设为1，直接比较源值
            ColorMask R          // 只写 R 通道

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float dist : TEXCOORD0;
            };

            float3 _PlayerPos;
            float _MaxRadius;

            #define PI 3.14159265359

            Varyings vert(Attributes input)
            {
                Varyings output;

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 viewPos = positionWS - _PlayerPos;

                float angle = atan2(viewPos.z, viewPos.x); 
                float dist = length(viewPos.xz);

                float clipX = angle / PI;
                float clipY = sin((positionWS.y)) / 2.0f;
                
                // --- 关键修改 3: 放弃用 Z 做剔除 ---
                // 直接设为 0.5 (在 DX[0,1] 和 GL[-1,1] 中都是安全的中间值)
                // 这样物体永远不会因为 Z 超出范围而被裁剪
                float clipZ = 0.5; 
                
                // W=1 正交投影
                output.positionCS = float4(clipX, clipY, clipZ, 1.0);
                
                output.dist = dist;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 配合 BlendOp Min，GPU 会自动保留该像素位置上最小的 input.dist
                return half4(input.dist, 0, 0, 1);
            }
            ENDHLSL
        }
    }
}