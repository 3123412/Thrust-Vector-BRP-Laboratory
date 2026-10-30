Shader "BRP/Starfield"
{
    Properties { _Horizon("Horizon",Color)=(.035,.07,.1,1) _Atmosphere("Atmosphere",Range(0,1))=1 }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes {float4 positionOS:POSITION;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct Varyings {float4 positionCS:SV_POSITION;float3 direction:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
            float4 _Horizon;
            float _Atmosphere;
            Varyings vert(Attributes i) {Varyings o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.direction=i.positionOS.xyz;return o;}
            float hash(float3 p) {return frac(sin(dot(p,float3(127.1,311.7,74.7)))*43758.5453);}
            half4 frag(Varyings i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 d=normalize(i.direction);
                float3 grid=d*420;float3 cell=floor(grid);float star=step(.9975,hash(cell));
                float spot=pow(saturate(1-length(frac(grid)-.5)*2),7)*star;
                float horizon=pow(saturate(1-abs(d.y)),5);
                float3 color=lerp(float3(.004,.008,.018),_Horizon.rgb,horizon);
                color+=spot*float3(.75,.86,1)*2.5;
                float sun=pow(saturate(dot(d,normalize(float3(-.5,.08,.8)))),1400);
                color+=sun*float3(1,.7,.35)*2;
                float3 air=lerp(float3(.035,.09,.17),float3(.25,.37,.45),horizon);
                if(d.y<0) air=lerp(float3(.025,.045,.055),float3(.25,.37,.45),pow(saturate(1+d.y),12));
                color=lerp(color,air+sun*float3(1,.73,.4)*2,_Atmosphere);
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
