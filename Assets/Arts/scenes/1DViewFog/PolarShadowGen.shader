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

            #define PI      3.14159265359
            #define TWO_PI  6.28318530718

            // 把 angle 钳制到 [-PI, PI]，避免 clipX 超界被裁掉
            float ClampAnglePI(float a)
            {
                return clamp(a, -PI, PI);
            }

            // 以物体 pivot 的 angleRef 为参考，把顶点 angle “拉到同一侧”
            float SeamFixAngle(float angle, float angleRef)
            {
                float d = angle - angleRef;

                // 如果差值跨过 PI，说明发生 seam 跳变
                if (d > PI)      angle -= TWO_PI;
                else if (d < -PI) angle += TWO_PI;

                // 最后钳回 [-PI, PI]，避免超出 clipX 范围
                // 这一步相当于把跨缝顶点“挤到边界”，避免跨屏三角形
                return ClampAnglePI(angle);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 viewPos = positionWS - _PlayerPos;

                // 顶点角度/距离
                float angle = atan2(viewPos.z, viewPos.x);
                float dist  = length(viewPos.xz);

                // 物体 pivot 作为参考角度（不需要额外传参）
                float3 pivotWS = TransformObjectToWorld(float3(0, 0, 0));
                float3 pivotView = pivotWS - _PlayerPos;
                float angleRef = atan2(pivotView.z, pivotView.x);

                // seam 修复：让同一物体的角度连续
                angle = SeamFixAngle(angle, angleRef);

                float clipX = angle / PI;

                // 你原来的 clipY、clipZ 逻辑保持不变
                float clipY = sin((positionWS.y)) / 2.0f;

                float clipZ = 0.5;

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