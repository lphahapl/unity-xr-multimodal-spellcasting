Shader "NarutoVR/Seal Glyph"
{
    Properties
    {
        [PerRendererData] _MainTex ("Glyph", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _HandDepthBias ("Hand overlap (meters)", Range(0,.12)) = .08
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Cull Off
        ZWrite Off
        ZTest LEqual
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex GlyphVert
            #pragma fragment SpriteFrag
            #pragma multi_compile_instancing
            #include "UnitySprites.cginc"
            float _HandDepthBias;

            v2f GlyphVert(appdata_t input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                v2f output = SpriteVert(input);
                float3 view = UnityObjectToViewPos(UnityFlipSprite(input.vertex.xyz, _Flip).xyz);
                // Keep the per-eye silhouette on the knuckles. Only the depth test moves forward.
                view.z = min(view.z + _HandDepthBias, -_ProjectionParams.y - .001);
                float4 biased = mul(UNITY_MATRIX_P, float4(view, 1));
                output.vertex.z = biased.z / biased.w * output.vertex.w;
                return output;
            }
            ENDCG
        }
    }
}
