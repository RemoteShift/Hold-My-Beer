Shader "HoldMyBeer/Cocktail Pack"
{
 Properties {
  _MainTex ("Pack sheet", 2D) = "white" {}
  _Recolor ("Recolor", Range(0,1)) = 0
  _LowColor ("Shadow color", Color) = (.3,.1,.01,1)
  _HighColor ("Highlight color", Color) = (1,.8,.3,1)
  _ColaPalette ("Cola palette", Range(0,1)) = 0
  _FoamColor ("Foam color", Color) = (1,.9,.75,1)
  _Opacity ("Opacity", Range(0,1)) = 1
  _Frame ("UV scale and offset", Vector) = (1,1,0,0)
 }
 SubShader {
  Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
  Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
  Pass {
   Tags { "LightMode"="UniversalForward" }
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   struct A { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
   struct V { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
   TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
   CBUFFER_START(UnityPerMaterial)
   float4 _Frame, _LowColor, _HighColor, _FoamColor;
   float _Recolor, _Opacity, _ColaPalette;
   CBUFFER_END
   V vert(A input) { V o; o.positionCS=TransformObjectToHClip(input.positionOS.xyz);
    o.uv=input.uv*_Frame.xy+_Frame.zw; return o; }
   half4 frag(V input):SV_Target {
    half4 pixel=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,input.uv);
    half brightness=max(max(pixel.r,pixel.g),pixel.b);
    half3 ramp=lerp(_LowColor.rgb,_HighColor.rgb,sqrt(saturate(brightness)));
    half lightness=dot(pixel.rgb,half3(.2126,.7152,.0722));
    half3 cola=lerp(_LowColor.rgb,_HighColor.rgb,sqrt(saturate(lightness)));
    cola=lerp(cola,_FoamColor.rgb,smoothstep(.78,.98,lightness));
    pixel.rgb=lerp(pixel.rgb,lerp(ramp,cola,_ColaPalette),_Recolor);
    pixel.a*=_Opacity;
    return pixel;
   }
   ENDHLSL
  }
 }
}
