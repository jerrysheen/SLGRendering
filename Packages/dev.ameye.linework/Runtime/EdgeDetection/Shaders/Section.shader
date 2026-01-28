Shader "Hidden/Outlines/Edge Detection/Section"
{
    Properties
    {
        _SectionTexture ("Section Texture", 2D) = "white" {}
        _BreakUpAmount ("Break up amount", Range(0, 1)) = 0
        _BreakUpScale ("Break up scale", Range(0, 20)) = 0
        [Toggle(OBJECT_ID)] OBJECT_ID ("Object Id", Float) = 0
        [Toggle(PARTICLES)] PARTICLES ("Particles", Float) = 0
        [Toggle(BREAKUP)] BREAKUP ("Breakup", Float) = 0
        [KeywordEnum(NONE, VERTEX_COLOR, TEXTURE)] INPUT("Input", Float) = 0
        [KeywordEnum(R, G, B, A)] VERTEX_COLOR_CHANNEL("Vertex Color Channel", Float) = 0
        [KeywordEnum(R, G, B, A)] TEXTURE_CHANNEL("Texture Channel", Float) = 0
        [KeywordEnum(UV0, UV1, UV2, UV3)] TEXTURE_UV_SET("Texture UV Set", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
        }

        ZWrite Off
        Blend Off

        Pass // 0: OBJECT ID
        {
            Name "OBJECT ID"

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"

            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ DOTS_INSTANCING_ON
            #if UNITY_PLATFORM_ANDROID || UNITY_PLATFORM_WEBGL || UNITY_PLATFORM_UWP
                #pragma target 3.5 DOTS_INSTANCING_ON
            #else
                #pragma target 4.5 DOTS_INSTANCING_ON
            #endif

            #pragma multi_compile_local _ OBJECT_ID
            #pragma multi_compile_local _ PARTICLES
            #pragma multi_compile_local _ INPUT_VERTEX_COLOR INPUT_TEXTURE
            #pragma multi_compile_local VERTEX_COLOR_CHANNEL_R VERTEX_COLOR_CHANNEL_G VERTEX_COLOR_CHANNEL_B VERTEX_COLOR_CHANNEL_A
            #pragma multi_compile_local TEXTURE_CHANNEL_R TEXTURE_CHANNEL_G TEXTURE_CHANNEL_B TEXTURE_CHANNEL_A
            #pragma multi_compile_local TEXTURE_UV_SET_UV0 TEXTURE_UV_SET_UV1 TEXTURE_UV_SET_UV2 TEXTURE_UV_SET_UV3
            #pragma multi_compile_local _ BREAKUP
            
            struct Attributes
            {
                float4 positionOS : POSITION;

                #if defined(INPUT_VERTEX_COLOR)
                half4 color : COLOR0;
                #endif

                #if !defined(INPUT_TEXTURE) && defined(PARTICLES)
                float4 uv : TEXCOORD0;
                #endif

                #if defined(INPUT_TEXTURE)
                #if defined(TEXTURE_UV_SET_UV0)
                float4 uv : TEXCOORD0;
                #endif
                #if defined(TEXTURE_UV_SET_UV1)
                float4 uv : TEXCOORD1;
                #endif
                #if defined(TEXTURE_UV_SET_UV2)
                float4 uv          : TEXCOORD2;
                #endif
                #if defined(TEXTURE_UV_SET_UV3)
                float4 uv           : TEXCOORD3;
                #endif
                #endif

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS:TEXCOORD1;

                #if defined(INPUT_VERTEX_COLOR)
                float4 color : COLOR0;
                #endif
                #if defined(INPUT_TEXTURE) || defined(PARTICLES)
                float4 uv : TEXCOORD0;
                #endif

                UNITY_VERTEX_OUTPUT_STEREO // VR support
            };

            TEXTURE2D(_SectionTexture);
            SAMPLER(sampler_SectionTexture);

            CBUFFER_START(UnityPerMaterial)
                float4 _SectionTexture_ST;
                float _BreakUpAmount;
                float _BreakUpScale;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT); // VR support

                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                #if defined(INPUT_VERTEX_COLOR)
                OUT.color = IN.color;
                #endif
                #if defined(INPUT_TEXTURE) || defined(PARTICLES)
                OUT.uv.xy = TRANSFORM_TEX(IN.uv, _SectionTexture);
                OUT.uv.zw = IN.uv.zw;
                #endif

                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                return OUT;
            }

            float hash(float3 p) {
                // return frac(dot(p, p) * 0.3); // collision if dot product is an integer
                // return frac(dot(p, float3(0.754, 0.569, 0.321)));
                return frac(sin(dot(p, float3(12.9898, 78.233, 45.164))) * 43758.5453);
            }

            float noise(float3 p)
            {
                float s = 0.0;
                s += sin(dot(p, float3(1.5, 3.4598, 1.234)));
                s += sin(dot(p, float3(3.12, -3.234, 4.221)));
                s += sin(dot(p, float3(0.355, 2.3, -1.375)));
                s += sin(dot(p, float3(-0.156, -3.34, -0.4566)));
                s += sin(dot(p, float3(-4.1235, -0.485, -1.45)));
                s += sin(dot(p, float3(2.54, -0.879, -2.123)));
                return s / 6.0;
            }

            half4 frag(Varyings IN) : SV_TARGET
            {
                float id = 0.5;

                // Object id.
                #if defined(OBJECT_ID)
                id = hash(GetAbsolutePositionWS(UNITY_MATRIX_M._m03_m13_m23));
                const float epsilon = 1e-3;
                id = clamp(id, epsilon, 1.0-epsilon);
                #if defined(PARTICLES)
                float particle_id = frac(dot(IN.uv.zw, IN.uv.zw) * 0.3);
                id = max(id, particle_id);
                #endif
                #endif

                float sample = 0;

                // Vertex color.
                #if defined(INPUT_VERTEX_COLOR)
                #if defined(VERTEX_COLOR_CHANNEL_R)
                sample = IN.color.r;
                #endif
                #if defined(VERTEX_COLOR_CHANNEL_G)
                sample = IN.color.g;
                #endif
                #if defined(VERTEX_COLOR_CHANNEL_B)
                sample = IN.color.b;
                #endif
                #if defined(VERTEX_COLOR_CHANNEL_A)
                sample = IN.color.a;
                #endif
                #endif

                // Section texture.
                #if defined(INPUT_TEXTURE)
                float4 section = SAMPLE_TEXTURE2D(_SectionTexture, sampler_SectionTexture, IN.uv);
                #if defined(VERTEX_COLOR_CHANNEL_R)
                sample = section.r;
                #endif
                #if defined(VERTEX_COLOR_CHANNEL_G)
                sample = section.g;
                #endif
                #if defined(VERTEX_COLOR_CHANNEL_B)
                sample = section.b;
                #endif
                #if defined(VERTEX_COLOR_CHANNEL_A)
                sample = section.a;
                #endif
                #endif

                #if (defined(INPUT_VERTEX_COLOR) || defined(INPUT_TEXTURE)) && defined(OBJECT_ID)
                id = lerp(0, (sample + id) * 0.5, sample);
                #elif (defined(INPUT_VERTEX_COLOR) || defined(INPUT_TEXTURE)) && !defined(OBJECT_ID)
                id = sample;
                #endif

                // Keep values of 1 as a 1.
                if (sample == 1) id = 1;
                
                // Add noise-based break up to the sections and store in B channel.
                #if defined(BREAKUP)
                float3 noise_uvs = IN.positionWS * _BreakUpScale + float3(3.324, 34.2, 56.343);
                float contribution = noise(noise_uvs) * 0.5 + 0.5;
                contribution = 1.0 - step(_BreakUpAmount, contribution);
                return half4(id, contribution, 0.0, 1.0);
                #endif
                
                return half4(id, 0.0, 0.0, 1.0);
            }
            ENDHLSL
        }
    }
}