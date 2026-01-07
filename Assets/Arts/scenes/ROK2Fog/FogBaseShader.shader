Shader "Elex/FogBaseShader"
{
    Properties
    {
        _FogColor ("Fog Color", Color) = (0.5, 0.5, 0.5, 1)
        _FogDensity ("Fog下压边缘过渡", Range(0, 5)) = 2
        _FogDepthLerp ("Fog建筑交互过渡", Range(0, 1)) = 0.1
    }
    SubShader
    {
        Tags 
        { 
            //"RenderType"="Transparent" 
            "Queue"="Transparent"
            //"RenderPipeline"="UniversalRenderPipeline"
        }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 fogHeight : TEXCOORD1;
                float viewZ : TEXCOORD2;  // 添加view space depth
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _FogColor;
                float _FogDensity;
                float _FogDepthLerp;
            CBUFFER_END

            Varyings vert (Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                
                // 计算world space位置，然后转换到view space获取z分量
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 positionVS = TransformWorldToView(positionWS);
                output.viewZ = positionVS.z;  // view space的z分量（通常是负值）
                output.fogHeight = input.uv;
                return output;
            }

            half4 frag (Varyings input) : SV_Target
            {
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                            
                
                float depth = SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_CameraDepthTexture, screenUV).r; //采样深度
                float depthValue = LinearEyeDepth(depth, _ZBufferParams); //转换深度到0-1区间灰度值
                
                depthValue -= abs(input.viewZ);
                depthValue = abs(depthValue) * _FogDepthLerp;
                depthValue = clamp(depthValue, 0.0, 1.0);
                depthValue *= depthValue;

                half vertexColor = clamp(input.fogHeight.x, 0.0, 1.0);
                vertexColor = log2(vertexColor);
                vertexColor *= _FogDensity;
                vertexColor = exp2(vertexColor);
                half minAlpha = min(depthValue, vertexColor);
                minAlpha = saturate(minAlpha * _FogColor.a);
                return half4(_FogColor.xyz, minAlpha);
          }
            ENDHLSL
        }
    }
}
