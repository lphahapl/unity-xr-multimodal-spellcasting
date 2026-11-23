Shader "NarutoVR/Wrist Gauge Inlay"
{
    Properties
    {
        _Enamel ("嵌色", Color) = (.7,.2,.07,1)
        _CellU ("横坐标", Vector) = (1,0,0,0)
        _CellV ("纵坐标", Vector) = (0,1,0,0)
        _CellState ("显示边缘、数值、增减余光", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "GaugeInlay"
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
                UNITY_DEFINE_INSTANCED_PROP(half4, _Enamel)
                UNITY_DEFINE_INSTANCED_PROP(float4, _CellU)
                UNITY_DEFINE_INSTANCED_PROP(float4, _CellV)
                UNITY_DEFINE_INSTANCED_PROP(float4, _CellState)
            UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 face : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                float4 cellU = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _CellU);
                float4 cellV = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _CellV);
                output.face = float2(dot(cellU, input.positionOS), dot(cellV, input.positionOS));
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = saturate(input.face);
                float4 cellState = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _CellState);
                half3 enamelColor = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Enamel).rgb;
                float edge = cellState.x;
                float gain = max(cellState.z, 0);
                float loss = max(-cellState.z, 0);
                // Three low-frequency scallops break up only the receding rim.
                float scallop = sin(uv.y * 16 + uv.x * 4) * .055 + sin(uv.y * 28) * .019;
                float distanceToEdge = edge - uv.x + scallop * loss;
                float aa = clamp(fwidth(uv.x) * .6, .006, .12);
                float filled = smoothstep(-aa, aa, distanceToEdge);
                if (edge <= 0) filled = 0;
                if (edge >= 1) filled = 1;
                half sheen = .78h + .22h * saturate(dot(normalize(input.normalWS), half3(.28,.88,.38)));
                half3 darkBronze = half3(.11,.071,.038) * sheen;
                half3 enamel = enamelColor * sheen * (.83h + .17h * uv.y);
                half3 color = lerp(darkBronze, enamel, filled);
                float narrow = 1 - smoothstep(.018, .145 + aa, abs(distanceToEdge));
                color += narrow * gain * half3(.95,.7,.32);
                color += narrow * loss * (.45h + .35h * sin(uv.y * 16)) * (enamelColor + half3(.33,.20,.085));
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
