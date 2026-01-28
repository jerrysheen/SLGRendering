Shader "Elex/CharectorOutline"
{
    Properties
    {
        [Header(Base)]
        _BaseMap("Base Texture", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (1,1,1,1)
        
        [Header(Outline)]
        _OutlineColor("Outline Color", Color) = (0,0,0,1)
        _OutlineWidth("Outline Width", Float) = 1.0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        // ------------------------------------------------------------------
        // Pass 1: 正常渲染 + 写入 Stencil
        // ------------------------------------------------------------------
        Pass
        {
            Name "Character_Base"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Stencil
            {
                Ref 1
                Pass Replace    // 渲染成功的像素，Stencil 值设为 1
                Comp Always     // 总是通过测试
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            float4 _BaseColor;

            Varyings vert(Attributes input) {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target {
                return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------
        // Pass 2: 描边渲染 + Stencil 剔除
        // ------------------------------------------------------------------
        Pass
        {
            Name "Character_Outline"
            Tags { "LightMode" = "UniversalForward" }

            
            Cull Front          // 剔除正面，只画背面
            ZWrite On           // 开启写入，保证深度正确
            
            Stencil
            {
                Ref 1
                Comp NotEqual   // 【核心】只有 Stencil 不等于 1 的地方才渲染（即模型外圈）
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv8 : TEXCOORD7; // 对应你 C# 脚本存入的 uv8 (index 7)
            };

            struct Varyings {
                float4 positionCS : SV_POSITION;
            };

            float4 _OutlineColor;
            float _OutlineWidth;

            // 八面体还原函数
            float3 UnpackOctahedralNormal(float2 f) {
                float3 n = float3(f.x, f.y, 1.0 - abs(f.x) - abs(f.y));
                float t = saturate(-n.z);
                n.xy += n.xy >= 0.0 ? -t : t;
                return normalize(n);
            }

            Varyings vert(Attributes input) {
                Varyings output;

                // 1. 还原平滑法线
                float3 smoothedNormalTS = UnpackOctahedralNormal(input.uv8);
                
                // 2. 构建 TBN 将法线转回模型空间 (支持骨骼动画)
                float3 bitangentOS = cross(input.normalOS, input.tangentOS.xyz) * input.tangentOS.w;
                float3x3 tbn = float3x3(input.tangentOS.xyz, bitangentOS, input.normalOS);
                float3 smoothedNormalOS = normalize(mul(smoothedNormalTS, tbn));

                // 3. 顶点外扩（OS 空间）
                float3 posOS = input.positionOS.xyz + smoothedNormalOS * _OutlineWidth * 0.01;
                
                output.positionCS = TransformObjectToHClip(posOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target {
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
}