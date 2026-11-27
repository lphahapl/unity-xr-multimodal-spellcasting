#ifndef QINGGUI_TRANSMISSION_INCLUDED
#define QINGGUI_TRANSMISSION_INCLUDED

// Opaque, thickness-driven single-scatter appearance approximation.
// Not screen-space SSS, refraction, or emission. Only the URP main directional light transmits.
half4 QingGuiFragment(Varyings i) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(i);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    SurfaceData surface;
    InitializeStandardLitSurfaceData(i.uv,surface);
    surface.alpha=1;
    surface.emission=0;
    InputData input;
    InitializeInputData(i,surface.normalTS,input);
    InitializeBakedGIData(i,input);
    half4 lit=UniversalFragmentPBR(input,surface);

    Light main=GetMainLight();
    float3 n=normalize(i.normalWS); // Geometric normal for physical path, never micro-normal detail.
    float cosine=dot(n,main.direction);
    float thickness=max(0,SAMPLE_TEXTURE2D(_ThicknessMap,sampler_ThicknessMap,i.uv).r*_ThicknessMaxMetres);
    // Thickness is an inward-normal ray map. The parallel-slab estimate is only
    // local: cap it at the actual renderer-space AABB exit to avoid leaping past
    // a nearby external caster at grazing incidence. This does not reconstruct
    // curved internal geometry and is not screen-space or volumetric SSS.
    float3 posOS=TransformWorldToObject(input.positionWS);
    float3 rayOS=mul((float3x3)GetWorldToObjectMatrix(),main.direction);
    // Keep rayOS unnormalised so intersection t remains measured in world metres.
    float3 safeRay=(step(0,rayOS)*2-1)*max(abs(rayOS),1e-8);
    float3 farBounds=lerp(_BoundsMinOS.xyz,_BoundsMaxOS.xyz,step(0,rayOS));
    float3 exits=(farBounds-posOS)/safeRay;
    float boxExit=max(0,min(exits.x,min(exits.y,exits.z)));
    float path=min(thickness/max(abs(cosine),_MinimumLightCosine),boxExit);
    float3 entryWS=input.positionWS+main.direction*(path+_ExitBiasMetres);
    half entryVisibility=MainLightRealtimeShadow(TransformWorldToShadowCoord(entryWS));
    half jade=saturate(SAMPLE_TEXTURE2D(_JadeMask,sampler_JadeMask,i.uv).r)*(1-saturate(surface.metallic));
    half phase=.35h+.65h*pow(saturate(dot(-input.viewDirectionWS,main.direction)),3);
    half3 attenuation=exp(-max(0,_AbsorptionPerMetre.rgb)*path);
    half3 transmission=main.color*main.distanceAttenuation*entryVisibility
        *saturate(-cosine)*phase*attenuation*jade*_TransmissionStrength;
    if(_TransmissionDebug>4.5h)return half4(surface.metallic.xxx,1);
    if(_TransmissionDebug>3.5h)return half4(jade.xxx,1);
    if(_TransmissionDebug>2.5h)return half4(thickness.xxx/max(.00001,_ThicknessMaxMetres),1);
    if(_TransmissionDebug>1.5h)return half4(entryVisibility.xxx,1);
    if(_TransmissionDebug>.5h)return half4(transmission,1);
    return half4(MixFog(lit.rgb+transmission,input.fogCoord),1);
}
#endif
