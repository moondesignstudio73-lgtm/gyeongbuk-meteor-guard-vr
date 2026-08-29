Shader "MeteorDefense/Soft Energy"
{
    Properties { [HDR]_BaseColor("Color",Color)=(1,1,1,1) _Ring("Ring",Range(0,1))=0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;float _Ring;
            CBUFFER_END
            struct A {float4 positionOS:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;UNITY_VERTEX_OUTPUT_STEREO};
            V vert(A v){V o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.uv=v.uv;o.color=v.color*_BaseColor;return o;}
            half4 frag(V i):SV_Target{UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);float r=length(i.uv*2-1);float core=pow(saturate(1-r),2);float ring=exp(-pow((r-.78)*25,2));float alpha=_Ring<0?pow(saturate(1-abs(i.uv.y*2-1)),2)*(.8+.2*sin(i.uv.x*45-_Time.y*80)):lerp(core,ring,_Ring);return half4(i.color.rgb,i.color.a*alpha);}
            ENDHLSL
        }
    }
}
