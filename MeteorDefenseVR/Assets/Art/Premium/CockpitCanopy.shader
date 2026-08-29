Shader "MeteorDefense/Cockpit Canopy"
{
    Properties
    {
        _Tint("Glass Tint", Color) = (0.035,0.16,0.2,0.12)
        _EdgeColor("Edge Reflection", Color) = (0.08,0.75,1,0.5)
        _Dust("Dust", Range(0,1)) = 0.16
        _Scratch("Micro Scratches", Range(0,1)) = 0.2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+15" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _Tint, _EdgeColor; float _Dust, _Scratch;
            CBUFFER_END
            struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; float3 normal:TEXCOORD1; float2 uv:TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };
            V vert(A v) { V o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.world=TransformObjectToWorld(v.positionOS.xyz); o.positionCS=TransformWorldToHClip(o.world); o.normal=TransformObjectToWorldNormal(v.normalOS); o.uv=v.uv; return o; }
            float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            half4 frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 n=normalize(i.normal), v=normalize(GetWorldSpaceViewDir(i.world));
                float fresnel=pow(1-saturate(abs(dot(n,v))),3.2);
                float2 cell=floor(i.uv*420); float dust=step(.992,hash(cell))*_Dust;
                float scratches=smoothstep(.985,1,sin((i.uv.x*1.7+i.uv.y)*900+hash(floor(i.uv*32))*9))
                    * smoothstep(.65,1,hash(floor(i.uv*90)))*_Scratch;
                float alpha=saturate(_Tint.a+fresnel*_EdgeColor.a+dust*.12+scratches*.035);
                float3 color=_Tint.rgb+_EdgeColor.rgb*(fresnel*.55+dust*.08+scratches*.045);
                return half4(color,alpha);
            }
            ENDHLSL
        }
    }
}
