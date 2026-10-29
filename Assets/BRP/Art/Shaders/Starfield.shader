Shader "BRP/Starfield"
{
    Properties { _Horizon("Horizon",Color)=(.035,.07,.1,1) }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes {float4 positionOS:POSITION;};
            struct Varyings {float4 positionCS:SV_POSITION;float3 direction:TEXCOORD0;};
            float4 _Horizon;
            Varyings vert(Attributes i) {Varyings o;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.direction=i.positionOS.xyz;return o;}
            float hash(float3 p) {return frac(sin(dot(p,float3(127.1,311.7,74.7)))*43758.5453);}
            half4 frag(Varyings i):SV_Target
            {
                float3 d=normalize(i.direction);
                float3 grid=d*420;float3 cell=floor(grid);float star=step(.9975,hash(cell));
                float spot=pow(saturate(1-length(frac(grid)-.5)*2),7)*star;
                float horizon=pow(saturate(1-abs(d.y)),5);
                float3 color=lerp(float3(.004,.008,.018),_Horizon.rgb,horizon);
                color+=spot*float3(.75,.86,1)*2.5;
                float sun=pow(saturate(dot(d,normalize(float3(-.5,.08,.8)))),1400);
                color+=sun*float3(1,.7,.35)*2;
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
