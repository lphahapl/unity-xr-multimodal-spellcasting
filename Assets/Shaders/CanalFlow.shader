Shader "NarutoVR/Canal Flow"
{
    Properties
    {
        [NoScaleOffset] _FlowMap ("流纹（RG 波面斜率、B 泡锋、A 散沫）", 2D) = "gray" {}
        _WaterColor ("渠底水色", Color) = (0.17, 0.33, 0.34, 1)
        _SkyWaterColor ("掠视水色", Color) = (0.46, 0.64, 0.68, 1)
        _CrestColor ("泡沫颜色", Color) = (0.80, 0.85, 0.80, 1)
        _FlowSpeed ("渠面流速（米/秒）", Range(0, 2)) = 0.62
        _WaveHeight ("逆流水面起伏上限（米）", Range(0, 0.03)) = 0.023
        _FoamStrength ("流动碎沫强度", Range(0, 1.5)) = 1.08
        _GlintStrength ("宽水光强度", Range(0, 1)) = 0.48
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "CanalFlowForward"
            Tags { "LightMode"="SRPDefaultUnlit" }
            ZWrite On
            // An open sheet can be seen from both banks, including the underside of the lifted flow.
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_FlowMap);
            SAMPLER(sampler_FlowMap);
            CBUFFER_START(UnityPerMaterial)
                half4 _WaterColor;
                half4 _SkyWaterColor;
                half4 _CrestColor;
                float _FlowSpeed;
                float _WaveHeight;
                half _FoamStrength;
                half _GlintStrength;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                // U spans the sheet. V is travelled distance in metres, positive towards each outlet.
                float2 uv : TEXCOORD0;
                // Width, lift blend, agitation, integrated travel time in baseline-flow metres.
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
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float across=input.uv.x;
                float air=input.channel.y;
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
                return half4(MixFog(color,input.fogAndBank.x),1.0h);
            }
            ENDHLSL
        }
    }
}
