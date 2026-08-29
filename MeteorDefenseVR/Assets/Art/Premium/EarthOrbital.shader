Shader "MeteorDefense/Orbital Earth"
{
    Properties
    {
        _BaseMap("Earth",2D)="white"{}
        _DayTint("Day Tint",Color)=(0.82,0.92,1,1)
        _NightColor("City Lights",Color)=(1.7,0.72,0.18,1)
        _AtmosphereColor("Atmosphere",Color)=(0.06,0.45,1,1)
        _CloudStrength("Cloud Detail",Range(0,1))=.28
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST,_DayTint,_NightColor,_AtmosphereColor; float _CloudStrength;
            CBUFFER_END
            struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; float3 normal:TEXCOORD1; float2 uv:TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };
            V vert(A v){V o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.world=TransformObjectToWorld(v.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);o.normal=TransformObjectToWorldNormal(v.normalOS);o.uv=TRANSFORM_TEX(v.uv,_BaseMap);return o;}
            half4 frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 n=normalize(i.normal), view=normalize(GetWorldSpaceViewDir(i.world)); Light key=GetMainLight();
                float sun=saturate(dot(n,key.direction)*1.45+.08), twilight=smoothstep(0,.18,sun);
                half3 map=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb;
                float land=saturate(map.r-map.b*.35), city=pow(saturate(land-map.g*.22),3)*pow(1-sun,5);
                float cloud=(sin(i.uv.x*76+sin(i.uv.y*41))*sin(i.uv.y*93+i.uv.x*11)*.5+.5)*_CloudStrength;
                cloud*=smoothstep(.38,.76,map.r+map.g);
                float rim=pow(1-saturate(dot(n,view)),4);
                half3 color=map*_DayTint.rgb*(.045+sun*.98)+cloud*twilight;
                color+=city*_NightColor.rgb+rim*_AtmosphereColor.rgb*1.15;
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
