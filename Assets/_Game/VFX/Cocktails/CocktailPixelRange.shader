Shader "HoldMyBeer/Pixel Cocktail Range"
{
 Properties { _Tint ("Color", Color) = (1,1,1,.65) }
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
   CBUFFER_START(UnityPerMaterial)
   half4 _Tint;
   CBUFFER_END
   V vert(A input) { V o; o.positionCS=TransformObjectToHClip(input.positionOS.xyz); o.uv=input.uv; return o; }
   half4 frag(V input):SV_Target {
    float2 pixel=(floor(input.uv*64)+.5)/64;
    float distance=length(pixel*2-1);
    clip(.018-abs(distance-.98));
    return _Tint;
   }
   ENDHLSL
  }
 }
}
