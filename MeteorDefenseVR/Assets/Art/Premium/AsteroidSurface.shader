Shader "MeteorDefense/Premium Asteroid"
{
    Properties
    {
        _BaseMap("Basalt",2D)="white"{}
        _BaseColor("Tint",Color)=(1,1,1,1)
        _EmissionColor("Heat",Color)=(0,0,0,1)
        _Heat("Fissures",Float)=0
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
            float4 _BaseColor, _EmissionColor;float _Heat;
            CBUFFER_END
            struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V {float4 positionCS:SV_POSITION;float3 local:TEXCOORD0;float3 world:TEXCOORD1;float3 normal:TEXCOORD2;UNITY_VERTEX_OUTPUT_STEREO};
            V vert(A v){V o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.local=v.positionOS.xyz;o.world=TransformObjectToWorld(v.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);o.normal=TransformObjectToWorldNormal(v.normalOS);return o;}
            float hash(float3 p){return frac(sin(dot(p,float3(17.13,127.1,311.7)))*43758.5453);}
            float fissure(float3 p)
            {
                p+=sin(p.yzx*3.7)*.32+sin(p.zxy*9.1)*.075;
                float3 c=floor(p),f=frac(p);float d1=10,d2=10;
                [unroll]for(int x=-1;x<=1;x++)[unroll]for(int y=-1;y<=1;y++)[unroll]for(int z=-1;z<=1;z++)
                {float3 g=float3(x,y,z);float3 q=g+float3(hash(c+g),hash(c+g+19),hash(c+g+37))-f;float d=dot(q,q);if(d<d1){d2=d1;d1=d;}else d2=min(d2,d);}
                return 1-smoothstep(.025,.09,d2-d1);
            }
            half4 frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 n=normalize(i.normal),w=pow(abs(n),4);w/=max(dot(w,1),.001);
                float3 p=i.local*2.7;
                half3 tex=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,p.yz).rgb*w.x+SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,p.xz).rgb*w.y+SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,p.xy).rgb*w.z;
                // Screen-space surface gradient from the actual rock albedo gives fine relief without a second 2K map.
                float h=dot(tex,float3(.3,.59,.11));float3 dpdx=ddx(i.world),dpdy=ddy(i.world);
                float3 r1=cross(dpdy,n),r2=cross(n,dpdx);float det=dot(dpdx,r1);
                n=normalize(abs(det)*n-sign(det)*(ddx(h)*r1+ddy(h)*r2)*.018);
                Light key=GetMainLight();float lit=saturate(dot(n,key.direction));
                half3 color=tex*_BaseColor.rgb*(half3(.085,.11,.15)+key.color*(lit*.95+.05));
                float rim=pow(1-saturate(dot(n,normalize(GetWorldSpaceViewDir(i.world)))),3);
                color+=rim*half3(.025,.055,.075);
                if(_Heat>.01){float crack=fissure(i.local*6.2);color*=1-crack*.55;color+=crack*_EmissionColor.rgb*3.5*_Heat;}
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
