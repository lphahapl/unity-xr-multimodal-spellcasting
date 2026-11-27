Shader "NarutoVR/H03 Close Surface"
{
    Properties
    {
        [MainTexture] _BaseMap("Original tower atlas",2D)="white"{}
        [MainColor] _BaseColor("Original tower tint",Color)=(1,1,1,1)
        [Normal] _BumpMap("Original normal",2D)="bump"{}
        _BumpScale("Original normal strength",Float)=1
        _MetallicGlossMap("Original metal and smoothness",2D)="white"{}
        _Metallic("Metallic",Range(0,1))=1
        _Smoothness("Smoothness",Range(0,1))=1
        _Cutoff("Cutoff",Range(0,1))=.5
        _Cull("Cull",Float)=2
        _Surface("Opaque",Float)=0
        _OcclusionStrength("Occlusion strength",Float)=1
        _SpecColor("Specular",Color)=(.2,.2,.2,1)
        [NoScaleOffset] _CastDetail("Casting surface RG/B/A",2D)="gray"{}
        [NoScaleOffset] _StoneDetail("Chisel surface RG/B/A",2D)="gray"{}
        _FootY("Foot datum in metres",Float)=0
        _ForceStone("Stone insert",Float)=0
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
        Pass
        {
            Name "H03 Bronze and Stone"
            Tags {"LightMode"="UniversalForwardOnly"}
            Cull [_Cull] ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex LitPassVertex
            #pragma fragment CloseFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma shader_feature_local_fragment _ _METALLICSPECGLOSSMAP
            #pragma shader_feature_local_fragment _ _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #define _NORMALMAP 1
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"

            TEXTURE2D(_CastDetail); SAMPLER(sampler_CastDetail);
            TEXTURE2D(_StoneDetail); SAMPLER(sampler_StoneDetail);
            float _FootY;
            float _ForceStone;

            void MeasuredSurface(TEXTURE2D_PARAM(map, smp), float3 p, half3 g, half3 weights,
                                 out half3 relief, out half2 valueRoughness)
            {
                half3 axisSign = half3(g.x<0?-1:1,g.y<0?-1:1,g.z<0?-1:1);
                half4 x = SAMPLE_TEXTURE2D(map,smp,float2(-axisSign.x*p.z,p.y));
                half4 y = SAMPLE_TEXTURE2D(map,smp,float2(p.x,-axisSign.y*p.z));
                half4 z = SAMPLE_TEXTURE2D(map,smp,float2(axisSign.z*p.x,p.y));
                half2 sx=x.rg*2-1, sy=y.rg*2-1, sz=z.rg*2-1;
                relief=weights.x*half3(0,sx.y,-axisSign.x*sx.x)
                      +weights.y*half3(sy.x,0,-axisSign.y*sy.y)
                      +weights.z*half3(axisSign.z*sz.x,sz.y,0);
                valueRoughness=x.ba*weights.x+y.ba*weights.y+z.ba*weights.z;
            }

            half4 CloseFragment(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                SurfaceData surface;
                InitializeStandardLitSurfaceData(i.uv,surface);
                InputData input;
                InitializeInputData(i,surface.normalTS,input);
                float3 p=i.positionWS;
                half nearFactor=1-smoothstep(11.0,24.0,distance(p,_WorldSpaceCameraPos));
                if (nearFactor>0.001h)
                {
                    half3 g=normalize(i.normalWS);
                    half3 w=pow(abs(g),4);
                    w/=max(.001h,w.x+w.y+w.z);
                    half y=p.y-_FootY;
                    // The cast gear and straps carry a warmer patina than the ashlars.
                    // The original circumferential foot binding crosses the hub sightline at +1.8 m.
                    half luminance=max(.025h,dot(surface.albedo,half3(.2126,.7152,.0722)));
                    half tint=smoothstep(.18h,.64h,(surface.albedo.r-surface.albedo.b)/luminance);
                    half collar=smoothstep(1.60h,1.69h,y)*(1-smoothstep(1.89h,1.97h,y));
                    half metal=max(tint,collar)*(1-_ForceStone);
                    half3 stoneSlope,castSlope;half2 stone,cast;
                    MeasuredSurface(TEXTURE2D_ARGS(_StoneDetail,sampler_StoneDetail),p/1.72,g,w,stoneSlope,stone);
                    MeasuredSurface(TEXTURE2D_ARGS(_CastDetail,sampler_CastDetail),p/1.35,g,w,castSlope,cast);
                    half2 detail=lerp(stone,cast,metal);
                    half3 gradient=lerp(stoneSlope*.71h,castSlope*.82h,metal);
                    input.normalWS=normalize(input.normalWS+nearFactor*(gradient-g*dot(g,gradient)));
                    half3 brass=half3(.21h,.14h,.075h)*clamp(luminance/.108h,.75h,1.24h);
                    surface.albedo=lerp(surface.albedo,brass,nearFactor*metal*.54h);
                    surface.albedo*=1+nearFactor*(detail.x-.5h)*lerp(.90h,1.09h,metal);
                    half polish=lerp(.12h+(.74h-detail.y)*.39h,.31h+(.70h-detail.y)*.67h,metal);
                    surface.smoothness=lerp(surface.smoothness,saturate(polish),nearFactor*.69h);
                    surface.metallic=lerp(surface.metallic,lerp(.025h,.40h,metal),nearFactor*.85h);
                }
                InitializeBakedGIData(i,input);
                half4 lit=UniversalFragmentPBR(input,surface);
                return half4(MixFog(lit.rgb,input.fogCoord),1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
        UsePass "Universal Render Pipeline/Lit/Meta"
    }
}
