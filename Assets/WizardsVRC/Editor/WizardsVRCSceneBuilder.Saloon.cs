using System.Collections.Generic;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Components;
using WizardsVRC.EditorTools.WorldData;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// The Saloon landmark (Dustwind's Dusty Lantern): a long two-storey timber hall, built new in a Western style but
    /// with every working part of the Crystal Tap (WizardsVRCSceneBuilder.Tavern.cs): the jukebox (here a player piano
    /// whose keys move while it plays, the screen on the back wall), the mug bell, seats you can sit in, the mirror you
    /// switch on yourself and the flickering fire (in a pot-belly stove).
    /// Built in its own frame: x across the front, z from the back wall (-) to the front doors (+), y up.
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        private const float SaloonWallH = 9f, SaloonGalleryY = 4.6f, SaloonGalleryW = 3.4f, SaloonFloorY = 0.14f;

        /// <summary>The saloon's frame: its middle (on the floor), the way its doors face, its inside width and length.</summary>
        private static bool SaloonFrame(ZoneGrid g, ZoneLandmark l, out Vector3 c, out Vector3 front, out float W, out float D)
        {
            var fp = g.Footprint(l.Symbol);
            var cells = g.Find(l.Symbol);
            c = RectCenter(g, fp);
            front = Vector3.forward;
            W = D = 0f;
            if (fp.width == 0 || cells.Count == 0) return false;
            c.y = g.Elevation(cells[0].x, cells[0].y);
            Vector2Int fd = g.FrontSide(fp);
            front = fd == Vector2Int.zero ? Vector3.back : ZoneGrid.ToLocalDir(fd);
            bool alongX = Mathf.Abs(front.x) > 0.5f;
            float across = (alongX ? fp.height : fp.width) * g.cell, deep = (alongX ? fp.width : fp.height) * g.cell;
            // Leave a margin inside the block: the porch out front and a walkway round the sides.
            W = Mathf.Max(18f, across - 4f);
            D = Mathf.Max(24f, deep - 7f);
            return true;
        }

        /// <summary>An NPC painted inside a saloon's block stands behind the bar (the first in the middle, the rest along it).</summary>
        private static bool SaloonBarkeepSpot(ZoneGrid g, ZoneDefinition z, Vector2Int cell, out Vector3 pos, out Vector3 front)
        {
            pos = front = Vector3.zero;
            if (z.landmarks == null) return false;
            foreach (var l in z.landmarks)
            {
                if (l == null || l.kind != LandmarkKind.Saloon) continue;
                var fp = g.Footprint(l.Symbol);
                if (fp.width == 0 || !fp.Contains(cell)) continue;
                Vector3 c, f;
                float W, D;
                if (!SaloonFrame(g, l, out c, out f, out W, out D)) continue;
                int slot = 0;
                if (z.npcs != null)
                    foreach (var other in z.npcs)
                    {
                        if (other == null) continue;
                        var oc = g.Find(other.Symbol);
                        if (oc.Count == 0) continue;
                        if (oc[0] == cell) break;
                        if (fp.Contains(oc[0])) slot++;
                    }
                Quaternion rot = Quaternion.LookRotation(f);
                float along = slot == 0 ? 0f : ((slot + 1) / 2) * 3f * (slot % 2 == 1 ? 1f : -1f);
                Vector3 local = new Vector3(-W / 2f + 1.2f, SaloonFloorY, SaloonBarZ(D) + along);
                pos = c + rot * local;
                front = rot * Vector3.right;
                return true;
            }
            return false;
        }

        /// <summary>The middle of the bar along the saloon's length.</summary>
        private static float SaloonBarZ(float D) { return D * 0.08f; }

        private static void SaloonLandmark(Transform root, ZoneGrid g, ZoneDefinition z, ZoneLandmark l, List<UdonSharpBehaviour> behaviours,
            Material gold, Material window)
        {
            Vector3 c, front;
            float W, D;
            if (!SaloonFrame(g, l, out c, out front, out W, out D)) return;
            string name = string.IsNullOrEmpty(l.label) ? "The Dusty Lantern Saloon" : l.label;
            Transform t = Child(root, "Saloon_" + l.symbol, c);
            t.localRotation = Quaternion.LookRotation(front);
            float hw = W / 2f, hd = D / 2f;

            var siding = PsxTexMat("SaloonSiding", new Color(0.72f, 0.55f, 0.38f), TileTexture(StreetTexDir + "/Clapboard.png"), 3f);
            var paint = PsxTexMat("SaloonPaint", new Color(0.86f, 0.62f, 0.32f), TileTexture(StreetTexDir + "/Clapboard.png"), 3f);
            var timber = TimberMat();
            var darkWood = PsxTexMat("SaloonDarkWood", new Color(0.34f, 0.2f, 0.12f), TileTexture(TerrainTexDir + "/WoodPlanks.png"), 1.6f);
            var planks = PsxTexMat("SaloonFloor", new Color(0.66f, 0.46f, 0.3f), TileTexture(TerrainTexDir + "/WoodPlanks.png"), 2.4f);
            var shingle = PsxTexMat("SaloonRoof", new Color(0.5f, 0.32f, 0.22f), TileTexture(StreetTexDir + "/Shingles.png"), 2.6f);
            var wallpaper = PsxTexMat("SaloonWallpaper", new Color(0.62f, 0.2f, 0.18f), TileTexture(StreetTexDir + "/Velvet.png"), 2f);
            var brass = PMat("Brass", new Color(0.85f, 0.65f, 0.25f), 1f, new Color(0.2f, 0.14f, 0.02f));
            var iron = PMat("StoveIron", new Color(0.14f, 0.13f, 0.14f), 1f);
            var glow = PMat("TavernLantern", new Color(1f, 0.78f, 0.4f), 1f, new Color(1f, 0.65f, 0.25f));
            var felt = PMat("CheckerRed", new Color(0.7f, 0.12f, 0.12f), 1f);
            var cream = PMat("CheckerCream", new Color(0.92f, 0.86f, 0.72f), 1f);
            var pewter = PMat("Pewter", new Color(0.6f, 0.62f, 0.66f), 1f);
            var foam = PMat("MugFoam", new Color(0.98f, 0.95f, 0.85f), 1f);
            var bottleG = PMat("BottleGreen", new Color(0.2f, 0.5f, 0.3f), 1f, new Color(0.02f, 0.1f, 0.04f));
            var bottleB = PMat("BottleAmber", new Color(0.7f, 0.4f, 0.12f), 1f, new Color(0.15f, 0.06f, 0f));

            // ---- floor, walls (door gap in the front), roof
            Block(t, "SaloonFloor", new Vector3(0, SaloonFloorY - 0.1f, 0), new Vector3(W, 0.2f, D), planks);
            const float th = 0.5f;
            const float doorW = 3.2f, doorH = 4.2f;
            Block(t, "BackWall", new Vector3(0, SaloonWallH / 2f, -hd - th / 2f), new Vector3(W + th * 2f, SaloonWallH, th), siding);
            foreach (float s in new[] { -1f, 1f })
                Block(t, "SideWall", new Vector3(s * (hw + th / 2f), SaloonWallH / 2f, 0), new Vector3(th, SaloonWallH, D + th * 2f), siding);
            float sideFront = (W - doorW) / 2f;
            foreach (float s in new[] { -1f, 1f })
                Block(t, "FrontWall", new Vector3(s * (doorW / 2f + sideFront / 2f), SaloonWallH / 2f, hd + th / 2f), new Vector3(sideFront + th, SaloonWallH, th), siding);
            Block(t, "DoorLintel", new Vector3(0, (doorH + SaloonWallH) / 2f, hd + th / 2f), new Vector3(doorW, SaloonWallH - doorH, th), siding);
            // Wallpaper inside (a little in from the walls), up to the gallery floor.
            Deco(t, PrimitiveType.Cube, "Wallpaper", new Vector3(0, SaloonGalleryY / 2f, -hd + 0.03f), new Vector3(W, SaloonGalleryY, 0.04f), wallpaper, Quaternion.identity, false);
            foreach (float s in new[] { -1f, 1f })
                Deco(t, PrimitiveType.Cube, "Wallpaper", new Vector3(s * (hw - 0.03f), SaloonGalleryY / 2f, 0), new Vector3(0.04f, SaloonGalleryY, D), wallpaper, Quaternion.identity, false);
            // Ceiling and a shingled gable roof over it (running front to back).
            Block(t, "Ceiling", new Vector3(0, SaloonWallH + 0.15f, 0), new Vector3(W + th * 2f, 0.3f, D + th * 2f), darkWood);
            float rise = W * 0.22f;
            float slope = Mathf.Atan2(rise, hw) * Mathf.Rad2Deg;
            float slab = Mathf.Sqrt(hw * hw + rise * rise) + 0.8f;
            foreach (float s in new[] { -1f, 1f })
                Deco(t, PrimitiveType.Cube, "RoofSlope", new Vector3(s * hw / 2f, SaloonWallH + 0.3f + rise / 2f, 0), new Vector3(slab, 0.3f, D + 1.6f), shingle,
                    Quaternion.Euler(0, 0, -s * slope), false);
            Deco(t, PrimitiveType.Cube, "RoofGableBack", new Vector3(0, SaloonWallH + rise / 2f, -hd - th / 2f), new Vector3(W * 0.7f, rise, th), siding, Quaternion.identity, false);

            // ---- the false front: a tall painted board facade over the door wall with the saloon's name, a cornice,
            // a covered porch on posts, hitching posts and a water trough.
            float ffH = SaloonWallH + rise + 3f;
            Block(t, "FalseFront", new Vector3(0, (SaloonWallH + ffH) / 2f, hd + th + 0.1f), new Vector3(W + th * 2f, ffH - SaloonWallH, 0.35f), paint);
            Deco(t, PrimitiveType.Cube, "FalseFrontCornice", new Vector3(0, ffH + 0.2f, hd + th + 0.25f), new Vector3(W + th * 2f + 0.6f, 0.45f, 0.8f), timber, Quaternion.identity, false);
            Deco(t, PrimitiveType.Cube, "FalseFrontPediment", new Vector3(0, ffH + 1.1f, hd + th + 0.15f), new Vector3(W * 0.35f, 1.4f, 0.3f), paint, Quaternion.identity, false);
            var sign = WorldText(t, "SaloonSign", new Vector3(0, SaloonWallH + rise * 0.55f + 1f, hd + th + 0.35f), Quaternion.Euler(0, 180f, 0),
                new Vector2(2200, 300), 0.007f, name.ToUpperInvariant(), 72);
            sign.color = new Color(0.98f, 0.88f, 0.55f);
            DressSign(sign, 2050f, 260f);
            const float porchD = 3.6f;
            Deco(t, PrimitiveType.Cube, "PorchRoof", new Vector3(0, 4.6f, hd + th + porchD / 2f), new Vector3(W + th * 2f, 0.2f, porchD + 0.4f), shingle,
                Quaternion.Euler(8f, 0, 0), false);
            Block(t, "PorchDeck", new Vector3(0, 0.15f, hd + th + porchD / 2f), new Vector3(W + th * 2f, 0.3f, porchD), planks);
            int porchPosts = Mathf.Max(3, Mathf.RoundToInt(W / 4.5f) + 1);
            for (int i = 0; i < porchPosts; i++)
            {
                float x = i / (float)(porchPosts - 1) * (W - 0.6f) - (W - 0.6f) / 2f;
                if (Mathf.Abs(x) < doorW / 2f + 0.3f) continue;
                Block(t, "PorchPost", new Vector3(x, 2.4f, hd + th + porchD - 0.25f), new Vector3(0.3f, 4.6f, 0.3f), timber);
            }
            foreach (float s in new[] { -1f, 1f })
            {
                // Hitching rails out front.
                float hx = s * (hw * 0.55f);
                foreach (float k in new[] { -1.4f, 1.4f })
                    Block(t, "HitchPost", new Vector3(hx + k, 0.6f, hd + th + porchD + 1.6f), new Vector3(0.22f, 1.2f, 0.22f), timber);
                Deco(t, PrimitiveType.Cube, "HitchRail", new Vector3(hx, 1.1f, hd + th + porchD + 1.6f), new Vector3(3.2f, 0.16f, 0.16f), timber, Quaternion.identity, false);
            }
            Block(t, "Trough", new Vector3(hw * 0.55f, 0.4f, hd + th + porchD + 3f), new Vector3(2.6f, 0.8f, 0.9f), timber);
            Deco(t, PrimitiveType.Cube, "TroughWater", new Vector3(hw * 0.55f, 0.72f, hd + th + porchD + 3f), new Vector3(2.4f, 0.04f, 0.7f),
                PMat("TroughWater", new Color(0.3f, 0.5f, 0.6f), 1f), Quaternion.identity, false);
            // Batwing doors, swung a little open.
            foreach (float s in new[] { -1f, 1f })
                Deco(t, PrimitiveType.Cube, "BatwingDoor", new Vector3(s * doorW * 0.27f, 2.1f, hd + th * 0.5f + 0.4f), new Vector3(doorW * 0.46f, 1.7f, 0.08f), darkWood,
                    Quaternion.Euler(0, s * 25f, 0), false);
            // Front windows either side of the door, and up on the gallery level.
            foreach (float s in new[] { -1f, 1f })
                foreach (float y in new[] { 2.4f, 6.6f })
                {
                    Deco(t, PrimitiveType.Cube, "Window", new Vector3(s * (doorW / 2f + sideFront / 2f), y, hd + th + 0.03f), new Vector3(2.4f, 1.8f, 0.08f), window, Quaternion.identity, false);
                    Deco(t, PrimitiveType.Cube, "Window", new Vector3(s * (doorW / 2f + sideFront / 2f), y, hd - 0.03f), new Vector3(2.4f, 1.8f, 0.08f), window, Quaternion.identity, false);
                }

            // ---- the long bar down the left side, with a brass foot rail, stools, mugs, the bell and bottle shelves
            float barZ = SaloonBarZ(D), barLen = Mathf.Min(D * 0.55f, 18f), barX = -hw + 2.6f;
            Block(t, "BarCounter", new Vector3(barX, 0.6f + SaloonFloorY, barZ), new Vector3(0.9f, 1.2f, barLen), darkWood);
            Deco(t, PrimitiveType.Cube, "BarTop", new Vector3(barX, 1.24f + SaloonFloorY, barZ), new Vector3(1.2f, 0.08f, barLen + 0.2f), darkWood, Quaternion.identity, false);
            Deco(t, PrimitiveType.Cylinder, "FootRail", new Vector3(barX + 0.65f, 0.25f + SaloonFloorY, barZ), new Vector3(0.07f, barLen / 2f, 0.07f), brass,
                Quaternion.Euler(90f, 0, 0), false);
            var mugs = new List<GameObject>();
            int stools = Mathf.Max(4, Mathf.FloorToInt(barLen / 1.6f));
            for (int i = 0; i < stools; i++)
            {
                float zz = barZ - barLen / 2f + (i + 0.5f) / stools * barLen;
                MakeSeat(t, "BarStool", new Vector3(barX + 1.5f, SaloonFloorY, zz), Vector3.left, 0.78f, darkWood, brass, behaviours);
                if (i % 2 == 0) mugs.Add(MakeMug(t, new Vector3(barX + 0.2f, 1.29f + SaloonFloorY, zz + 0.3f), pewter, foam));
            }
            for (int shelf = 0; shelf < 3; shelf++)
            {
                float y = 1.6f + shelf * 0.75f;
                Deco(t, PrimitiveType.Cube, "BottleShelf", new Vector3(-hw + 0.35f, y, barZ), new Vector3(0.6f, 0.08f, barLen), darkWood, Quaternion.identity, false);
                for (int b = 0; b < Mathf.FloorToInt(barLen / 0.7f); b++)
                {
                    if ((b + shelf) % 4 == 3) continue;
                    float zz = barZ - barLen / 2f + 0.4f + b * 0.7f;
                    Shape(t, "Bottle", CylMesh(), new Vector3(-hw + 0.35f, y + 0.3f, zz), new Vector3(0.18f, 0.25f, 0.18f), (b + shelf) % 2 == 0 ? bottleG : bottleB, false, Quaternion.identity);
                }
            }
            var bellGo = Shape(t, "Bell", BlobMesh(), new Vector3(barX, 1.5f + SaloonFloorY, barZ + barLen / 2f - 0.4f), new Vector3(0.38f, 0.42f, 0.38f), brass, false, Quaternion.identity);
            bellGo.isStatic = false;
            bellGo.AddComponent<SphereCollider>().radius = 0.6f;
            var bellAudio = bellGo.AddComponent<AudioSource>();
            bellAudio.playOnAwake = false;
            bellAudio.spatialBlend = 1f;
            bellAudio.maxDistance = 25f;
            bellAudio.clip = UiClip("ui_health_powerup_01");
            var reset = UdonSharpUndo.AddComponent<MugReset>(bellGo);
            reset.mugs = mugs.ToArray();
            reset.bell = bellAudio;
            SetInteractText(reset, "Ring for fresh sarsaparilla");
            behaviours.Add(reset);

            // ---- the long mirror behind the bar (above the shelves), off until you switch it on
            var mirror = GameObject.CreatePrimitive(PrimitiveType.Quad);
            mirror.name = "Mirror";
            Object.DestroyImmediate(mirror.GetComponent<Collider>());
            mirror.transform.SetParent(t, false);
            mirror.transform.localPosition = new Vector3(-hw + 0.08f, 3.2f, barZ);
            mirror.transform.localRotation = Quaternion.Euler(0, -90f, 0);
            mirror.transform.localScale = new Vector3(Mathf.Min(barLen, 10f), 1.6f, 1f);
            Block(t, "MirrorFrame", new Vector3(-hw + 0.05f, 3.2f, barZ), new Vector3(0.06f, 1.9f, Mathf.Min(barLen, 10f) + 0.3f), brass);
            var mirrorShader = Shader.Find("FX/MirrorReflection");
            if (mirrorShader != null)
            {
                var mirrorMat = AssetDatabase.LoadAssetAtPath<Material>(MaterialsDir + "/TavernMirror.mat");
                if (mirrorMat == null)
                {
                    mirrorMat = new Material(mirrorShader);
                    AssetDatabase.CreateAsset(mirrorMat, MaterialsDir + "/TavernMirror.mat");
                }
                mirror.GetComponent<Renderer>().sharedMaterial = mirrorMat;
            }
            mirror.AddComponent<VRCMirrorReflection>();
            mirror.SetActive(false);
            var mirrorButton = Shape(t, "MirrorToggle", GemMesh(), new Vector3(barX + 0.1f, 1.55f + SaloonFloorY, barZ - barLen / 2f + 0.4f), new Vector3(0.3f, 0.45f, 0.3f),
                PMat("MirrorGem", new Color(0.6f, 0.9f, 1f), 1f, new Color(0.3f, 0.55f, 0.7f)), false, Quaternion.identity);
            mirrorButton.isStatic = false;
            mirrorButton.AddComponent<SphereCollider>().radius = 0.5f;
            var mirrorToggle = UdonSharpUndo.AddComponent<LocalToggle>(mirrorButton);
            mirrorToggle.targets = new[] { mirror };
            SetInteractText(mirrorToggle, "Mirror on/off (just for you)");
            behaviours.Add(mirrorToggle);

            // ---- the stage at the back: low planks, footlights, a red curtain, the player piano and the jukebox
            float stageD = Mathf.Min(6.5f, D * 0.2f), stageW = W * 0.6f;
            Block(t, "Stage", new Vector3(0, 0.35f + SaloonFloorY, -hd + stageD / 2f), new Vector3(stageW, 0.7f, stageD), planks);
            Deco(t, PrimitiveType.Cube, "StageStep", new Vector3(0, 0.18f + SaloonFloorY, -hd + stageD + 0.4f), new Vector3(3f, 0.36f, 0.8f), planks, Quaternion.identity, false);
            var velvet = PsxTexMat("TavernVelvet", new Color(0.78f, 0.14f, 0.2f), TileTexture(StreetTexDir + "/Velvet.png"), 1f);
            foreach (float s in new[] { -1f, 1f })
                Deco(t, PrimitiveType.Cube, "StageCurtain", new Vector3(s * (stageW / 2f - 0.7f), 3.2f, -hd + 0.4f), new Vector3(1.6f, 5.4f, 0.2f), velvet, Quaternion.identity, false);
            Deco(t, PrimitiveType.Cube, "StageValance", new Vector3(0, 5.6f, -hd + 0.4f), new Vector3(stageW, 0.9f, 0.25f), velvet, Quaternion.identity, false);
            for (int k = 0; k < 6; k++)
                Shape(t, "Footlight", GemMesh(), new Vector3((k + 0.5f) / 6f * stageW - stageW / 2f, 0.85f + SaloonFloorY, -hd + stageD - 0.2f), new Vector3(0.25f, 0.3f, 0.25f), glow,
                    false, Quaternion.identity);
            PointLight(t, new Vector3(0, 4f, -hd + stageD + 1.5f), new Color(1f, 0.8f, 0.55f), 12f, 1.4f);
            // The player piano, its keys moving while the jukebox plays.
            Vector3 piano = new Vector3(-stageW / 2f + 2.2f, 0.7f + SaloonFloorY, -hd + 1.2f);
            Block(t, "PianoBody", piano + new Vector3(0, 1.1f, 0), new Vector3(2.6f, 2.2f, 0.9f), darkWood);
            Deco(t, PrimitiveType.Cube, "PianoKeybed", piano + new Vector3(0, 1f, 0.6f), new Vector3(2.4f, 0.12f, 0.45f), darkWood, Quaternion.identity, false);
            Deco(t, PrimitiveType.Cube, "PianoRoll", piano + new Vector3(0, 1.75f, 0.47f), new Vector3(1.2f, 0.5f, 0.04f), cream, Quaternion.identity, false);
            var keys = new List<Transform>();
            var ivory = PMat("PianoIvory", new Color(0.95f, 0.93f, 0.86f), 1f);
            var ebony = PMat("PianoEbony", new Color(0.08f, 0.07f, 0.07f), 1f);
            for (int k = 0; k < 14; k++)
            {
                float x = (k + 0.5f) / 14f * 2.2f - 1.1f;
                var key = Deco(t, PrimitiveType.Cube, "PianoKey", piano + new Vector3(x, 1.08f, 0.65f), new Vector3(0.14f, 0.05f, 0.3f), k % 3 == 1 ? ebony : ivory, Quaternion.identity, false);
                key.isStatic = false;
                keys.Add(key.transform);
            }
            MakeSeat(t, "PianoBench", piano + new Vector3(0, 0, 1.5f) - new Vector3(0, 0.7f, 0) + new Vector3(0, 0.7f, 0), Vector3.back, 0.6f, darkWood, brass, behaviours);
            var centre = Child(t, "JukeboxCentre", new Vector3(0, SaloonFloorY, 0));
            var jukebox = BuildJukeboxAt(t, centre, new Vector3(0.8f, 6.6f, -hd + 0.15f), Quaternion.Euler(0, 180f, 0), new Vector3(0, 3f, -hd + 1.5f),
                new Vector3(0, 4.6f, -hd + 0.9f), new Vector3(stageW / 2f + 1.2f, SaloonFloorY, -hd + stageD + 1.2f), Quaternion.Euler(0, 180f, 0), 0f,
                new Vector2(hw + 0.4f, hd + 0.4f), SaloonWallH + rise, "Saloon Piano", darkWood, darkWood, behaviours);
            jukebox.pianoKeys = keys.ToArray();

            // ---- checker tables with stools across the floor
            int rows = Mathf.Max(2, Mathf.FloorToInt((D - stageD - 8f) / 5.5f));
            for (int r = 0; r < rows; r++)
                for (int col = 0; col < 2; col++)
                {
                    float x = col == 0 ? hw * 0.05f : hw * 0.55f;
                    float zz = -hd + stageD + 4f + r * 5.5f;
                    if (zz > hd - 4f) continue;
                    Vector3 tp = new Vector3(x, SaloonFloorY, zz);
                    Deco(t, PrimitiveType.Cylinder, "TableLeg", tp + new Vector3(0, 0.5f, 0), new Vector3(0.18f, 0.5f, 0.18f), darkWood, Quaternion.identity, false);
                    Block(t, "TableTop", tp + new Vector3(0, 1.0f, 0), new Vector3(1.6f, 0.08f, 1.6f), darkWood);
                    for (int q = 0; q < 4; q++)
                        Deco(t, PrimitiveType.Cube, "Checker", tp + new Vector3((q % 2 - 0.5f) * 0.7f, 1.05f, (q / 2 - 0.5f) * 0.7f), new Vector3(0.68f, 0.02f, 0.68f),
                            (q % 2 == q / 2) ? felt : cream, Quaternion.identity, false);
                    foreach (var d in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
                        MakeSeat(t, "TableStool", tp + d * 1.4f, -d, 0.62f, darkWood, brass, behaviours);
                    mugs.Add(MakeMug(t, tp + new Vector3(0.45f, 1.06f, 0.45f), pewter, foam));
                }
            reset.mugs = mugs.ToArray();

            // ---- the pot-belly stove (front right) with the flickering fire and log seats round it
            Vector3 stove = new Vector3(-hw * 0.35f, SaloonFloorY, hd - 5.5f);
            Shape(t, "StoveBelly", BlobMesh(), stove + new Vector3(0, 1f, 0), new Vector3(1.4f, 1.6f, 1.4f), iron, true, Quaternion.identity);
            Shape(t, "StoveBase", CylMesh(), stove + new Vector3(0, 0.15f, 0), new Vector3(1.2f, 0.15f, 1.2f), iron, false, Quaternion.identity);
            Shape(t, "StovePipe", CylMesh(), stove + new Vector3(0, (2f + SaloonWallH) / 2f, 0), new Vector3(0.3f, (SaloonWallH - 2f) / 2f, 0.3f), iron, false, Quaternion.identity);
            PointLight(t, stove + new Vector3(0, 1.2f, 0), new Color(1f, 0.55f, 0.2f), 10f, 1.6f);
            Light stoveLight = t.GetChild(t.childCount - 1).GetComponent<Light>();
            stoveLight.gameObject.name = "HearthLight";
            var fire = HearthFire(t, stove + new Vector3(0, 0.55f, 0.55f), Vector3.right, Quaternion.LookRotation(Vector3.forward), stoveLight);
            fire.transform.localScale = Vector3.one * 0.45f;
            behaviours.Add(fire);
            foreach (float a in new[] { 150f, 200f, 250f })
            {
                Vector3 d = Quaternion.Euler(0, a, 0) * Vector3.forward;
                MakeSeat(t, "LogSeat", stove + d * 2.6f, -d, 0.5f, darkWood, brass, behaviours, true);
            }

            // ---- the galleries upstairs along both side walls (the back wall is the stage's, with the screen), with rails,
            // benches looking down on the stage, and a staircase up to the right-hand one
            var gfloor = PsxTexMat("SaloonGallery", new Color(0.6f, 0.42f, 0.28f), TileTexture(TerrainTexDir + "/WoodPlanks.png"), 2.4f);
            float gw = SaloonGalleryW;
            foreach (float s in new[] { -1f, 1f })
                Block(t, "GallerySide", new Vector3(s * (hw - gw / 2f), SaloonGalleryY, -hd + gw + (D - gw - 9f) / 2f), new Vector3(gw, 0.3f, D - gw - 9f), gfloor);
            // Railings (solid, so nobody walks off) and posts down to the floor.
            float sideLen = D - gw - 9f, sideMid = -hd + gw + sideLen / 2f;
            foreach (float s in new[] { -1f, 1f })
            {
                Block(t, "GalleryRail", new Vector3(s * (hw - gw), SaloonGalleryY + 0.6f, sideMid), new Vector3(0.12f, 1.1f, sideLen), timber);
                Block(t, "GalleryRailBackEnd", new Vector3(s * (hw - gw / 2f), SaloonGalleryY + 0.6f, -hd + gw), new Vector3(gw, 1.1f, 0.12f), timber);
                // The right-hand gallery's end is open: the stairs come up there.
                if (s < 0f) Block(t, "GalleryRailEnd", new Vector3(s * (hw - gw / 2f), SaloonGalleryY + 0.6f, sideMid + sideLen / 2f), new Vector3(gw, 1.1f, 0.12f), timber);
                for (float zz = -hd + gw; zz <= sideMid + sideLen / 2f + 0.01f; zz += 4f)
                    Block(t, "GalleryPost", new Vector3(s * (hw - gw), SaloonGalleryY / 2f, zz), new Vector3(0.25f, SaloonGalleryY, 0.25f), timber);
            }
            // Benches along the gallery rails, facing the stage.
            foreach (float s in new[] { -1f, 1f })
                for (float zz = -hd + gw + 2f; zz < sideMid + sideLen / 2f - 1f; zz += 3f)
                    MakeSeat(t, "GalleryBench", new Vector3(s * (hw - gw / 2f), SaloonGalleryY + 0.15f, zz), new Vector3(-s, 0, -0.6f).normalized, 0.5f, darkWood, brass, behaviours);
            // The staircase: a ramp you walk up (steps drawn on it), from the floor by the front up to the right gallery.
            float stairZ0 = sideMid + sideLen / 2f;
            float stairLen = Mathf.Min(7.5f, hd - 0.8f - stairZ0);
            float stairAngle = Mathf.Atan2(SaloonGalleryY, stairLen) * Mathf.Rad2Deg;
            Vector3 rampMid = new Vector3(hw - gw / 2f, SaloonGalleryY / 2f, stairZ0 + stairLen / 2f);
            Block(t, "StairRamp", rampMid, new Vector3(gw - 0.4f, 0.2f, Mathf.Sqrt(stairLen * stairLen + SaloonGalleryY * SaloonGalleryY)), planks, Quaternion.Euler(stairAngle, 0, 0));
            int steps = 14;
            for (int k = 0; k < steps; k++)
            {
                float f = (k + 0.5f) / steps;
                Deco(t, PrimitiveType.Cube, "Step", new Vector3(hw - gw / 2f, f * SaloonGalleryY + 0.1f, stairZ0 + stairLen * (1f - f)), new Vector3(gw - 0.4f, 0.06f, stairLen / steps * 0.6f),
                    timber, Quaternion.identity, false);
            }
            Block(t, "StairRail", new Vector3(hw - gw + 0.1f, SaloonGalleryY / 2f + 1f, stairZ0 + stairLen / 2f), new Vector3(0.1f, 0.9f, Mathf.Sqrt(stairLen * stairLen + SaloonGalleryY * SaloonGalleryY)),
                timber, Quaternion.Euler(stairAngle, 0, 0));

            // ---- wagon-wheel chandeliers and oil lamps on the posts
            for (int k = 0; k < 3; k++)
            {
                float zz = -hd + stageD + 3f + k * (D - stageD - 6f) / 2f;
                Vector3 cc = new Vector3(hw * 0.25f, 7.2f, zz);
                Deco(t, PrimitiveType.Cube, "ChandelierChain", cc + new Vector3(0, (SaloonWallH - 7.2f) / 2f, 0), new Vector3(0.06f, SaloonWallH - 7.2f, 0.06f), iron, Quaternion.identity, false);
                for (int sp = 0; sp < 8; sp++)
                {
                    Quaternion q = Quaternion.Euler(0, sp * 22.5f, 0);
                    Deco(t, PrimitiveType.Cube, "WheelSpoke", cc, new Vector3(0.08f, 0.08f, 2.4f), timber, q, false);
                    Vector3 rimP = Quaternion.Euler(0, sp * 45f + 22.5f, 0) * new Vector3(0, 0, 1.15f);
                    Deco(t, PrimitiveType.Cube, "WheelRim", cc + rimP, new Vector3(0.95f, 0.12f, 0.12f), timber, Quaternion.Euler(0, sp * 45f + 22.5f + 90f, 0), false);
                    if (sp % 2 == 0)
                        Shape(t, "ChandelierLamp", GemMesh(), cc + Quaternion.Euler(0, sp * 45f, 0) * new Vector3(0, 0.3f, 1.15f), new Vector3(0.3f, 0.45f, 0.3f), glow, false, Quaternion.identity);
                }
                PointLight(t, cc - new Vector3(0, 0.6f, 0), new Color(1f, 0.82f, 0.6f), 14f, 1.2f);
            }
            for (float zz = -hd + gw + 4f; zz < hd - 2f; zz += 8f)
                foreach (float s in new[] { -1f, 1f })
                    Shape(t, "OilLamp", GemMesh(), new Vector3(s * (hw - 0.3f), 3f, zz), new Vector3(0.28f, 0.4f, 0.28f), glow, false, Quaternion.identity);
            if (gold != null) Deco(t, PrimitiveType.Cube, "StageStar", new Vector3(0, 6.6f, -hd + 0.25f), new Vector3(0.6f, 0.6f, 0.1f), gold, Quaternion.Euler(0, 0, 45f), false);
        }
    }
}
