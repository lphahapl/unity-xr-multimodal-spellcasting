Shader "NarutoVR/PlainWardShell"
{
    Properties
    {
        _RimTex ("原盾边缘", 2D) = "white" {}
        _BodyColor ("盾身", Color) = (.045,.42,.56,1)
        _EdgeColor ("边光", Color) = (.12,.87,.95,1)
        _Pulse ("施法与受击反馈", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+10" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_RimTex); SAMPLER(sampler_RimTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _BodyColor, _EdgeColor;
                half _Pulse;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 normalAndInside : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalAndInside.xyz = TransformObjectToWorldNormal(v.normalOS);
                float3 eye = TransformWorldToObject(GetCameraPositionWS());
                o.normalAndInside.w = dot(eye,eye) < dot(v.positionOS.xyz,v.positionOS.xyz) ? 1 : 0;
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                bool inside = i.normalAndInside.w > .5;
                half3 n = normalize(i.normalAndInside.xyz);
                half facing = dot(n,GetWorldSpaceNormalizeViewDir(i.positionWS));
                if ((facing > 0) == inside) discard;
                half drift = .5h + .5h * sin(_Time.y * 2.1 + n.y * 3.2 + n.x * 1.7);
                if (inside)
                {
                    // Keep the player's forward view clear. Motion reads mostly below eye level.
                    half3 ray = -GetWorldSpaceNormalizeViewDir(i.positionWS);
                    half low = smoothstep(.04h,.72h,-ray.y);
                    // A single travelling edge follows the shell below the horizon.
                    half edgeHeight = lerp(.12h,.78h,_Pulse);
                    half sweep = (1 - smoothstep(.025h,.15h,abs(-ray.y - edgeHeight))) * _Pulse;
                    half sheen = .22h + .18h * low + .60h * sweep;
                    half alpha = .016h + low * (.09h + drift * .03h) + sweep * .34h;
                    return half4(lerp(_BodyColor.rgb,_EdgeColor.rgb,sheen),alpha);
                }
                half nv = saturate(facing);
                half2 xy = TransformWorldToViewDir(n).xy;
                half radial = sqrt(saturate(1 - nv * nv));
                float2 uv = .5 + .43 * radial * xy * rsqrt(max(dot(xy,xy), .0001h));
                half rim = saturate(SAMPLE_TEXTURE2D(_RimTex,sampler_RimTex,uv).a * 1.55h);
                half feather = smoothstep(0,.32h,nv);
                half sheen = saturate(rim * (.84h + .16h * drift) + _Pulse * .20h);
                half alpha = (.048h + .035h * drift + rim * .86h + _Pulse * .11h) * feather;
                return half4(lerp(_BodyColor.rgb,_EdgeColor.rgb,sheen),saturate(alpha));
            }
            ENDHLSL
        }
    }
}
