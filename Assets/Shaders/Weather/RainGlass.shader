// 빗물이 흐르는 유리 (5층 유리지붕). SkylightRain이 유리 판마다 머티리얼을 만들어 방향을 넣는다.
// 정적 배칭이 돼도 깨지지 않게 월드 좌표 + 판 방향(_Origin·_Down·_Across)으로 무늬를 계산한다.
// 물방울 자리는 뒤 화면(_CameraOpaqueTexture)을 굴절시켜 보여 준다 → URP 에셋의 Opaque Texture가 켜져 있어야 함 (PC_RPAsset은 켜져 있음).
Shader "NightPatrol/RainGlass"
{
	Properties
	{
		_Tint ("Glass Tint (A = 유리 진하기)", Color) = (0.78, 0.85, 0.88, 0.14)
		_DropDensity ("Drop Density (1m당 칸)", Float) = 5
		_DropSize ("Drop Size", Range(0.05, 0.6)) = 0.3
		_Speed ("Flow Speed", Float) = 0.6
		_Static ("Static Drops", Range(0, 1)) = 0.7
		_Refraction ("Refraction", Range(0, 0.1)) = 0.03
		_Highlight ("Highlight", Range(0, 2)) = 0.7
		_HighlightColor ("Highlight Color", Color) = (0.92, 0.95, 1, 1)
		[HideInInspector] _Origin ("Origin (월드)", Vector) = (0, 0, 0, 0)
		[HideInInspector] _Down ("Down Slope (월드)", Vector) = (0, 0, -1, 0)
		[HideInInspector] _Across ("Across (월드)", Vector) = (1, 0, 0, 0)
	}

	SubShader
	{
		Tags { "RenderType" = "Transparent" "Queue" = "Transparent+50" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
		Blend SrcAlpha OneMinusSrcAlpha
		ZWrite Off
		Cull Back

		Pass
		{
			Name "RainGlass"
			Tags { "LightMode" = "UniversalForward" }

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_fog
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

			CBUFFER_START(UnityPerMaterial)
				half4 _Tint;
				float _DropDensity;
				half _DropSize;
				float _Speed;
				half _Static;
				half _Refraction;
				half _Highlight;
				half4 _HighlightColor;
				float4 _Origin;
				float4 _Down;
				float4 _Across;
			CBUFFER_END

			struct Attributes
			{
				float4 positionOS : POSITION;
			};

			struct Varyings
			{
				float4 positionCS : SV_POSITION;
				float3 positionWS : TEXCOORD0;
				float4 screenPos : TEXCOORD1;
				half fog : TEXCOORD2;
			};

			float Hash21 (float2 p)
			{
				p = frac(p * float2(233.34, 851.73));
				p += dot(p, p + 23.45);
				return frac(p.x * p.y);
			}

			// 흐르는 방울 한 겹. uv.x = 가로, uv.y = 아래로 갈수록 커짐. 반환: xy = 굴절 방향, z = 방울 세기
			float3 FlowLayer (float2 uv, float t, float size)
			{
				const float2 cell = float2(1.0, 2.5);                 // 세로로 긴 칸 (방울 하나가 이 안을 내려감)
				float2 g = uv / cell;
				g.y += Hash21(float2(floor(g.x), 7.1)) * 3.0;          // 열마다 시작 높이를 어긋나게
				float2 id = floor(g);
				float2 f = frac(g) - 0.5;
				float rnd = Hash21(id);
				float alive = step(0.3, rnd);                            // 칸 30%는 비움

				float speed = lerp(0.6, 1.4, Hash21(id + 3.3));
				float phase = frac(t * speed * 0.25 + rnd);
				phase += 0.05 * sin(phase * 25.0 + rnd * 6.28);           // 멈칫거리며 내려감
				float y = lerp(-0.42, 0.42, saturate(phase));
				float x = (rnd - 0.5) * 0.5 + 0.04 * sin(g.y * 9.0 + rnd * 20.0);

				float r = size * lerp(0.7, 1.2, Hash21(id + 9.1)) * 0.35;
				float2 d = (f - float2(x, y)) * cell;
				float drop = smoothstep(r, r * 0.55, length(d * float2(1.0, 0.85)));

				// 지나온 자리(위쪽)에 남는 작은 방울 줄
				float passed = step(f.y, y);
				float fade = saturate(1.0 - (y - f.y) * 1.6);
				float2 td = float2((f.x - x) * cell.x, (frac(f.y * 10.0) - 0.5) * cell.y / 10.0);
				float trail = smoothstep(r * 0.35, r * 0.18, length(td)) * passed * fade;

				float2 offs = d * drop + td * trail;
				return float3(offs, saturate(drop + trail)) * alive;
			}

			// 맺혀 있다가 사라지는 작은 방울
			float3 StaticLayer (float2 uv, float t)
			{
				float2 id = floor(uv);
				float2 f = frac(uv) - 0.5;
				float rnd = Hash21(id);
				float2 p = (float2(Hash21(id + 1.7), Hash21(id + 4.1)) - 0.5) * 0.7;
				float2 d = f - p;
				float life = frac(t * 0.08 + rnd);
				float fade = smoothstep(0.0, 0.1, life) * smoothstep(1.0, 0.6, life);
				float r = 0.18 * lerp(0.5, 1.0, Hash21(id + 2.2));
				float m = smoothstep(r, r * 0.5, length(d)) * fade * step(0.45, rnd);
				return float3(d * m, m);
			}

			Varyings vert (Attributes v)
			{
				Varyings o;
				VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
				o.positionCS = p.positionCS;
				o.positionWS = p.positionWS;
				o.screenPos = ComputeScreenPos(p.positionCS);
				o.fog = ComputeFogFactor(p.positionCS.z);
				return o;
			}

			half4 frag (Varyings i) : SV_Target
			{
				float3 rel = i.positionWS - _Origin.xyz;
				float2 m = float2(dot(rel, _Across.xyz), dot(rel, _Down.xyz));   // 미터 단위 (가로, 아래)
				float t = _Time.y * _Speed;

				float3 a = FlowLayer(m * _DropDensity, t, _DropSize);
				float3 b = FlowLayer(m * _DropDensity * 1.7 + 13.1, t * 1.15, _DropSize * 0.8);
				float3 s = StaticLayer(m * _DropDensity * 3.0, t) * _Static;

				float2 offs = a.xy + b.xy * 0.7 + s.xy * 0.5;
				float mask = saturate(a.z + b.z * 0.7 + s.z);

				float2 suv = i.screenPos.xy / i.screenPos.w;
				half3 behind = SampleSceneColor(suv + offs * _Refraction * 10.0);

				// 방울 아래쪽 가장자리가 빛을 받은 것처럼 밝게
				half hl = mask * _Highlight * saturate(0.35 + offs.y * 25.0);
				half3 dropCol = behind * (1.0 - 0.25 * mask) + _HighlightColor.rgb * hl;

				half3 col = lerp(_Tint.rgb, dropCol, mask);
				half alpha = saturate(max(_Tint.a, mask));
				col = MixFog(col, i.fog);
				return half4(col, alpha);
			}
			ENDHLSL
		}
	}
}
