Shader "NarutoVR/Props/Stake Gauze"
{
    Properties { _Silk ("Silk",Color)=(.80,.77,.62,1) _Hem("Sewn hem",Color)=(.24,.26,.19,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "Gauze"
            Tags { "LightMode"="UniversalForwardOnly" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _Silk, _Hem, _FieldPhase, _Impact;
            CBUFFER_END
            struct App {float3 p:POSITION;float3 n:NORMAL;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct Vary {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;half fold:TEXCOORD1;half wave:TEXCOORD2;float path:TEXCOORD3;UNITY_VERTEX_OUTPUT_STEREO};
            Vary Vert(App a)
            {
                Vary o=(Vary)0;
                UNITY_SETUP_INSTANCE_ID(a);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float2 uv=a.uv;
                float age=_FieldPhase.z;
                float side=(a.n.z>.5)?0:((a.n.x>.5)?1:((a.n.z<-.5)?2:3));
                float path=side+uv.x;
                float swell=sin(path*3.14159+uv.y*1.8-age*1.45);
                float edge=sin(3.14159*uv.x);
                float fold=sin(24.1*uv.x+.58*sin(11.2*uv.x)+.8*uv.y+.55*swell);
                float rippleDist=length((a.p-_Impact.xyz)*float3(1,1.55,1));
                float pulse=exp(-12*abs(rippleDist-_Impact.w*1.55))*saturate(1-_Impact.w*1.8);
                float3 pos=a.p+a.n*edge*(.023*fold+.038*swell+.062*pulse);
                pos.y=(uv.y-(.030+.023*swell)*edge*uv.y)*_FieldPhase.x;
                o.pos=TransformObjectToHClip(pos);o.path=path;o.uv=uv;o.fold=fold;o.wave=pulse;return o;
            }
            half4 Frag(Vary i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 uv=i.uv;
                float2 distanceToEdge=min(uv,1-uv);
                float2 antialias=max(fwidth(uv),.0001);
                float2 seam=1-smoothstep(float2(.004,.009)-antialias,float2(.004,.009)+antialias,distanceToEdge);
                half hem=max(seam.x,seam.y);
                float2 grid=uv*float2(410,274);
                // Fade individual threads before subpixel frequencies alias under stereo head movement.
                float2 resolved=saturate(1-2.2*fwidth(grid));
                float2 yarn=.5+.5*cos(grid*6.28318);
                half weave=dot((yarn-.5)*resolved,float2(.075,.075));
                half scallop=0.5h+0.5h*i.fold;
                half3 silk=_Silk.rgb*(.75h+.20h*scallop+weave)+half3(.09,.067,.014)*i.wave;
                half opacity=.22h+.115h*scallop+weave+i.wave*.12h;
                float runner=frac(i.path*.5-_FieldPhase.z*.24-uv.y*.035);
                float glint=saturate(1-min(runner,1-runner)*11);
                glint=glint*glint*(3-2*glint);
                half piping=1-smoothstep(.012,.024,distanceToEdge.y);
                half gleam=glint*(piping*.86h + (1-hem)*scallop*.18h);
                half3 dyed=lerp(silk,_Hem.rgb,hem*.88h);
                half3 woven=lerp(dyed,half3(.98,.67,.25),saturate(gleam));
                return half4(woven,saturate(lerp(opacity,.74h,hem)+gleam*.26h)*_FieldPhase.y);
            }
            ENDHLSL
        }
    }
}
