Shader "NarutoVR/Exterior Terrain"
{
    Properties
    {
        [MainTexture] _BaseMap("石土细节", 2D) = "white" {}
        _RockTint("岩石底色", Color) = (.63,.68,.70,1)
        _MossTint("缓坡苔色", Color) = (.36,.43,.30,1)
        _TextureMetres("纹理周期（米）", Float) = 3
        _MacroMetres("色块尺度（米）", Float) = 16
        _MossAmount("苔色覆盖", Range(0,1)) = .6
        _MacroContrast("大面明暗差", Range(0,.6)) = .28
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "TerrainForward"
            Tags { "LightMode"="UniversalForwardOnly" }
            ZWrite On
            Cull Back
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                half4 _RockTint, _MossTint;
                float _TextureMetres, _MacroMetres;
                half _MossAmount, _MacroContrast;
            CBUFFER_END
            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 tintFog : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            float Hash(float2 p)
            {
                float3 q=frac(float3(p.xyx)*.1031);
                q+=dot(q,q.yzx+33.33);
                return frac((q.x+q.y)*q.z);
            }
            float Noise(float2 p)
            {
                float2 i=floor(p), f=frac(p);f=f*f*(3-2*f);
                return lerp(lerp(Hash(i),Hash(i+float2(1,0)),f.x),
                            lerp(Hash(i+float2(0,1)),Hash(i+1),f.x),f.y);
            }
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs p=GetVertexPositionInputs(input.positionOS);
                half3 n=TransformObjectToWorldNormal(input.normalOS);
                output.positionCS=p.positionCS;output.positionWS=p.positionWS;output.normalWS=n;
                // Large colour variation is evaluated per vertex; no noise texture or extra pixel sample.
                float broad=Noise(p.positionWS.xz/max(_MacroMetres,1));
                float fine=Noise(p.positionWS.xz/max(_MacroMetres*.37,1)+float2(12.7,4.1));
                half moss=saturate((n.y-.45)*1.82)*saturate((broad-.24)*1.8)*_MossAmount;
                half value=1-_MacroContrast+_MacroContrast*(.75*broad+.25*fine)*2;
                output.tintFog=half4(lerp(_RockTint.rgb,_MossTint.rgb,moss)*value,ComputeFogFactor(p.positionCS.z));
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                // Oblique world projection avoids straight vertical stretching on steep slopes.
                float2 uv=float2(input.positionWS.x+input.positionWS.y*.43,
                                 input.positionWS.z+input.positionWS.y*.61)/max(_TextureMetres,.1);
                half3 detail=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv).rgb;
                half3 n=normalize(input.normalWS);
                float4 shadowCoord;
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    shadowCoord=ComputeScreenPos(TransformWorldToHClip(input.positionWS));
                #else
                    shadowCoord=TransformWorldToShadowCoord(input.positionWS);
                #endif
                Light sun=GetMainLight(shadowCoord,input.positionWS,half4(1,1,1,1));
                half3 light=max(SampleSH(n),half3(.04,.04,.04));
                light+=sun.color*saturate(dot(n,sun.direction))*sun.shadowAttenuation*sun.distanceAttenuation;
                half3 color=detail*input.tintFog.rgb*light;
                return half4(MixFog(color,input.tintFog.a),1);
            }
            ENDHLSL
        }
    }
}
