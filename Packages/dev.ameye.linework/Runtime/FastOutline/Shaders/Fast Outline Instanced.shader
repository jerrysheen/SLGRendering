Shader "Hidden/Outlines/Fast Outline/Outline Instanced"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (0,1,0,1)
        _OutlineOccludedColor ("Outline Occluded Color", Color) = (1,0,0,1)
        _OutlineWidth ("Outline Width", Range (0, 1)) = 0.5
        _MinimumOutlineWidth ("Minimum Outline Width", Range (0, 1)) = 0.5

        _SrcBlend ("_SrcBlend", Int) = 0
        _DstBlend ("_DstBlend", Int) = 0
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

        ZWrite On
        Cull [_Cull]
        ZTest [_ZTest]
        Blend [_SrcBlend] [_DstBlend]

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        
        #pragma fragment frag

        #pragma multi_compile_instancing

        #pragma multi_compile _ DOTS_INSTANCING_ON
        #if UNITY_PLATFORM_ANDROID || UNITY_PLATFORM_WEBGL || UNITY_PLATFORM_UWP
            #pragma target 3.5 DOTS_INSTANCING_ON
        #else
            #pragma target 4.5 DOTS_INSTANCING_ON
        #endif

        #pragma multi_compile _ SCALE_WITH_DISTANCE
        #pragma multi_compile _ OCCLUSION
        
        #if defined(OCCLUSION)
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
        #endif

        UNITY_INSTANCING_BUFFER_START(InstancedProperties)
            UNITY_DEFINE_INSTANCED_PROP(half4, _OutlineColor)
            UNITY_DEFINE_INSTANCED_PROP(half4, _OutlineOccludedColor)
            UNITY_DEFINE_INSTANCED_PROP(half, _OutlineWidth)
            UNITY_DEFINE_INSTANCED_PROP(half, _MinimumOutlineWidth)
        UNITY_INSTANCING_BUFFER_END(InstancedProperties)
        
        struct Attributes
        {
            float4 positionOS : POSITION;
            float4 tangentOS : TANGENT;
            half3 normalOS : NORMAL;
            half4 color : COLOR;
            half2 bakedDirection: TEXCOORD7;
            
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionHCS : SV_POSITION;

            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        half4 frag(Varyings IN) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(IN);
            
            #if defined(OCCLUSION)
            float2 uv = IN.positionHCS.xy / _ScaledScreenParams.xy;

            #if UNITY_REVERSED_Z
            real depth = SampleSceneDepth(uv);
            #else
            real depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(uv));
            #endif
            return IN.positionHCS.z < depth ? UNITY_ACCESS_INSTANCED_PROP(InstancedProperties, _OutlineOccludedColor) : UNITY_ACCESS_INSTANCED_PROP(InstancedProperties, _OutlineColor);
            #else
            return UNITY_ACCESS_INSTANCED_PROP(InstancedProperties, _OutlineColor);
            #endif
        }
        ENDHLSL

        Pass // 0: VERTEX POSITION (OBJECT SPACE)
        {
            Name "VERTEX POSITION (OBJECT SPACE)"

            HLSLPROGRAM
            #pragma vertex vert

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
            
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                half width = UNITY_ACCESS_INSTANCED_PROP(InstancedProperties, _OutlineWidth);

                #if defined(SCALE_WITH_DISTANCE)
                half distance = TransformObjectToHClip(IN.positionOS.xyz).z;
                width = max(width / (distance * 100.0), UNITY_ACCESS_INSTANCED_PROP(InstancedProperties, _MinimumOutlineWidth));
                #endif

                // Move vertex along vertex position in object space.
                IN.positionOS.xyz += IN.positionOS.xyz * width;

                // Transform vertex from object space to clip space.
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);

                return OUT;
            }
            ENDHLSL
        }

        Pass // 1: NORMALIZED VERTEX POSITION (OBJECT SPACE)
        {
            Name "NORMALIZED VERTEX POSITION (OBJECT SPACE)"

            HLSLPROGRAM
            #pragma vertex vert

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
            
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                half width = UNITY_ACCESS_INSTANCED_PROP(InstancedProperties, _OutlineWidth);

                #if defined(SCALE_WITH_DISTANCE)
                half distance = TransformObjectToHClip(IN.positionOS.xyz).z;
                width = max(width / (distance * 100.0), UNITY_ACCESS_INSTANCED_PROP(InstancedProperties, _MinimumOutlineWidth));
                #endif

                // Move vertex along normalized vertex position in object space.
                IN.positionOS.xyz += normalize(IN.positionOS.xyz) * width;

                // Transform vertex from object space to clip space.
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);

                return OUT;
            }
            ENDHLSL
        }

        Pass // 2: NORMAL VECTOR (OBJECT SPACE)
        {
            Name "NORMAL VECTOR (OBJECT SPACE)"

            HLSLPROGRAM
            #pragma vertex vert

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
            
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                half width = UNITY_ACCESS_INSTANCED_PROP(InstancedProperties, _OutlineWidth);

                #if defined(SCALE_WITH_DISTANCE)
                half distance = TransformObjectToHClip(IN.positionOS.xyz).z;
                width = max(width / (distance * 100.0), UNITY_ACCESS_INSTANCED_PROP(InstancedProperties, _MinimumOutlineWidth));
                #endif

                // Move vertex along normal vector in object space.
                IN.positionOS.xyz += IN.normalOS * width;

                // Transform vertex from object space to clip space.
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);

                return OUT;
            }
            ENDHLSL
        }

        Pass // 3: VERTEX COLOR (OBJECT SPACE)
        {
            Name "VERTEX COLOR (OBJECT SPACE)"

            HLSLPROGRAM
            #pragma vertex vert

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
            
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                half width = UNITY_ACCESS_INSTANCED_PROP(InstancedProperties, _OutlineWidth);

                #if defined(SCALE_WITH_DISTANCE)
                half distance = TransformObjectToHClip(IN.positionOS.xyz).z;
                width = max(width / (distance * 100.0), UNITY_ACCESS_INSTANCED_PROP(InstancedProperties, _MinimumOutlineWidth));
                #endif

                // Move vertex along vertex color in object space.
                IN.positionOS.xyz += IN.color.xyz * width;

                // Transform vertex from object space to clip space.
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);

                return OUT;
            }
            ENDHLSL
        }

        Pass // 4: NORMAL VECTOR (CLIP SPACE)
        {
            Name "NORMAL VECTOR (CLIP SPACE)"

            HLSLPROGRAM
            #pragma vertex vert
            
            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                half width = UNITY_ACCESS_INSTANCED_PROP(InstancedProperties, _OutlineWidth);

                // Transform vertex from object space to clip space.
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);

                #if defined(SCALE_WITH_DISTANCE)
                half distance = OUT.positionHCS.z;
                width = max(width / (distance * 100.0), UNITY_ACCESS_INSTANCED_PROP(InstancedProperties, _MinimumOutlineWidth));
                #endif

                // Transform normal vector from object space to clip space.
                // NOTE: Why is this just with regular matrices and not inverted/transpose for normal vectors?
                float3 normalHCS = mul((float3x3)UNITY_MATRIX_VP, mul((float3x3)UNITY_MATRIX_M, IN.normalOS));

                // In clip space, x and y correspond to horizontal/vertical on screen so when we extrude in clip space,
                // we don't extrude with a physical object or world space 3D distance, but a distance/portion of the screen.
                // So yes, we only extrude in x/y direction
                // We divide by _ScreenParams.xy which is width/height
                // We multiply by positionHCS.w because during perspective division, the positionHCS.xyz will be divided by positionHCS.w so to end up with an
                // equal-width outline AFTER perspective division, we multiply by positionHCS.w
                OUT.positionHCS.xy += normalize(normalHCS.xy) / _ScreenParams.xy * OUT.positionHCS.w * width * 2;
                //OUT.positionHCS.xy += normalize(normalHCS.xy) / _ScreenParams.xy * OUT.positionHCS.w * width * 2;


                //    width =  width *;

                // using a depth offset we can 'move back' the outline artificially
                // OUT.positionHCS.z -= depth_offset;

                // NOTE: can precompute reciprocals of screen width/height and premultiplying them by outline width in pixels on CPU then
                // passing them to shader! so yeah precompute
                // also, now UNITY_ACCESS_INSTANCED_PROP(InstancedProperties, _OutlineWidth) is in pixels!
                // clip space, then x/y coordinates range from -w to +w, after perspective division this is -1 to +1 so total range of 2
                // so need to divide screen width/height by 2 or multiply total line by 2 so that _width will correspond to 1 pixel
                return OUT;
            }
            ENDHLSL
        }

        Pass // 5: NORMAL VECTOR (SCREEN SPACE)
        {
            Name "NORMAL VECTOR (SCREEN SPACE)"

            HLSLPROGRAM
            #pragma vertex vert
            Varyings vert(Attributes IN)
            {
                Varyings OUT;
            
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                half width = UNITY_ACCESS_INSTANCED_PROP(InstancedProperties, _OutlineWidth);

                // Transform vertex from object space to clip space.
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);

                #if defined(SCALE_WITH_DISTANCE)
                half distance = OUT.positionHCS.z;
                width = max(width / (distance * 100.0), UNITY_ACCESS_INSTANCED_PROP(InstancedProperties, _MinimumOutlineWidth));
                #endif

                // Transform normal vector from object space to view space.
                // When transforming normals you need to transform with the transpose of the inverse of the transformation for positions.
                float3 normalVS = normalize(mul((float3x3)UNITY_MATRIX_IT_MV, IN.normalOS));

                // this is normalHCS.xy?
                float2 offset = mul((float2x2)UNITY_MATRIX_P, normalVS.xy);

                OUT.positionHCS.xy += offset * OUT.positionHCS.z * width;

                return OUT;
            }
            ENDHLSL
        }

        Pass // 6: NORMAL VECTOR (WORLD SPACE)
        {
            Name "NORMAL VECTOR (WORLD SPACE)"

            HLSLPROGRAM
            #pragma vertex vert
            Varyings vert(Attributes IN)
            {
                Varyings OUT;
            
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                half width = UNITY_ACCESS_INSTANCED_PROP(InstancedProperties, _OutlineWidth);

                #if defined(SCALE_WITH_DISTANCE)
                half distance = TransformObjectToHClip(IN.positionOS.xyz).z;
                width = max(width / (distance * 100.0), UNITY_ACCESS_INSTANCED_PROP(InstancedProperties, _MinimumOutlineWidth));
                #endif

                // Transform vertex from object space to world space.
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);

                // Transform normal vector from object space to world space.
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);

                // Move vertex along normal vector in world space.
                positionWS += normalWS * width;

                // Transform vertex from world space to clip space.
                OUT.positionHCS = TransformWorldToHClip(positionWS);

                return OUT;
            }
            ENDHLSL
        }

        Pass // 7: SMOOTHED NORMALS
        {
            Name "SMOOTHED NORMALS"

            HLSLPROGRAM
            #pragma vertex vert

            float3 OctahedronToUnitVector(float2 octahedron)
            {
                float3 normal = float3(octahedron, 1 - dot(1, abs(octahedron)));
                if (normal.z < 0) {
                    normal.xy = (1 - abs(normal.yx)) * (normal.xy >= 0 ? float2(1, 1) : float2(-1, -1));
                }
                return normalize(normal);
            }

            float3 TransformTBN(float2 bakedNormal, float3x3 tbn)
            {
                float3 normal = OctahedronToUnitVector(bakedNormal);
                return mul(normal, tbn);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
            
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                half width = UNITY_ACCESS_INSTANCED_PROP(InstancedProperties, _OutlineWidth) * 0.1;

                #if defined(SCALE_WITH_DISTANCE)
                half distance = TransformObjectToHClip(IN.positionOS.xyz).z;
                width = max(width / (distance * 100.0), UNITY_ACCESS_INSTANCED_PROP(InstancedProperties, _MinimumOutlineWidth));
                #endif

                // Extract baked direction.
                float3 normalOS = normalize(IN.normalOS);
                float3 tangentOS = normalize(IN.tangentOS.xyz);
                float3 bitangentOS = normalize(cross(normalOS, tangentOS) * IN.tangentOS.w);
                float3x3 tbn = float3x3(tangentOS, bitangentOS, normalOS);
                float3 bakedDirection = TransformTBN(IN.bakedDirection, tbn);

                // Move vertex along baked direction in object space.
                IN.positionOS.xyz += bakedDirection * width;

                // Transform vertex from object space to clip space.
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);

                return OUT;
            }
            ENDHLSL
        }

        Pass // 8: EXPERIMENTAL
        {
            Name "EXPERIMENTAL"

            HLSLPROGRAM
            #pragma vertex vert

            float3 OctahedronToUnitVector(float2 octahedron)
            {
                float3 normal = float3(octahedron, 1 - dot(1, abs(octahedron)));
                if (normal.z < 0) {
                    normal.xy = (1 - abs(normal.yx)) * (normal.xy >= 0 ? float2(1, 1) : float2(-1, -1));
                }
                return normalize(normal);
            }

            float3 TransformTBN(float2 bakedNormal, float3x3 tbn)
            {
                float3 normal = OctahedronToUnitVector(bakedNormal);
                return mul(normal, tbn);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                // Extract baked direction.
                float3 normalOS = normalize(IN.normalOS);
                float3 tangentOS = normalize(IN.tangentOS.xyz);
                float3 bitangentOS = normalize(cross(normalOS, tangentOS) * IN.tangentOS.w);
                float3x3 tbn = float3x3(tangentOS, bitangentOS, normalOS);
                float3 bakedDirection = TransformTBN(IN.bakedDirection, tbn);
            
                float4 positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                float Set_OutlineWidth = positionHCS.w * UNITY_ACCESS_INSTANCED_PROP(InstancedProperties, _OutlineWidth);
                Set_OutlineWidth = min(Set_OutlineWidth, UNITY_ACCESS_INSTANCED_PROP(InstancedProperties, _OutlineWidth));
                Set_OutlineWidth *= UNITY_ACCESS_INSTANCED_PROP(InstancedProperties, _OutlineWidth);
                Set_OutlineWidth = min(Set_OutlineWidth, UNITY_ACCESS_INSTANCED_PROP(InstancedProperties, _OutlineWidth)) * 0.1;

                IN.positionOS.xyz += bakedDirection * Set_OutlineWidth;
            
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);

                return OUT;
            }
            ENDHLSL
        }
    }
}