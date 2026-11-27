Shader "NarutoVR/VFX/ImpactFlame"
{
    Properties
    {
        [MainTexture] _MainTex ("Flame atlas (2 x 2)", 2D) = "white" {}
        _Turbulence ("Flame turbulence", 2D) = "grey" {}
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Cull Off
            ZWrite Off
            Blend One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_Turbulence); SAMPLER(sampler_Turbulence);

            Varyings Vert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.color = input.color;
                o.uv = input.uv;
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                // The original atlas reaches its tile borders. Taper each lobe before that seam.
                float2 local = frac(i.uv * 2.0) * 2.0 - 1.0;
                half flame = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a;
                half detail = SAMPLE_TEXTURE2D(_Turbulence, sampler_Turbulence,
                    i.uv * 3.0 + float2(.075, -.11) * _Time.y).r;
                half rim = saturate(1.0 - dot(local, local));
                half heat = saturate(flame * (.48 + .87 * detail) + rim * .15);
                half alpha = smoothstep(.1, .58, flame + .1 * detail)
                    * smoothstep(0.0, .46, rim) * i.color.a;
                half3 ember = half3(.28, .034, .003);
                half3 orange = half3(1.12, .23, .023);
                half3 yellow = half3(1.75, .90, .27);
                half3 color = lerp(ember, orange, smoothstep(.10, .65, heat));
                color = lerp(color, yellow, smoothstep(.50, .96, heat));
                return half4(color * i.color.rgb * alpha, alpha * .85);
            }
            ENDHLSL
        }
    }
}
