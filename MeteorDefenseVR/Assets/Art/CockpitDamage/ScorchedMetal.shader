Shader "MeteorDefense/Scorched Metal"
{
    Properties { _Severity("Damage",Range(0,1))=.33 _Heat("Residual heat",Range(0,1))=0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+8" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off Offset -1,-1
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float _Severity,_Heat;
            CBUFFER_END
            struct A {float4 positionOS:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
            V vert(A v){V o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.uv=v.uv;return o;}
            half4 frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 p=i.uv*2-1;
                float grain=frac(sin(dot(floor(p*70),float2(12.9898,78.233)))*43758.5453);
                float rough=sin(p.x*19+p.y*15)*sin(p.y*29-p.x*9)*.07+sin(p.x*5-p.y*11)*.10;
                float r=length(p)+rough;
                float outer=lerp(.42,.94,_Severity);
                float soot=1-smoothstep(outer*.55,outer,r);
                // Small broken hot spots, not a neon outline around the soot.
                float ember=exp(-abs(r-outer*.65)*35)*_Heat*smoothstep(.72,1,grain)*.24;
                float3 metal=float3(.009,.012,.015)+grain*.022;
                return half4(metal+float3(2.6,.35,.035)*ember,soot*.92);
            }
            ENDHLSL
        }
    }
}
