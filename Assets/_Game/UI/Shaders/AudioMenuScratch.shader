Shader "UI/Audio Scratch Saturation"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _CarveRelief ("Carved Edge Strength", Range(0,1)) = 0
        _CutColor ("Recessed Edge", Color) = (0.025,0.017,0.009,1)
        _RimColor ("Exposed Lower Edge", Color) = (0.58,0.46,0.30,1)
        _WoodTint ("Exposed Wood", Color) = (0.26,0.20,0.13,1)
        _WoodTintStrength ("Wood Tone Blend", Range(0,1)) = 0
        _Saturation ("Saturation", Range(0,1)) = 0.4
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct appdata { float4 vertex : POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; float4 worldPosition : TEXCOORD1; };
            sampler2D _MainTex;
            fixed4 _Color;
            float _Saturation;
            fixed4 _WoodTint;
            float4 _MainTex_TexelSize;
            float _CarveRelief;
            fixed4 _CutColor;
            fixed4 _RimColor;
            float _WoodTintStrength;
            float4 _ClipRect;
            v2f vert(appdata v)
            {
                v2f o;
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 color = tex2D(_MainTex, i.uv);
                color.rgb = lerp(color.rgb, _WoodTint.rgb, _WoodTintStrength);
                float above = tex2D(_MainTex, i.uv + float2(0, _MainTex_TexelSize.y)).a;
                float below = tex2D(_MainTex, i.uv - float2(0, _MainTex_TexelSize.y)).a;
                float upperEdge = saturate(color.a - above);
                float lowerEdge = saturate(color.a - below);
                color.rgb = lerp(color.rgb, _CutColor.rgb, upperEdge * _CarveRelief);
                color.rgb = lerp(color.rgb, _RimColor.rgb, lowerEdge * _CarveRelief);
                float grey = dot(color.rgb, float3(0.299,0.587,0.114));
                color.rgb = lerp(grey.xxx, color.rgb, _Saturation);
                color *= i.color;
                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif
                return color;
            }
            ENDCG
        }
    }
}