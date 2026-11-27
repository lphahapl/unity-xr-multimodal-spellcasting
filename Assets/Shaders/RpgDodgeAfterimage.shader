Shader "NarutoVR/Combat/Dodge Ink Imprint"
{
    Properties
    {
        _Opacity ("墨量", Range(0,1)) = 1
        [HideInInspector] _InkShape ("轮廓、晕染密度、半径、退墨", Vector) = (1,0,0,0)
        [HideInInspector] _GroundY ("起步地面", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            ZTest LEqual
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _InkShape;
                float _GroundY;
                float _Opacity;
            CBUFFER_END
            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 corner : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half2 disc : TEXCOORD0;
                half3 pigment : TEXCOORD1;
                half density : TEXCOORD2;
                float aboveGround : TEXCOORD3;
                half isWash : TEXCOORD4;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            float Hash(float2 p)
            {
                float3 q = frac(float3(p.xyx) * float3(.1031, .1030, .0973));
                q += dot(q, q.yzx + 33.33);
                return frac((q.x + q.y) * q.z);
            }
            float Wash(float2 p)
            {
                float2 cell = floor(p), t = frac(p);
                t = t * t * (3.0 - 2.0 * t);
                return lerp(lerp(Hash(cell), Hash(cell + float2(1,0)), t.x),
                            lerp(Hash(cell + float2(0,1)), Hash(cell + 1.0), t.x), t.y);
            }
            Varyings Vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                bool wash = v.corner.z > 0;
                float strength = wash ? _InkShape.y * v.corner.z : _InkShape.x;
                if (strength <= .00015)
                {
                    // All vertices of inactive primitives coincide. No raster or
                    // fragment work for the sharp surface during the long wash.
                    o.positionCS = float4(0,0,0,1);
                    return o;
                }
                float3 world = TransformObjectToWorld(v.positionOS);
                float nearFade = smoothstep(.12, .34, distance(world, GetCameraPositionWS()));
                float2 paper = float2(v.positionOS.x + .57 * v.positionOS.z, v.positionOS.y);
                half pool = Wash(paper * 4.5);
                o.isWash = wash ? 1 : 0;
                o.pigment = lerp(half3(.008,.012,.013), half3(.046,.056,.055), pool);
                if (wash)
                {
                    // Fixed 3D centres retain depth and binocular parallax. Only
                    // each radial footprint faces the current eye, including ortho views.
                    half wetness = Wash(paper * float2(2.35,2.0) + float2(1.76,-.63) + .6 * pool);
                    half load = smoothstep(.24h,.76h, wetness);
                    // Dilute patches wick slightly further along the paper, dense
                    // pools keep their pigment. All neighbours share this broad
                    // field in the frozen body frame so the wash stays continuous.
                    float seep = _InkShape.z * (.55 + .45 * _InkShape.w);
                    float3 wick = float3(.10 * (wetness - .45), .07 * (pool - .40), -.08 * (pool - .5));
                    world += mul((float3x3)GetObjectToWorldMatrix(), wick * seep);
                    float2 offset = v.corner.xy * (_InkShape.z * lerp(.98h, .88h, load));
                    world += mul((float3x3)UNITY_MATRIX_I_V, float3(offset, 0));
                    o.disc = v.corner.xy;
                    strength *= lerp(.75h, 1.37h, load);
                    half3 washInk = lerp(half3(.075,.083,.074), half3(.009,.021,.023), load);
                    o.pigment = lerp(washInk, half3(.12,.135,.13), .14h + .34h * (1.0h - _Opacity));
                }
                o.density = strength * nearFade;
                o.aboveGround = world.y - _GroundY;
                o.positionCS = TransformWorldToHClip(world);
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half alpha = i.density;
                if (i.isWash > .5h)
                {
                    half radius2 = dot(i.disc, i.disc);
                    // Gaussian density has measurable coverage outside the original
                    // mesh. Smoothly truncate only its very faint outermost tail.
                    alpha *= exp2(-5.2h * radius2) * saturate((1.0h - radius2) * 6.0h);
                    alpha *= smoothstep(-.004, .036, i.aboveGround);
                }
                return half4(i.pigment, alpha);
            }
            ENDHLSL
        }
    }
}
