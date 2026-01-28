Shader "Hidden/Outlines/Soft Outline/Silhouette"
{
    Properties
    {
        _OutlineColor ("_OutlineColor", Color) = (1, 1, 1, 1)
        
        [Toggle(ALPHA_CUTOUT)] _AlphaCutout ("_AlphaCutout", Float) = 0
        _AlphaCutoutTexture ("_AlphaCutoutTexture", 2D) = "white" {}
        _AlphaCutoutThreshold ("_AlphaCutoutThreshold", Float) = 0.5
        _AlphaCutoutUVTransform ("_AlphaCutoutUVTransform", Vector) = (1, 1, 0, 0)

        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0.0
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull [_Cull]
        ZWrite Off
        ZTest [_ZTest]

        Blend Off

        Pass // 0: SILHOUETTE
        {
            Name "SILHOUETTE"

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ DOTS_INSTANCING_ON
            #if UNITY_PLATFORM_ANDROID || UNITY_PLATFORM_WEBGL || UNITY_PLATFORM_UWP
                #pragma target 3.5 DOTS_INSTANCING_ON
            #else
                #pragma target 4.5 DOTS_INSTANCING_ON
            #endif
            
            #pragma multi_compile_local _ ALPHA_CUTOUT

            TEXTURE2D(_AlphaCutoutTexture);
            SAMPLER(sampler_AlphaCutoutTexture);
            
            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                half _AlphaCutoutThreshold;
                float4 _AlphaCutoutUVTransform;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                
                #if defined(ALPHA_CUTOUT)
                float2 texcoord     : TEXCOORD0;
                #endif

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                
                #if defined(ALPHA_CUTOUT)
                float2 uv           : TEXCOORD0;
                #endif

                UNITY_VERTEX_OUTPUT_STEREO // VR support
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT); // VR support

                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                #if defined(ALPHA_CUTOUT)
                OUT.uv = IN.texcoord * _AlphaCutoutUVTransform.xy + _AlphaCutoutUVTransform.zw;
                #endif
                
                return OUT;
            }

            half4 frag(Varyings IN) : SV_TARGET
            {
                #if defined(ALPHA_CUTOUT)
                float alpha = SAMPLE_TEXTURE2D(_AlphaCutoutTexture, sampler_AlphaCutoutTexture, IN.uv).a;
                clip(alpha - _AlphaCutoutThreshold);
                #endif
                
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
}