Shader "MeteorDefense/Energy Shield"
{
    Properties
    {
        [HDR] _BaseColor("Shield Color", Color) = (0.04, 0.62, 1.0, 1.0)
        _BaseAlpha("Interior Alpha", Range(0, 0.12)) = 0.004
        _GridStrength("Grid Strength", Range(0, 1)) = 0.22
        _RimStrength("Rim Strength", Range(0, 1)) = 0.30
        _RimPower("Rim Power", Range(1, 8)) = 3.8
        _HitPulse("Hit Pulse", Range(0, 1)) = 0
        _HitPoint("Hit Point", Vector) = (0, 0, 0, 0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+40" "RenderType"="Transparent" }
        Pass
        {
            Name "EnergyShield"
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float4 _HitPoint;
            float _BaseAlpha;
            float _GridStrength;
            float _RimStrength;
            float _RimPower;
            float _HitPulse;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 viewDir = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float fresnel = pow(saturate(1.0 - abs(dot(normalize(input.normalWS), viewDir))), _RimPower);

                float2 latticeUv = input.uv * float2(40.0, 20.0);
                latticeUv.x += _Time.y * 0.055;
                float2 cell = abs(frac(latticeUv) - 0.5);
                float hexDistance = max(cell.x * 0.866025 + cell.y * 0.5, cell.y);
                float lattice = smoothstep(0.405, 0.485, hexDistance);
                float latticePulse = 0.72 + 0.28 * sin(_Time.y * 2.1 + input.positionWS.y * 1.4);

                float scanPhase = abs(frac(input.uv.y * 4.0 - _Time.y * 0.16) - 0.5) * 2.0;
                float scan = pow(saturate(1.0 - scanPhase), 18.0);

                float hitRadius = (1.0 - _HitPulse) * 4.6;
                float hitDistance = distance(input.positionWS, _HitPoint.xyz);
                float hitRing = exp(-pow((hitDistance - hitRadius) * 2.7, 2.0)) * _HitPulse;
                float hitWash = _HitPulse * _HitPulse * 0.055;

                float alpha = _BaseAlpha;
                alpha += fresnel * _RimStrength;
                float centerClarity = lerp(0.20, 1.0, fresnel);
                alpha += lattice * _GridStrength * 0.065 * latticePulse * centerClarity;
                alpha += scan * 0.012;
                alpha += hitRing * 0.30 + hitWash;
                alpha = saturate(alpha) * _BaseColor.a;

                float3 color = _BaseColor.rgb * (1.05 + fresnel * 1.85 + lattice * 0.42);
                color = lerp(color, float3(0.72, 0.96, 1.0) * 2.1, saturate(hitRing + hitWash * 4.0));
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
