#!/usr/bin/env python3
"""The Sunfire Wyrm's dragon as a rig to use anywhere: one glTF 2.0 binary (.glb) with the model, its skin, a skeleton
and four animations. Blender, Godot, Unreal, three.js and Unity (with a glTF importer) all read it; for programs that
want an FBX or an OBJ, Tools/convert_sunfire_wyrm.py makes those from this file.

    python3 Tools/export_sunfire_wyrm.py              writes Exports/SunfireWyrm/SunfireWyrm.glb and WyrmSkin.png
    python3 Tools/export_sunfire_wyrm.py check        reads the file back, skins it by hand and compares every frame of
                                                      every clip with the dragon the game's own flight model makes
    python3 Tools/export_sunfire_wyrm.py sheet OUT.png   draws frames of each clip from the file

The model is the game's (Tools/make_sunfire_wyrm.py: the same meshes and the same 64 x 64 skin), 1 unit = 1 metre,
+Y up, facing +Z. It is one skinned mesh of 2,392 triangles, flat-shaded, every vertex on exactly one bone.

The skeleton (Exports/SunfireWyrm/README.md has the same list):
    Root                      where the dragon is (in the Spell clip: where its caster stands)
      Head                    pivot at the back of the skull
        Jaw                   pivot at its hinge; turns about X to open (38 degrees is wide open)
        MouthPoint, ManePoint not bones: where its breath leaves and where its mane burns, for effects
      Body_00 ... Body_23     a chain from the neck to the tail, each bone at the middle of its length of body,
                              0.38 m behind the one before (Body_04 and Body_14 carry the legs, Body_23 is the tail)
        Size_00 ... Size_23   one under each Body bone, carrying that length's vertices: scale it to grow or shrink
                              the length without touching the chain (the Spell clip does, as the dragon comes out of
                              its caster and burns away)

The clips, 30 frames a second:
    Spell    the whole of the spell's flight for one target 7.27 m ahead of Root: out of the caster's chest, three
             twirls round them, at rest behind them with a roar, through the target, round, its breath, and away
    Fly      a loop: swimming forward on the spot, the wave running down its body
    Idle     a loop: hanging in the air in a lazy S, breathing
    Reared   one pose: as on its card, reared out of a coil with its jaws open (its head 1.6 times life size)

glTF is right-handed and Unity is left-handed, so everything is mirrored in X on the way out (and each triangle's
winding turned): the dragon is the same shape, and its left is still its left.
"""
import json
import math
import os
import shutil
import struct
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import make_sunfire_wyrm as model          # noqa: E402
import sunfire_wyrm_flight as flight       # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_DIR = os.path.join(ROOT, 'Exports', 'SunfireWyrm')
OUT = os.path.join(OUT_DIR, 'SunfireWyrm.glb')

SEGS = model.SEGMENTS
NECK, SPACING = flight.NECK, flight.SPACING
JAW_OPEN = 38.0          # SunfireWyrm.jawOpenDegrees
FPS = 30
GONE = 0.001             # the scale of a length that isn't there (not 0: some importers can't take a bone apart at 0)
UP = np.array([0.0, 1.0, 0.0])
MIRROR = np.array([-1.0, 1.0, 1.0])


def norm(v):
    n = np.linalg.norm(v)
    return v / n if n > 1e-9 else v


def look(fwd, up=UP):
    """Quaternion.LookRotation as a matrix whose columns are right, up, forward (as the game and the previewer do)."""
    f = norm(np.array(fwd, dtype=float))
    r = np.cross(up, f)
    if np.linalg.norm(r) < 1e-5:
        r = np.cross(np.array([0.0, 0.0, 1.0]), f)
    r = norm(r)
    return np.stack([r, np.cross(f, r), f], axis=1)


def rot_x(deg):
    """Quaternion.Euler(deg, 0, 0) as a matrix."""
    a = math.radians(deg)
    return np.array([[1, 0, 0], [0, math.cos(a), -math.sin(a)], [0, math.sin(a), math.cos(a)]])


def seg_home(i):
    """Where length i sits in the bind pose: laid out straight behind the head."""
    return np.array([0.0, 0.0, -(NECK + i * SPACING)])


def seg_mesh(i):
    return 'Tail' if i == SEGS - 1 else ('SegmentLegs' if i in model.LEG_SEGMENTS else 'Segment')


# ---------------------------------------------------------------------- poses
# A pose: {'head': (position, rotation matrix, scale), 'jaw': degrees, 'segs': [(position, rotation matrix, scale)]}
# in the game's own numbers (x right, y up, z forward; left-handed).

def pose_from_flight(p, origin):
    """A pose from the flight model's (or the card's): lengths that aren't out yet wait, tiny, where the dragon starts."""
    P, D, U, hs = p['head']
    head = (np.array(P, float), look(D, U), max(GONE, float(hs)))
    segs = []
    for sg in p['segs']:
        if sg is None or sg[3] <= GONE:
            if sg is None:
                segs.append((np.array(origin, float), head[1], GONE))
            else:
                segs.append((np.array(sg[0], float), look(sg[1], sg[2]), GONE))
            continue
        segs.append((np.array(sg[0], float), look(sg[1], sg[2]), float(sg[3])))
    return {'head': head, 'jaw': p['jaw'], 'segs': segs}


def pose_on_curve(curve, jaw, head_turn=None):
    """A pose with the dragon lying along 'curve' (a function of metres behind its nose-end pivot), every length the
    usual distance along it behind the head and looking at the one ahead."""
    # walk the curve finely, so lengths sit at true distances along it
    qs = np.linspace(0.0, 14.0, 1401)
    pts = np.array([curve(q) for q in qs])
    along = np.concatenate([[0.0], np.cumsum(np.linalg.norm(np.diff(pts, axis=0), axis=1))])

    def at(s):
        k = int(np.searchsorted(along, s))
        k = min(max(k, 1), len(pts) - 1)
        f = (s - along[k - 1]) / max(1e-9, along[k] - along[k - 1])
        return pts[k - 1] + (pts[k] - pts[k - 1]) * f

    P = at(0.0)
    D = norm(P - at(0.6))
    if head_turn is not None:
        D = norm(D + head_turn)
    segs = []
    ahead = P
    for i in range(SEGS):
        p = at(NECK + i * SPACING)
        d = norm(ahead - p)
        segs.append((p, look(d, UP), 1.0))
        ahead = p
    return {'head': (P, look(D, UP), 1.0), 'jaw': jaw, 'segs': segs}


def clip_spell():
    """The spell, for one target straight ahead of the caster (who stands on Root, facing +Z)."""
    target = np.array([0.0, 0.0, flight.SEAT_RADIUS])
    w = flight.Wyrm()
    w.plan(np.zeros(3), target, -1.0, 1.0, 1.0, [target])        # Sunfire Wyrm's recipe is mirrored: flip = -1
    t = flight.cast_times()
    w.schedule(t['unl'], t['disp'], t['exp'], t['bar'], t['strike'], t['exit'], t['after'])
    start = w.at(0.0)
    frames = []
    n = int(math.floor((t['after'] - t['unl']) * FPS))
    for k in range(n):
        p = w.pose(t['unl'] + k / FPS + 1e-4)
        if p is None:
            continue
        pose = pose_from_flight(p, start)
        pose['jaw'] = p['jaw'] * JAW_OPEN
        frames.append(pose)
    return frames


def clip_fly(seconds=2.0):
    frames = []
    n = int(round(seconds * FPS))
    for k in range(n + 1):
        ph = 2.0 * math.pi * k / n

        def curve(s, ph=ph):
            amp = 0.05 + 0.5 * (s / 9.1) ** 1.2
            w = 2.0 * math.pi * s / 4.2
            return np.array([amp * math.sin(w - ph), 0.45 * amp * math.sin(w - ph + 1.3) + 0.05 * math.sin(ph), -s * 0.985])

        frames.append(pose_on_curve(curve, 4.0 + 3.0 * math.sin(ph)))
    return frames


def clip_idle(seconds=4.0):
    frames = []
    n = int(round(seconds * FPS))
    for k in range(n + 1):
        ph = 2.0 * math.pi * k / n

        def curve(s, ph=ph):
            soft = min(1.0, s / 2.5)
            x = 0.9 * math.sin(s * 0.62 + 0.25 * math.sin(ph)) * soft
            y = -0.1 * s + 0.3 * math.sin(s * 0.5 - ph) * min(1.0, s / 3.0) + 0.06 * math.sin(ph)
            return np.array([x, y, -s * 0.9])

        turn = np.array([0.12 * math.sin(ph), 0.05 * math.sin(2.0 * ph), 0.0])
        frames.append(pose_on_curve(curve, 6.0 + 5.0 * max(0.0, math.sin(ph * 2.0)), turn))
    return frames


def clip_reared():
    p = model.card_pose()
    pose = pose_from_flight(p, np.zeros(3))
    pose['jaw'] = p['jaw']      # the card's is in degrees already
    return [pose, pose]


CLIPS = (('Spell', clip_spell), ('Fly', clip_fly), ('Idle', clip_idle), ('Reared', clip_reared))


# ---------------------------------------------------------------------- the skeleton
# Node numbers in the file.
N_TOP, N_MESH, N_ROOT, N_HEAD, N_JAW, N_MOUTH, N_MANE = 0, 1, 2, 3, 4, 5, 6
N_BODY = 7
N_SIZE = N_BODY + SEGS
JOINTS = [N_ROOT, N_HEAD, N_JAW] + [N_BODY + i for i in range(SEGS)] + [N_SIZE + i for i in range(SEGS)]
J_HEAD, J_JAW = 1, 2


def j_size(i):
    return 3 + SEGS + i


def quat(m):
    """A rotation matrix as a unit quaternion (x, y, z, w)."""
    t = m[0, 0] + m[1, 1] + m[2, 2]
    if t > 0.0:
        s = math.sqrt(t + 1.0) * 2.0
        q = ((m[2, 1] - m[1, 2]) / s, (m[0, 2] - m[2, 0]) / s, (m[1, 0] - m[0, 1]) / s, 0.25 * s)
    elif m[0, 0] > m[1, 1] and m[0, 0] > m[2, 2]:
        s = math.sqrt(1.0 + m[0, 0] - m[1, 1] - m[2, 2]) * 2.0
        q = (0.25 * s, (m[0, 1] + m[1, 0]) / s, (m[0, 2] + m[2, 0]) / s, (m[2, 1] - m[1, 2]) / s)
    elif m[1, 1] > m[2, 2]:
        s = math.sqrt(1.0 + m[1, 1] - m[0, 0] - m[2, 2]) * 2.0
        q = ((m[0, 1] + m[1, 0]) / s, 0.25 * s, (m[1, 2] + m[2, 1]) / s, (m[0, 2] - m[2, 0]) / s)
    else:
        s = math.sqrt(1.0 + m[2, 2] - m[0, 0] - m[1, 1]) * 2.0
        q = ((m[0, 2] + m[2, 0]) / s, (m[1, 2] + m[2, 1]) / s, 0.25 * s, (m[1, 0] - m[0, 1]) / s)
    q = np.array(q)
    return q / np.linalg.norm(q)


def mirrored(m):
    """A rotation in the game's numbers, in the file's (mirrored in X)."""
    return m * np.outer(MIRROR, MIRROR)


def locals_of(pose):
    """Every animated node's local translation, rotation (quaternion) and scale for a pose, in the file's numbers:
    {node: (t, q, s)} (None for a part that node doesn't animate)."""
    out = {}
    P, R, hs = pose['head']
    out[N_HEAD] = (P * MIRROR, quat(mirrored(R)), np.full(3, hs))
    out[N_JAW] = (None, quat(mirrored(rot_x(pose['jaw']))), None)
    parent_p, parent_r = np.zeros(3), np.eye(3)
    for i, (p, r, k) in enumerate(pose['segs']):
        out[N_BODY + i] = ((parent_r.T @ (p - parent_p)) * MIRROR, quat(mirrored(parent_r.T @ r)), None)
        out[N_SIZE + i] = (None, None, np.full(3, k))
        parent_p, parent_r = p, r
    return out


# ---------------------------------------------------------------------- the file

class Bin:
    """The file's one buffer, and its views and accessors."""

    def __init__(self):
        self.data = bytearray()
        self.views = []
        self.accessors = []

    def view(self, raw, target=None):
        while len(self.data) % 4:
            self.data.append(0)
        v = {'buffer': 0, 'byteOffset': len(self.data), 'byteLength': len(raw)}
        if target is not None:
            v['target'] = target
        self.data += raw
        self.views.append(v)
        return len(self.views) - 1

    def accessor(self, array, kind, component, target=None, bounds=False):
        a = np.ascontiguousarray(array)
        acc = {'bufferView': self.view(a.tobytes(), target), 'componentType': component, 'count': int(a.shape[0]), 'type': kind}
        if bounds:
            flat2 = a.reshape(a.shape[0], -1)
            acc['min'] = [float(x) for x in flat2.min(axis=0)]
            acc['max'] = [float(x) for x in flat2.max(axis=0)]
        self.accessors.append(acc)
        return len(self.accessors) - 1


FLOAT, UBYTE, USHORT = 5126, 5121, 5123


def geometry():
    """The dragon in its bind pose, in the file's numbers: positions, normals, uvs, the bone each vertex rides."""
    meshes = model.build_all()
    parts = [(meshes['Head'][0], meshes['Head'][1], J_HEAD), (meshes['Jaw'][0] + np.array(model.JAW_HINGE), meshes['Jaw'][1], J_JAW)]
    for i in range(SEGS):
        v, uv = meshes[seg_mesh(i)]
        across, along = model.segment_scale(i)
        parts.append((v * np.array([across, across, along]) + seg_home(i), uv, j_size(i)))
    pos, nor, uvs, bone = [], [], [], []
    for v, uv, j in parts:
        v = (v * MIRROR)[:, [0, 2, 1]]          # mirrored, so each triangle is wound the other way round
        uv = uv[:, [0, 2, 1]]
        n = np.cross(v[:, 1] - v[:, 0], v[:, 2] - v[:, 0])
        n /= np.maximum(np.linalg.norm(n, axis=1, keepdims=True), 1e-12)
        pos.append(v.reshape(-1, 3))
        nor.append(np.repeat(n, 3, axis=0))
        uvs.append(np.stack([uv[:, :, 0], 1.0 - uv[:, :, 1]], axis=2).reshape(-1, 2))    # the file's v runs down
        bone.append(np.full(len(v) * 3, j))
    return np.concatenate(pos), np.concatenate(nor), np.concatenate(uvs), np.concatenate(bone)


def build():
    b = Bin()
    pos, nor, uvs, bone = geometry()
    n = len(pos)
    joints = np.zeros((n, 4), dtype=np.uint8)
    joints[:, 0] = bone
    weights = np.zeros((n, 4), dtype=np.float32)
    weights[:, 0] = 1.0
    prim = {
        'attributes': {
            'POSITION': b.accessor(pos.astype(np.float32), 'VEC3', FLOAT, 34962, bounds=True),
            'NORMAL': b.accessor(nor.astype(np.float32), 'VEC3', FLOAT, 34962),
            'TEXCOORD_0': b.accessor(uvs.astype(np.float32), 'VEC2', FLOAT, 34962),
            'JOINTS_0': b.accessor(joints, 'VEC4', UBYTE, 34962),
            'WEIGHTS_0': b.accessor(weights, 'VEC4', FLOAT, 34962),
        },
        'indices': b.accessor(np.arange(n, dtype=np.uint16), 'SCALAR', USHORT, 34963),
        'material': 0, 'mode': 4,
    }

    # ---- nodes: the bind pose
    def node(name, t=None, children=None):
        d = {'name': name}
        if t is not None and np.linalg.norm(t) > 0:
            d['translation'] = [float(x) for x in np.array(t, float) * MIRROR]
        if children:
            d['children'] = children
        return d

    nodes = [None] * (N_SIZE + SEGS)
    nodes[N_TOP] = node('SunfireWyrm', children=[N_MESH, N_ROOT])
    nodes[N_MESH] = {'name': 'SunfireWyrm_Mesh', 'mesh': 0, 'skin': 0}
    nodes[N_ROOT] = node('Root', children=[N_HEAD, N_BODY])
    nodes[N_HEAD] = node('Head', children=[N_JAW, N_MOUTH, N_MANE])
    nodes[N_JAW] = node('Jaw', model.JAW_HINGE)
    nodes[N_MOUTH] = node('MouthPoint', model.MOUTH)
    nodes[N_MANE] = node('ManePoint', model.MANE)
    world = {N_ROOT: np.zeros(3), N_HEAD: np.zeros(3), N_JAW: np.array(model.JAW_HINGE)}
    for i in range(SEGS):
        kids = [N_SIZE + i] + ([N_BODY + i + 1] if i + 1 < SEGS else [])
        nodes[N_BODY + i] = node('Body_%02d' % i, seg_home(i) - (seg_home(i - 1) if i else np.zeros(3)), kids)
        nodes[N_SIZE + i] = node('Size_%02d' % i)
        world[N_BODY + i] = world[N_SIZE + i] = seg_home(i)
    # Each bone's inverse bind matrix (column-major): the bind pose has no turns or scales, so it is a move back.
    ibm = np.zeros((len(JOINTS), 16), dtype=np.float32)
    for k, nd in enumerate(JOINTS):
        m = np.eye(4)
        m[:3, 3] = -(world[nd] * MIRROR)
        ibm[k] = m.T.reshape(16)
    skin = {'name': 'SunfireWyrm_Rig', 'joints': JOINTS, 'skeleton': N_ROOT, 'inverseBindMatrices': b.accessor(ibm, 'MAT4', FLOAT)}

    # ---- the clips
    animations = []
    for name, make in CLIPS:
        frames = make()
        keyed = [locals_of(p) for p in frames]
        times = (np.arange(len(frames), dtype=np.float32) / FPS).reshape(-1, 1)
        t_acc = b.accessor(times, 'SCALAR', FLOAT, bounds=True)
        samplers, channels = [], []
        for nd in sorted(keyed[0]):
            for slot, path, kind in ((0, 'translation', 'VEC3'), (1, 'rotation', 'VEC4'), (2, 'scale', 'VEC3')):
                if keyed[0][nd][slot] is None:
                    continue
                vals = np.array([k[nd][slot] for k in keyed], dtype=np.float64)
                if slot == 1:
                    for f in range(1, len(vals)):       # the short way round, frame to frame
                        if np.dot(vals[f], vals[f - 1]) < 0.0:
                            vals[f] = -vals[f]
                samplers.append({'input': t_acc, 'output': b.accessor(vals.astype(np.float32), kind, FLOAT), 'interpolation': 'LINEAR'})
                channels.append({'sampler': len(samplers) - 1, 'target': {'node': nd, 'path': path}})
        animations.append({'name': name, 'samplers': samplers, 'channels': channels})

    # ---- the skin: the game's own sheet, point-filtered
    png = open(model.SKIN_OUT, 'rb').read()
    image_view = b.view(png)
    gltf = {
        'asset': {'version': '2.0', 'generator': 'WizardsVRC Tools/export_sunfire_wyrm.py',
                  'copyright': 'Original art made for WizardsVRC (the Sunfire Wyrm).'},
        'scene': 0,
        'scenes': [{'name': 'SunfireWyrm', 'nodes': [N_TOP]}],
        'nodes': nodes,
        'meshes': [{'name': 'SunfireWyrm', 'primitives': [prim]}],
        'skins': [skin],
        'materials': [{'name': 'WyrmSkin', 'pbrMetallicRoughness': {'baseColorTexture': {'index': 0}, 'metallicFactor': 0.0, 'roughnessFactor': 1.0},
                       'emissiveFactor': [0.1, 0.035, 0.01]}],
        'textures': [{'name': 'WyrmSkin', 'sampler': 0, 'source': 0}],
        'samplers': [{'magFilter': 9728, 'minFilter': 9728, 'wrapS': 33071, 'wrapT': 33071}],
        'images': [{'name': 'WyrmSkin', 'mimeType': 'image/png', 'bufferView': image_view}],
        'animations': animations,
        'accessors': b.accessors,
        'bufferViews': b.views,
    }
    while len(b.data) % 4:
        b.data.append(0)
    gltf['buffers'] = [{'byteLength': len(b.data)}]
    text = json.dumps(gltf, separators=(',', ':')).encode('utf-8')
    text += b' ' * (-len(text) % 4)
    blob = struct.pack('<4sII', b'glTF', 2, 12 + 8 + len(text) + 8 + len(b.data))
    blob += struct.pack('<I4s', len(text), b'JSON') + text + struct.pack('<I4s', len(b.data), b'BIN\0') + bytes(b.data)
    return blob, gltf


def write():
    os.makedirs(OUT_DIR, exist_ok=True)
    blob, gltf = build()
    with open(OUT, 'wb') as f:
        f.write(blob)
    shutil.copyfile(model.SKIN_OUT, os.path.join(OUT_DIR, 'WyrmSkin.png'))
    tris = gltf['accessors'][gltf['meshes'][0]['primitives'][0]['indices']]['count'] // 3
    print('%s: %.0f KB, %d triangles, %d bones, clips: %s' % (
        os.path.relpath(OUT, ROOT), len(blob) / 1024.0, tris, len(JOINTS),
        ', '.join('%s (%.2f s)' % (a['name'], gltf['accessors'][a['samplers'][0]['input']]['max'][0]) for a in gltf['animations'])))


# ---------------------------------------------------------------------- reading it back

TYPES = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4, 'MAT4': 16}
DTYPES = {FLOAT: np.float32, UBYTE: np.uint8, USHORT: np.uint16, 5125: np.uint32}


def read(path=OUT):
    raw = open(path, 'rb').read()
    magic, version, total = struct.unpack_from('<4sII', raw, 0)
    assert magic == b'glTF' and version == 2 and total == len(raw), 'not a glTF 2 binary, or cut short'
    n, kind = struct.unpack_from('<I4s', raw, 12)
    assert kind == b'JSON' and n % 4 == 0
    gltf = json.loads(raw[20:20 + n].decode('utf-8'))
    m, kind2 = struct.unpack_from('<I4s', raw, 20 + n)
    assert kind2 == b'BIN\0' and m % 4 == 0 and 28 + n + m == len(raw) and gltf['buffers'][0]['byteLength'] == m
    data = raw[28 + n:]

    def acc(i):
        a = gltf['accessors'][i]
        v = gltf['bufferViews'][a['bufferView']]
        dt, width = DTYPES[a['componentType']], TYPES[a['type']]
        assert v['byteOffset'] % 4 == 0 and v['byteOffset'] + v['byteLength'] <= len(data)
        assert a['count'] * width * np.dtype(dt).itemsize <= v['byteLength']
        return np.frombuffer(data, dtype=dt, count=a['count'] * width, offset=v['byteOffset']).reshape(a['count'], width)

    return gltf, acc, data


def qmat(q):
    x, y, z, w = q
    return np.array([[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
                     [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
                     [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]])


def skinned(gltf, acc, clip=None, frame=0):
    """The mesh's vertices where the skeleton puts them (the bind pose, or a frame of a clip), worked out the way a
    renderer does: each bone's world matrix times its inverse bind matrix."""
    nodes = gltf['nodes']
    trs = {}
    for i, nd in enumerate(nodes):
        trs[i] = [np.array(nd.get('translation', [0, 0, 0]), float), np.array(nd.get('rotation', [0, 0, 0, 1]), float),
                  np.array(nd.get('scale', [1, 1, 1]), float)]
    if clip is not None:
        anim = [a for a in gltf['animations'] if a['name'] == clip][0]
        for ch in anim['channels']:
            s = anim['samplers'][ch['sampler']]
            trs[ch['target']['node']][('translation', 'rotation', 'scale').index(ch['target']['path'])] = acc(s['output'])[frame].astype(float)
    parent = {}
    for i, nd in enumerate(nodes):
        for c in nd.get('children', []):
            parent[c] = i
    world = {}

    def mat(i):
        if i in world:
            return world[i]
        t, q, s = trs[i]
        m = np.eye(4)
        m[:3, :3] = qmat(q / np.linalg.norm(q)) * s
        m[:3, 3] = t
        world[i] = (mat(parent[i]) @ m) if i in parent else m
        return world[i]

    skin = gltf['skins'][0]
    ibm = acc(skin['inverseBindMatrices']).reshape(-1, 4, 4).transpose(0, 2, 1).astype(float)
    bones = np.array([mat(j) @ ibm[k] for k, j in enumerate(skin['joints'])])
    prim = gltf['meshes'][0]['primitives'][0]
    pos = acc(prim['attributes']['POSITION']).astype(float)
    j = acc(prim['attributes']['JOINTS_0'])[:, 0]
    w = acc(prim['attributes']['WEIGHTS_0'])
    assert np.allclose(w[:, 0], 1.0) and np.allclose(w[:, 1:], 0.0)
    m = bones[j]
    return np.einsum('nij,nj->ni', m[:, :3, :3], pos) + m[:, :3, 3]


def game_vertices(pose):
    """The same vertices straight from the game's meshes and a pose (in the game's numbers)."""
    meshes = model.build_all()
    P, R, hs = pose['head']
    out = [(meshes['Head'][0] * hs) @ R.T + P]
    hinge = P + R @ (np.array(model.JAW_HINGE) * hs)
    out.append((meshes['Jaw'][0] * hs) @ (R @ rot_x(pose['jaw'])).T + hinge)
    for i, (p, r, k) in enumerate(pose['segs']):
        across, along = model.segment_scale(i)
        out.append((meshes[seg_mesh(i)][0] * (np.array([across, across, along]) * k)) @ r.T + p)
    return np.concatenate(out).reshape(-1, 3)


def check():
    gltf, acc, data = read()
    prim = gltf['meshes'][0]['primitives'][0]
    pos = acc(prim['attributes']['POSITION'])
    a = gltf['accessors'][prim['attributes']['POSITION']]
    assert np.allclose(pos.min(axis=0), a['min']) and np.allclose(pos.max(axis=0), a['max']), 'POSITION bounds'
    nor = acc(prim['attributes']['NORMAL'])
    assert np.allclose(np.linalg.norm(nor, axis=1), 1.0, atol=1e-4), 'normals are unit length'
    uv = acc(prim['attributes']['TEXCOORD_0'])
    assert uv.min() >= 0.0 and uv.max() <= 1.0, 'uvs are on the sheet'
    idx = acc(prim['indices'])[:, 0]
    assert idx.max() < len(pos) and len(idx) % 3 == 0
    assert acc(prim['attributes']['JOINTS_0']).max() < len(gltf['skins'][0]['joints'])
    # Every triangle faces outwards: its normal is its winding's (counter-clockwise, as glTF has it).
    tri = pos[idx].reshape(-1, 3, 3).astype(float)
    wound = np.cross(tri[:, 1] - tri[:, 0], tri[:, 2] - tri[:, 0])
    wound /= np.maximum(np.linalg.norm(wound, axis=1, keepdims=True), 1e-12)
    assert np.allclose(wound, nor[idx].reshape(-1, 3, 3)[:, 0], atol=1e-3), 'winding agrees with the normals'
    for nd in range(len(gltf['nodes'])):
        assert sum(nd in n.get('children', []) for n in gltf['nodes']) <= 1, 'a node has one parent'
    print('file: %d bytes, %d vertices, %d triangles, %d nodes, %d bones, %d accessors' % (
        os.path.getsize(OUT), len(pos), len(idx) // 3, len(gltf['nodes']), len(gltf['skins'][0]['joints']), len(gltf['accessors'])))

    def unmirror(v):      # back to the game's numbers and winding
        return (v * MIRROR).reshape(-1, 3, 3)[:, [0, 2, 1]].reshape(-1, 3)

    # The bind pose is the dragon laid out straight.
    straight = {'head': (np.zeros(3), np.eye(3), 1.0), 'jaw': 0.0, 'segs': [(seg_home(i), np.eye(3), 1.0) for i in range(SEGS)]}
    worst = float(np.abs(unmirror(skinned(gltf, acc)) - game_vertices(straight)).max())
    print('bind pose: furthest any vertex is from the game\'s: %.2g m' % worst)
    bad = worst > 1e-4
    for name, make in CLIPS:
        frames = make()
        anim = [x for x in gltf['animations'] if x['name'] == name][0]
        times = acc(anim['samplers'][0]['input'])[:, 0]
        assert len(times) == len(frames) and np.all(np.diff(times) > 0), 'one key a frame, in order'
        for s in anim['samplers']:
            out = acc(s['output'])
            assert len(out) == len(frames) and np.all(np.isfinite(out))
            if out.shape[1] == 4:
                assert np.allclose(np.linalg.norm(out, axis=1), 1.0, atol=1e-4), 'rotations are unit quaternions'
        worst = 0.0
        step = max(1, len(frames) // 60)
        for f in list(range(0, len(frames), step)) + [len(frames) - 1]:
            worst = max(worst, float(np.abs(unmirror(skinned(gltf, acc, name, f)) - game_vertices(frames[f])).max()))
        loop = ''
        if name in ('Fly', 'Idle'):
            gap = float(np.abs(skinned(gltf, acc, name, 0) - skinned(gltf, acc, name, len(frames) - 1)).max())
            loop = ', loop seam %.2g m' % gap
            bad |= gap > 1e-4
        print('%-7s %4d frames (%.2f s), %d channels: furthest any vertex is from the game\'s: %.2g m%s' % (
            name, len(frames), times[-1], len(anim['channels']), worst, loop))
        bad |= worst > 2e-3
    print('PROBLEMS' if bad else 'the file matches the game\'s dragon')
    return 1 if bad else 0


def sheet(out):
    """Frames of each clip, drawn from the file with the spell previewer's renderer and the game's skin."""
    import preview_sunfire_wyrm as P
    from PIL import Image, ImageDraw
    gltf, acc, data = read()
    prim = gltf['meshes'][0]['primitives'][0]
    uv = acc(prim['attributes']['TEXCOORD_0']).astype(float)
    uv = np.stack([uv[:, 0], 1.0 - uv[:, 1]], axis=1).reshape(-1, 3, 2)[:, [0, 2, 1]]
    rows = []
    for name, make in CLIPS:
        anim = [x for x in gltf['animations'] if x['name'] == name][0]
        n = gltf['accessors'][anim['samplers'][0]['input']]['count']
        picks = [0] if name == 'Reared' else [int(round(k * (n - 1) / 5.0)) for k in range(6)]
        if name == 'Spell':
            picks = [int(n * f) for f in (0.04, 0.2, 0.42, 0.58, 0.78, 0.9)]
        cells = []
        for f in picks:
            v = (skinned(gltf, acc, name, f) * MIRROR).reshape(-1, 3, 3)[:, [0, 2, 1]]
            lo, hi = v.reshape(-1, 3).min(axis=0), v.reshape(-1, 3).max(axis=0)
            mid = (lo + hi) / 2.0
            reach = max(2.5, float(np.linalg.norm(hi - lo)) * 0.62)
            cam = P.Cam()
            cam.set(mid + np.array([0.75, 0.38, -0.55]) * reach * 1.25, mid)
            fb = P.FB()
            fb.col[:] = np.array([0.1, 0.09, 0.16])
            P.draw_tris(fb, cam, v, uv, P.SKIN, emission=P.WYRM_EMISSION, paint=0.15)
            im = Image.fromarray((np.clip(fb.col, 0, 1) * 255).astype(np.uint8))
            ImageDraw.Draw(im).text((6, 4), '%s  %.2f s' % (name, f / FPS), fill=(255, 220, 130))
            cells.append(im)
        rows.append(cells)
    w, h = rows[0][0].size
    cols = max(len(r) for r in rows)
    board = Image.new('RGB', (cols * w, len(rows) * h), (16, 14, 24))
    for y, cells in enumerate(rows):
        for x, im in enumerate(cells):
            board.paste(im, (x * w, y * h))
    board.save(out)
    print('%s: %d x %d' % (out, board.size[0], board.size[1]))


if __name__ == '__main__':
    args = sys.argv[1:]
    if not args:
        write()
    elif args[0] == 'check':
        sys.exit(check())
    elif args[0] == 'sheet' and len(args) > 1:
        sheet(args[1])
    else:
        print(__doc__)
