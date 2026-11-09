Shader "NarutoVR/PlayerHitVignette"
{
    Properties
    {
        _Cinnabar ("朱砂边色", Color) = (0.72, 0.20, 0.10, 1)
        _Strength ("受击强度", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Overlay" "DisableBatching"="True" }
        Pass
        {
            Name "HitEdge"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ColorMask RGB
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Cinnabar;
                float _Strength;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half2 eyePosition : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                // The frame mesh uses per-eye clip coordinates. The stereo macros route
                // it to each view even when the eyes have asymmetric projection matrices.
                output.positionCS = float4(input.positionOS.xy, 0.5, 1);
                output.eyePosition = input.positionOS.xy;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half2 p = abs(input.eyePosition);
                // Give the side edges more weight and soften the turn into each corner.
                p.y *= 0.94h;
                half minor = min(p.x, p.y);
                half edge = max(p.x, p.y) + 0.19h * minor * minor;
                half wash = smoothstep(0.62h, 1.07h, edge);
                half rim = smoothstep(0.87h, 1.11h, edge);
                half alpha = (0.47h * wash + 0.16h * rim) * saturate(_Strength) * _Cinnabar.a;
                return half4(_Cinnabar.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
