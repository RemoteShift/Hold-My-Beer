Shader "HoldMyBeer/Results/Celebration"
{
    Properties { _Clock("Unscaled Clock", Float) = 0 }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
            float _Clock;
            CBUFFER_END
            Varyings vert(Attributes v) { Varyings o; o.positionCS=TransformObjectToHClip(v.positionOS.xyz); o.uv=v.uv; return o; }
            half4 frag(Varyings i):SV_Target
            {
                // Large plane: use world-like UV scale so beams fill the camera frustum.
                float2 p=(i.uv-.5)*18;
                float a=atan2(p.y+.04,p.x), r=length(p);
                float wave=sin(a*12+_Clock*.55);
                float edge=max(fwidth(wave),.012);
                float ray=smoothstep(.25-edge,.25+edge,wave);
                float sweep=.5+.5*sin(a*3-_Clock*.68+r*.25);
                float3 burgundy=float3(.14,.012,.028), amber=float3(.42,.10,.018), gold=float3(.82,.43,.09);
                float3 col=lerp(lerp(burgundy,amber,sweep*.6),gold,ray*.85);
                return half4(col,1);
            }
            ENDHLSL
        }
    }
}
