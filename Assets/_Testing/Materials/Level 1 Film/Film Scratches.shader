Shader "Hold My Beer/Film Scratches"
{
    Properties
    {
        [PerRendererData] _MainTex ("UI Texture", 2D) = "white" {}
        _Opacity ("Scratch Opacity", Range(0, 0.2)) = 0.085
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float _Opacity;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float Hash(float value)
            {
                return frac(sin(value * 127.1 + 33.3) * 43758.5453);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float time = _Time.y / 1.85;
                float reel = floor(time);
                float onset = lerp(0.14, 0.73, Hash(reel + 9.1));
                float age = (frac(time) - onset) * 1.85;
                float duration = lerp(0.16, 0.25, Hash(reel + 39.0));
                float visible = step(0.39, Hash(reel + 1.7));
                float fade = smoothstep(0.0, 0.035, age)
                    * (1.0 - smoothstep(duration - 0.055, duration, age));

                float position = lerp(0.055, 0.945, Hash(reel + 3.3));
                float lean = (Hash(reel + 74.2) - 0.5) * 0.007;
                float jitter = (Hash(reel + floor(age * 24.0)) - 0.5) * 0.0013;
                float distanceToLine = abs(uv.x - position - uv.y * lean - jitter);
                float pixel = max(fwidth(uv.x), 0.00001);
                float stroke = 1.0 - smoothstep(pixel * 0.2, pixel * 1.25, distanceToLine);

                float start = lerp(0.04, 0.48, Hash(reel + 4.8));
                float end = min(0.97, start + lerp(0.23, 0.56, Hash(reel + 8.2)));
                float segment = smoothstep(start, start + 0.025, uv.y)
                    * (1.0 - smoothstep(end - 0.03, end, uv.y));
                float broken = 0.30 + 0.70 * smoothstep(0.16, 0.44,
                    Hash(floor(uv.y * 36.0) + reel * 13.0));
                return half4(0.84, 0.76, 0.61, _Opacity * visible * fade * stroke * segment * broken);
            }
            ENDHLSL
        }
    }
}
