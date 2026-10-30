// Shared PlayStation 1 tricks for the WizardsVRC shaders.
//   PsxSnap      vertex snapping to a coarse screen grid (the PS1 had no sub-pixel precision, so models wobble)
//   PsxDither    15-bit colour (32 levels per channel) with the PS1's 4x4 ordered dither, in chunky virtual pixels
//   PsxHash      a steady per-vertex random number (for hand-painted vertex colour)
#ifndef PSX_COMMON_INCLUDED
#define PSX_COMMON_INCLUDED

// How many dither cells across the screen (the PS1 drew 320 pixels across; higher = finer, subtler grain).
#define PSX_DITHER_RES 480.0

static const float PsxBayer[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

// Snaps a clip-space position to a grid 'res' cells across half the screen's width (square cells). 0 = off.
float4 PsxSnap(float4 clip, float res)
{
    if (res <= 0) return clip;
    float w = max(clip.w, 0.0001);
    float2 grid = res * float2(1.0, _ScreenParams.y / max(_ScreenParams.x, 1.0));
    clip.xy = round(clip.xy / w * grid) / grid * w;
    return clip;
}

// -0.5..0.5 from the 4x4 Bayer matrix at this pixel ('screen' from ComputeScreenPos).
float PsxBayerAt(float4 screen)
{
    float2 pix = screen.xy / max(screen.w, 0.0001) * _ScreenParams.xy;
    float cell = max(1.0, _ScreenParams.x / PSX_DITHER_RES);
    float2 p = fmod(floor(pix / cell), 4.0);
    return (PsxBayer[(int)p.y * 4 + (int)p.x] + 0.5) / 16.0 - 0.5;
}

// Cuts the colour down to 'levels' steps per channel (32 = the PS1's 15-bit colour) with ordered dithering.
float3 PsxDither(float3 col, float4 screen, float levels)
{
    float l = max(levels, 2.0) - 1.0;
    return floor(saturate(col) * l + 0.5 + PsxBayerAt(screen)) / l;
}

float PsxHash(float3 p)
{
    p = frac(p * 0.3183099 + 0.1);
    p *= 17.0;
    return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
}

#endif
