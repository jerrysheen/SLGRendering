Shader "Hidden/Outlines/Wide Outline/Outline"
{
    Properties
    {
        _SrcBlend ("_SrcBlend", Int) = 0
        _DstBlend ("_DstBlend", Int) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
        }

        HLSLINCLUDE
        #define SNORM16_MAX_FLOAT_MINUS_EPSILON ((float)(32768-2) / (float)(32768-1))
        #define FLOOD_ENCODE_OFFSET float2(1.0, SNORM16_MAX_FLOAT_MINUS_EPSILON)
        #define FLOOD_ENCODE_SCALE float2(2.0, 1.0 + SNORM16_MAX_FLOAT_MINUS_EPSILON)
        #define FLOOD_NULL_POS float2(-1.0, -1.0)
        ENDHLSL

        Pass // 0: SILHOUETTE
        {
            Name "SILHOUETTE"

            Cull Off
            ZWrite Off
            ZTest Always

            Stencil
            {
                Ref 1
                ReadMask 1
                Comp Equal
            }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #if UNITY_VERSION < 202300
            CBUFFER_START(UnityPerMaterial)
            float4 _BlitTexture_TexelSize;
            CBUFFER_END
            #endif

            half4 _OutlineColor;

            half Frag(Varyings IN) : SV_TARGET
            {
                return 1;
            }
            ENDHLSL
        }

        Pass // 1: JFA INIT
        {
            Name "JFA INIT"

            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #if UNITY_VERSION < 202300
            CBUFFER_START(UnityPerMaterial)
            float4 _BlitTexture_TexelSize;
            CBUFFER_END
            #endif

            float2 Frag(Varyings IN) : SV_TARGET
            {
                // integer pixel position
                int2 uvInt = IN.positionCS.xy;

                // sample silhouette texture for sobel
                half3x3 values;

                UNITY_UNROLL
                for (int u = 0; u < 3; u++) {
                    UNITY_UNROLL
                    for (int v = 0; v < 3; v++) {
                        uint2 sampleUV = clamp(uvInt + int2(u - 1, v - 1), int2(0, 0), (int2)_BlitTexture_TexelSize.zw - 1);
                        float4 sample = _BlitTexture.Load(int3(sampleUV, 0));
                        values[u][v] = step(0.01, max(sample.r, max(sample.g, max(sample.b, sample.a))));
                    }
                }

                float2 screen_space_position = IN.positionCS.xy * abs(_BlitTexture_TexelSize.xy) * FLOOD_ENCODE_SCALE - FLOOD_ENCODE_OFFSET;

                // inside mask
                if (values._m11 > 0.99)
                    return screen_space_position;
                else return FLOOD_NULL_POS;
            }
            ENDHLSL
        }

        Pass // 2: JFA FLOOD SINGLE AXIS
        {
            Name "JFA FLOOD SINGLE AXIS"

            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #pragma multi_compile_local _ SCALE_WITH_RESOLUTION
            
            CBUFFER_START(UnityPerMaterial)
                #if UNITY_VERSION < 202300
                float4 _BlitTexture_TexelSize;
                #endif
            CBUFFER_END

            int2 _AxisWidth;

            half2 frag(Varyings IN) : SV_TARGET
            {
                // integer pixel position
                int2 uvInt = int2(IN.positionCS.xy);

                // initialize best distance at infinity
                float best_distance = 1.#INF;
                float2 best_position;

                // jump samples
                UNITY_UNROLL
                for (int u = -1; u <= 1; u++) {
                    // calculate offset sample position
                    int2 offset_uv = uvInt + int2((float2)_AxisWidth) * u;

                    // .Load() acts funny when sampling outside of bounds, so don't
                    offset_uv = clamp(offset_uv, int2(0, 0), (int2)_BlitTexture_TexelSize.zw - 1);

                    // decode position from buffer
                    float2 offset_position = (_BlitTexture.Load(int3(offset_uv, 0)).rg + FLOOD_ENCODE_OFFSET) * _BlitTexture_TexelSize.zw / FLOOD_ENCODE_SCALE;

                    // the offset from current position
                    float2 disp = IN.positionCS.xy - offset_position;

                    // square distance
                    float pixelDistance = dot(disp, disp);

                    // if offset position isn't a null position or is closer than the best
                    // set as the new best and store the position
                    if (offset_position.x != -1.0 && pixelDistance < best_distance) {
                        best_distance = pixelDistance;
                        best_position = offset_position;
                    }
                }

                // if not valid best distance output null position, otherwise output encoded position
                return isinf(best_distance) ? FLOOD_NULL_POS : best_position * _BlitTexture_TexelSize.xy * FLOOD_ENCODE_SCALE - FLOOD_ENCODE_OFFSET;
            }
            ENDHLSL
        }

        Pass // 3: OUTLINE
        {
            Name "OUTLINE"

            Cull Off
            ZTest [_ZTest]
            ZWrite Off
            Blend [_SrcBlend] [_DstBlend]

            Stencil
            {
                Ref 0
                Comp Equal
                Pass Zero
                Fail Zero
            }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag

            #pragma multi_compile_local _ CUSTOM_DEPTH
            #pragma multi_compile_local _ VERTEX_ANIMATION
            #pragma multi_compile_local _ INFORMATION_BUFFER

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #pragma multi_compile_local _ SCALE_WITH_RESOLUTION
            
            CBUFFER_START(UnityPerMaterial)
                float _ReferenceResolution;
                #if UNITY_VERSION < 202300
                float4 _BlitTexture_TexelSize;
                #endif
            CBUFFER_END

            TEXTURE2D(_SilhouetteBuffer);
            SAMPLER(sampler_SilhouetteBuffer);
            float4 _SilhouetteBuffer_TexelSize;

            float4 SampleSilhouetteBuffer(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_SilhouetteBuffer, sampler_SilhouetteBuffer, uv).rgba;
            }

            TEXTURE2D(_InformationBuffer);
            SAMPLER(sampler_InformationBuffer);
            float4 _InformationBuffer_TexelSize;
            
            float SampleInformationBuffer(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_InformationBuffer, sampler_InformationBuffer, uv).r;
            }

            #if defined(CUSTOM_DEPTH)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_SilhouetteDepthBuffer);
            SAMPLER(sampler_SilhouetteDepthBuffer);
            float4 _SilhouetteDepthBuffer_TexelSize;

            float SampleSilhouetteDepthBuffer(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_SilhouetteDepthBuffer, sampler_SilhouetteDepthBuffer, uv).r;
            }

            half4 _OutlineOccludedColor;
            #endif

            half4 _OutlineColor;
            float _OutlineWidth;
            float _OutlineGap;
            float _RenderScale;

            half4 frag(Varyings IN) : SV_Target
            {
                float scale = 1;
                #if defined(SCALE_WITH_RESOLUTION)
                scale = _ScreenParams.y / _ReferenceResolution;
                #endif

                half width = _OutlineWidth * scale;
                
                // integer pixel position
                int2 uvInt = int2(IN.positionCS.xy);

                // load encoded position
                float2 encodedPos = _BlitTexture.Load(int3(uvInt, 0)).rg;

                // early out if null position
                if (encodedPos.y == -1) {
                    return half4(0, 0, 0, 0);
                }

                // decode closest position
                float2 nearestPos = (encodedPos + FLOOD_ENCODE_OFFSET) * abs(_ScreenParams.xy) / FLOOD_ENCODE_SCALE;

                // current pixel position
                float2 currentPos = IN.positionCS.xy * (1.0 / _RenderScale);

                // distance in pixels to closest position
                half dist = length(nearestPos - currentPos);
                
                // if(SampleSilhouetteDepthBuffer(IN.texcoord) > 0) {
                //     return 0;
                // }

                // calculate outline
                // + 1.0 is because encoded nearest position is half a pixel inset
                // not + 0.5 because we want the anti-aliased edge to be aligned between pixels
                // distance is already in pixels, so this is already perfectly anti-aliased!
                
                #if defined(INFORMATION_BUFFER)
                width = SampleInformationBuffer(nearestPos / _ScreenParams.xy) * 100.0f;
                #endif
                
                half outline = saturate(width - dist + 1.0) - saturate((_OutlineGap * width) - dist + 1.0);

                #if defined(CUSTOM_DEPTH)
                half depth1 = SampleSilhouetteDepthBuffer(nearestPos / _ScreenParams.xy);
                half depth2 = SampleSceneDepth(IN.texcoord);
                half depthDifference = LinearEyeDepth(depth1, _ZBufferParams) - LinearEyeDepth(depth2, _ZBufferParams);
                half g = depthDifference > 0.001; // depth check threshold here
                
                half4 color = SampleSilhouetteBuffer(nearestPos / _ScreenParams.xy);
                half4 col = g > 0 ? _OutlineOccludedColor : color;
                
                col *= outline;
                
                return col;
                
                #else
                
                // #if defined(VERTEX_ANIMATION)
                // half4 color = _OutlineColor;
                // #else
                half4 color = SampleSilhouetteBuffer(nearestPos / _ScreenParams.xy);
                // #endif
                color *= outline;

                return color;
                
                 #endif
            }
            ENDHLSL
        }
    }
}