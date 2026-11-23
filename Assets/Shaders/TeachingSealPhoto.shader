Shader "NarutoVR/Teaching Seal Photo"
{
    Properties
    {
        [PerRendererData] _MainTex ("Hand photo", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _StencilComp ("Stencil comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil operation", Float) = 0
        _StencilWriteMask ("Stencil write mask", Float) = 255
        _StencilReadMask ("Stencil read mask", Float) = 255
        _ColorMask ("Color mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use alpha clip", Float) = 0
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
        Cull Off Lighting Off ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize, _ClipRect;
            half4 _Color;

            struct Input
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 localPosition : TEXCOORD1;
                half4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Input v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.position = UnityObjectToClipPos(v.vertex);
                o.localPosition = v.vertex.xy;
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }
            half4 SamplePhoto(float2 uv)
            {
                half4 c = tex2D(_MainTex, uv);
                c.rgb *= c.a;
                return c;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 dx = ddx(i.uv), dy = ddy(i.uv);
                float2 dxTexels = dx * _MainTex_TexelSize.zw;
                float2 dyTexels = dy * _MainTex_TexelSize.zw;
                half4 photo;
                if (max(dot(dxTexels, dxTexels), dot(dyTexels, dyTexels)) <= 1)
                    photo = SamplePhoto(i.uv);
                else
                {
                    // Four subpixel locations sample a tilted or distant hand without smearing its alpha edge.
                    float2 a = dx * .375 + dy * .125;
                    float2 b = dy * .375 - dx * .125;
                    photo = .25h * (SamplePhoto(i.uv-a) + SamplePhoto(i.uv+a) +
                                     SamplePhoto(i.uv-b) + SamplePhoto(i.uv+b));
                }
                half opacity = i.color.a;
                #ifdef UNITY_UI_CLIP_RECT
                    opacity *= UnityGet2DClipping(i.localPosition, _ClipRect);
                #endif
                photo.rgb *= i.color.rgb * opacity;
                photo.a *= opacity;
                #ifdef UNITY_UI_ALPHACLIP
                    clip(photo.a - .001h);
                #endif
                return photo;
            }
            ENDCG
        }
    }
}