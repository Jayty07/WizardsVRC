// The spell cinematic's fade (SpellCinematic): drawn on the inside of a small sphere that follows the caster's head.
// _Fade 0 = clear, 1 = black. The view closes like an iris from the edges in, with a thin glowing rim in the spell's
// school colour (_Glow) at the closing edge; it opens the same way. Drawn over everything.
Shader "WizardsVRC/SpellFade"
{
    Properties
    {
        _Fade ("Fade", Range(0, 1)) = 0
        _Glow ("Rim colour", Color) = (1, 0.6, 0.2, 1)
        _RimWidth ("Rim width", Range(0.005, 0.2)) = 0.035
    }
    SubShader
    {
        Tags { "Queue"="Overlay+50" "RenderType"="Transparent" "IgnoreProjector"="True" }
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

            float _Fade, _RimWidth;
            fixed4 _Glow;

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
                o.dir = v.vertex.xyz; // the sphere turns with the head: +Z is where you look
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                // How far from straight ahead (0) to straight behind (1).
                float r = acos(clamp(d.z, -1.0, 1.0)) / 3.14159265;
                // The still-open circle shrinks to nothing as _Fade reaches 1.
                float edge = (1.0 - _Fade) * 0.62;
                float dark = smoothstep(edge - 0.06, edge + 0.01, r);
                if (_Fade >= 0.999) dark = 1.0;
                if (_Fade <= 0.001) dark = 0.0;
                float x = (r - edge) / _RimWidth;
                float rim = exp(-x * x) * saturate(_Fade * 5.0) * saturate((1.0 - _Fade) * 5.0);
                fixed3 col = _Glow.rgb * rim * 1.6;
                float a = saturate(max(dark, rim * 0.9));
                return fixed4(col, a);
            }
            ENDCG
        }
    }
}
