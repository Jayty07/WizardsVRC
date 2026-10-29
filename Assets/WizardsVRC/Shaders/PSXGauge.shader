// Segmented circular gauge for the duel-circle seats (health). The mask texture (Tools/make_seat_gauge.py) holds the
// band (red 255) and its dark frame and gaps (red 128). The band fills clockwise from the top of the texture:
// _Fill in _FillColor, then health just lost (_Fill .. _Trail) in _TrailColor, the rest in _EmptyColor. Fill snaps
// to whole segments (any health left shows at least one). Unlit, point-sampled cutout with fog.
Shader "WizardsVRC/PSXGauge"
{
    Properties
    {
        _MainTex ("Gauge Mask", 2D) = "white" {}
        _Fill ("Fill (0-1)", Range(0, 1)) = 1
        _Trail ("Recently Lost (0-1, >= Fill)", Range(0, 1)) = 1
        _Segments ("Segments (match the mask)", Float) = 24
        _FillColor ("Fill", Color) = (0.25, 0.9, 0.35, 1)
        _TrailColor ("Recently Lost", Color) = (1, 0.94, 0.78, 1)
        _EmptyColor ("Empty", Color) = (0.13, 0.12, 0.17, 1)
        _FrameColor ("Frame", Color) = (0.05, 0.04, 0.08, 1)
    }
    SubShader
    {
        Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" "IgnoreProjector"="True" }
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            float _Fill;
            float _Trail;
            float _Segments;
            fixed4 _FillColor;
            fixed4 _TrailColor;
            fixed4 _EmptyColor;
            fixed4 _FrameColor;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 m = tex2D(_MainTex, i.uv);
                clip(m.a - 0.5);
                // Angle of this texel's centre: 0 at the top, increasing clockwise (as seen from above).
                float2 size = max(_MainTex_TexelSize.zw, float2(1, 1));
                float2 c = (floor(i.uv * size) + 0.5) / size - 0.5;
                float a = atan2(c.x, c.y) / 6.28318530718;
                a = a < 0 ? a + 1 : a;
                float seg = max(_Segments, 1);
                float fill = ceil(saturate(_Fill) * seg - 0.001) / seg;
                float trail = ceil(saturate(max(_Trail, _Fill)) * seg - 0.001) / seg;
                fixed4 col = m.r > 0.75 ? (a < fill ? _FillColor : (a < trail ? _TrailColor : _EmptyColor)) : _FrameColor;
                col.a = 1;
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
    Fallback Off
}
