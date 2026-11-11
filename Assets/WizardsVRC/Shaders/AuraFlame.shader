// The battle aura round a caster as they charge a spell (SpellCinematic.AnimateAura): a flame-shaped shell of light.
// The mesh is a smooth teardrop standing on its origin, 1 tall; this shader makes it burn:
//   - the vertices wave (two ripples running round and up it, stronger towards the tip), so its outline licks and
//     flickers like fire; _Flicker is how fast, _Wobble how far;
//   - it is bright at its edge and nearly clear through the middle (so whoever stands in it is seen, wrapped in an
//     outline of flame), white-hot at the root (_Core), the school's colour above (_Color), thinning to nothing at the tip;
//   - additive, both sides drawn, never written to depth: it only ever adds light.
// _Power is its brightness (the script drives it: the ignition, each beat, the flare before the release).
Shader "WizardsVRC/AuraFlame"
{
    Properties
    {
        _Color ("Flame colour", Color) = (1, 0.8, 0.3, 1)
        _Core ("Colour at its root", Color) = (1, 1, 1, 1)
        _Power ("Brightness", Range(0, 6)) = 1
        _Flicker ("Flicker speed", Float) = 9
        _Wobble ("How far the flames lick", Range(0, 0.6)) = 0.16
        _Rim ("Edge sharpness", Range(0.5, 6)) = 2.2
        _Fill ("Glow through the middle", Range(0, 1)) = 0.1
        _Seed ("Phase (so two shells don't move together)", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent+5" "RenderType"="Transparent" "IgnoreProjector"="True" }
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

            fixed4 _Color, _Core;
            float _Power, _Flicker, _Wobble, _Rim, _Fill, _Seed;

            struct appdata { float4 vertex : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 normal : TEXCOORD0;
                float3 view : TEXCOORD1;
                float height : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 p = v.vertex.xyz;
                float h = saturate(p.y);
                // Two ripples running round the shell and up it; they move it more the higher up it is.
                float ang = atan2(p.z, p.x + 0.00001);
                float t = _Time.y * _Flicker + _Seed;
                float lick = sin(ang * 3.0 + t + p.y * 7.0) * 0.6 + sin(ang * 5.0 - t * 1.7 + p.y * 11.0) * 0.4;
                float rise = h * h;
                p.xz *= 1.0 + lick * _Wobble * (0.25 + rise);
                p.y += max(0.0, lick) * _Wobble * 1.6 * rise;
                float4 moved = float4(p, 1.0);
                o.pos = UnityObjectToClipPos(moved);
                // A turned shape: its surface faces out from its axis, leaning up towards the tip.
                float3 n = normalize(float3(v.vertex.x, 0.35 * h + 0.0001, v.vertex.z));
                o.normal = UnityObjectToWorldNormal(n);
                o.view = WorldSpaceViewDir(moved);
                o.height = h;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float facing = abs(dot(normalize(i.normal), normalize(i.view)));
                float rim = pow(1.0 - facing, _Rim);
                float tip = 1.0 - smoothstep(0.72, 1.0, i.height);
                fixed3 col = lerp(_Core.rgb, _Color.rgb, smoothstep(0.0, 0.55, i.height));
                return fixed4(col * (rim + _Fill) * tip * _Power, 1.0);
            }
            ENDCG
        }
    }
}
