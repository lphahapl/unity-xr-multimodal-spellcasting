Shader "NarutoVR/Opening/Cloudline Granite"
{
 Properties
 {
  _BaseMap ("Baked granite", 2D) = "white" {}
  _Color ("Cool mineral tint", Color) = (.69,.79,.81,1)
 }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-70" }
  Pass
  {
   Cull Back ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile_instancing
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
   CBUFFER_START(UnityPerMaterial)
    half4 _Color;
   CBUFFER_END
   struct Attributes { float3 pos:POSITION; float2 uv:TEXCOORD0; float3 n:NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
   struct Varyings { float4 pos:SV_POSITION; float3 world:TEXCOORD0; half3 n:TEXCOORD1; float2 uv:TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };
   Varyings Vert(Attributes i) {
    Varyings o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    o.world=TransformObjectToWorld(i.pos);o.pos=TransformWorldToHClip(o.world);
    o.n=TransformObjectToWorldNormal(i.n);o.uv=i.uv;return o;
   }
   half4 Frag(Varyings i):SV_Target {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    // The lower rock disappears into the cloud deck at a common world height, independent of eye or LOD.
    float emergence=smoothstep(-17.5,5.5,i.world.y);
    clip(emergence-.004);
    half3 albedo=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb;
    half3 normal=normalize(i.n);
    Light sun=GetMainLight();half light=saturate(dot(normal,sun.direction));
    half3 stone=albedo*_Color.rgb*(.46h+.83h*light);
    half distanceFog=saturate((distance(_WorldSpaceCameraPos,i.world)-55.)/225.);
    stone=lerp(stone,half3(.20h,.32h,.38h),.18h+.26h*distanceFog);
    return half4(stone,emergence*(.90h-distanceFog*.13h));
   }
   ENDHLSL
  }
 }
}
