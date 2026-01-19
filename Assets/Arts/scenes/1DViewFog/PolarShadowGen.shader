Shader "Hidden/PolarShadowGen_URP"
{
    Properties
    {
        _MainColor ("Debug Color", Color) = (1,0,0,1)
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
            "RenderPipeline"="UniversalPipeline"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #define PI 3.14159265359
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

        struct Attributes
        {
            float4 positionOS : POSITION;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float  dist       : TEXCOORD0;
        };

        float3 _PlayerPos;
        float  _MaxRadius;

        // wrap angle to [-PI, PI]
        float WrapAnglePI(float a)
        {
            // move to [0, TWO_PI)
            a = a + PI;
            a = a - floor(a / TWO_PI) * TWO_PI;
            // back to [-PI, PI)
            return a - PI;
        }

        // PolarShadowGen.shader -> HLSLINCLUDE 块内

        Varyings CommonVert(Attributes input, float seamOffsetRad)
        {
            Varyings output;
            float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
            float3 v = positionWS - _PlayerPos;

            float angle = atan2(v.z, v.x);
            // 1. 对顶点角度应用偏移
            angle = WrapAnglePI(angle + seamOffsetRad);
            
            float dist = length(v.xz);

            // 物体 pivot 作为参考角度
            float3 pivotWS = TransformObjectToWorld(float3(0, 0, 0));
            float3 pivotView = pivotWS - _PlayerPos;
            float angleRef = atan2(pivotView.z, pivotView.x);

            // [修正点]：angleRef 必须跟随 seamOffsetRad 一起旋转！
            // 这样计算差值 d = angle - angleRef 时，两者的坐标系才一致。
            angleRef = WrapAnglePI(angleRef + seamOffsetRad); 

            // seam 修复：让同一物体的角度连续
            angle = SeamFixAngle(angle, angleRef);
            
            float clipX = angle / PI;

            float clipY = sin((positionWS.y)) / 2.0f;
            float clipZ = 0.5;

            output.positionCS = float4(clipX, clipY, clipZ, 1.0);
            output.dist = dist;
            return output;
        }

        half4 CommonFrag(Varyings input) : SV_Target
        {
            // BlendOp Min 会保留最小 dist
            return half4(input.dist, input.dist, 0, 1);
        }
        ENDHLSL

        // ---------- Pass 0 : write R (seam = 0) ----------
        Pass
        {
            Name "ShadowGen_R"
            Tags { "LightMode"="UniversalForward" }

            Cull Front
            ZWrite Off
            ZTest Always

            BlendOp Min
            Blend One One
            ColorMask R

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            Varyings vert(Attributes input)
            {
                return CommonVert(input, 0.0);
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 只写 R，但返回什么都无所谓（ColorMask 控制）
                return CommonFrag(input);
            }
            ENDHLSL
        }

        // ---------- Pass 1 : write G (seam rotated 180deg) ----------
        Pass
        {
            Name "ShadowGen_G"
            Tags { "LightMode"="UniversalForward" }

            Cull Front
            ZWrite Off
            ZTest Always

            BlendOp Min
            Blend One One
            ColorMask G

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            Varyings vert(Attributes input)
            {
                // seam offset = PI (180°)
                return CommonVert(input, PI);
            }

            half4 frag(Varyings input) : SV_Target
            {
                return CommonFrag(input);
            }
            ENDHLSL
        }
    }
}
