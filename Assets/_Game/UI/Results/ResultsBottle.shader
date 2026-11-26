Shader "HoldMyBeer/Results/Bottle"
{
    Properties { _MainTex("Bottle Atlas", 2D) = "white" {} }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float3 normalWS:TEXCOORD1; float3 view:TEXCOORD2; };
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            Varyings vert(Attributes v)
            {
                Varyings o; o.positionCS=TransformObjectToHClip(v.positionOS.xyz); o.uv=v.uv;
                o.normalWS=TransformObjectToWorldNormal(v.normalOS); o.view=GetWorldSpaceViewDir(TransformObjectToWorld(v.positionOS.xyz)); return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                float3 n=normalize(i.normalWS); float facing=saturate(dot(n,normalize(float3(-.5,.7,-1))));
                float rim=pow(1-saturate(dot(n,normalize(i.view))),3);
                float3 base=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv).rgb;
                return half4(base*(.65+facing*.65)+rim*float3(.13,.055,.015),1);
            }
            ENDHLSL
        }
    }
}
