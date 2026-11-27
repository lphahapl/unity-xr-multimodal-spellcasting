Shader "NarutoVR/Review/QingGui R02 Local Jade Transparency"
{
    Properties
    {
        [MainTexture] _BaseMap("Base colour (sRGB)",2D)="white"{}
        [MainColor] _BaseColor("Tint",Color)=(.5,.68,.57,1)
        [Normal] _BumpMap("Tangent normal",2D)="bump"{}
        _BumpScale("Normal scale",Float)=1
        _MetallicGlossMap("Metal R / smooth A",2D)="white"{}
        _Metallic("Metallic",Range(0,1))=0
        _Smoothness("Smoothness",Range(0,1))=.65
        _OcclusionMap("AO (G)",2D)="white"{}
        _OcclusionStrength("AO strength",Range(0,1))=1
        [NoScaleOffset] _ThicknessMap("Measured thickness (linear R)",2D)="white"{}
        [NoScaleOffset] _JadeMask("Jade only (linear R)",2D)="white"{}
        _ThicknessMaxMetres("Thickness map white in metres",Float)=.12
        _AbsorptionPerMetre("RGB absorption per metre (appearance fit)",Vector)=(25,28,38,0)
        _TransmissionStrength("Main-light transmission",Range(0,1))=.55
        _ExitBiasMetres("Outside entrance bias (m)",Range(0,.003))=.001
        _MinimumLightCosine("Near-parallel numerical guard",Range(.01,.1))=.01
        [HideInInspector] _BoundsMinOS("Mesh bounds minimum",Vector)=(-.5,-.5,-.5,0)
        [HideInInspector] _BoundsMaxOS("Mesh bounds maximum",Vector)=(.5,.5,.5,0)
        [NoScaleOffset] _JadeCloud("Internal density field (linear 3D)",3D)="white"{}
        _CloudScaleMetres("Internal cloud period (m)",Float)=.06
        _InnerDepthMetres("Shallow internal sample depth (m)",Float)=.012
        _DepthColorStrength("Internal colour weight",Range(0,1))=.45
        _ScatterStrength("Jade shallow scatter share",Range(0,1))=.3
        [HideInInspector] _JadeLayerMode("0 opaque all / 1 opaque metal / 2 transparent jade",Float)=0
        _JadeTransparency("Measured-thickness jade transparency weight",Range(0,.65))=.5
        [HideInInspector] _TransmissionDebug("0 surface / 1 delta / 2 entrance shadow / 3 thickness",Float)=0
        [HideInInspector] _SpecColor("Specular",Color)=(.2,.2,.2,1)
        [HideInInspector] _EmissionColor("No emission",Color)=(0,0,0,0)
        [HideInInspector] _Cutoff("Cutoff",Float)=.5
        [HideInInspector] _Surface("Opaque",Float)=0
        [HideInInspector] _Cull("Cull",Float)=2
        _ClearCoatMask("Jade polish layer",Range(0,1))=.6
        _ClearCoatSmoothness("Polish layer smoothness",Range(0,1))=.9
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent"}
        Pass
        {
            Name "ForwardLit"
            Tags {"LightMode"="UniversalForwardOnly"}
            Cull Back ZWrite On
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex LitPassVertex
            #pragma fragment QingGuiFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma shader_feature_local_fragment _ _METALLICSPECGLOSSMAP
            #pragma shader_feature_local_fragment _ _OCCLUSIONMAP
            #pragma shader_feature_local_fragment _ _CLEARCOAT
            #define _NORMALMAP 1
            #include "QingGuiLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"
            #include "QingGuiTransmission.hlsl"
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
