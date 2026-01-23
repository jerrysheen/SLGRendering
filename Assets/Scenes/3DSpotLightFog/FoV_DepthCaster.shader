Shader "Hidden/FoV_DepthCaster_URP"
{
    SubShader
    {
        // URP 识别标签
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline" }
        
        Pass
        {
            Name "FoVDepthCaster"
            // 在 URP 中手动渲染通常不需要特定的 LightMode，但如果整合进管线可能需要
            Tags { "LightMode" = "UniversalForward" } 

            // 【关键】通常 Depth Caster 需要剔除正面（只渲染背面），
            // 这样能确保存储的是物体最远端的深度，避免自身的 Z-Fighting
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            // 引入 URP 核心库
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            // 全局变量 (由 C# SetGlobal 传入)
            // URP 中推荐放在 CBUFFER 中以支持 SRP Batcher，
            // 但对于 SetGlobalVector 这种全局变量，直接声明也可以。
            float3 _FoVLightPos;
            float _FoVRange;

            Varyings vert (Attributes input)
            {
                Varyings output;
                
                // URP 获取顶点数据的辅助函数
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                
                // 转换到裁剪空间 (Clip Space)
                output.positionCS = vertexInput.positionCS;
                
                // 获取世界坐标 (World Space)
                output.positionWS = vertexInput.positionWS;
                
                return output;
            }

            half4 frag (Varyings input) : SV_Target
            {
                // 计算当前像素到光源的线性距离
                float dist = distance(input.positionWS, _FoVLightPos);
                
                // 归一化到 0~1
                float normalizedDist = dist / _FoVRange;
                
                // 输出 R 通道 (通常 ShadowMap 只需要单通道)
                return half4(normalizedDist, 0, 0, 1);
            }
            ENDHLSL
        }
    }
}