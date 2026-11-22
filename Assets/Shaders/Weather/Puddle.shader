// 물 새는 자리 바닥 웅덩이. 방울이 떨어진 순간(_LastDrop, 초)부터 물결이 퍼진다. CeilingDrip이 값을 넣음.
Shader "NightPatrol/Puddle"
{
	Properties
	{
		_Color ("Color (A = 진하기)", Color) = (0.12, 0.13, 0.14, 0.35)
		_RingColor ("Ripple Color", Color) = (0.85, 0.9, 0.95, 0.5)
		_RippleSpeed ("Ripple Speed (반지름/초)", Float) = 0.9
		_RippleLife ("Ripple Life (초)", Float) = 0.9
		_LastDrop ("Last Drop Time", Float) = -100
	}

	SubShader
	{
		Tags { "RenderType" = "Transparent" "Queue" = "Transparent-5" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
		Blend SrcAlpha OneMinusSrcAlpha
		ZWrite Off
		Cull Off
		Offset -1, -1

		Pass
		{
			Name "Puddle"
			Tags { "LightMode" = "UniversalForward" }

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

			CBUFFER_START(UnityPerMaterial)
				half4 _Color;
				half4 _RingColor;
				float _RippleSpeed;
				float _RippleLife;
				float _LastDrop;
			CBUFFER_END

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
				o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
				o.uv = v.uv;
				return o;
			}

			half4 frag (Varyings i) : SV_Target
			{
				float2 p = i.uv * 2.0 - 1.0;
				float r = length(p);
				float edge = r + (Noise(i.uv * 6.0) - 0.5) * 0.3;          // 울퉁불퉁한 웅덩이 테두리
				float mask = smoothstep(0.95, 0.75, edge);

				float age = _Time.y - _LastDrop;
				float live = saturate(1.0 - age / max(0.01, _RippleLife)) * step(0.0, age);
				float ring = smoothstep(0.05, 0.0, abs(r - age * _RippleSpeed)) * live;
				float ring2 = smoothstep(0.04, 0.0, abs(r - age * _RippleSpeed * 0.6)) * live * 0.6;
				float rings = saturate(ring + ring2) * mask;

				half3 col = lerp(_Color.rgb, _RingColor.rgb, rings);
				half alpha = mask * _Color.a + rings * _RingColor.a;
				return half4(col, saturate(alpha));
			}
			ENDHLSL
		}
	}
}
