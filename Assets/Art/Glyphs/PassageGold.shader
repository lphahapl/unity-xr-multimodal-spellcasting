Shader "NarutoVR/Glyphs/Passage Gold"
{
    Properties
    {
        _BaseColor ("Pale gold film", Color) = (1,.85,.57,1)
        _EdgeColor ("Warm gold light", Color) = (1,.94,.76,1)
        _Opacity ("Film opacity", Range(0,1)) = .26
        _Shimmer ("Shimmer strength", Range(0,1)) = .75
        _FlowSpeed ("Light travel (metres per second)", Range(0,2)) = .84
        [Toggle(_PASSAGE_CUE)] _Cue ("Selection ribbon", Float) = 0
        [Toggle(_PASSAGE_SIGIL)] _Sigil ("Palm and contact seal", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _CullMode ("Cull", Float) = 0
        [HideInInspector] _LitAt ("Ignition time", Float) = 0
        [HideInInspector] _TargetMark ("Contact seal", Float) = 0
        [HideInInspector] _RayRibbon ("Finger ray", Float) = 0
        [HideInInspector] _HasTarget ("Legal marker", Float) = 0
        [HideInInspector] _Length ("Visible length (m)", Float) = 1
        [HideInInspector] _Growing ("Leading edge", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            // Screen blending transmits background detail. Overlapping ribbons compose in either order.
            Blend One OneMinusSrcColor, Zero One
            ZWrite Off
            ZTest LEqual
            Cull [_CullMode]
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma shader_feature_local _ _PASSAGE_CUE _PASSAGE_SIGIL
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor, _EdgeColor;
                float _Opacity, _Shimmer, _FlowSpeed, _Length, _Growing, _Cue;
                float _Sigil, _CullMode, _LitAt, _TargetMark, _RayRibbon, _HasTarget;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 deck : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                #if defined(_PASSAGE_CUE) || defined(_PASSAGE_SIGIL)
                    o.deck = v.uv;
                #else
                    o.deck = float2(v.positionOS.x, (v.positionOS.y + .5) * _Length);
                #endif
                return o;
            }
            float Stroke(float distanceToLine, float width)
            {
                float pixel = max(fwidth(distanceToLine), .0001);
                return (1 - smoothstep(width - pixel, width + pixel, distanceToLine)) * saturate(width / pixel);
            }
            float Segment(float2 p, float2 a, float2 b)
            {
                float2 span = b - a;
                return length(p - a - span * saturate(dot(p-a,span) / dot(span,span)));
            }
            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                #if defined(_PASSAGE_SIGIL)
                float2 p = i.deck * 2 - 1;
                float r = length(p);
                float age = max(0, _Time.y - _LitAt);
                float settle = smoothstep(0, .32, age);
                float pulse = .5 + .5 * cos(_Time.y * _FlowSpeed * TWO_PI);
                float2 a = abs(p);
                float corner = min(a.x,a.y);
                float rim = Stroke(abs(r - lerp(.86,.70,settle)),.025) * smoothstep(.055,.13,corner);
                float ticks = Stroke(corner,.026) * smoothstep(.47,.53,r) * (1 - smoothstep(.75,.79,r));
                // The two-stroke passage glyph: an open-bottom frame with a short bar inside.
                float door = min(Segment(float2(a.x,p.y),float2(.27,-.31),float2(.27,.29)),
                                 Segment(p,float2(-.27,.29),float2(.27,.29)));
                door = min(door,Segment(p,float2(-.16,-.04),float2(.16,-.04)));
                float contact = abs(a.x+a.y - .37);
                float rune = Stroke(lerp(door,contact,_TargetMark),.040);
                float echo = (1 - settle) * Stroke(abs(r - lerp(.29,.89,settle)),.055);
                float halo = .14 * exp2(-8 * abs(r-.64)) + .22 * exp2(-10 * lerp(door,contact,_TargetMark));
                float light = saturate(rune + ticks*.85 + rim*(.40+.57*_Shimmer*pulse) + echo);
                half3 ink = _BaseColor.rgb * halo + lerp(_BaseColor.rgb,_EdgeColor.rgb,light) * light;
                return half4(saturate(ink) * _Opacity * (1 - smoothstep(.90,.99,r)),1);
                #elif defined(_PASSAGE_CUE)
                if (_RayRibbon > .5)
                {
                    float s = i.deck.x * _Length;
                    float side = abs(i.deck.y * 2 - 1);
                    float phase = abs(frac((s - _Time.y * _FlowSpeed * 2)/.72 + .5)-.5);
                    float knot = phase/.16 + side;
                    float packet = (1 - smoothstep(.50,1,knot)) * _Shimmer;
                    float core = 1 - smoothstep(.08,.24,side);
                    float envelope = 1 - smoothstep(.22,1,side);
                    float power = (.055*envelope + .46*core + .96*packet) * lerp(.70,1,_HasTarget);
                    return half4(lerp(_BaseColor.rgb,_EdgeColor.rgb,packet) * saturate(power),1);
                }
                float core = 1 - smoothstep(.14, .50, abs(i.deck.y - .5));
                float travel = .5 + .5 * cos(TWO_PI * (i.deck.x - _Time.y * _FlowSpeed));
                float pulse = travel * travel * _Shimmer;
                half3 cue = lerp(_BaseColor.rgb, _EdgeColor.rgb, pulse) *
                    saturate(_Opacity + .32 * pulse) * (.40 + .60 * core);
                return half4(cue, 1);
                #else
                float x = abs(i.deck.x);
                float z = i.deck.y;
                // Distances start at the first bank, so the inlay stays put while the front advances.
                float rail = max(Stroke(abs(x - .473), .008), .55 * Stroke(abs(x - .436), .004));
                float tick = Stroke(abs(frac(z * 2 + .5) - .5), .018) * smoothstep(.33, .37, x);
                float cell = abs(frac(z / 1.05) - .5);
                float diamond = Stroke(abs(x / .22 + cell / .34 - 1), .032);
                float phase = (z - _Time.y * _FlowSpeed) / 2.15 + .19 * x;
                float wave = .5 + .5 * cos(TWO_PI * phase);
                float glint = wave * wave * wave * wave * _Shimmer;
                float border = max(rail, tick);
                float front = (1 - smoothstep(.015, .13, _Length - z)) * _Growing;
                float glow = saturate(border * (.40 + .40 * glint) +
                    diamond * (.07 + .50 * glint) + glint * .15 + front * .72);
                half3 light = lerp(_BaseColor.rgb * _Opacity, _EdgeColor.rgb, glow);
                return half4(saturate(light), 1);
                #endif
            }
            ENDHLSL
        }
    }
}
