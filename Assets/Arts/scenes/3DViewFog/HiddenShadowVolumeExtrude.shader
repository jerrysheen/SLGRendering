Shader "Hidden/ShadowVolumeExtrudeSimple"
{
    Properties
    {
        _Color ("Shadow Color", Color) = (0,0,0,1)
        _ExtrudeDist ("Extrude Distance", Float) = 50.0
    }
    SubShader
    {
        Tags { "Queue"="Transparent+100" "RenderPipeline"="UniversalPipeline" }

        Cull Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color  : COLOR; // r: 0=base, 1=extrude
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                half4  col : COLOR;
            };

            float3 _PlayerPos;
            float  _ExtrudeDist;
            half4  _Color;

            v2f vert(appdata v)
            {
                v2f o;
                float3 worldPos = TransformObjectToWorld(v.vertex.xyz);

                float3 dir = worldPos - _PlayerPos;
                dir.y = 0;
                dir = normalize(dir + 1e-8);

                float isExtruded = step(0.5, v.color.r);
                float startBias  = (isExtruded > 0) ? 0.0 : 0.05;
                worldPos += dir * (_ExtrudeDist * isExtruded - startBias);

                o.pos = TransformWorldToHClip(worldPos);
                o.col = _Color;
                return o;
            }

            half4 frag(v2f i) : SV_Target { return i.col; }
            ENDHLSL
        }
    }
}
