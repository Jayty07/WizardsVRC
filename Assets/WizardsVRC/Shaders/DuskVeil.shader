// The dark that falls for a big spell's charge (SpellCinematic, tier 3 and up): a great sphere round the duel ring that
// darkens everything already drawn (the scenery, the fighters, the sky) and is drawn before every glowing thing (the
// particles, the runes, the panels), so the magic is all that stays bright. _Color's alpha is how dark; its colour tints
// the dark. Only someone inside the sphere sees it: from outside nothing changes.
Shader "WizardsVRC/DuskVeil"
{
    Properties
    {
        _Color ("Colour (alpha = how dark)", Color) = (0, 0, 0, 0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent-10" "RenderType"="Transparent" "IgnoreProjector"="True" }
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

            struct appdata { float4 vertex : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float inside : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                // The sphere's middle and radius in the world (the mesh is 0.5 in radius), and whether this eye is in it.
                float3 centre = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
                float radius = length(mul(unity_ObjectToWorld, float4(0.5, 0, 0, 0)).xyz);
                o.inside = step(distance(_WorldSpaceCameraPos, centre), radius * 0.97);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return fixed4(_Color.rgb, saturate(_Color.a) * i.inside);
            }
            ENDCG
        }
    }
}
