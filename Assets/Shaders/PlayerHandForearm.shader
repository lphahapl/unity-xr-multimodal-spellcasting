Shader "NarutoVR/Player Hand Forearm"
{
    Properties
    {
        [MainTexture] _BaseMap("Skin", 2D) = "white" {}
        [MainColor] _BaseColor("Skin Tint", Color) = (0.79,0.57,0.43,1)
        [Toggle(_COPPER_DETAIL)] _CopperDetail("Copper highlight from Skin alpha", Float) = 0
        [Toggle(_FOREARM_FADE)] _ForearmFade("Fade End Slot", Float) = 0
        _FadeStart("Start (UV0 V)", Range(0,1)) = 0
        _Feather("Feather (UV0 V)", Range(0.001,1)) = 1
        _Tiles("Dither cells per UV unit", Range(8,120)) = 60
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Back
        ZWrite On
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            float _ForearmFade, _FadeStart, _Feather, _Tiles, _CopperDetail;
        CBUFFER_END
        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            half3 normalWS : TEXCOORD1;
            #if defined(_COPPER_DETAIL)
                half3 halfDirWS : TEXCOORD2;
            #endif
            UNITY_VERTEX_OUTPUT_STEREO
        };
        Varyings Vert(Attributes input)
        {
            UNITY_SETUP_INSTANCE_ID(input);
            Varyings output = (Varyings)0;
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            #if defined(_COPPER_DETAIL)
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.halfDirWS = SafeNormalize(GetWorldSpaceNormalizeViewDir(positionWS) + GetMainLight().direction);
            #endif
            output.uv = input.uv;
            return output;
        }
        void ClipEnd(float2 uv)
        {
            #if defined(_FOREARM_FADE)
                // Four-by-four ordered coverage fixed to the skin in both eyes.
                float2 p = floor(uv * _Tiles);
                float2 low = frac(p * 0.5) * 2;
                float2 high = frac(floor(p * 0.5) * 0.5) * 2;
                float rank = 4 * (2 * abs(low.x-low.y) + low.y) + 2 * abs(high.x-high.y) + high.y;
                float coverage = 1 - smoothstep(_FadeStart, _FadeStart + max(.001, _Feather), uv.y);
                clip(coverage - (rank + 0.5) / 16);
            #endif
        }
        half4 Shade(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            ClipEnd(input.uv);
            half3 normal = normalize(input.normalWS);
            Light sun = GetMainLight();
            half4 surface = SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,TRANSFORM_TEX(input.uv,_BaseMap));
            half diffuse = saturate(dot(normal,sun.direction));
            half3 color = surface.rgb * _BaseColor.rgb * (SampleSH(normal) + sun.color * diffuse);
            #if defined(_COPPER_DETAIL)
                half sheen = pow(saturate(dot(normal,SafeNormalize(input.halfDirWS))),24.0h);
                color += sun.color * half3(.45h,.30h,.15h) * (surface.a * sheen * diffuse);
            #endif
            return half4(color, 1);
        }
        half4 Depth(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            ClipEnd(input.uv);
            return 0;
        }
        ENDHLSL
        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Shade
            #pragma multi_compile_instancing
            #pragma shader_feature_local_fragment _FOREARM_FADE
            #pragma shader_feature_local _COPPER_DETAIL
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Depth
            #pragma multi_compile_instancing
            #pragma shader_feature_local_fragment _FOREARM_FADE
            ENDHLSL
        }
    }
}
