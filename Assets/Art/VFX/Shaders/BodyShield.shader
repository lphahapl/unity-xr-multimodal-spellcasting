Shader "NarutoVR/BodyShield"
{
    Properties { _Pulse ("护盾波纹", Range(0,1)) = 0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+10" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half _Pulse;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half3 normalWS : TEXCOORD1; float2 uv : TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv;
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half rim = 1 - saturate(abs(dot(normalize(i.normalWS), GetWorldSpaceNormalizeViewDir(i.positionWS))));
                float lat = i.uv.y;
                // Leave an open viewing band at eye height. Graduated dashes wrap the lower shell.
                float band = min(abs(lat - .48), min(abs(lat - .43), abs(lat - .79)));
                float aa = max(.001, fwidth(lat));
                half bandMask = 1 - smoothstep(.001, .0025 + aa, band);
                float tickPosition = abs(frac(i.uv.x * 32 + .5) - .5);
                half tick = (1 - smoothstep(.045, .10 + fwidth(i.uv.x) * 16, tickPosition))
                    * (1 - smoothstep(.006, .014, abs(lat - .46)));
                half sweep = (1 - smoothstep(.01, .065, abs(lat - lerp(.18, .86, _Pulse)))) * _Pulse;
                half ink = max(bandMask, tick);
                half alpha = .025 + rim * rim * .24 + ink * .44 + sweep * .35 + _Pulse * .045;
                return half4(lerp(half3(.46,.78,.90), half3(1,.85,.53), max(ink,sweep)), alpha);
            }
            ENDHLSL
        }
    }
}
