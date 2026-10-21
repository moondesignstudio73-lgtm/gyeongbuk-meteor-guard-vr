Shader "MeteorDefenseVR/DamagedRockSurface"
{
    Properties { _Damage("Damage fraction", Range(0,1)) = 0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry+10" "RenderType"="TransparentCutout" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            ZWrite Off ZTest LEqual Cull Back
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Damage;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 rock:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes input)
            {
                Varyings o;UNITY_SETUP_INSTANCE_ID(input);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                // Conform to the original mesh: no billboard or screen-space halo.
                o.positionCS=TransformObjectToHClip(input.positionOS.xyz+input.normalOS*.0015);
                o.rock=input.positionOS.xyz*6;
                return o;
            }
            half4 Frag(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                clip(_Damage-.0001);
                float3 p=input.rock;
                float a=abs(p.x+.32*sin(p.y*2.7)+.12*sin(p.y*9+p.z*2)-.22);
                float b=abs(p.y+.55*p.z+.24*sin(p.x*4)+.1*sin(p.z*11));
                float branch=abs(p.x+p.y*.7+.18*sin(p.z*7)-1.1);
                float seam=min(a,b);
                if(_Damage>.5)seam=min(seam,branch);
                float width=lerp(.12,.22,_Damage);
                float aa=max(fwidth(seam)*.65,.012);
                float rim=1-smoothstep(width,width+aa,seam);
                clip(rim-.02);
                float core=1-smoothstep(width*.35,width*.6+aa,seam);
                // Thin warm exposed fracture with a dark rim; rock remains the main silhouette.
                half3 color=lerp(half3(.018,.011,.009),half3(.70,.30,.12),core);
                return half4(color,rim);
            }
            ENDHLSL
        }
    }
}
