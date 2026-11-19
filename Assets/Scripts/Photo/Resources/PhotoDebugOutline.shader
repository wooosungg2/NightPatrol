Shader "Hidden/NightPatrol/PhotoDebugOutline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Mask", 2D) = "black" {}
        _OutlineColor ("Outline", Color) = (0.2, 1, 0.3, 1)
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct Attributes { float4 vertex : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 localPosition : TEXCOORD1;
                half4 color : COLOR;
            };
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            half4 _OutlineColor;
            float4 _ClipRect;

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.localPosition = v.vertex;
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            half Neighbours(float2 uv, float radius)
            {
                float2 d = abs(_MainTex_TexelSize.xy) * radius;
                half value = tex2D(_MainTex, uv + float2(d.x, 0)).r;
                value = max(value, tex2D(_MainTex, uv - float2(d.x, 0)).r);
                value = max(value, tex2D(_MainTex, uv + float2(0, d.y)).r);
                value = max(value, tex2D(_MainTex, uv - float2(0, d.y)).r);
                value = max(value, tex2D(_MainTex, uv + d).r);
                value = max(value, tex2D(_MainTex, uv - d).r);
                value = max(value, tex2D(_MainTex, uv + float2(d.x, -d.y)).r);
                return max(value, tex2D(_MainTex, uv + float2(-d.x, d.y)).r);
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half center = tex2D(_MainTex, i.uv).r;
                half edge = saturate(Neighbours(i.uv, 1.0) - center);
                half glow = saturate(Neighbours(i.uv, 3.0) - center);
                half4 color = half4(lerp(_OutlineColor.rgb, half3(0.75, 1, 0.8), edge * 0.5),
                    max(edge, glow * 0.35) * _OutlineColor.a) * i.color;
                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(i.localPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif
                return color;
            }
            ENDHLSL
        }
    }
}
