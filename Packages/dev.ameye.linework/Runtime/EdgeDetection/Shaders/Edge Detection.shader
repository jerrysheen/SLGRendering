Shader "Hidden/Outlines/Edge Detection/Outline"
{
    Properties
    {
        // Debug.
        [Toggle(DEBUG_SECTIONS_PERCEPTUAL)] _DebugSectionsPerceptual ("Debug Sections Perceptual", Float) = 0
        
        // Discontinuity sections.
        [Toggle(SECTIONS_MASK)] _SectionsMask ("Sections Mask", Float) = 0

        // Discontinuity depth.
        _DepthSensitivity ("Depth Sensitivity", Range(0, 50)) = 0
        _DepthDistanceModulation ("Depth Non-Linearity Factor", Range(0, 3)) = 1
        _GrazingAngleMaskPower ("Grazing Angle Mask Power", Range(0, 1)) = 1
        _GrazingAngleMaskHardness("Grazing Angle Mask Hardness", Range(0,1)) = 1
        [Toggle(DEPTH_MASK)] _DepthMask ("Depth Mask", Float) = 0

        // Discontinuity normals.
        _NormalSensitivity ("Normals Sensitivity", Range(0, 50)) = 0
        [Toggle(NORMALS_MASK)] _NormalsMask ("Normals Mask", Float) = 0

        // Discontinuity luminance.
        _LuminanceSensitivity ("Luminance Sensitivity", Range(0, 50)) = 0
        [Toggle(LUMINANCE_MASK)] _LuminanceMask ("Luminance Mask", Float) = 0

        // Outline sampling.
        [KeywordEnum(Cross, Sobel, Circular)] _Operator("Edge Detection Operator", Float) = 0
        _OutlineThickness ("Outline Thickness", Float) = 1
        [Toggle(SCALE_WITH_DISTANCE)] _ScaleWithDistance ("Scale Outline Thickness With Distance", Float) = 0
        _DistanceScaleStart ("Distance Scale Start", Float) = 100
        _DistanceScaleDistance ("Distance Scale Distance", Float) = 10
        _DistanceScaleMin ("Distance Scale Min", Float) = 10
        [Toggle(SCALE_WITH_RESOLUTION)] _ResolutionDependent ("Resolution Dependent", Float) = 0
        _ReferenceResolution ("Reference Resolution", Float) = 1080
        
        // Outline distortion.
         [Toggle(DISTORTION)] _Distortion ("Distortion", Float) = 0
        _DistortionTexture ("Distortion Texture", 3D) = "" {}
        _DistortionStrength("Distortion Strength", Range(0,1)) = 1
        _DistortionThicknessInfluence("Distortion Thickness Influence", Range(0,10)) = 0
        _DistortionStepRate("Distortion Step Rate", Integer) = 0
        _DistortionScale("Distortion Scale", Range(0,1)) = 1
        
        // Outline colors.
        _OutlineColor ("Outline Color", Color) = (0, 0, 0, 1)
        [Toggle(OVERRIDE_SHADOW)] _OverrideShadow ("Override Outline Color In Shadow", Float) = 0
        _OutlineColorShadow ("Outline Color Shadow", Color) = (1, 1, 1, 1)
        _BackgroundColor ("Background Color", Color) = (0, 0, 0, 0)
        [Toggle(FILL)] _Fill ("Fill", Float) = 0
        _FillColor ("Fill Color", Color) = (0, 0, 0, 1)
        [Toggle(FADE_BY_DISTANCE)] _FadeByDistance ("Fade Outline by Distance", Float) = 0
        _DistanceFadeStart ("Distance Fade Start", Float) = 100
        _DistanceFadeDistance ("Distance Fade Distance", Float) = 10
        _DistanceFadeColor ("Distance Fade Color", Color) = (0, 0, 0, 0)
         [Toggle(FADE_BY_HEIGHT)] _FadeInDistance ("Fade Outline by height", Float) = 0
        _HeightFadeStart ("Height Fade Start", Float) = 100
        _HeightFadeDistance ("Height Fade Distance", Float) = 10
        _HeightFadeColor ("Height Fade Color", Color) = (0, 0, 0, 0)

        _SrcBlend ("_SrcBlend", Int) = 0
        _DstBlend ("_DstBlend", Int) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType"="Opaque"
        }

        ZWrite Off
        Cull Off

        HLSLINCLUDE
        #pragma multi_compile _ DEPTH
        #pragma multi_compile _ NORMALS
        #pragma multi_compile _ LUMINANCE
        #pragma multi_compile _ SECTIONS

        #pragma multi_compile _ OVERRIDE_SHADOW
        #pragma multi_compile _ SCALE_WITH_DISTANCE
        #pragma multi_compile _ SCALE_WITH_RESOLUTION
        #pragma multi_compile _ FILL
        #pragma multi_compile _ FADE_BY_DISTANCE
        #pragma multi_compile _ FADE_BY_HEIGHT
        #pragma multi_compile _ DEPTH_MASK
        #pragma multi_compile _ NORMALS_MASK
        #pragma multi_compile _ LUMINANCE_MASK
        #pragma multi_compile OPERATOR_CROSS OPERATOR_SOBEL OPERATOR_CIRCULAR
        #pragma multi_compile _ DISTORTION

        #pragma shader_feature_local _ DEBUG_DEPTH DEBUG_NORMALS DEBUG_LUMINANCE DEBUG_SECTIONS
        #pragma shader_feature_local _ DEBUG_SECTIONS_R
        #pragma shader_feature_local _ DEBUG_SECTIONS_G
        #pragma shader_feature_local _ DEBUG_SECTIONS_B
        #pragma shader_feature_local _ DEBUG_SECTIONS_PERCEPTUAL
        ENDHLSL

        Pass // 0: EDGE DETECTION OUTLINE
        {
            Name "EDGE DETECTION OUTLINE"

            Blend [_SrcBlend] [_DstBlend]

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #if defined(DEPTH) || defined(OVERRIDE_SHADOW) || defined(SCALE_WITH_DISTANCE) || defined(FADE_BY_DISTANCE) || defined(FADE_BY_HEIGHT) || defined(DEBUG_DEPTH) || defined(DISTORTION) || defined(OPERATOR_CIRCULAR)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #endif

            #if defined(NORMALS) || defined(DEPTH) || defined(DEBUG_NORMALS)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
            #endif

            #if defined(LUMINANCE) || defined(DEBUG_LUMINANCE)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #endif

            #include "Packages/dev.ameye.linework/Runtime/EdgeDetection/Shaders/DeclareSectioningTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float4 _BackgroundColor, _OutlineColor, _FillColor, _OutlineColorShadow, _DistanceFadeColor, _HeightFadeColor;
            float _OverrideOutlineColorShadow;
            float _OutlineThickness;
            float _ReferenceResolution;
            float _DistanceScaleStart, _DistanceScaleDistance, _DistanceScaleMin;
            float _DistanceFadeStart, _DistanceFadeDistance;
            float _HeightFadeStart, _HeightFadeDistance;
            float _DepthSensitivity, _DepthDistanceModulation, _GrazingAngleMaskPower, _GrazingAngleMaskHardness;
            float _NormalSensitivity;
            float _LuminanceSensitivity;

            // Distortion.
            float _DistortionStrength;
            float _DistortionThicknessInfluence;
            int _DistortionStepRate;
            float _DistortionScale;
            TEXTURE3D(_DistortionTexture);
            SAMPLER(sampler_DistortionTexture);

            #pragma vertex Vert
            #pragma fragment frag
            
            float RobertsCross(float3 samples[4])
            {
                const float3 difference_1 = samples[1] - samples[2];
                const float3 difference_2 = samples[0] - samples[3];
                return sqrt(dot(difference_1, difference_1) + dot(difference_2, difference_2));
            }

            float RobertsCrossSection(float3 samples[4])
            {
                const float3 difference_1 = samples[1].r - samples[2].r;
                const float3 difference_2 = samples[0].r - samples[3].r;
                return sqrt(dot(difference_1, difference_1) + dot(difference_2, difference_2));
            }

            float RobertsCross(float samples[4])
            {
                const float difference_1 = samples[1] - samples[2];
                const float difference_2 = samples[0] - samples[3];
                return sqrt(difference_1 * difference_1 + difference_2 * difference_2);
            }

            float SobelSection(float3 samples[9])
            {
                const float3 difference_1 = samples[0].r - samples[2].r + 2 * samples[3].r - 2 * samples[5].r + samples[6].r - samples[8].r;
                const float3 difference_2 = samples[0].r - samples[6].r + 2 * samples[1].r - 2 * samples[7].r + samples[2].r - samples[8].r;
                return sqrt(dot(difference_1, difference_1) + dot(difference_2, difference_2));
            }
            
            float Sobel(float3 samples[9])
            {
                const float3 difference_1 = samples[0] - samples[2] + 2 * samples[3] - 2 * samples[5] + samples[6] - samples[8];
                const float3 difference_2 = samples[0] - samples[6] + 2 * samples[1] - 2 * samples[7] + samples[2] - samples[8];
                return sqrt(dot(difference_1, difference_1) + dot(difference_2, difference_2));
            }
            
            float Sobel(float samples[9])
            {
                const float difference_1 = samples[0] - samples[2] + 2 * samples[3] - 2 * samples[5] + samples[6] - samples[8];
                const float difference_2 = samples[0] - samples[6] + 2 * samples[1] - 2 * samples[7] + samples[2] - samples[8];
                return sqrt(difference_1 * difference_1 + difference_2 * difference_2);
            }

            #if defined(NORMALS)
            float3 SampleSceneNormalsRemapped(float2 uv)
            {
                return SampleSceneNormals(uv) * 0.5 + 0.5;
            }
            #endif

            #if defined(LUMINANCE) || defined(DEBUG_LUMINANCE)
            float SampleSceneLuminance(float2 uv)
            {
                float3 color = SampleSceneColor(uv);
                return color.r * 0.3 + color.g * 0.59 + color.b * 0.11;
            }
            #endif

            half3 HSVToRGB(half3 In)
            {
                half4 K = half4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
                half3 P = abs(frac(In.xxx + K.xyz) * 6.0 - K.www);
                return In.z * lerp(K.xxx, saturate(P - K.xxx), In.y);
            }

            half4 frag(Varyings IN) : SV_TARGET
            {
                float2 uv = IN.texcoord;

                ///
                /// DISCONTINUITY SOURCES
                ///

                #if defined(DEPTH) || defined(OVERRIDE_SHADOW) || defined(SCALE_WITH_DISTANCE) || defined(FADE_BY_DISTANCE) || defined(FADE_BY_HEIGHT) || defined(DEBUG_DEPTH) || defined(DISTORTION) || defined(OPERATOR_CIRCULAR)
                float center_depth = SampleSceneDepth(uv);
                #if !UNITY_REVERSED_Z // Transform depth from [0, 1] to [-1, 1] on OpenGL.
                center_depth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, center_depth); // Alternatively: depth = 1.0 - depth
                #endif
                float3 positionWS = ComputeWorldSpacePosition(uv, center_depth, UNITY_MATRIX_I_VP); // Calculate world position from depth.
                #endif

                ///
                /// DISTORTION
                /// 
                
                #if defined(DISTORTION)
                float steppedTime = 0;
                if (_DistortionStepRate > 0.0) {
                    float stepIndex = floor(_Time.y * _DistortionStepRate);
                    steppedTime = stepIndex / _DistortionStepRate;
                }
                
                // Distortion strength + scale.
                float distortionStrength = _DistortionStrength * 0.05;
                float distortionScale = _DistortionScale * 2;
                
                // World space distortion.
                float3 distortionUV = positionWS * distortionScale;
                distortionUV = float3(distortionUV.x + steppedTime, distortionUV.y, distortionUV.z);
                
                // Sample 3D noise
                float distortionOffset = SAMPLE_TEXTURE3D(_DistortionTexture, sampler_DistortionTexture, distortionUV).r;
                float distortionNoise = distortionOffset * 2 - 1; 
                _OutlineThickness *= 1 + (distortionNoise * _DistortionThicknessInfluence);
          
                // Distort UVs.
                uv = uv + distortionNoise * distortionStrength;
                #endif

                ///
                /// EDGE DETECTION
                ///
                
                #if defined(DEPTH) || defined(DEBUG_NORMALS)
                float3 center_normal = SampleSceneNormals(uv);
                #endif

                bool mask = false;
                bool fill = false;
                float4 section_center = SampleSceneSection(uv);
                if (section_center.r == 1.0) fill = true;
                if (section_center.r == 0.0) mask = true;

                float edge_depth = 0;
                float edge_normal = 0;
                float edge_luminance = 0;
                float edge_section = 0;

                float2 texel_size = float2(1.0 / _ScreenParams.x, 1.0 / _ScreenParams.y); // Same as _BlitTexture_TexelSize.xy but this is broken atm

                #if defined(SCALE_WITH_RESOLUTION)
                float scaled_outline_thickness = _OutlineThickness * _ScreenParams.y / _ReferenceResolution;
                #else
                float scaled_outline_thickness = _OutlineThickness;
                #endif
                
                // Fade/scale by distance
                #if defined(SCALE_WITH_DISTANCE) || defined(FADE_BY_DISTANCE)
                float worldSpaceDistance = length(positionWS - _WorldSpaceCameraPos);
                #endif
                
                #if defined(SCALE_WITH_DISTANCE)
                float distance_scale = saturate((worldSpaceDistance - _DistanceScaleStart) / _DistanceScaleDistance);
                scaled_outline_thickness *= lerp(1.0, _DistanceScaleMin, distance_scale);
                #endif
                
                #if defined(OPERATOR_CROSS)
                const float half_width_f = floor(scaled_outline_thickness * 0.5);
                const float half_width_c = ceil(scaled_outline_thickness * 0.5);

                // Generate samples.
                float2 uvs[4];
                uvs[0] = uv + texel_size * float2(half_width_f, half_width_c) * float2(-1, 1);  // top left
                uvs[1] = uv + texel_size * float2(half_width_c, half_width_c) * float2(1, 1);   // top right
                uvs[2] = uv + texel_size * float2(half_width_f, half_width_f) * float2(-1, -1); // bottom left
                uvs[3] = uv + texel_size * float2(half_width_c, half_width_f) * float2(1, -1);  // bottom right
        
                float3 normal_samples[4], section_samples[4];
                float depth_samples[4], luminance_samples[4];
                
                for (int i = 0; i < 4; i++) {
                #if defined(DEPTH)
                    depth_samples[i] = SampleSceneDepth(uvs[i]);
                #endif
                #if defined(NORMALS)
                    normal_samples[i] = SampleSceneNormalsRemapped(uvs[i]);
                #endif
                #if defined(LUMINANCE)
                    luminance_samples[i] = SampleSceneLuminance(uvs[i]);
                #endif
                    section_samples[i] = SampleSceneSection(uvs[i]);
                    if(section_samples[i].r == 1) fill = true;
                    if(section_samples[i].r == 0) mask = true;
                }

                #if defined(DEPTH)
                #if defined(DEPTH_MASK)
                edge_depth = mask ? 0 : RobertsCross(depth_samples);
                #else
                edge_depth = RobertsCross(depth_samples);
                #endif
                #endif

                #if defined(NORMALS)
                #if defined(NORMALS_MASK)
                edge_normal = mask ? 0 : RobertsCross(normal_samples);
                #else
                edge_normal = RobertsCross(normal_samples);
                #endif
                #endif

                #if defined(LUMINANCE)
                #if defined(LUMINANCE_MASK)
                edge_luminance = mask ? 0 : RobertsCross(luminance_samples);
                #else
                edge_luminance = RobertsCross(luminance_samples);
                #endif
                #endif

                #if defined(SECTIONS)
                edge_section = mask ? 0 : RobertsCrossSection(section_samples);
                #endif

                #elif defined(OPERATOR_SOBEL)
                float scale = floor(scaled_outline_thickness);

                float2 uvs[9];
                uvs[0] = uv + texel_size * scale * float2(-1, 1);  // top left
                uvs[1] = uv + texel_size * scale * float2(0, 1);   // top center
                uvs[2] = uv + texel_size * scale * float2(1, 1);   // top right
                uvs[3] = uv + texel_size * scale * float2(-1, 0);  // middle left
                uvs[4] = uv + texel_size * scale * float2(0, 0);   // middle center
                uvs[5] = uv + texel_size * scale * float2(1, 0);   // middle right
                uvs[6] = uv + texel_size * scale * float2(-1, -1); // bottom left
                uvs[7] = uv + texel_size * scale * float2(0, -1);  // bottom center
                uvs[8] = uv + texel_size * scale * float2(1, -1);  // bottom right

                float3 normal_samples[9], section_samples[9];
                float depth_samples[9], luminance_samples[9];

                for (int i = 0; i < 9; i++) {
                #if defined(DEPTH)
                    depth_samples[i] = SampleSceneDepth(uvs[i]);
                #endif
                #if defined(NORMALS)
                    normal_samples[i] = SampleSceneNormalsRemapped(uvs[i]);
                #endif
                #if defined(LUMINANCE)
                    luminance_samples[i] = SampleSceneLuminance(uvs[i]);
                #endif
                    section_samples[i] = SampleSceneSection(uvs[i]);
                    if(section_samples[i].r == 1) fill = true;
                    if(section_samples[i].r == 0) mask = true;
                }
                
                #if defined(DEPTH)
                #if defined(DEPTH_MASK)
                edge_depth = mask ? 0 : Sobel(depth_samples);
                #else
                edge_depth = Sobel(depth_samples);
                #endif
                #endif

                #if defined(NORMALS)
                #if defined(NORMALS_MASK)
                edge_normal = mask ? 0 : Sobel(normal_samples);
                #else
                edge_normal = Sobel(normal_samples);
                #endif
                #endif

                #if defined(LUMINANCE)
                #if defined(LUMINANCE_MASK)
                edge_luminance = mask ? 0 : Sobel(luminance_samples);
                #else
                edge_luminance = Sobel(luminance_samples);
                #endif
                #endif

                #if defined(SECTIONS)
                edge_section = mask ? 0 : SobelSection(section_samples);
                #endif

                #elif defined(OPERATOR_CIRCULAR)
                float sum_section = 0;
                float rotation_step = 2 * PI / 20;
                for (int index = 0; index < 20 ; index++) {
                    float rotation = index * rotation_step;
                    float2 offset = float2(cos(rotation), sin(rotation)) * scaled_outline_thickness * texel_size;
                    float2 sample_uv = uv + offset;

                    #if defined(SECTIONS)
                    float section_diff = SampleSceneSection(sample_uv).r - section_center.r;
                    sum_section += section_diff * section_diff;
                    #endif
                }
                edge_section = sqrt(sum_section) > 0.0;
                #endif

                ///
                /// DISCONTINUITIY THRESHOLDING
                ///

                #if defined(DEPTH)
                float depth_threshold = 1 / _DepthSensitivity;

                // 1. The depth buffer is non-linear so two objects 1m apart close to camera will have much larger depth difference than two
                //    objects 1m apart far away from the camera. For this, we multiply the threshold by the depth buffer so that nearby objects
                //    will have to have a larger discontinuity in order to be detected as an 'edge'.
                depth_threshold = max(depth_threshold * 0.01, depth_threshold * _DepthDistanceModulation * SampleSceneDepth(uv));

                // 2. At small grazing angles, the depth difference will grow larger and so faces can be wrongly detected. For this, the depth threshold
                //    can be modulated by the grazing angle, given by the dot product between the normal vector and the view direction. If the normal vector
                //    and the view direction are almost perpendicular, the depth threshold should be increased.
                float3 viewWS = normalize(_WorldSpaceCameraPos.xyz - positionWS);
                float fresnel = pow(1.0 - dot(normalize(center_normal), normalize(viewWS)), 1.0);
                float grazingAngleMask = _GrazingAngleMaskHardness * saturate((fresnel + _GrazingAngleMaskPower - 1) / _GrazingAngleMaskPower); // a mask between 0 and 1
                depth_threshold = depth_threshold * (1 + grazingAngleMask);
                
                edge_depth = edge_depth > depth_threshold ? 1 : 0;
                #endif

                #if defined(NORMALS)
                float normalThreshold = 1 / _NormalSensitivity;
                edge_normal = edge_normal > normalThreshold ? 1 : 0;
                #endif

                #if defined(LUMINANCE)
                float luminanceThreshold = 1 / _LuminanceSensitivity;
                edge_luminance = edge_luminance > luminanceThreshold ? 1 : 0;
                #endif

                #if defined(SECTIONS)
                edge_section = edge_section > 0 ? 1 : 0;
                #endif

                float edge = section_center.g == 1 ? 0 : max(edge_depth, max(edge_normal, max(edge_luminance, edge_section)));
                
                ///
                /// DEBUG VIEWS
                ///

                #if defined(DEBUG_DEPTH)
                return lerp(half4(center_depth, center_depth, center_depth, 1), half4(1,1,1,1), edge_depth);
                #endif

                #if defined(DEBUG_NORMALS)
                return lerp(half4(center_normal * 0.5 + 0.5, 1), half4(0,0,0,1), edge_normal);
                #endif

                #if defined(DEBUG_LUMINANCE)
                half luminance = SampleSceneLuminance(uv);
                return lerp(half4(luminance, luminance, luminance, 1), half4(1,0,0,1), edge_luminance);
                #endif

                #if defined(DEBUG_SECTIONS)
                if(fill) return half4(0,1,0,1);
                if(mask) return half4(0,0,1,1);

                #if !defined(DEBUG_SECTIONS_PERCEPTUAL)
                half3 col = section_center.xyz;
                #if !defined(DEBUG_SECTIONS_R)
                    col.r = 0;
                #endif
                #if !defined(DEBUG_SECTIONS_G)
                    col.g = 0;
                #endif
                #if !defined(DEBUG_SECTIONS_B)
                    col.b = 0;
                #endif
                return section_center.g == 1 ? half4(col, 1.0) : lerp(half4(col, 1.0), half4(1,1,1,1), edge_section);
                #else // perceptual
                half4 section_perceptual = half4(HSVToRGB(half3(section_center.r * 360.0, 0.5, 1.0)), 1.0);
                if(mask) section_perceptual = half4(1.0, 1.0, 1.0, 1.0);
                return lerp(section_perceptual, half4(0,0,0,1), edge_section);
                #endif
                #endif

                ///
                /// COMPOSITE EDGES
                ///

                #if defined(FILL)
                if (fill) return _FillColor;
                #endif
                
                float4 line_color = _OutlineColor;
                
                // Shadows.
                #if defined(OVERRIDE_SHADOW)
                float shadow = 1 - SampleShadowmap(
                    TransformWorldToShadowCoord(positionWS),
                    TEXTURE2D_ARGS(_MainLightShadowmapTexture, sampler_MainLightShadowmapTexture),
                    GetMainLightShadowSamplingData(),
                    GetMainLightShadowStrength(),
                    false);
                line_color = lerp(line_color, _OutlineColorShadow, shadow);
                #endif

                #if defined(FADE_BY_DISTANCE)
                float distance_fade = 1.0 - saturate(1.0 - (worldSpaceDistance - _DistanceFadeStart) / _DistanceFadeDistance);
                line_color = lerp(line_color, _DistanceFadeColor * _DistanceFadeColor.a, distance_fade);
                #endif

                #if defined(FADE_BY_HEIGHT)
                float height = positionWS.y;
                float height_fade = 1.0 - saturate(1.0 - (height - _HeightFadeStart) / _HeightFadeDistance);
                line_color = lerp(line_color, _HeightFadeColor * _HeightFadeColor.a, height_fade);
                #endif

                return lerp(_BackgroundColor, line_color, edge);
            }
            ENDHLSL
        }
    }
}