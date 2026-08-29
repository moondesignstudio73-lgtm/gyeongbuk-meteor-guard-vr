Shader "MeteorDefense/Orbital Sky"
{
    Properties { _MainTex("Nebula Panorama",2D)="black"{} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
            struct A{float4 positionOS:POSITION;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V{float4 positionCS:SV_POSITION;float3 dir:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
            V vert(A v){V o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.dir=v.positionOS.xyz;return o;}
            float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float stars(float2 uv,float grid,float threshold)
            {
                float2 cell=floor(uv*grid),f=frac(uv*grid);float seed=hash(cell);float2 pos=float2(hash(cell+9),hash(cell+27))*.6+.2;
                float radius=lerp(.024,.09,hash(cell+81));float d=length(f-pos);float aa=max(fwidth(d),.007);
                return step(threshold,seed)*(1-smoothstep(radius-aa,radius+aa,d))*lerp(.25,1.6,hash(cell+53));
            }
            half4 frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);float3 d=normalize(i.dir);
                float2 uv=float2(atan2(d.z,d.x)/6.2831853+.5,asin(d.y)/3.14159265+.5);
                float3 nebula=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv).rgb*.035;
                float s=stars(uv,380,.985)+stars(uv,270,.996)*.7;
                return half4(nebula+float3(.58,.76,1)*s+float3(.0007,.0015,.003),1);
            }
            ENDHLSL
        }
    }
}
