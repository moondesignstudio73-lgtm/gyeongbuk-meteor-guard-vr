Shader "MeteorDefense/Explosion Flipbook"
{
    Properties { _BaseMap("Fire Atlas",2D)="black"{} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        Pass
        {
            Blend One OneMinusSrcAlpha ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
            struct A{float4 positionOS:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V{float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;UNITY_VERTEX_OUTPUT_STEREO};
            V vert(A v){V o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.uv=v.uv;o.color=v.color;return o;}
            half4 frag(V i):SV_Target{UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);float2 local=frac(i.uv*4);float mask=smoothstep(0,.055,min(min(local.x,1-local.x),min(local.y,1-local.y)));half3 tex=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb;float coverage=saturate(max(tex.r,max(tex.g,tex.b))*5);return half4(tex*i.color.rgb*i.color.a*mask,coverage*i.color.a*mask);}
            ENDHLSL
        }
    }
}
