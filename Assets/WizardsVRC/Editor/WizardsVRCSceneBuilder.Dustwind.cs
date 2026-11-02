using System.Collections.Generic;
using UnityEngine;
using WizardsVRC.EditorTools.WorldData;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// Dustwind's (World 2) dressing and bodies: the props a Dustwind map places with point looks (barrels, cattle cars, the
    /// stagecoach, ore carts, water towers, windmills, rail lines, gravestones, Warden stones, drill rigs, a locomotive,
    /// the town pump), board-and-batten shops for the boomtowns, and the creature shapes the plan asks for (the
    /// tumbleweed, the cactus, the Juggernaut and the ghost train). Everything is primitives, like the rest of the world.
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        /// <summary>True for Dustwind's street styles (their props, shops and bodies are Western).</summary>
        private static bool IsFrontierStyle(ZoneDefinition z)
        {
            return z != null && (z.textureStyle == ZoneTextureStyle.Desert || z.textureStyle == ZoneTextureStyle.Boomtown || z.textureStyle == ZoneTextureStyle.Mine);
        }

        // ================================================================== point looks

        /// <summary>The axis (local) a prop on this cell lines up with: along neighbouring cells with the same symbol or rails.</summary>
        private static Vector3 PropAxis(ZoneGrid g, Vector2Int cell, char symbol, char rails)
        {
            bool ns = false, ew = false;
            foreach (var d in ZoneGrid.Dirs)
            {
                char c = g.At(cell.x + d.x, cell.y + d.y);
                if (c != symbol && (rails == '\0' || c != rails)) continue;
                if (d.x != 0) ew = true; else ns = true;
            }
            if (ew && !ns) return Vector3.right;
            if (ns) return Vector3.forward;
            // Nothing to line up with: face the nearest road.
            Vector3 open = OpenFront(g, cell);
            return Mathf.Abs(open.x) > Mathf.Abs(open.z) ? Vector3.forward : Vector3.right;
        }

        /// <summary>The map symbol of the zone's rail-line points (Rails look), or '\0'.</summary>
        private static char RailSymbol(ZoneDefinition z)
        {
            if (z == null || z.points == null) return '\0';
            foreach (var p in z.points) if (p != null && p.look == ZonePointLook.Rails) return p.Symbol;
            return '\0';
        }

        /// <summary>
        /// Builds one of Dustwind's point looks on its cell (holder sits on the cell's middle). Returns what you click for an
        /// Interact point (null: a click box is made for you). labelY is where the point's label floats.
        /// </summary>
        private static GameObject DustwindLook(Transform holder, ZoneGrid g, Vector2Int cell, ZonePoint p, Material wood, ref float labelY)
        {
            var z = g.zone;
            float cs = g.cell;
            Material timber = TimberMat();
            var iron = PMat("StoveIron", new Color(0.14f, 0.13f, 0.14f), 1f);
            var steel = PMat("RailSteel", new Color(0.45f, 0.45f, 0.5f), 1f);
            var brass = PMat("Brass", new Color(0.85f, 0.65f, 0.25f), 1f, new Color(0.2f, 0.14f, 0.02f));
            var stone = PsxTexMat("WardenStone", new Color(0.66f, 0.56f, 0.46f), TileTexture(TerrainTexDir + "/RedRock.png"), 1.2f);
            char rails = RailSymbol(z);
            float hash = g.Hash(cell.x, cell.y, 61);
            switch (p.look)
            {
                case ZonePointLook.Barrel:
                {
                    // Two or three barrels and a crate.
                    int n = 2 + (hash > 0.5f ? 1 : 0);
                    GameObject first = null;
                    for (int k = 0; k < n; k++)
                    {
                        Vector3 at = Quaternion.Euler(0, k * 120f + hash * 90f, 0) * new Vector3(0, 0, 0.9f);
                        var b = Deco(holder, PrimitiveType.Cylinder, "Barrel", at + new Vector3(0, 0.6f, 0), new Vector3(0.9f, 0.6f, 0.9f), timber, Quaternion.identity, true);
                        foreach (float y in new[] { 0.25f, 0.95f })
                            Deco(holder, PrimitiveType.Cylinder, "BarrelHoop", at + new Vector3(0, y, 0), new Vector3(0.94f, 0.04f, 0.94f), iron, Quaternion.identity, false);
                        if (first == null) first = b;
                    }
                    labelY = 2f;
                    return first;
                }
                case ZonePointLook.Wagon:
                case ZonePointLook.Locomotive:
                {
                    Vector3 axis = PropAxis(g, cell, p.Symbol, rails);
                    Quaternion rot = Quaternion.LookRotation(axis);
                    if (rails != '\0' && (g.At(cell.x, cell.y - 1) == rails || g.At(cell.x, cell.y + 1) == rails || g.At(cell.x - 1, cell.y) == rails || g.At(cell.x + 1, cell.y) == rails))
                        RailSegment(holder, axis, cs, timber, steel);
                    if (p.look == ZonePointLook.Locomotive)
                    {
                        var engine = LocomotiveShape(holder, rot, PMat("JuggernautIron", new Color(0.22f, 0.22f, 0.26f), 1f), brass,
                            PMat("FireboxGlow", new Color(1f, 0.5f, 0.2f), 1f, new Color(1f, 0.4f, 0.1f)), cs * 1.1f);
                        var box = engine.AddComponent<BoxCollider>();
                        box.center = new Vector3(0, 1.8f, 0);
                        box.size = new Vector3(2.8f, 3.6f, cs * 1.1f);
                        labelY = 6f;
                        return engine;
                    }
                    // A slatted cattle car on four wheels.
                    var car = Child(holder, "CattleCar", Vector3.zero);
                    car.localRotation = rot;
                    float len = cs * 0.85f;
                    var body = Deco(car, PrimitiveType.Cube, "CarBody", new Vector3(0, 2.2f, 0), new Vector3(2.6f, 2.4f, len), timber, Quaternion.identity, true);
                    Deco(car, PrimitiveType.Cube, "CarRoof", new Vector3(0, 3.5f, 0), new Vector3(2.9f, 0.2f, len + 0.3f), PMat("CarRoof", new Color(0.32f, 0.3f, 0.3f), 1f), Quaternion.identity, false);
                    for (int k = 0; k < 4; k++)
                        Deco(car, PrimitiveType.Cube, "Slat", new Vector3(0, 1.4f + k * 0.55f, 0), new Vector3(2.66f, 0.12f, len - 0.2f), PMat("SlatGap", new Color(0.12f, 0.08f, 0.06f), 1f), Quaternion.identity, false);
                    foreach (float s in new[] { -1f, 1f })
                        foreach (float t in new[] { -1f, 1f })
                            Shape(car, "Wheel", CylMesh(), new Vector3(s * 1.15f, 0.5f, t * len * 0.32f), new Vector3(0.9f, 0.12f, 0.9f), iron, false, Quaternion.Euler(0, 0, 90f));
                    labelY = 4.5f;
                    return body;
                }
                case ZonePointLook.Stagecoach:
                {
                    Vector3 front = OpenFront(g, cell);
                    var coach = Child(holder, "Stagecoach", Vector3.zero);
                    coach.localRotation = Quaternion.LookRotation(Vector3.Cross(Vector3.up, front));
                    var paint = PMat("CoachRed", new Color(0.62f, 0.14f, 0.12f), 1f);
                    var body = Deco(coach, PrimitiveType.Cube, "CoachBody", new Vector3(0, 1.9f, 0), new Vector3(1.9f, 1.7f, 2.6f), paint, Quaternion.identity, true);
                    Deco(coach, PrimitiveType.Cube, "CoachRoof", new Vector3(0, 2.85f, 0), new Vector3(2f, 0.15f, 2.8f), timber, Quaternion.identity, false);
                    Deco(coach, PrimitiveType.Cube, "DriverBox", new Vector3(0, 2.4f, 1.8f), new Vector3(1.6f, 0.2f, 0.9f), timber, Quaternion.identity, false);
                    foreach (float s in new[] { -1f, 1f })
                    {
                        Deco(coach, PrimitiveType.Cube, "CoachWindow", new Vector3(s * 0.96f, 2.1f, 0), new Vector3(0.04f, 0.7f, 1f), PMat("CoachWindow", new Color(0.15f, 0.12f, 0.1f), 1f), Quaternion.identity, false);
                        foreach (float t in new[] { -1f, 1f })
                            Shape(coach, "CoachWheel", CylMesh(), new Vector3(s * 1.05f, 0.75f, t * 1.1f), new Vector3(1.4f, 0.08f, 1.4f), PMat("WheelYellow", new Color(0.85f, 0.65f, 0.2f), 1f), false, Quaternion.Euler(0, 0, 90f));
                    }
                    Deco(coach, PrimitiveType.Cube, "CoachTongue", new Vector3(0, 0.6f, 2.6f), new Vector3(0.15f, 0.15f, 2f), timber, Quaternion.identity, false);
                    labelY = 3.6f;
                    return body;
                }
                case ZonePointLook.OreCart:
                {
                    Vector3 axis = PropAxis(g, cell, p.Symbol, rails);
                    var cart = Child(holder, "OreCart", Vector3.zero);
                    cart.localRotation = Quaternion.LookRotation(axis);
                    RailSegment(cart, Vector3.forward, cs * 0.6f, timber, steel);
                    var tub = Deco(cart, PrimitiveType.Cube, "CartTub", new Vector3(0, 1f, 0), new Vector3(1.3f, 0.9f, 1.8f), PMat("CartRust", new Color(0.45f, 0.28f, 0.18f), 1f), Quaternion.identity, true);
                    Shape(cart, "CartOre", BlobMesh(), new Vector3(0, 1.5f, 0), new Vector3(1.1f, 0.45f, 1.5f), PMat("LightOre", new Color(1f, 0.75f, 0.35f), 1f, new Color(0.8f, 0.45f, 0.1f)), false, Quaternion.identity);
                    foreach (float s in new[] { -1f, 1f })
                        foreach (float t in new[] { -1f, 1f })
                            Shape(cart, "CartWheel", CylMesh(), new Vector3(s * 0.62f, 0.35f, t * 0.6f), new Vector3(0.55f, 0.08f, 0.55f), iron, false, Quaternion.Euler(0, 0, 90f));
                    labelY = 2.5f;
                    return tub;
                }
                case ZonePointLook.WaterTower:
                    WaterTower(holder, Vector3.zero, timber, PsxTexMat("TowerShingles", new Color(0.45f, 0.3f, 0.22f), TileTexture(StreetTexDir + "/Shingles.png"), 1.6f), true);
                    labelY = 12f;
                    return null;
                case ZonePointLook.Windmill:
                    Windmill(holder, Vector3.zero, Quaternion.LookRotation(OpenFront(g, cell)), timber, PMat("WindmillMetal", new Color(0.55f, 0.55f, 0.58f), 2f));
                    Deco(holder, PrimitiveType.Cube, "WindmillFoot", new Vector3(0, 0.5f, 0), new Vector3(2.2f, 1f, 2.2f), timber, Quaternion.identity, true);
                    labelY = 12f;
                    return null;
                case ZonePointLook.Rails:
                {
                    // Along every neighbour that's also track (straight runs, corners and junctions all just work).
                    bool any = false;
                    foreach (var d in ZoneGrid.Dirs)
                    {
                        char c = g.At(cell.x + d.x, cell.y + d.y);
                        if (c != p.Symbol && !IsRailProp(z, c)) continue;
                        Vector3 dir = ZoneGrid.ToLocalDir(d);
                        var half = Child(holder, "RailHalf", dir * (cs * 0.25f));
                        RailSegment(half, dir, cs * 0.5f, timber, steel);
                        any = true;
                    }
                    if (!any) RailSegment(holder, Vector3.forward, cs, timber, steel);
                    return null;
                }
                case ZonePointLook.Gravestone:
                {
                    Quaternion face = Quaternion.LookRotation(OpenFront(g, cell)) * Quaternion.Euler(0, (hash - 0.5f) * 20f, (hash - 0.5f) * 8f);
                    var grave = PsxTexMat("Gravestone", new Color(0.55f, 0.55f, 0.58f), TileTexture(TerrainTexDir + "/StoneWall.png"), 1f);
                    var slab = Deco(holder, PrimitiveType.Cube, "Gravestone", new Vector3(0, 0.75f, 0), new Vector3(1f, 1.5f, 0.25f), grave, face, true);
                    Deco(holder, PrimitiveType.Cylinder, "GravestoneTop", new Vector3(0, 1.5f, 0), new Vector3(1f, 0.125f, 1f), grave, face * Quaternion.Euler(90f, 0, 0), false);
                    Deco(holder, PrimitiveType.Cube, "GraveMound", new Vector3(0, 0.1f, 0) - (face * Vector3.forward) * 1.2f, new Vector3(1.1f, 0.2f, 2f), PMat("GraveDirt", new Color(0.35f, 0.3f, 0.28f), 1f), face, false);
                    labelY = 2.4f;
                    return slab;
                }
                case ZonePointLook.StandingStone:
                {
                    var rock = Deco(holder, PrimitiveType.Cube, "StandingStone", new Vector3(0, 2.2f, 0), new Vector3(1.4f, 4.4f, 0.9f), stone,
                        Quaternion.Euler((hash - 0.5f) * 8f, hash * 360f, (hash - 0.5f) * 6f), true);
                    Shape(holder, "StoneCap", BlobMesh(), new Vector3(0, 4.4f, 0), new Vector3(1.3f, 0.6f, 0.9f), stone, false, Quaternion.Euler(0, hash * 360f, 0));
                    if (p.kind == ZonePointKind.Interact)
                    {
                        // A carved sun-rune that lights up once the stone's woken.
                        var rune = Shape(holder, "Rune", GemMesh(), new Vector3(0, 2.6f, 0), new Vector3(0.5f, 0.7f, 0.5f),
                            PMat("WardenRune", new Color(1f, 0.75f, 0.35f), 1f, new Color(1f, 0.6f, 0.2f)), false, Quaternion.identity);
                        rune.SetActive(true);
                    }
                    labelY = 5.4f;
                    return rock;
                }
                case ZonePointLook.DrillRig:
                {
                    // A lattice derrick with a drill bit and a glowing light-ore boiler.
                    const float h = 9f;
                    foreach (float sx in new[] { -1f, 1f })
                        foreach (float sz in new[] { -1f, 1f })
                            Deco(holder, PrimitiveType.Cube, "DerrickLeg", new Vector3(sx * 1.2f, h / 2f, sz * 1.2f), new Vector3(0.25f, h, 0.25f), iron,
                                Quaternion.Euler(-sz * 6f, 0, sx * 6f), false);
                    for (int k = 1; k < 4; k++)
                        Deco(holder, PrimitiveType.Cube, "DerrickBand", new Vector3(0, k * h / 4f, 0), new Vector3(2.6f - k * 0.3f, 0.15f, 2.6f - k * 0.3f), iron, Quaternion.identity, false);
                    Deco(holder, PrimitiveType.Cylinder, "DrillShaft", new Vector3(0, h / 2f, 0), new Vector3(0.35f, h / 2f, 0.35f), steel, Quaternion.identity, false);
                    Shape(holder, "DrillBit", ConeMesh(), new Vector3(0, 0.5f, 0), new Vector3(0.9f, -0.9f, 0.9f), steel, false, Quaternion.identity);
                    var boiler = Deco(holder, PrimitiveType.Cylinder, "RigBoiler", new Vector3(1.9f, 1f, 0), new Vector3(1.4f, 1f, 1.4f), iron, Quaternion.identity, true);
                    Shape(holder, "BoilerGlow", BlobMesh(), new Vector3(1.9f, 1.4f, 0.72f), new Vector3(0.6f, 0.5f, 0.1f),
                        PMat("FireboxGlow", new Color(1f, 0.5f, 0.2f), 1f, new Color(1f, 0.4f, 0.1f)), false, Quaternion.identity);
                    Deco(holder, PrimitiveType.Cube, "RigBase", new Vector3(0, 0.15f, 0), new Vector3(3f, 0.3f, 3f), timber, Quaternion.identity, true);
                    labelY = h + 1f;
                    return boiler;
                }
                case ZonePointLook.WaterPump:
                {
                    // A cast-iron hand pump on a stone pad, with a trough under its spout.
                    Quaternion face = Quaternion.LookRotation(OpenFront(g, cell));
                    var pad = PsxTexMat("PumpPad", new Color(0.6f, 0.52f, 0.45f), TileTexture(TerrainTexDir + "/StoneWall.png"), 1f);
                    Deco(holder, PrimitiveType.Cylinder, "PumpPad", new Vector3(0, 0.12f, 0), new Vector3(2.4f, 0.12f, 2.4f), pad, Quaternion.identity, false);
                    var body = Deco(holder, PrimitiveType.Cylinder, "PumpBody", new Vector3(0, 1f, 0), new Vector3(0.45f, 0.9f, 0.45f), iron, Quaternion.identity, true);
                    Deco(holder, PrimitiveType.Cylinder, "PumpSpout", face * new Vector3(0, 1.45f, 0.4f), new Vector3(0.14f, 0.35f, 0.14f), iron, face * Quaternion.Euler(70f, 0, 0), false);
                    Deco(holder, PrimitiveType.Cube, "PumpHandle", face * new Vector3(0, 2.05f, -0.5f), new Vector3(0.1f, 0.1f, 1.2f), iron, face * Quaternion.Euler(-20f, 0, 0), false);
                    Deco(holder, PrimitiveType.Cube, "PumpTrough", face * new Vector3(0, 0.35f, 1.1f), new Vector3(1.6f, 0.5f, 0.7f), timber, face, true);
                    labelY = 2.9f;
                    return body;
                }
            }
            return null;
        }

        /// <summary>True when the symbol is a rail-line point or a prop that sits on rails (cattle cars, locomotives).</summary>
        private static bool IsRailProp(ZoneDefinition z, char c)
        {
            if (z == null || z.points == null) return false;
            foreach (var p in z.points)
                if (p != null && p.Symbol == c && (p.look == ZonePointLook.Rails || p.look == ZonePointLook.Wagon || p.look == ZonePointLook.Locomotive)) return true;
            return false;
        }

        /// <summary>A length of track centred on the parent: ties (the RailTies texture) and two steel rails, flat on the ground.</summary>
        private static void RailSegment(Transform parent, Vector3 dir, float length, Material timber, Material steel)
        {
            Quaternion rot = Quaternion.LookRotation(dir);
            var ties = PsxTexMat("RailTieBed", new Color(0.62f, 0.5f, 0.4f), TileTexture(TerrainTexDir + "/RailTies.png"), 1f);
            Deco(parent, PrimitiveType.Cube, "RailBed", new Vector3(0, 0.05f, 0), new Vector3(2.8f, 0.1f, length + 0.02f), ties, rot, false);
            foreach (float s in new[] { -0.75f, 0.75f })
                Deco(parent, PrimitiveType.Cube, "Rail", rot * new Vector3(s, 0.17f, 0), new Vector3(0.12f, 0.14f, length + 0.02f), steel, rot, false);
        }

        // ================================================================== creatures

        /// <summary>Bodies for Dustwind's creature types ("Tumbleweed", "Cactus", "Locomotive", "GhostTrain"); null for anything else.</summary>
        private static GameObject DustwindCreature(Transform parent, CardDatabase db, MonsterEntry entry, string name, float scale)
        {
            if (entry == null) return null;
            string type = entry.creatureType;
            if (type != "Tumbleweed" && type != "Cactus" && type != "Locomotive" && type != "GhostTrain") return null;
            Material mat = MonsterMat(db, entry.id);
            if (mat == null) mat = PMat("Monster_" + entry.id, entry.bodyColor, 1f);
            var eyeWhite = PMat("CreatureEyeWhite", new Color(0.98f, 0.96f, 0.9f), 1f);
            var pupil = PMat("KrakenPupil", new Color(0.05f, 0.03f, 0.05f), 1f);
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localScale = Vector3.one * scale;
            switch (type)
            {
                case "Tumbleweed":
                {
                    // A ball of tangled twigs (crossed thin cylinders round a hollow), two bright eyes peeking out.
                    var twig = PMat("Twig_" + entry.id, Color.Lerp(entry.bodyColor, new Color(0.4f, 0.3f, 0.2f), 0.3f), 1f);
                    Shape(root, "Core", BlobMesh(), new Vector3(0, 0.9f, 0), Vector3.one * 1.3f, mat, false, Quaternion.identity);
                    var rng = new System.Random(entry.id * 31 + 7);
                    for (int k = 0; k < 22; k++)
                    {
                        Quaternion q = Quaternion.Euler((float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 360f);
                        Shape(root, "Twig", CylMesh(), new Vector3(0, 0.9f, 0) + q * new Vector3(0, 0, 0.55f), new Vector3(0.06f, 0.75f, 0.06f), twig, false, q);
                    }
                    foreach (float sx in new[] { -1f, 1f })
                    {
                        Shape(root, "Eye", BlobMesh(), new Vector3(sx * 0.25f, 1.05f, 0.62f), new Vector3(0.26f, 0.3f, 0.12f), eyeWhite, false, Quaternion.identity);
                        Shape(root, "Pupil", BlobMesh(), new Vector3(sx * 0.25f, 1.02f, 0.68f), new Vector3(0.12f, 0.16f, 0.06f), pupil, false, Quaternion.identity);
                    }
                    break;
                }
                case "Cactus":
                {
                    // A walking saguaro: ribbed trunk, raised arms, a cross face, little root feet. Big ones get a flower.
                    float h = 2.6f;
                    Shape(root, "Trunk", CylMesh(), new Vector3(0, h / 2f + 0.2f, 0), new Vector3(0.95f, h / 2f, 0.95f), mat, false, Quaternion.identity);
                    Shape(root, "Crown", BlobMesh(), new Vector3(0, h + 0.2f, 0), new Vector3(0.95f, 0.7f, 0.95f), mat, false, Quaternion.identity);
                    var rib = PMat("CactusRib_" + entry.id, Color.Lerp(entry.bodyColor, Color.black, 0.25f), 1f);
                    for (int k = 0; k < 6; k++)
                    {
                        Quaternion q = Quaternion.Euler(0, k * 60f, 0);
                        Shape(root, "Rib", CylMesh(), q * new Vector3(0, h / 2f + 0.2f, 0.47f), new Vector3(0.08f, h / 2f - 0.1f, 0.08f), rib, false, q);
                    }
                    foreach (float sx in new[] { -1f, 1f })
                    {
                        Shape(root, "ArmOut", CylMesh(), new Vector3(sx * 0.75f, 1.5f, 0), new Vector3(0.45f, 0.4f, 0.45f), mat, false, Quaternion.Euler(0, 0, 90f));
                        Shape(root, "ArmUp", CylMesh(), new Vector3(sx * 1.05f, 2.05f + (sx > 0 ? 0.2f : 0f), 0), new Vector3(0.42f, 0.55f, 0.42f), mat, false, Quaternion.identity);
                        Shape(root, "ArmTip", BlobMesh(), new Vector3(sx * 1.05f, 2.6f + (sx > 0 ? 0.2f : 0f), 0), Vector3.one * 0.42f, mat, false, Quaternion.identity);
                        Shape(root, "Eye", BlobMesh(), new Vector3(sx * 0.22f, 2.2f, 0.45f), new Vector3(0.2f, 0.12f, 0.08f), pupil, false, Quaternion.Euler(0, 0, sx * -18f));
                        Shape(root, "Foot", BlobMesh(), new Vector3(sx * 0.35f, 0.15f, 0.1f), new Vector3(0.45f, 0.3f, 0.6f), rib, false, Quaternion.identity);
                    }
                    Shape(root, "Mouth", BlobMesh(), new Vector3(0, 1.85f, 0.46f), new Vector3(0.35f, 0.08f, 0.06f), pupil, false, Quaternion.identity);
                    if (entry.isBoss)
                        Shape(root, "Flower", GemMesh(), new Vector3(0, h + 0.75f, 0), new Vector3(0.5f, 0.35f, 0.5f), PMat("CactusFlower", new Color(1f, 0.45f, 0.6f), 1f, new Color(0.4f, 0.1f, 0.15f)), false, Quaternion.identity);
                    break;
                }
                default:
                {
                    // The Juggernaut, or the Midnight Express (pale and glowing): a short armored engine facing forward.
                    bool ghost = type == "GhostTrain";
                    Material trim = ghost ? PMat("GhostTrim", new Color(0.6f, 1f, 0.9f), 1f, new Color(0.3f, 0.7f, 0.6f)) : PMat("Brass", new Color(0.85f, 0.65f, 0.25f), 1f, new Color(0.2f, 0.14f, 0.02f));
                    Material glow = ghost ? PMat("GhostLamp", new Color(0.7f, 1f, 0.95f), 1f, new Color(0.5f, 1f, 0.9f)) : PMat("FireboxGlow", new Color(1f, 0.5f, 0.2f), 1f, new Color(1f, 0.4f, 0.1f));
                    var engine = LocomotiveShape(root, Quaternion.identity, mat, trim, glow, 5.2f);
                    engine.transform.localScale = Vector3.one * 0.8f;
                    break;
                }
            }
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = false;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return root.gameObject;
        }

        /// <summary>
        /// A steam engine pointing along rot's forward, 'length' long: boiler, smokestack, cab, cowcatcher, six wheels,
        /// riveted armor plates and a headlamp. Returns its root (no colliders).
        /// </summary>
        private static GameObject LocomotiveShape(Transform parent, Quaternion rot, Material iron, Material trim, Material glow, float length)
        {
            var t = Child(parent, "Locomotive", Vector3.zero);
            t.localRotation = rot;
            float L = length;
            Shape(t, "Boiler", CylMesh(), new Vector3(0, 1.9f, L * 0.08f), new Vector3(1.9f, L * 0.32f, 1.9f), iron, false, Quaternion.Euler(90f, 0, 0));
            Shape(t, "BoilerFront", CylMesh(), new Vector3(0, 1.9f, L * 0.4f), new Vector3(2f, 0.08f, 2f), trim, false, Quaternion.Euler(90f, 0, 0));
            Shape(t, "Headlamp", GemMesh(), new Vector3(0, 2.3f, L * 0.43f), new Vector3(0.5f, 0.5f, 0.3f), glow, false, Quaternion.identity);
            Shape(t, "Smokestack", CylMesh(), new Vector3(0, 3.3f, L * 0.28f), new Vector3(0.6f, 0.6f, 0.6f), iron, false, Quaternion.identity);
            Shape(t, "StackCrown", CylMesh(), new Vector3(0, 3.95f, L * 0.28f), new Vector3(0.95f, 0.12f, 0.95f), trim, false, Quaternion.identity);
            Shape(t, "SteamDome", BlobMesh(), new Vector3(0, 2.95f, L * 0.02f), new Vector3(0.8f, 0.55f, 0.8f), trim, false, Quaternion.identity);
            Block(t, "Cab", new Vector3(0, 2.3f, -L * 0.3f), new Vector3(2.4f, 2.6f, L * 0.26f), iron, Quaternion.identity);
            Block(t, "CabRoof", new Vector3(0, 3.7f, -L * 0.3f), new Vector3(2.7f, 0.2f, L * 0.32f), trim, Quaternion.identity);
            foreach (float s in new[] { -1f, 1f })
            {
                Deco(t, PrimitiveType.Cube, "CabWindow", new Vector3(s * 1.21f, 2.7f, -L * 0.3f), new Vector3(0.04f, 0.8f, L * 0.12f), glow, Quaternion.identity, false);
                Deco(t, PrimitiveType.Cube, "ArmorPlate", new Vector3(s * 1.05f, 1.4f, L * 0.08f), new Vector3(0.12f, 1.2f, L * 0.62f), iron, Quaternion.identity, false);
                for (int k = 0; k < 3; k++)
                {
                    float z = L * (0.3f - k * 0.27f);
                    Shape(t, "DriveWheel", CylMesh(), new Vector3(s * 1.15f, 0.7f, z), new Vector3(1.3f, 0.1f, 1.3f), iron, false, Quaternion.Euler(0, 0, 90f));
                    Shape(t, "WheelHub", CylMesh(), new Vector3(s * 1.22f, 0.7f, z), new Vector3(0.4f, 0.04f, 0.4f), trim, false, Quaternion.Euler(0, 0, 90f));
                }
                Deco(t, PrimitiveType.Cube, "ConnectingRod", new Vector3(s * 1.28f, 0.7f, L * 0.03f), new Vector3(0.06f, 0.12f, L * 0.55f), trim, Quaternion.identity, false);
            }
            Deco(t, PrimitiveType.Cube, "Frame", new Vector3(0, 0.6f, 0), new Vector3(2f, 0.3f, L), iron, Quaternion.identity, false);
            // The cowcatcher: a wedge of slats at the front.
            foreach (float s in new[] { -1f, 1f })
                Deco(t, PrimitiveType.Cube, "Cowcatcher", new Vector3(s * 0.55f, 0.45f, L * 0.5f + 0.35f), new Vector3(1.3f, 0.6f, 0.12f), trim, Quaternion.Euler(-30f, s * 35f, 0), false);
            return t.gameObject;
        }

        // ================================================================== boomtown shops

        /// <summary>
        /// A boomtown shop over its block: the board building with its false front and porch (FrontierHouse), the shop's
        /// name across the false front, and the shop counter out on the boardwalk.
        /// </summary>
        private static void FrontierShop(Transform root, ZoneGrid g, ZoneLandmark l, int index, Material gold, Material window, Material wood)
        {
            var fp = g.Footprint(l.Symbol);
            if (fp.width == 0) return;
            Color[] paints = { new Color(0.78f, 0.6f, 0.42f), new Color(0.55f, 0.62f, 0.7f), new Color(0.75f, 0.5f, 0.42f), new Color(0.62f, 0.68f, 0.5f) };
            var wall = PsxTexMat("ShopBoards" + index % paints.Length, paints[index % paints.Length], TileTexture(StreetTexDir + "/Clapboard.png"), 3f);
            var roof = PsxTexMat("ShopShingles", new Color(0.45f, 0.3f, 0.22f), TileTexture(StreetTexDir + "/Shingles.png"), 2.6f);
            FrontierHouse(root, g, g.zone, fp, wall, roof, window);
            Vector2Int fd = g.FrontSide(fp);
            Vector3 front = fd == Vector2Int.zero ? Vector3.back : ZoneGrid.ToLocalDir(fd);
            Vector3 c = RectCenter(g, fp);
            float depth = (Mathf.Abs(front.x) > 0.5f ? fp.width : fp.height) * g.cell;
            float width = (Mathf.Abs(front.x) > 0.5f ? fp.height : fp.width) * g.cell;
            Vector3 face = c + front * (depth / 2f);
            ShopCounter(root, face + front * 0.3f, front, l.label, wood, gold, index);
            if (!string.IsNullOrEmpty(l.label))
            {
                var sign = WorldText(root, "ShopName", face + front * 0.4f + new Vector3(0, 7.2f, 0), Quaternion.LookRotation(-front), new Vector2(Mathf.Max(600f, width * 120f), 160), 0.006f,
                    l.label.ToUpperInvariant(), 64);
                sign.color = new Color(0.98f, 0.88f, 0.55f);
                DressSign(sign, Mathf.Max(560f, width * 115f), 150f);
            }
        }
    }
}
