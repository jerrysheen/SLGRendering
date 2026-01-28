Shader "Hidden/Outlines/Soft Outline/Outline"
{
    Properties
    {
        [Toggle(HARD)] _Hard ("Hard", Float) = 0

        _OutlineColor ("Outline Color", Color) = (1, 1, 1, 1)
        _OutlineHardness ("Outline Hardness", Range (0, 1)) = 0.5

        _SrcBlend ("_SrcBlend", Int) = 0
        _DstBlend ("_DstBlend", Int) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
        }

        ZWrite Off
        Blend [_SrcBlend] [_DstBlend]

        Pass // 0: OUTLINE
        {
            Name "OUTLINE"

            Stencil
            {
                Ref 0
                Comp Equal
                Pass Keep
                Fail Zero
            }

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #pragma vertex Vert
            #pragma fragment frag

            #pragma multi_compile_local _ HARD_OUTLINE

            half4 _OutlineColor;
            half _OutlineHardness;
            half _OutlineIntensity;
            half _OutlineGap;

            half4 frag(Varyings IN) : SV_TARGET
            {
                #if defined(HARD_OUTLINE)
                half value = SAMPLE_TEXTURE2D(_BlitTexture, sampler_PointClamp, IN.texcoord).r;
               
                float outline = step(0.05, value) - step(1.05 - _OutlineGap, value);
                float blendedAlpha = lerp(value, outline, _OutlineHardness);
                return float4(_OutlineColor.rgb * blendedAlpha, saturate(blendedAlpha)) * _OutlineIntensity;
                #else
                half4 color = SAMPLE_TEXTURE2D(_BlitTexture, sampler_PointClamp, IN.texcoord).rgba;
                return color.rgba * _OutlineIntensity;
                #endif
            }
            ENDHLSL
        }
    }
}