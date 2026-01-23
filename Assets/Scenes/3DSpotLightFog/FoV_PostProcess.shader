Shader "Hidden/FoV_PostProcess"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {} // 兼容旧版 Blit
        _FogColor ("Fog Color", Color) = (0,0,0,1)
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
            Name "FoVPostProcess"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            
            // 定义 Attributes 以匹配你的 Vert 函数
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

                        // URP SRP Batcher 兼容 (将属性放入 CBUFFER)
            CBUFFER_START(UnityPerMaterial)
                float4 _FogColor;
                // _MainTex_ST 不需要显式声明，如果用到 Tiling/Offset 可以加
            CBUFFER_END
            
            // URP Blit 传参
            float4 _BlitScaleBias;
            TEXTURE2D(_BlitTexture); // URP FullScreenPass 的源纹理通常是 _BlitTexture
            SAMPLER(sampler_BlitTexture);

            // FoV 阴影参数 (C# SetGlobal)
            float4x4 _FoVShadowVP; 
            TEXTURE2D(_FoVShadowMap);
            SAMPLER(sampler_FoVShadowMap);
            float3 _FoVLightPos;
            float _FoVRange;

            // 你的顶点着色器逻辑
            Varyings Vert(Attributes input)
            {
                Varyings output;
                // 这一行在某些 URP 版本需要，用于设置 Instance ID，虽然后处理通常不用 Instancing
                // UNITY_SETUP_INSTANCE_ID(input); 
                // UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

            #if SHADER_API_GLES
                float4 pos = input.positionOS;
                float2 uv  = input.uv;
            #else
                // URP 提供的生成全屏三角形顶点的帮助函数
                float4 pos = GetFullScreenTriangleVertexPosition(input.vertexID);
                float2 uv  = GetFullScreenTriangleTexCoord(input.vertexID);
            #endif

                output.positionCS = pos;
                // 处理 Blit 的缩放和偏移（处理 RT Handle 的视口问题）
                output.texcoord   = uv * _BlitScaleBias.xy + _BlitScaleBias.zw;
                return output;
            }

             half4 Frag(Varyings input) : SV_Target
            {
                // 1. 基础采样 (颜色 & 深度)
                half4 sceneColor = SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, input.texcoord);
                
                float depth = SampleSceneDepth(input.texcoord);
                
                // 2. 重建世界坐标
                float3 worldPos = ComputeWorldSpacePosition(input.texcoord, depth, UNITY_MATRIX_I_VP);

                // ==========================================================
                // 第一步：计算球形范围 (Sphere Visibility)
                // ==========================================================
                
                // 按照你的要求：把光源位置 Y 压扁为 0，然后计算距离
                float3 flatPlayerPos = _FoVLightPos;
                flatPlayerPos.y = 0;

                // 注意：通常 distance 不需要乘系数，除非是为了特殊缩放
                float distToPlayer = distance(worldPos, flatPlayerPos) * 1.5; 
                // 如果你有缩放需求，恢复成: distance(worldPos, flatPlayerPos) * 3.0f;
                
                // 计算渐变 (1 = 核心可见, 0 = 边缘不可见)
                // 建议把 EdgeSoftness 暴露为变量，这里先硬编码你的 0.1f
                
                float sphereVis = 1.0 - (distToPlayer - _FoVRange)/ _FoVRange;
                // ==========================================================
                // 第二步：计算阴影遮挡 (Shadow Visibility)
                // ==========================================================
                // 默认遮挡 (visibility = 0)
                float shadowVis = 0.0;

                // 【优化】只有在球形范围内的像素 (sphereVis > 0)，才需要去查阴影
                // 如果已经在球外面了，它肯定是黑的，没必要浪费性能去 Sample 阴影图
                //if (sphereVis > 0.001)
                //{
                    // A. 准备 ShadowMap 采样坐标 (强制 Y=0 扁平化，匹配 Caster)
                    float3 flatWorldPos = float3(worldPos.x, 0, worldPos.z);
                    float3 flatLightPos = float3(_FoVLightPos.x, 0, _FoVLightPos.z);

                    // B. 投影到阴影图空间
                    float4 shadowClip = mul(_FoVShadowVP, float4(flatWorldPos, 1.0));
                    
                    // 剔除相机背面
                    if (shadowClip.w > 0)
                    {
                        float2 shadowUV = shadowClip.xy / shadowClip.w;
                        shadowUV = shadowUV * 0.5 + 0.5; // NDC -> UV

                        // 采样阴影图 (RFloat 格式通常只取 R)
                        // URP 中采样全局纹理
                        float closestDist = SAMPLE_TEXTURE2D(_FoVShadowMap, sampler_FoVShadowMap, shadowUV).r;

                        // 计算当前距离
                        float currentDist = distance(worldPos, _FoVLightPos) / _FoVRange;

                        // 比较 (Bias)
                        float bias = 0.01;
                        shadowVis = (currentDist - bias) < closestDist ? 1.0 : 0.0;
                                                    
                        if(shadowUV.x < 0 || shadowUV.x > 1 || shadowUV.y < 0 || shadowUV.y > 1)
                            shadowVis = 0.0;
                        // 剔除背后投影
                        if (shadowClip.w < 0) shadowVis = 0;
                    }
                //}
                // ==========================================================
                // 第三步：混合 (叠加)
                // ==========================================================
                
                // 逻辑：可见性 = 球形渐变值 * 阴影开关
                // 1. 如果 shadowVis 是 0 (被墙挡住)，结果为 0 -> 黑
                // 2. 如果 sphereVis 是 0 (超出范围)，结果为 0 -> 黑
                // 3. 如果 shadowVis 是 1 且 sphereVis 是 0.5 (在边缘)，结果为 0.5 -> 半透明

                half s = saturate(shadowVis);
                half v = step(0.9,saturate(sphereVis));
                // 只有当两者都 > 0.5 时才亮（重叠区域）
                half finalVisibility = s * v;
                // 输出：混合黑色(或阴影色)与场景色
                return lerp(_FogColor, sceneColor, saturate(finalVisibility + _FogColor.a));
            }
            ENDHLSL
        }
    }
}