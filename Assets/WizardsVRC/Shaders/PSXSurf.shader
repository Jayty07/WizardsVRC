// PSX surf: the white water where the sea meets an island's cliffs. Put it on a flat ring mesh whose UV v runs from 0
// at the shore to 1 out at sea (the builder makes PSX_SurfRing meshes). A band of churning foam hugs the rocks and
// lines of breaking waves roll in toward them, broken up by the ripple texture; it fades out to sea, bobs with the
// swell, and is drawn in 15-bit colour with ordered dithering. Semi-transparent, unlit, fogged, VR-safe.
//   _MainTex: Water/WaterRipple.png.
Shader "WizardsVRC/PSXSurf"
{
    Properties
    {
        _MainTex ("Ripples", 2D) = "gray" {}
        _FoamColor ("Foam Colour", Color) = (0.95, 0.98, 1, 1)
        _WaterColor ("Shallow Water Colour", Color) = (0.35, 0.75, 0.95, 1)
        _TileSize ("Ripple Tile Size (m)", Float) = 4
        _Waves ("Breaking Lines", Float) = 3
        _Speed ("Waves per Second", Float) = 0.18
        _Bob ("Bob (m)", Float) = 0.25
        _ColorDepth ("Colour Levels per Channel (32 = PS1)", Float) = 32
    }
    SubShader
    {
        Tags { "Queue"="Transparent-8" "RenderType"="Transparent" "IgnoreProjector"="True" }
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

            sampler2D _MainTex;
            fixed4 _FoamColor, _WaterColor;
            float _TileSize, _Waves, _Speed, _Bob, _ColorDepth;
            static const float Bayer[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 uv : TEXCOORD0;     // xy world ripple, z shore distance (0 shore - 1 sea), w along the shore
                float4 screen : TEXCOORD1;
                UNITY_FOG_COORDS(2)
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                wp.y += sin(_Time.y * 1.3 + v.uv.y * 9.0 + v.uv.x * 40.0) * _Bob * v.uv.y;
                float4 clip = UnityWorldToClipPos(wp);
                float w = max(clip.w, 0.0001);
                clip.xy = round(clip.xy / w * 900.0) / 900.0 * w;
                o.pos = clip;
                o.uv = float4(wp.xz / max(_TileSize, 0.01) + _Time.y * float2(0.03, 0.02), v.uv.y, v.uv.x);
                o.screen = ComputeScreenPos(clip);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float r = tex2D(_MainTex, i.uv.xy).r;
                float d = i.uv.z;
                // Lines of breaking waves rolling in to the shore (phase wobbles along the shore so they don't look ruled).
                float phase = d * _Waves + _Time.y * _Speed + sin(i.uv.w * 37.0) * 0.15;
                float crest = 1.0 - abs(frac(phase) - 0.5) * 2.0;
                float foam = step(0.78 - r * 0.25, crest) * (1.0 - d);
                // Churning white water right against the rocks.
                foam = max(foam, step(d, 0.12 + r * 0.08));
                float3 col = lerp(_WaterColor.rgb, _FoamColor.rgb, foam);
                float alpha = max(foam, 0.35 * (1.0 - d)) * saturate((1.0 - d) * 3.0);

                float2 px = floor(fmod(i.screen.xy / max(i.screen.w, 0.0001) * _ScreenParams.xy, 4));
                float dd = (Bayer[(int)px.y * 4 + (int)px.x] + 0.5) / 16.0 - 0.5;
                float levels = max(_ColorDepth, 2) - 1;
                col = floor(saturate(col) * levels + 0.5 + dd) / levels;
                fixed4 c = fixed4(col, alpha);
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }
    Fallback Off
}
