#!/usr/bin/env python3
"""FBX 7.4 files (the binary kind), read and written by hand: there is no FBX library here, and the file is plain enough.

An FBX file is a tree of records. A record has a name, a row of values (numbers, strings, arrays of numbers) and
records of its own. This module knows that much and no more: helpers to build records, encode() to make a file of them,
decode() to read a file back, and the tables every FBX carries (the "property templates": the default of every setting
an object of each kind has). What the records say is up to whoever builds them: Tools/convert_sunfire_wyrm.py builds
the dragon's.

    python3 Tools/fbx_binary.py FILE.fbx [NAME ...]     prints a file's records (all, or the named top-level ones)

The layout is the one Blender's exporter writes, which every program that reads FBX is used to.
"""
import struct
import sys
import zlib

import numpy as np

MAGIC = b'Kaydara FBX Binary  \x00\x1a\x00'
VERSION = 7400
END = b'\0' * 13            # closes a list of records: a record's worth of nothing

# An FBX's id and its last bytes have to agree with the time it says it was written, by a rule Autodesk has not
# published. Blender's exporter always gives the same time, with the id and the last bytes that go with it; so does this.
TIME_ID = b'1970-01-01 10:00:00:000'
FILE_ID = bytes.fromhex('28b32aebb624ccc2bfc8b02aa92bfcf1')
FOOT_ID = bytes.fromhex('fabcab09d0c8d466b176fb831cf7267e')
FOOT_END = bytes.fromhex('f85a8c6adef5d97eece90ce3758f290b')

SCALARS = {'Y': '<h', 'C': '<B', 'I': '<i', 'F': '<f', 'D': '<d', 'L': '<q'}
ARRAYS = {'f': '<f4', 'd': '<f8', 'l': '<i8', 'i': '<i4', 'b': '<i1'}


# ---------------------------------------------------------------------- building records

def rec(name, *values, kids=()):
    """A record: [name, its values (each a (type letter, value)), the records inside it]."""
    return [name, list(values), list(kids)]


def I(v):       # noqa: E743
    return ('I', int(v))


def L(v):
    return ('L', int(v))


def D(v):
    return ('D', float(v))


def C(v):
    return ('C', int(v))


def S(v):
    return ('S', v.encode('utf-8') if isinstance(v, str) else bytes(v))


def R(v):
    return ('R', bytes(v))


def A(kind, values):
    """An array: kind is 'd' (doubles), 'f' (floats), 'i' (ints) or 'l' (long ints)."""
    return (kind, np.ascontiguousarray(np.asarray(values).reshape(-1), dtype=ARRAYS[kind]))


def named(name, kind):
    """An object's name as a binary FBX holds it: the name, then what kind of thing it is."""
    return S(name.encode('utf-8') + b'\x00\x01' + kind.encode('utf-8'))


WHOLE = ('int', 'enum', 'bool', 'Visibility Inheritance')
LONG = ('KTime', 'ULongLong')
TEXT = ('KString', 'DateTime')
BARE = ('object', 'Compound')


def P(name, kind, sub, flags, *values):
    """A property (a row of a Properties70 record): its name, its two type names, its flags ('A': can be animated,
    'A+': is) and its values, each stored the way its type is."""
    if kind in BARE:
        vals = []
    elif kind in TEXT:
        vals = [S(v) for v in values]
    elif kind in LONG:
        vals = [L(v) for v in values]
    elif kind in WHOLE:
        vals = [I(v) for v in values]
    else:
        vals = [D(v) for v in values]
    return rec('P', S(name), S(kind), S(sub), S(flags), *vals)


def props70(*rows):
    return rec('Properties70', kids=rows)


# ---------------------------------------------------------------------- writing

def value_bytes(kind, v):
    code = kind.encode('ascii')
    if kind in SCALARS:
        return code + struct.pack(SCALARS[kind], v)
    if kind in 'SR':
        return code + struct.pack('<I', len(v)) + v
    if isinstance(v, bytes):            # an array just as a file had it (decode(..., arrays=False))
        return code + v
    raw = np.ascontiguousarray(v, dtype=ARRAYS[kind]).tobytes()
    packed = zlib.compress(raw, 1) if len(raw) > 128 else raw           # long arrays are deflated, short ones not
    return code + struct.pack('<III', len(v), 1 if len(raw) > 128 else 0, len(packed)) + packed


def encode(records, version=VERSION, foot_id=FOOT_ID):
    """The file that holds these top-level records."""
    assert version < 7500, 'FBX 7.5 and later number their bytes with 64 bits; this writes the 32-bit files before it'
    out = bytearray(MAGIC + struct.pack('<I', version))

    def put(r, last):
        name, values, kids = r
        body = b''.join(value_bytes(k, v) for k, v in values)
        at = len(out)
        out.extend(struct.pack('<IIIB', 0, len(values), len(body), len(name)) + name.encode('ascii') + body)
        if kids:
            for k in kids:
                put(k, k is kids[-1])
            out.extend(END)
        elif not values and not last:
            out.extend(END)             # a record with nothing at all in it is closed like a list, unless it is the last
        struct.pack_into('<I', out, at, len(out))       # where the record ends

    for r in records:
        put(r, r is records[-1])
    out.extend(END)
    out.extend(foot_id + b'\0' * 4)
    out.extend(b'\0' * (16 - len(out) % 16))            # up to a multiple of 16 (a whole 16 if it is one already)
    out.extend(struct.pack('<I', version) + b'\0' * 120 + FOOT_END)
    return bytes(out)


# ---------------------------------------------------------------------- reading

def decode(raw, arrays=True):
    """A binary FBX's top-level records, its version and what follows its records (its footer). With arrays=False an
    array is left as the bytes the file holds for it, so encode() gives the same file back."""
    assert raw[:23] == MAGIC, 'not a binary FBX'
    version = struct.unpack_from('<I', raw, 23)[0]
    assert version < 7500, 'FBX %d: this reads the 32-bit files before 7.5' % version

    def value(at):
        kind = chr(raw[at])
        at += 1
        if kind in SCALARS:
            return (kind, struct.unpack_from(SCALARS[kind], raw, at)[0]), at + struct.calcsize(SCALARS[kind])
        if kind in ARRAYS:
            n, packing, size = struct.unpack_from('<III', raw, at)
            assert packing in (0, 1) and at + 12 + size <= len(raw), 'an array runs off the file'
            if not arrays:
                return (kind, raw[at:at + 12 + size]), at + 12 + size
            data = raw[at + 12:at + 12 + size]
            if packing:
                data = zlib.decompress(data)
            assert len(data) == n * np.dtype(ARRAYS[kind]).itemsize, 'an array is not as long as it says'
            return (kind, np.frombuffer(data, dtype=ARRAYS[kind])), at + 12 + size
        assert kind in 'SR', 'unknown kind of value %r at byte %d' % (kind, at - 1)
        n = struct.unpack_from('<I', raw, at)[0]
        assert at + 4 + n <= len(raw), 'a string runs off the file'
        return (kind, raw[at + 4:at + 4 + n]), at + 4 + n

    def record(at):
        end, count, size, length = struct.unpack_from('<IIIB', raw, at)
        if end == 0:
            assert raw[at:at + 13] == END
            return None, at + 13
        at += 13
        name = raw[at:at + length].decode('ascii')
        at += length
        stop = at + size
        values = []
        for _ in range(count):
            v, at = value(at)
            values.append(v)
        assert at == stop, '%s: its values are not as long as it says' % name
        kids = []
        while at < end:
            k, at = record(at)
            if k is None:
                break
            kids.append(k)
        assert at == end, '%s: does not end where it says' % name
        return [name, values, kids], at

    at, top = 27, []
    while True:
        r, at = record(at)
        if r is None:
            break
        top.append(r)
    return top, version, raw[at:]


def find(record, name):
    """The first record called 'name' inside this one (None if there is none)."""
    for k in record[2]:
        if k[0] == name:
            return k
    return None


def every(record, name):
    return [k for k in record[2] if k[0] == name]


def value(record, i=0):
    return record[1][i][1]


def text(record, i=0):
    """A string value; an object's name comes out as (name, kind)."""
    raw = record[1][i][1]
    if b'\x00\x01' in raw:
        return tuple(x.decode('utf-8') for x in raw.split(b'\x00\x01'))
    return raw.decode('utf-8')


def prop(record, name):
    """A property's values out of a record's Properties70 (None if it isn't set there)."""
    table = find(record, 'Properties70')
    for p in (table[2] if table else []):
        if p[1][0][1] == name.encode('utf-8'):
            return [v for _, v in p[1][4:]]
    return None


# ---------------------------------------------------------------------- the tables every FBX carries
# Each kind of object's property template: 'name | type | type | flags | values'. These are the format's own defaults,
# as Blender's exporter and Autodesk's write them.

TEMPLATE_ROWS = '''
# Geometry FbxMesh
Color | ColorRGB | Color |  | 0.8, 0.8, 0.8
BBoxMin | Vector3D | Vector |  | 0, 0, 0
BBoxMax | Vector3D | Vector |  | 0, 0, 0
Primary Visibility | bool |  |  | 1
Casts Shadows | bool |  |  | 1
Receive Shadows | bool |  |  | 1
# Model FbxNode
QuaternionInterpolate | enum |  |  | 0
RotationOffset | Vector3D | Vector |  | 0, 0, 0
RotationPivot | Vector3D | Vector |  | 0, 0, 0
ScalingOffset | Vector3D | Vector |  | 0, 0, 0
ScalingPivot | Vector3D | Vector |  | 0, 0, 0
TranslationActive | bool |  |  | 0
TranslationMin | Vector3D | Vector |  | 0, 0, 0
TranslationMax | Vector3D | Vector |  | 0, 0, 0
TranslationMinX | bool |  |  | 0
TranslationMinY | bool |  |  | 0
TranslationMinZ | bool |  |  | 0
TranslationMaxX | bool |  |  | 0
TranslationMaxY | bool |  |  | 0
TranslationMaxZ | bool |  |  | 0
RotationOrder | enum |  |  | 0
RotationSpaceForLimitOnly | bool |  |  | 0
RotationStiffnessX | double | Number |  | 0
RotationStiffnessY | double | Number |  | 0
RotationStiffnessZ | double | Number |  | 0
AxisLen | double | Number |  | 10
PreRotation | Vector3D | Vector |  | 0, 0, 0
PostRotation | Vector3D | Vector |  | 0, 0, 0
RotationActive | bool |  |  | 0
RotationMin | Vector3D | Vector |  | 0, 0, 0
RotationMax | Vector3D | Vector |  | 0, 0, 0
RotationMinX | bool |  |  | 0
RotationMinY | bool |  |  | 0
RotationMinZ | bool |  |  | 0
RotationMaxX | bool |  |  | 0
RotationMaxY | bool |  |  | 0
RotationMaxZ | bool |  |  | 0
InheritType | enum |  |  | 0
ScalingActive | bool |  |  | 0
ScalingMin | Vector3D | Vector |  | 0, 0, 0
ScalingMax | Vector3D | Vector |  | 1, 1, 1
ScalingMinX | bool |  |  | 0
ScalingMinY | bool |  |  | 0
ScalingMinZ | bool |  |  | 0
ScalingMaxX | bool |  |  | 0
ScalingMaxY | bool |  |  | 0
ScalingMaxZ | bool |  |  | 0
GeometricTranslation | Vector3D | Vector |  | 0, 0, 0
GeometricRotation | Vector3D | Vector |  | 0, 0, 0
GeometricScaling | Vector3D | Vector |  | 1, 1, 1
MinDampRangeX | double | Number |  | 0
MinDampRangeY | double | Number |  | 0
MinDampRangeZ | double | Number |  | 0
MaxDampRangeX | double | Number |  | 0
MaxDampRangeY | double | Number |  | 0
MaxDampRangeZ | double | Number |  | 0
MinDampStrengthX | double | Number |  | 0
MinDampStrengthY | double | Number |  | 0
MinDampStrengthZ | double | Number |  | 0
MaxDampStrengthX | double | Number |  | 0
MaxDampStrengthY | double | Number |  | 0
MaxDampStrengthZ | double | Number |  | 0
PreferedAngleX | double | Number |  | 0
PreferedAngleY | double | Number |  | 0
PreferedAngleZ | double | Number |  | 0
LookAtProperty | object |  |  |
UpVectorProperty | object |  |  |
Show | bool |  |  | 1
NegativePercentShapeSupport | bool |  |  | 1
DefaultAttributeIndex | int | Integer |  | -1
Freeze | bool |  |  | 0
LODBox | bool |  |  | 0
Lcl Translation | Lcl Translation |  | A | 0, 0, 0
Lcl Rotation | Lcl Rotation |  | A | 0, 0, 0
Lcl Scaling | Lcl Scaling |  | A | 1, 1, 1
Visibility | Visibility |  | A | 1
Visibility Inheritance | Visibility Inheritance |  |  | 1
# Material FbxSurfacePhong
ShadingModel | KString |  |  | Phong
MultiLayer | bool |  |  | 0
EmissiveColor | Color |  | A | 0, 0, 0
EmissiveFactor | Number |  | A | 1
AmbientColor | Color |  | A | 0.2, 0.2, 0.2
AmbientFactor | Number |  | A | 1
DiffuseColor | Color |  | A | 0.8, 0.8, 0.8
DiffuseFactor | Number |  | A | 1
TransparentColor | Color |  | A | 0, 0, 0
TransparencyFactor | Number |  | A | 0
Opacity | Number |  | A | 1
NormalMap | Vector3D | Vector |  | 0, 0, 0
Bump | Vector3D | Vector |  | 0, 0, 0
BumpFactor | double | Number |  | 1
DisplacementColor | ColorRGB | Color |  | 0, 0, 0
DisplacementFactor | double | Number |  | 1
VectorDisplacementColor | ColorRGB | Color |  | 0, 0, 0
VectorDisplacementFactor | double | Number |  | 1
SpecularColor | Color |  | A | 0.2, 0.2, 0.2
SpecularFactor | Number |  | A | 1
Shininess | Number |  | A | 20
ShininessExponent | Number |  | A | 20
ReflectionColor | Color |  | A | 0, 0, 0
ReflectionFactor | Number |  | A | 1
# Texture FbxFileTexture
TextureTypeUse | enum |  |  | 0
AlphaSource | enum |  |  | 2
Texture alpha | double | Number |  | 1
PremultiplyAlpha | bool |  |  | 1
CurrentTextureBlendMode | enum |  |  | 1
CurrentMappingType | enum |  |  | 0
UVSet | KString |  |  | default
WrapModeU | enum |  |  | 0
WrapModeV | enum |  |  | 0
UVSwap | bool |  |  | 0
Translation | Vector3D | Vector |  | 0, 0, 0
Rotation | Vector3D | Vector |  | 0, 0, 0
Scaling | Vector3D | Vector |  | 1, 1, 1
TextureRotationPivot | Vector3D | Vector |  | 0, 0, 0
TextureScalingPivot | Vector3D | Vector |  | 0, 0, 0
UseMaterial | bool |  |  | 0
UseMipMap | bool |  |  | 0
# Video FbxVideo
Width | int | Integer |  | 0
Height | int | Integer |  | 0
Path | KString | Url |  |
AccessMode | enum |  |  | 0
StartFrame | int | Integer |  | 0
StopFrame | int | Integer |  | 0
Offset | KTime | Time |  | 0
PlaySpeed | double | Number |  | 0
FreeRunning | bool |  |  | 0
Loop | bool |  |  | 0
InterlaceMode | enum |  |  | 0
ImageSequence | bool |  |  | 0
ImageSequenceOffset | int | Integer |  | 0
FrameRate | double | Number |  | 0
LastFrame | int | Integer |  | 0
# AnimationStack FbxAnimStack
Description | KString |  |  |
LocalStart | KTime | Time |  | 0
LocalStop | KTime | Time |  | 0
ReferenceStart | KTime | Time |  | 0
ReferenceStop | KTime | Time |  | 0
# AnimationLayer FbxAnimLayer
Weight | Number |  | A | 100
Mute | bool |  |  | 0
Solo | bool |  |  | 0
Lock | bool |  |  | 0
Color | ColorRGB | Color |  | 0.8, 0.8, 0.8
BlendMode | enum |  |  | 0
RotationAccumulationMode | enum |  |  | 0
ScaleAccumulationMode | enum |  |  | 0
BlendModeBypass | ULongLong |  |  | 0
# AnimationCurveNode FbxAnimCurveNode
d | Compound |  |  |
'''


def _templates():
    out, kind = {}, None
    for line in TEMPLATE_ROWS.strip().split('\n'):
        if line.startswith('#'):
            _, kind, cls = line.split()
            out[kind] = (cls, [])
            continue
        name, ptype, sub, flags, vals = [x.strip() for x in line.split('|')]
        if ptype in BARE:
            row = ()
        elif ptype in TEXT:
            row = (vals,)
        else:
            row = tuple(float(v) for v in vals.split(','))
        out[kind][1].append((name, ptype, sub, flags) + row)
    return out


TEMPLATES = _templates()


def definitions(counts):
    """The Definitions record: how many objects of each kind the file holds ((kind, how many), in order), and each
    kind's property template."""
    kids = [rec('Version', I(100)), rec('Count', I(sum(n for _, n in counts)))]
    for kind, n in counts:
        body = [rec('Count', I(n))]
        if kind in TEMPLATES:
            cls, rows = TEMPLATES[kind]
            body.append(rec('PropertyTemplate', S(cls), kids=[props70(*[P(*row) for row in rows])]))
        kids.append(rec('ObjectType', S(kind), kids=body))
    return rec('Definitions', kids=kids)


# ---------------------------------------------------------------------- looking at a file

def show(r, depth=0, most=6, out=sys.stdout):
    name, values, kids = r
    parts = []
    for kind, v in values:
        if kind in ARRAYS:
            parts.append('%s[%d]{%s%s}' % (kind, len(v), ','.join('%.6g' % x for x in v[:most]), ',...' if len(v) > most else ''))
        elif kind in 'SR':
            parts.append('%r' % (v if len(v) < 90 else v[:90] + b'...'))
        else:
            parts.append('%s:%r' % (kind, v))
    out.write('  ' * depth + name + ': ' + ', '.join(parts) + '\n')
    for k in kids:
        show(k, depth + 1, most, out)


if __name__ == '__main__':
    if len(sys.argv) < 2:
        print(__doc__)
        sys.exit(0)
    top, version, foot = decode(open(sys.argv[1], 'rb').read())
    print('FBX %d, %d top-level records, %d bytes after them' % (version, len(top), len(foot)))
    for r in top:
        if len(sys.argv) == 2 or r[0] in sys.argv[2:]:
            show(r)
