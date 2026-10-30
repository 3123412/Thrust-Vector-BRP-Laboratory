Shader "BRP/Earth"
{
    Properties
    {
        _BaseColor ("Ocean", Color) = (0.02,0.12,0.22,1)
        _MainTex ("NASA Blue Marble", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 normalWS:TEXCOORD0; float3 positionWS:TEXCOORD1; float2 uv:TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };
            float4 _BaseColor;
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            Varyings vert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS=TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS=TransformWorldToHClip(o.positionWS);
                o.normalWS=TransformObjectToWorldNormal(input.normalOS);
                o.uv=input.uv;
                return o;
            }
            float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float noise(float2 p)
            {
                float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);
                return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);
            }
            half4 frag(Varyings i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 n=normalize(i.normalWS);
                float2 uv=i.uv*float2(120,60);
                float3 color=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv).rgb;
                float cloud=smoothstep(.78,.96,noise(uv)+noise(uv*2.1)*.3+noise(uv*4.3)*.15);
                cloud*=smoothstep(.4,.7,noise(i.uv*float2(16,8)));
                color=lerp(color,float3(.82,.9,.92),cloud*.6);
                float light=saturate(dot(n,normalize(float3(-.6,.7,-.5))));
                color*=.13+light*.95;
                float3 view=normalize(_WorldSpaceCameraPos-i.positionWS);
                float rim=pow(1-saturate(dot(n,view)),3);
                color+=float3(.08,.35,.6)*rim*.65;
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
