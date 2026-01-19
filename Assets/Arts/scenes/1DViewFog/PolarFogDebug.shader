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
            sampler2D _PolarShadowMap;

            float _SecondPassAngleOffsetRad;

            #define PI 3.14159265359

            half4 Frag(Varyings input) : SV_Target
            {
                float depth = SampleSceneDepth(input.texcoord);
                float3 worldPos = ComputeWorldSpacePosition(input.texcoord, depth, UNITY_MATRIX_I_VP);

                float3 offset = worldPos - _PlayerPos;
                float pixelDist = length(offset.xz);
                if (pixelDist > _MaxRadius) return half4(0, 0, 0, 1);

                float angle = atan2(offset.z, offset.x);


                float u0 = angle / (2.0 * PI) + 0.5;
                u0 = frac(u0);

                float u1 = frac(u0 + 0.5);          // 对应 Pass1 写入时 angle+PI

                float r = tex2D(_PolarShadowMap, float2(u0, 0.5)).r; // seam=0
                float g = tex2D(_PolarShadowMap, float2(u1, 0.5)).g; // seam rotated 180deg

                float blockerDist = min(r, g);

                float edgeSoftness = 0.5;
                float visibility = 1.0 - smoothstep(blockerDist - edgeSoftness, blockerDist + edgeSoftness, pixelDist);

                return half4(visibility, visibility, visibility, 1);
            }

            ENDHLSL
        }
    }
}