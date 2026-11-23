
Shader "NightPatrol/NorthKoreanBroadcastCRT"
{
    Properties
    {
        [MainTexture] _BaseMap ("Broadcast Image", 2D) = "white" {}
        [HDR] _BaseColor ("Screen Color", Color) = (1.5, 1.5, 1.5, 1)
        _ScanlineStrength ("CRT Scanlines", Range(0, 1)) = 0.08
        _NoiseStrength ("Noise Strength", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "BroadcastScreen"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _ScanlineStrength;
                float _NoiseStrength;
            CBUFFER_END

            float RandomNoise(float2 p)
            {
                return frac(
                    sin(dot(p, float2(127.1, 311.7)))
                    * 43758.5453
                );
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS =
                    TransformObjectToHClip(input.positionOS.xyz);

                output.uv =
                    input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float timeStep = floor(_Time.y * 30.0);

                // 1. CRT 가로줄
                float scanline =
                    0.5 + 0.5 * sin(uv.y * 850.0);

                // 2. 수평 화면 밀림
                float band = floor(uv.y * 35.0);
                float offset =
                    (RandomNoise(float2(band, timeStep)) - 0.5)
                    * _NoiseStrength * 0.05;

                float2 distortedUV = uv;
                distortedUV.x += offset;

                // 3. 방송 이미지
                half4 color = SAMPLE_TEXTURE2D(
                    _BaseMap,
                    sampler_BaseMap,
                    distortedUV
                );

                // 4. CRT 가로줄 적용
                color.rgb *=
                    1.0 - scanline * _ScanlineStrength;

                // 5. 순간적인 치지직 노이즈
                float noise = RandomNoise(
                    floor(uv * float2(640, 360)) + timeStep
                );

                color.rgb = lerp(
                    color.rgb,
                    noise.xxx,
                    _NoiseStrength
                );

                // 6. HDR 밝기 적용
                color.rgb *= _BaseColor.rgb;

                return half4(color.rgb, 1.0);
            }
            ENDHLSL
        }
    }
}
