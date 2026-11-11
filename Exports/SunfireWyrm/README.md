# The Sunfire Wyrm's dragon, as a rig

`SunfireWyrm.glb` is the dragon from the Sunfire Wyrm spell as one file to use anywhere: the model, its skin, a
skeleton and four animations, in glTF 2.0 (binary). `WyrmSkin.png` is the same 64 x 64 skin on its own (it is also
inside the `.glb`). Original art, made for WizardsVRC.

It is written by `python3 Tools/export_sunfire_wyrm.py` from the game's own meshes and flight, so it is the dragon
the game has, not a copy drawn again. `python3 Tools/export_sunfire_wyrm.py check` reads the file back, skins it
by hand and compares every clip with the game's dragon (they agree to under a thousandth of a millimetre). It has
not been opened in Blender or Unity from here: if an importer objects to anything, that is worth knowing.

## What is in it

- **Model**: one skinned mesh, 2,392 triangles, flat-shaded (PS1 style), one material. 1 unit = 1 metre; laid out straight the
  dragon is 11.6 m from its nose to the tip of its tail flame and 1.3 m across its legs, facing +Z, +Y up. The skin is pixel art: keep its filter on **nearest / point**.
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

- **Blender**: File > Import > glTF 2.0. It comes in as an armature with the mesh on it and the four clips as
  actions. To make your own flight, put a Spline IK on the `Body` chain and draw the curve. Export to FBX from there
  if another program wants FBX.
- **Unity**: Unity does not read glTF by itself. Either add the **glTFast** package (Package Manager > Add package by
  name > `com.unity.cloud.gltfast`) and drop the `.glb` into Assets, or bring it through Blender as an FBX. Set the
  skin's Filter Mode to Point. Inside the WizardsVRC project you do not need the file at all: the menu
  **WizardsVRC > Export > Sunfire Wyrm Rig (prefab)** saves the same rig (no animations, no script) as
  `Assets/WizardsVRC/Generated/Wyrm/SunfireWyrmRig.prefab`, built from the meshes the project already has.
- **Godot, Unreal, three.js and the rest**: import the `.glb` as it is.

glTF is right-handed and Unity is left-handed, so the model is mirrored in X on the way out and comes back the right
way round in any importer that follows the standard.
