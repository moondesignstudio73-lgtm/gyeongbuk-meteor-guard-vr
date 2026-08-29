Shader "MeteorDefense/Fractured Glass"
{
    Properties { [HDR]_BaseColor("Fracture tint",Color)=(.65,.86,1,.82) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+12" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            CBUFFER_END
            struct A {float4 positionOS:POSITION;float3 normal:NORMAL;float4 tangent:TANGENT;float2 uv:TEXCOORD0;float4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V {float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;float3 tangent:TEXCOORD2;float2 uv:TEXCOORD3;float4 color:COLOR;UNITY_VERTEX_OUTPUT_STEREO};
            V vert(A v)
            {
                V o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.world=TransformObjectToWorld(v.positionOS.xyz);
                o.normal=TransformObjectToWorldNormal(v.normal);o.tangent=TransformObjectToWorldDir(v.tangent.xyz);
                o.uv=v.uv;o.color=v.color;return o;
            }
            half4 frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                // Analytic crack normal: a bevel on each thin ribbon, not a scene-color copy.
                // Facets have low vertex alpha and real small depth offsets. No per-eye refraction RT.
                float ridge=i.uv.y*2-1;
                float3 across=normalize(cross(i.normal,i.tangent)+float3(.0001,0,0));
                float3 bevel=normalize(i.normal+across*ridge*1.4);
                float3 view=GetWorldSpaceNormalizeViewDir(i.world);
                float glint=pow(saturate(dot(reflect(-normalize(float3(-.4,.7,-1)),bevel),view)),18);
                float rim=pow(1-saturate(abs(dot(view,bevel))),2);
                float edge=smoothstep(.4,.95,abs(ridge));
                float bright=smoothstep(-.3,.75,ridge);
                float3 tint=lerp(float3(.045,.095,.13),_BaseColor.rgb*1.6,bright);
                tint+=glint*.85+rim*_BaseColor.rgb*.5;
                return half4(tint,_BaseColor.a*i.color.a*lerp(.52,1,edge));
            }
            ENDHLSL
        }
    }
}
