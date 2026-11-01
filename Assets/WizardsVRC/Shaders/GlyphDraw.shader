// A school's symbol drawn in the air with arcane energy (the duel cast effect).
// _MainTex is a data map from Tools/make_glyph_draw.py, read point-filtered and linear (not as colour):
//   R = when the pen passes this pixel (0..1), G = bright core (1) or glow (lower), A = ink.
// _Reveal runs 0 -> 1 as the caster draws: pixels the pen has passed show, with a hot white-ish tip just behind the pen.
// _Fade dims the whole symbol (it fades as the spell lands, or sputters out on a fizzle); _Flare brightens it when the
// spell is released. Additive, so it reads as light. _Style gives each school its own kind of energy:
//   0 General  steady arcane glow           4 Life     slow, soft breathing
//   1 Fire     flickering flame              5 Death    dim, with ghostly wisps crawling up it
//   2 Ice      frosty, glinting sparkles     6 Myth     a golden shimmer sweeping across
//   3 Storm    crackling, jittering bolts    7 Balance  even, gently pulsing
Shader "WizardsVRC/GlyphDraw"
{
    Properties
    {
        _MainTex ("Order (R) glow (G) ink (A)", 2D) = "black" {}
        _Color ("Energy", Color) = (1, 0.5, 0.2, 1)
        _EdgeColor ("Pen tip", Color) = (1, 0.95, 0.8, 1)
        _Reveal ("Reveal", Range(0, 1.05)) = 1
        _Fade ("Fade", Range(0, 1)) = 1
        _Flare ("Flare", Range(0, 3)) = 0
        _Style ("Style", Float) = 0
        _Intensity ("Intensity", Float) = 1.3
    }
    SubShader
    {
        Tags { "Queue"="Transparent+20" "RenderType"="Transparent" "IgnoreProjector"="True" "DisableBatching"="True" }
        Blend One One
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            fixed4 _EdgeColor;
            float _Reveal;
            float _Fade;
            float _Flare;
            float _Style;
            float _Intensity;

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
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float time = _Time.y;
                int style = (int)round(_Style);
                float2 uv = i.uv;
                // Storm: the strokes jitter sideways in little jumps, a few times a second, row by row.
                if (style == 3)
                {
                    float jump = floor(time * 14.0);
                    uv.x += (Hash(float2(jump, floor(uv.y * 16.0))) - 0.5) * 0.025;
                }
                // Point sampling on the texel grid (the data must not be blended between pixels).
                float2 texel = (floor(uv * _MainTex_TexelSize.zw) + 0.5) * _MainTex_TexelSize.xy;
                float4 d = tex2D(_MainTex, texel);
                if (d.a < 0.5) discard;
                float order = d.r;
                if (order > _Reveal) discard;

                float age = _Reveal - order;
                float tip = saturate(1.0 - age / 0.06);
                float glow = d.g;
                float3 col = lerp(_Color.rgb, _EdgeColor.rgb, tip);
                float b = glow * (1.0 + 1.6 * tip);
                float2 cell = floor(uv * 64.0);
                float tick = floor(time * 10.0);

                if (style == 1)        // fire: flicker, brighter licks running up the strokes
                {
                    b *= 0.75 + 0.3 * sin(time * 23.0 + order * 31.0 + uv.y * 9.0) + 0.25 * Hash(cell + tick);
                    col = lerp(col, float3(1.0, 0.85, 0.35), 0.35 * saturate(sin(uv.y * 20.0 - time * 9.0)));
                }
                else if (style == 2)   // ice: cold glints that twinkle on and off
                {
                    if (Hash(cell + floor(time * 7.0)) > 0.94) { col = float3(1, 1, 1); b *= 1.8; }
                    b *= 0.9 + 0.1 * sin(time * 2.5);
                }
                else if (style == 3)   // storm: crackles, dropping out and flaring
                {
                    float c = Hash(float2(floor(time * 16.0), floor(order * 9.0)));
                    b *= c > 0.25 ? 1.0 + 0.6 * step(0.9, c) : 0.3;
                }
                else if (style == 4)   // life: slow breathing glow
                {
                    b *= 0.82 + 0.18 * sin(time * 3.5 + order * 4.0);
                }
                else if (style == 5)   // death: dim, with dark wisps crawling upward
                {
                    float wisp = sin(uv.y * 26.0 - time * 3.0 + sin(uv.x * 9.0 + time) * 2.0);
                    b *= 0.55 + 0.45 * saturate(wisp);
                }
                else if (style == 6)   // myth: a golden shimmer sweeping across
                {
                    float band = frac((uv.x + uv.y) * 0.6 - time * 0.55);
                    b *= 0.85 + 0.9 * saturate(1.0 - abs(band - 0.5) * 8.0);
                }
                else if (style == 7)   // balance: even, gently pulsing
                {
                    b *= 0.9 + 0.1 * sin(time * 2.0);
                }

                b *= _Fade * (1.0 + _Flare) * _Intensity;
                return fixed4(col * b, 1);
            }
            ENDCG
        }
    }
}
