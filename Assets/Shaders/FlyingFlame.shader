Shader "NarutoVR/Feiyan/Rolling Fireball"
{
    Properties
    {
        [NoScaleOffset] _FireAtlas ("Dragon breath flame atlas (2x2)", 2D) = "white" {}
        [NoScaleOffset] _HeatRamp ("Dragon breath temperature ramp", 2D) = "white" {}
        [NoScaleOffset] _FlowNoise ("Turbulence", 2D) = "gray" {}
        [HideInInspector] _FlameShape ("Age, radius, tail length", Vector) = (.62,.45,.92,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "BreathCloud"
            Tags { "LightMode"="UniversalForwardOnly" }
            Blend One OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_FireAtlas); SAMPLER(sampler_FireAtlas);
            TEXTURE2D(_HeatRamp); SAMPLER(sampler_HeatRamp);
            TEXTURE2D(_FlowNoise); SAMPLER(sampler_FlowNoise);
            CBUFFER_START(UnityPerMaterial)
                float4 _FlameShape;
            CBUFFER_END
            struct Attributes
            {
                float3 corner : POSITION;
                float4 patch : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 uvCell : TEXCOORD0;
                float2 rollingField : TEXCOORD2;
                half4 flame : TEXCOORD1; // layer, fuel, density, variation
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float layer = v.patch.x, phase = v.patch.y, bearing = v.patch.z, variation = v.patch.w;
                float age = _FlameShape.x, r = _FlameShape.y, tail = _FlameShape.z;
                float speed = layer > 1.5 ? 1.35 + .52 * variation : .92 + .39 * variation;
                float u = frac(phase + age * speed);
                float angle = 6.28318*bearing + u*(.68+.43*variation);
                float2 side = float2(cos(angle),sin(angle));
                float3 center;
                float size, lengthening = 0, fuel, density;
                // Fade each wisp at birth and burnout; the envelope itself never pulses.
                float life = smoothstep(0,.14,u) * (1-smoothstep(.72,1,u));
                float trailOn = smoothstep(.06, .45, tail);
                float trailingWidth = (1 - u) * (1 - u);
                if (layer < .5) // Cooler gas follows the head and opens into the wake.
                {
                    center = float3(side * r * (.27 + .42 * u), -(.32 * r + tail * (.28 + .68 * u)));
                    center.y += r * (.11 + .64 * u * u);
                    size = r * (.27 + .29 * u);
                    lengthening = tail * (.08 + .08 * u);
                    fuel = .07;
                    density = .16 * life * trailOn;
                }
                else if (layer < 1.5) // Compact and hot along the forward cap.
                {
                    float radial = sqrt(saturate(1 - (1 - 2 * u) * (1 - 2 * u))) * (.40 + .29 * variation);
                    center = float3(side * radial * r, (1.04 - 1.70 * u) * r);
                    size = r * (.75 + .20 * variation + .10 * u);
                    lengthening = r * (.12 + .17 * (1 - u));
                    fuel = .96 - .36 * u + .07 * variation;
                    density = (.92 + .10 * (1 - u)) * life;
                }
                else if (layer < 2.5) // Connected tapered wake shows the flight axis.
                {
                    center = float3(side * r * (.04 + .36 * trailingWidth), -(.18 * r + tail * .91 * u));
                    center.y += r * (.04 + .13 * u * u);
                    size = r * (.07 + .51 * trailingWidth);
                    lengthening = tail * (.10 + .13 * (1 - u));
                    fuel = .88 - .58 * u;
                    density = (1.05 - .47 * u) * life * trailOn;
                }
                else if (layer < 3.5) // Uneven flame tongues peel backward off the cap.
                {
                    center = float3(side * r * (.26 + .44 * (1 - u)), -(.32 * r + tail * (.16 + .76 * u)));
                    center.y += r * (.12 + .47 * u * u) * (.45 + variation);
                    size = r * (.10 + .26 * (1 - u));
                    lengthening = tail * (.08 + .10 * variation);
                    fuel = .72 - .46 * u;
                    density = .73 * life * trailOn;
                }
                else // Slender embers remain behind the body and follow its axis.
                {
                    center = float3(side * r * (.30 + .72 * u), -(.26 * r + tail * (.30 + .77 * u)));
                    center.y += r * .60 * u * u;
                    size = r * (.013 + .015 * variation);
                    lengthening = size * (2 + 2.9 * variation);
                    fuel = .95 - .56 * u;
                    density = .87 * life * trailOn;
                }

                float3 centerWS = TransformObjectToWorld(center);
                float3 right = UNITY_MATRIX_I_V._m00_m10_m20;
                float3 up = UNITY_MATRIX_I_V._m01_m11_m21;
                float3 forward = TransformObjectToWorldDir(float3(0,0,1));
                float2 axis = float2(dot(forward,right),dot(forward,up));
                float projected = length(axis);
                float2 tangent = projected > .001 ? axis/projected : float2(1,0);
                float2 crossAxis = float2(-tangent.y,tangent.x);
                float scale = length(TransformObjectToWorldDir(float3(1,0,0),false));
                float2 card = tangent * (v.corner.x*(size + lengthening*projected))
                            + crossAxis * (v.corner.y*size);
                float3 world = centerWS + (card.x*right + card.y*up)*scale;
                o.positionCS = TransformWorldToHClip(world);
                // A broad convection roll spans adjoining wisps rather than flashing independently on every card.
                float3 gas = TransformWorldToObject(world) / max(r,.032);
                o.rollingField = float2(dot(gas,float3(.55,.28,.48)), dot(gas,float3(-.24,.64,.31)));

                float spin = 0;
                if (layer < 1.5) spin = bearing * 6.28318 + (variation - .44) * u * 1.9;
                else if (layer < 3.5) spin = (variation - .5) * .86;
                float s,c; sincos(spin,s,c);
                float2 rotated = float2(c*v.corner.x-s*v.corner.y, s*v.corner.x+c*v.corner.y);
                o.uvCell.xy = .5+.51*rotated;
                float frame = floor(variation*3.999);
                o.uvCell.zw = float2(fmod(frame,2),floor(frame*.5));
                o.flame = half4(layer,fuel,density,variation);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 uv = i.uvCell.xy;
                half layer = i.flame.x, fuel = i.flame.y, density = i.flame.z;
                if (layer > 3.5h)
                {
                    half glow = saturate(1 - length(2*uv-1));
                    half a = density*glow*glow;
                    return half4(half3(2.2, .63+.40*fuel, .12)*a, a);
                }
                float time = _FlameShape.x;
                float2 offset = float2(i.flame.w*2.74, -i.flame.w);
                float2 flow = float2(.31,-.19)*time;
                half large = SAMPLE_TEXTURE2D(_FlowNoise,sampler_FlowNoise, uv*.83+offset+flow).r;
                half fine = SAMPLE_TEXTURE2D(_FlowNoise,sampler_FlowNoise, uv*2.95+offset-flow*.71).r;
                float2 distorted = uv + .14*float2(large-.19, fine-.19);
                float2 atlasUV = (clamp(distorted,.005,.995)+i.uvCell.zw)*.5;
                half flame = SAMPLE_TEXTURE2D(_FireAtlas,sampler_FireAtlas,atlasUV).a;
                float2 edge = saturate(min(uv,1-uv)*12);
                half feather = 1-smoothstep(.72,1,length(uv-.5)*2);
                half mask = flame * edge.x*edge.y * feather;
                half filaments = saturate(.46 + 1.8*large + .95*fine);
                half a = saturate(mask * filaments * density * 1.45);
                half convection = SAMPLE_TEXTURE2D(_FlowNoise,sampler_FlowNoise, i.rollingField*.64+float2(-.24,.31)*time).r;
                half roll = .5h+.5h*sin(i.rollingField.y*6.8 + i.rollingField.x*2.2 + convection*8.3 - 5.1*time);
                half heat = saturate(.19 + .40*fuel + .27*mask + .24*convection + .16*roll);
                half3 color = SAMPLE_TEXTURE2D(_HeatRamp,sampler_HeatRamp,float2(lerp(.85,.055,heat),.5)).rgb;
                if (layer < .5h)
                    color = lerp(half3(.038,.026,.021),half3(.20,.084,.031),mask);
                else
                {
                    color *= half3(1.12,.72,.40) * (.82 + .77*heat*heat);
                    half headLight = layer < 1.5h ? smoothstep(.74h, .98h, heat) * mask : 0;
                    color = lerp(color, half3(1.52, 1.13, .62), headLight * .68h);
                }
                return half4(color*a, a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
