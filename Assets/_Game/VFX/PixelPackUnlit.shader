Shader "HoldMyBeer/PixelPackUnlit"
{
 Properties {
  [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
  _BeerRecolor ("Amber Beer Palette", Range(0,1)) = 0
 }
 SubShader {
  Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "CanUseSpriteAtlas"="True" }
  Cull Off ZWrite Off
  Blend SrcAlpha OneMinusSrcAlpha
  Pass {
   Tags { "LightMode"="UniversalForward" }
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
   struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
   TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
   CBUFFER_START(UnityPerMaterial)
   float _BeerRecolor;
   CBUFFER_END
   Varyings vert(Attributes input) {
    Varyings output;
    output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
    output.uv=input.uv; output.color=input.color; return output;
   }
   half4 frag(Varyings input):SV_Target {
    half4 pixel=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,input.uv);
    half brightness=max(max(pixel.r,pixel.g),pixel.b);
    half3 amber=lerp(half3(0.55,0.21,0.025),half3(1.0,0.94,0.68),saturate((brightness-0.2)/0.8));
    pixel.rgb=lerp(pixel.rgb,amber,_BeerRecolor);
    return pixel*input.color;
   }
   ENDHLSL
  }
 }
}