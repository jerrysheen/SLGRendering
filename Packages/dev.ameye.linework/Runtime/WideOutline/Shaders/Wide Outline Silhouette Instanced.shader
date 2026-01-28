Shader "Hidden/Outlines/Wide Outline/Silhouette Instanced"
{
    Properties
    {
        _OutlineColor ("_OutlineColor", Color) = (1, 1, 1, 1)
        _Information ("_Information", Vector) = (1, 1, 1, 1)
        
        [Toggle(ALPHA_CUTOUT)] _AlphaCutout ("_AlphaCutout", Float) = 0
        _AlphaCutoutTexture ("_AlphaCutoutTexture", 2D) = "white" {}
        _AlphaCutoutThreshold ("_AlphaCutoutThreshold", Float) = 0.5
        _AlphaCutoutUVTransform ("_AlphaCutoutUVTransform", Vector) = (1, 1, 0, 0)
        
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0.0
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 0.0
        _ZWrite("ZWrite", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }
        
        Cull [_Cull]
        ZWrite [_ZWrite] // ! Required
        ZTest [_ZTest]
        
        Blend Off
        
        Pass // 0: SILHOUETTE
        {
            Name "SILHOUETTE"

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            #pragma vertex vert
            #pragma fragment frag
            
            #pragma multi_compile_instancing
            
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #if UNITY_PLATFORM_ANDROID || UNITY_PLATFORM_WEBGL || UNITY_PLATFORM_UWP
                #pragma target 3.5 DOTS_INSTANCING_ON
            #else
                #pragma target 4.5 DOTS_INSTANCING_ON
            #endif

            #pragma multi_compile_local _ ALPHA_CUTOUT
            #pragma multi_compile_local _ INFORMATION_BUFFER

            TEXTURE2D(_AlphaCutoutTexture);
            SAMPLER(sampler_AlphaCutoutTexture);
            
            UNITY_INSTANCING_BUFFER_START(InstancedProperties)
                UNITY_DEFINE_INSTANCED_PROP(float4, _OutlineColor)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Information)
                UNITY_DEFINE_INSTANCED_PROP(float4, _AlphaCutoutUVTransform)
                UNITY_DEFINE_INSTANCED_PROP(float, _AlphaCutoutThreshold)
            UNITY_INSTANCING_BUFFER_END(InstancedProperties)
            
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
                
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                // FIXME: this 1.01  fixes some artifacts... not sure I want it.. it also introduces a gap!! -> left at 1.0 for now again
                float offset = 1.00;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz * offset);
                #if defined(ALPHA_CUTOUT)
                float4 uvTransform = UNITY_ACCESS_INSTANCED_PROP(InstancedProperties, _AlphaCutoutUVTransform);
                OUT.uv = IN.texcoord * uvTransform.xy + uvTransform.zw;
                #endif
                
                return OUT;
            }

            half4 frag(Varyings IN) : SV_TARGET
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                
                #if defined(ALPHA_CUTOUT)
                float alpha = SAMPLE_TEXTURE2D(_AlphaCutoutTexture, sampler_AlphaCutoutTexture, IN.uv).a;
                clip(alpha - UNITY_ACCESS_INSTANCED_PROP(InstancedProperties, _AlphaCutoutThreshold));
                #endif

                #if defined(INFORMATION_BUFFER)
                return UNITY_ACCESS_INSTANCED_PROP(InstancedProperties, _Information);
                #else
                return UNITY_ACCESS_INSTANCED_PROP(InstancedProperties, _OutlineColor);
                #endif
            }
            ENDHLSL
        }
    }
}