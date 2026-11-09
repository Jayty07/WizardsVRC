// The Starfall Ward: the great protective dome over World 1, raised by the Starfall Crystal in the castle. It is one dome
// for the whole world, centred on the crystal and far bigger than any street: from a street you see its crown standing
// over the castle, its wall sweeping down behind you, and the crystal's beam of light climbing to the crown. Every World 1
// place draws it on the inside of a hemisphere that follows the camera just inside the draw distance; each pixel traces
// its view ray out to the real dome (centre _Centre, radius _Radius, in world space), so the dome can be any size and
// stand anywhere. Additive light over the sky, never fogged, so it shows through the haze:
//   - a lattice of glowing hexagons, its colour drifting through blue, violet and gold, cells flaring now and then;
//   - aurora curtains rippling round the lower dome, and starlight twinkling in the cells;
//   - every few seconds a pulse spreading out from the crown (so, from a street, rolling towards you from the castle);
//   - the crystal's beam, motes of light streaming up it to the crown;
//   - a ring of slowly turning runes and a glowing seam where it meets the ground.
// _Crack (0 = whole, 1 = failing) is for the story to come: dark fractures spread, the light gutters and goes red.
Shader "WizardsVRC/WardDome"
{
    Properties
    {
        _Color ("Ward colour", Color) = (0.45, 0.75, 1, 1)
        _Glow ("Highlight colour", Color) = (0.9, 0.85, 1, 1)
        _Aurora ("Aurora colour", Color) = (0.55, 0.35, 1, 1)
        _Gold ("Starlight colour", Color) = (1, 0.85, 0.45, 1)
        _Strength ("Strength", Range(0, 2)) = 1
        _Cells ("Hexagons across the crown", Range(4, 80)) = 30
        _PulseEvery ("Seconds between pulses", Range(2, 30)) = 9
        _Crack ("Crack (story: 0 whole, 1 failing)", Range(0, 1)) = 0
        _Centre ("Dome centre (world; the crystal, at the ground)", Vector) = (0, 0, 0, 0)
        _Radius ("Dome radius (m)", Float) = 1600
        _Screen ("Distance it's drawn at (m, inside the draw distance)", Float) = 280
        _BeamWidth ("Crystal beam half-width (m)", Float) = 7
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

            fixed4 _Color, _Glow, _Aurora, _Gold;
            float _Strength, _Cells, _PulseEvery, _Crack, _Radius, _Screen, _BeamWidth;
            float4 _Centre;

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 ray : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                // The hemisphere follows the camera (only its directions matter), always just inside the draw distance, so
                // nothing nearer is ever drawn over and nothing of it is ever clipped.
                float3 d = normalize(v.vertex.xyz);
                float3 w = _WorldSpaceCameraPos + d * _Screen;
                o.pos = mul(UNITY_MATRIX_VP, float4(w, 1.0));
                o.ray = d;
                return o;
            }

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float noise2(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash21(i), b = hash21(i + float2(1, 0)), c = hash21(i + float2(0, 1)), e = hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, e, f.x), f.y);
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
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float t = _Time.y;
                float3 o = _WorldSpaceCameraPos;
                float3 v = normalize(i.ray);
                float3 c = _Centre.xyz;
                float R = max(_Radius, 1.0);

                // Out along the view ray to the dome (from inside: the far side).
                float3 oc = o - c;
                float b = dot(v, oc);
                float k = dot(oc, oc) - R * R;
                float disc = b * b - k;
                if (disc <= 0.0) discard;
                float th = -b + sqrt(disc);
                if (th <= 0.0) discard;
                float3 n = (oc + v * th) / R;                    // where on the dome, from its centre
                float up = saturate(n.y);                        // 0 at the ground, 1 at the crown
                float az = atan2(n.z, n.x);

                // Stereographic map from the crown: hexagons tile it, shrinking towards the ground.
                float2 plane = n.xz / (1.0 + max(n.y, 0.02));
                float2 id;
                float edge = hexEdge(plane * _Cells, id);
                float line_ = smoothstep(0.07, 0.0, edge);
                float glowIn = smoothstep(0.22, 0.0, edge) * 0.35; // a soft halo inside each edge
                float cellHash = hash21(id);

                // Colour drifting round the dome: blue through violet to a touch of gold.
                float hue = 0.5 + 0.5 * sin(az * 2.0 + up * 3.0 - t * 0.12 + cellHash * 1.5);
                float3 wardCol = lerp(_Color.rgb, _Aurora.rgb, hue * 0.6);
                wardCol = lerp(wardCol, _Gold.rgb, pow(saturate(sin(az * 3.0 - t * 0.07) * 0.5 + 0.5), 6.0) * 0.5);

                // Shimmer: bands drifting round the dome and up it.
                float shimmer = 0.55 + 0.45 * sin(az * 3.0 + up * 9.0 - t * 0.35) * sin(az * 5.0 - up * 4.0 + t * 0.21);

                // A cell flaring now and then.
                float flareT = frac(t * 0.07 + cellHash * 13.0);
                float flare = smoothstep(0.0, 0.05, flareT) * smoothstep(0.25, 0.05, flareT) * step(0.9, frac(cellHash * 7.31));

                // Starlight twinkling in the cells.
                float2 sp = plane * _Cells * 4.0;
                float2 sid = floor(sp);
                float sh = hash21(sid + 17.0);
                float2 sf = frac(sp) - (0.25 + 0.5 * float2(hash21(sid + 3.1), hash21(sid + 7.7)));
                float star = step(0.82, sh) * smoothstep(0.09, 0.0, length(sf)) * (0.5 + 0.5 * sin(t * (1.5 + sh * 4.0) + sh * 60.0));

                // Aurora curtains rippling round the lower dome.
                float curtain = noise2(float2(az * 6.0 + t * 0.05, t * 0.1)) * 0.7 + noise2(float2(az * 15.0 - t * 0.08, 3.0)) * 0.3;
                float folds = 0.5 + 0.5 * sin(az * 40.0 + curtain * 9.0 + t * 0.4);
                float band = smoothstep(0.08, 0.25, up) * smoothstep(0.65, 0.3, up) * smoothstep(0.35, 0.8, curtain);
                float aurora = band * (0.35 + 0.65 * folds) * (1.0 - up);

                // The pulse spreading out from the crown (from a street: rolling towards you from the castle).
                float phase = frac(t / _PulseEvery);
                float ringAt = 1.0 - phase;
                float pd = (up - ringAt) * 16.0;
                float pulse = exp(-pd * pd) * (1.0 - phase * 0.6);

                // Brighter where you look through it edge-on, and low on the dome; faint overhead.
                float edgeOn = pow(1.0 - saturate(dot(v, n)), 2.0);
                float rim = max(pow(1.0 - up, 3.0), edgeOn * 0.8);

                // Runes turning round the base, and the seam where the ward meets the ground.
                float runeBand = smoothstep(0.015, 0.03, up) * smoothstep(0.09, 0.07, up);
                float ra = az * 96.0 / 6.2831853 + t * 0.05;
                float rcell = floor(ra);
                float rx = frac(ra), ry = saturate((up - 0.03) / 0.05);
                float rh = hash21(float2(rcell, 3.7));
                float rune = step(0.25, rx) * step(rx, 0.75) * (step(abs(ry - 0.5), 0.36) * step(abs(rx - 0.5), 0.07 + 0.18 * step(0.5, rh))
                           + step(abs(ry - (0.25 + 0.5 * rh)), 0.06));
                rune = saturate(rune) * runeBand * (0.6 + 0.4 * sin(t * 1.3 + rcell));
                float seam = exp(-up * 90.0);

                // The crystal's beam, from the crystal (the dome's centre, on the ground) up to the crown, motes streaming up it.
                float beam = 0.0;
                float2 v2 = v.xz;
                float vv = dot(v2, v2);
                if (vv > 1e-5)
                {
                    float2 o2 = oc.xz;
                    float tb = -dot(o2, v2) / vv;
                    if (tb > 0.0 && tb < th)
                    {
                        float dist = length(o2 + v2 * tb);
                        float y = oc.y + v.y * tb;                // height up the beam
                        float along = saturate(y / R);
                        float w = _BeamWidth * (1.0 + along * 2.0);
                        float core = exp(-(dist * dist) / (w * w));
                        float halo = exp(-dist / (w * 6.0)) * 0.25;
                        float motes = 0.6 + 0.4 * sin(y * 0.05 - t * 3.0) * sin(y * 0.013 - t * 1.1);
                        beam = (core * motes + halo) * step(0.0, y) * smoothstep(1.0, 0.92, along);
                        // In the castle itself the crystal's own beam (real geometry, over the fountain) takes over.
                        beam *= smoothstep(350.0, 500.0, length(o2));
                    }
                }

                // The story's crack: fractures (cells breaking along their edges), the light guttering and turning red.
                float broken = step(1.0 - _Crack * 1.2, cellHash);
                float fracture = saturate(_Crack * 2.0) * smoothstep(0.03, 0.0, edge) * step(0.55, hash21(id + 9.1));
                float gutter = 1.0 - _Crack * (0.5 + 0.5 * sin(t * 7.0 + cellHash * 40.0)) * 0.6;

                float3 col = wardCol * ((line_ + glowIn) * (0.2 + 0.55 * rim) * shimmer + 0.03 + 0.12 * rim);
                col += _Aurora.rgb * aurora * 0.45 + _Color.rgb * aurora * folds * 0.15;
                col += _Gold.rgb * star * (0.4 + 0.6 * up) * 0.8;
                col += _Glow.rgb * (flare * 0.4 + pulse * (0.1 + 0.55 * line_) + rune * 0.9 + seam * 0.9);
                col += lerp(_Glow.rgb, _Gold.rgb, 0.35) * beam * 1.2;
                col *= gutter * (1.0 - broken * 0.85);
                col = lerp(col, col.grr * float3(1.6, 0.35, 0.3), _Crack * 0.7);
                col += float3(1.0, 0.25, 0.15) * fracture * 0.8;
                // Nothing below the ground (the dome comes down into the haze at the horizon).
                col *= smoothstep(-0.01, 0.0, n.y);
                return fixed4(col * _Strength, 1.0);
            }
            ENDCG
        }
    }
}
