using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// The people you talk to: the Headmaster, the seven professors and every street NPC, each with their own design, built
    /// in the game's PS1 style from the same low-poly shapes as everything else.
    ///
    /// Every NPC is a jointed body (Model > Hips > Spine > Neck/Head and both arms, each with an elbow), so it can move:
    /// an Animator plays a slow idle (breathing, looking about, shifting its weight; one of three so neighbours don't move
    /// in step, a floating one for ghosts, and a slouched one: hunched, head thrust forward) and a "Talk" gesture (a nod and
    /// an open hand) when you click it.
    /// Some professors are other peoples (NpcSpecies): a Draconian, a minotaur, a kobold from Bigdig and a unicorn, each with
    /// its own head (snout or muzzle, horns, ears or frills, crest or mane) and a tail (and wings) on the same jointed body.
    /// A design (<see cref="NpcLook"/>) sets build and height, skin, hair and beard, face, outfit (robe, dress, coat, vest,
    /// overalls, tunic, armour, or bark for a tree spirit), extras (cape, star mantle, apron, sash, scarf, collar, fur
    /// trim, badge, satchel, spectacles, goggles, stone shoulders), a hat (20-odd kinds) and what it carries in each hand.
    /// The looks themselves are in WizardsVRCSceneBuilder.NpcLooks.cs. The model faces its local -Z, like the old one.
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        private const string NpcDir = "Assets/WizardsVRC/Generated/Npcs";

        private enum NpcBuild { Average, Slim, Stout, Tall, Small, Elder, Hulking }
        private enum NpcOutfit { Robe, Dress, Coat, Vest, Overalls, Tunic, Armor, Bark }
        private enum NpcHair { Bald, Short, Long, Bun, Ponytail, Wild, Braids }
        private enum NpcBeard { None, Long, Short, Mustache, Handlebar, Sideburns }
        private enum NpcHat
        {
            None, Pointed, Crooked, StarHat, Hood, FlatCap, Cowboy, TopHat, Bowler, Helmet, PlumedHelmet, Kerchief, Circlet, Ranger,
            Conductor, MinerLamp, Bonnet, FurCap, Turban, Laurel, FlowerCrown, Captain, Veil, Straw, Leaves,
        }
        private enum NpcItem
        {
            None, Staff, StarStaff, FlameRod, IceStaff, SparkRod, VineStaff, SkullCane, BalanceStaff, OpenBook, Halberd, Flag, Pitchfork,
            Pickaxe, Cane, Book, Ledger, Lantern, SignalLamp, Hammer, Tongs, Wrench, Keys, Scroll, Telescope, Trowel, Mug, Candle, Lasso,
            Net, Pan, Quill, Wand,
        }

        /// <summary>What a character is: people, or (for some professors) another of the world's peoples.</summary>
        private enum NpcSpecies { Human, Draconian, Minotaur, Kobold, Unicorn }

        private class NpcLook
        {
            public NpcSpecies species = NpcSpecies.Human;
            public Color belly = new Color(-1f, 0f, 0f);   // snout, muzzle and belly (r < 0 = the skin, lightened)
            public Color horn = new Color(0.92f, 0.86f, 0.7f);
            public Color mane2 = new Color(-1f, 0f, 0f);   // a unicorn's second mane colour (r < 0 = the hair)
            public float stand;                            // height of a crate it stands on (a kobold, to look you in the eye)
            public float coatLength = 1f;                  // a coat's tails: 1 = to the knee, more = longer (trimmed in gold)
            public NpcBuild build = NpcBuild.Average;
            public float height = 1f;
            public bool feminine;
            public Color skin = new Color(0.93f, 0.76f, 0.6f);
            public Color hair = new Color(0.38f, 0.24f, 0.14f);
            public Color beardColor = new Color(-1f, 0f, 0f);   // r < 0 = same as the hair
            public Color eyes = new Color(0.08f, 0.06f, 0.08f);
            public bool glowEyes;
            public NpcHair hairStyle = NpcHair.Short;
            public NpcBeard beard = NpcBeard.None;
            public float brows;   // tilt in degrees: + stern, - kind
            public NpcOutfit outfit = NpcOutfit.Robe;
            public Color main = new Color(0.3f, 0.3f, 0.5f);
            public Color second = new Color(0.85f, 0.8f, 0.7f);
            public Color trim = new Color(0.85f, 0.7f, 0.3f);
            public Color extra = new Color(0.55f, 0.35f, 0.2f);   // cape, apron, sash, scarf
            public bool cape, starMantle, apron, sash, scarf, highCollar, furTrim, badge, satchel, spectacles, goggles, gloves, epaulettes, stony;
            public NpcHat hat = NpcHat.None;
            public Color hatColor = new Color(0.2f, 0.2f, 0.35f);
            public float hatScale = 1f;
            public NpcItem right = NpcItem.None, left = NpcItem.None;
            public Color gem = new Color(0.6f, 0.85f, 1f);
            public bool ghost;
            public int idle = -1;   // -1 = picked from the name
        }

        // Skin and hair colours the looks pick from.
        private static readonly Color SkinFair = new Color(0.95f, 0.8f, 0.66f), SkinLight = new Color(0.88f, 0.69f, 0.52f),
                                      SkinTan = new Color(0.74f, 0.53f, 0.37f), SkinBrown = new Color(0.56f, 0.38f, 0.25f),
                                      SkinDeep = new Color(0.4f, 0.27f, 0.18f), SkinPale = new Color(0.84f, 0.84f, 0.82f);
        private static readonly Color HairWhite = new Color(0.92f, 0.92f, 0.95f), HairGrey = new Color(0.6f, 0.6f, 0.64f),
                                      HairBlack = new Color(0.12f, 0.1f, 0.1f), HairBrown = new Color(0.38f, 0.24f, 0.14f),
                                      HairAuburn = new Color(0.58f, 0.24f, 0.12f), HairBlonde = new Color(0.9f, 0.76f, 0.42f),
                                      HairRed = new Color(0.82f, 0.3f, 0.1f);

        private static readonly Dictionary<string, RuntimeAnimatorController> _npcControllers = new Dictionary<string, RuntimeAnimatorController>();

        /// <summary>Called at the start of a rebuild: the NPC animation assets are made fresh.</summary>
        private static void ResetNpcModels()
        {
            _npcControllers.Clear();
            _wingMesh = null;
            if (AssetDatabase.IsValidFolder(NpcDir)) AssetDatabase.DeleteAsset(NpcDir);
        }

        // ================================================================== small helpers

        private static Material NpcMat(NpcLook L, string part, Color c, float glow = 0f)
        {
            if (L.ghost)
            {
                c = Color.Lerp(c, new Color(0.62f, 0.92f, 0.88f), 0.55f);
                glow = Mathf.Max(glow, 0.35f);
            }
            string key = "Npc" + part + "_" + ColorUtility.ToHtmlStringRGB(c) + (glow > 0f ? "_G" + Mathf.RoundToInt(glow * 100f) : "");
            return PMat(key, c, 1f, glow > 0f ? c * glow : default(Color));
        }

        private static GameObject NP(Transform parent, string name, Mesh mesh, Vector3 pos, Vector3 scale, Material mat, Vector3 euler = default(Vector3))
        {
            var go = Shape(parent, name, mesh, pos, scale, mat, false, Quaternion.Euler(euler));
            go.isStatic = false;
            return go;
        }

        private static GameObject NBox(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat, Vector3 euler = default(Vector3))
        {
            var go = Deco(parent, PrimitiveType.Cube, name, pos, scale, mat, Quaternion.Euler(euler), false);
            go.isStatic = false;
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        private static Transform Pivot(Transform parent, string name, Vector3 pos, Vector3 euler = default(Vector3))
        {
            var t = Child(parent, name, pos);
            t.localRotation = Quaternion.Euler(euler);
            t.gameObject.isStatic = false;
            return t;
        }

        private struct NpcShape { public float scale, w, d, sh, head, arm; }

        private static NpcShape ShapeOf(NpcLook L)
        {
            var b = new NpcShape { scale = 1f, w = 0.52f, d = 0.36f, sh = 0.3f, head = 1f, arm = 1f };
            switch (L.build)
            {
                case NpcBuild.Slim: b.w = 0.44f; b.d = 0.32f; b.sh = 0.26f; break;
                case NpcBuild.Stout: b.w = 0.66f; b.d = 0.5f; b.sh = 0.34f; break;
                case NpcBuild.Tall: b.w = 0.46f; b.d = 0.33f; b.sh = 0.28f; b.scale = 1.08f; break;
                case NpcBuild.Small: b.w = 0.46f; b.d = 0.34f; b.sh = 0.26f; b.head = 1.2f; b.scale = 0.7f; break;
                case NpcBuild.Elder: b.w = 0.5f; b.d = 0.36f; b.sh = 0.29f; b.scale = 0.96f; break;
                case NpcBuild.Hulking: b.w = 0.78f; b.d = 0.52f; b.sh = 0.44f; b.scale = 1.08f; b.arm = 1.35f; break;
            }
            if (L.feminine) { b.w *= 0.93f; b.sh *= 0.95f; }
            b.scale *= L.height;
            return b;
        }

        private static bool LongItem(NpcItem i)
        {
            return i == NpcItem.Staff || i == NpcItem.StarStaff || i == NpcItem.IceStaff || i == NpcItem.VineStaff || i == NpcItem.BalanceStaff ||
                   i == NpcItem.Halberd || i == NpcItem.Flag || i == NpcItem.Pitchfork;
        }

        private static bool HeldInFront(NpcItem i)
        {
            return i == NpcItem.Book || i == NpcItem.Ledger || i == NpcItem.OpenBook || i == NpcItem.Mug || i == NpcItem.Candle || i == NpcItem.Scroll ||
                   i == NpcItem.Telescope || i == NpcItem.Pan || i == NpcItem.Quill || i == NpcItem.Wand || i == NpcItem.FlameRod || i == NpcItem.SparkRod;
        }

        // ================================================================== the model

        /// <summary>
        /// Builds an NPC from its look, facing local -Z at pos: the jointed model, its name and hint tags above its head and a
        /// click box. 'top' is how tall it stands (hat included), 'faceY' the height of its face, both in its parent's space.
        /// </summary>
        private static Transform NpcModel(Transform parent, string name, Vector3 pos, float yaw, NpcLook L, string title, string hint, out float top, out float faceY)
        {
            var root = Child(parent, name, pos);
            root.localRotation = Quaternion.Euler(0, yaw, 0);
            NpcShape B = ShapeOf(L);
            var model = Pivot(root, "Model", Vector3.zero);
            model.localScale = Vector3.one * B.scale;
            if (L.ghost) model.localPosition = new Vector3(0, 0.15f, 0);
            if (L.stand > 0f)
            {
                // A sturdy crate with brass corners to stand on.
                var crateWood = NpcMat(L, "Crate", new Color(0.5f, 0.33f, 0.18f));
                var brass = NpcMat(L, "Brass", new Color(0.78f, 0.6f, 0.3f));
                NBox(root, "Crate", new Vector3(0, L.stand / 2f, 0), new Vector3(0.9f, L.stand, 0.7f), crateWood);
                foreach (float y in new[] { 0.06f, L.stand - 0.06f })
                    NBox(root, "CrateBand", new Vector3(0, y, 0), new Vector3(0.92f, 0.05f, 0.72f), brass);
                model.localPosition = new Vector3(0, L.stand, 0);
            }

            var skin = NpcMat(L, "Skin", L.stony ? new Color(0.5f, 0.48f, 0.46f) : L.skin);
            var main = NpcMat(L, "Main", L.main);
            var second = NpcMat(L, "Second", L.second);
            var trim = NpcMat(L, "Trim", L.trim);
            var extra = NpcMat(L, "Extra", L.extra);
            var hairMat = NpcMat(L, "Hair", L.hair);
            var beardMat = NpcMat(L, "Hair", L.beardColor.r < 0f ? L.hair : L.beardColor);
            var dark = NpcMat(L, "Dark", new Color(0.16f, 0.12f, 0.1f));
            var leather = NpcMat(L, "Leather", new Color(0.36f, 0.22f, 0.12f));
            var metal = NpcMat(L, "Metal", new Color(0.62f, 0.64f, 0.68f));
            var gold = NpcMat(L, "Gold", new Color(0.95f, 0.78f, 0.3f), 0.15f);
            var hands = L.gloves ? leather : skin;
            float w = B.w, d = B.d;

            // ---- hips and legs (or a skirt down to the ground)
            var hips = Pivot(model, "Hips", new Vector3(0, 1.05f, 0));
            bool skirt = L.outfit == NpcOutfit.Robe || L.outfit == NpcOutfit.Dress || L.outfit == NpcOutfit.Bark;
            if (L.ghost)
            {
                // No feet: the figure trails away into a wisp.
                NP(hips, "GhostWisp", ConeMesh(), new Vector3(0, -0.5f, 0), new Vector3(w * 1.6f, 1.05f, d * 2.1f), main, new Vector3(180f, 0, 0));
                NP(hips, "GhostWispTail", ConeMesh(), new Vector3(0, -1.05f, 0.08f), new Vector3(w * 0.5f, 0.3f, d * 0.6f), main, new Vector3(160f, 0, 0));
            }
            else if (skirt)
            {
                float flare = L.outfit == NpcOutfit.Dress ? 1.75f : 1.95f;
                Material lower = L.outfit == NpcOutfit.Bark ? skin : main;
                Flare(hips, "Skirt", w, d, new Vector2(w * flare, d * 2.5f), 0.05f, -1.02f, lower);
                if (L.outfit != NpcOutfit.Bark)
                    NP(hips, "Hem", CylMesh(), new Vector3(0, -1.0f, 0), new Vector3(w * flare * 1.01f, 0.035f, d * 2.52f), L.furTrim ? NpcMat(L, "Fur", new Color(0.93f, 0.93f, 0.95f)) : trim);
                if (L.outfit == NpcOutfit.Bark)
                    for (int k = 0; k < 5; k++)   // roots spreading over the ground
                        NP(hips, "Root" + k, ConeMesh(), new Vector3(Mathf.Sin(k * 1.257f) * w * 0.9f, -0.98f, Mathf.Cos(k * 1.257f) * d * 1.1f),
                           new Vector3(0.14f, 0.5f, 0.14f), skin, new Vector3(80f, k * 72f, 0));
                else
                    foreach (float x in new[] { -0.12f, 0.12f })
                        NP(hips, "Foot", BlobMesh(), new Vector3(x, -1.02f, -d * 1.12f), new Vector3(0.18f, 0.11f, 0.24f), dark);
            }
            else
            {
                Material legs = L.outfit == NpcOutfit.Overalls ? main : (L.outfit == NpcOutfit.Armor ? metal : NpcMat(L, "Trousers", Color.Lerp(L.second, new Color(0.2f, 0.18f, 0.16f), 0.65f)));
                foreach (float x in new[] { -0.13f, 0.13f })
                {
                    NP(hips, "Leg", CylMesh(), new Vector3(x, -0.5f, 0), new Vector3(0.21f, 0.5f, 0.23f), legs);
                    if (L.species == NpcSpecies.Draconian)
                    {
                        // Bare clawed feet.
                        NP(hips, "Foot", BlobMesh(), new Vector3(x, -0.99f, -0.1f), new Vector3(0.24f, 0.13f, 0.38f), skin);
                        for (int k = -1; k <= 1; k++)
                            Spike(hips, "ToeClaw", new Vector3(x * 1.1f + k * 0.065f, -1.0f, -0.27f), new Vector3(k * 0.25f, -0.2f, -1f), 0.11f, 0.045f, NpcMat(L, "Horn", L.horn));
                        Spike(hips, "HeelSpur", new Vector3(x, -0.97f, 0.06f), new Vector3(0, -0.3f, 1f), 0.08f, 0.04f, NpcMat(L, "Horn", L.horn));
                    }
                    else NP(hips, "Boot", BlobMesh(), new Vector3(x, -0.98f, -0.05f), new Vector3(0.25f, 0.17f, 0.36f), L.outfit == NpcOutfit.Armor ? metal : leather);
                }
                if (L.outfit == NpcOutfit.Coat)
                {
                    float tailBottom = 0.05f - 0.75f * L.coatLength;
                    Flare(hips, "CoatTails", w, d, new Vector2(w * 1.55f, d * 1.9f), 0.05f, tailBottom, main);
                    if (L.coatLength > 1f)
                    {
                        // A long coat: open down the front, edged and hemmed in gold. The edges lie on the coat's front,
                        // which leans out from the waist (the flare's front point) to the hem.
                        float hgt = 0.05f - tailBottom, zTop = -d * 1.05f / 2f, zBot = -d * 1.9f / 2f;
                        float lean = Mathf.Atan2(zTop - zBot, hgt) * Mathf.Rad2Deg, len = Mathf.Sqrt(hgt * hgt + (zTop - zBot) * (zTop - zBot));
                        foreach (float x in new[] { -1f, 1f })
                            NBox(hips, "CoatEdge", new Vector3(x * w * 0.07f, (0.05f + tailBottom) / 2f, (zTop + zBot) / 2f * 0.97f - 0.012f),
                                new Vector3(0.045f, len, 0.02f), trim, new Vector3(lean, 0, 0));
                        NP(hips, "CoatHem", CylMesh(), new Vector3(0, tailBottom + 0.02f, 0), new Vector3(w * 1.57f, 0.03f, d * 1.92f), trim);
                    }
                }
                if (L.outfit == NpcOutfit.Tunic) Flare(hips, "TunicSkirt", w, d, new Vector2(w * 1.4f, d * 1.75f), 0.05f, -0.425f, main);
                if (L.outfit == NpcOutfit.Armor)
                {
                    Flare(hips, "Tassets", w, d, new Vector2(w * 1.5f, d * 1.8f), 0.05f, -0.49f, metal);
                    NBox(hips, "Tabard", new Vector3(0, -0.28f, -d * 0.82f), new Vector3(w * 0.55f, 0.62f, 0.03f), main);
                }
            }

            // ---- torso
            var spine = Pivot(hips, "Spine", Vector3.zero, L.build == NpcBuild.Elder ? new Vector3(-11f, 0, 0) : Vector3.zero);
            Material torsoMat = L.outfit == NpcOutfit.Vest || L.outfit == NpcOutfit.Overalls ? second : (L.outfit == NpcOutfit.Armor ? metal : main);
            if (L.outfit == NpcOutfit.Bark) torsoMat = skin;
            NP(spine, "Torso", CylMesh(), new Vector3(0, 0.34f, 0), new Vector3(w, 0.34f, d), torsoMat);
            NP(spine, "Chest", BlobMesh(), new Vector3(0, 0.6f, 0), new Vector3(w * 1.05f, 0.24f, d * 1.05f), torsoMat);
            if (L.build == NpcBuild.Stout || L.build == NpcBuild.Hulking)
                NP(spine, "Belly", BlobMesh(), new Vector3(0, 0.24f, -d * 0.18f), new Vector3(w * 1.08f, 0.55f, d * 1.3f), torsoMat);
            if (L.outfit == NpcOutfit.Vest)
            {
                NP(spine, "Vest", CylMesh(), new Vector3(0, 0.36f, 0), new Vector3(w * 1.05f, 0.3f, d * 1.06f), main);
                NBox(spine, "Shirt", new Vector3(0, 0.42f, -d * 0.54f), new Vector3(0.12f, 0.46f, 0.02f), second);
                for (int k = 0; k < 3; k++) NP(spine, "Button", BlobMesh(), new Vector3(0.09f, 0.2f + k * 0.14f, -d * 0.55f), Vector3.one * 0.04f, gold);
            }
            if (L.outfit == NpcOutfit.Overalls)
            {
                NBox(spine, "Bib", new Vector3(0, 0.28f, -d * 0.53f), new Vector3(w * 0.6f, 0.4f, 0.02f), main);
                foreach (float x in new[] { -0.1f, 0.1f }) NBox(spine, "Strap", new Vector3(x, 0.55f, -d * 0.5f), new Vector3(0.05f, 0.25f, 0.02f), main, new Vector3(0, 0, x * 60f));
            }
            if (L.outfit == NpcOutfit.Coat)
            {
                NBox(spine, "Lapels", new Vector3(0, 0.45f, -d * 0.53f), new Vector3(0.16f, 0.4f, 0.02f), second);
                for (int k = 0; k < 3; k++) NP(spine, "Button", BlobMesh(), new Vector3(-0.1f, 0.15f + k * 0.14f, -d * 0.54f), Vector3.one * 0.045f, gold);
            }
            if (L.outfit == NpcOutfit.Armor)
            {
                NP(spine, "Breastplate", BlobMesh(), new Vector3(0, 0.42f, -d * 0.2f), new Vector3(w * 1.02f, 0.62f, d * 0.95f), metal);
                NBox(spine, "TabardTop", new Vector3(0, 0.3f, -d * 0.62f), new Vector3(w * 0.55f, 0.5f, 0.03f), main);
            }
            if (L.outfit == NpcOutfit.Bark)
                for (int k = 0; k < 4; k++)   // a few ridges of bark
                    NBox(spine, "BarkRidge", new Vector3(-w * 0.3f + k * w * 0.2f, 0.32f, -d * 0.5f), new Vector3(0.05f, 0.55f, 0.04f), NpcMat(L, "BarkDark", Color.Lerp(L.skin, Color.black, 0.35f)));
            if (!L.ghost && L.outfit != NpcOutfit.Bark)
            {
                NP(spine, "Belt", CylMesh(), new Vector3(0, 0.03f, 0), new Vector3(w * 1.05f, 0.05f, d * 1.07f), leather);
                NBox(spine, "Buckle", new Vector3(0, 0.03f, -d * 0.55f), new Vector3(0.1f, 0.08f, 0.02f), gold);
            }
            if (L.sash) NBox(spine, "Sash", new Vector3(0, 0.36f, -d * 0.53f), new Vector3(0.1f, 0.85f, 0.02f), extra, new Vector3(0, 0, 33f));
            if (L.apron)
            {
                if (skirt) NBox(spine, "Apron", new Vector3(0, -0.47f, -d * 0.92f), new Vector3(w * 0.85f, 0.98f, 0.02f), extra, new Vector3(14f, 0, 0));
                else NBox(spine, "Apron", new Vector3(0, -0.22f, -d * 0.6f), new Vector3(w * 0.85f, 0.95f, 0.02f), extra);
                NBox(spine, "ApronBib", new Vector3(0, 0.36f, -d * 0.56f), new Vector3(w * 0.55f, 0.36f, 0.02f), extra);
            }
            if (L.badge) NP(spine, "Badge", GemMesh(), new Vector3(0.12f, 0.5f, -d * 0.56f), new Vector3(0.09f, 0.1f, 0.03f), gold, new Vector3(90f, 0, 0));
            if (L.satchel)
            {
                NBox(spine, "Strap", new Vector3(0, 0.35f, -d * 0.55f), new Vector3(0.05f, 0.9f, 0.02f), leather, new Vector3(0, 0, -35f));
                NBox(spine, "Satchel", new Vector3(-w * 0.55f, -0.05f, -0.05f), new Vector3(0.12f, 0.24f, 0.3f), leather);
            }
            if (L.cape || L.starMantle)
            {
                Material capeMat = L.starMantle ? second : extra;
                NBox(spine, "Cape", new Vector3(0, -0.2f, d * 0.6f), new Vector3(w * 1.3f, 1.6f, 0.04f), capeMat, new Vector3(-4f, 0, 0));
                if (L.starMantle)
                {
                    NP(spine, "Mantle", BlobMesh(), new Vector3(0, 0.6f, 0.02f), new Vector3(w * 1.5f, 0.24f, d * 1.55f), capeMat);
                    // Stars scattered on the lining.
                    var rng = new System.Random(7);
                    for (int k = 0; k < 9; k++)
                        NP(spine, "Star", GemMesh(), new Vector3((float)(rng.NextDouble() - 0.5) * w * 1.1f, -0.85f + (float)rng.NextDouble() * 1.3f, d * 0.6f + 0.03f),
                           new Vector3(0.07f, 0.09f, 0.02f), gold, new Vector3(90f, 0, 0));
                }
            }
            if (L.highCollar) NP(spine, "Collar", ConeMesh(), new Vector3(0, 0.74f, 0.03f), new Vector3(w * 0.75f, 0.2f, d * 0.95f), trim, new Vector3(180f, 0, 0));
            if (L.furTrim) NP(spine, "FurCollar", BlobMesh(), new Vector3(0, 0.66f, 0), new Vector3(w * 1.05f, 0.14f, d * 1.15f), NpcMat(L, "Fur", new Color(0.93f, 0.93f, 0.95f)));
            if (L.scarf)
            {
                NP(spine, "Scarf", CylMesh(), new Vector3(0, 0.7f, 0), new Vector3(0.32f, 0.06f, 0.32f), extra);
                NBox(spine, "ScarfTail", new Vector3(0.08f, 0.45f, -d * 0.56f), new Vector3(0.09f, 0.42f, 0.02f), extra, new Vector3(0, 0, -6f));
            }
            if (L.epaulettes || L.stony)
                foreach (float x in new[] { -1f, 1f })
                    NP(spine, L.stony ? "Boulder" : "Epaulette", BlobMesh(), new Vector3(x * B.sh, 0.66f, 0), L.stony ? new Vector3(0.36f, 0.3f, 0.36f) : new Vector3(0.2f, 0.07f, 0.2f),
                       L.stony ? NpcMat(L, "Stone", new Color(0.42f, 0.4f, 0.38f)) : gold, new Vector3(0, 0, x * 12f));
            if (L.stony)
                for (int k = 0; k < 3; k++)   // crystals growing out of its shoulders
                    NP(spine, "Crystal" + k, GemMesh(), new Vector3((k - 1) * B.sh * 0.8f, 0.82f, 0.08f), new Vector3(0.1f, 0.26f, 0.1f), NpcMat(L, "Crystal", L.gem, 0.6f), new Vector3(0, 0, (k - 1) * 18f));
            float neckW = L.species == NpcSpecies.Minotaur ? 0.28f : L.species == NpcSpecies.Unicorn ? 0.22f : L.species == NpcSpecies.Draconian ? 0.17f : 0.16f;
            // A Draconian's long neck carries the head up and out in front of the shoulders.
            Vector3 neckAt = L.species == NpcSpecies.Draconian ? new Vector3(0, 0.9f, -0.1f) : new Vector3(0, 0.74f, 0);
            if (L.species == NpcSpecies.Draconian)
                NP(spine, "Neck", CylMesh(), new Vector3(0, 0.79f, -0.05f), new Vector3(neckW, 0.14f, neckW), skin, new Vector3(-24f, 0, 0));
            else NP(spine, "Neck", CylMesh(), new Vector3(0, 0.72f, 0), new Vector3(neckW, 0.07f, neckW), skin);

            // ---- head
            var neck = Pivot(spine, "Neck", neckAt, L.build == NpcBuild.Elder ? new Vector3(9f, 0, 0) : Vector3.zero);
            var head = Pivot(neck, "Head", Vector3.zero);
            var hs = Pivot(head, "HeadShape", Vector3.zero);
            hs.localScale = Vector3.one * B.head;
            float hatBase = BuildHead(hs, L, skin, hairMat, beardMat, gold, metal, dark);

            // ---- arms (right is the model's -X; arms hang a little out from the body)
            Transform handR = BuildArm(spine, "R", -1f, B, L, L.right, main, hands);
            Transform handL = BuildArm(spine, "L", 1f, B, L, L.left, main, hands);
            if (L.right != NpcItem.None) BuildItem(handR, model, L.right, L, -1f);
            if (L.left != NpcItem.None) BuildItem(handL, model, L.left, L, 1f);
            if (L.species != NpcSpecies.Human) BuildSpeciesBody(hips, spine, handR, handL, B, L, skin, hairMat, gold);

            foreach (var t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = false;
            foreach (var r in model.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var anim = model.gameObject.AddComponent<Animator>();
            anim.runtimeAnimatorController = NpcController(L.ghost ? 3 : (L.idle >= 0 ? L.idle : Mathf.Abs(name.GetHashCode()) % 3));
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.CullCompletely;

            // How tall it stands (hat included) and where its face is, in the parent's space.
            float headTop = 1.05f + neckAt.y + (0.49f + hatBase) * B.head;
            if (L.build == NpcBuild.Elder) headTop -= 0.06f;
            top = headTop * B.scale + (L.ghost ? 0.15f : 0f) + L.stand;
            faceY = (1.05f + neckAt.y + 0.25f * B.head) * B.scale + (L.ghost ? 0.15f : 0f) + L.stand;
            NameTag(root, "NameTag", new Vector3(0, top + 0.4f, 0), title);
            HintTag(root, "TalkHint", new Vector3(0, top + 0.08f, 0), hint);
            var box = root.gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0, top * 0.5f, 0);
            box.size = new Vector3(Mathf.Max(1.1f, w * 2.2f * B.scale), top, Mathf.Max(1.1f, d * 2.6f * B.scale));
            return root;
        }

        /// <summary>
        /// The head under its pivot: face, hair, beard (or another species' head), glasses, hat. Returns how far the hat,
        /// horns or ears rise above the head (unscaled).
        /// </summary>
        private static float BuildHead(Transform h, NpcLook L, Material skin, Material hairMat, Material beardMat, Material gold, Material metal, Material dark)
        {
            float rise = 0f;
            if (L.species == NpcSpecies.Human) BuildHumanFace(h, L, skin, hairMat, beardMat);
            else rise = BuildSpeciesHead(h, L, skin, hairMat, gold, dark);

            if (L.spectacles)
                foreach (float x in new[] { -1f, 1f })
                {
                    NP(h, "Lens", CylMesh(), new Vector3(x * 0.077f, 0.275f, -0.2f), new Vector3(0.1f, 0.006f, 0.1f), NpcMat(L, "Glass", new Color(0.78f, 0.88f, 0.95f), 0.1f), new Vector3(90f, 0, 0));
                    NBox(h, "Bridge", new Vector3(0, 0.28f, -0.215f), new Vector3(0.06f, 0.012f, 0.012f), gold);
                }
            if (L.goggles)
            {
                NP(h, "GoggleBand", CylMesh(), new Vector3(0, 0.37f, 0.01f), new Vector3(0.5f, 0.03f, 0.5f), NpcMat(L, "Leather", new Color(0.36f, 0.22f, 0.12f)));
                foreach (float x in new[] { -1f, 1f })
                    NP(h, "Goggle", CylMesh(), new Vector3(x * 0.085f, 0.39f, -0.215f), new Vector3(0.11f, 0.035f, 0.11f), NpcMat(L, "Brass", new Color(0.78f, 0.6f, 0.3f)), new Vector3(70f, 0, 0));
            }
            return Mathf.Max(rise, BuildHat(h, L, gold, metal, dark));
        }

        // ================================================================== other peoples

        /// <summary>A cone of length 'len' from 'from' pointing along 'dir' (horns, spikes, ear ribs).</summary>
        private static GameObject Spike(Transform t, string name, Vector3 from, Vector3 dir, float len, float width, Material mat)
        {
            dir.Normalize();
            return NP(t, name, ConeMesh(), from + dir * (len / 2f), new Vector3(width, len, width), mat, Quaternion.FromToRotation(Vector3.up, dir).eulerAngles);
        }

        private static Material Belly(NpcLook L)
        {
            return NpcMat(L, "Belly", L.belly.r < 0f ? Color.Lerp(L.skin, Color.white, 0.45f) : L.belly);
        }

        /// <summary>A pair of eyes: an iris, a pupil (a slit for reptiles), a glint, lashes if asked.</summary>
        private static void SpeciesEyes(Transform h, NpcLook L, float x, float y, float z, Vector3 size, Color iris, float glow, bool slit, bool lashes)
        {
            var irisMat = NpcMat(L, "Iris", iris, glow);
            var pupil = NpcMat(L, "Pupil", new Color(0.05f, 0.04f, 0.06f));
            var shine = NpcMat(L, "EyeShine", Color.white, 0.3f);
            foreach (float sx in new[] { -1f, 1f })
            {
                NP(h, "Eye", BlobMesh(), new Vector3(sx * x, y, z), size, irisMat);
                if (slit) NBox(h, "Pupil", new Vector3(sx * x, y, z - size.z * 0.48f), new Vector3(size.x * 0.18f, size.y * 0.8f, 0.01f), pupil);
                else NP(h, "Pupil", BlobMesh(), new Vector3(sx * x, y, z - size.z * 0.42f), new Vector3(size.x * 0.5f, size.y * 0.6f, size.z * 0.4f), pupil);
                NP(h, "EyeShine", BlobMesh(), new Vector3(sx * x - size.x * 0.2f, y + size.y * 0.22f, z - size.z * 0.55f), Vector3.one * Mathf.Min(size.x, size.y) * 0.3f, shine);
                if (lashes)
                    NBox(h, "Lash", new Vector3(sx * (x + size.x * 0.25f), y + size.y * 0.52f, z - size.z * 0.2f), new Vector3(size.x * 0.9f, 0.014f, 0.012f), pupil, new Vector3(0, 0, sx * 18f));
                NBox(h, "Brow", new Vector3(sx * x, y + size.y * 0.75f, z - size.z * 0.1f), new Vector3(size.x * 1.25f, 0.024f, 0.03f),
                    NpcMat(L, "BrowRidge", Color.Lerp(L.skin, Color.black, 0.2f)), new Vector3(0, 0, sx * L.brows));
            }
        }

        /// <summary>
        /// The head of one of the world's other peoples, facing -Z like a person's (hats, goggles and glasses still fit).
        /// Returns how far horns, crests or ears rise above the top of a person's head (0.49).
        ///   Draconian: a crimson dragon's head, a long snout, slit ember eyes, two pairs of swept-back horns and a crest.
        ///   Minotaur: a broad bull's head with a pale muzzle and gold nose ring, ears out sideways and great curling horns.
        ///   Kobold: a little scaly head with a short snout, huge bright eyes, big frilled ears and a gold tooth (Bigdig's people).
        ///   Unicorn: a horse's long gentle face, upright ears, a flowing two-coloured mane and a spiral horn that glows.
        /// </summary>
        private static float BuildSpeciesHead(Transform h, NpcLook L, Material skin, Material hairMat, Material gold, Material dark)
        {
            var belly = Belly(L);
            var horn = NpcMat(L, "Horn", L.horn);
            var nostril = NpcMat(L, "Nostril", new Color(0.12f, 0.07f, 0.07f));
            switch (L.species)
            {
                case NpcSpecies.Draconian:
                {
                    NP(h, "HeadBall", BlobMesh(), new Vector3(0, 0.28f, 0.05f), new Vector3(0.4f, 0.38f, 0.46f), skin);
                    NP(h, "Snout", BlobMesh(), new Vector3(0, 0.22f, -0.25f), new Vector3(0.24f, 0.18f, 0.48f), skin);
                    NP(h, "Jaw", BlobMesh(), new Vector3(0, 0.13f, -0.21f), new Vector3(0.21f, 0.1f, 0.42f), belly);
                    // Bony ridges along the top of the snout, up to the brow.
                    Spike(h, "SnoutRidge", new Vector3(0, 0.29f, -0.36f), new Vector3(0, 1f, 0.6f), 0.06f, 0.04f, horn);
                    Spike(h, "SnoutRidge", new Vector3(0, 0.31f, -0.25f), new Vector3(0, 1f, 0.6f), 0.07f, 0.045f, horn);
                    foreach (float x in new[] { -1f, 1f })
                    {
                        NP(h, "Nostril", BlobMesh(), new Vector3(x * 0.04f, 0.27f, -0.47f), Vector3.one * 0.028f, nostril);
                        Spike(h, "Fang", new Vector3(x * 0.07f, 0.16f, -0.36f), Vector3.down, 0.05f, 0.025f, NpcMat(L, "Fang", new Color(0.96f, 0.93f, 0.85f)));
                        // Long horns swept back from the brow, a shorter pair under them.
                        Spike(h, "Horn", new Vector3(x * 0.11f, 0.42f, 0.02f), new Vector3(x * 0.25f, 0.45f, 0.85f), 0.4f, 0.085f, horn);
                        Spike(h, "HornSmall", new Vector3(x * 0.16f, 0.32f, 0.1f), new Vector3(x * 0.5f, 0.2f, 0.85f), 0.22f, 0.06f, horn);
                        // Fins swept back from the cheeks, like ears.
                        NP(h, "CheekFin", BlobMesh(), new Vector3(x * 0.2f, 0.24f, 0.1f), new Vector3(0.035f, 0.16f, 0.24f), hairMat, new Vector3(-10f, x * 22f, 0));
                        Spike(h, "FinSpine", new Vector3(x * 0.18f, 0.2f, 0.02f), new Vector3(x * 0.35f, 0.25f, 1f), 0.24f, 0.03f, horn);
                    }
                    SpeciesEyes(h, L, 0.1f, 0.33f, -0.13f, new Vector3(0.075f, 0.055f, 0.04f), L.gem, 0.8f, true, false);
                    // A crest of spines down the back of the head.
                    Spike(h, "Crest", new Vector3(0, 0.47f, 0.08f), new Vector3(0, 1f, 0.6f), 0.12f, 0.06f, hairMat);
                    Spike(h, "Crest", new Vector3(0, 0.42f, 0.2f), new Vector3(0, 0.7f, 0.8f), 0.1f, 0.05f, hairMat);
                    Spike(h, "Crest", new Vector3(0, 0.33f, 0.28f), new Vector3(0, 0.4f, 1f), 0.08f, 0.045f, hairMat);
                    return 0.12f;
                }
                case NpcSpecies.Minotaur:
                {
                    NP(h, "HeadBall", BlobMesh(), new Vector3(0, 0.29f, 0.02f), new Vector3(0.5f, 0.48f, 0.52f), skin);
                    NP(h, "BrowRidge", BlobMesh(), new Vector3(0, 0.36f, -0.17f), new Vector3(0.4f, 0.08f, 0.14f), skin);
                    NP(h, "Muzzle", BlobMesh(), new Vector3(0, 0.17f, -0.23f), new Vector3(0.36f, 0.26f, 0.3f), belly);
                    NP(h, "NosePad", BlobMesh(), new Vector3(0, 0.2f, -0.37f), new Vector3(0.24f, 0.12f, 0.06f), NpcMat(L, "NosePad", Color.Lerp(L.belly.r < 0f ? L.skin : L.belly, new Color(0.35f, 0.25f, 0.28f), 0.6f)));
                    NP(h, "NoseRing", CylMesh(), new Vector3(0, 0.115f, -0.385f), new Vector3(0.1f, 0.012f, 0.1f), gold, new Vector3(90f, 0, 0));
                    foreach (float x in new[] { -1f, 1f })
                    {
                        NP(h, "Nostril", BlobMesh(), new Vector3(x * 0.055f, 0.2f, -0.4f), Vector3.one * 0.035f, nostril);
                        NP(h, "Ear", BlobMesh(), new Vector3(x * 0.3f, 0.33f, 0.02f), new Vector3(0.17f, 0.07f, 0.1f), skin, new Vector3(0, 0, x * -25f));
                        NP(h, "EarInner", BlobMesh(), new Vector3(x * 0.31f, 0.33f, -0.01f), new Vector3(0.12f, 0.045f, 0.04f), NpcMat(L, "EarInner", new Color(0.82f, 0.6f, 0.6f)), new Vector3(0, 0, x * -25f));
                        // The horns: out sideways from the brow, then curling up, banded and tipped in gold.
                        Vector3 baseA = new Vector3(x * 0.2f, 0.47f, 0.02f), dirA = new Vector3(x, 0.25f, 0f).normalized;
                        Spike(h, "Horn", baseA, dirA, 0.22f, 0.11f, horn);
                        Vector3 baseB = baseA + dirA * 0.18f;
                        Spike(h, "HornTip", baseB, new Vector3(x * 0.35f, 1f, -0.2f), 0.22f, 0.08f, horn);
                        NP(h, "HornBand", CylMesh(), baseA + dirA * 0.05f, new Vector3(0.12f, 0.018f, 0.12f), gold, Quaternion.FromToRotation(Vector3.up, dirA).eulerAngles);
                    }
                    SpeciesEyes(h, L, 0.13f, 0.3f, -0.2f, new Vector3(0.06f, 0.07f, 0.035f), new Color(0.24f, 0.14f, 0.08f), 0f, false, L.feminine);
                    NP(h, "Forelock", BlobMesh(), new Vector3(0, 0.47f, -0.06f), new Vector3(0.2f, 0.1f, 0.18f), hairMat);
                    if (L.hairStyle != NpcHair.Bald) NBox(h, "Mane", new Vector3(0, 0.1f, 0.17f), new Vector3(0.4f, 0.5f, 0.12f), hairMat);
                    return 0.23f;
                }
                case NpcSpecies.Kobold:
                {
                    NP(h, "HeadBall", BlobMesh(), new Vector3(0, 0.27f, 0f), new Vector3(0.44f, 0.42f, 0.44f), skin);
                    NP(h, "Snout", BlobMesh(), new Vector3(0, 0.19f, -0.2f), new Vector3(0.24f, 0.17f, 0.24f), skin);
                    NP(h, "Chin", BlobMesh(), new Vector3(0, 0.14f, -0.19f), new Vector3(0.2f, 0.09f, 0.2f), belly);
                    NBox(h, "Grin", new Vector3(0, 0.135f, -0.305f), new Vector3(0.1f, 0.012f, 0.01f), nostril);
                    NBox(h, "GoldTooth", new Vector3(0.035f, 0.122f, -0.307f), new Vector3(0.024f, 0.032f, 0.012f), gold);
                    var frill = NpcMat(L, "Frill", L.extra);
                    var rib = NpcMat(L, "FrillRib", Color.Lerp(L.skin, Color.black, 0.25f));
                    foreach (float x in new[] { -1f, 1f })
                    {
                        NP(h, "Nostril", BlobMesh(), new Vector3(x * 0.035f, 0.22f, -0.32f), Vector3.one * 0.022f, nostril);
                        // Big frilled ears: a fan of thin ribs with skin stretched between them.
                        Vector3 root = new Vector3(x * 0.2f, 0.32f, 0.03f);
                        NP(h, "Frill", BlobMesh(), root + new Vector3(x * 0.13f, 0.06f, 0.07f), new Vector3(0.03f, 0.26f, 0.22f), frill, new Vector3(0, x * -20f, x * -35f));
                        Spike(h, "FrillRib", root, new Vector3(x, 0.5f, 0.3f), 0.3f, 0.025f, rib);
                        Spike(h, "FrillRib", root, new Vector3(x, 0.12f, 0.5f), 0.28f, 0.025f, rib);
                        Spike(h, "FrillRib", root, new Vector3(x * 0.85f, 0.9f, 0.2f), 0.26f, 0.025f, rib);
                    }
                    SpeciesEyes(h, L, 0.105f, 0.31f, -0.17f, new Vector3(0.1f, 0.1f, 0.05f), L.eyes, 0.25f, false, L.feminine);
                    Spike(h, "Crest", new Vector3(0, 0.47f, -0.02f), new Vector3(0, 1f, 0.3f), 0.12f, 0.05f, hairMat);
                    Spike(h, "Crest", new Vector3(0, 0.46f, 0.08f), new Vector3(0, 1f, 0.6f), 0.1f, 0.045f, hairMat);
                    Spike(h, "Crest", new Vector3(0, 0.41f, 0.17f), new Vector3(0, 0.6f, 0.9f), 0.08f, 0.04f, hairMat);
                    return 0.12f;
                }
                default:   // Unicorn
                {
                    NP(h, "HeadBall", BlobMesh(), new Vector3(0, 0.3f, 0.03f), new Vector3(0.42f, 0.46f, 0.46f), skin);
                    NP(h, "Muzzle", BlobMesh(), new Vector3(0, 0.2f, -0.22f), new Vector3(0.26f, 0.24f, 0.38f), skin);
                    NP(h, "MuzzleTip", BlobMesh(), new Vector3(0, 0.13f, -0.37f), new Vector3(0.22f, 0.18f, 0.16f), belly);
                    var hornMat = NpcMat(L, "UnicornHorn", L.horn, 0.45f);
                    foreach (float x in new[] { -1f, 1f })
                    {
                        NP(h, "Nostril", BlobMesh(), new Vector3(x * 0.05f, 0.15f, -0.44f), Vector3.one * 0.03f, nostril);
                        Spike(h, "Ear", new Vector3(x * 0.13f, 0.48f, 0.06f), new Vector3(x * 0.3f, 1f, 0.1f), 0.18f, 0.08f, skin);
                        Spike(h, "EarInner", new Vector3(x * 0.13f, 0.49f, 0.04f), new Vector3(x * 0.3f, 1f, 0.1f), 0.13f, 0.045f, NpcMat(L, "EarInner", new Color(1f, 0.74f, 0.82f)));
                    }
                    // The horn: a spiral of pearl and gold, glowing softly.
                    Vector3 hb = new Vector3(0, 0.52f, -0.12f), hd = new Vector3(0, 1f, -0.45f).normalized;
                    Spike(h, "Horn", hb, hd, 0.36f, 0.08f, hornMat);
                    for (int k = 0; k < 3; k++)
                        NP(h, "HornSpiral", CylMesh(), hb + hd * (0.06f + k * 0.08f), new Vector3(0.07f - k * 0.016f, 0.012f, 0.07f - k * 0.016f), gold,
                            (Quaternion.FromToRotation(Vector3.up, hd) * Quaternion.Euler(0, 0, 14f)).eulerAngles);
                    SpeciesEyes(h, L, 0.135f, 0.32f, -0.15f, new Vector3(0.075f, 0.085f, 0.04f), L.eyes, 0.1f, false, L.feminine);
                    // The mane: a forelock between the ears, flowing down the back of the head and neck in two colours.
                    var mane2 = L.mane2.r < 0f ? hairMat : NpcMat(L, "Mane2", L.mane2);
                    NP(h, "Forelock", BlobMesh(), new Vector3(0, 0.49f, -0.06f), new Vector3(0.16f, 0.12f, 0.15f), hairMat);
                    for (int k = 0; k < 5; k++)
                        NP(h, "Mane", BlobMesh(), new Vector3(0, 0.5f - k * 0.12f, 0.12f + k * 0.04f), new Vector3(0.17f, 0.18f, 0.13f), k % 2 == 0 ? hairMat : mane2);
                    return 0.34f;
                }
            }
        }

        /// <summary>Tails (and a Draconian's folded wings) for the other peoples, on the hips and back.</summary>
        private static void BuildSpeciesBody(Transform hips, Transform spine, Transform handR, Transform handL, NpcShape B, NpcLook L, Material skin,
            Material hairMat, Material gold)
        {
            float d = B.d;
            if (L.species == NpcSpecies.Draconian && !L.gloves)
                foreach (var hand in new[] { handR, handL })
                    for (int k = -1; k <= 1; k++)   // three hooked claws on each hand
                        Spike(hand, "Claw", new Vector3(k * 0.04f, -0.07f, -0.03f), new Vector3(k * 0.2f, -1f, -0.5f), 0.1f, 0.03f, NpcMat(L, "Horn", L.horn));
            switch (L.species)
            {
                case NpcSpecies.Draconian:
                case NpcSpecies.Kobold:
                {
                    // A scaly tail out of the back of the robe, curving down to the ground, a spade on a Draconian's.
                    var belly = Belly(L);
                    for (int k = 0; k < 4; k++)
                    {
                        float t = k / 3f;
                        NP(hips, "Tail", BlobMesh(), new Vector3(0, -0.32f - 0.17f * k, d * 0.9f + 0.2f * k), new Vector3(0.2f - 0.04f * k, 0.2f - 0.04f * k, 0.3f), k == 0 ? skin : (k % 2 == 0 ? skin : belly),
                            new Vector3(-30f - 12f * k, 0, 0));
                    }
                    if (L.species == NpcSpecies.Draconian)
                    {
                        NP(hips, "TailSpade", GemMesh(), new Vector3(0, -0.92f, d * 0.9f + 0.78f), new Vector3(0.16f, 0.2f, 0.04f), NpcMat(L, "Horn", L.horn), new Vector3(70f, 0, 0));
                        // Great wings spread wide from the shoulder blades: a bony arm out to a clawed wrist, fingers fanning
                        // down, and the membrane stretched between them in scallops. On pivots, so the slouched idle can
                        // flex them as she breathes.
                        var membrane = NpcMat(L, "WingSkin", Color.Lerp(L.skin, new Color(0.3f, 0.08f, 0.04f), 0.3f));
                        var claw = NpcMat(L, "Horn", L.horn);
                        foreach (float x in new[] { -1f, 1f })
                        {
                            var wing = Pivot(spine, x > 0f ? "WingL" : "WingR", new Vector3(x * 0.13f, 0.6f, d * 0.55f), new Vector3(-8f, -x * 15f, 0));
                            wing.localScale = new Vector3(x * 1.25f, 1.25f, 1.25f);
                            BuildWing(wing, skin, membrane, claw);
                        }
                    }
                    break;
                }
                case NpcSpecies.Minotaur:
                {
                    Vector3 dir = new Vector3(0, -0.7f, 0.7f).normalized;
                    NP(hips, "Tail", CylMesh(), new Vector3(0, -0.3f, d * 0.95f) + dir * 0.3f, new Vector3(0.05f, 0.3f, 0.05f), skin, Quaternion.FromToRotation(Vector3.up, dir).eulerAngles);
                    NP(hips, "TailTuft", BlobMesh(), new Vector3(0, -0.3f, d * 0.95f) + dir * 0.64f, new Vector3(0.12f, 0.18f, 0.12f), hairMat);
                    break;
                }
                case NpcSpecies.Unicorn:
                {
                    var mane2 = L.mane2.r < 0f ? hairMat : NpcMat(L, "Mane2", L.mane2);
                    float[] size = { 0.14f, 0.19f, 0.21f, 0.16f };
                    for (int k = 0; k < 4; k++)
                        NP(hips, "Tail", BlobMesh(), new Vector3(0, -0.28f - 0.16f * k, d * 0.95f + 0.1f + 0.1f * k), new Vector3(size[k], size[k] * 1.4f, size[k]), k % 2 == 0 ? hairMat : mane2,
                            new Vector3(-20f, 0, 0));
                    break;
                }
            }
        }

        private static Mesh _wingMesh;

        /// <summary>
        /// One bat-like wing in its own space (out along +X, up +Y, swept back along +Z): bones from the root to the elbow and
        /// a clawed wrist, four fingers fanning down from the wrist, and a two-sided membrane in scallops between them.
        /// </summary>
        private static void BuildWing(Transform wing, Material bone, Material membrane, Material claw)
        {
            Vector3 root = Vector3.zero, elbow = new Vector3(0.32f, 0.3f, 0.1f), wrist = new Vector3(0.62f, 0.62f, 0.16f);
            Vector3[] tips = { new Vector3(1.25f, 0.42f, 0.22f), new Vector3(1.18f, -0.05f, 0.24f), new Vector3(0.86f, -0.42f, 0.22f), new Vector3(0.42f, -0.55f, 0.16f) };
            WingBone(wing, root, elbow, 0.065f, bone);
            WingBone(wing, elbow, wrist, 0.055f, bone);
            foreach (var t in tips) WingBone(wing, wrist, t, 0.026f, bone);
            Spike(wing, "WingClaw", wrist, new Vector3(0.15f, 1f, -0.1f), 0.16f, 0.05f, claw);
            if (_wingMesh == null)
            {
                Vector3 body = new Vector3(0.06f, -0.42f, 0.04f);
                // The edge, round from the elbow: root, body, then each finger tip with a scallop pulled in toward the wrist.
                var edge = new List<Vector3> { elbow, root, body };
                var tipsIn = new List<Vector3>(tips);
                tipsIn.Reverse();
                Vector3 prev = body;
                foreach (var t in tipsIn)
                {
                    Vector3 mid = (prev + t) / 2f;
                    edge.Add(mid + (wrist - mid) * 0.22f);
                    edge.Add(t);
                    prev = t;
                }
                var verts = new List<Vector3>();
                var tris = new List<int>();
                for (int i = 0; i + 1 < edge.Count; i++)
                    foreach (bool back in new[] { false, true })
                    {
                        int v = verts.Count;
                        verts.Add(wrist);
                        verts.Add(back ? edge[i + 1] : edge[i]);
                        verts.Add(back ? edge[i] : edge[i + 1]);
                        tris.AddRange(new[] { v, v + 1, v + 2 });
                    }
                var mesh = new Mesh { name = "NpcDragonWing" };
                mesh.SetVertices(verts);
                mesh.SetTriangles(tris, 0);
                mesh.SetUVs(0, new List<Vector2>(new Vector2[verts.Count]));
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                Directory.CreateDirectory(NpcDir);
                AssetDatabase.CreateAsset(mesh, NpcDir + "/DragonWing.asset");
                _wingMesh = mesh;
            }
            var mgo = new GameObject("WingMembrane");
            mgo.transform.SetParent(wing, false);
            mgo.AddComponent<MeshFilter>().sharedMesh = _wingMesh;
            mgo.AddComponent<MeshRenderer>().sharedMaterial = membrane;
        }

        private static void WingBone(Transform wing, Vector3 a, Vector3 b, float width, Material mat)
        {
            Vector3 dir = b - a;
            NP(wing, "WingBone", CylMesh(), (a + b) / 2f, new Vector3(width, dir.magnitude / 2f, width), mat, Quaternion.FromToRotation(Vector3.up, dir).eulerAngles);
        }

        /// <summary>
        /// A skirt, coat tails, a tunic's skirt or armour's tassets on the hips: a flared tube whose top fits the waist
        /// (just inside the belt, so it meets the torso with no pinch) and whose foot is 'bottom' wide and deep.
        /// </summary>
        private static GameObject Flare(Transform hips, string name, float w, float d, Vector2 bottom, float yTop, float yBottom, Material mat)
        {
            float kx = Mathf.Clamp(w * 1.03f / bottom.x, 0.2f, 1f), kz = Mathf.Clamp(d * 1.05f / bottom.y, 0.2f, 1f);
            return NP(hips, name, FrustumMesh(kx, kz), new Vector3(0, (yTop + yBottom) / 2f, 0), new Vector3(bottom.x, yTop - yBottom, bottom.y), mat);
        }

        /// <summary>Radius 0.5 at the foot (y -0.5), kx and kz of that at the top (y 0.5), 8 sides, closed (shared per shape).</summary>
        private static Mesh FrustumMesh(float kx, float kz)
        {
            kx = Mathf.Round(kx * 20f) / 20f;
            kz = Mathf.Round(kz * 20f) / 20f;
            var t = new List<Vector3>();
            const int sides = 8;
            for (int s = 0; s < sides; s++)
            {
                Vector3 a = Ring(s, sides, 0.5f, -0.5f), b = Ring(s + 1, sides, 0.5f, -0.5f);
                Vector3 c = Ring(s + 1, sides, 0.5f, 0.5f), e = Ring(s, sides, 0.5f, 0.5f);
                c.x *= kx; c.z *= kz; e.x *= kx; e.z *= kz;
                Quad(t, a, b, c, e);
                t.Add(new Vector3(0, 0.5f, 0)); t.Add(e); t.Add(c);
                t.Add(new Vector3(0, -0.5f, 0)); t.Add(a); t.Add(b);
            }
            return SaveMesh("PSX_Flare_" + Mathf.RoundToInt(kx * 100f) + "_" + Mathf.RoundToInt(kz * 100f), t, Vector3.zero);
        }

        /// <summary>A person's face: head, ears, nose, eyes and brows, mouth, hair and beard.</summary>
        private static void BuildHumanFace(Transform h, NpcLook L, Material skin, Material hairMat, Material beardMat)
        {
            NP(h, "HeadBall", BlobMesh(), new Vector3(0, 0.25f, 0), new Vector3(0.46f, 0.5f, 0.46f), skin);
            foreach (float x in new[] { -1f, 1f }) NP(h, "Ear", BlobMesh(), new Vector3(x * 0.232f, 0.25f, 0.01f), new Vector3(0.05f, 0.1f, 0.07f), skin);
            NP(h, "Nose", BlobMesh(), new Vector3(0, 0.22f, -0.235f), new Vector3(0.065f, L.build == NpcBuild.Elder ? 0.09f : 0.07f, 0.07f),
               NpcMat(L, "Nose", L.stony ? new Color(0.44f, 0.42f, 0.4f) : Color.Lerp(L.skin, new Color(0.7f, 0.35f, 0.3f), 0.15f)));
            var eyeMat = NpcMat(L, "Eyes", L.ghost || L.glowEyes ? (L.glowEyes ? L.gem : new Color(0.4f, 0.95f, 0.9f)) : L.eyes, L.glowEyes || L.ghost ? 0.9f : 0f);
            var shine = NpcMat(L, "EyeShine", Color.white, 0.3f);
            foreach (float x in new[] { -1f, 1f })
            {
                NP(h, "Eye", BlobMesh(), new Vector3(x * 0.077f, 0.275f, -0.205f), new Vector3(0.055f, 0.068f, 0.035f), eyeMat);
                if (!L.glowEyes) NP(h, "EyeShine", BlobMesh(), new Vector3(x * 0.077f - 0.012f, 0.29f, -0.222f), Vector3.one * 0.018f, shine);
                NBox(h, "Brow", new Vector3(x * 0.08f, 0.335f, -0.206f), new Vector3(0.085f, 0.02f, 0.02f), L.beardColor.r < 0f ? hairMat : beardMat, new Vector3(0, 0, x * L.brows));
            }
            NBox(h, "Mouth", new Vector3(0, 0.165f, -0.214f), new Vector3(0.075f, 0.013f, 0.01f), NpcMat(L, "Mouth", new Color(0.42f, 0.18f, 0.16f)));

            // Hair (under any hat).
            bool hooded = L.hat == NpcHat.Hood || L.hat == NpcHat.Kerchief || L.hat == NpcHat.Turban || L.hat == NpcHat.Bonnet || L.hat == NpcHat.Helmet ||
                          L.hat == NpcHat.PlumedHelmet || L.hat == NpcHat.MinerLamp || L.hat == NpcHat.FurCap;
            if (L.hairStyle != NpcHair.Bald && !(hooded && L.hairStyle == NpcHair.Short))
                NP(h, "Hair", BlobMesh(), new Vector3(0, 0.31f, 0.025f), new Vector3(0.49f, 0.42f, 0.49f), hairMat);
            switch (L.hairStyle)
            {
                case NpcHair.Bald:
                    if (L.beard != NpcBeard.None || L.build == NpcBuild.Elder)
                        foreach (float x in new[] { -1f, 1f }) NP(h, "Tuft", BlobMesh(), new Vector3(x * 0.21f, 0.26f, 0.07f), new Vector3(0.1f, 0.12f, 0.14f), hairMat);
                    break;
                case NpcHair.Long:
                    NBox(h, "HairBack", new Vector3(0, 0.08f, 0.13f), new Vector3(0.42f, 0.5f, 0.12f), hairMat);
                    break;
                case NpcHair.Bun:
                    NP(h, "Bun", BlobMesh(), new Vector3(0, 0.43f, 0.17f), Vector3.one * 0.19f, hairMat);
                    break;
                case NpcHair.Ponytail:
                    NP(h, "Ponytail", CylMesh(), new Vector3(0, 0.17f, 0.26f), new Vector3(0.09f, 0.18f, 0.09f), hairMat, new Vector3(22f, 0, 0));
                    break;
                case NpcHair.Wild:
                    for (int k = 0; k < 7; k++)
                    {
                        float a = k * 51.4f;
                        Vector3 dir = Quaternion.Euler(0, a, 0) * new Vector3(0, 0.6f, 0.75f);
                        NP(h, "Spike", ConeMesh(), new Vector3(0, 0.33f, 0.03f) + dir * 0.2f, new Vector3(0.13f, 0.22f, 0.13f), hairMat,
                           Quaternion.FromToRotation(Vector3.up, dir).eulerAngles);
                    }
                    break;
                case NpcHair.Braids:
                    foreach (float x in new[] { -1f, 1f })
                    {
                        NP(h, "Braid", CylMesh(), new Vector3(x * 0.19f, 0.02f, -0.02f), new Vector3(0.075f, 0.2f, 0.075f), hairMat);
                        NP(h, "BraidTie", BlobMesh(), new Vector3(x * 0.19f, -0.19f, -0.02f), Vector3.one * 0.06f, NpcMat(L, "Tie", L.trim));
                    }
                    break;
            }

            // Beard and moustache.
            switch (L.beard)
            {
                case NpcBeard.Long:
                    NP(h, "Beard", ConeMesh(), new Vector3(0, 0.0f, -0.13f), new Vector3(0.32f, 0.46f, 0.24f), beardMat, new Vector3(180f, 0, 0));
                    break;
                case NpcBeard.Short:
                    NP(h, "Beard", BlobMesh(), new Vector3(0, 0.12f, -0.13f), new Vector3(0.33f, 0.22f, 0.25f), beardMat);
                    break;
                case NpcBeard.Sideburns:
                    foreach (float x in new[] { -1f, 1f }) NBox(h, "Sideburn", new Vector3(x * 0.215f, 0.2f, -0.04f), new Vector3(0.05f, 0.15f, 0.09f), beardMat);
                    break;
            }
            if (L.beard == NpcBeard.Long || L.beard == NpcBeard.Short || L.beard == NpcBeard.Mustache || L.beard == NpcBeard.Handlebar)
                foreach (float x in new[] { -1f, 1f })
                {
                    NP(h, "Mustache", BlobMesh(), new Vector3(x * 0.052f, 0.19f, -0.225f), new Vector3(0.085f, 0.036f, 0.04f), beardMat, new Vector3(0, 0, x * -14f));
                    if (L.beard == NpcBeard.Handlebar)
                        NP(h, "MustacheTip", ConeMesh(), new Vector3(x * 0.11f, 0.215f, -0.215f), new Vector3(0.03f, 0.06f, 0.03f), beardMat, new Vector3(0, 0, x * -40f));
                }
        }

        /// <summary>The hat. Returns its height above the top of the head.</summary>
        private static float BuildHat(Transform h, NpcLook L, Material gold, Material metal, Material dark)
        {
            if (L.hat == NpcHat.None) return 0f;
            var hp = Pivot(h, "Hat", Vector3.zero);
            hp.localScale = Vector3.one * L.hatScale;
            var hm = NpcMat(L, "Hat", L.hatColor);
            var band = NpcMat(L, "HatBand", L.trim);
            float rise = 0f;
            switch (L.hat)
            {
                case NpcHat.Pointed:
                case NpcHat.StarHat:
                    NP(hp, "Brim", CylMesh(), new Vector3(0, 0.44f, 0), new Vector3(0.8f, 0.025f, 0.8f), hm);
                    NP(hp, "Band", CylMesh(), new Vector3(0, 0.5f, 0), new Vector3(0.47f, 0.04f, 0.47f), band);
                    NP(hp, "Cone", ConeMesh(), new Vector3(0, 0.86f, 0.03f), new Vector3(0.47f, 0.82f, 0.47f), hm, new Vector3(-7f, 0, 0));
                    if (L.hat == NpcHat.StarHat)
                        for (int k = 0; k < 5; k++)
                            NP(hp, "HatStar", GemMesh(), new Vector3(Mathf.Sin(k * 2.4f) * 0.13f * (1f - k * 0.15f), 0.62f + k * 0.11f, -0.16f + k * 0.03f), new Vector3(0.05f, 0.06f, 0.02f), gold, new Vector3(90f, 0, 0));
                    rise = 0.8f;
                    break;
                case NpcHat.Crooked:
                    NP(hp, "Brim", CylMesh(), new Vector3(0, 0.44f, 0), new Vector3(0.82f, 0.025f, 0.82f), hm);
                    NP(hp, "Cone", ConeMesh(), new Vector3(0, 0.72f, 0), new Vector3(0.47f, 0.56f, 0.47f), hm);
                    NP(hp, "Tip", ConeMesh(), new Vector3(0, 1.04f, 0.09f), new Vector3(0.28f, 0.42f, 0.28f), hm, new Vector3(28f, 0, 0));
                    NP(hp, "Band", CylMesh(), new Vector3(0, 0.5f, 0), new Vector3(0.47f, 0.04f, 0.47f), band);
                    rise = 0.75f;
                    break;
                case NpcHat.Hood:
                    NP(hp, "Hood", BlobMesh(), new Vector3(0, 0.29f, 0.1f), new Vector3(0.58f, 0.62f, 0.52f), hm);
                    NP(hp, "HoodTip", ConeMesh(), new Vector3(0, 0.33f, 0.33f), new Vector3(0.2f, 0.3f, 0.2f), hm, new Vector3(65f, 0, 0));
                    rise = 0.06f;
                    break;
                case NpcHat.FlatCap:
                    NP(hp, "Cap", BlobMesh(), new Vector3(0, 0.43f, 0.02f), new Vector3(0.52f, 0.2f, 0.54f), hm);
                    NBox(hp, "Peak", new Vector3(0, 0.41f, -0.25f), new Vector3(0.34f, 0.025f, 0.16f), hm, new Vector3(12f, 0, 0));
                    rise = 0.05f;
                    break;
                case NpcHat.Cowboy:
                    NP(hp, "Brim", CylMesh(), new Vector3(0, 0.44f, 0), new Vector3(0.95f, 0.02f, 0.86f), hm);
                    foreach (float x in new[] { -1f, 1f }) NBox(hp, "BrimCurl", new Vector3(x * 0.42f, 0.47f, 0), new Vector3(0.14f, 0.02f, 0.6f), hm, new Vector3(0, 0, x * -28f));
                    NP(hp, "Crown", CylMesh(), new Vector3(0, 0.57f, 0), new Vector3(0.42f, 0.13f, 0.38f), hm);
                    NP(hp, "Band", CylMesh(), new Vector3(0, 0.47f, 0), new Vector3(0.43f, 0.03f, 0.39f), dark);
                    NBox(hp, "Dent", new Vector3(0, 0.7f, 0), new Vector3(0.05f, 0.02f, 0.28f), dark);
                    rise = 0.22f;
                    break;
                case NpcHat.TopHat:
                    NP(hp, "Brim", CylMesh(), new Vector3(0, 0.44f, 0), new Vector3(0.62f, 0.02f, 0.62f), hm);
                    NP(hp, "Crown", CylMesh(), new Vector3(0, 0.66f, 0), new Vector3(0.4f, 0.22f, 0.4f), hm);
                    NP(hp, "Band", CylMesh(), new Vector3(0, 0.49f, 0), new Vector3(0.41f, 0.04f, 0.41f), band);
                    rise = 0.4f;
                    break;
                case NpcHat.Bowler:
                    NP(hp, "Brim", CylMesh(), new Vector3(0, 0.43f, 0), new Vector3(0.6f, 0.02f, 0.6f), hm);
                    NP(hp, "Dome", BlobMesh(), new Vector3(0, 0.5f, 0), new Vector3(0.47f, 0.32f, 0.47f), hm);
                    NP(hp, "Band", CylMesh(), new Vector3(0, 0.46f, 0), new Vector3(0.46f, 0.03f, 0.46f), band);
                    rise = 0.17f;
                    break;
                case NpcHat.Helmet:
                case NpcHat.PlumedHelmet:
                    NP(hp, "Helmet", BlobMesh(), new Vector3(0, 0.42f, 0.02f), new Vector3(0.53f, 0.38f, 0.53f), metal);
                    NBox(hp, "NoseGuard", new Vector3(0, 0.3f, -0.235f), new Vector3(0.035f, 0.12f, 0.02f), metal);
                    NP(hp, "Rim", CylMesh(), new Vector3(0, 0.35f, 0.02f), new Vector3(0.54f, 0.025f, 0.54f), NpcMat(L, "HatBand", L.hatColor));
                    if (L.hat == NpcHat.PlumedHelmet)
                        NP(hp, "Plume", ConeMesh(), new Vector3(0, 0.7f, 0.06f), new Vector3(0.12f, 0.36f, 0.16f), NpcMat(L, "Plume", L.trim), new Vector3(-22f, 0, 0));
                    rise = L.hat == NpcHat.PlumedHelmet ? 0.38f : 0.12f;
                    break;
                case NpcHat.Kerchief:
                    NP(hp, "Kerchief", BlobMesh(), new Vector3(0, 0.34f, 0.04f), new Vector3(0.51f, 0.38f, 0.51f), hm);
                    NBox(hp, "Knot", new Vector3(0, 0.3f, 0.26f), new Vector3(0.08f, 0.12f, 0.05f), hm, new Vector3(-20f, 0, 0));
                    rise = 0.04f;
                    break;
                case NpcHat.Circlet:
                    NP(hp, "Circlet", CylMesh(), new Vector3(0, 0.37f, 0), new Vector3(0.495f, 0.02f, 0.495f), NpcMat(L, "Circlet", L.hatColor, 0.15f));
                    NP(hp, "CircletGem", GemMesh(), new Vector3(0, 0.39f, -0.25f), new Vector3(0.07f, 0.11f, 0.04f), NpcMat(L, "Gem", L.gem, 0.7f), new Vector3(90f, 0, 0));
                    rise = 0f;
                    break;
                case NpcHat.Ranger:
                    NP(hp, "Brim", CylMesh(), new Vector3(0, 0.44f, 0), new Vector3(1f, 0.02f, 1f), hm);
                    NP(hp, "Crown", BlobMesh(), new Vector3(0, 0.52f, 0), new Vector3(0.45f, 0.22f, 0.45f), hm);
                    NP(hp, "Feather", ConeMesh(), new Vector3(0.21f, 0.63f, 0.1f), new Vector3(0.05f, 0.32f, 0.07f), NpcMat(L, "Plume", L.trim), new Vector3(0, 0, -32f));
                    rise = 0.2f;
                    break;
                case NpcHat.Conductor:
                case NpcHat.Captain:
                    NP(hp, "Crown", CylMesh(), new Vector3(0, 0.5f, 0), new Vector3(0.47f, 0.075f, 0.47f), hm);
                    NP(hp, "Top", CylMesh(), new Vector3(0, 0.57f, 0), new Vector3(L.hat == NpcHat.Captain ? 0.58f : 0.5f, 0.018f, L.hat == NpcHat.Captain ? 0.58f : 0.5f),
                       L.hat == NpcHat.Captain ? NpcMat(L, "Cap", new Color(0.92f, 0.92f, 0.9f)) : hm);
                    NBox(hp, "Visor", new Vector3(0, 0.45f, -0.25f), new Vector3(0.3f, 0.02f, 0.15f), dark, new Vector3(14f, 0, 0));
                    NP(hp, "Band", CylMesh(), new Vector3(0, 0.47f, 0), new Vector3(0.48f, 0.022f, 0.48f), band);
                    NP(hp, "CapBadge", GemMesh(), new Vector3(0, 0.51f, -0.24f), new Vector3(0.06f, 0.07f, 0.02f), gold, new Vector3(90f, 0, 0));
                    rise = 0.1f;
                    break;
                case NpcHat.MinerLamp:
                    NP(hp, "HardHat", BlobMesh(), new Vector3(0, 0.42f, 0.01f), new Vector3(0.53f, 0.32f, 0.53f), hm);
                    NP(hp, "Brim", CylMesh(), new Vector3(0, 0.37f, 0.01f), new Vector3(0.6f, 0.016f, 0.6f), hm);
                    NP(hp, "Lamp", CylMesh(), new Vector3(0, 0.44f, -0.27f), new Vector3(0.1f, 0.04f, 0.1f), metal, new Vector3(90f, 0, 0));
                    NP(hp, "LampGlow", CylMesh(), new Vector3(0, 0.44f, -0.31f), new Vector3(0.075f, 0.005f, 0.075f), NpcMat(L, "LampGlow", new Color(1f, 0.92f, 0.6f), 1.2f), new Vector3(90f, 0, 0));
                    rise = 0.1f;
                    break;
                case NpcHat.Bonnet:
                    NP(hp, "Bonnet", BlobMesh(), new Vector3(0, 0.31f, 0.08f), new Vector3(0.53f, 0.52f, 0.48f), hm);
                    foreach (float x in new[] { -1f, 1f }) NBox(hp, "BonnetSide", new Vector3(x * 0.25f, 0.27f, -0.12f), new Vector3(0.04f, 0.38f, 0.16f), hm);
                    NBox(hp, "BonnetTop", new Vector3(0, 0.48f, -0.12f), new Vector3(0.54f, 0.05f, 0.16f), hm);
                    NBox(hp, "Ribbon", new Vector3(0, 0.06f, -0.12f), new Vector3(0.3f, 0.025f, 0.02f), band);
                    rise = 0.05f;
                    break;
                case NpcHat.FurCap:
                    NP(hp, "Fur", CylMesh(), new Vector3(0, 0.47f, 0), new Vector3(0.54f, 0.14f, 0.54f), NpcMat(L, "Fur", new Color(0.93f, 0.93f, 0.95f)));
                    NP(hp, "Crown", BlobMesh(), new Vector3(0, 0.6f, 0), new Vector3(0.48f, 0.16f, 0.48f), hm);
                    rise = 0.2f;
                    break;
                case NpcHat.Turban:
                    NP(hp, "Wrap1", BlobMesh(), new Vector3(0, 0.42f, 0), new Vector3(0.55f, 0.26f, 0.55f), hm);
                    NP(hp, "Wrap2", BlobMesh(), new Vector3(0, 0.53f, 0.01f), new Vector3(0.47f, 0.22f, 0.47f), hm, new Vector3(0, 0, 8f));
                    NP(hp, "TurbanGem", GemMesh(), new Vector3(0, 0.45f, -0.275f), new Vector3(0.07f, 0.1f, 0.04f), NpcMat(L, "Gem", L.gem, 0.6f), new Vector3(90f, 0, 0));
                    rise = 0.15f;
                    break;
                case NpcHat.Laurel:
                    for (int k = 0; k < 10; k++)
                    {
                        float a = k * 36f * Mathf.Deg2Rad;
                        NP(hp, "LaurelLeaf", GemMesh(), new Vector3(Mathf.Sin(a) * 0.245f, 0.39f, Mathf.Cos(a) * 0.245f), new Vector3(0.05f, 0.1f, 0.03f), NpcMat(L, "Laurel", L.hatColor, 0.2f),
                           new Vector3(0, k * 36f, 60f));
                    }
                    rise = 0f;
                    break;
                case NpcHat.FlowerCrown:
                    Color[] petals = { new Color(1f, 0.6f, 0.75f), new Color(1f, 0.95f, 0.95f), new Color(1f, 0.85f, 0.3f) };
                    for (int k = 0; k < 8; k++)
                    {
                        float a = k * 45f * Mathf.Deg2Rad;
                        Vector3 p = new Vector3(Mathf.Sin(a) * 0.245f, 0.4f, Mathf.Cos(a) * 0.245f);
                        NP(hp, "Blossom", BlobMesh(), p, Vector3.one * 0.08f, NpcMat(L, "Petal", petals[k % 3], 0.1f));
                        NP(hp, "CrownLeaf", GemMesh(), p + new Vector3(0, -0.02f, 0), new Vector3(0.06f, 0.09f, 0.02f), NpcMat(L, "Leaf", new Color(0.3f, 0.6f, 0.25f)), new Vector3(0, k * 45f + 20f, 70f));
                    }
                    rise = 0.03f;
                    break;
                case NpcHat.Veil:
                    NP(hp, "VeilHat", CylMesh(), new Vector3(0, 0.47f, 0.02f), new Vector3(0.36f, 0.04f, 0.36f), hm);
                    NBox(hp, "Veil", new Vector3(0, 0.17f, 0.17f), new Vector3(0.52f, 0.6f, 0.05f), hm);
                    foreach (float x in new[] { -1f, 1f }) NBox(hp, "VeilSide", new Vector3(x * 0.25f, 0.2f, 0.02f), new Vector3(0.04f, 0.5f, 0.3f), hm);
                    rise = 0.04f;
                    break;
                case NpcHat.Straw:
                    NP(hp, "Brim", CylMesh(), new Vector3(0, 0.44f, 0), new Vector3(1f, 0.02f, 1f), hm);
                    NP(hp, "Crown", CylMesh(), new Vector3(0, 0.52f, 0), new Vector3(0.43f, 0.09f, 0.43f), hm);
                    NP(hp, "Band", CylMesh(), new Vector3(0, 0.47f, 0), new Vector3(0.44f, 0.03f, 0.44f), band);
                    rise = 0.13f;
                    break;
                case NpcHat.Leaves:
                    // A tree spirit's crown: clumps of leaves with a few bare twigs poking out.
                    var leaf = NpcMat(L, "Leaves", L.hatColor);
                    var leaf2 = NpcMat(L, "Leaves", Color.Lerp(L.hatColor, new Color(0.7f, 0.85f, 0.3f), 0.35f));
                    for (int k = 0; k < 7; k++)
                    {
                        float a = k * 51.4f * Mathf.Deg2Rad;
                        NP(hp, "LeafClump", BlobMesh(), new Vector3(Mathf.Sin(a) * 0.24f, 0.48f + (k % 2) * 0.08f, Mathf.Cos(a) * 0.24f), Vector3.one * (0.28f + (k % 3) * 0.04f), k % 2 == 0 ? leaf : leaf2);
                    }
                    NP(hp, "LeafTop", BlobMesh(), new Vector3(0, 0.66f, 0), Vector3.one * 0.36f, leaf);
                    for (int k = 0; k < 3; k++)
                        NP(hp, "Twig", ConeMesh(), new Vector3((k - 1) * 0.2f, 0.82f, 0.05f), new Vector3(0.05f, 0.3f, 0.05f), NpcMat(L, "Skin", L.skin), new Vector3(0, 0, (k - 1) * -30f));
                    rise = 0.45f;
                    break;
            }
            return rise * L.hatScale;
        }

        /// <summary>One arm: shoulder (resting a little out from the body), upper arm and sleeve, elbow, forearm and hand. Returns the hand.</summary>
        private static Transform BuildArm(Transform spine, string side, float sx, NpcShape B, NpcLook L, NpcItem item, Material sleeveMat, Material hands)
        {
            bool longItem = LongItem(item), front = HeldInFront(item);
            var shoulder = Pivot(spine, "Shoulder" + side, new Vector3(sx * B.sh, 0.6f, 0), new Vector3(front ? 10f : 0f, 0, sx * (longItem ? 12f : 8f)));
            var arm = Pivot(shoulder, "Arm" + side, Vector3.zero);
            float a = B.arm;
            bool robeSleeve = L.outfit == NpcOutfit.Robe || L.outfit == NpcOutfit.Dress;
            Material upper = L.outfit == NpcOutfit.Vest || L.outfit == NpcOutfit.Overalls ? NpcMat(L, "Second", L.second) :
                             L.outfit == NpcOutfit.Armor ? NpcMat(L, "Metal", new Color(0.62f, 0.64f, 0.68f)) : L.outfit == NpcOutfit.Bark ? hands : sleeveMat;
            NP(arm, "UpperArm", CylMesh(), new Vector3(0, -0.19f, 0), new Vector3(0.15f * a, 0.19f, 0.15f * a), upper);
            var elbow = Pivot(arm, "Elbow" + side, new Vector3(0, -0.38f, 0), new Vector3(longItem ? 62f : (front ? 78f : 6f), 0, 0));
            NP(elbow, "Forearm", CylMesh(), new Vector3(0, -0.16f, 0), new Vector3(0.13f * a, 0.16f, 0.13f * a), upper);
            if (robeSleeve) NP(elbow, "Sleeve", ConeMesh(), new Vector3(0, -0.2f, 0), new Vector3(0.3f * a, 0.4f, 0.3f * a), upper);
            if (L.furTrim) NP(elbow, "Cuff", CylMesh(), new Vector3(0, -0.32f, 0), new Vector3(0.18f * a, 0.03f, 0.18f * a), NpcMat(L, "Fur", new Color(0.93f, 0.93f, 0.95f)));
            var hand = Pivot(elbow, "Hand" + side, new Vector3(0, -0.36f, 0));
            NP(hand, "Hand", BlobMesh(), Vector3.zero, new Vector3(0.13f * a, 0.14f * a, 0.11f * a), hands);
            return hand;
        }

        /// <summary>What the NPC carries, held in 'hand' but built upright (staffs stand straight however the arm hangs).</summary>
        private static void BuildItem(Transform hand, Transform model, NpcItem item, NpcLook L, float sx)
        {
            var it = Child(hand, "Item_" + item, Vector3.zero);
            it.gameObject.isStatic = false;
            it.rotation = model.rotation;
            it.position = hand.position;
            var wood = NpcMat(L, "Wood", new Color(0.5f, 0.32f, 0.17f));
            var darkWood = NpcMat(L, "DarkWood", new Color(0.26f, 0.17f, 0.1f));
            var metal = NpcMat(L, "Metal", new Color(0.62f, 0.64f, 0.68f));
            var iron = NpcMat(L, "Iron", new Color(0.25f, 0.24f, 0.26f));
            var gold = NpcMat(L, "Gold", new Color(0.95f, 0.78f, 0.3f), 0.15f);
            var gem = NpcMat(L, "Gem", L.gem, 0.8f);
            var paper = NpcMat(L, "Paper", new Color(0.93f, 0.88f, 0.74f));
            var warm = NpcMat(L, "LampGlow", new Color(1f, 0.85f, 0.45f), 1.1f);
            var cover = NpcMat(L, "BookCover", Color.Lerp(L.main, new Color(0.35f, 0.15f, 0.1f), 0.4f));
            switch (item)
            {
                case NpcItem.Staff:
                    NP(it, "Pole", CylMesh(), new Vector3(0, -0.02f, 0), new Vector3(0.075f, 1.06f, 0.075f), wood);
                    NP(it, "StaffGem", GemMesh(), new Vector3(0, 1.17f, 0), new Vector3(0.2f, 0.3f, 0.2f), gem);
                    foreach (float x in new[] { -1f, 1f }) NP(it, "Prong", ConeMesh(), new Vector3(x * 0.08f, 1.08f, 0), new Vector3(0.04f, 0.18f, 0.04f), gold, new Vector3(0, 0, x * -20f));
                    break;
                case NpcItem.StarStaff:
                    NP(it, "Pole", CylMesh(), new Vector3(0, 0.08f, 0), new Vector3(0.08f, 1.18f, 0.08f), NpcMat(L, "Silverwood", new Color(0.78f, 0.78f, 0.82f)));
                    foreach (float x in new[] { -1f, 1f }) NP(it, "Crescent", ConeMesh(), new Vector3(x * 0.13f, 1.33f, 0), new Vector3(0.05f, 0.32f, 0.05f), gold, new Vector3(0, 0, x * -28f));
                    NP(it, "StarCrystal", GemMesh(), new Vector3(0, 1.58f, 0), new Vector3(0.24f, 0.4f, 0.24f), gem);
                    NP(it, "StarRing", CylMesh(), new Vector3(0, 1.58f, 0), new Vector3(0.44f, 0.008f, 0.44f), gold, new Vector3(20f, 0, 15f));
                    break;
                case NpcItem.FlameRod:
                    NP(it, "Rod", CylMesh(), new Vector3(0, 0.2f, 0), new Vector3(0.07f, 0.36f, 0.07f), iron);
                    NP(it, "Cup", ConeMesh(), new Vector3(0, 0.6f, 0), new Vector3(0.24f, 0.16f, 0.24f), gold, new Vector3(180f, 0, 0));
                    NP(it, "Flame", ConeMesh(), new Vector3(0, 0.78f, 0), new Vector3(0.18f, 0.32f, 0.18f), NpcMat(L, "Flame", new Color(1f, 0.45f, 0.1f), 1.2f));
                    NP(it, "FlameCore", ConeMesh(), new Vector3(0, 0.74f, 0), new Vector3(0.09f, 0.18f, 0.09f), NpcMat(L, "FlameCore", new Color(1f, 0.92f, 0.6f), 1.3f));
                    break;
                case NpcItem.IceStaff:
                    NP(it, "Pole", CylMesh(), new Vector3(0, -0.02f, 0), new Vector3(0.075f, 1.06f, 0.075f), NpcMat(L, "Silverwood", new Color(0.78f, 0.82f, 0.88f)));
                    for (int k = 0; k < 3; k++)
                        NP(it, "Icicle", GemMesh(), new Vector3((k - 1) * 0.07f, 1.2f + (k == 1 ? 0.08f : 0f), 0), new Vector3(0.12f, k == 1 ? 0.42f : 0.3f, 0.12f), gem, new Vector3(0, k * 40f, (k - 1) * -16f));
                    break;
                case NpcItem.SparkRod:
                    NP(it, "Rod", CylMesh(), new Vector3(0, 0.22f, 0), new Vector3(0.07f, 0.4f, 0.07f), NpcMat(L, "Copper", new Color(0.78f, 0.45f, 0.25f)));
                    for (int k = 0; k < 2; k++) NP(it, "Coil", CylMesh(), new Vector3(0, 0.4f + k * 0.14f, 0), new Vector3(0.16f, 0.015f, 0.16f), gold);
                    NP(it, "Spark", GemMesh(), new Vector3(0, 0.78f, 0), new Vector3(0.16f, 0.26f, 0.16f), gem, new Vector3(0, 30f, 0));
                    break;
                case NpcItem.VineStaff:
                    var vine = NpcMat(L, "Vine", new Color(0.35f, 0.42f, 0.18f));
                    NP(it, "Pole", CylMesh(), new Vector3(0, -0.02f, 0), new Vector3(0.07f, 1.06f, 0.07f), wood, new Vector3(0, 0, 2f));
                    NP(it, "Vine", CylMesh(), new Vector3(0.03f, 0.1f, 0), new Vector3(0.04f, 0.95f, 0.04f), vine, new Vector3(4f, 0, -3f));
                    NP(it, "Blossom", BlobMesh(), new Vector3(0, 1.15f, 0), new Vector3(0.26f, 0.16f, 0.26f), NpcMat(L, "Petal", new Color(1f, 0.6f, 0.75f), 0.2f));
                    NP(it, "BlossomHeart", BlobMesh(), new Vector3(0, 1.21f, 0), Vector3.one * 0.09f, NpcMat(L, "Pollen", new Color(1f, 0.85f, 0.3f), 0.4f));
                    for (int k = 0; k < 3; k++)
                        NP(it, "VineLeaf", GemMesh(), new Vector3(Mathf.Sin(k * 2.1f) * 0.09f, 0.5f + k * 0.2f, Mathf.Cos(k * 2.1f) * 0.09f), new Vector3(0.08f, 0.14f, 0.03f), NpcMat(L, "Leaf", new Color(0.3f, 0.6f, 0.25f)), new Vector3(0, k * 120f, 50f));
                    break;
                case NpcItem.SkullCane:
                    NP(it, "Cane", CylMesh(), new Vector3(0, -0.42f, 0), new Vector3(0.06f, 0.47f, 0.06f), NpcMat(L, "Ebony", new Color(0.12f, 0.1f, 0.12f)));
                    NP(it, "CaneSkull", BlobMesh(), new Vector3(0, 0.1f, 0), new Vector3(0.17f, 0.16f, 0.19f), NpcMat(L, "Bone", new Color(0.88f, 0.85f, 0.74f)));
                    foreach (float x in new[] { -1f, 1f }) NP(it, "SkullEye", BlobMesh(), new Vector3(x * 0.04f, 0.12f, -0.085f), Vector3.one * 0.04f, gem);
                    break;
                case NpcItem.BalanceStaff:
                    NP(it, "Pole", CylMesh(), new Vector3(0, -0.02f, 0), new Vector3(0.075f, 1.06f, 0.075f), wood);
                    NBox(it, "Beam", new Vector3(0, 1.04f, 0), new Vector3(0.52f, 0.03f, 0.03f), gold);
                    foreach (float x in new[] { -1f, 1f })
                    {
                        NBox(it, "String", new Vector3(x * 0.24f, 0.95f, 0), new Vector3(0.008f, 0.18f, 0.008f), gold);
                        NP(it, "Pan", CylMesh(), new Vector3(x * 0.24f, 0.86f, 0), new Vector3(0.17f, 0.012f, 0.17f), gold);
                    }
                    NP(it, "SunDisc", GemMesh(), new Vector3(0, 1.17f, 0), new Vector3(0.18f, 0.22f, 0.08f), gem);
                    break;
                case NpcItem.OpenBook:
                    // Floating open over the upturned hand, glowing.
                    NBox(it, "BookCover", new Vector3(0, 0.12f, -0.12f), new Vector3(0.44f, 0.02f, 0.3f), cover);
                    foreach (float x in new[] { -1f, 1f })
                        NBox(it, "Pages", new Vector3(x * 0.1f, 0.15f, -0.12f), new Vector3(0.2f, 0.025f, 0.28f), NpcMat(L, "MagicPaper", new Color(1f, 0.95f, 0.78f), 0.35f), new Vector3(0, 0, x * -12f));
                    NP(it, "BookGlow", GemMesh(), new Vector3(0, 0.32f, -0.12f), new Vector3(0.08f, 0.12f, 0.08f), gem);
                    break;
                case NpcItem.Halberd:
                    NP(it, "Pole", CylMesh(), new Vector3(0, 0.05f, 0), new Vector3(0.07f, 1.16f, 0.07f), darkWood);
                    NBox(it, "Blade", new Vector3(0.1f, 1.04f, 0), new Vector3(0.22f, 0.3f, 0.02f), metal);
                    NP(it, "Spike", ConeMesh(), new Vector3(0, 1.32f, 0), new Vector3(0.07f, 0.24f, 0.07f), metal);
                    break;
                case NpcItem.Flag:
                    NP(it, "Pole", CylMesh(), new Vector3(0, 0.02f, 0), new Vector3(0.06f, 1.1f, 0.06f), darkWood);
                    NBox(it, "FlagCloth", new Vector3(0.25f * -sx, 0.88f, 0), new Vector3(0.46f, 0.32f, 0.02f), NpcMat(L, "WhiteFlag", new Color(0.95f, 0.95f, 0.93f)));
                    NP(it, "Finial", BlobMesh(), new Vector3(0, 1.13f, 0), Vector3.one * 0.07f, gold);
                    break;
                case NpcItem.Pitchfork:
                    NP(it, "Pole", CylMesh(), new Vector3(0, -0.04f, 0), new Vector3(0.06f, 1.02f, 0.06f), wood);
                    NBox(it, "ForkBar", new Vector3(0, 0.98f, 0), new Vector3(0.26f, 0.03f, 0.03f), iron);
                    for (int k = -1; k <= 1; k++) NP(it, "Tine", ConeMesh(), new Vector3(k * 0.11f, 1.12f, 0), new Vector3(0.03f, 0.26f, 0.03f), iron);
                    break;
                case NpcItem.Pickaxe:
                    NP(it, "Handle", CylMesh(), new Vector3(0, 0.22f, 0), new Vector3(0.06f, 0.42f, 0.06f), wood);
                    NBox(it, "PickHead", new Vector3(0, 0.62f, 0), new Vector3(0.5f, 0.07f, 0.07f), iron);
                    foreach (float x in new[] { -1f, 1f }) NP(it, "PickPoint", ConeMesh(), new Vector3(x * 0.3f, 0.6f, 0), new Vector3(0.06f, 0.16f, 0.06f), iron, new Vector3(0, 0, x * -100f));
                    break;
                case NpcItem.Cane:
                    NP(it, "Cane", CylMesh(), new Vector3(0, -0.42f, 0), new Vector3(0.06f, 0.47f, 0.06f), wood);
                    NP(it, "Crook", BlobMesh(), new Vector3(0, 0.06f, -0.05f), new Vector3(0.1f, 0.08f, 0.17f), wood);
                    break;
                case NpcItem.Book:
                case NpcItem.Ledger:
                    NBox(it, "BookCover", new Vector3(0, 0.02f, -0.08f), new Vector3(0.26f, 0.32f, 0.07f), item == NpcItem.Ledger ? NpcMat(L, "Ledger", new Color(0.25f, 0.3f, 0.2f)) : cover, new Vector3(-20f, 0, 0));
                    NBox(it, "Pages", new Vector3(0.012f, 0.02f, -0.08f), new Vector3(0.24f, 0.3f, 0.06f), paper, new Vector3(-20f, 0, 0));
                    break;
                case NpcItem.Lantern:
                case NpcItem.SignalLamp:
                    NP(it, "LanternRing", CylMesh(), new Vector3(0, -0.05f, 0), new Vector3(0.08f, 0.01f, 0.08f), iron, new Vector3(90f, 0, 0));
                    NBox(it, "LanternFrame", new Vector3(0, -0.24f, 0), new Vector3(0.18f, 0.24f, 0.18f), iron);
                    NBox(it, "LanternGlass", new Vector3(0, -0.24f, 0), new Vector3(0.2f, 0.18f, 0.14f),
                         item == NpcItem.SignalLamp ? NpcMat(L, "RedGlass", new Color(1f, 0.25f, 0.15f), 1.1f) : warm);
                    NP(it, "LanternTop", ConeMesh(), new Vector3(0, -0.08f, 0), new Vector3(0.2f, 0.1f, 0.2f), iron);
                    break;
                case NpcItem.Hammer:
                    NP(it, "Handle", CylMesh(), new Vector3(0, -0.12f, 0), new Vector3(0.05f, 0.24f, 0.05f), wood);
                    NBox(it, "HammerHead", new Vector3(0, -0.38f, 0), new Vector3(0.24f, 0.11f, 0.11f), iron);
                    break;
                case NpcItem.Tongs:
                    foreach (float x in new[] { -1f, 1f }) NBox(it, "Tong", new Vector3(x * 0.03f, -0.25f, 0), new Vector3(0.02f, 0.5f, 0.02f), iron, new Vector3(0, 0, x * 4f));
                    NBox(it, "Jaw", new Vector3(0, -0.5f, 0), new Vector3(0.1f, 0.04f, 0.04f), iron);
                    break;
                case NpcItem.Wrench:
                    NBox(it, "WrenchHandle", new Vector3(0, -0.16f, 0), new Vector3(0.05f, 0.36f, 0.03f), metal);
                    foreach (float x in new[] { -1f, 1f }) NBox(it, "WrenchJaw", new Vector3(x * 0.05f, -0.38f, 0), new Vector3(0.035f, 0.1f, 0.03f), metal);
                    break;
                case NpcItem.Keys:
                    NP(it, "KeyRing", CylMesh(), new Vector3(0, -0.08f, 0), new Vector3(0.13f, 0.008f, 0.13f), gold, new Vector3(90f, 0, 0));
                    for (int k = -1; k <= 1; k++) NBox(it, "Key", new Vector3(k * 0.04f, -0.2f, 0), new Vector3(0.025f, 0.14f, 0.01f), gold, new Vector3(0, 0, k * 14f));
                    break;
                case NpcItem.Scroll:
                    NP(it, "Scroll", CylMesh(), new Vector3(0, 0.03f, -0.08f), new Vector3(0.08f, 0.16f, 0.08f), paper, new Vector3(0, 0, 90f));
                    NP(it, "Seal", CylMesh(), new Vector3(0, 0.03f, -0.13f), new Vector3(0.05f, 0.01f, 0.05f), NpcMat(L, "Seal", new Color(0.7f, 0.12f, 0.1f)), new Vector3(90f, 0, 0));
                    break;
                case NpcItem.Telescope:
                    NP(it, "Tube", CylMesh(), new Vector3(0, 0.12f, -0.08f), new Vector3(0.09f, 0.2f, 0.09f), NpcMat(L, "Brass", new Color(0.78f, 0.6f, 0.3f)), new Vector3(-35f, 0, 0));
                    NP(it, "Tube2", CylMesh(), new Vector3(0, 0.38f, -0.26f), new Vector3(0.065f, 0.14f, 0.065f), NpcMat(L, "Brass", new Color(0.78f, 0.6f, 0.3f)), new Vector3(-35f, 0, 0));
                    NP(it, "TubeBand", CylMesh(), new Vector3(0, 0.27f, -0.18f), new Vector3(0.1f, 0.02f, 0.1f), cover, new Vector3(-35f, 0, 0));
                    break;
                case NpcItem.Trowel:
                    NP(it, "Handle", CylMesh(), new Vector3(0, -0.06f, 0), new Vector3(0.05f, 0.1f, 0.05f), wood);
                    NBox(it, "Blade", new Vector3(0, -0.26f, 0), new Vector3(0.1f, 0.2f, 0.015f), metal);
                    break;
                case NpcItem.Mug:
                    NP(it, "Mug", CylMesh(), new Vector3(0, 0.05f, -0.06f), new Vector3(0.14f, 0.09f, 0.14f), NpcMat(L, "Pewter", new Color(0.6f, 0.6f, 0.62f)));
                    NP(it, "Foam", BlobMesh(), new Vector3(0, 0.15f, -0.06f), new Vector3(0.14f, 0.06f, 0.14f), NpcMat(L, "Foam", new Color(0.97f, 0.95f, 0.88f)));
                    NBox(it, "MugHandle", new Vector3(0.09f * -sx, 0.05f, -0.06f), new Vector3(0.03f, 0.1f, 0.03f), NpcMat(L, "Pewter", new Color(0.6f, 0.6f, 0.62f)));
                    break;
                case NpcItem.Candle:
                    NP(it, "Dish", CylMesh(), new Vector3(0, 0.0f, -0.06f), new Vector3(0.18f, 0.012f, 0.18f), gold);
                    NP(it, "Wax", CylMesh(), new Vector3(0, 0.1f, -0.06f), new Vector3(0.06f, 0.1f, 0.06f), paper);
                    NP(it, "CandleFlame", ConeMesh(), new Vector3(0, 0.25f, -0.06f), new Vector3(0.05f, 0.1f, 0.05f), warm);
                    break;
                case NpcItem.Lasso:
                    for (int k = 0; k < 4; k++)
                        NP(it, "Coil", CylMesh(), new Vector3(0.02f * k * -sx, -0.18f, 0.01f * k), new Vector3(0.32f, 0.012f, 0.32f), NpcMat(L, "Rope", new Color(0.78f, 0.66f, 0.42f)), new Vector3(90f, 0, k * 5f));
                    break;
                case NpcItem.Net:
                    NP(it, "Net", BlobMesh(), new Vector3(0, -0.24f, -0.02f), new Vector3(0.34f, 0.4f, 0.18f), NpcMat(L, "NetRope", new Color(0.62f, 0.55f, 0.4f)));
                    for (int k = 0; k < 3; k++) NP(it, "Float", BlobMesh(), new Vector3((k - 1) * 0.1f, -0.42f, -0.1f), Vector3.one * 0.06f, NpcMat(L, "Cork", new Color(0.85f, 0.45f, 0.2f)));
                    break;
                case NpcItem.Pan:
                    NP(it, "Pan", CylMesh(), new Vector3(0, 0.04f, -0.14f), new Vector3(0.38f, 0.02f, 0.38f), iron, new Vector3(-12f, 0, 0));
                    NP(it, "Nugget", GemMesh(), new Vector3(0.05f, 0.07f, -0.16f), new Vector3(0.05f, 0.04f, 0.05f), gold);
                    break;
                case NpcItem.Quill:
                    NP(it, "Feather", ConeMesh(), new Vector3(0, 0.14f, -0.04f), new Vector3(0.05f, 0.3f, 0.07f), NpcMat(L, "Feather", new Color(0.95f, 0.95f, 0.92f)), new Vector3(-15f, 0, 10f));
                    break;
                case NpcItem.Wand:
                    NP(it, "Wand", CylMesh(), new Vector3(0, 0.1f, -0.08f), new Vector3(0.035f, 0.16f, 0.035f), wood, new Vector3(-35f, 0, 0));
                    NP(it, "WandTip", GemMesh(), new Vector3(0, 0.22f, -0.2f), new Vector3(0.06f, 0.08f, 0.06f), gem, new Vector3(-35f, 0, 0));
                    break;
            }
        }

        /// <summary>Points every NPC click (QuestNpc, NpcInteractions, EventRelay) at its body's Animator. Returns how many.</summary>
        private static int WireTalkGestures(UnityEngine.SceneManagement.Scene scene)
        {
            int n = 0;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var mb in root.GetComponentsInChildren<UdonSharp.UdonSharpBehaviour>(true))
                {
                    Transform model = mb.transform.Find("Model");
                    Animator a = model != null ? model.GetComponent<Animator>() : null;
                    if (a == null) continue;
                    if (mb is QuestNpc) ((QuestNpc)mb).talkAnimator = a;
                    else if (mb is NpcInteractions) ((NpcInteractions)mb).talkAnimator = a;
                    else if (mb is EventRelay) ((EventRelay)mb).talkAnimator = a;
                    else continue;
                    UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(mb);
                    EditorUtility.SetDirty(mb);
                    n++;
                }
            return n;
        }

        // ================================================================== animation

        /// <summary>
        /// The NPC Animator: one of three idles (0-2) or the ghost's float (3), and a "Talk" trigger (a nod, an open hand)
        /// from any state that goes back to idle. Shared by every NPC with that idle.
        /// </summary>
        private static RuntimeAnimatorController NpcController(int idle)
        {
            string key = "Npc_Idle" + idle;
            RuntimeAnimatorController rc;
            if (_npcControllers.TryGetValue(key, out rc)) return rc;
            Directory.CreateDirectory(NpcDir);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(NpcDir + "/" + key + ".controller");
            AnimationClip idleClip = NpcIdleClip(idle), talk = NpcTalkClip(idle == 4);
            // Both set every joint either moves (Talk's first keys are the rest pose), so an NPC switched off mid-gesture
            // (its area culled) doesn't come back with its arm stuck up.
            CompleteClips(new[] { idleClip, talk }, new[] { 1, 0 });
            AssetDatabase.AddObjectToAsset(idleClip, ctrl);
            AssetDatabase.AddObjectToAsset(talk, ctrl);
            ctrl.AddParameter("Talk", AnimatorControllerParameterType.Trigger);
            var sm = ctrl.layers[0].stateMachine;
            var idleState = sm.AddState("Idle");
            idleState.motion = idleClip;
            var talkState = sm.AddState("Talk");
            talkState.motion = talk;
            sm.defaultState = idleState;
            var t = sm.AddAnyStateTransition(talkState);
            t.AddCondition(AnimatorConditionMode.If, 0, "Talk");
            t.duration = 0.15f;
            t.hasExitTime = false;
            t.canTransitionToSelf = false;
            var back = talkState.AddTransition(idleState);
            back.hasExitTime = true;
            back.exitTime = 0.95f;
            back.duration = 0.3f;
            EditorUtility.SetDirty(ctrl);
            _npcControllers[key] = ctrl;
            return ctrl;
        }

        private const string NpcHead = "Hips/Spine/Neck/Head", NpcArmL = "Hips/Spine/ShoulderL/ArmL", NpcArmR = "Hips/Spine/ShoulderR/ArmR";

        private static AnimationClip NpcIdleClip(int v)
        {
            var c = NewClip("NpcIdle" + v, true);
            switch (v)
            {
                case 0:   // breathing, a slow look round to the left and back
                    Wave(c, "Hips", "posy", 6f, 3, 1.05f, 0.008f);
                    Wave(c, "Hips/Spine", "scaley", 6f, 3, 1f, 0.018f);
                    Keys(c, NpcHead, "roty", 0, 0, 1.4f, -16, 2.8f, -16, 3.6f, 8, 5.2f, 8, 6f, 0);
                    Wave(c, NpcHead, "rotx", 6f, 2, 0f, 2.5f);
                    Wave(c, NpcArmL, "rotz", 6f, 2, 0f, 2.5f);
                    Wave(c, NpcArmR, "rotz", 6f, 2, 0f, -2.5f, 0.5f);
                    break;
                case 1:   // shifts its weight from foot to foot, glancing about
                    Wave(c, "Hips", "posy", 7f, 2, 1.05f, 0.01f);
                    Wave(c, "Hips", "rotz", 7f, 1, 0f, 2.2f);
                    Wave(c, "Hips/Spine", "rotz", 7f, 1, 0f, -2.6f);
                    Wave(c, "Hips/Spine", "scaley", 7f, 3, 1f, 0.016f);
                    Keys(c, NpcHead, "roty", 0, 0, 1.2f, 12, 3f, 12, 4f, -8, 6f, -8, 7f, 0);
                    Wave(c, NpcArmL, "rotx", 7f, 1, 0f, -4f);
                    Wave(c, NpcArmR, "rotx", 7f, 1, 0f, -4f, 0.5f);
                    break;
                case 2:   // thoughtful: slow nods, a tilt of the head
                    Wave(c, "Hips", "posy", 5f, 2, 1.05f, 0.008f);
                    Wave(c, "Hips/Spine", "scaley", 5f, 2, 1f, 0.02f);
                    Keys(c, NpcHead, "rotx", 0, 0, 1.2f, 7, 1.8f, 2, 2.6f, 6, 3.6f, 0, 5f, 0);
                    Keys(c, NpcHead, "rotz", 0, 0, 2f, 6, 3.5f, 6, 5f, 0);
                    Wave(c, NpcArmL, "rotz", 5f, 1, 0f, 3f);
                    Wave(c, NpcArmR, "rotz", 5f, 1, 0f, -3f);
                    break;
                case 4:   // slouched: hunched over like a dragon on its hoard, head thrust forward, heavy slow breaths
                    Wave(c, "Hips", "posy", 6f, 2, 1.02f, 0.01f);
                    Wave(c, "Hips", "rotx", 6f, 2, -4f, 1f);
                    Wave(c, "Hips/Spine", "rotx", 6f, 2, -18f, 2f, 0.1f);
                    Wave(c, "Hips/Spine", "scaley", 6f, 2, 1f, 0.02f);
                    Wave(c, "Hips/Spine/Neck", "rotx", 6f, 2, 8f, 1f);
                    Wave(c, NpcHead, "rotx", 6f, 2, 14f, 2f, 0.3f);
                    Keys(c, NpcHead, "roty", 0, 0, 1.6f, -14, 3f, -14, 3.8f, 10, 5.2f, 10, 6f, 0);
                    Wave(c, NpcArmL, "rotx", 6f, 2, 24f, 2f);
                    Wave(c, NpcArmR, "rotx", 6f, 2, 24f, 2f, 0.4f);
                    Wave(c, NpcArmL, "rotz", 6f, 1, 4f, 1.5f);
                    Wave(c, NpcArmR, "rotz", 6f, 1, -4f, 1.5f, 0.5f);
                    // Wings (a Draconian's) lift a little with each breath.
                    Wave(c, "Hips/Spine/WingL", "rotz", 6f, 2, 0f, 4f, 0.15f);
                    Wave(c, "Hips/Spine/WingR", "rotz", 6f, 2, 0f, -4f, 0.15f);
                    break;
                default:   // a ghost: drifting up and down, swaying
                    Wave(c, "Hips", "posy", 4f, 1, 1.12f, 0.1f);
                    Wave(c, "Hips", "rotz", 4f, 1, 0f, 3f, 0.25f);
                    Wave(c, "Hips/Spine", "scaley", 4f, 2, 1f, 0.02f);
                    Keys(c, NpcHead, "roty", 0, 0, 1f, -10, 3f, 10, 4f, 0);
                    Wave(c, NpcArmL, "rotz", 4f, 1, 0f, 6f);
                    Wave(c, NpcArmR, "rotz", 4f, 1, 0f, -6f, 0.5f);
                    break;
            }
            return c;
        }

        /// <summary>The "Talk" gesture: a nod and an open hand. Slouched, the same gesture from the hunch (it never straightens).</summary>
        private static AnimationClip NpcTalkClip(bool slouched = false)
        {
            var c = NewClip(slouched ? "NpcTalkSlouched" : "NpcTalk", false);
            float hx = slouched ? 14f : 0f, ax = slouched ? 24f : 0f;
            Keys(c, NpcHead, "rotx", 0, hx, 0.25f, hx + 9, 0.5f, hx, 0.8f, hx + 7, 1.1f, hx, 2.2f, hx);
            Keys(c, NpcHead, "roty", 0, 0, 2.2f, 0);
            // An open hand: the left arm comes up and out, then settles.
            Keys(c, NpcArmL, "rotx", 0, ax, 0.45f, ax + 50, 1.3f, ax + 44, 2.2f, ax);
            Keys(c, NpcArmL, "rotz", 0, slouched ? 4f : 0f, 0.45f, 18, 1.3f, 14, 2.2f, slouched ? 4f : 0f);
            Keys(c, "Hips", "posy", 0, slouched ? 1.02f : 1.05f, 2.2f, slouched ? 1.02f : 1.05f);
            if (slouched)
            {
                Keys(c, "Hips", "rotx", 0, -4f, 2.2f, -4f);
                Keys(c, "Hips/Spine", "rotx", 0, -18f, 0.45f, -14f, 1.3f, -15f, 2.2f, -18f);
                Keys(c, "Hips/Spine/Neck", "rotx", 0, 8f, 2.2f, 8f);
                Keys(c, NpcArmR, "rotx", 0, ax, 2.2f, ax);
            }
            return c;
        }
    }
}
