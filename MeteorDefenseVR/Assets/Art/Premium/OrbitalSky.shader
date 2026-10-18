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
            float noise(float2 p)
            {
                float2 cell=floor(p),f=frac(p);f=f*f*(3-2*f);
                return lerp(lerp(hash(cell),hash(cell+float2(1,0)),f.x),lerp(hash(cell+float2(0,1)),hash(cell+1),f.x),f.y);
            }
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
                float3 panorama=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv).rgb;
                float cloud=noise(uv*float2(7,4))+noise(uv*float2(15,8)+17)*.42;
                float band=smoothstep(.62,1.12,cloud)*(.35+.65*smoothstep(.04,.32,abs(d.y)));
                float3 nebula=panorama*.042+band*float3(.0035,.009,.016);
                float fine=stars(uv,380,.985),hero=stars(uv,270,.996)*.7;
                float warmth=hash(floor(uv*270)+41);
                float3 starTint=lerp(float3(.47,.72,1),float3(1,.78,.58),smoothstep(.84,1,warmth));
                return half4(nebula+float3(.55,.73,1)*fine+starTint*hero+float3(.0007,.0015,.003),1);
            }
            ENDHLSL
        }
    }
}
