#ifndef QINGGUI_R03_SCATTERING_INCLUDED
#define QINGGUI_R03_SCATTERING_INCLUDED
// Opaque, warm white nephrite appearance approximation. No scene colour,
// transparency, emission, or measured IOR claim. Thickness is measured geometry;
// absorption/scatter coefficients below are explicitly artistic fits.
half4 QingGuiFragment(Varyings i) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(i);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    SurfaceData surface;
    InitializeStandardLitSurfaceData(i.uv,surface);
    surface.alpha=1;
    surface.emission=0;
    surface.metallic=0; // This shader is bound only to the closed JadeCore.
    InputData input;
    InitializeInputData(i,surface.normalTS,input);
    InitializeBakedGIData(i,input);
    Light main=GetMainLight();
    float3 n=normalize(i.normalWS);
    float cosine=dot(n,main.direction);
    float thickness=max(0,SAMPLE_TEXTURE2D(_ThicknessMap,sampler_ThicknessMap,i.uv).r*_ThicknessMaxMetres);
    float3 posOS=TransformWorldToObject(input.positionWS);
    float3 rayOS=mul((float3x3)GetWorldToObjectMatrix(),main.direction);
    float3 safeRay=(step(0,rayOS)*2-1)*max(abs(rayOS),1e-8);
    float3 farBounds=lerp(_BoundsMinOS.xyz,_BoundsMaxOS.xyz,step(0,rayOS));
    float3 exits=(farBounds-posOS)/safeRay;
    float boxExit=max(0,min(exits.x,min(exits.y,exits.z)));
    // Local-normal thickness is a single-ray approximation, not directional
    // volumetric ray tracing. The actual object bounds cap grazing path growth.
    float path=min(thickness/max(abs(cosine),_MinimumLightCosine),boxExit);
    float3 entryWS=input.positionWS+main.direction*(path+_ExitBiasMetres);
    half entryVisibility=MainLightRealtimeShadow(TransformWorldToShadowCoord(entryWS));
    // The chapter deliberately draws shadows at strength .68. Recover the
    // geometric visibility for direct through-light, so an opaque blocker does
    // not transmit the artistic 32% shadow-floor. PBR surface shadows keep .68.
    half shadowStrength=GetMainLightShadowParams().x;
    entryVisibility=shadowStrength>.0001h?saturate((entryVisibility-(1-shadowStrength))/shadowStrength):1;
    half surfaceVisibility=MainLightRealtimeShadow(TransformWorldToShadowCoord(input.positionWS));
    float3 scaleOS=float3(length(TransformObjectToWorldDir(float3(1,0,0),false)),length(TransformObjectToWorldDir(float3(0,1,0),false)),length(TransformObjectToWorldDir(float3(0,0,1),false)));
    float3 viewOS=normalize(mul((float3x3)GetWorldToObjectMatrix(),input.viewDirectionWS));
    float3 sampleOS=posOS*scaleOS-viewOS*_InnerDepthMetres*.15;
    half grain=SAMPLE_TEXTURE3D(_JadeCloud,sampler_JadeCloud,sampleOS/max(.005,_CloudScaleMetres)).r;
    half density=lerp(.97h,1.03h,grain);
    half3 bodyColor=surface.albedo*lerp(1.0h,density,_DepthColorStrength);
    half scatterShare=saturate(_ScatterStrength);
    surface.albedo=bodyColor*(1-scatterShare);
    half4 lit=UniversalFragmentPBR(input,surface);
    half wrap=saturate((dot(input.normalWS,main.direction)+.35h)/1.35h);
    half3 shallow=bodyColor*scatterShare*(main.color*main.distanceAttenuation*surfaceVisibility*wrap+input.bakedGI)*surface.occlusion;
    half phase=.45h+.55h*pow(saturate(dot(-input.viewDirectionWS,main.direction)),3);
    half3 attenuation=exp(-max(0,_AbsorptionPerMetre.rgb)*path*density);
    half3 transmission=main.color*main.distanceAttenuation*entryVisibility*saturate(-cosine)*phase*attenuation*_TransmissionStrength;
    if(_TransmissionDebug>6.5h)return half4(abs(_ShadowBias.x)*2,abs(_ShadowBias.y)*2,_ShadowBias.x<0?1:0,1);
    if(_TransmissionDebug>4.5h)return half4(surfaceVisibility.xxx,1);
    if(_TransmissionDebug>3.5h)return half4(1,1,1,1);
    if(_TransmissionDebug>2.5h)return half4(thickness.xxx/max(.00001,_ThicknessMaxMetres),1);
    if(_TransmissionDebug>1.5h)return half4(entryVisibility.xxx,1);
    if(_TransmissionDebug>.5h)return half4(transmission,1);
    return half4(MixFog(lit.rgb+shallow+transmission,input.fogCoord),1);
}
#endif
