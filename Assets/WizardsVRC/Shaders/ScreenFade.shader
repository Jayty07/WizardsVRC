// A plain fade to a colour over everything you see: drawn on the inside of a small sphere round your head (SceneFade),
// on top of the world, the HUD and every panel. _Alpha 0 = clear, 1 = fully covered.
Shader "WizardsVRC/ScreenFade"
{
    Properties
    {
        _Color ("Colour", Color) = (0, 0, 0, 1)
        _Alpha ("Alpha", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Overlay+200" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
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

            fixed4 _Color;
            float _Alpha;

            struct appdata { float4 vertex : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return fixed4(_Color.rgb, saturate(_Alpha));
            }
            ENDCG
        }
    }
}
