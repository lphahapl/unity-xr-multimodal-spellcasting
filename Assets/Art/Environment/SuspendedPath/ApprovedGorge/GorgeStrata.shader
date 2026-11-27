Shader "NarutoVR/Approved Gorge"
{
    Properties
    {
        [MainTexture] _BaseMap("Baked gorge relief and exposed rock", 2D) = "white" {}
        [NoScaleOffset] _Detail("Scan normal XY, albedo variation, roughness", 2D) = "gray" {}
        [NoScaleOffset] _Stone("Exposed cliff photo", 2D) = "gray" {}
        [NoScaleOffset] _Canopy("Pine stand occlusion", 2D) = "black" {}
        _TileMeters("Photo scan extent (m)", Float) = 3.2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "GorgeForward"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_Detail); SAMPLER(sampler_Detail);
            TEXTURE2D(_Stone); SAMPLER(sampler_Stone);
            TEXTURE2D(_Canopy); SAMPLER(sampler_Canopy);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float _TileMeters;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0;
                half3 normalWS:TEXCOORD1; float2 uv:TEXCOORD2; half fog:TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes v)
            {
                Varyings o=(Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs p=GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS=p.positionCS; o.positionWS=p.positionWS; o.uv=v.uv;
                o.normalWS=TransformObjectToWorldNormal(v.normalOS);
                o.fog=ComputeFogFactor(p.positionCS.z); return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half4 macro=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv);
                half3 n=normalize(i.normalWS);
                half3 blend=abs(n); blend*=blend; blend*=blend;
                blend/=max(dot(blend,half3(1,1,1)),0.001h);
                float3 p=i.positionWS/_TileMeters;
                half4 dx=SAMPLE_TEXTURE2D(_Detail,sampler_Detail,p.zy);
                half4 dy=SAMPLE_TEXTURE2D(_Detail,sampler_Detail,p.xz);
                half4 dz=SAMPLE_TEXTURE2D(_Detail,sampler_Detail,p.xy);
                half4 scan=dx*blend.x+dy*blend.y+dz*blend.z;
                half3 bump=half3(dy.r*blend.y+dz.r*blend.z,dx.g*blend.x+dz.g*blend.z,dx.r*blend.x+dy.g*blend.y)*2
                      -half3(blend.y+blend.z,blend.x+blend.z,blend.x+blend.y);
                half3 N=normalize(n+bump*lerp(.10h,.39h,macro.a));
                Light sun=GetMainLight();
                half key=saturate(dot(N,sun.direction));
                half3 base=macro.rgb*lerp(1.0h,clamp(scan.b*2,.63h,1.48h),lerp(.27h,.62h,macro.a));
                // Steep cuts have little planimetric UV area. Project the
                // actual stratified rock scan in metres directly on those walls.
                half sheer=smoothstep(.35h,.78h,1.0h-abs(n.y));
                if(sheer>.002h)
                {
                    half axis=blend.x/max(blend.x+blend.z,.001h);
                    half3 sideX=SAMPLE_TEXTURE2D(_Stone,sampler_Stone,p.zy*.56).rgb;
                    half3 sideZ=SAMPLE_TEXTURE2D(_Stone,sampler_Stone,p.xy*.56).rgb;
                    half3 strata=lerp(sideZ,sideX,axis)*half3(1.04h,.99h,.94h);
                    base=lerp(base,strata*(.76h+scan.b*.47h),sheer*.82h);
                }
                half shade=SAMPLE_TEXTURE2D(_Canopy,sampler_Canopy,i.uv).r;
                base*=lerp(half3(1,1,1),half3(.43h,.48h,.49h),shade);
                half3 diffuse=base*(half3(.67h,.72h,.77h)+(.55h*key)*min(sun.color,half3(1.5h,1.5h,1.5h)));
                return half4(MixFog(diffuse,i.fog),1);
            }
            ENDHLSL
        }
    }
}
