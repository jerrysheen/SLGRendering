Shader "Custom/URP_FogHighlight_Overlay"
{
    Properties
    {
        [HDR] _BaseColor("Highlight Color", Color) = (0, 1, 1, 1)
        _Opacity("Overall Opacity", Range(0, 1)) = 0.5
        
    }

    SubShader
    {
        Tags 
        { 
            "RenderType"="Transparent" 
            "Queue"="Transparent" 
            "RenderPipeline"="UniversalPipeline" 
        }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float _Opacity;
            CBUFFER_END
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                
                // 计算流动的 UV
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 核心计算：颜色 * 强度 * 贴图掩码
                half3 finalColor = _BaseColor.rgb;
                half finalAlpha = _BaseColor.a * _Opacity;

                return half4(finalColor, finalAlpha);
            }
            ENDHLSL
        }
    }
}