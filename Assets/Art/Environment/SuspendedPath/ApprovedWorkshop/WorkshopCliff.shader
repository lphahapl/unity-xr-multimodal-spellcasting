Shader "NarutoVR/Workshop Cliff"
{
    Properties
    {
        [MainTexture] _BaseMap("Layered cliff photo",2D)="gray" {}
        [NoScaleOffset] [Normal] _BumpMap("Stone laminae",2D)="bump" {}
        [NoScaleOffset] _RockMask("Cavity and measured smoothness",2D)="white" {}
        [NoScaleOffset] _Lichen("Lichen rock photo",2D)="gray" {}
        _Scale("Bedding width in metres",Float)=3.8
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "RockFaceForward"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
            TEXTURE2D(_Lichen); SAMPLER(sampler_Lichen);
            TEXTURE2D(_RockMask); SAMPLER(sampler_RockMask);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float _Scale;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS:POSITION; float3 normalOS:NORMAL; half4 color:COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; half3 norm:TEXCOORD1;
                half4 color:COLOR; half fog:TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes i)
            {
                Varyings o=(Varyings)0;
                UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs v=GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS=v.positionCS; o.world=v.positionWS; o.norm=TransformObjectToWorldNormal(i.normalOS);
                o.color=i.color; o.fog=ComputeFogFactor(v.positionCS.z); return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 p=i.world; half3 n=normalize(i.norm);
                // Bedding dips locally along the ravine, with strata remaining continuous across corners.
                float h=p.y+p.x*.10-p.z*.14+sin(p.z*.075+p.x*.055)*.68;
                float2 ux=float2(p.z,h)/_Scale, uz=float2(-p.x,h)/_Scale;
                float2 top=p.xz/_Scale;
                half3 w=pow(abs(n),3.0);w/=max(w.x+w.y+w.z,.001h);
                half3 strata=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,ux).rgb*w.x+
                             SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uz).rgb*w.z+
                             SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,top).rgb*w.y;
                // A second, rotated outcrop exposure breaks the photo's repeated block shapes at a larger scale.
                float2 outcrop=float2((p.x*.52+p.z*.85)*.092,h*.107);
                half3 broad=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,outcrop+float2(.23,.51)).rgb;
                half3 albedo=lerp(strata,broad,.29h)*lerp(.66h,1.14h,i.color.r);
                half3 lichen=SAMPLE_TEXTURE2D(_Lichen,sampler_Lichen,top*.69+outcrop*.23).rgb;
                albedo=lerp(albedo,lichen,saturate((n.y*.49h+i.color.g*.56h-.26h)*1.22h));
                half3 nx=UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,ux),.63h);
                half3 nz=UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,uz),.63h);
                half3 nt=UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,top),.63h);
                half3 detail=half3(-nz.x*w.z+nt.x*w.y,nx.y*w.x+nz.y*w.z,nx.x*w.x+nt.y*w.y);
                n=normalize(n+detail-n*dot(detail,n));
                float2 maskUV=w.y>w.x && w.y>w.z ? top : (w.x>w.z ? ux : uz);
                half4 mask=SAMPLE_TEXTURE2D(_RockMask,sampler_RockMask,maskUV);
                Light sun=GetMainLight(TransformWorldToShadowCoord(p));
                half lit=saturate(dot(n,sun.direction));
                half3 sky=max(SampleSH(n),half3(.24h,.255h,.275h));
                half3 diffuse=albedo*(sky*.82h+(.10h+.78h*lit*sun.shadowAttenuation)*min(sun.color,half3(1.5h,1.5h,1.5h)));
                half3 halfDirection=normalize(sun.direction+GetWorldSpaceNormalizeViewDir(p));
                half glint=pow(saturate(dot(n,halfDirection)),lerp(5.0h,38.0h,mask.a));
                diffuse*=lerp(.78h,1.0h,mask.g);
                diffuse+=sun.color*(glint*lit*mask.a*.018h*sun.shadowAttenuation);
                return half4(MixFog(diffuse,i.fog),1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
}
