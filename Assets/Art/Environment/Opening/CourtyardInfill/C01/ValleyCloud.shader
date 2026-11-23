Shader "Kaogong/Environment/ValleyCloudCard"
{
 Properties { _BaseMap("Cloud RGB and opacity", 2D)="white" {} _Opacity("Layer opacity", Range(0,1))=.72 }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-25" "RenderType"="Transparent" }
  Pass
  {
   Name "Low valley clouds"
   Tags { "LightMode"="SRPDefaultUnlit" }
   Cull Off ZWrite Off ZTest LEqual Blend SrcAlpha OneMinusSrcAlpha
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile_instancing
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
   CBUFFER_START(UnityPerMaterial)
   float4 _BaseMap_ST;
   half _Opacity;
   CBUFFER_END
   struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
   struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half alpha:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
   Varyings Vert(Attributes input)
   {
    Varyings o=(Varyings)0; UNITY_SETUP_INSTANCE_ID(input); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    o.positionCS=TransformObjectToHClip(input.positionOS.xyz); o.uv=TRANSFORM_TEX(input.uv,_BaseMap);
    o.alpha=input.color.a*_Opacity;return o;
   }
   half4 Frag(Varyings i):SV_Target
   {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    half4 cloud=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv);
    return half4(cloud.rgb,cloud.a*i.alpha);
   }
   ENDHLSL
  }
 }
}
