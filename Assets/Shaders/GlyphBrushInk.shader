Shader "NarutoVR/UI/Glyph Brush Ink"
{
    Properties
    {
        [PerRendererData] _MainTex ("Canvas Texture", 2D) = "white" {}
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct Attributes
            {
                float4 position : POSITION;
                half4 color : COLOR;
                float2 brush : TEXCOORD0;
                float2 profile : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                half4 color : COLOR;
                float2 brush : TEXCOORD0;
                float2 profile : TEXCOORD1;
                float2 paper : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float4 _ClipRect;
            int _UIVertexColorAlwaysGammaSpace;

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.position = UnityObjectToClipPos(input.position);
                output.color = input.color;
                #ifndef UNITY_COLORSPACE_GAMMA
                if (_UIVertexColorAlwaysGammaSpace)
                    output.color.rgb = GammaToLinearSpace(output.color.rgb);
                #endif
                output.brush = input.brush;
                output.profile = input.profile;
                output.paper = input.position.xy;
                return output;
            }

            float Grain(float2 cell)
            {
                cell = frac(cell * float2(.1031, .11369));
                cell += dot(cell, cell.yx + 19.19);
                return frac((cell.x + cell.y) * cell.x);
            }

            float BrushDrift(float position, float seed)
            {
                float cell = floor(position);
                float t = frac(position);
                return lerp(Grain(float2(cell, seed)), Grain(float2(cell + 1.0, seed)),
                    t * t * (3.0 - 2.0 * t));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float wetness = input.color.a;
                float2 paperFootprint = fwidth(input.paper);
                float footprint = max(paperFootprint.x, paperFootprint.y);
                float tooth = lerp(Grain(floor(input.paper * float2(1.45, 2.4))), .5,
                    smoothstep(.30, .85, footprint));
                float across = input.brush.x;
                float along = input.brush.y;

                // Uneven bristle splay follows the stroke, while paper tooth stays fixed on the board.
                float load = BrushDrift(along * .098, 4.3);
                float splay = BrushDrift(along * .39, 8.7);
                float shift = (load - .5) * .24 + (splay - .5) * .12;
                float taper = lerp(.06, 1.0, pow(saturate(input.profile.x), .68));
                float radius = lerp(abs(across / taper - shift),
                    length(float2(across + .19 * along, along * .88)), input.profile.y);
                float dry = saturate((.91 - wetness) * 2.4 + (.5 - load) * .23);
                float spread = lerp(.78, .98, wetness) + (splay - .5) * .19
                    + (tooth - .5) * lerp(.23, .13, wetness);
                float feather = clamp(.57 * fwidth(radius), .022, .25);
                float body = 1.0 - smoothstep(spread - feather, spread + feather, radius);
                float absorbed = 1.0 - smoothstep(spread - feather, spread + .20 + feather, radius);
                float wick = (absorbed - body) * wetness * (.22 + .26 * tooth);

                float lane = across * 3.55 + (splay - .5) * .60 + (load - .5) * .32;
                float footprintAlongLane = fwidth(lane);
                float strand = abs(frac(lane) - .5);
                float gapWidth = lerp(.09, .24, Grain(float2(floor(lane), 6.97)));
                float split = 1.0 - smoothstep(gapWidth * .35, gapWidth + .09, strand);
                split = lerp(split, .20, smoothstep(.69, 1.60, footprintAlongLane));
                float paperShows = saturate(split * lerp(.50, 1.04, tooth) + .23 * tooth);
                float alpha = saturate(body * lerp(.77, .985, wetness) *
                    (1.0 - min(.94, dry * (.18 + 1.03 * paperShows))) + wick);

                float rim = smoothstep(.57, .96, radius) * wetness;
                float pigmentValue = lerp(1.21, .73, wetness) *
                    (1.0 + .24 * (load - .5) + .11 * (tooth - .5) - .23 * rim);
                half3 tint = input.color.rgb * pigmentValue;

                #ifdef UNITY_UI_CLIP_RECT
                alpha *= UnityGet2DClipping(input.paper, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(alpha - .001);
                #endif
                return half4(tint, alpha);
            }
            ENDCG
        }
    }
}
