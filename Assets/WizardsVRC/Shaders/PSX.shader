// PSX-style lit shader: vertex snapping, partial affine texture warping,
// per-vertex lighting, point-filtered textures, fog. Built-in pipeline, VR-safe.
Shader "WizardsVRC/PSX"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Texture", 2D) = "white" {}
        _EmissionColor ("Emission", Color) = (0,0,0,0)
        _SnapRes ("Vertex Snap Resolution (0 = off)", Float) = 900
        _Affine ("Affine Warp (0-1)", Range(0,1)) = 0.2
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _EmissionColor;
            float _SnapRes;
            float _Affine;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 affineUv : TEXCOORD1;
                fixed3 light : COLOR0;
                UNITY_FOG_COORDS(2)
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float4 clip = UnityObjectToClipPos(v.vertex);
                float w = max(clip.w, 0.0001);
                if (_SnapRes > 0)
                    clip.xy = round(clip.xy / w * _SnapRes) / _SnapRes * w;
                o.pos = clip;
                float2 uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.uv = uv;
                o.affineUv = float3(uv * w, w);
                float3 n = UnityObjectToWorldNormal(v.normal);
                float ndl = max(0, dot(n, normalize(_WorldSpaceLightPos0.xyz)));
                o.light = ShadeSH9(float4(n, 1)) + _LightColor0.rgb * ndl;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = lerp(i.uv, i.affineUv.xy / i.affineUv.z, _Affine);
                fixed4 col = tex2D(_MainTex, uv) * _Color;
                col.rgb = col.rgb * i.light + _EmissionColor.rgb;
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
    Fallback "VertexLit"
}
