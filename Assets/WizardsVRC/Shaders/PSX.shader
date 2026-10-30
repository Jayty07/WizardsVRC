// PSX lit shader in the style of the original PlayStation 1 platformers (Spyro the Dragon and friends):
//   - Gouraud lighting worked out per vertex (sun, sky/ground ambient and up to four point lights), never per pixel
//   - shadowed sides tinted cool (a purple-blue bounce) instead of going grey, and slightly boosted saturation
//   - "hand-painted" vertex colour: each vertex a touch lighter or darker, warmer or cooler, so big flat faces get the
//     soft colour gradients of baked PS1 levels
//   - vertices snapped to a coarse screen grid (the PS1 wobble), partly affine texture mapping (the texture swim)
//   - point-filtered textures, per-vertex fog, and 15-bit colour with the PS1's 4x4 ordered dither
// Built-in pipeline, VR-safe (single-pass instanced).
Shader "WizardsVRC/PSX"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Texture", 2D) = "white" {}
        _EmissionColor ("Emission", Color) = (0,0,0,0)
        _WorldUV ("World-space UVs: metres per texture tile (0 = mesh UVs)", Float) = 0
        _Cutoff ("Alpha Cutoff (0 = solid; e.g. 0.5 for banners with cut-out shapes)", Range(0,1)) = 0

        [Header(PlayStation 1)]
        _SnapRes ("Vertex Snap Resolution (0 = off, 160 = real PS1)", Float) = 560
        _Affine ("Affine Texture Warp (0-1)", Range(0,1)) = 0.35
        _ColorDepth ("Colour Levels per Channel (32 = PS1, 256 = off)", Float) = 32

        [Header(Spyro style colour)]
        _ShadowTint ("Shadow Tint (alpha = strength)", Color) = (0.32, 0.24, 0.62, 0.35)
        _VertexPaint ("Hand-painted Vertex Colour (0-1)", Range(0,1)) = 0.3
        _Saturation ("Saturation", Range(0,2)) = 1.15
        _PointLights ("Point Light Strength", Range(0,3)) = 0.8

        [Header(Layering)]
        _OffsetFactor ("Depth Offset Factor (negative = drawn in front of what it lies on)", Float) = 0
        _OffsetUnits ("Depth Offset Units", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            Offset [_OffsetFactor], [_OffsetUnits]
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma multi_compile _ VERTEXLIGHT_ON
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "PSXCommon.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _EmissionColor;
            fixed4 _ShadowTint;
            float _SnapRes, _Affine, _WorldUV, _ColorDepth, _VertexPaint, _Saturation, _PointLights, _Cutoff;

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
                float2 uv : TEXCOORD0;
                float3 affineUv : TEXCOORD1;
                float4 screen : TEXCOORD2;
                fixed3 light : COLOR0;
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
                float4 clip = PsxSnap(UnityWorldToClipPos(wp), _SnapRes);
                float w = max(clip.w, 0.0001);
                o.pos = clip;
                o.screen = ComputeScreenPos(clip);

                float3 n = UnityObjectToWorldNormal(v.normal);
                float2 uv = TRANSFORM_TEX(v.uv, _MainTex);
                if (_WorldUV > 0)
                {
                    // Texture tiles at a fixed size in the world whatever the object's scale: top faces use XZ,
                    // walls use the axis they face along (good for paving, lawns and retaining walls).
                    float3 an = abs(n);
                    float2 w2 = an.y > 0.6 ? wp.xz : (an.x > an.z ? wp.zy : wp.xy);
                    uv = w2 / _WorldUV + _MainTex_ST.zw;
                }
                o.uv = uv;
                o.affineUv = float3(uv * w, w);

                // Gouraud lighting: sun + sky/ground ambient, worked out at the vertex only.
                float ndl = dot(n, normalize(_WorldSpaceLightPos0.xyz));
                float3 light = ShadeSH9(float4(n, 1)) + _LightColor0.rgb * max(0, ndl);
                // Sides turned away from the sun pick up a cool bounce instead of going flat grey.
                light += _ShadowTint.rgb * _ShadowTint.a * saturate(0.5 - ndl * 0.5);
                #if defined(VERTEXLIGHT_ON)
                light += _PointLights * Shade4PointLights(unity_4LightPosX0, unity_4LightPosY0, unity_4LightPosZ0,
                    unity_LightColor[0].rgb, unity_LightColor[1].rgb, unity_LightColor[2].rgb, unity_LightColor[3].rgb,
                    unity_4LightAtten0, wp, n);
                #endif
                // Hand-painted vertex colour (steady: it comes from the mesh's own vertex positions).
                float h1 = PsxHash(v.vertex.xyz * 1.7 + 3.1);
                float h2 = PsxHash(v.vertex.zxy * 2.3 + 11.7);
                light *= 1.0 + (h1 - 0.5) * _VertexPaint * 0.5;
                light += (h2 - 0.5) * _VertexPaint * float3(0.16, 0.04, -0.14);
                // The PS1 modulated texture by vertex colour up to 2x brightness.
                o.light = clamp(light, 0, 2);

                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = lerp(i.uv, i.affineUv.xy / i.affineUv.z, PsxAffine(_Affine));
                fixed4 col = tex2D(_MainTex, uv) * _Color;
                if (_Cutoff > 0) clip(col.a - _Cutoff);
                col.rgb = col.rgb * i.light + _EmissionColor.rgb;
                float grey = dot(col.rgb, float3(0.299, 0.587, 0.114));
                col.rgb = max(0, lerp(grey.xxx, col.rgb, _Saturation));
                UNITY_APPLY_FOG(i.fogCoord, col);
                col.rgb = PsxDither(col.rgb, i.screen, _ColorDepth);
                return col;
            }
            ENDCG
        }
    }
    Fallback "VertexLit"
}
