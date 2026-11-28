Shader "Hold My Beer/Pause Gaussian Blur"
{
    Properties { _MainTex("Paused World", 2D) = "white" {} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZTest Always ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex vert_img
            #pragma fragment Frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float2 _Direction;
            half4 Frag(v2f_img input) : SV_Target
            {
                half3 color = tex2D(_MainTex, input.uv).rgb * .2270270270h;
                color += (tex2D(_MainTex, input.uv + _Direction).rgb + tex2D(_MainTex, input.uv - _Direction).rgb) * .1945945946h;
                color += (tex2D(_MainTex, input.uv + _Direction * 2).rgb + tex2D(_MainTex, input.uv - _Direction * 2).rgb) * .1216216216h;
                color += (tex2D(_MainTex, input.uv + _Direction * 3).rgb + tex2D(_MainTex, input.uv - _Direction * 3).rgb) * .0540540541h;
                color += (tex2D(_MainTex, input.uv + _Direction * 4).rgb + tex2D(_MainTex, input.uv - _Direction * 4).rgb) * .0162162162h;
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
