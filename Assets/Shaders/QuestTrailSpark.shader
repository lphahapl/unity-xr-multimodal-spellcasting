Shader "NarutoVR/Quest Trail Spark"
{
    Properties
    {
        _CoreColor ("Light centre", Color) = (1.0, 0.89, 0.49, 1)
        _GlowColor ("Gold halo", Color) = (1.0, 0.61, 0.13, 1)
        _Radius ("Halo radius (m)", Range(0.02, 0.09)) = 0.058
        _BobHeight ("Vertical amplitude (m)", Range(0, 0.035)) = 0.015
        _BobRate ("Vertical cycles per second", Range(0, 1)) = 0.42
        _PulseRate ("Shimmer cycles per second", Range(0, 1.5)) = 0.85
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Name "GoldDots"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _CoreColor, _GlowColor;
                float _Radius, _BobHeight, _BobRate, _PulseRate;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 corner : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half2 corner : TEXCOORD0;
                half shimmer : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 centre = TransformObjectToWorld(input.positionOS);
                float phase = dot(centre.xz, float2(3.23, 5.41));
                float angle = _Time.y * 6.2831853;
                centre.y += _BobHeight * sin(angle * _BobRate + phase);
                output.shimmer = 0.5h + 0.5h * sin(angle * _PulseRate - phase * 1.31);
                float3 view = TransformWorldToView(centre);
                view.xy += input.corner * _Radius * lerp(0.91, 1.05, output.shimmer);
                output.positionCS = mul(UNITY_MATRIX_P, float4(view, 1.0));
                output.corner = input.corner;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half r2 = dot(input.corner, input.corner);
                half core = 1.0h - smoothstep(0.035h, 0.205h, r2);
                half halo = saturate(1.0h - r2);
                halo *= halo;
                half alpha = saturate(core * 0.78h + halo * 0.52h) * lerp(0.57h, 1.0h, input.shimmer);
                half3 colour = lerp(_GlowColor.rgb, _CoreColor.rgb, core);
                return half4(colour * alpha, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
