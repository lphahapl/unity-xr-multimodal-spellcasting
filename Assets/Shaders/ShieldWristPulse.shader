Shader "NarutoVR/护盾瞬态度纹"
{
    Properties
    {
        [HDR] _Jade("青玉度纹", Color) = (0.19, 1.35, 0.89, 1)
        [HDR] _Bronze("铜金刻度", Color) = (1.45, 0.76, 0.26, 1)
        [HideInInspector] _Pulse("进度、力度、类型、近眼可见度", Vector) = (1, 0, 0, 0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend One One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Jade, _Bronze;
                float4 _Pulse;
            CBUFFER_END
            struct Input { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Output { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            Output Vert(Input v)
            {
                Output o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv * 2 - 1;
                return o;
            }
            float Stroke(float delta, float width, float aa)
            {
                return 1 - smoothstep(width, width + aa, abs(delta));
            }
            // Circular angular window. Its softened ends taper the engraved strokes.
            float Arc(float turn, float center, float span)
            {
                float d = abs(frac(turn - center + .5) - .5);
                return 1 - smoothstep(span * .76, span, d);
            }
            half4 Frag(Output input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 p = input.uv;
                float r = length(p);
                float aa = clamp(fwidth(r), .002, .035);
                float a = atan2(p.y, p.x) / TWO_PI;
                float t = saturate(_Pulse.x);
                bool cast = _Pulse.z < .5;
                bool broken = _Pulse.z > 1.5;
                float travel = 1 - (1-t)*(1-t)*(1-t);
                float visibility = cast ? smoothstep(0, .14, t) * (1-smoothstep(.48,1,t)) : 1-smoothstep(.06,1,t);
                float radius = cast ? lerp(.87,.59,travel) : lerp(.47, broken ? .92 : .79,travel);
                float turn = a + (cast ? .15*(1-travel) : -.035*travel);
                float reach = cast ? lerp(.14,1, smoothstep(0,.53,t)) : 1;
                float arcs = Arc(turn,.066,.158*reach)+Arc(turn,.406,.095*reach)+Arc(turn,.736,.11*reach);
                // A narrower jade core crosses the softer main band. The secondary
                // wave travels behind it on impact, then separates on shield break.
                float taper = saturate(arcs);
                float core = Stroke(r-radius,.003+.009*taper,aa)*arcs;
                float main = Stroke(r-radius,.007+.026*taper,aa*1.7)*arcs;
                float inner = radius - (cast ? .09 : lerp(.028, broken ? .17 : .095,travel));
                float echo = Stroke(r-inner,.012,aa) * (Arc(turn,.11,.095*reach)+Arc(turn,.60,.17*reach));
                float row = frac((turn+(r-radius)*.055)*18+.5)-.5;
                float mark = Stroke(row,.045,min(.12,aa*2));
                float spoke = mark*Stroke(r-(radius+.064),.018,aa)*arcs;
                float drift = .46+.48*travel;
                float chips = Arc(turn+.085*(r-drift),.61,.018) + Arc(turn-.12*(r-drift),.15,.012) + Arc(turn,.39,.009);
                float split = broken ? chips*Stroke(r-drift,.008+.016*(1-t),aa) : 0;
                float coil = Stroke(r-inner-.025*sin(turn*14.+travel*3.),.004,aa)*Arc(turn,.80,.09)*sin(PI*t);
                half3 jade = _Jade.rgb * (main*.39 + echo*.76 + core*.66 + coil*.58);
                half3 metal = _Bronze.rgb * (spoke*.95 + split + (broken ? core*.44 : core*.12));
                half3 outColor = min(jade+metal, half3(1.7,1.85,1.6)) * visibility * _Pulse.y * _Pulse.w;
                return half4(outColor, 0);
            }
            ENDHLSL
        }
    }
}
