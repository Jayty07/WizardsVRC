// The Starfall Ward: the great protective dome over World 1, drawn on the inside of a huge hemisphere centred on each
// World 1 place (the hub and its streets). Additive light over the sky, never fogged, so it shows through the haze:
//   - a lattice of glowing hexagons over the whole dome, smaller towards the horizon;
//   - shimmering bands drifting round it, and now and then a cell flaring as the ward drinks in starlight;
//   - every few seconds a bright pulse rolling down from the crown to the horizon;
//   - a ring of slowly turning runes and a glowing seam where it meets the ground.
// _Crack (0 = whole, 1 = failing) is for the story to come: dark fractures spread, the light gutters and goes red.
Shader "WizardsVRC/WardDome"
{
    Properties
    {
        _Color ("Ward colour", Color) = (0.45, 0.75, 1, 1)
        _Glow ("Highlight colour", Color) = (0.9, 0.85, 1, 1)
        _Strength ("Strength", Range(0, 2)) = 1
        _Cells ("Hexagons across the crown", Range(4, 60)) = 22
        _PulseEvery ("Seconds between pulses", Range(2, 30)) = 9
        _Crack ("Crack (story: 0 whole, 1 failing)", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent-10" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Sphere" }
        Cull Off
        ZWrite Off
        Blend One One

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            fixed4 _Color, _Glow;
            float _Strength, _Cells, _PulseEvery, _Crack;

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0;
                float3 world : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            // Distance to the nearest hexagon edge (0 on the edge, ~0.5 at the middle) and the cell's id.
            float hexEdge(float2 p, out float2 id)
            {
                const float2 s = float2(1.0, 1.7320508);
                float4 hc = floor(float4(p, p - float2(0.5, 1.0)) / s.xyxy) + 0.5;
                float4 h = float4(p - hc.xy * s, p - (hc.zw + 0.5) * s);
                float2 q;
                if (dot(h.xy, h.xy) < dot(h.zw, h.zw)) { q = h.xy; id = hc.xy; }
                else { q = h.zw; id = hc.zw + 0.5; }
                q = abs(q);
                float d = max(dot(q, s * 0.5), q.x);
                return 0.5 - d;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float up = saturate(d.y);                       // 0 at the horizon, 1 at the crown
                float t = _Time.y;

                // Stereographic map from the crown: hexagons tile it, shrinking towards the horizon.
                float2 plane = d.xz / (1.0 + max(d.y, 0.02));
                float2 id;
                float edge = hexEdge(plane * _Cells, id);
                float line_ = smoothstep(0.06, 0.0, edge);
                float cellHash = hash21(id);

                // Shimmer: bands drifting round the dome and up it.
                float az = atan2(d.z, d.x);
                float shimmer = 0.55 + 0.45 * sin(az * 3.0 + up * 9.0 - t * 0.35) * sin(az * 5.0 - up * 4.0 + t * 0.21);

                // A cell flaring now and then.
                float flareT = frac(t * 0.07 + cellHash * 13.0);
                float flare = smoothstep(0.0, 0.05, flareT) * smoothstep(0.25, 0.05, flareT) * step(0.9, frac(cellHash * 7.31));

                // The pulse rolling down from the crown.
                float phase = frac(t / _PulseEvery);
                float ringAt = 1.0 - phase;                      // elevation of the pulse front
                float pd = (up - ringAt) * 18.0;
                float pulse = exp(-pd * pd) * (1.0 - phase * 0.6);

                // Brighter where you look through it edge-on (towards the horizon), faint overhead.
                float rim = pow(1.0 - up, 3.0);

                // Runes turning round the base, and the seam where the ward meets the ground.
                float runeBand = smoothstep(0.02, 0.04, up) * smoothstep(0.12, 0.09, up);
                float ra = az * 48.0 / 6.2831853 + t * 0.05;
                float rcell = floor(ra);
                float rx = frac(ra), ry = saturate((up - 0.04) / 0.07);
                float rh = hash21(float2(rcell, 3.7));
                float rune = step(0.25, rx) * step(rx, 0.75) * (step(abs(ry - 0.5), 0.36) * step(abs(rx - 0.5), 0.07 + 0.18 * step(0.5, rh))
                           + step(abs(ry - (0.25 + 0.5 * rh)), 0.06)) ;
                rune = saturate(rune) * runeBand * (0.6 + 0.4 * sin(t * 1.3 + rcell));
                float seam = exp(-up * 70.0);

                // The story's crack: fractures (cells breaking along their edges), the light guttering and turning red.
                float broken = step(1.0 - _Crack * 1.2, cellHash);
                float fracture = saturate(_Crack * 2.0) * smoothstep(0.03, 0.0, edge) * step(0.55, hash21(id + 9.1));
                float gutter = 1.0 - _Crack * (0.5 + 0.5 * sin(t * 7.0 + cellHash * 40.0)) * 0.6;

                float3 col = _Color.rgb * (line_ * (0.18 + 0.5 * rim) * shimmer + 0.035 + 0.12 * rim);
                col += _Glow.rgb * (flare * 0.35 + pulse * (0.12 + 0.5 * line_) + rune * 0.9 + seam * 0.8);
                col *= gutter * (1.0 - broken * 0.85);
                col = lerp(col, col.grr * float3(1.6, 0.35, 0.3), _Crack * 0.7);
                col += float3(1.0, 0.25, 0.15) * fracture * 0.8;
                // Fades away just below the horizon (the dome comes down out of sight into the haze).
                col *= smoothstep(-0.06, 0.0, d.y);
                return fixed4(col * _Strength, 1.0);
            }
            ENDCG
        }
    }
}
