Shader "NarutoVR/Environment/青灰薄云天空"
{
    Properties
    {
        _ZenithColor ("天顶颜色", Color) = (0.46, 0.56, 0.64, 1)
        _HorizonColor ("地平线颜色", Color) = (0.72, 0.78, 0.80, 1)
        _LowerColor ("下半球雾色", Color) = (0.60, 0.68, 0.71, 1)
        _GradientPower ("天空渐变曲线", Range(0.3, 3)) = 0.65
        _Exposure ("整体亮度", Range(0, 2)) = 1
        [Toggle(_CLOUDS_ON)] _Clouds ("启用薄云（关闭可省纹理采样）", Float) = 1
        [NoScaleOffset] _CloudMap ("薄云密度图（灰度）", 2D) = "black" {}
        _CloudColor ("薄云颜色", Color) = (0.89, 0.91, 0.92, 1)
        _CloudOpacity ("云层不透明度", Range(0, 1)) = 0.72
        _CloudDensity ("云纹增强", Range(0, 4)) = 1.7
        _CloudScale ("主层纹理尺度", Range(0.05, 1)) = 0.23
        _WindDirection ("主层风向（角度）", Range(0, 360)) = 25
        _WindSpeed ("主层流速（纹理格每秒）", Range(0, 0.02)) = 0.0015
        [Toggle(_SECOND_LAYER)] _SecondLayer ("启用第二层薄云", Float) = 1
        _Layer2Scale ("第二层相对尺度", Range(0.5, 3)) = 1.6
        _Layer2Weight ("第二层强度", Range(0, 1)) = 0.32
        _Layer2Speed ("第二层相对速度", Range(0, 2)) = 0.6
        _HorizonFade ("地平线消隐高度", Range(0.03, 0.4)) = 0.15
        [ToggleUI] _Animate ("云层流动（关闭仅冻结，不降低采样）", Float) = 1
        [HideInInspector] _PreviewTime ("预览固定时间，负数用游戏时间", Float) = -1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            Name "ThinCloudSky"
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma shader_feature_local_fragment _CLOUDS_ON
            #pragma shader_feature_local_fragment _SECOND_LAYER
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_CloudMap);
            SAMPLER(sampler_CloudMap);
            CBUFFER_START(UnityPerMaterial)
                half4 _ZenithColor, _HorizonColor, _LowerColor, _CloudColor;
                float _GradientPower, _Exposure, _Clouds, _CloudOpacity, _CloudDensity;
                float _CloudScale, _WindDirection, _WindSpeed, _SecondLayer;
                float _Layer2Scale, _Layer2Weight, _Layer2Speed, _HorizonFade, _Animate, _PreviewTime;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction : TEXCOORD0;
                float2 drift : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                // Unity 天空盒绘制会提供以相机为中心的盒体；方向只取顶点，不使用头部平移。
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz;
                float sine, cosine;
                sincos(radians(_WindDirection), sine, cosine);
                float seconds = _PreviewTime >= 0 ? _PreviewTime : _Time.y;
                output.drift = float2(cosine, sine) * seconds * _WindSpeed * _Animate;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 direction = normalize(input.direction);
                half upper = pow(saturate(direction.y), max(_GradientPower, 0.01));
                half lower = saturate(-direction.y * 3.0);
                half3 sky = lerp(_HorizonColor.rgb, _ZenithColor.rgb, upper);
                sky = lerp(sky, _LowerColor.rgb, lower);
                #if defined(_CLOUDS_ON)
                    // 平面投影无经度接缝和极点；地平线淡出避免远处纹理堆叠。
                    // 不对 UV 做 frac，保留导数连续性，让 mip 正确滤除远处细丝。
                    float2 uv = direction.xz / (max(direction.y, 0.0) + 0.18) * _CloudScale;
                    half cloud = SAMPLE_TEXTURE2D(_CloudMap, sampler_CloudMap, uv + input.drift).r;
                    #if defined(_SECOND_LAYER)
                        // 次层只偏转约 20 度，避免细长云丝正交交叉成网格。
                        float2 secondaryUV = float2(uv.x * 0.939693 - uv.y * 0.342020,
                                                    uv.x * 0.342020 + uv.y * 0.939693)
                            * _Layer2Scale + float2(0.37, 0.61);
                        half secondary = SAMPLE_TEXTURE2D(_CloudMap, sampler_CloudMap,
                            secondaryUV + input.drift * _Layer2Speed).r * _Layer2Weight;
                        cloud = 1.0h - (1.0h - cloud) * (1.0h - secondary);
                    #endif
                    half fade = smoothstep(0.01, max(_HorizonFade, 0.03), direction.y);
                    half density = saturate(cloud * _CloudDensity) * _CloudOpacity * fade;
                    sky = lerp(sky, _CloudColor.rgb, density);
                #endif
                return half4(sky * _Exposure, 1.0h);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
