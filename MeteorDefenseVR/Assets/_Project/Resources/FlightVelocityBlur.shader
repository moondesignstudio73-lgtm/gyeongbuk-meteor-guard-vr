Shader "MeteorDefenseVR/FlightVelocityBlur"
{
    Properties
    {
        [PerRendererData] _MainTex ("Film", 2D) = "white" {}
        _Strength ("Velocity blur", Range(0,0.04)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" }
        Cull Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; UNITY_VERTEX_OUTPUT_STEREO };
            sampler2D _MainTex;
            float _Strength;
            v2f vert(appdata v) { v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color; return o; }
            fixed4 frag(v2f i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 velocity=(i.uv-.5)*_Strength;
                fixed4 c=tex2D(_MainTex,i.uv);
                c+=tex2D(_MainTex,i.uv-velocity);
                c+=tex2D(_MainTex,i.uv-velocity*2);
                c+=tex2D(_MainTex,i.uv-velocity*3);
                c+=tex2D(_MainTex,i.uv-velocity*4);
                return fixed4(c.rgb*.2,1)*i.color;
            }
            ENDHLSL
        }
    }
}
