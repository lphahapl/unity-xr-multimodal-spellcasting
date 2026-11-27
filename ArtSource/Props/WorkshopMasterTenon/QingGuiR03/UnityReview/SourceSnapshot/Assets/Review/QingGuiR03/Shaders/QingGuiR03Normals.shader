Shader "Hidden/QingGuiR03/MeshNormals" {
SubShader { Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"} Pass {
HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
struct A{float4 positionOS:POSITION;float3 normalOS:NORMAL;};struct V{float4 positionCS:SV_POSITION;float3 normalWS:TEXCOORD0;};
V vert(A i){V o;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.normalWS=TransformObjectToWorldNormal(i.normalOS);return o;}
half4 frag(V i):SV_Target{return half4(normalize(i.normalWS)*.5+.5,1);}
ENDHLSL
} } }
