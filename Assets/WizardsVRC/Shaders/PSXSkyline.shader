// PSX skybox with a skyline: the three-colour gradient sky of PSXSky, plus a pixel-art panorama strip wrapped round
// the horizon (distant islands, the castle, the streets you can walk to, clouds), point-sampled and dithered to 15-bit
// colour. The builder paints one strip per area so each place shows the others in the direction they really lie.
//   _Skyline: RGBA strip, u = bearing (0 = north / +Z, clockwise), v = elevation from _Low to _High degrees.
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
            fixed4 frag (v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float up = pow(saturate(d.y), 1.0 / _Sharpness);
                float down = pow(saturate(-d.y), 1.0 / _Sharpness);
                float3 col = lerp(_HorizonColor.rgb, _TopColor.rgb, up);
                col = lerp(col, _BottomColor.rgb, down);

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
