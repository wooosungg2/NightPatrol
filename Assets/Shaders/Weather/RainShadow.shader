// 유리지붕 아래 바닥에 일렁이는 빗물 그림자 (곱하기 블렌드). SkylightRain이 바닥에 깐 사각형에 씀.
Shader "NightPatrol/RainShadow"
{
	Properties
	{
		_Strength ("Strength", Range(0, 0.5)) = 0.18
		_Scale ("Pattern Scale (1m당)", Float) = 2.2
		_Speed ("Flow Speed", Float) = 0.5
		_EdgeFade ("Edge Fade", Range(0.01, 0.5)) = 0.18
	}

	SubShader
	{
		Tags { "RenderType" = "Transparent" "Queue" = "Transparent-10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
		Blend DstColor Zero
		ZWrite Off
		Cull Off
		Offset -1, -1

		Pass
		{
			Name "RainShadow"
			Tags { "LightMode" = "UniversalForward" }

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

			CBUFFER_START(UnityPerMaterial)
				half _Strength;
				float _Scale;
				float _Speed;
				half _EdgeFade;
			CBUFFER_END

			struct Attributes
			{
				float4 positionOS : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct Varyings
			{
				float4 positionCS : SV_POSITION;
				float3 positionWS : TEXCOORD0;
				float2 uv : TEXCOORD1;
			};

			float Hash21 (float2 p)
			{
				p = frac(p * float2(127.1, 311.7));
				p += dot(p, p + 19.19);
				return frac(p.x * p.y);
			}

			float Noise (float2 p)
			{
				float2 i = floor(p);
				float2 f = frac(p);
				f = f * f * (3.0 - 2.0 * f);
				float a = Hash21(i), b = Hash21(i + float2(1, 0)), c = Hash21(i + float2(0, 1)), d = Hash21(i + float2(1, 1));
				return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
			}

			Varyings vert (Attributes v)
			{
				Varyings o;
				VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
				o.positionCS = p.positionCS;
				o.positionWS = p.positionWS;
				o.uv = v.uv;
				return o;
			}

			half4 frag (Varyings i) : SV_Target
			{
				float t = _Time.y * _Speed;
				float2 p = i.positionWS.xz * _Scale;
				// 두 겹이 서로 다른 방향으로 흘러서 물결처럼 일렁임
				float n = Noise(p + float2(0.0, -t)) * 0.6 + Noise(p * 2.3 + float2(t * 0.7, 3.1)) * 0.4;
				float blobs = smoothstep(0.55, 0.8, n);
				float bright = smoothstep(0.75, 0.95, n) * 0.5;            // 방울 가운데는 살짝 밝게 (빛이 모임)

				float2 e = min(i.uv, 1.0 - i.uv);
				float edge = smoothstep(0.0, _EdgeFade, e.x) * smoothstep(0.0, _EdgeFade, e.y);

				half v = 1.0 - _Strength * blobs + _Strength * bright;
				v = lerp(1.0, v, edge);
				return half4(v, v, v, 1.0);
			}
			ENDHLSL
		}
	}
}
