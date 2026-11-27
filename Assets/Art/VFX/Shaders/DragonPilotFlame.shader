Shader "NarutoVR/DragonPilotFlame"
{
    Properties
    {
        [NoScaleOffset] _Turbulence ("Rising density", 2D) = "gray" {}
        [NoScaleOffset] _HeatRamp ("Dragon breath heat ramp", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Back
            ZWrite Off
            Blend One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_Turbulence);
            SAMPLER(sampler_Turbulence);
            TEXTURE2D(_HeatRamp);
            SAMPLER(sampler_HeatRamp);

            struct Attributes
            {
                float3 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 surface : TEXCOORD0;
                float3 eyeOS : TEXCOORD1;
                float phase : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.surface = v.positionOS;
                o.positionCS = TransformObjectToHClip(v.positionOS);
                o.eyeOS = TransformWorldToObject(GetCameraPositionWS());
                o.phase = 37.0 * length(mul((float3x3)unity_ObjectToWorld, float3(0,1,0)));
                return o;
            }

            // Two small gas volumes share the same temperature ramp as the sustained jet.
            // Density moves upwards through a steady ignition point at the mouth.
            float2 SampleFlame(float3 p, float time, float phase)
            {
                float h = saturate(p.y + .5);
                float2 flow = float2(.75*p.x + .44*p.z + phase, .38*p.y - .21*time);
                float coarse = SAMPLE_TEXTURE2D_LOD(_Turbulence, sampler_Turbulence, flow, 0).r;
                float fine = SAMPLE_TEXTURE2D_LOD(_Turbulence, sampler_Turbulence,
                    float2(p.z - .75*p.x, p.y*.76 - .32*time + phase) + .14*coarse, 0).r;
                float noise = sqrt(saturate(.66*coarse + .34*fine));
                float2 axis = h*h * float2(.07 + .065*sin(6*h - 3*time + phase),
                                            .025 + .039*sin(8*h - 3.7*time));
                float distance = length(p.xz - axis);
                float width = (.14 + .27 * (1-h)) * sin(3.14159 * pow(h, .71));
                float boundary = width - distance + (noise-.46) * (.07 + .22*h);
                float density = smoothstep(-.032, .086, boundary) * smoothstep(.015,.13,h);
                density *= saturate((1.0-h)*2.2 + (noise-.51)*1.4);
                float core = saturate(1.0 - distance / max(width, .018));
                float heat = saturate(.20 + .58*core + .45*(noise-.34) + .14*(1-h));
                return float2(density, heat);
            }

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                // Integrate across the actual chord of the 0.5-radius sphere, independently for each eye.
                float3 front = normalize(i.surface) * .5;
                float3 ray = normalize(front - i.eyeOS);
                float chord = max(0, -2 * dot(ray,front));
                const int samples = 6;
                float stepSize = chord / samples;
                float opacity = 0, weightedHeat = 0;
                [unroll]
                for (int j = 0; j < samples; ++j)
                {
                    float3 p = front + ray * ((j+.5)*stepSize);
                    float2 flame = SampleFlame(p, _Time.y, i.phase);
                    float absorption = 1 - exp2(-7 * flame.x * stepSize);
                    float weight = (1-opacity) * absorption;
                    opacity += weight;
                    weightedHeat += weight * flame.y;
                }
                float heat = weightedHeat / max(opacity,.001);
                float rampU = lerp(.80,.065,heat);
                half3 radiance = SAMPLE_TEXTURE2D(_HeatRamp, sampler_HeatRamp,float2(rampU,.5)).rgb;
                radiance *= 1.15 + 1.3*heat*heat;
                return half4(radiance * opacity, opacity);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
