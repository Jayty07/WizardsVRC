// PSX skybox with a skyline: the three-colour gradient sky of PSXSky, plus a pixel-art panorama strip wrapped round
// the horizon (distant islands, the castle, the streets you can walk to, clouds), point-sampled and dithered to 15-bit
// colour. The builder paints one strip per area so each place shows the others in the direction they really lie.
//   _Skyline: RGBA strip, u = bearing (0 = north / +Z, clockwise), v = elevation from _Low to _High degrees.
//
// With the PSX_SUN keyword (World 1's skies: WizardsVRCSceneBuilder.Sun.cs) the sky also has a great stylized sun and
// solar winds streaming from it right across the dome, all drawn as pixel art: every shape is worked out at the middle
// of a "sky pixel" (_SkyPixels of them to the degree, the same size as the strip's), so its edges are stair-stepped and
// stay put in the sky as you turn; soft things (the glow, the winds) are cut to a few flat steps with a 4x4 ordered
// dither laid on those same sky pixels; and the whole of it moves in steps, _SkyFps times a second, like a sprite.
//   The sun (on pixels of its own, square to it): a white-hot disc with a paler rim, a shaded crescent and a pulsing
//   eight-pointed star in its face; a plain ring and a ring of rune marks turning the other way; sixteen rays, long and
//   short by turns, turning slowly, their lengths rippling round; needles of light between them, flickering; a glow in
//   three dithered steps that breathes; a ring of light that leaves it every five seconds and thins as it spreads;
//   six little diamonds going round it on a dotted path; and stars twinkling in its light.
//   The winds (on the sky's pixels: a cube of equal-angle squares, so there's no pinch overhead): nine great ribbons
//   leaving the sun and wandering across the sky to the far side, gusts of brightness running out along them; finer
//   streaks between them; thin bright filaments in dashes that run outward; and motes with tails riding out like
//   shooting stars. Gold by the sun, through rose, to aqua across the sky.
// The strip (clouds, islands, the castle) is drawn over all of it. Tools/preview_sun_sky.py is the same maths in numpy,
// for seeing what a change does without Unity: keep the two in step.
Shader "WizardsVRC/PSXSkyline"
{
    Properties
    {
        _TopColor ("Top", Color) = (0.25,0.35,0.85,1)
        _HorizonColor ("Horizon", Color) = (1,0.6,0.55,1)
        _BottomColor ("Bottom", Color) = (0.45,0.3,0.6,1)
        _Sharpness ("Horizon Sharpness", Float) = 1.6
        _Skyline ("Skyline Strip", 2D) = "black" {}
        _Low ("Strip Bottom (degrees)", Float) = -6
        _High ("Strip Top (degrees)", Float) = 16
        _Rotation ("Turn (degrees)", Float) = 0
        _ColorDepth ("Colour Levels per Channel (32 = PS1)", Float) = 32

        [Header(Sun and solar winds)]
        _SunDir ("Sun direction (to the sun)", Vector) = (0.43, 0.5, -0.75, 0)
        _SunSize ("Sun size (1 = a disc 5 degrees across)", Float) = 1.5
        _SunPower ("Sun strength (1 = solid; less = veiled)", Range(0, 1)) = 1
        _WindPower ("Wind strength", Range(0, 2)) = 1
        _SkyPixels ("Sky pixels per degree", Float) = 5.69
        _SkyFps ("Animation steps per second (0 = smooth)", Float) = 12
        _SunCore ("Sun: white-hot", Color) = (1, 0.98, 0.84, 1)
        _SunGold ("Sun: gold", Color) = (1, 0.82, 0.34, 1)
        _SunDeep ("Sun: deep orange", Color) = (1, 0.52, 0.24, 1)
        _WindA ("Wind by the sun", Color) = (1, 0.88, 0.52, 1)
        _WindB ("Wind across the sky", Color) = (1, 0.62, 0.78, 1)
        _WindC ("Wind on the far side", Color) = (0.62, 0.93, 1, 1)
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
            #pragma target 3.0
            #pragma multi_compile_local __ PSX_SUN
            #include "UnityCG.cginc"
            #include "PSXCommon.cginc"
            fixed4 _TopColor, _HorizonColor, _BottomColor;
            float _Sharpness, _Low, _High, _Rotation, _ColorDepth;
            sampler2D _Skyline;
            struct appdata { float4 vertex : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; float4 screen : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                o.screen = ComputeScreenPos(o.pos);
                return o;
            }

#ifdef PSX_SUN
            fixed4 _SunCore, _SunGold, _SunDeep, _WindA, _WindB, _WindC;
            float4 _SunDir;
            float _SunSize, _SunPower, _WindPower, _SkyPixels, _SkyFps;

            float SunHash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            // 0..1 from the 4x4 Bayer matrix at a sky pixel (its whole-number place).
            float SkyBayer(float2 cell)
            {
                float2 p = cell - 4.0 * floor(cell / 4.0);
                return (PsxBayer[(int)p.y * 4 + (int)p.x] + 0.5) / 16.0;
            }

            // The middle of the sky pixel a direction falls in (a cube of equal-angle squares, 'res' pixels to the
            // radian), and that pixel's whole-number place on its face.
            float3 SkyPixel(float3 d, float res, out float2 cell)
            {
                float3 a = abs(d);
                float3 q;
                float2 c;
                if (a.y >= a.x && a.y >= a.z)
                {
                    cell = floor(atan(d.xz / a.y) * res);
                    c = tan((cell + 0.5) / res);
                    q = float3(c.x, sign(d.y), c.y);
                }
                else if (a.x >= a.z)
                {
                    cell = floor(atan(d.yz / a.x) * res);
                    c = tan((cell + 0.5) / res);
                    q = float3(sign(d.x), c.x, c.y);
                }
                else
                {
                    cell = floor(atan(d.xy / a.z) * res);
                    c = tan((cell + 0.5) / res);
                    q = float3(c.x, c.y, sign(d.z));
                }
                return normalize(q);
            }

            // The winds, then the sun, laid over the sky's colour 'col' in the direction d.
            float3 SunAndWinds(float3 col, float3 d)
            {
                const float PI = 3.14159265;
                const float TAU = 6.2831853;
                const float DEG = 57.29578;
                float tq = _SkyFps > 0.5 ? floor(_Time.y * _SkyFps) / _SkyFps : _Time.y;
                float3 S = normalize(_SunDir.xyz);
                float3 R = normalize(cross(float3(0.0, 1.0, 0.0), S));
                float3 U = cross(S, R);

                // ---- the solar winds: on the sky's own pixels
                float2 cell;
                float3 q = SkyPixel(d, _SkyPixels * DEG, cell);
                float by = SkyBayer(cell);
                float rho = acos(clamp(dot(q, S), -1.0, 1.0));        // angle from the sun
                float th = atan2(dot(q, U), dot(q, R));               // angle round it
                float rn = rho / PI;
                // Ribbons: nine great streams leaving the sun, wandering as they cross the sky.
                float lane = (th + 0.38 * sin(rho * 2.3 + tq * 0.05) + 0.16 * sin(rho * 5.1 - tq * 0.08 + 1.7)) * 9.0 / TAU;
                float li = floor(lane);
                float lf = frac(lane);
                li = li - 9.0 * floor(li / 9.0);
                float lh = SunHash(float2(li, li * 0.37 + 3.1));
                float width = lerp(0.16, 0.42, smoothstep(0.03, 0.5, rn)) * (0.7 + 0.6 * lh);
                float ribbon = saturate(1.0 - abs(lf - 0.5) / width);
                float gust = 0.5 + 0.5 * sin(rho * 7.0 - tq * 0.9 + lh * 6.2832);
                float gust2 = 0.5 + 0.5 * sin(rho * 17.0 - tq * 1.7 + lh * 17.0);
                float flow = ribbon * (0.35 + 0.45 * gust + 0.2 * gust2);
                // Finer streaks between them, quicker.
                float lane2 = (th + 0.2 * sin(rho * 3.7 - tq * 0.06 + 0.9)) * 23.0 / TAU;
                float l2i = floor(lane2);
                float l2f = frac(lane2);
                l2i = l2i - 23.0 * floor(l2i / 23.0);
                float l2h = SunHash(float2(l2i + 11.0, l2i * 0.61));
                float streak = saturate(1.0 - abs(l2f - 0.5) / 0.16) * smoothstep(0.55, 1.0, 0.5 + 0.5 * sin(rho * 11.0 - tq * 1.5 + l2h * 6.2832)) * step(0.35, l2h);
                float reach = smoothstep(0.012, 0.07, rn) * lerp(1.0, 0.7, smoothstep(0.1, 0.9, rn)) * smoothstep(-0.04, 0.06, q.y);
                float wind = (flow * 0.95 + streak * 0.5) * reach * _WindPower;
                // Filaments: thin bright lines along each ribbon, in dashes that run outward and taper at both ends.
                float strand = floor(lf * 3.0 + lh * 3.0);
                float fl = frac(lf * 3.0 + lh * 3.0);
                float dash = frac(rho * 2.6 - tq * 0.24 + lh * 7.0 + strand * 0.37);
                float taper = sin(saturate(dash / 0.62) * PI);
                float thin = (0.5 / _SkyPixels + 0.03) / max(sin(rho) * (TAU / 27.0) * DEG, 0.05);   // one sky pixel wide
                float filament = step(abs(fl - 0.5), thin) * step(dash, 0.62) * ribbon * reach * _WindPower;
                // Cut to three steps of light, dithered between; gold by the sun, rose, then aqua across the sky.
                float tone = floor(saturate(wind) * 3.0 + by);
                float hue = floor(saturate(rn * 1.35) * 4.0 + by);
                float3 wcol = hue < 1.0 ? _WindA.rgb : hue < 2.0 ? lerp(_WindA.rgb, _WindB.rgb, 0.6) : hue < 3.0 ? _WindB.rgb
                            : hue < 4.0 ? lerp(_WindB.rgb, _WindC.rgb, 0.6) : _WindC.rgb;
                col = lerp(col, wcol, tone >= 3.0 ? 0.66 : tone >= 2.0 ? 0.44 : tone >= 1.0 ? 0.22 : 0.0);
                float ftone = floor(saturate(filament * taper * 1.6) * 2.0 + by * 0.999);
                col = lerp(col, lerp(wcol, float3(1.0, 1.0, 1.0), 0.55), ftone >= 2.0 ? 0.85 : ftone >= 1.0 ? 0.5 : 0.0);
                // Motes riding the streaks outward: a bright head and a tail behind it.
                float ms = frac(rn * 4.0 - tq * 0.11 + l2h * 5.0);
                float across = abs(l2f - 0.5) * (TAU / 23.0) * sin(rho) * DEG;        // degrees off the streak's middle
                float mote = step(across, 0.5 / _SkyPixels + 0.02) * step(0.5, l2h) * smoothstep(0.02, 0.08, rn) * smoothstep(-0.02, 0.04, q.y);
                float mtone = floor(saturate((ms - 0.84) / 0.16) * mote * 2.0 + by * 0.999);
                col = lerp(col, lerp(wcol, float3(1.0, 1.0, 1.0), 0.5), 0.55 * step(1.0, mtone));
                col = lerp(col, float3(1.0, 1.0, 1.0), mote * step(0.975, ms) * 0.95);

                // ---- the sun: on pixels of its own, square to it
                float front = dot(d, S);
                if (front <= 0.8) return col;
                float2 ic = floor(float2(dot(d, R), dot(d, U)) / front * DEG * _SkyPixels);
                float px = _SkyPixels * _SunSize;                     // sky pixels to one of the sun's own degrees
                float2 p = (ic + 0.5) / px;                           // the pixel's middle, in the sun's degrees
                float r = length(p);
                float ang = atan2(p.y, p.x);
                float sb = SkyBayer(ic);
                float body = _SunPower;
                // Its glow: three dithered steps of light, breathing; and a ring of light that leaves it every few
                // seconds and thins as it spreads.
                float glow = 1.5 * exp(-max(r - 2.6, 0.0) / (3.0 * (1.0 + 0.08 * sin(tq * 0.9)))) * step(r, 30.0);
                float wave = frac(tq / 5.0);
                glow += step(abs(r - (4.0 + wave * 15.0)), 0.24 + 0.5 * wave) * (1.0 - wave) * 0.75;
                float gt = floor(saturate(glow) * 3.0 + sb);
                float3 gcol = gt >= 3.0 ? _SunCore.rgb : gt >= 2.0 ? _SunGold.rgb : lerp(_SunGold.rgb, _SunDeep.rgb, 0.6);
                col = lerp(col, gcol, (gt >= 3.0 ? 0.78 : gt >= 2.0 ? 0.5 : gt >= 1.0 ? 0.24 : 0.0) * body);
                // Needles of light between the rays, far longer, flickering one after another.
                float nsec = (ang + tq * 0.035) * 16.0 / TAU + 0.5;
                float ni = floor(nsec);
                ni = ni - 16.0 * floor(ni / 16.0);
                float nlen = 4.0 + 4.5 * (0.5 + 0.5 * sin(tq * 2.3 + ni * 2.4));
                float off = abs(frac(nsec) - 0.5) * (TAU / 16.0) * r * px;       // sky pixels off the needle's line
                if (r >= 4.3 && r <= 4.3 + nlen && off <= 0.5) col = lerp(col, _SunCore.rgb, 0.85 * body);
                // Rays: sixteen, long and short by turns, turning slowly, their lengths rippling round.
                float sec = (ang + tq * 0.035) * 16.0 / TAU;
                float si = floor(sec);
                float sf = frac(sec);
                float rlen = lerp(2.3, 4.3, step(0.5, frac(si * 0.5))) * (1.0 + 0.14 * sin(tq * 1.6 + si * 0.7853982));
                float along = (r - 4.05) / rlen;
                if (along >= 0.0 && along <= 1.0 && abs(sf - 0.5) <= 0.36 * (1.0 - along))
                    col = lerp(col, along < 0.34 ? lerp(_SunCore.rgb, _SunGold.rgb, 0.5) : along < 0.7 ? _SunGold.rgb : _SunDeep.rgb, body);
                // Two rings round it: a plain one, and a band of rune marks (a dot, a dash...) turning the other way.
                float marks = (ang - tq * 0.06) * 28.0 / TAU;
                float mwide = lerp(0.3, 0.62, step(0.5, frac(floor(marks) * 0.5)));
                if (r >= 3.0 && r <= 3.24) col = lerp(col, _SunGold.rgb, body);
                else if (r >= 3.48 && r <= 3.86 && frac(marks) <= mwide) col = lerp(col, _SunDeep.rgb, body);
                // The disc: white-hot middle, gold edge, a paler rim, a shaded crescent low on one side, and a star
                // set in its face (four long points and four short), pulsing.
                if (r <= 2.6)
                {
                    float3 dcol = r < 1.75 ? _SunCore.rgb : lerp(_SunCore.rgb, _SunGold.rgb, 0.55);
                    if (r >= 1.55 && (p.x * 0.6 - p.y * 0.8) / max(r, 0.001) >= 0.35) dcol = lerp(_SunGold.rgb, _SunDeep.rgb, 0.45);
                    if (r > 2.36) dcol = lerp(_SunCore.rgb, float3(1.0, 1.0, 1.0), 0.5);
                    float pulse = 1.0 + 0.1 * sin(tq * 2.2);
                    float sa = (ang + tq * 0.02) * 2.0;
                    float spike = max(1.5 * pow(saturate(1.0 - abs(sin(sa))), 1.7), 0.9 * pow(saturate(1.0 - abs(cos(sa))), 1.7));
                    if (r <= (0.4 + spike) * pulse) dcol = r < 0.42 * pulse ? _SunGold.rgb : float3(1.0, 1.0, 1.0);
                    col = lerp(col, dcol, body);
                }
                // Little diamonds going round it on a dotted path.
                const float orbit = 9.4;
                float oturn = ang - tq * 0.05;
                float oc = (floor(oturn * 6.0 / TAU) + 0.5) * (TAU / 6.0) + tq * 0.05;
                float2 g = p - float2(cos(oc), sin(oc)) * orbit;
                float gd = (abs(g.x) + abs(g.y)) * px;
                if (abs(r - orbit) <= 0.5 / px && frac(oturn * 60.0 / TAU) <= 0.34) col = lerp(col, _SunGold.rgb, 0.7 * body);
                if (gd <= 2.6) col = lerp(col, gd < 1.0 ? float3(1.0, 1.0, 1.0) : gd < 1.9 ? _SunCore.rgb : _SunGold.rgb, body);
                // Stars twinkling out in its light: each a dot that opens into a little cross and closes again.
                const float plot = 2.3;
                float2 cc = floor(p / plot);
                float ch = SunHash(cc + float2(7.0, 13.0));
                float2 o = (cc + 0.2 + 0.6 * float2(SunHash(cc + float2(1.3, 9.1)), SunHash(cc + float2(5.7, 2.9)))) * plot;
                float2 kk = abs(round((p - o) * px));
                float frame = floor(frac(tq * (0.35 + 0.5 * ch) + ch * 9.0) * 6.0);
                float arm = frame == 3.0 ? 2.0 : (frame == 2.0 || frame == 4.0) ? 1.0 : (frame == 1.0 || frame == 5.0) ? 0.0 : -1.0;
                float orr = length(o);
                if (ch >= 0.62 && orr >= 5.2 && orr <= 17.0 && min(kk.x, kk.y) <= 0.0 && max(kk.x, kk.y) <= arm)
                    col = lerp(col, kk.x + kk.y < 0.5 ? float3(1.0, 1.0, 1.0) : _SunCore.rgb, body);
                return col;
            }
#endif

            fixed4 frag (v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float up = pow(saturate(d.y), 1.0 / _Sharpness);
                float down = pow(saturate(-d.y), 1.0 / _Sharpness);
                float3 col = lerp(_HorizonColor.rgb, _TopColor.rgb, up);
                col = lerp(col, _BottomColor.rgb, down);

#ifdef PSX_SUN
                // The sun and its winds, behind the strip's clouds and islands.
                col = SunAndWinds(col, d);
#endif

                // The strip: bearing round the horizon, elevation up the strip.
                float bearing = atan2(d.x, d.z) / 6.2831853 + _Rotation / 360.0;
                float elev = degrees(asin(clamp(d.y, -1.0, 1.0)));
                float v = (elev - _Low) / max(_High - _Low, 0.01);
                if (v >= 0.0 && v <= 1.0)
                {
                    fixed4 s = tex2Dlod(_Skyline, float4(frac(bearing), v, 0, 0));
                    col = lerp(col, s.rgb, s.a);
                }
                else if (v < 0.0)
                {
                    // Below the strip: the sea fading into the bottom colour.
                    fixed4 s = tex2Dlod(_Skyline, float4(frac(bearing), 0.0, 0, 0));
                    col = lerp(col, s.rgb, s.a * saturate(1.0 + v * 4.0));
                }

                return fixed4(PsxDither(col, i.screen, _ColorDepth), 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
