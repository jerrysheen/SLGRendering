Shader "ELEX/OutLine"
{
    Properties
    {
        [MainColor]_BaseColor("Color", Color) = (0, 0, 0, 0)
        _EdgeWidth("EdgeWidth", Range(0,1)) = 0.003
        _CameraMinHeight("_CameraMinHeight", float) = 50
        _CameraMaxHeight("_CameraMaxHeight", float) = 100
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "IgnoreProjector" = "True" "RenderPipeline" = "UniversalPipeline" }
        LOD 100
        Cull Front
        Pass
        {
            Name "Unlit"
            Tags
            {
                "LightMode" = "UniversalForward"
            }
            HLSLPROGRAM
            // Required to compile gles 2.0 with standard srp library
            //#pragma exclude_renderers d3d11_9x

            #pragma vertex vert
            #pragma fragment frag

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            //#pragma enable_d3d11_debug_symbols
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
            
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _EdgeWidth;
                half _CameraMinHeight;
                half _CameraMaxHeight;
            CBUFFER_END

            #ifdef UNITY_DOTS_INSTANCING_ENABLED
            UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
                UNITY_DOTS_INSTANCED_PROP(float4, _BaseColor)
                UNITY_DOTS_INSTANCED_PROP(float , _EdgeWidth)
            UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)

            #define _BaseColor          UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4 , _BaseColor)
            #define _EdgeWidth             UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float  , _EdgeWidth)
            #endif

            struct Attributes
            {
                float4 positionOS       : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                float3 encodedSmoothedNormal : TEXCOORD7;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 vertex : SV_POSITION;

                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            // 八面体编码还原函数
            float3 UnpackOctahedralNormal(float2 f)
            {
                // 假设输入 f 是映射到 [-1, 1] 范围的坐标
                float3 n = float3(f.x, f.y, 1.0 - abs(f.x) - abs(f.y));
                
                // 如果 z < 0，说明在八面体的下半部分，需要特殊处理处理 xy
                float t = saturate(-n.z);
                n.xy += n.xy >= 0.0 ? -t : t;
                
                return normalize(n);
            }

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                // 1. 还原切线空间的平滑法线 (Tangent Space)
                float2 encodedNormal = input.encodedSmoothedNormal; 
                // 注意：如果预览不对，尝试启用下面这行映射范围
                // encodedNormal = encodedNormal * 2.0 - 1.0; 
                float3 smoothedNormalTS = UnpackOctahedralNormal(encodedNormal);

                // 2. 【关键修正】构建 TBN 矩阵，将法线转回模型空间 (Object Space)
                float3 normalOS_base = input.normal;
                float4 tangentOS_base = input.tangent;
                float3 bitangentOS_base = cross(normalOS_base, tangentOS_base.xyz) * tangentOS_base.w;
                
                // 构建 TBN 矩阵 (从切线空间到模型空间)
                float3x3 tbn = float3x3(tangentOS_base.xyz, bitangentOS_base, normalOS_base);
                
                // 将还原出的平滑法线从 TS 转到 OS
                // 这里的 mul 顺序很重要，取决于你 tbn 矩阵是行优先还是列优先
                float3 smoothedNormalOS = normalize(mul(smoothedNormalTS, tbn));

                // 3. 计算基础裁剪空间坐标
                output.vertex = TransformObjectToHClip(input.positionOS.xyz);

                // 4. 屏幕空间偏移逻辑 (你原本的逻辑)
                // 注意：这里使用刚才转换得到的 smoothedNormalOS
                float3 viewNormal = mul((float3x3)UNITY_MATRIX_IT_MV, smoothedNormalOS);
                float3 clipNormal = normalize(mul((float3x3)UNITY_MATRIX_P, viewNormal));
                
                // 处理屏幕长宽比
                float4 screenParam = GetScaledScreenParams();
                half aspect = screenParam.y / screenParam.x;
                clipNormal.x *= aspect;

                // 处理距离缩放和高度衰减
                half ratio = 1.0 - smoothstep(_CameraMinHeight, _CameraMaxHeight, _WorldSpaceCameraPos.y);
                
                // 偏移顶点
                // 乘以 output.vertex.w 是为了抵消透视除法，实现近处远处一样粗的线条
                output.vertex.xy += clipNormal.xy * _EdgeWidth * output.vertex.w * ratio * 0.01; // 0.01 是微调系数
                
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                
                //half3 color = _BaseColor.rgb;
                return half4(0,0,0, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
