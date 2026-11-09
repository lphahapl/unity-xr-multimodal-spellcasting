Shader "NarutoVR/技能能量叠加"
{
    Properties
    {
        [HDR] _BaseColor ("能量颜色", Color) = (1, 0.3, 0.04, 1)
        _Opacity ("透明度", Range(0,1)) = 1
        _VisualOpacity ("动画透明度", Range(0,1)) = 1
        _Style ("样式：线0／流壳1／粒子2", Float) = 0
        _FlowSpeed ("流动速度", Float) = 2
        _BandScale ("流动纹路密度", Float) = 7
        _RimPower ("边缘锐度", Range(0.2,8)) = 2
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "能量叠加"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _Opacity, _VisualOpacity, _Style, _FlowSpeed, _BandScale, _RimPower;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                half4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float alpha = _Opacity * _VisualOpacity * input.color.a * _BaseColor.a;
                float energy = 1;
                if (_Style > 1.5)
                {
                    float radius = length(input.uv * 2 - 1);
                    alpha *= pow(saturate(1 - radius), 1.5);
                    energy = 1 + saturate(1 - radius * 3);
                }
                else if (_Style > .5)
                {
                    float rim = pow(saturate(1 - abs(dot(normalize(input.normalWS), GetWorldSpaceNormalizeViewDir(input.positionWS)))), _RimPower);
                    float t = _Time.y * _FlowSpeed;
                    float twist = sin(input.uv.y * 13 + t) * 1.7;
                    float bands = pow(saturate(.5 + .5 * sin(input.uv.x * _BandScale * 6.28318 + twist - t * 1.8)), 4);
                    float eddies = .5 + .5 * sin(input.uv.y * 19 - t * 2 + sin(input.uv.x * 25 + t));
                    alpha *= saturate(.08 + rim * .6 + bands * eddies * .7);
                    energy = 1 + bands * .5;
                }
                return half4(_BaseColor.rgb * input.color.rgb * energy, alpha);
            }
            ENDHLSL
        }
    }
}
