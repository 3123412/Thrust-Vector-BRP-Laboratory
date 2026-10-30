Shader "BRP/Exhaust"
{
    Properties
    {
        _BaseColor ("Tint", Color) = (1,1,1,1)
        _Softness ("Softness", Range(0.1,5)) = 1.5
        _Turbulence ("Turbulence", Range(0,1)) = 0.2
        _SrcBlend ("Source", Float) = 5
        _DstBlend ("Destination", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend [_SrcBlend] [_DstBlend]
        ZWrite Off
        Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION;float2 uv:TEXCOORD0;half4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;half4 color:COLOR; UNITY_VERTEX_OUTPUT_STEREO };
            half4 _BaseColor;
            float _Softness,_Turbulence;
            Varyings vert(Attributes v)
            {
                Varyings o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.uv=v.uv;o.color=v.color;return o;
            }
            float hash(float2 p) {return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float noise(float2 p)
            {
                float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);
                return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);
            }
            half4 frag(Varyings i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 p=i.uv*2-1;
                float turbulence=noise(i.uv*6+float2(_Time.y*.8,-_Time.y*2));
                float radius=length(p)+_Turbulence*(turbulence-.5);
                float mask=pow(saturate(1-radius),_Softness)*smoothstep(1,.74,radius);
                half4 color=i.color*_BaseColor;color.a*=mask;
                return color;
            }
            ENDHLSL
        }
    }
}
