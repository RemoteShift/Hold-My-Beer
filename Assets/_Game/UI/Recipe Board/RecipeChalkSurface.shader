Shader "Hold My Beer/Recipe Chalk Surface"
{
    Properties
    {
        [PerRendererData] _MainTex("Recipe Design", 2D) = "white" {}
        _ChalkTex("Main Menu Chalk Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Cull Off ZWrite Off ZTest [unity_GUIZTestMode]
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_ChalkTex); SAMPLER(sampler_ChalkTex);
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv; output.color = input.color;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half4 recipe = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half3 background = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, float2(0, 0)).rgb;
                half3 difference = abs(recipe.rgb - background);
                half ink = step(.00001h, max(difference.r, max(difference.g, difference.b)));
                half3 chalk = SAMPLE_TEXTURE2D(_ChalkTex, sampler_ChalkTex, input.uv).rgb;
                return half4(lerp(chalk, recipe.rgb, ink) * input.color.rgb, recipe.a * input.color.a);
            }
            ENDHLSL
        }
    }
}
