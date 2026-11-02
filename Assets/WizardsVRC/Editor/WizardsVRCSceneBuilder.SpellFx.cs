using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// Spell animations and the card art drawn from them.
    ///
    /// After a spell's wind-up (the glyph drawn in the air), its animation plays out in the ring (BattleClient): creature
    /// spells (Ember Imp, Pyre Hawk, Frost Wyvern, Thunder Golem, Grove Stag, Bone Dragon, Stone Basilisk, Sand Djinn...)
    /// summon their creature at the caster, a rig of the same kinds as the monsters (WizardsVRCSceneBuilder.Rigs.cs) in the
    /// school's colours, sized by the spell's pips, which charges or flies at the target and strikes; bolts fly as a
    /// school-shaped missile; area spells rain down or burst over the far side; heals bloom and sparkle up round the
    /// target; shields, blades, traps and charms spiral up round it. Every animation lands its effect at its strike time.
    ///
    /// Each card's art is a painted picture filling the card's window, its subject a render of that same animation at its
    /// most dramatic moment (the creature mid-cast, the bolt, the rain, the bloom) over a backdrop for the school, or the
    /// emblem of a shield, blade, trap or charm (WizardsVRCSceneBuilder.CardArt.cs), saved in Textures/CardArt.
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        private const string CardArtDir = "Assets/WizardsVRC/Textures/CardArt";
        private const string SpellClipDir = "Assets/WizardsVRC/Generated/SpellFx";

        // Animation modes (CardDatabase.animModes): what BattleClient does with the spell's parts.
        private const int FxNone = 0, FxCreature = 1, FxBolt = 2, FxArea = 3, FxLegacy = 4;

        private class SpellLook
        {
            public int mode;
            public RigLook creature;    // FxCreature
            public float creatureScale = 1f;
            public string area = "Burst";   // Burst, Rain, Pillar, Bloom, Spiral, Portal
            public float strike = 1.1f, seconds = 2.1f;
        }

        /// <summary>How a spell looks when it's cast, from its name, effect and school.</summary>
        private static SpellLook SpellLookFor(CardDatabase db, int i)
        {
            string n = db.names[i].ToLowerInvariant();
            int e = db.effects[i];
            int school = db.schools[i];
            bool aoe = db.aoe[i] != 0;
            Color col = school >= 0 && school < SchoolColors.Length ? SchoolColors[school] : new Color(0.85f, 0.8f, 0.6f);
            var L = new SpellLook();
            bool attack = e == CardDatabase.EFFECT_DAMAGE || e == CardDatabase.EFFECT_DRAIN || e == CardDatabase.EFFECT_DOT ||
                          e == CardDatabase.EFFECT_STUN || e == CardDatabase.EFFECT_DETONATE || e == CardDatabase.EFFECT_SHATTER;
            if (e == CardDatabase.EFFECT_HEAL || e == CardDatabase.EFFECT_SELFHEAL)
            {
                L.mode = FxArea; L.area = "Bloom"; L.strike = 0.9f; L.seconds = 1.8f;
                return L;
            }
            if (e == CardDatabase.EFFECT_MINION)
            {
                L.mode = FxArea; L.area = "Portal"; L.strike = 0.8f; L.seconds = 1.6f;
                return L;
            }
            if (!attack)
            {
                L.mode = FxArea; L.area = "Spiral"; L.strike = 0.8f; L.seconds = 1.5f;
                return L;
            }
            var look = new RigLook { accent = Color.Lerp(col, Color.white, 0.4f), voice = "Spirit" };
            if (Has(n, "imp", "ghast", "thrall", "lancer", "golem", "colossus", "behemoth", "titan", "treant", "sentinel", "djinn", "warden"))
            {
                look.kind = RigKind.Biped;
                look.heavy = Has(n, "golem", "colossus", "behemoth", "titan", "sentinel", "warden", "treant");
                look.horns = Has(n, "imp", "behemoth", "titan");
                look.small = Has(n, "imp", "golemling");
                look.bones = Has(n, "thrall", "ghast");
                look.helmet = Has(n, "lancer", "sentinel");
                look.crown = Has(n, "titan");
                look.robe = Has(n, "djinn", "ghast");
            }
            else if (Has(n, "hound", "hare", "stag", "basilisk", "sphinx", "chimera", "stampede", "hydra"))
            {
                look.kind = RigKind.Quadruped;
                look.tail = true;
                look.ears = Has(n, "hound", "hare", "sphinx", "chimera");
                look.tusks = Has(n, "stag", "stampede");
                look.pincers = false;
            }
            else if (Has(n, "hawk", "wyvern", "dragon", "wyrm", "serpent", "leviathan", "sprite", "mite", "wisp", "spark", "leech", "swarm", "eye", "maelstrom"))
            {
                look.kind = RigKind.Floater;
                look.wings = Has(n, "hawk", "wyvern", "dragon", "wyrm", "sprite", "mite");
                look.beak = Has(n, "hawk", "wyvern", "dragon", "wyrm");
                look.tail = Has(n, "serpent", "leviathan", "wisp", "wyrm", "dragon", "leech");
            }
            else
            {
                // No creature: a bolt for single targets, rain or a burst over the far side for area spells.
                if (aoe) { L.mode = FxArea; L.area = Has(n, "rain", "gale", "swarm", "sandstorm", "storm") ? "Rain" : "Pillar"; L.strike = 1.1f; L.seconds = 2f; }
                else { L.mode = FxBolt; L.strike = 0.7f; L.seconds = 1.5f; }
                return L;
            }
            L.mode = FxCreature;
            L.creature = look;
            L.creatureScale = 0.6f + db.pipCosts[i] * 0.22f;
            L.strike = 1.2f;
            L.seconds = 2.2f;
            if (aoe) L.area = "Rain";
            return L;
        }

        /// <summary>
        /// Builds every spell's animation (a user prefab in SpellAnimations/&lt;id&gt; or &lt;name&gt; wins) under one root that's
        /// never culled, fills CardDatabase's animation tables, and renders each card's art from it.
        /// </summary>
        private static void AttachSpellAnimations(Transform arenaRoot, CardDatabase db)
        {
            var holder = new GameObject("SpellAnimations").transform;   // its own root: shared by every ring, never culled
            int n = db.ids.Length;
            var anims = new GameObject[n];
            db.animModes = new int[n];
            db.animStrikes = new float[n];
            db.animSeconds = new float[n];
            db.cardArt = new Texture[n];
            int built = 0, arts = 0;
            Directory.CreateDirectory(CardArtDir);
            for (int i = 0; i < n; i++)
            {
                int school = db.schools[i];
                Color col = school >= 0 && school < SchoolColors.Length ? SchoolColors[school] : new Color(0.85f, 0.8f, 0.6f);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SpellAnimationDir + "/" + db.ids[i] + ".prefab");
                if (prefab == null) prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SpellAnimationDir + "/" + db.names[i] + ".prefab");
                SpellLook look = SpellLookFor(db, i);
                GameObject fx;
                if (prefab != null)
                {
                    fx = (GameObject)PrefabUtility.InstantiatePrefab(prefab, holder);
                    look.mode = FxLegacy;
                    look.seconds = 2f;
                    look.strike = 0f;
                }
                else fx = BuildSpellFx(holder, db.ids[i], look, col);
                fx.name = db.ids[i] + " " + db.names[i];
                db.animModes[i] = look.mode;
                db.animStrikes[i] = look.strike;
                db.animSeconds[i] = look.seconds;
                // The card's art, from the animation itself.
                if (prefab == null)
                {
                    db.cardArt[i] = PaintCardArt(fx, db, i, look);
                    if (db.cardArt[i] != null) arts++;
                }
                fx.SetActive(false);
                anims[i] = fx;
                built++;
            }
            db.spellAnimations = anims;
            Debug.Log("[WizardsVRC] Spell animations: " + built + " spells, " + arts + " card arts rendered into " + CardArtDir + ".");
        }

        // ================================================================== building the animations

        /// <summary>
        /// One spell's animation, in the caster's frame (its root sits on the caster's seat facing the target; BattleClient
        /// moves 'Summon' and 'Projectile' and puts 'Area' on the target). Parts start switched off.
        /// </summary>
        private static GameObject BuildSpellFx(Transform holder, int id, SpellLook look, Color col)
        {
            var root = Child(holder, "Spell" + id, Vector3.zero);
            var glow = PMat("SpellGlow_" + ColorUtility.ToHtmlStringRGB(col), col, 1f, col * 1.2f);
            var core = PMat("SpellCore_" + ColorUtility.ToHtmlStringRGB(col), Color.Lerp(col, Color.white, 0.6f), 1f, Color.Lerp(col, Color.white, 0.6f) * 1.4f);
            if (look.mode == FxCreature)
            {
                var summon = Child(root, "Summon", Vector3.zero);
                var body = PMat("SpellBody_" + ColorUtility.ToHtmlStringRGB(col), Color.Lerp(col, Color.white, 0.15f), 1f, col * 0.35f);
                var rigRoot = Child(summon, "Rig", Vector3.zero);
                Transform bob = Child(rigRoot, "Bob", Vector3.zero);
                BuildRigShapes(bob, look.creature, body, col);
                foreach (var r in rigRoot.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rigRoot.localScale = Vector3.one * look.creatureScale;
                var anim = rigRoot.gameObject.AddComponent<Animator>();
                anim.runtimeAnimatorController = RigController(look.creature.kind, true, look.creature.heavy);
                anim.applyRootMotion = false;
                // A sparkle trail behind it as it moves.
                var trail = SpellParticles(summon, "Trail", col, 25f, 0.6f, 0.3f, true);
                trail.transform.localPosition = new Vector3(0, 1f * look.creatureScale, 0);
                summon.gameObject.SetActive(false);
            }
            if (look.mode == FxBolt)
            {
                var bolt = Child(root, "Projectile", Vector3.zero);
                Shape(bolt, "BoltCore", GemMesh(), Vector3.zero, new Vector3(0.45f, 0.45f, 0.9f), core, false, Quaternion.Euler(90f, 0, 0));
                Shape(bolt, "BoltGlow", BlobMesh(), Vector3.zero, new Vector3(0.8f, 0.8f, 1.2f), glow, false, Quaternion.identity);
                SpellParticles(bolt, "Trail", col, 60f, 0.5f, 0.25f, true);
                SpinClip(bolt.gameObject, "Bolt" + id, 0.4f);
                bolt.gameObject.SetActive(false);
            }
            var area = Child(root, "Area", Vector3.zero);
            BuildArea(area, look, id, col, glow, core);
            area.gameObject.SetActive(false);
            return root.gameObject;
        }

        /// <summary>The effect on the target: its shapes animate once (a clip on an Animator) each time it's switched on.</summary>
        private static void BuildArea(Transform area, SpellLook look, int id, Color col, Material glow, Material core)
        {
            var clip = NewClip("Area" + id, false);
            string kind = look.mode == FxArea ? look.area : (look.area == "Rain" ? "Rain" : "Burst");
            float s0 = look.mode == FxArea ? look.strike : 0f;   // when it hits, in the clip
            switch (kind)
            {
                case "Rain":
                {
                    // Bolts of the school falling on the far side, each splashing as it lands.
                    var rng = new System.Random(id);
                    for (int k = 0; k < 10; k++)
                    {
                        Vector3 at = new Vector3((float)rng.NextDouble() * 6f - 3f, 0, (float)rng.NextDouble() * 6f - 3f);
                        var drop = Child(area, "Drop" + k, at);
                        Shape(drop, "DropShape", GemMesh(), Vector3.zero, new Vector3(0.35f, 1.1f, 0.35f), k % 2 == 0 ? core : glow, false, Quaternion.identity);
                        float t0 = Mathf.Max(0f, s0 - 0.5f) + (float)rng.NextDouble() * 0.5f;
                        Keys(clip, "Drop" + k, "posy", 0, 14f, t0, 14f, t0 + 0.35f, 0.3f, t0 + 0.5f, -0.6f);
                        Keys(clip, "Drop" + k, "scaley", 0, 1, t0 + 0.35f, 1, t0 + 0.5f, 0.2f);
                    }
                    Ring(area, clip, "Splash", glow, s0, s0 + 0.6f, 0.5f, 4f);
                    break;
                }
                case "Pillar":
                {
                    var p = Child(area, "Pillar", Vector3.zero);
                    Shape(p, "PillarShape", CylMesh(), new Vector3(0, 1f, 0), new Vector3(3.2f, 1f, 3.2f), glow, false, Quaternion.identity);
                    Shape(p, "PillarCore", CylMesh(), new Vector3(0, 1f, 0), new Vector3(1.6f, 1f, 1.6f), core, false, Quaternion.identity);
                    Keys(clip, "Pillar", "scaley", 0, 0.01f, s0 - 0.2f, 0.01f, s0, 6f, s0 + 0.4f, 5f, s0 + 0.8f, 0.01f);
                    Keys(clip, "Pillar", "scalex", 0, 1, s0, 1, s0 + 0.8f, 0.2f);
                    Keys(clip, "Pillar", "scalez", 0, 1, s0, 1, s0 + 0.8f, 0.2f);
                    Ring(area, clip, "Shock", glow, s0, s0 + 0.6f, 0.5f, 6f);
                    break;
                }
                case "Bloom":
                {
                    // Petals open round the target and sparkles rise.
                    var bloom = Child(area, "Bloom", Vector3.zero);
                    for (int k = 0; k < 6; k++)
                        Shape(bloom, "Petal", BlobMesh(), Quaternion.Euler(0, k * 60f, 0) * new Vector3(0, 0.1f, 0.7f), new Vector3(0.6f, 0.08f, 1.1f), k % 2 == 0 ? glow : core, false, Quaternion.Euler(0, k * 60f, 0) * Quaternion.Euler(-25f, 0, 0));
                    Keys(clip, "Bloom", "scalex", 0, 0.01f, s0 * 0.6f, 1.4f, s0 + 0.6f, 1.2f, look.seconds, 0.01f);
                    Keys(clip, "Bloom", "scaley", 0, 0.01f, s0 * 0.6f, 1.4f, s0 + 0.6f, 1.2f, look.seconds, 0.01f);
                    Keys(clip, "Bloom", "scalez", 0, 0.01f, s0 * 0.6f, 1.4f, s0 + 0.6f, 1.2f, look.seconds, 0.01f);
                    Keys(clip, "Bloom", "roty", 0, 0, look.seconds, 120);
                    var up = SpellParticles(area, "Sparkles", Color.Lerp(col, Color.white, 0.5f), 40f, 1.2f, 0.18f, false);
                    var main = up.main;
                    main.startSpeed = 2.5f;
                    break;
                }
                case "Spiral":
                {
                    // Three runes of the school spiralling up round the target.
                    for (int k = 0; k < 3; k++)
                    {
                        var arm = Child(area, "Arm" + k, Vector3.zero);
                        Shape(arm, "Rune", GemMesh(), new Vector3(0, 0, 1.1f), new Vector3(0.35f, 0.5f, 0.35f), core, false, Quaternion.identity);
                        Keys(clip, "Arm" + k, "roty", 0, k * 120f, look.seconds, k * 120f + 540f);
                        Keys(clip, "Arm" + k, "posy", 0, 0.2f, s0, 1.6f, look.seconds, 2.4f);
                        Keys(clip, "Arm" + k, "scalex", 0, 1, s0, 1, look.seconds, 0.01f);
                        Keys(clip, "Arm" + k, "scaley", 0, 1, s0, 1, look.seconds, 0.01f);
                        Keys(clip, "Arm" + k, "scalez", 0, 1, s0, 1, look.seconds, 0.01f);
                    }
                    Ring(area, clip, "Glyph", glow, 0f, s0 + 0.3f, 0.3f, 2.4f);
                    break;
                }
                case "Portal":
                {
                    Ring(area, clip, "PortalRing", glow, 0f, look.seconds, 1.2f, 2.8f);
                    var p = Child(area, "Swirl", new Vector3(0, 1.2f, 0));
                    Shape(p, "SwirlCore", BlobMesh(), Vector3.zero, new Vector3(1.6f, 2.2f, 0.2f), core, false, Quaternion.identity);
                    Keys(clip, "Swirl", "scalex", 0, 0.01f, s0, 1, look.seconds, 0.01f);
                    Keys(clip, "Swirl", "scaley", 0, 0.01f, s0, 1, look.seconds, 0.01f);
                    Keys(clip, "Swirl", "rotz", 0, 0, look.seconds, 720);
                    break;
                }
                default:   // Burst: a flash and a shock ring where the creature or bolt hits
                {
                    var flash = Child(area, "Flash", new Vector3(0, 1.1f, 0));
                    Shape(flash, "FlashShape", BlobMesh(), Vector3.zero, Vector3.one * 1.2f, core, false, Quaternion.identity);
                    Keys(clip, "Flash", "scalex", 0, 0.2f, 0.12f, 2.2f, 0.45f, 0.01f);
                    Keys(clip, "Flash", "scaley", 0, 0.2f, 0.12f, 2.2f, 0.45f, 0.01f);
                    Keys(clip, "Flash", "scalez", 0, 0.2f, 0.12f, 2.2f, 0.45f, 0.01f);
                    Ring(area, clip, "Shock", glow, 0f, 0.5f, 0.4f, 3.5f);
                    break;
                }
            }
            var burst = SpellParticles(area, "Burst", col, 0f, 0.7f, 0.25f, false);
            var em = burst.emission;
            em.SetBursts(new[] { new ParticleSystem.Burst(Mathf.Max(0f, s0), 40) });
            var bm = burst.main;
            bm.startSpeed = 5f;
            burst.transform.localPosition = new Vector3(0, 1.1f, 0);
            Directory.CreateDirectory(SpellClipDir);
            AssetDatabase.CreateAsset(clip, SpellClipDir + "/Area" + id + ".anim");
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(SpellClipDir + "/Area" + id + ".controller");
            var st = ctrl.layers[0].stateMachine.AddState("Play");
            st.motion = clip;
            var anim = area.gameObject.AddComponent<Animator>();
            anim.runtimeAnimatorController = ctrl;
            foreach (var t in area.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = false;
            foreach (var r in area.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>A flat ring on the ground that grows from 'r0' to 'r1' metres between t0 and t1 and fades out by shrinking flat.</summary>
        private static void Ring(Transform area, AnimationClip clip, string name, Material mat, float t0, float t1, float r0, float r1)
        {
            var ring = Child(area, name, new Vector3(0, 0.08f, 0));
            Shape(ring, "RingShape", CylMesh(), Vector3.zero, new Vector3(2f, 0.02f, 2f), mat, false, Quaternion.identity);
            Keys(clip, name, "scalex", 0, 0.01f, Mathf.Max(0f, t0 - 0.01f), 0.01f, t0, r0, t1, r1, t1 + 0.05f, 0.01f);
            Keys(clip, name, "scalez", 0, 0.01f, Mathf.Max(0f, t0 - 0.01f), 0.01f, t0, r0, t1, r1, t1 + 0.05f, 0.01f);
            Keys(clip, name, "scaley", 0, 1, t1, 1, t1 + 0.05f, 0.01f);
        }

        /// <summary>A school-coloured particle system: 'rate' a second (0 = bursts only), world-space so trails stay behind.</summary>
        private static ParticleSystem SpellParticles(Transform parent, string name, Color col, float rate, float life, float size, bool trail)
        {
            var go = Child(parent, name, Vector3.zero).gameObject;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = life;
            main.startSize = size;
            main.startSpeed = trail ? 0.3f : 2f;
            main.startColor = new ParticleSystem.MinMaxGradient(col, Color.Lerp(col, Color.white, 0.6f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 120;
            main.playOnAwake = true;
            main.loop = rate > 0f;
            var em = ps.emission;
            em.rateOverTime = rate;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = trail ? 0.2f : 0.6f;
            var col2 = ps.colorOverLifetime;
            col2.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col2.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = PMat("SpellSpark_" + ColorUtility.ToHtmlStringRGB(col), col, 1f, col * 1.5f);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return ps;
        }

        /// <summary>A looping spin about the flight axis for a bolt.</summary>
        private static void SpinClip(GameObject go, string name, float seconds)
        {
            var clip = NewClip(name, true);
            Keys(clip, "BoltCore", "rotz", 0, 0, seconds, 360);
            Directory.CreateDirectory(SpellClipDir);
            AssetDatabase.CreateAsset(clip, SpellClipDir + "/" + name + ".anim");
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(SpellClipDir + "/" + name + ".controller");
            ctrl.layers[0].stateMachine.AddState("Spin").motion = clip;
            go.AddComponent<Animator>().runtimeAnimatorController = ctrl;
        }

        // ================================================================== card art (WizardsVRCSceneBuilder.CardArt.cs)

        /// <summary>Clears the generated spell clips from the last build (they're made fresh each time).</summary>
        private static void ResetSpellFx()
        {
            if (AssetDatabase.IsValidFolder(SpellClipDir)) AssetDatabase.DeleteAsset(SpellClipDir);
        }
    }
}
