Shader "NarutoVR/Review/QingGui R02 Comparison R01"
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
        _AbsorptionPerMetre("RGB absorption per metre (appearance fit)",Vector)=(90,45,75,0)
        _TransmissionStrength("Main-light transmission",Range(0,1))=.65
        _ExitBiasMetres("Outside entrance bias (m)",Range(0,.003))=.001
        _MinimumLightCosine("Near-parallel numerical guard",Range(.01,.1))=.01
        [HideInInspector] _BoundsMinOS("Mesh bounds minimum",Vector)=(-.5,-.5,-.5,0)
        [HideInInspector] _BoundsMaxOS("Mesh bounds maximum",Vector)=(.5,.5,.5,0)
        [HideInInspector] _TransmissionDebug("0 surface / 1 delta / 2 entrance shadow / 3 thickness",Float)=0
        [HideInInspector] _SpecColor("Specular",Color)=(.2,.2,.2,1)
        [HideInInspector] _EmissionColor("No emission",Color)=(0,0,0,0)
        [HideInInspector] _Cutoff("Cutoff",Float)=.5
        [HideInInspector] _Surface("Opaque",Float)=0
        [HideInInspector] _Cull("Cull",Float)=2
        [HideInInspector] _ClearCoatMask("Clearcoat",Float)=0
        [HideInInspector] _ClearCoatSmoothness("Clearcoat roughness",Float)=0
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
        Pass
        {
            Name "ForwardLit"
            Tags {"LightMode"="UniversalForwardOnly"}
            Cull Back ZWrite On
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
            #define _NORMALMAP 1
            #include "QingGuiLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"
            #include "QingGuiTransmission.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags {"LightMode"="ShadowCaster"}
            ZWrite On ZTest LEqual ColorMask 0 Cull Back
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "QingGuiLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags {"LightMode"="DepthOnly"}
            ZWrite On ColorMask R Cull Back
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing
            #include "QingGuiLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags {"LightMode"="DepthNormals"}
            ZWrite On Cull Back
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma multi_compile_instancing
            #define _NORMALMAP 1
            #include "QingGuiLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitDepthNormalsPass.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "Meta"
            Tags {"LightMode"="Meta"}
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex UniversalVertexMeta
            #pragma fragment UniversalFragmentMetaLit
            #pragma shader_feature EDITOR_VISUALIZATION
            #pragma shader_feature_local_fragment _ _METALLICSPECGLOSSMAP
            #include "QingGuiLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitMetaPass.hlsl"
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
