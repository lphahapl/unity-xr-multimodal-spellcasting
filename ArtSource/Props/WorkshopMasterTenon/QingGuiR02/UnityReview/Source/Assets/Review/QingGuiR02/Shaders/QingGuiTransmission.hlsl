#ifndef QINGGUI_R02_POLISHED_INCLUDED
#define QINGGUI_R02_POLISHED_INCLUDED
// Opaque layered jade appearance: standard URP coat/reflection, shallow internal
// density parallax and light-driven scattering. No scene refraction or emission.
half4 QingGuiFragment(Varyings i) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(i);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    SurfaceData surface;
    InitializeStandardLitSurfaceData(i.uv,surface);
    surface.alpha=1; surface.emission=0;
    half jade=saturate(SAMPLE_TEXTURE2D(_JadeMask,sampler_JadeMask,i.uv).r)*(1-saturate(surface.metallic));
    if(_JadeLayerMode>.5h && _JadeLayerMode<1.5h)clip(.5h-jade);
    if(_JadeLayerMode>1.5h)clip(jade-.5h);
    surface.clearCoatMask*=jade; // The actual _CLEARCOAT variant is enabled on jade materials.
    InputData input;
    InitializeInputData(i,surface.normalTS,input);
    InitializeBakedGIData(i,input);
    Light main=GetMainLight();
    float3 n=normalize(i.normalWS); // Interpolated mesh normal, not texture microdetail.
    float cosine=dot(n,main.direction);
    float thickness=max(0,SAMPLE_TEXTURE2D(_ThicknessMap,sampler_ThicknessMap,i.uv).r*_ThicknessMaxMetres);
    float3 posOS=TransformWorldToObject(input.positionWS);
    float3 rayOS=mul((float3x3)GetWorldToObjectMatrix(),main.direction);
    float3 safeRay=(step(0,rayOS)*2-1)*max(abs(rayOS),1e-8);
    float3 farBounds=lerp(_BoundsMinOS.xyz,_BoundsMaxOS.xyz,step(0,rayOS));
    float3 exits=(farBounds-posOS)/safeRay;
    float boxExit=max(0,min(exits.x,min(exits.y,exits.z)));
    float path=min(thickness/max(abs(cosine),_MinimumLightCosine),boxExit);
    float3 entryWS=input.positionWS+main.direction*(path+_ExitBiasMetres);
    half entryVisibility=MainLightRealtimeShadow(TransformWorldToShadowCoord(entryWS));
    half surfaceVisibility=MainLightRealtimeShadow(TransformWorldToShadowCoord(input.positionWS));

    // Local material-space 3D density field; moving the item does not swim the clouds.
    float3 scaleOS=float3(length(TransformObjectToWorldDir(float3(1,0,0),false)),length(TransformObjectToWorldDir(float3(0,1,0),false)),length(TransformObjectToWorldDir(float3(0,0,1),false)));
    float3 refractedWS=refract(-input.viewDirectionWS,input.normalWS,1.0/1.54);
    float3 inwardM=normalize(mul((float3x3)GetWorldToObjectMatrix(),refractedWS)*scaleOS);
    float depth=min(thickness,max(.0001,_InnerDepthMetres));
    float3 cloudUV=(posOS*scaleOS+inwardM*depth)/max(.005,_CloudScaleMetres);
    half density=lerp(.85h,1.15h,SAMPLE_TEXTURE3D(_JadeCloud,sampler_JadeCloud,cloudUV).r);
    half3 bodyColor=surface.albedo*lerp(half3(1,1,1),exp(-max(0,_AbsorptionPerMetre.rgb)*depth*density),jade*_DepthColorStrength);
    half scatterShare=jade*_ScatterStrength;
    surface.albedo=bodyColor*(1-scatterShare);
    half4 lit=UniversalFragmentPBR(input,surface);
    half wrap=saturate((dot(input.normalWS,main.direction)+.25h)/1.25h);
    half3 shallow=bodyColor*scatterShare*(main.color*main.distanceAttenuation*surfaceVisibility*wrap+input.bakedGI)*(.96h*(1-.04h*surface.clearCoatMask));
    half phase=.35h+.65h*pow(saturate(dot(-input.viewDirectionWS,main.direction)),3);
    half3 attenuation=exp(-max(0,_AbsorptionPerMetre.rgb)*path*density);
    half3 transmission=main.color*main.distanceAttenuation*entryVisibility*saturate(-cosine)*phase*attenuation*jade*_TransmissionStrength;
    if(_TransmissionDebug>7.5h)return half4(density.xxx/1.45h,1);
    if(_TransmissionDebug>6.5h)return half4(surface.clearCoatMask.xxx,1);
    if(_TransmissionDebug>5.5h)return half4(shallow,1);
    if(_TransmissionDebug>4.5h)return half4(surface.metallic.xxx,1);
    if(_TransmissionDebug>3.5h)return half4(jade.xxx,1);
    if(_TransmissionDebug>2.5h)return half4(thickness.xxx/max(.00001,_ThicknessMaxMetres),1);
    if(_TransmissionDebug>1.5h)return half4(entryVisibility.xxx,1);
    if(_TransmissionDebug>.5h)return half4(transmission,1);
    half opacity=1;
    if(_JadeLayerMode>1.5h) {
        float viewPath=thickness/max(.2,abs(dot(n,input.viewDirectionWS)));
        half surviving=exp(-dot(max(0,_AbsorptionPerMetre.rgb),half3(.2126,.7152,.0722))*viewPath*density);
        opacity=1-jade*_JadeTransparency*surviving;
    }
    return half4(MixFog(lit.rgb+shallow+transmission,input.fogCoord),opacity);
}
#endif
