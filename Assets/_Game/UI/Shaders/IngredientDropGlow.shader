Shader "HoldMyBeer/Ingredient Drop Glow"
{
    Properties
    {
        [HDR] _GlowColor ("Glow Color", Color) = (1.7,1.1,.35,1)
        _Strength ("Glow Strength", Range(0,1)) = .45
        _Phase ("Pulse Phase", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off
        Blend SrcAlpha One
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; };
            float4 _GlowColor;
            float _Strength, _Phase;
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex=UnityObjectToClipPos(v.vertex);
                o.uv=v.uv;
                return o;
            }
            float4 frag(v2f i):SV_Target
            {
                float2 offset=i.uv*2-1;
                float distance=length(offset);
                float falloff=exp2(-3*dot(offset,offset))*(1-smoothstep(.55,1,distance));
                float clock=floor(_Time.y*12)/12;
                float pulse=.78+.22*sin(clock*3+_Phase);
                return float4(_GlowColor.rgb,falloff*_Strength*pulse);
            }
            ENDCG
        }
    }
}
