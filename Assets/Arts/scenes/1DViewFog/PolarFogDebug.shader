Shader "Hidden/PolarFogDebug"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZTest Always ZWrite Off Cull Off

        Pass
        {
            Name "PolarFogVisualization"
            
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            // 引入 URP 核心库
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            
            #if SHADER_API_GLES
            struct Attributes
            {
                float4 positionOS       : POSITION;
                float2 uv               : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            #else
            struct Attributes
            {
                uint vertexID : SV_VertexID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            #endif

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 texcoord   : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

            #if SHADER_API_GLES
                float4 pos = input.positionOS;
                float2 uv  = input.uv;
            #else
                float4 pos = GetFullScreenTriangleVertexPosition(input.vertexID);
                float2 uv  = GetFullScreenTriangleTexCoord(input.vertexID);
            #endif

                output.positionCS = pos;
                output.texcoord   = uv * 1 + 0;
                return output;
            }


            // 全局变量 (由 C# 传入)
            float3 _PlayerPos;
            float _MaxRadius;
            sampler2D _PolarShadowMap; // 1D 阻挡图

            #define PI 3.14159265359
            

            half4 Frag(Varyings input) : SV_Target
            {
                // 1. 获取场景深度 (Device Depth)
                float depth = SampleSceneDepth(input.texcoord);
                
                // 2. 重构世界坐标 (修复点：使用最通用的矩阵逆变换方法)
                // UNITY_MATRIX_I_VP 是 Inverse View Projection Matrix
                float3 worldPos = ComputeWorldSpacePosition(input.texcoord, depth, UNITY_MATRIX_I_VP);
                
                // 3. 计算相对于玩家的向量
                float3 offset = worldPos - _PlayerPos;
                float pixelDist = length(offset.xz);

                // 4. 超过最大半径直接显示黑色 (迷雾)
                if (pixelDist > _MaxRadius) return half4(0, 0, 0, 1);

                // 5. 计算极坐标角度 (范围 -PI 到 PI)
                float angle = atan2(offset.z, offset.x);
                
                // 6. 映射到 UV 空间 (0 到 1)
                float u = angle / (2.0 * PI) + 0.5;

                // 7. 采样 ShadowMap (1D 纹理)
                // 在后处理 Pass 中直接用 tex2D 即可
                float blockerDist = tex2D(_PolarShadowMap, float2(u, 0.5)).r;

                // 8. 比较 visibility
                // 如果 像素距离 > 阻挡距离，说明被挡住了 -> 0 (不可见)
                // 添加 0.5 的软边缘过渡
                float edgeSoftness = 0.5; 
                float visibility = 1.0 - smoothstep(blockerDist - edgeSoftness, blockerDist + edgeSoftness, pixelDist);

                // 输出：可见区域白色，不可见黑色
                // 如果你想看到原始游戏画面叠加迷雾，请取消下面这行的注释并修改返回值：
                // half4 originalColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv); // 需要在上面声明 _MainTex
                
                return half4(visibility, visibility, visibility, 1);
            }
            ENDHLSL
        }
    }
}