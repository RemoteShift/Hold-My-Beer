Shader "Hold My Beer/Independent Loading Film"
{
    Properties
    {
        [PerRendererData] _MainTex ("Artwork", 2D) = "white" {}
        _Background ("Background mode", Float) = 0
        _FilmTime ("Unscaled film time", Float) = 0
        _Grain ("Grain", Range(0,0.2)) = 0.075
        _FringeWidth ("Fringe width in pixels", Range(0,3)) = 1.75
        _FringeStrength ("Fringe strength", Range(0,1)) = 0.54
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" }
        Cull Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            float _Background, _FilmTime, _Grain, _FringeWidth, _FringeStrength;
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = input.uv; o.color = input.color; return o;
            }
            float Noise(float2 pixel, float frame) { return frac(sin(dot(pixel,float2(12.9898,78.233)) + frame * 19.19) * 43758.5453); }
            half4 Frag(Varyings input) : SV_Target
            {
                float noise = Noise(floor(input.positionCS.xy), floor(_FilmTime * 24));
                if (_Background > 0.5)
                {
                    // The backdrop remains nearly black; grain is deliberately subtle here.
                    float3 color = (noise * .003) * float3(1,.94,.84);
                    return half4(color,1);
                }
                float2 offset = (ddx(input.uv)*.86 + ddy(input.uv)*.51) * _FringeWidth;
                half4 center = SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,input.uv) * input.color;
                half4 ahead = SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,input.uv+offset) * input.color;
                half4 behind = SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,input.uv-offset) * input.color;
                float3 weights = float3(.2126,.7152,.0722);
                float c = dot(center.rgb,weights)*center.a;
                float a = dot(ahead.rgb,weights)*ahead.a, b = dot(behind.rgb,weights)*behind.a;
                float purple = smoothstep(.008,.16,abs(a-c)), green = smoothstep(.008,.16,abs(b-c));
                float3 fringe = purple*float3(.64,-.31,1.19)*lerp(c,a,.38) + green*float3(-.62,.24,-.61)*lerp(c,b,.38);
                float3 color = center.rgb * float3(1.025,.98,.93) + fringe * _FringeStrength;
                color += (noise-.5)*_Grain*(.3+c);
                return half4(saturate(color),center.a);
            }
            ENDHLSL
        }
    }
}
