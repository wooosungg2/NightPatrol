Shader "Hidden/NightPatrol/PhotoDebugMask"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Cull Off
            ZTest Always
            ZWrite Off
            Blend Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            float4x4 _PhotoDebugVP;

            float4 Vert(float4 vertex : POSITION) : SV_POSITION
            {
                return mul(_PhotoDebugVP, mul(unity_ObjectToWorld, vertex));
            }

            half4 Frag() : SV_Target { return half4(1, 1, 1, 1); }
            ENDHLSL
        }
    }
}
