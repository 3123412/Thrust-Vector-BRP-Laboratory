Shader "BRP/Plume"
{
    Properties
    {
        _Power ("Power", Range(0,1)) = 1
        _BaseColor ("Color",Color) = (3,1.4,.35,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes {float4 positionOS:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct Varyings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
            float _Power;
            half4 _BaseColor;
            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 p=input.positionOS.xyz;
                float wave=sin(p.y*18+_Time.y*35)*sin(p.y*31-_Time.y*52);
                p.xz*=1+wave*.09*input.uv.y;
                p.y*=.2+.8*_Power;
                output.positionCS=TransformObjectToHClip(p);output.uv=input.uv;return output;
            }
            half4 frag(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float t=input.uv.y;
                float diamonds=.8+.2*cos(t*65-_Time.y*16);
                half3 color=lerp(half3(3.8,3.5,2.8),_BaseColor.rgb,saturate(t*3));
                float alpha=pow(saturate(1-t),1.5)*smoothstep(0,.04,t)*_Power*diamonds;
                return half4(color,alpha*.65);
            }
            ENDHLSL
        }
    }
}
