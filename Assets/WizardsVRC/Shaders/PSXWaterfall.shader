// PSX waterfall: a sheet of falling water in the style of the original Spyro games. Two layers of a streak texture
// race downward at different speeds (in world space, so tall and short falls look the same), the sheet sways a little,
// there's a bright foaming lip at the top and churning white water at the bottom, all in 15-bit colour with ordered
// dithering. Semi-transparent, unlit, fogged, VR-safe.
//
// Use it on a vertical, subdivided sheet whose UV v runs 0 (bottom) to 1 (top) - the builder makes PSX_WaterfallSheet.
// Also fine on fountain jets (a cylinder) for falling-water columns.
//   _MainTex: Water/WaterfallStreaks.png.
Shader "WizardsVRC/PSXWaterfall"
{
    Properties
    {
        _MainTex ("Streaks", 2D) = "gray" {}
        _Color ("Water Colour", Color) = (0.22, 0.58, 0.95, 1)
        _StreakColor ("Streak Colour", Color) = (0.75, 0.92, 1, 1)
        _FoamColor ("Foam Colour", Color) = (1, 1, 1, 1)
        _Alpha ("Opacity", Range(0, 1)) = 0.86
        _TileSize ("Texture Tile Size (m)", Float) = 2.5
        _Speed ("Fall Speed (tiles per second)", Float) = 1.3
        _Speed2 ("Second Layer Speed", Float) = 0.8
        _Lip ("Foamy Lip (top share)", Range(0, 0.5)) = 0.08
        _Splash ("Churning Foot (bottom share)", Range(0, 0.6)) = 0.18
        _Sway ("Sway (m)", Float) = 0.06
        _SnapRes ("Vertex Snap Resolution (0 = off)", Float) = 900
        _ColorDepth ("Colour Levels per Channel (32 = PS1)", Float) = 32
    }
    SubShader
    {
        Tags { "Queue"="Transparent-5" "RenderType"="Transparent" "IgnoreProjector"="True" }
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
            fixed4 _Color, _StreakColor, _FoamColor;
            float _Alpha, _TileSize, _Speed, _Speed2, _Lip, _Splash, _Sway, _SnapRes, _ColorDepth;

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
                float4 uv : TEXCOORD0;     // xy layer 1, zw layer 2
                float height : TEXCOORD1;  // mesh v: 0 bottom, 1 top
                float4 screen : TEXCOORD2;
                UNITY_FOG_COORDS(3)
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 n = UnityObjectToWorldNormal(v.normal);
                // A slow sway out of the sheet, more at the bottom where the water is heaviest.
                wp += n * sin(_Time.y * 3.1 + wp.y * 1.7 + (wp.x + wp.z) * 0.9) * _Sway * (1.2 - v.uv.y);
                float4 clip = UnityWorldToClipPos(wp);
                float w = max(clip.w, 0.0001);
                clip = PsxSnap(clip, _SnapRes);
                o.pos = clip;
                float tile = max(_TileSize, 0.01);
                // Across: object X in metres; down: world height, so every fall streaks at the same scale.
                float across = v.vertex.x * length(unity_ObjectToWorld._m00_m10_m20) / tile;
                float down = wp.y / tile;
                o.uv = float4(across, down + _Time.y * _Speed, across * 1.31 + 0.37, down * 0.77 + _Time.y * _Speed2);
                o.height = v.uv.y;
                o.screen = ComputeScreenPos(clip);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float s = max(tex2D(_MainTex, i.uv.xy).r, tex2D(_MainTex, i.uv.zw).r);
                float3 col = lerp(_Color.rgb, _StreakColor.rgb, saturate(s * 1.4 - 0.35));
                float alpha = _Alpha;
                // Foamy lip at the top and churning foam at the foot, broken up by the streaks.
                float lip = saturate((i.height - (1.0 - _Lip)) / max(_Lip, 0.001));
                float foot = saturate((_Splash - i.height) / max(_Splash, 0.001));
                float foam = saturate(max(lip, foot * foot) * 1.3 + (s - 0.5) * 0.8);
                col = lerp(col, _FoamColor.rgb, step(0.5, foam) * 0.9 + foam * 0.1);
                alpha = lerp(alpha, 1, step(0.5, foam));

                fixed4 c = fixed4(col, alpha);
                UNITY_APPLY_FOG(i.fogCoord, c);
                c.rgb = PsxDither(c.rgb, i.screen, _ColorDepth);
                return c;
            }
            ENDCG
        }
    }
    Fallback Off
}
