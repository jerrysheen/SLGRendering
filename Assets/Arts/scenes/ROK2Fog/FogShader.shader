Shader "Elex/FogShader"
{
    Properties
    {
        _Fog1 ("_Fog1", 2D) = "white" {}
        _Fog2 ("_Fog2", 2D) = "white" {}
        _FogDepthLerp ("Fog下压边缘过渡", Range(0, 6)) = 3.3
        _FLowSpeednDir ("迷雾边缘流速xy第一层|zw第二层", Vector) = (0.2, 0.2, 0.15, 0.15)
        _FLowTilling ("迷雾边缘贴图平铺比例xy第一层|zw第二层", Vector) = (2, 2, 2, 2)
        _FLowSpeednDir0 ("迷雾主体流速", Float) = 10
        _FogDensity1 ("迷雾浓度|迷雾扰动", Range(0, 1)) = 0.6
        _FogDensity2 ("迷雾浓度", Range(0, 0.3)) = 0.1
        _FogDisturb ("迷雾扰动", Range(0, 1)) = 0.6
        
        [HDR]_FogColor ("浓迷雾部分颜色", Color) = (1, 1, 1, 1)
        [HDR]_FogColor1 ("淡迷雾部分颜色", Color) = (0.3, 0.3, 0.3, 1)


    }
    SubShader
    {
        Tags 
        { 
            "Queue"="Transparent" 
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
            #pragma enable_d3d11_debug_symbols

            struct Attributes
            {
                float4 positionOS : POSITION;
                half2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half2 fogHeight : TEXCOORD0;
                float3 positionWS : TEXCOORD1;

                float viewZ : TEXCOORD2;  // 添加view space depth
            };

            CBUFFER_START(UnityPerMaterial)
                float _FogDepthLerp;
                half4 _Fog1_ST;
                half4 _Fog2_ST;
                half4 _FLowSpeednDir;
                half _FLowSpeednDir0;
                half4 _FLowTilling;
                half _FogDensity1;
                half _FogDisturb;
                half _FogDensity2;
                half4 _FogColor;
                half4 _FogColor1;
            CBUFFER_END
            TEXTURE2D(_Fog1);            SAMPLER(sampler_Fog1);
            TEXTURE2D(_Fog2);            SAMPLER(sampler_Fog2);
            Varyings vert (Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                
                // 计算world space位置，然后转换到view space获取z分量
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 positionVS = TransformWorldToView(positionWS);
                output.viewZ = positionVS.z;  // view space的z分量（通常是负值）

                output.fogHeight = input.uv;
                output.positionWS = positionWS;
                return output;
            }

            half4 frag (Varyings input) : SV_Target
            {
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                
                float2 WorldUV = input.positionWS.xz / 100.0f;
                float4 FlowSpeedNDir = _Time.yyyy / 10.0f * _FLowSpeednDir;

                float2 Layer1Speed = (WorldUV * float2(1.3, 1.3)) + FlowSpeedNDir.zw;
                float2 Layer2Speed = (WorldUV * half2(1.1, 1.1)) - FlowSpeedNDir.xy;
                half fogFlowDir1 = SAMPLE_TEXTURE2D(_Fog1, sampler_Fog1, _FLowTilling.xy * Layer1Speed).x;
                half fogFlowDir2 = SAMPLE_TEXTURE2D(_Fog1, sampler_Fog1, _FLowTilling.zw * Layer2Speed).x;
                

                half fogFlowTotal = (fogFlowDir1 + fogFlowDir2) * 0.5;
                half edge = input.fogHeight.x;
                edge = log2(edge);
                float _30x = edge * _FogDepthLerp;
                edge *= 0.4f;
                edge = exp2(edge);
                _30x = exp2(_30x);
                float _31 = min(_30x, 1.0);
                _31 = log2(_31);
                fogFlowTotal = exp2(_31 * fogFlowTotal);
                edge *= fogFlowTotal;
                //...........采样噪声图
                float3 worldPosToCamera = input.positionWS + (-_WorldSpaceCameraPos);
                float cameraDistance = sqrt(dot(worldPosToCamera, worldPosToCamera));
                float3 worldToCamUV = worldPosToCamera / cameraDistance;
                float worldToCamUVy = max(abs(worldToCamUV.y), 1.0);
                float2 worldToCamUVxz = worldToCamUV.xz * _FogDisturb;
                worldToCamUVxz = worldToCamUVxz / worldToCamUVy;
                half noise0;
                half noise1;
                noise0 = SAMPLE_TEXTURE2D(_Fog2, sampler_Fog2, _Fog2_ST.xy * WorldUV + _Time.yy /_FLowSpeednDir0).x;
                noise1 = SAMPLE_TEXTURE2D(_Fog2, sampler_Fog2, _Fog2_ST.xy * WorldUV + _Time.yy /(_FLowSpeednDir0 * 1.5f)).x;
                half noiseTotal = ((-noise0) * noise1 * _FogDensity1) + 1.0;
                worldToCamUVxz = (worldToCamUVxz * noiseTotal) + WorldUV;
                half noise2 = SAMPLE_TEXTURE2D(_Fog2, sampler_Fog2,  worldToCamUVxz );
                half fogResult = noise2 * noise1;

                
                float depth = SampleSceneDepth(screenUV); //采样深度
                float depthValue = LinearEyeDepth(depth, _ZBufferParams); //转到LinearEyeDepth
                depthValue += -abs(input.viewZ);

                float _33x = noise2 *  _FogDensity1 * _FogDensity2;
                _30x = abs(depthValue) * _33x;
                _30x = clamp(_30x,0,1);
                _30x *= _30x;
                float _res = saturate(min(edge, _30x));
                //smoothstep(depthValue, 0.0f, 5.0f);
                half4 color = _FogColor - _FogColor1;
                color = color * fogResult + _FogColor1;
                return half4(color.xyz, _res * _FogColor.a);
          } 
            ENDHLSL
        }
    }
}
