#!/usr/bin/env python3
"""Converts the Sunfire Wyrm's rig, Exports/SunfireWyrm/SunfireWyrm.glb, for programs that don't read glTF.

    python3 Tools/convert_sunfire_wyrm.py           writes, beside the .glb:
        SunfireWyrm.fbx          FBX 7.4 (binary): the model, its skeleton and skin weights, and the four clips as takes
        SunfireWyrm.obj + .mtl   the model alone, laid out straight (OBJ holds no bones or animation), with a group
                                 for each bone's part: Head, Jaw, Body_00 ... Body_23
        SunfireWyrm_Reared.obj   the model alone in the card's reared pose
    python3 Tools/convert_sunfire_wyrm.py check     reads the three files back and compares them with the .glb: the
                                 OBJs triangle for triangle, the FBX by posing its skeleton from its own curves and
                                 skinning its mesh by its own clusters, on every frame of every take

Everything comes out of the .glb (Tools/export_sunfire_wyrm.py writes that from the game's meshes and flight), so run
that first if the dragon has changed. All three use the skin beside them, WyrmSkin.png.

The FBX is laid out the way Blender's exporter lays one out (Tools/fbx_binary.py writes the file): centimetres, Y up,
right-handed, 30 frames a second, the mesh and the skeleton's Root side by side at the top. FBX keeps a rotation as three
angles (turned about X, then Y, then Z) where glTF keeps a quaternion, so each key's angles are worked out from the
.glb's, taking of the ways to write the same rotation the one nearest the key before: no curve jumps a turn between
two keys, and every frame is the .glb's exactly.
"""
import collections
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import export_sunfire_wyrm as E      # noqa: E402
import fbx_binary as fbx             # noqa: E402
from fbx_binary import A, C, D, I, L, P, R, S, named, props70, rec      # noqa: E402

DIR = E.OUT_DIR
FBX = os.path.join(DIR, 'SunfireWyrm.fbx')
OBJ = os.path.join(DIR, 'SunfireWyrm.obj')
OBJ_REARED = os.path.join(DIR, 'SunfireWyrm_Reared.obj')
MTL = os.path.join(DIR, 'SunfireWyrm.mtl')
SKIN_FILE = 'WyrmSkin.png'

CM = 100.0
KTIME = 46186158000          # FBX's clock: ticks a second
FRAME = KTIME // E.FPS       # 1539538600: one frame at 30 a second
CREATOR = 'WizardsVRC Tools/convert_sunfire_wyrm.py'
LCL = {'T': 'Lcl Translation', 'R': 'Lcl Rotation', 'S': 'Lcl Scaling'}


# ---------------------------------------------------------------------- the rig, out of the .glb

class Rig:
    def __init__(self):
        self.gltf, self.acc, _ = E.read()
        g = self.gltf
        prim = g['meshes'][0]['primitives'][0]
        self.pos = self.acc(prim['attributes']['POSITION']).astype(float)
        self.uv = self.acc(prim['attributes']['TEXCOORD_0']).astype(float)
        self.uv = np.stack([self.uv[:, 0], 1.0 - self.uv[:, 1]], axis=1)       # v up, as OBJ and FBX have it
        skin = g['skins'][0]
        self.joints = list(skin['joints'])
        self.bone = np.array(self.joints)[self.acc(prim['attributes']['JOINTS_0'])[:, 0]]    # the node each vertex rides
        self.nodes = g['nodes']
        self.parent = {}
        for i, nd in enumerate(self.nodes):
            for c in nd.get('children', []):
                self.parent[c] = i
        self.root = skin['skeleton']
        # The skeleton's nodes, parents before children (the two effect points under the head with them).
        self.order = []
        todo = [self.root]
        while todo:
            n = todo.pop(0)
            self.order.append(n)
            todo += self.nodes[n].get('children', [])
        self.local = {n: np.array(self.nodes[n].get('translation', [0.0, 0.0, 0.0]), float) for n in self.order}
        self.world = {}
        for n in self.order:
            self.world[n] = self.local[n] + (self.world[self.parent[n]] if n != self.root else 0.0)

    def name(self, n):
        return self.nodes[n]['name']

    def part(self, n):
        """What a bone's part of the model is called: a Size bone's is its Body bone's."""
        return self.name(self.parent[n]) if self.name(n).startswith('Size_') else self.name(n)

    def clip(self, name):
        """{(node, path): values (frames, 3 or 4)} and how many frames."""
        anim = [a for a in self.gltf['animations'] if a['name'] == name][0]
        out = {}
        for ch in anim['channels']:
            out[(ch['target']['node'], ch['target']['path'])] = self.acc(anim['samplers'][ch['sampler']]['output']).astype(float)
        return out, self.gltf['accessors'][anim['samplers'][0]['input']]['count']

    def clips(self):
        return [a['name'] for a in self.gltf['animations']]


def flat_normals(tri):
    n = np.cross(tri[:, 1] - tri[:, 0], tri[:, 2] - tri[:, 0])
    return n / np.maximum(np.linalg.norm(n, axis=1, keepdims=True), 1e-12)


def unique_rows(a, places=6):
    """The different rows of 'a', and each row's place among them."""
    keys = np.round(a, places)
    _, first, where = np.unique(keys, axis=0, return_index=True, return_inverse=True)
    order = np.argsort(first)                # in the order they first appear
    rank = np.empty(len(order), dtype=int)
    rank[order] = np.arange(len(order))
    return a[first[order]], rank[np.asarray(where).reshape(-1)]


# ---------------------------------------------------------------------- OBJ

def write_obj(path, rig, pos, title):
    tri = pos.reshape(-1, 3, 3)
    nor = np.repeat(flat_normals(tri), 3, axis=0)
    v, vi = unique_rows(pos)
    vt, ti = unique_rows(rig.uv)
    vn, ni = unique_rows(nor, 5)
    out = ['# ' + title, '# Original art made for WizardsVRC. Metres, +Y up, facing +Z. Written by Tools/convert_sunfire_wyrm.py.',
           'mtllib ' + os.path.basename(MTL), 'o SunfireWyrm']
    out += ['v %.6f %.6f %.6f' % tuple(p) for p in np.round(v, 6) + 0.0]        # + 0.0: no "-0.000000"
    out += ['vt %.6f %.6f' % tuple(t) for t in np.round(vt, 6) + 0.0]
    out += ['vn %.5f %.5f %.5f' % tuple(n) for n in np.round(vn, 5) + 0.0]
    last = None
    for f in range(len(tri)):
        part = rig.part(rig.bone[f * 3])
        if part != last:
            out += ['g ' + part, 'usemtl WyrmSkin', 's off']
            last = part
        out.append('f ' + ' '.join('%d/%d/%d' % (vi[f * 3 + k] + 1, ti[f * 3 + k] + 1, ni[f * 3 + k] + 1) for k in range(3)))
    with open(path, 'w', newline='\n') as fh:
        fh.write('\n'.join(out) + '\n')
    return len(v), len(tri)


def write_mtl():
    with open(MTL, 'w', newline='\n') as fh:
        fh.write('# The Sunfire Wyrm\'s skin: one 64 x 64 sheet of pixel art (keep its filter on nearest / point).\n'
                 'newmtl WyrmSkin\nKa 0.2 0.2 0.2\nKd 1 1 1\nKs 0 0 0\nKe 0.1 0.035 0.01\nd 1\nillum 1\nmap_Kd %s\n' % SKIN_FILE)


def read_obj(path):
    """An OBJ's triangles as they are drawn: positions (n, 3, 3), uvs (n, 3, 2), normals (n, 3, 3), and each one's group."""
    v, vt, vn, tris, groups, group = [], [], [], [], [], ''
    for line in open(path):
        p = line.split()
        if not p:
            continue
        if p[0] == 'v':
            v.append([float(x) for x in p[1:4]])
        elif p[0] == 'vt':
            vt.append([float(x) for x in p[1:3]])
        elif p[0] == 'vn':
            vn.append([float(x) for x in p[1:4]])
        elif p[0] == 'g':
            group = p[1]
        elif p[0] == 'f':
            tris.append([[int(x) - 1 for x in c.split('/')] for c in p[1:4]])
            groups.append(group)
    t = np.array(tris)
    return np.array(v)[t[:, :, 0]], np.array(vt)[t[:, :, 1]], np.array(vn)[t[:, :, 2]], groups


# ---------------------------------------------------------------------- FBX: rotations

def euler_xyz(m, prev):
    """A rotation matrix as FBX's Euler angles (degrees; turned about X, then Y, then Z: R = Rz Ry Rx), choosing, of
    the ways to write the same rotation, the angles nearest 'prev'."""
    sy = -m[2, 0]
    if abs(sy) < 0.9999995:
        e = np.array([math.atan2(m[2, 1], m[2, 2]), math.asin(sy), math.atan2(m[1, 0], m[0, 0])])
    else:           # looking straight along the Y turn's pole: X and Z turn about the same line, so Z is left at 0
        e = np.array([math.atan2(-m[1, 2], m[1, 1]), math.copysign(math.pi / 2.0, sy), 0.0])
    e = np.degrees(e)
    best = None
    for cand in (e, np.array([e[0] + 180.0, 180.0 - e[1], e[2] + 180.0])):
        cand = cand + 360.0 * np.round((prev - cand) / 360.0)
        d = float(np.abs(cand - prev).sum())
        if best is None or d < best[0]:
            best = (d, cand)
    return best[1]


def euler_matrix(e):
    a, b, c = np.radians(e)
    rx = np.array([[1, 0, 0], [0, math.cos(a), -math.sin(a)], [0, math.sin(a), math.cos(a)]])
    ry = np.array([[math.cos(b), 0, math.sin(b)], [0, 1, 0], [-math.sin(b), 0, math.cos(b)]])
    rz = np.array([[math.cos(c), -math.sin(c), 0], [math.sin(c), math.cos(c), 0], [0, 0, 1]])
    return rz @ ry @ rx


def off_between(e0, e1):
    """Two keys' angles: how far the bone turns from one to the other, and how far the pose half-way between the angles
    is from half-way along that turn (degrees). The second is what a program that blends the angles, rather than
    sampling the frames, gets wrong for that instant."""
    q0, q1 = E.quat(euler_matrix(e0)), E.quat(euler_matrix(e1))
    dot = float(np.dot(q0, q1))
    turn = math.degrees(2.0 * math.acos(min(1.0, abs(dot))))
    mid = q0 + (q1 if dot >= 0.0 else -q1)
    if np.linalg.norm(mid) < 1e-9:
        return turn, 0.0
    blended = euler_matrix((np.asarray(e0) + np.asarray(e1)) / 2.0)
    c = (np.trace(blended.T @ E.qmat(mid / np.linalg.norm(mid))) - 1.0) / 2.0
    return turn, math.degrees(math.acos(max(-1.0, min(1.0, c))))


# ---------------------------------------------------------------------- FBX: the dragon's records

def matrix16(m):
    """A 4 x 4 the way FBX stores one: column after column (a move's three numbers are its 13th to 15th)."""
    return np.asarray(m, float).T.reshape(16)


def move(t):
    m = np.eye(4)
    m[:3, 3] = t
    return m


def fbx_records(rig):
    """Every record of the dragon's FBX, and a few counts to print."""
    ids = {}

    def uid(*key):
        return ids.setdefault(key, 1000000 + len(ids))

    order = rig.order
    bones = [n for n in order if n in rig.joints]

    # ---- the mesh: control points welded within each bone's part, so each belongs to one bone
    cp = np.zeros(len(rig.pos), dtype=int)
    points, cluster_points = [], {}
    for b in bones:
        mine = np.nonzero(rig.bone == b)[0]
        if not len(mine):
            continue
        v, where = unique_rows(rig.pos[mine])
        cp[mine] = where + len(points)
        cluster_points[b] = np.arange(len(points), len(points) + len(v))
        points += list(v)
    points = np.array(points)
    normals = np.repeat(flat_normals(rig.pos.reshape(-1, 3, 3)), 3, axis=0)
    uvs, uv_index = unique_rows(rig.uv)
    polys = cp.reshape(-1, 3).copy()
    polys[:, 2] = -polys[:, 2] - 1          # a polygon's last corner is written as -(index + 1)

    # ---- the clips: each animated channel as Euler angles / centimetres / scales, a key a frame
    clips = []
    for name in rig.clips():
        data, frames = rig.clip(name)
        curves = collections.OrderedDict()      # (node, 'T' | 'R' | 'S') -> values (frames, 3)
        for (n, path), vals in data.items():
            if path == 'translation':
                curves[(n, 'T')] = vals * CM
            elif path == 'scale':
                curves[(n, 'S')] = vals
            else:
                e = np.zeros((frames, 3))
                prev = np.zeros(3)
                for f in range(frames):
                    prev = euler_xyz(E.qmat(vals[f] / np.linalg.norm(vals[f])), prev)
                    e[f] = prev
                curves[(n, 'R')] = e
        clips.append((name, frames, curves))
    animated = set(k for _, _, c in clips for k in c)
    longest = max(frames for _, frames, _ in clips) - 1

    objects, links = [], []

    def join(child, parent, prop=None):
        links.append((child, parent, prop))

    def model(key, name, kind, t, node=None):
        flag = lambda what: 'A+' if (node, what) in animated else 'A'      # noqa: E731
        return rec('Model', L(uid(*key)), named(name, 'Model'), S(kind), kids=[
            rec('Version', I(232)),
            props70(P('Lcl Translation', 'Lcl Translation', '', flag('T'), *t),
                    P('Lcl Rotation', 'Lcl Rotation', '', flag('R'), 0.0, 0.0, 0.0),
                    P('Lcl Scaling', 'Lcl Scaling', '', flag('S'), 1.0, 1.0, 1.0),
                    P('DefaultAttributeIndex', 'int', 'Integer', '', 0),
                    P('InheritType', 'enum', '', '', 1)),       # a parent's scale is applied in the parent's own space
            rec('MultiLayer', I(0)), rec('MultiTake', I(0)), rec('Shading', C(1)), rec('Culling', S('CullingOff'))])

    def layer(kind, name, mapping, reference, *arrays):
        return rec(kind, I(0), kids=[rec('Version', I(101)), rec('Name', S(name)), rec('MappingInformationType', S(mapping)),
                                     rec('ReferenceInformationType', S(reference))] + list(arrays))

    # ---- the mesh and its model
    geometry, mesh = uid('geometry'), uid('mesh')
    objects.append(rec('Geometry', L(geometry), named('SunfireWyrm', 'Geometry'), S('Mesh'), kids=[
        rec('GeometryVersion', I(124)),
        rec('Vertices', A('d', points * CM)),
        rec('PolygonVertexIndex', A('i', polys)),
        layer('LayerElementNormal', '', 'ByPolygonVertex', 'Direct', rec('Normals', A('d', normals))),
        layer('LayerElementUV', 'UVMap', 'ByPolygonVertex', 'IndexToDirect', rec('UV', A('d', uvs)), rec('UVIndex', A('i', uv_index))),
        layer('LayerElementMaterial', '', 'AllSame', 'IndexToDirect', rec('Materials', A('i', [0]))),
        rec('Layer', I(0), kids=[rec('Version', I(100))] + [
            rec('LayerElement', kids=[rec('Type', S(kind)), rec('TypedIndex', I(0))])
            for kind in ('LayerElementNormal', 'LayerElementUV', 'LayerElementMaterial')])]))
    objects.append(model(('mesh',), 'SunfireWyrm_Mesh', 'Mesh', (0.0, 0.0, 0.0)))
    join(mesh, 0)
    join(geometry, mesh)

    # ---- the skeleton: a model and what it is (a bone, or a bare point) for every node
    for n in order:
        bone = n in rig.joints
        me, attr = uid('model', n), uid('attr', n)
        objects.append(model(('model', n), rig.name(n), 'LimbNode' if bone else 'Null', rig.local[n] * CM, n))
        if bone:
            objects.append(rec('NodeAttribute', L(attr), named(rig.name(n), 'NodeAttribute'), S('LimbNode'), kids=[
                rec('TypeFlags', S('Skeleton')), props70(P('Size', 'double', 'Number', '', 10.0))]))
        else:
            objects.append(rec('NodeAttribute', L(attr), named(rig.name(n), 'NodeAttribute'), S('Null'), kids=[
                rec('TypeFlags', S('Null')),
                props70(P('Color', 'ColorRGB', 'Color', '', 0.8, 0.8, 0.8), P('Size', 'double', 'Number', '', 10.0), P('Look', 'enum', '', '', 1))]))
        join(me, uid('model', rig.parent[n]) if n != rig.root else 0)
        join(attr, me)

    # ---- the bind pose, and the skin: a cluster for every bone (the bones that carry no vertices too)
    posed = [(mesh, np.eye(4))] + [(uid('model', n), move(rig.world[n] * CM)) for n in bones]
    objects.append(rec('Pose', L(uid('pose')), named('SunfireWyrm', 'Pose'), S('BindPose'), kids=[
        rec('Type', S('BindPose')), rec('Version', I(100)), rec('NbPoseNodes', I(len(posed)))] + [
        rec('PoseNode', kids=[rec('Node', L(node)), rec('Matrix', A('d', matrix16(m)))]) for node, m in posed]))
    skin = uid('skin')
    objects.append(rec('Deformer', L(skin), named('SunfireWyrm', 'Deformer'), S('Skin'), kids=[
        rec('Version', I(101)), rec('Link_DeformAcuracy', D(50.0))]))
    join(skin, geometry)
    for b in bones:
        cluster = uid('cluster', b)
        inside = [rec('Version', I(100)), rec('UserData', S(''), S(''))]
        if b in cluster_points:
            inside += [rec('Indexes', A('i', cluster_points[b])), rec('Weights', A('d', np.ones(len(cluster_points[b]))))]
        # The mesh as the bone sees it at bind time, and the bone as the world does (the mesh sits on the origin).
        inside += [rec('Transform', A('d', matrix16(move(-rig.world[b] * CM)))),
                   rec('TransformLink', A('d', matrix16(move(rig.world[b] * CM))))]
        objects.append(rec('Deformer', L(cluster), named(rig.name(b), 'SubDeformer'), S('Cluster'), kids=inside))
        join(cluster, skin)
        join(uid('model', b), cluster)

    # ---- the skin's material: the sheet beside the file
    material, texture, video = uid('material'), uid('texture'), uid('video')
    objects.append(rec('Material', L(material), named('WyrmSkin', 'Material'), S(''), kids=[
        rec('Version', I(102)), rec('ShadingModel', S('Phong')), rec('MultiLayer', I(0)),
        props70(P('DiffuseColor', 'Color', '', 'A', 1.0, 1.0, 1.0),
                P('EmissiveColor', 'Color', '', 'A', 0.1, 0.035, 0.01),
                P('SpecularFactor', 'Number', '', 'A', 0.0),
                P('Shininess', 'Number', '', 'A', 2.0),
                P('ShininessExponent', 'Number', '', 'A', 2.0),
                P('ReflectionFactor', 'Number', '', 'A', 0.0))]))
    objects.append(rec('Texture', L(texture), named('WyrmSkin', 'Texture'), S(''), kids=[
        rec('Type', S('TextureVideoClip')), rec('Version', I(202)), rec('TextureName', named('WyrmSkin', 'Texture')),
        rec('Media', named('WyrmSkin', 'Video')), rec('FileName', S(SKIN_FILE)), rec('RelativeFilename', S(SKIN_FILE)),
        props70(P('UseMaterial', 'bool', '', '', 1))]))
    objects.append(rec('Video', L(video), named('WyrmSkin', 'Video'), S('Clip'), kids=[
        rec('Type', S('Clip')), props70(P('Path', 'KString', 'Url', '', SKIN_FILE)),
        rec('UseMipMap', I(0)), rec('Filename', S(SKIN_FILE)), rec('RelativeFilename', S(SKIN_FILE))]))
    join(material, mesh)
    join(texture, material, 'DiffuseColor')
    join(video, texture)

    # ---- the clips: a stack and a layer each, a curve node for every animated property, a curve for each of its axes
    keys = 0
    takes = [rec('Current', S(clips[0][0]))]
    for name, frames, curves in clips:
        stack, lay, end = uid('stack', name), uid('layer', name), (frames - 1) * FRAME
        objects.append(rec('AnimationStack', L(stack), named(name, 'AnimStack'), S(''), kids=[props70(
            P('LocalStart', 'KTime', 'Time', '', 0), P('LocalStop', 'KTime', 'Time', '', end),
            P('ReferenceStart', 'KTime', 'Time', '', 0), P('ReferenceStop', 'KTime', 'Time', '', end))]))
        objects.append(rec('AnimationLayer', L(lay), named(name, 'AnimLayer'), S('')))
        join(lay, stack)
        for (n, what), vals in curves.items():
            node = uid('node', name, n, what)
            objects.append(rec('AnimationCurveNode', L(node), named(what, 'AnimCurveNode'), S(''), kids=[props70(
                *[P('d|' + axis, 'Number', '', 'A', vals[0][k]) for k, axis in enumerate('XYZ')])]))
            join(node, lay)
            join(node, uid('model', n), LCL[what])
            for k, axis in enumerate('XYZ'):
                col = vals[:, k]
                # A channel that never changes needs only its first and last key.
                at = np.arange(frames, dtype=np.int64) if float(np.ptp(col)) > 1e-7 else np.array([0, frames - 1], dtype=np.int64)
                keys += len(at)
                curve = uid('curve', name, n, what, k)
                objects.append(rec('AnimationCurve', L(curve), named('', 'AnimCurve'), S(''), kids=[
                    rec('Default', D(col[0])), rec('KeyVer', I(4008)),
                    rec('KeyTime', A('l', at * FRAME)), rec('KeyValueFloat', A('f', col[at])),
                    rec('KeyAttrFlags', A('i', [24836])),                                   # every key: straight to the next
                    rec('KeyAttrDataFloat', A('f', [0.0, 0.0, 9.419963346924634e-30, 0.0])),
                    rec('KeyAttrRefCount', A('i', [len(at)]))]))
                join(curve, node, 'd|' + axis)
        takes.append(rec('Take', S(name), kids=[rec('FileName', S(name + '.tak')), rec('LocalTime', L(0), L(end)),
                                                rec('ReferenceTime', L(0), L(end))]))

    # ---- the file around them
    about = [('ApplicationVendor', 'WizardsVRC'), ('ApplicationName', 'convert_sunfire_wyrm.py'), ('ApplicationVersion', '1')]
    info = [P('DocumentUrl', 'KString', 'Url', '', 'SunfireWyrm.fbx'), P('SrcDocumentUrl', 'KString', 'Url', '', 'SunfireWyrm.fbx')]
    for group in ('Original', 'LastSaved'):
        info += [P(group, 'Compound', '', '')] + [P(group + '|' + k, 'KString', '', '', v) for k, v in about]
        info += [P(group + '|DateTime_GMT', 'DateTime', '', '', '01/01/1970 00:00:00.000')]
        if group == 'Original':
            info += [P('Original|FileName', 'KString', '', '', 'SunfireWyrm.fbx')]
    stamp = (('Version', 1000), ('Year', 2026), ('Month', 11), ('Day', 11), ('Hour', 12), ('Minute', 0), ('Second', 0), ('Millisecond', 0))
    header = rec('FBXHeaderExtension', kids=[
        rec('FBXHeaderVersion', I(1003)), rec('FBXVersion', I(fbx.VERSION)), rec('EncryptionType', I(0)),
        rec('CreationTimeStamp', kids=[rec(k, I(v)) for k, v in stamp]),
        rec('Creator', S(CREATOR)),
        rec('SceneInfo', named('GlobalInfo', 'SceneInfo'), S('UserData'), kids=[
            rec('Type', S('UserData')), rec('Version', I(100)),
            rec('MetaData', kids=[rec('Version', I(100)), rec('Title', S('Sunfire Wyrm')), rec('Subject', S('')),
                                  rec('Author', S('WizardsVRC')), rec('Keywords', S('')), rec('Revision', S('')),
                                  rec('Comment', S('Original art made for WizardsVRC.'))]),
            props70(*info)])])
    settings = rec('GlobalSettings', kids=[rec('Version', I(1000)), props70(
        P('UpAxis', 'int', 'Integer', '', 1), P('UpAxisSign', 'int', 'Integer', '', 1),
        P('FrontAxis', 'int', 'Integer', '', 2), P('FrontAxisSign', 'int', 'Integer', '', 1),
        P('CoordAxis', 'int', 'Integer', '', 0), P('CoordAxisSign', 'int', 'Integer', '', 1),
        P('OriginalUpAxis', 'int', 'Integer', '', -1), P('OriginalUpAxisSign', 'int', 'Integer', '', 1),
        P('UnitScaleFactor', 'double', 'Number', '', 1.0), P('OriginalUnitScaleFactor', 'double', 'Number', '', 1.0),
        P('AmbientColor', 'ColorRGB', 'Color', '', 0.0, 0.0, 0.0), P('DefaultCamera', 'KString', '', '', 'Producer Perspective'),
        P('TimeMode', 'enum', '', '', 6),                         # 30 frames a second
        P('TimeSpanStart', 'KTime', 'Time', '', 0), P('TimeSpanStop', 'KTime', 'Time', '', longest * FRAME),
        P('CustomFrameRate', 'double', 'Number', '', float(E.FPS)))])
    documents = rec('Documents', kids=[rec('Count', I(1)), rec('Document', L(uid('document')), S('Scene'), S('Scene'), kids=[
        props70(P('SourceObject', 'object', '', ''), P('ActiveAnimStackName', 'KString', '', '', clips[0][0])),
        rec('RootNode', L(0))])])
    count = collections.Counter(o[0] for o in objects)
    kinds = ('NodeAttribute', 'Geometry', 'Model', 'Pose', 'Deformer', 'Material', 'Texture', 'Video',
             'AnimationStack', 'AnimationLayer', 'AnimationCurveNode', 'AnimationCurve')
    assert set(count) == set(kinds)
    definitions = fbx.definitions([('GlobalSettings', 1)] + [(k, count[k]) for k in kinds])
    connections = rec('Connections', kids=[
        rec('C', S('OO'), L(a), L(b)) if prop is None else rec('C', S('OP'), L(a), L(b), S(prop)) for a, b, prop in links])
    records = [header, rec('FileId', R(fbx.FILE_ID)), rec('CreationTime', S(fbx.TIME_ID)), rec('Creator', S(CREATOR)),
               settings, documents, rec('References'), definitions, rec('Objects', kids=objects), connections,
               rec('Takes', kids=takes)]
    return records, (len(points), len(order), len(bones), count['AnimationCurve'], keys)


def write():
    rig = Rig()
    write_mtl()
    v, tris = write_obj(OBJ, rig, rig.pos, 'The Sunfire Wyrm, laid out straight.')
    print('%s: %d vertices, %d triangles, %d groups' % (os.path.basename(OBJ), v, tris, len(set(rig.part(int(b)) for b in rig.bone))))
    reared = E.skinned(rig.gltf, rig.acc, 'Reared', 0)
    v, tris = write_obj(OBJ_REARED, rig, reared, 'The Sunfire Wyrm, reared out of a coil with its jaws open (as on its card).')
    print('%s: %d vertices, %d triangles' % (os.path.basename(OBJ_REARED), v, tris))
    records, (points, nodes, bones, curves, keys) = fbx_records(rig)
    with open(FBX, 'wb') as fh:
        fh.write(fbx.encode(records))
    print('%s: %.0f KB, %d control points, %d skeleton nodes (%d bones), %d takes, %d curves, %d keys' % (
        os.path.basename(FBX), os.path.getsize(FBX) / 1024.0, points, nodes, bones, len(rig.clips()), curves, keys))


# ---------------------------------------------------------------------- reading them back

def check_obj(rig):
    bad = False
    for path, pos in ((OBJ, rig.pos), (OBJ_REARED, E.skinned(rig.gltf, rig.acc, 'Reared', 0))):
        v, vt, vn, groups = read_obj(path)
        tri = pos.reshape(-1, 3, 3)
        dv = float(np.abs(v - tri).max())
        dt = float(np.abs(vt - rig.uv.reshape(-1, 3, 2)).max())
        dn = float(np.abs(vn - flat_normals(tri)[:, None, :]).max())
        wound = float(np.min(np.sum(flat_normals(v) * vn[:, 0], axis=1)))
        named_right = all(g == rig.part(int(rig.bone[f * 3])) for f, g in enumerate(groups))
        print('%-22s %d triangles: furthest vertex %.2g m, uv %.2g, normal %.2g; wound outwards: %s; groups right: %s' % (
            os.path.basename(path), len(v), dv, dt, dn, wound > 0.99, named_right))
        bad |= dv > 2e-6 or dt > 2e-6 or dn > 2e-5 or wound < 0.99 or not named_right
    bad |= not os.path.exists(os.path.join(DIR, SKIN_FILE)) or SKIN_FILE not in open(MTL).read()
    return bad


def check_fbx(rig):
    """Reads the FBX as a program would: its records, what is joined to what, the skeleton posed from its own curves
    and the mesh skinned by its own clusters."""
    raw = open(FBX, 'rb').read()
    top, version, foot = fbx.decode(raw)
    bad = version != fbx.VERSION or fbx.encode(top) != raw            # the records, written again, are the same file
    section = {r[0]: r for r in top}
    bad |= [r[0] for r in top] != ['FBXHeaderExtension', 'FileId', 'CreationTime', 'Creator', 'GlobalSettings', 'Documents',
                                   'References', 'Definitions', 'Objects', 'Connections', 'Takes']
    bad |= (fbx.value(section['FileId']), fbx.value(section['CreationTime']), foot[:16]) != (fbx.FILE_ID, fbx.TIME_ID, fbx.FOOT_ID)
    g = section['GlobalSettings']
    bad |= [fbx.prop(g, k)[0] for k in ('UpAxis', 'FrontAxis', 'CoordAxis', 'UnitScaleFactor', 'TimeMode')] != [1, 2, 0, 1.0, 6]

    # ---- the objects, and what is joined to what
    things = {}
    for r in section['Objects'][2]:
        name, kind = fbx.text(r, 1)
        assert fbx.value(r) not in things and fbx.value(r) != 0, 'two objects with one number'
        things[fbx.value(r)] = (r, name, fbx.text(r, 2))
    count = collections.Counter(r[0] for r, _, _ in things.values())
    for d in fbx.every(section['Definitions'], 'ObjectType'):
        if fbx.text(d) != 'GlobalSettings':
            bad |= count[fbx.text(d)] != fbx.value(fbx.find(d, 'Count'))
    oo, op = [], []
    for c in section['Connections'][2]:
        a, b = fbx.value(c, 1), fbx.value(c, 2)
        assert a in things and (b in things or b == 0), 'something is joined to nothing'
        (oo if fbx.text(c) == 'OO' else op).append((a, b, fbx.text(c, 3) if len(c[1]) > 3 else ''))
    kind = lambda i: things[i][0][0]        # noqa: E731
    under = lambda i, what: [a for a, b, _ in oo if b == i and kind(a) == what]      # noqa: E731
    models = [i for i in things if kind(i) == 'Model']
    parent = {a: b for a, b, _ in oo if kind(a) == 'Model' and (b == 0 or kind(b) == 'Model')}
    assert sorted(parent) == sorted(models), 'every model hangs from one thing'
    by_name = {things[i][1]: i for i in models}
    for i in models:        # a mesh model holds its geometry, any other holds what says it is a bone or a point
        holds = under(i, 'Geometry') if things[i][2] == 'Mesh' else under(i, 'NodeAttribute')
        bad |= len(holds) != 1 or things[holds[0]][2] != things[i][2]
    mesh = by_name['SunfireWyrm_Mesh']
    geometry_id = under(mesh, 'Geometry')[0]
    geometry = things[geometry_id][0]
    material = under(mesh, 'Material')
    texture = [a for a, b, p in op if b == material[0] and p == 'DiffuseColor']
    bad |= len(material) != 1 or len(texture) != 1 or len(under(texture[0], 'Video')) != 1
    bad |= fbx.text(fbx.find(things[texture[0]][0], 'RelativeFilename')) != SKIN_FILE

    # ---- the mesh
    points = fbx.value(fbx.find(geometry, 'Vertices')).reshape(-1, 3)
    polys = fbx.value(fbx.find(geometry, 'PolygonVertexIndex')).astype(int).reshape(-1, 3)
    assert (polys[:, 2] < 0).all() and (polys[:, :2] >= 0).all(), 'every polygon is a triangle'
    corners = np.where(polys < 0, -polys - 1, polys)
    assert corners.max() < len(points)
    uv_layer, normal_layer = fbx.find(geometry, 'LayerElementUV'), fbx.find(geometry, 'LayerElementNormal')
    uv = fbx.value(fbx.find(uv_layer, 'UV')).reshape(-1, 2)[fbx.value(fbx.find(uv_layer, 'UVIndex'))]
    normals = fbx.value(fbx.find(normal_layer, 'Normals')).reshape(-1, 3)
    bad |= uv.shape != rig.uv.shape or float(np.abs(uv - rig.uv).max()) > 2e-6 or normals.shape != (corners.size, 3)

    # ---- the skin
    skin = under(geometry_id, 'Deformer')
    assert len(skin) == 1 and things[skin[0]][2] == 'Skin'
    clusters = []
    weight = np.zeros(len(points))
    for c in under(skin[0], 'Deformer'):
        r = things[c][0]
        bone = under(c, 'Model')
        assert len(bone) == 1 and things[bone[0]][2] == 'LimbNode' and things[bone[0]][1] == things[c][1]
        index = fbx.find(r, 'Indexes')
        index = fbx.value(index).astype(int) if index else np.zeros(0, dtype=int)
        if len(index):
            weight[index] += fbx.value(fbx.find(r, 'Weights'))
        clusters.append((bone[0], index, fbx.value(fbx.find(r, 'TransformLink')).reshape(4, 4).T,
                         fbx.value(fbx.find(r, 'Transform')).reshape(4, 4).T))
    assert np.allclose(weight, 1.0), 'every control point is on exactly one bone'

    def pose(values):
        """Each model's place in the world, from {(model, 'T' | 'R' | 'S'): three numbers} over its own settings."""
        world = {0: np.eye(4)}

        def mat(i):
            if i not in world:
                r = things[i][0]
                m = np.eye(4)
                m[:3, :3] = euler_matrix(values.get((i, 'R'), fbx.prop(r, 'Lcl Rotation'))) * np.array(values.get((i, 'S'), fbx.prop(r, 'Lcl Scaling')))
                m[:3, 3] = values.get((i, 'T'), fbx.prop(r, 'Lcl Translation'))
                world[i] = mat(parent[i]) @ m
            return world[i]

        for i in models:
            mat(i)
        return world

    def skinned(world):
        out = np.zeros_like(points)
        for bone, index, link, _ in clusters:
            m = world[bone] @ np.linalg.inv(link)
            out[index] = points[index] @ m[:3, :3].T + m[:3, 3]
        return out[corners.reshape(-1)] / CM

    rest = pose({})
    for bone, _, link, transform in clusters:
        bad |= not np.allclose(transform @ link, np.eye(4), atol=1e-6)        # Transform undoes the bone's bind matrix,
        bad |= not np.allclose(rest[bone], link, atol=1e-6)                   # which is where the bone stands at rest,
    poses = [i for i in things if kind(i) == 'Pose']
    for node in fbx.every(things[poses[0]][0], 'PoseNode'):                   # and what the bind pose says of it
        bad |= not np.allclose(fbx.value(fbx.find(node, 'Matrix')).reshape(4, 4).T, rest[fbx.value(fbx.find(node, 'Node'))], atol=1e-6)
    d = float(np.abs(skinned(rest) - rig.pos).max())
    dn = float(np.abs(normals - np.repeat(flat_normals(rig.pos.reshape(-1, 3, 3)), 3, axis=0)).max())
    print('%-22s FBX %d, %d control points, %d triangles, %d models, %d clusters: bind pose furthest vertex %.2g m, normal %.2g' % (
        os.path.basename(FBX), version, len(points), len(polys), len(models), len(clusters), d, dn))
    bad |= d > 5e-6 or dn > 1e-9 or len(clusters) != len(rig.joints) or len(models) != len(rig.order) + 1

    # ---- the takes: every frame of each, posed from its curves
    stacks = {things[i][1]: i for i in things if kind(i) == 'AnimationStack'}
    takes = {fbx.text(t): t for t in fbx.every(section['Takes'], 'Take')}
    bad |= list(stacks) != rig.clips() or list(takes) != rig.clips()
    which = {v: k for k, v in LCL.items()}
    for name in rig.clips():
        data, frames = rig.clip(name)
        end = (frames - 1) * FRAME
        bad |= fbx.prop(things[stacks[name]][0], 'LocalStop') != [end] or fbx.value(fbx.find(takes[name], 'LocalTime'), 1) != end
        lay = under(stacks[name], 'AnimationLayer')
        assert len(lay) == 1
        chans = {}
        for node in under(lay[0], 'AnimationCurveNode'):
            (target, what), = [(b, p) for a, b, p in op if a == node]
            axes = {}
            for curve, axis in [(a, p) for a, b, p in op if b == node]:
                r = things[curve][0]
                times, vals = fbx.value(fbx.find(r, 'KeyTime')), fbx.value(fbx.find(r, 'KeyValueFloat'))
                assert len(times) == len(vals) == fbx.value(fbx.find(r, 'KeyAttrRefCount'))[0] and np.all(np.diff(times) > 0)
                assert np.all(times % FRAME == 0) and times[0] == 0 and times[-1] == end
                axes['XYZ'.index(axis[-1])] = (times / FRAME, vals.astype(float))
            assert sorted(axes) == [0, 1, 2] and (target, which[what]) not in chans
            chans[(target, which[what])] = axes
        bad |= len(chans) != len(data)
        worst = 0.0
        for f in range(frames):
            values = {k: [float(np.interp(f, *c[a])) for a in range(3)] for k, c in chans.items()}
            worst = max(worst, float(np.abs(skinned(pose(values)) - E.skinned(rig.gltf, rig.acc, name, f)).max()))
        # Between frames: leaving out the snaps (a bone turned 45 degrees or more in one frame has no in-between to speak of).
        off = 0.0
        for (target, what), c in chans.items():
            if what == 'R':
                e = np.stack([np.interp(np.arange(frames), *c[a]) for a in range(3)], axis=1)
                steps = [off_between(e[f], e[f + 1]) for f in range(frames - 1)]
                off = max([off] + [o for turn, o in steps if turn < 45.0])
        print('    %-7s %4d frames, %3d animated properties: furthest vertex from the .glb on any frame %.2g m; '
              'half-way between two frames, blended angles are at most %.2g degrees off' % (name, frames, len(chans), worst, off))
        bad |= worst > 2e-4
    return bad


def check():
    rig = Rig()
    bad = check_obj(rig)
    bad |= check_fbx(rig)
    print('PROBLEMS' if bad else 'the FBX and the OBJs match the .glb')
    return 1 if bad else 0


if __name__ == '__main__':
    args = sys.argv[1:]
    if not args:
        write()
    elif args[0] == 'check':
        sys.exit(check())
    else:
        print(__doc__)
