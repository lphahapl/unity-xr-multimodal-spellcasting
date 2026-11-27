#ifndef QINGGUI_R02_GOLD_FORWARD_INCLUDED
#define QINGGUI_R02_GOLD_FORWARD_INCLUDED
// Standard opaque URP metal with bounded geometric specular anti-aliasing.
// Base smoothness remains the artist's .68; only subpixel normal variance
// broadens the local lobe. No albedo/emission fill or transparency is added.
half4 QingGuiGoldFragment(Varyings i):SV_Target
{
    UNITY_SETUP_INSTANCE_ID(i);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    SurfaceData surface;
    InitializeStandardLitSurfaceData(i.uv,surface);
    InputData input;
    InitializeInputData(i,surface.normalTS,input);
    InitializeBakedGIData(i,input);
    float3 dx=ddx(input.normalWS),dy=ddy(input.normalWS);
    float variance=.5*(dot(dx,dx)+dot(dy,dy));
    float perceptualRoughness=1-surface.smoothness;
    float roughness=perceptualRoughness*perceptualRoughness;
    float filteredRoughness=sqrt(saturate(roughness*roughness+min(.04,variance)));
    surface.smoothness=1-sqrt(filteredRoughness);
    surface.alpha=1;surface.emission=0;surface.clearCoatMask=0;
    half4 color=UniversalFragmentPBR(input,surface);
    return half4(MixFog(color.rgb,input.fogCoord),1);
}
#endif
