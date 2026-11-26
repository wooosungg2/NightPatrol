// 빗줄기·물방울 파티클 (텍스처 없이 모양을 계산). SkylightRain·CeilingDrip이 머티리얼을 만들어 씀.
// Round 0 = 길쭉한 빗줄기 (Stretched Billboard용), 1 = 동그란 물방울·튀는 방울
Shader "NightPatrol/RainParticle"
{
	Properties
	{
		_Color ("Color", Color) = (0.78, 0.84, 0.92, 0.3)
		_Round ("Round (0 빗줄기, 1 방울)", Range(0, 1)) = 0
		_Softness ("Softness", Range(0.01, 1)) = 0.5
		_AlongU ("빗줄기 방향 = U (가로로 뚱뚱하면 0)", Range(0, 1)) = 1
	}

	SubShader
	{
		Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
		Blend SrcAlpha OneMinusSrcAlpha
		ZWrite Off
		Cull Off

		Pass
		{
			Name "RainParticle"
			Tags { "LightMode" = "UniversalForward" }

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_fog
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

			CBUFFER_START(UnityPerMaterial)
				half4 _Color;
				half _Round;
				half _Softness;
				half _AlongU;
			CBUFFER_END

			struct Attributes
			{
				float4 positionOS : POSITION;
				half4 color : COLOR;
				float2 uv : TEXCOORD0;
			};

			struct Varyings
			{
				float4 positionCS : SV_POSITION;
				half4 color : COLOR;
				float2 uv : TEXCOORD0;
				half fog : TEXCOORD1;
			};

			Varyings vert (Attributes v)
			{
				Varyings o;
				o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
				o.color = v.color;
				o.uv = v.uv;
				o.fog = ComputeFogFactor(o.positionCS.z);
				return o;
			}

			half4 frag (Varyings i) : SV_Target
			{
				float2 p = i.uv * 2.0 - 1.0;
				float along = lerp(p.y, p.x, _AlongU);   // 빗줄기 길이 방향
				float across = lerp(p.x, p.y, _AlongU);  // 빗줄기 폭 방향

				half w = saturate(1.0 - abs(across));
				half l = saturate(1.0 - abs(along));
				half streak = w * w * smoothstep(0.0, 0.7, l);
				half drop = smoothstep(0.0, _Softness, saturate(1.0 - length(p)));

				half4 c = _Color * i.color;
				c.a *= lerp(streak, drop, _Round);
				c.rgb = MixFog(c.rgb, i.fog);
				return c;
			}
			ENDHLSL
		}
	}
}
