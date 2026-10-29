// PSX-style billboard for floating labels (NPC name tags, quest markers).
// The quad turns to face each viewer's own camera on the GPU (no scripts, works per player and in VR, where it faces
// the point between the eyes so both eyes see the same quad). It stays upright, snaps its vertices like the rest of
// the world, uses point-filtered cutout pixels, and dithers away between _FadeStart and _FadeEnd metres.
// Put it on a Quad (or any mesh centred on its pivot). The object's rotation is ignored; its scale sets the size.
Shader "WizardsVRC/PSXBillboard"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _SnapRes ("Vertex Snap Resolution (0 = off)", Float) = 900
        _FadeStart ("Fade Start (m)", Float) = 22
        _FadeEnd ("Fade End (m)", Float) = 30
    }
    SubShader
    {
        // Batching would merge the quad into world space and lose its pivot, so it is disabled for this shader.
        Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" "IgnoreProjector"="True" "DisableBatching"="True" }
        Cull Off
        ZWrite On
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _SnapRes;
            float _FadeStart;
            float _FadeEnd;
            static const float Bayer[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float fade : TEXCOORD1;
                float4 screen : TEXCOORD2;
                UNITY_FOG_COORDS(3)
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float3 ViewerPosition()
            {
                #if defined(USING_STEREO_MATRICES)
                    return (unity_StereoWorldSpaceCameraPos[0] + unity_StereoWorldSpaceCameraPos[1]) * 0.5;
                #else
                    return _WorldSpaceCameraPos;
                #endif
            }

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float3 pivot = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
                float2 scale = float2(length(unity_ObjectToWorld._m00_m10_m20), length(unity_ObjectToWorld._m01_m11_m21));
                float3 toLabel = pivot - ViewerPosition();
                float dist = length(toLabel);
                float3 fwd = toLabel / max(dist, 0.0001);
                // Upright: the right vector stays level. Looking straight down, fall back to the camera's right.
                float3 right = cross(float3(0, 1, 0), fwd);
                right = dot(right, right) > 0.0001 ? normalize(right) : UNITY_MATRIX_V[0].xyz;
                float3 up = cross(fwd, right);
                float3 world = pivot + right * (v.vertex.x * scale.x) + up * (v.vertex.y * scale.y);

                float4 clip = UnityWorldToClipPos(world);
                float w = max(clip.w, 0.0001);
                if (_SnapRes > 0)
                    clip.xy = round(clip.xy / w * _SnapRes) / _SnapRes * w;
                o.pos = clip;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.fade = saturate((_FadeEnd - dist) / max(_FadeEnd - _FadeStart, 0.001));
                o.screen = ComputeScreenPos(clip);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv) * _Color;
                // 4x4 ordered dither for the distance fade (PSX has no smooth alpha on cutouts).
                float2 p = floor(fmod(i.screen.xy / max(i.screen.w, 0.0001) * _ScreenParams.xy, 4));
                float threshold = (Bayer[(int)p.y * 4 + (int)p.x] + 0.5) / 16.0;
                clip(min(col.a - 0.5, i.fade - threshold));
                col.a = 1;
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
    Fallback Off
}
