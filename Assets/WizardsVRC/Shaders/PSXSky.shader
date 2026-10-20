// Three-colour gradient skybox (top / horizon / bottom) for the PSX look.
Shader "WizardsVRC/PSXSky"
{
    Properties
    {
        _TopColor ("Top", Color) = (0.25,0.35,0.85,1)
        _HorizonColor ("Horizon", Color) = (1,0.6,0.55,1)
        _BottomColor ("Bottom", Color) = (0.45,0.3,0.6,1)
        _Sharpness ("Horizon Sharpness", Float) = 1.6
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _TopColor, _HorizonColor, _BottomColor;
            float _Sharpness;
            struct appdata { float4 vertex : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                float y = normalize(i.dir).y;
                float up = pow(saturate(y), 1.0 / _Sharpness);
                float down = pow(saturate(-y), 1.0 / _Sharpness);
                fixed4 c = lerp(_HorizonColor, _TopColor, up);
                return lerp(c, _BottomColor, down);
            }
            ENDCG
        }
    }
}
