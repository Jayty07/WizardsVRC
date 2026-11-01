// The turn clock set into the middle of the duel circle (Textures/DuelClock.png, from Tools/make_duel_clock.py).
// The dial's art comes from the texture; the track round its edge is drawn here: 40 rune segments that stay lit for the
// time left to choose spells and go dark clockwise from the top as it runs out. With little time left the lit segments
// turn from gold to red and pulse. _Remaining 0..1 (BattleClient sets it on its own copy of the material).
// Unlit and cut out like the rest of the circle; fogged.
Shader "WizardsVRC/DuelClock"
{
    Properties
    {
        _MainTex ("Dial (RGBA)", 2D) = "black" {}
        _Remaining ("Time left", Range(0, 1)) = 1
        _LitColor ("Lit segments", Color) = (1, 0.8, 0.3, 1)
        _WarnColor ("Running out", Color) = (1, 0.25, 0.15, 1)
        _DimColor ("Spent segments", Color) = (0.16, 0.13, 0.24, 1)
        _Track ("Track inner, outer radius (UV)", Vector) = (0.78, 0.93, 0, 0)
        _Segments ("Segments", Float) = 40
        _Glow ("Glow", Float) = 1.6
    }
    SubShader
    {
        Tags { "Queue"="AlphaTest+5" "RenderType"="TransparentCutout" "IgnoreProjector"="True" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Remaining;
            fixed4 _LitColor, _WarnColor, _DimColor;
            float4 _Track;
            float _Segments;
            float _Glow;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; UNITY_FOG_COORDS(1) UNITY_VERTEX_OUTPUT_STEREO };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                float2 p = i.uv * 2 - 1;
                float r = length(p);
                if (r > _Track.x && r < _Track.y)
                {
                    // Angle clockwise from the top (texture up = the ring's +Z), 0..1.
                    float a = frac(atan2(p.x, p.y) / 6.2831853 + 1);
                    float seg = a * _Segments;
                    float gap = frac(seg);
                    float band = (r - _Track.x) / (_Track.y - _Track.x);
                    // Each segment is a little rune tile with a gap either side and a bevel (lighter outer edge).
                    if (gap > 0.12 && gap < 0.88 && band > 0.08 && band < 0.92)
                    {
                        bool lit = (floor(seg) + 0.5) / _Segments >= 1 - _Remaining;
                        float warn = saturate((0.3 - _Remaining) / 0.25);
                        float pulse = 0.75 + 0.25 * sin(_Time.y * 9);
                        fixed3 litCol = lerp(_LitColor.rgb, _WarnColor.rgb, warn) * _Glow * lerp(1, pulse, warn);
                        fixed3 c = lit ? litCol : _DimColor.rgb;
                        c *= 0.8 + 0.35 * band;
                        // The segment about to go dark flickers.
                        float edge = floor((1 - _Remaining) * _Segments);
                        if (lit && floor(seg) == edge && _Remaining < 1) c *= 0.7 + 0.3 * sin(_Time.y * 20);
                        col = fixed4(c, 1);
                    }
                    else col = fixed4(_DimColor.rgb * 0.45, 1);
                }
                clip(col.a - 0.5);
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
    Fallback "Unlit/Transparent Cutout"
}
