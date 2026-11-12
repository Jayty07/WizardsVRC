# The Sunfire Wyrm's dragon, as a rig

The dragon from the Sunfire Wyrm spell as files to use anywhere. Original art, made for WizardsVRC.

| File | What it holds |
|---|---|
| `SunfireWyrm.glb` | glTF 2.0 (binary): the model, its skin, a skeleton and four animations, all in the one file |
| `SunfireWyrm.fbx` | FBX 7.4 (binary): the same model, skeleton, skin weights and four animations (as takes). Its skin is `WyrmSkin.png`, beside it |
| `SunfireWyrm.obj` + `SunfireWyrm.mtl` | The model alone, laid out straight. OBJ holds no bones and no animation; each bone's part is a group (`Head`, `Jaw`, `Body_00` ... `Body_23`). Its skin is `WyrmSkin.png` |
| `SunfireWyrm_Reared.obj` | The model alone in its card's pose: reared out of a coil, jaws open |
| `WyrmSkin.png` | The 64 x 64 skin. Keep it in the same folder as the `.fbx` and the `.obj`s (the `.glb` carries its own copy) |

The `.glb` is written by `python3 Tools/export_sunfire_wyrm.py` from the game's own meshes and flight, so it is the
dragon the game has, not a copy drawn again; `python3 Tools/convert_sunfire_wyrm.py` writes the `.fbx` and the `.obj`s
from the `.glb`. Each tool has a `check` that reads its files back: the `.glb` is skinned by hand and compared with the
game's dragon on every frame of every clip, and the `.fbx` and `.obj`s are compared with the `.glb` the same way.
They agree to a hundredth of a millimetre.

**None of these has been opened in Blender, Unity or Maya from here.** There is no FBX library on the machine that
made them, so the `.fbx` is written by hand (`Tools/fbx_binary.py`) in the layout Blender's exporter uses. That
writer gives a real Blender-exported FBX back byte for byte, and every kind of record in the dragon's file has the
shape it has in real FBX files, except the animation curves, which no file to hand had. If an importer objects to
anything, that is worth knowing.

## What is in it

- **Model**: one skinned mesh, 2,392 triangles, flat-shaded (PS1 style), one material. Laid out straight the
  dragon is 11.6 m from its nose to the tip of its tail flame and 1.3 m across its legs, facing +Z, +Y up. The `.glb`
  and the `.obj`s are in metres; the `.fbx` is in centimetres, FBX's usual unit (the file says so, and importers
  convert). The skin is pixel art: keep its filter on **nearest / point**.
- **Skeleton** (51 bones; every vertex rides exactly one):

  | Bone | What it moves |
  |---|---|
  | `Root` | The whole dragon. In the `Spell` clip it is where the caster stands |
  | `Head` | The head; pivot at the back of the skull |
  | `Jaw` | The lower jaw; pivot at its hinge. Turn it about X to open: 38° is wide open |
  | `Body_00` ... `Body_23` | A chain from the neck to the tail, each at the middle of its length of body, 0.38 m behind the one before. `Body_04` and `Body_14` carry the legs; `Body_23` is the tail and its tuft of flame |
  | `Size_00` ... `Size_23` | One under each `Body` bone, holding that length's vertices. Scale it to grow or shrink the length without disturbing the chain |

  `MouthPoint` and `ManePoint` (under `Head`) are not bones: they mark where its breath leaves and where its mane
  burns, for hanging effects on.
- **Clips**, 30 frames a second:

  | Clip | Length | What |
  |---|---|---|
  | `Spell` | 10.67 s | The spell's whole flight for one target 7.27 m in front of `Root`: out of the caster's chest, three twirls round them, at rest behind them with a roar, through the target, round, its breath, and burning away. The `Size` bones grow each length as it comes out and shrink it as it burns |
  | `Fly` | 2 s, loops | Swimming forward on the spot, the wave running down its body |
  | `Idle` | 4 s, loops | Hanging in the air in a lazy S, breathing |
  | `Reared` | one pose | As on its card: reared out of a coil, jaws open (the head 1.6 times life size) |

The fire it sheds, its breath and its roar are particle systems and sounds in the game, not part of the model.

## Using it

- **Unity**: drop `SunfireWyrm.fbx` and `WyrmSkin.png` into Assets together. The mesh, the skeleton and the four
  takes should come in as one model with four clips; tick Loop Time on `Fly` and `Idle`, and set the skin's Filter
  Mode to Point (Compression: None). If the material comes in without its picture, drag `WyrmSkin` onto it. In the
  `Spell` clip the dragon flies a long way from where it is laid out, so tick **Update When Offscreen** on its
  Skinned Mesh Renderer (or enlarge its bounds): otherwise Unity stops drawing it when the laid-out shape leaves the
  view. Inside the WizardsVRC project you do not need a file at all: the menu **WizardsVRC > Export > Sunfire Wyrm
  Rig (prefab)** saves the same rig (no animations, no script) as
  `Assets/WizardsVRC/Generated/Wyrm/SunfireWyrmRig.prefab`, built from the meshes the project already has.
- **Blender**: File > Import > glTF 2.0, with the `.glb`. It arrives as an armature with the mesh on it and the four
  clips as actions. To make your own flight, put a Spline IK on the `Body` chain and draw the curve. (Blender should
  read the `.fbx` as well, but prefer the `.glb` there: it keeps every rotation whole and carries its skin inside.)
- **Maya, 3ds Max, MotionBuilder, Unreal**: import the `.fbx`.
- **Godot, three.js and the rest**: import the `.glb` as it is.
- **Anything that only takes a still model** (a slicer, a sculpting or painting program): the `.obj`s.

Unity is left-handed; glTF, FBX and OBJ are right-handed. So the model is mirrored in X on the way out and comes back
the right way round in any importer that follows the standard.

In the `.fbx` a rotation is three angles where the `.glb` has a quaternion. Every frame is the same pose in both. A
program that blends the angles between two frames, rather than sampling the frames, can be a few degrees out for
that instant where the dragon whips round in the `Spell` clip (ten at the worst).
