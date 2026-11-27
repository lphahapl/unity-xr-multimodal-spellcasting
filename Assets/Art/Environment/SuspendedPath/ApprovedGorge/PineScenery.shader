Shader "NarutoVR/Gorge Pine Stands"
{
    Properties { [MainTexture] _BaseMap("Pine azimuth atlas", 2D) = "white" {} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        Pass
        {
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            struct A { float4 p:POSITION;float2 uv:TEXCOORD0;half4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 p:SV_POSITION;float2 uv:TEXCOORD0;half fog:TEXCOORD1;half3 tint:TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };
            V Vert(A a)
            {
                V v=(V)0;
                UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(v);
                v.p=TransformObjectToHClip(a.p.xyz);v.uv=a.uv;v.tint=a.color.rgb;
                v.fog=ComputeFogFactor(v.p.z);return v;
            }
            half4 Frag(V v):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(v);
                half4 t=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,v.uv);
                clip(t.a-.32h);
                return half4(MixFog(t.rgb*v.tint,v.fog),1);
            }
            ENDHLSL
        }
    }
}
