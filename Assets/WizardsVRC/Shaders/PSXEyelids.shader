// Eyelids for the awakening intro: drawn on the inside of a small sphere that follows the viewer's head (see
// AwakeningSequence). _Open 0 = eyes shut (black), 1 = wide open. The lids close from the top and bottom with a
// curved edge, a vignette darkens the edges, _Tint washes the view (the dream's blue-violet haze) and _Flash fills
// it with white (each vision's flash). Drawn over everything except the subtitles.
Shader "WizardsVRC/PSXEyelids"
{
    Properties
    {
        _Open ("Open", Range(0, 1)) = 0
        _Softness ("Lid softness", Range(0.01, 0.5)) = 0.12
        _Vignette ("Vignette", Range(0, 1)) = 0.5
        _Tint ("Tint (alpha = strength)", Color) = (0.25, 0.2, 0.45, 0.0)
        _Flash ("Flash", Range(0, 1)) = 0
        _FlashColor ("Flash color", Color) = (1, 0.97, 0.9, 1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent+100" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Front
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            float _Open, _Softness, _Vignette, _Flash;
            fixed4 _Tint, _FlashColor;

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz; // the sphere turns with the head: +Z is where you look, +Y is up
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                // Lid edge: how far up or down from the middle of the view, curved so the lids meet like eyelids.
                float edge = abs(d.y) + 0.45 * d.x * d.x;
                float open = _Open * 1.25;
                float lid = smoothstep(open - _Softness, open, edge);
                if (_Open <= 0.001) lid = 1;
                // Vignette: darker away from straight ahead; behind you is always dark.
                float vig = _Vignette * smoothstep(0.85, 0.15, d.z);
                float dark = saturate(max(lid, vig));
                fixed3 col = lerp(_Tint.rgb * 0.35, fixed3(0, 0, 0), dark);
                float a = saturate(dark + _Tint.a * (1 - dark));
                col = lerp(col, _FlashColor.rgb, _Flash);
                a = max(a, _Flash);
                return fixed4(col, a);
            }
            ENDCG
        }
    }
}
