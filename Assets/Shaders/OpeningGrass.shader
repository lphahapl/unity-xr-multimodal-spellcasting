Shader "NarutoVR/Opening Grass"
{
    Properties
    {
        _WindAmplitude("Tip amplitude (m)", Range(0, 0.04)) = 0.025
        _WindFrequency("Frequency (Hz)", Range(0.35, 0.65)) = 0.5
        _WindDirection("Wind direction (world XZ)", Vector) = (0.8, 0.6, 0, 0)
        _WindWaveNumber("Wind travel (radians per metre)", Range(0, 3)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "DisableBatching" = "True"
        }

        Pass
        {
            Name "GrassForward"
            Tags { "LightMode" = "UniversalForwardOnly" }
            ZWrite On
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _WindDirection;
                float _WindAmplitude;
                float _WindFrequency;
                float _WindWaveNumber;
            CBUFFER_END

            float4 _OpeningGrassViewer;

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 rootPhaseOS : TEXCOORD1;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 colorFog : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float3 GrassViewerPositionWS()
            {
                if (_OpeningGrassViewer.w > 0.5) return _OpeningGrassViewer.xyz;

                // The scene view and camera previews render without the runtime controller.
                #if defined(USING_STEREO_MATRICES)
                    return (unity_StereoWorldSpaceCameraPos[0] + unity_StereoWorldSpaceCameraPos[1]) * 0.5;
                #else
                    return GetCameraPositionWS();
                #endif
            }

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 rootWS = TransformObjectToWorld(input.rootPhaseOS.xyz);
                float distanceToViewer = distance(rootWS, GrassViewerPositionWS());
                float windFade = 1.0 - smoothstep(6.0, 12.0, distanceToViewer);
                float visibleScale = 1.0 - smoothstep(14.0, 18.0, distanceToViewer);
                float weight = input.color.a;
                float2 direction = _WindDirection.xy;
                direction *= rsqrt(max(dot(direction, direction), 0.0001));
                float phase = _Time.y * (TWO_PI * _WindFrequency) + input.rootPhaseOS.w
                    - dot(rootWS.xz, direction) * _WindWaveNumber;
                float sway = _WindAmplitude * weight * weight * sin(phase) * windFade;

                float3 positionWS = TransformObjectToWorld(input.positionOS);
                positionWS = rootWS + (positionWS - rootWS + float3(direction.x, 0.0, direction.y) * sway) * visibleScale;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.colorFog = half4(input.color.rgb, ComputeFogFactor(output.positionCS.z));
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return half4(MixFog(input.colorFog.rgb, input.colorFog.a), 1.0);
            }
            ENDHLSL
        }
    }
}
