Shader "NarutoVR/Canal Flow"
{
    Properties
    {
        [NoScaleOffset] _FlowMap ("流纹（RG 波面斜率、B 泡锋、A 散沫）", 2D) = "gray" {}
        _WaterColor ("渠底水色", Color) = (0.17, 0.33, 0.34, 1)
        _SkyWaterColor ("掠视水色", Color) = (0.46, 0.64, 0.68, 1)
        _CrestColor ("泡沫颜色", Color) = (0.80, 0.85, 0.80, 1)
        _FlowSpeed ("渠面流速（米/秒）", Range(0, 2)) = 0.62
        _WaveHeight ("离槽水面起伏上限（米）", Range(0, 0.03)) = 0.023
        _FoamStrength ("流动碎沫强度", Range(0, 1.5)) = 1.08
        _GlintStrength ("宽水光强度", Range(0, 1)) = 0.48
        [Toggle(_CANAL_GAUGE)] _GaugeWater ("水尺近景", Float) = 0
        _OpenChannel ("整段平渠浅水", Range(0, 1)) = 0
        [NoScaleOffset] _Courtyard ("院落倒影", Cube) = "black" {}
        [HideInInspector] _SrcBlend ("Source blend", Float) = 1
        [HideInInspector] _DstBlend ("Destination blend", Float) = 0
        [HideInInspector] _DepthWrite ("Depth write", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "CanalFlowForward"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_DepthWrite]
            // An open sheet can be seen from both banks, including the underside of the lifted flow.
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma shader_feature_local _CANAL_GAUGE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_FlowMap);
            SAMPLER(sampler_FlowMap);
            TEXTURECUBE(_Courtyard);
            SAMPLER(sampler_Courtyard);
            CBUFFER_START(UnityPerMaterial)
                half4 _WaterColor;
                half4 _SkyWaterColor;
                half4 _CrestColor;
                float _FlowSpeed;
                float _WaveHeight;
                half _FoamStrength;
                half _GlintStrength;
                half _OpenChannel;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                // U spans the sheet. V is geometric distance from the first authored station.
                float2 uv : TEXCOORD0;
                // Width, lift blend, agitation, signed travel coordinate increasing downstream.
                float4 channel : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 flowUV : TEXCOORD0;
                half4 normalAndAir : TEXCOORD1;
                half4 tangentAndSign : TEXCOORD2;
                half4 viewAndFoam : TEXCOORD3;
                half2 fogAndBank : TEXCOORD4;
                #if defined(_CANAL_GAUGE)
                    half4 gauge : TEXCOORD5;
                #endif
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float across=input.uv.x;
                float air=input.channel.y;
                #if defined(_CANAL_GAUGE)
                    float z=input.positionOS.z;
                    float reach=smoothstep(50.12,50.75,z)*(1.-smoothstep(55.25,55.92,z));
                    float2 toLanding=(input.positionOS.xz-float2(.43,52.52))/float2(.42,.63);
                    float fall=step(0.,input.tangentOS.w);
                    // The transparent pool cannot depth-occlude the end of the falling strip.
                    float abovePool=smoothstep(.15,.24,input.positionOS.y);
                    output.gauge=half4(max(reach,fall),saturate(1.-dot(toLanding,toLanding)),
                        across,lerp(1.,abovePool,fall));
                    // A level canal has no waterfall mouth or submerged end to mask.
                    output.gauge=lerp(output.gauge,half4(1.,0.,across,1.),_OpenChannel);
                #endif
                float travelled=input.channel.w-_Time.y*_FlowSpeed;
                float bank=saturate(4.0*across*(1.0-across));
                float amplitude=_WaveHeight*lerp(0.40*bank, 0.58+0.42*bank, air);
                float primary=sin(travelled*5.35+across*2.7);
                float secondary=sin(travelled*8.10-across*4.6);
                input.positionOS+=input.normalOS*amplitude*(0.68*primary+0.32*secondary);

                VertexPositionInputs p=GetVertexPositionInputs(input.positionOS);
                VertexNormalInputs n=GetVertexNormalInputs(input.normalOS,input.tangentOS);
                output.positionCS=p.positionCS;
                output.normalAndAir=half4(n.normalWS,air);
                output.tangentAndSign=half4(n.tangentWS,input.tangentOS.w*GetOddNegativeScale());
                output.viewAndFoam=half4(GetWorldSpaceViewDir(p.positionWS),input.channel.z);
                // World-metre scale across both widths keeps individual bubbles a consistent size.
                // Keep V continuous across vertices: implicit derivatives then select the correct mip in VR.
                float lateral=lerp(input.positionOS.x-.539,(across-.5)*input.channel.x,air);
                float lateralShape=saturate(lateral/max(input.channel.x,.15)+.5);
                output.flowUV=float2(lateral*1.18+.035*primary+.025*sin(input.channel.w*1.7),
                                     travelled*.49+.065*(lateralShape-.5)*(lateralShape-.5));
                // A narrow bank ribbon uses the existing interpolator and texture channels.
                output.fogAndBank=half2(ComputeFogFactor(p.positionCS.z),
                    saturate((abs(across-.5)-.38)*8.33)*(1.0-air));
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 pattern=SAMPLE_TEXTURE2D(_FlowMap,sampler_FlowMap,input.flowUV);
                half air=input.normalAndAir.w;
                half3 n=normalize(input.normalAndAir.xyz);
                half3 tangent=input.tangentAndSign.xyz;
                half3 bitangent=cross(n,tangent)*input.tangentAndSign.w;
                half2 slope=(pattern.rg*2.0h-1.0h)*lerp(.24h,.33h,air);
                n=normalize(n+tangent*slope.x+bitangent*slope.y);
                half3 view=SafeNormalize(input.viewAndFoam.xyz);
                half grazing=1.0h-abs(dot(n,view));
                half sky=grazing*grazing;
                half3 color=lerp(_WaterColor.rgb,_SkyWaterColor.rgb,saturate(.09h+.62h*sky+.16h*slope.y+.07h*air));

                // A broad sky lobe is stable at long distances. Local wave normals break it into small moving glints.
                half3 halfVector=SafeNormalize(view+half3(-.44h,.78h,.44h));
                half light=saturate(abs(dot(n,halfVector)));
                half broad=light*light;
                broad*=broad;
                half glint=broad*broad;glint*=glint;
                color*=.94h+.18h*slope.x+.19h*slope.y;
                // Broad lobe remains readable on Quest; the small lobe breaks into moving highlights.
                color+=_SkyWaterColor.rgb*(.26h*broad+.74h*glint)*_GlintStrength;
                half turbulence=input.viewAndFoam.w;
                half foam=saturate(pattern.b*(.72h+.29h*air+.85h*turbulence)
                                  +pattern.a*(.40h+.44h*air+1.15h*turbulence));
                half bankFoam=input.fogAndBank.y*(pattern.b*.28h+pattern.a*.46h);
                foam=saturate((foam+bankFoam)*_FoamStrength);
                color=lerp(color,_CrestColor.rgb,foam);
                #if defined(_CANAL_GAUGE)
                    // The opaque stone under the water supplies binocular depth without a screen copy.
                    half4 detail=SAMPLE_TEXTURE2D(_FlowMap,sampler_FlowMap,
                        input.flowUV*float2(2.35,.72)+float2(.27,.41));
                    half2 fine=(detail.rg-.5h)*.10h;
                    half3 ripple=normalize(n+tangent*fine.x+bitangent*fine.y);
                    half facing=saturate(abs(dot(ripple,view)));
                    half fresnel=.06h+.85h*pow(1.h-facing,3.h);
                    half3 reflected=SAMPLE_TEXTURECUBE_LOD(_Courtyard,sampler_Courtyard,reflect(-view,ripple),1.5h).rgb;
                    half3 body=_WaterColor.rgb*half3(.67h,.81h,.84h);
                    half3 surface=lerp(body,reflected,fresnel)+_SkyWaterColor.rgb*glint*.13h;
                    half bubbles=saturate(pattern.b*.70h+detail.a*.65h+detail.b*.32h);
                    half landing=input.gauge.y;
                    half rim=smoothstep(.27h,.67h,landing)*(1.h-smoothstep(.78h,1.h,landing));
                    half streak=detail.a*.40h+detail.b*.26h;
                    half lather=saturate(bubbles*(.055h+landing*1.16h)+landing*.19h+rim*(.16h+.55h*detail.r)
                        +air*streak+bankFoam*.11h);
                    surface=lerp(surface,_CrestColor.rgb,lather);
                    half edge=saturate(min(input.gauge.z,1.h-input.gauge.z)*9.h);
                    half opacity=saturate(.20h+.70h*fresnel+.18h*air+landing*.12h+lather*.66h);
                    opacity*=lerp(1.h,lerp(.46h,1.h,edge),air)*input.gauge.w;
                    return half4(MixFog(lerp(color,surface,input.gauge.x),input.fogAndBank.x),
                        lerp(1.h,opacity,input.gauge.x));
                #else
                return half4(MixFog(color,input.fogAndBank.x),1.0h);
                #endif
            }
            ENDHLSL
        }
    }
}
