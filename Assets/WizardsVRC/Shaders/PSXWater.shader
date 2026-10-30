// PSX water in the style of the original Spyro the Dragon games: big gently heaving polygons (vertex waves, snapped
// like every other PSX surface), brightness that rolls across the surface with the waves (the Gouraud "shimmer"),
// two layers of a pixel ripple texture scrolling against each other, pixel sparkles where their bright lines cross,
// partial affine texture warp, and 15-bit colour with ordered dithering. Semi-transparent, unlit, fogged, VR-safe.
//
// Use it on a subdivided plane (the builder makes PSX_WaterGrid meshes) so the waves have vertices to move. Textures
// tile in world space (_TileSize metres), so any size of pond, moat or sea shares one material.
//   _MainTex: Water/WaterRipple.png (two patterns: red and green channels).
Shader "WizardsVRC/PSXWater"
{
    Properties
    {
        _MainTex ("Ripples (R and G = two patterns)", 2D) = "gray" {}
        _DeepColor ("Deep Colour", Color) = (0.05, 0.24, 0.62, 1)
        _ShallowColor ("Shallow Colour", Color) = (0.25, 0.68, 0.98, 1)
        _LineColor ("Ripple Line Colour", Color) = (0.62, 0.9, 1, 1)
        _SparkleColor ("Sparkle Colour", Color) = (1, 1, 1, 1)
        _Sparkle ("Sparkle Amount (0-1)", Range(0, 1)) = 0.35
        _Alpha ("Opacity", Range(0, 1)) = 0.82
        _TileSize ("Texture Tile Size (m)", Float) = 6
        _Scroll ("Scroll: layer 1 (xy), layer 2 (zw), tiles per second", Vector) = (0.035, 0.02, -0.025, 0.03)
        _WaveHeight ("Wave Height (m)", Float) = 0.12
        _WaveLength ("Wave Length (m)", Float) = 7
        _WaveSpeed ("Wave Speed", Float) = 1.1
        _Shimmer ("Wave Shimmer (brightness roll)", Range(0, 1)) = 0.22
        _CrestFoam ("Whitecaps on wave crests (0 = none)", Range(0, 1)) = 0
        _FoamColor ("Whitecap Colour", Color) = (0.95, 0.98, 1, 1)
        _SnapRes ("Vertex Snap Resolution (0 = off)", Float) = 900
        _Affine ("Affine Warp (0-1)", Range(0, 1)) = 0.35
        _ColorDepth ("Colour Levels per Channel (32 = PS1)", Float) = 32
    }
    SubShader
    {
        Tags { "Queue"="Transparent-10" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            #include "PSXCommon.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _DeepColor, _ShallowColor, _LineColor, _SparkleColor;
            float _Sparkle, _Alpha, _TileSize;
            float4 _Scroll;
            float _WaveHeight, _WaveLength, _WaveSpeed, _Shimmer, _CrestFoam;
            fixed4 _FoamColor;
            float _SnapRes, _Affine, _ColorDepth;

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 uv : TEXCOORD0;          // xy layer 1, zw layer 2 (perspective-correct)
                float4 affine1 : TEXCOORD1;     // layer 1 * w, w
                float4 affine2 : TEXCOORD2;     // layer 2 * w
                float2 shade : TEXCOORD3;      // x brightness, y wave height (-1..1)
                float4 screen : TEXCOORD4;
                UNITY_FOG_COORDS(5)
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                float t = _Time.y * _WaveSpeed;
                float wl = max(_WaveLength, 0.1);
                // Two crossing swells; waves are in world space so neighbouring water pieces line up.
                float wave = sin(dot(wp.xz, float2(0.8, 0.6)) / wl * 6.2831 + t) * 0.6
                           + sin(dot(wp.xz, float2(-0.35, 0.94)) / (wl * 0.63) * 6.2831 + t * 1.37) * 0.4;
                wp.y += wave * _WaveHeight;
                o.shade = float2(1 + wave * _Shimmer, wave);

                float4 clip = UnityWorldToClipPos(wp);
                float w = max(clip.w, 0.0001);
                clip = PsxSnap(clip, _SnapRes);
                o.pos = clip;

                float tile = max(_TileSize, 0.01);
                float2 base = wp.xz / tile;
                float2 uv1 = base + _Time.y * _Scroll.xy;
                // The second layer is turned and a little bigger, so the two never line up.
                float2 uv2 = float2(base.x * 0.8 - base.y * 0.6, base.x * 0.6 + base.y * 0.8) * 0.72 + _Time.y * _Scroll.zw;
                o.uv = float4(uv1, uv2);
                o.affine1 = float4(uv1 * w, w, 0);
                o.affine2 = float4(uv2 * w, 0, 0);
                o.screen = ComputeScreenPos(clip);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float iw = 1.0 / max(i.affine1.z, 0.0001);
                float2 uv1 = lerp(i.uv.xy, i.affine1.xy * iw, _Affine);
                float2 uv2 = lerp(i.uv.zw, i.affine2.xy * iw, _Affine);
                float a = tex2D(_MainTex, uv1).r;
                float b = tex2D(_MainTex, uv2).g;

                float3 col = lerp(_DeepColor.rgb, _ShallowColor.rgb, saturate(0.35 + (a + b) * 0.35));
                col = lerp(col, _LineColor.rgb, saturate(max(a, b) * 1.2 - 0.45));
                col *= i.shade.x;
                // Whitecaps: broken foam (cut up by the ripple pattern) along the tops of the swells.
                float crest = saturate((i.shade.y - (1.0 - _CrestFoam)) / max(_CrestFoam, 0.001));
                col = lerp(col, _FoamColor.rgb, step(0.45, crest * (0.55 + max(a, b))) * step(0.001, _CrestFoam));
                // Pixel sparkles where both layers' bright lines cross; they come and go as the layers slide.
                float sparkle = step(1.0 - _Sparkle * 0.5, a * b + 0.0001);
                col = lerp(col, _SparkleColor.rgb, sparkle);

                fixed4 c = fixed4(col, saturate(_Alpha + sparkle * 0.2));
                UNITY_APPLY_FOG(i.fogCoord, c);
                // 15-bit colour with the PS1's ordered dither (after the fog, like the real thing).
                c.rgb = PsxDither(c.rgb, i.screen, _ColorDepth);
                return c;
            }
            ENDCG
        }
    }
    Fallback Off
}
