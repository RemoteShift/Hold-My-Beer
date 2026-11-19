Shader "Hold My Beer/Level 1 Color Fringe"
{
    Properties
    {
        _Width ("Fringe Width (pixels)", Range(0, 3)) = 1.25
        _Strength ("Fringe Strength", Range(0, 1)) = 0.38
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Purple and Green Fringe"
            Cull Off ZWrite Off ZTest Always
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Width;
                float _Strength;
            CBUFFER_END

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float2 offset = min(abs(_BlitTexture_TexelSize.xy), rcp(_ScaledScreenParams.xy))
                    * _Width * float2(0.86, 0.51);

                float4 center = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0);
                float3 ahead = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv + offset, 0).rgb;
                float3 behind = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv - offset, 0).rgb;

                const float3 luminance = float3(0.2126, 0.7152, 0.0722);
                float centerLuma = dot(center.rgb, luminance);
                float aheadLuma = dot(ahead, luminance);
                float behindLuma = dot(behind, luminance);
                float purple = smoothstep(0.008, 0.16, abs(aheadLuma - centerLuma));
                float green = smoothstep(0.008, 0.16, abs(behindLuma - centerLuma));
                float3 purpleTint = float3(0.64, -0.31, 1.19);
                float3 greenTint = float3(-0.62, 0.24, -0.61);

                // Thin tints on opposing sides of contrast edges, with stationary source UVs.
                float3 fringe = purple * purpleTint * lerp(centerLuma, aheadLuma, 0.38)
                    + green * greenTint * lerp(centerLuma, behindLuma, 0.38);
                return float4(max(0.0, center.rgb + fringe * _Strength), center.a);
            }
            ENDHLSL
        }
    }
}
