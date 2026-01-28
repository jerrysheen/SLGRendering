Shader "Hidden/Outlines/Surface Fill/Fill"
{
    Properties
    {
        // Colors
        _PrimaryColor("Primary Color", Color) = (1, 0, 0, 1)
        _SecondaryColor("Secondary Color", Color) = (0,  1, 0, 1)
        
        // Pattern
        [KeywordEnum(Solid, Stripes, Squares, Checkerboard, Dots, Glow, Texture)] _Pattern("Pattern", int) = 0
        [KeywordEnum(R, G, B, A)] _Channel("Channel", int) = 0
     
        // Pattern properties
        _Rotation("Pattern Rotation",Range(0.0, 360.0))=0.0
        _Offset("Pattern Offset",Range(0.0, 1.0))=0.0
        _Density("Pattern Density",Range(0.0, 1.0))=0.0
        _FrequencyX("Pattern Frequency",Range(0.1, 100.0))=1.0
        _Scale("Pattern Scale",Range(0.1, 100.0))=1.0
        _Direction("Pattern Direction",Range(0.0, 360.0))=0.0
        _Speed("Pattern Speed",Range(0.0, 1.0))=0.0
        _Softness("Glow Softness",Range(0.0, 1.0))=0.0
        _Width("Glow Width",Range(0.0, 1.0))=0.0
        _Power("Glow Power",Range(0.0, 1.0))=0.0
        _Texture("Texture", 2D) = "white"
        
        // Blend
        _SrcBlend ("_SrcBlend", Int) = 0
        _DstBlend ("_DstBlend", Int) = 0
        
        // Stencil
        _StencilComp		("Stencil Comp", Float) = 8
	    _StencilRef			("Stencil Ref", Float) = 0
	    _StencilPass		("Stencil Pass", Float) = 0
        _StencilFail		("Stencil Fail", Float) = 0
	    _StencilReadMask	("Stencil ReadMask", Float) = 255
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType"="Opaque"
        }
        
        ZTest Off
        ZWrite Off
        Blend [_SrcBlend] [_DstBlend]
        
        Stencil
        {
            Ref [_StencilRef]
		    Comp [_StencilComp]
		    Pass [_StencilPass]
		    Fail [_StencilFail]
		    ReadMask [_StencilReadMask]
        }
         
        Pass // 0: SURFACE FILL
        {
            Name "SURFACE FILL"
            
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #ifdef _PATTERN_GLOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl" 
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl" 
            #endif
            
            #pragma vertex Vert
            #pragma fragment frag

            #pragma multi_compile_local _PATTERN_SOLID _PATTERN_STRIPES _PATTERN_SQUARES _PATTERN_DOTS _PATTERN_CHECKERBOARD _PATTERN_TEXTURE _PATTERN_GLOW
            #pragma multi_compile_local _CHANNEL_R _CHANNEL_G _CHANNEL_B _CHANNEL_A
            
            #ifdef _PATTERN_TEXTURE
            TEXTURE2D(_Texture);
            SAMPLER(sampler_Texture);
            #endif

            CBUFFER_START(UnityPerMaterial)
                half4 _PrimaryColor;
                half4 _SecondaryColor;
                float4 _Texture_ST;
                float _Rotation;
                float _Direction;
                float _Speed;
                float _FrequencyX;
                float _Density;
                float _Offset;
                float _Scale;
                float _Softness;
                float _Width;
                float _Power;
            CBUFFER_END

            float2 scroll_uvs(float2 uv, float direction, float speed)
            {
                direction = radians(180 - direction);
                float2 scroll_direction = normalize(float2(cos(direction), sin(direction)));
                float2 scroll_offset = scroll_direction * speed * _Time.y;
                return uv + scroll_offset;
            }

            float2 rotate_uvs_around_center(float2 uv, float rotation)
            {
                float2 center = float2(0.5, 0.5);
                rotation = (180 - rotation ) * (3.1415926f/180.0f);
                uv -= center;
                float s, c;
                sincos(rotation, s, c);
                float3 r = float3(-s, c, s);
                return float2(dot(uv, r.yz), dot(uv, r.xy)) + center;
            }
            
            half4 frag(Varyings IN) : SV_TARGET
            {
                float2 uv = IN.texcoord;

                // Screen space uvs.
                float aspect_ratio = _ScreenParams.x / _ScreenParams.y;
                uv = float2(uv.x * aspect_ratio, uv.y);

                // Move + rotate uvs.
                float2 scrolling_uvs = scroll_uvs(uv, _Direction,  _Speed);
                float2 rotated_uvs = rotate_uvs_around_center(scrolling_uvs, _Rotation);
               
                // Pattern.
                float pattern = 0;
                #ifdef _PATTERN_SOLID
                pattern = 1;
                #elif _PATTERN_STRIPES
                float2 stripes_uv = rotate_uvs_around_center(uv, _Rotation);
                stripes_uv += _Speed * _Time.y;
                stripes_uv = stripes_uv * float2(_FrequencyX, 1);
                stripes_uv = frac(stripes_uv);
                float2 d = abs(stripes_uv * 2 - 1) - float2(_Density, 1);
                d = 1 - d / fwidth(d);
                pattern = saturate(min(d.x, d.y));
                #elif _PATTERN_SQUARES
                float2 squares_uv = rotated_uvs * _FrequencyX;
                float2 distance = abs(frac(squares_uv) * 2 - 1) - float2(_Density, _Density);
                distance = 1 - distance / fwidth(distance);
                pattern = saturate(min(distance.x, distance.y));
                #elif _PATTERN_DOTS
                float2 dots_uv = rotated_uvs * _FrequencyX;
                float row = floor(dots_uv.y);
                float rowOffset = fmod(row, 2.0);
                float uv_x = dots_uv.x + rowOffset * _Offset;
                float uv_y = dots_uv.y;
                float2 grid = frac(float2(uv_x, uv_y));
                float2 distance = length((grid * 2 - 1) / float2(_Density, _Density));
                pattern = saturate((1 - distance) / fwidth(distance));
                #elif _PATTERN_CHECKERBOARD
                rotated_uvs = (rotated_uvs.xy + 0.5) * _FrequencyX;
                float4 derivatives = float4(ddx(rotated_uvs), ddy(rotated_uvs));
                float2 duv_length = sqrt(float2(dot(derivatives.xz, derivatives.xz), dot(derivatives.yw, derivatives.yw)));
                float width = 1.0;
                float2 distance3 = 4.0 * abs(frac(rotated_uvs + 0.25) - 0.5) - width;
                float2 scale = 0.35 / duv_length.xy;
                float frequency_limiter = sqrt(clamp(1.1 - max(duv_length.x, duv_length.y), 0.0, 1.0));
                float2 vector_alpha = clamp(distance3 * scale.xy, -1.0, 1.0);
                pattern = saturate(0.5 + 0.5 * vector_alpha.x * vector_alpha.y * frequency_limiter);
                #elif _PATTERN_GLOW
                float3 positionWS = ComputeWorldSpacePosition(IN.texcoord, SampleSceneDepth(IN.texcoord), UNITY_MATRIX_I_VP);
                float3 normal = SampleSceneNormals(IN.texcoord);
                float3 view = normalize(_WorldSpaceCameraPos - positionWS);
                float ndotv = pow(1.0 - saturate(dot(normal, view)), _Power);
                pattern = lerp(1, smoothstep(1.0 - _Width, 1.0 - _Width + _Softness, ndotv), step(0, 1.0 - _Width));
                #elif _PATTERN_TEXTURE
                float2 texture_uvs = rotated_uvs * _Scale;
                float4 texture_sample = SAMPLE_TEXTURE2D(_Texture, sampler_Texture, texture_uvs * _Scale);
                #if _CHANNEL_R
                pattern = texture_sample.r;
                #elif _CHANNEL_G
                pattern = texture_sample.g;
                #elif _CHANNEL_B
                pattern = texture_sample.b;
                #elif _CHANNEL_A
                pattern = texture_sample.a;
                #endif
                #endif
                
                // Color
                half4 color = _PrimaryColor * pattern + _SecondaryColor * (1.0 - pattern);
                
                return color;
            }
            ENDHLSL
        }
    }
}
