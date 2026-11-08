"""Builds the art pilot's models (hob, pan, steak) and exports them as FBX for Unity.

Run from the repository root:
  /Applications/Blender.app/Contents/MacOS/Blender -b -P blender/build_pilot_models.py
Add `-- --preview <folder>` to also render a PNG of each model.

Shapes are generated rather than saved as .blend files, so changes can be reviewed as text.
Each model's coordinates are the object's own: 1 unit = 1 metre, origin at the base, centred.
Mesh names must be unique across all models: Blender renames duplicates, and Unity finds meshes by name.

Exporting maps Blender (x, y, z) to Unity (-x, z, -y), so Blender's +Y is the side facing the
camera in the game (Unity's south, -Z). Unity's `ArtPilotAssetTests` checks these assumptions
against the imported meshes.
"""

import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MODELS_FOLDER = os.path.join(REPO_ROOT, "Assets", "_Project", "Art", "Models")

# Edges sharper than this stay crisp; softer ones shade smooth. This is what gives the chunky,
# soft-edged look a bevel needs in order to catch the light.
SHARP_ANGLE = math.radians(40.0)
BEVEL_SEGMENTS = 2
LATHE_SEGMENTS = 48

# Must match the hob's top in GreyboxKitchenBuilder (CounterHeight).
HOB_HEIGHT = 0.9
# The pan rests on this height in the hob's model; food sits on the floor at PAN_FLOOR (PanDepth).
PAN_FLOOR = 0.04


def add_box(bm, size, centre, bevel=0.0):
    """Adds a box, optionally bevelled, to `bm`."""
    transform = Matrix.Translation(centre) @ Matrix.Diagonal((*size, 1.0))
    created = bmesh.ops.create_cube(bm, size=1.0, matrix=transform)
    if bevel > 0.0:
        verts = created["verts"]
        edges = list({edge for vert in verts for edge in vert.link_edges})
        bmesh.ops.bevel(bm, geom=edges, offset=bevel, segments=BEVEL_SEGMENTS, profile=0.5,
                        affect='EDGES', clamp_overlap=True)


def add_lathe(bm, profile, segments=LATHE_SEGMENTS, centre=(0.0, 0.0, 0.0), axis_matrix=None):
    """Spins a (radius, height) profile round the Z axis. A radius of 0 closes that end."""
    matrix = Matrix.Translation(centre) @ (axis_matrix or Matrix.Identity(4))
    rings = []
    for radius, height in profile:
        if radius <= 0.0:
            rings.append([bm.verts.new(matrix @ Vector((0.0, 0.0, height)))])
            continue
        rings.append([
            bm.verts.new(matrix @ Vector((
                radius * math.cos(2.0 * math.pi * i / segments),
                radius * math.sin(2.0 * math.pi * i / segments),
                height)))
            for i in range(segments)
        ])
    connect_rings(bm, rings)


def connect_rings(bm, rings):
    """Joins consecutive rings of vertices with quads, or triangles where a ring is one point."""
    for lower, upper in zip(rings, rings[1:]):
        count = max(len(lower), len(upper))
        for i in range(count):
            a = lower[i % len(lower)]
            b = lower[(i + 1) % len(lower)]
            c = upper[(i + 1) % len(upper)]
            d = upper[i % len(upper)]
            if len(lower) == 1:
                bm.faces.new([a, c, d])  # fan from a single point below
            elif len(upper) == 1:
                bm.faces.new([a, b, c])  # fan up to a single point above
            else:
                bm.faces.new([a, b, c, d])


def finish(name, bm):
    """Turns `bm` into an object with outward normals and hard/soft edges decided by angle."""
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.edges.ensure_lookup_table()
    for face in bm.faces:
        face.smooth = True
    for edge in bm.edges:
        edge.smooth = not edge.is_manifold or edge.calc_face_angle() < SHARP_ANGLE
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return obj


def build_hob():
    """A squat range: top slab, carcass, oven door and knobs on the side facing the camera."""
    bm = bmesh.new()
    top_thickness = 0.06
    plinth = 0.07
    add_box(bm, (1.0, 1.0, top_thickness), (0.0, 0.0, HOB_HEIGHT - top_thickness / 2.0), bevel=0.018)
    carcass_height = HOB_HEIGHT - top_thickness - plinth
    add_box(bm, (0.96, 0.92, carcass_height), (0.0, -0.04, plinth + carcass_height / 2.0), bevel=0.02)
    add_box(bm, (0.88, 0.84, plinth), (0.0, -0.04, plinth / 2.0))
    add_box(bm, (0.78, 0.03, 0.42), (0.0, 0.43, 0.38), bevel=0.012)  # oven door
    add_box(bm, (0.56, 0.03, 0.035), (0.0, 0.46, 0.53), bevel=0.012)  # door handle

    # Knobs: chunky enough to read from above, under the slab's front edge.
    knob_axis = Matrix.Rotation(math.radians(-90.0), 4, 'X')
    for x in (-0.28, 0.0, 0.28):
        add_lathe(bm, [(0.05, 0.0), (0.05, 0.03), (0.04, 0.05), (0.0, 0.05)], segments=16,
                  centre=(x, 0.44, 0.73), axis_matrix=knob_axis)
    body = finish("HobBody", bm)

    # The burner glows as one piece (HobView tints it). Its surface stays below the pan's
    # resting height of 0.91, so the pan never sinks into it.
    bm = bmesh.new()
    add_lathe(bm, [
        (0.31, HOB_HEIGHT), (0.31, HOB_HEIGHT + 0.004), (0.30, HOB_HEIGHT + 0.008),
        (0.215, HOB_HEIGHT + 0.008), (0.20, HOB_HEIGHT + 0.004), (0.0, HOB_HEIGHT + 0.004),
    ])
    burner = finish("HobBurner", bm)
    return [body, burner]


def build_pan():
    """A thick-based pan with a rounded rim, and a handle towards the camera (+Y here)."""
    bm = bmesh.new()
    add_lathe(bm, [
        (0.0, 0.0), (0.20, 0.0), (0.212, 0.008), (0.218, 0.035), (0.22, 0.07),
        (0.214, 0.076), (0.205, 0.07), (0.196, PAN_FLOOR + 0.004), (0.185, PAN_FLOOR), (0.0, PAN_FLOOR),
    ])
    body = finish("PanBody", bm)

    bm = bmesh.new()
    length = 0.24
    start = 0.20  # overlaps the wall, so no gap shows
    created = bmesh.ops.create_cube(bm, size=1.0)
    for vert in created["verts"]:
        along = vert.co.y + 0.5  # 0 at the pan, 1 at the far end
        width = 0.05 + 0.012 * along  # widens towards the grip
        vert.co = Vector((vert.co.x * width, start + along * length, 0.052 + vert.co.z * 0.03 + along * 0.016))
    bmesh.ops.bevel(bm, geom=list(bm.edges), offset=0.009, segments=BEVEL_SEGMENTS, profile=0.5,
                    affect='EDGES', clamp_overlap=True)
    handle = finish("PanHandle", bm)
    return [body, handle]


def steak_outline(angle):
    """A slightly lopsided rounded shape, so it reads as a cut of meat rather than a disc."""
    half_x, half_y = 0.14, 0.11
    cos_a, sin_a = math.cos(angle), math.sin(angle)
    # A superellipse with an exponent above 2 is squarer than an ellipse: plumper, more meat-like.
    exponent = 2.6
    radius = (abs(cos_a / half_x) ** exponent + abs(sin_a / half_y) ** exponent) ** (-1.0 / exponent)
    radius *= 1.0 + 0.05 * math.sin(2.0 * angle + 0.6) + 0.025 * math.sin(3.0 * angle + 2.0)
    return radius * cos_a, radius * sin_a


def build_steak():
    """One mesh. The shader colours it by height and normal, so no separate bands are needed."""
    thickness = 0.05
    segments = 64
    # (outline scale, height), bottom to top: rounded at both edges, with flat faces that end in
    # a smaller inner ring so the top keeps its soft rim when shaded smooth.
    profile = [
        (0.0, 0.0), (0.70, 0.0), (0.93, 0.0), (1.0, 0.012), (1.0, thickness * 0.5),
        (1.0, thickness - 0.012), (0.93, thickness), (0.70, thickness), (0.0, thickness),
    ]
    bm = bmesh.new()
    rings = []
    edge_distance = {}
    for scale, height in profile:
        if scale <= 0.0:
            ring = [bm.verts.new((0.0, 0.0, height))]
        else:
            ring = []
            for i in range(segments):
                x, y = steak_outline(2.0 * math.pi * i / segments)
                ring.append(bm.verts.new((x * scale, y * scale, height)))
        for vert in ring:
            edge_distance[vert] = scale
        rings.append(ring)
    connect_rings(bm, rings)

    # UV x is how far a point is from the centre to the outline. The shader uses it to place fat
    # round the edge, which it can't work out from position alone on an irregular shape.
    uv_layer = bm.loops.layers.uv.new("UVMap")
    for face in bm.faces:
        for loop in face.loops:
            loop[uv_layer].uv = (edge_distance[loop.vert], 0.5)
    return [finish("SM_Steak", bm)]


# Storage units are tall, with two tiers of two slots seen from the front, and the same 1 m
# footprint as other stations. Slot positions live in StorageArt.SlotPosition; the models only
# give the shelves those slots sit on, at heights 0.45 and 1.05.
FRIDGE_HEIGHT = 0.9  # a chest at counter height (decision 2026-11-08)
SHELF_HEIGHT = 1.05  # the upper tier's board is its top (decision 2026-11-08)
TIER_TOPS = (0.45, 1.05)
# The chest's four slots sit on one level, its floor, near the top so the stock shows through the
# lid from the camera's 55° view on any side. Must match StorageArt.ChestFloor.
FRIDGE_FLOOR = 0.7
# The glass lid hinges along the back of the top, a little in from the edge, so even upright it
# stays over the footprint; its meshes are built around that hinge, lying shut towards the front.
# Must match StorageArt.LidHinge: the game's (0, 0.9, 0.44).
FRIDGE_LID_HINGE = (0.0, -0.44, FRIDGE_HEIGHT)
# The chef bends at the waist: the torso's origin is there. Must match the player builder.
CHEF_WAIST = 0.85


def build_fridge():
    """A chest fridge at counter height (decision 2026-11-08): a white box with a pale inside, four
    baskets on one level near the top, and a glass lid in a white frame, so the stock shows from
    above whichever way it's turned."""
    wall = 0.04
    bm = bmesh.new()
    add_box(bm, (0.96, 0.96, 0.12), (0.0, 0.0, 0.06), bevel=0.01)  # plinth
    for x in (-0.48, 0.48):
        add_box(bm, (wall, 1.0, FRIDGE_HEIGHT - 0.12), (x, 0.0, 0.12 + (FRIDGE_HEIGHT - 0.12) / 2.0), bevel=0.01)
    for y in (-0.48, 0.48):
        add_box(bm, (1.0, wall, FRIDGE_HEIGHT - 0.12), (0.0, y, 0.12 + (FRIDGE_HEIGHT - 0.12) / 2.0), bevel=0.01)
    body = finish("FridgeBody", bm)

    # The pale inside: a floor the baskets stand on, and walls down to it.
    bm = bmesh.new()
    add_box(bm, (0.92, 0.92, 0.02), (0.0, 0.0, FRIDGE_FLOOR - 0.01))
    for x in (-0.455, 0.455):
        add_box(bm, (0.01, 0.92, FRIDGE_HEIGHT - FRIDGE_FLOOR), (x, 0.0, (FRIDGE_HEIGHT + FRIDGE_FLOOR) / 2.0))
    for y in (-0.455, 0.455):
        add_box(bm, (0.92, 0.01, FRIDGE_HEIGHT - FRIDGE_FLOOR), (0.0, y, (FRIDGE_HEIGHT + FRIDGE_FLOOR) / 2.0))
    liner = finish("FridgeLiner", bm)

    # The lid: a frame round glass, lying shut over the top, towards the front from its hinge.
    size, length, bar, depth = 0.96, 0.9, 0.06, 0.03
    bm = bmesh.new()
    for x in (-(size - bar) / 2.0, (size - bar) / 2.0):
        add_box(bm, (bar, length, depth), (x, length / 2.0, depth / 2.0), bevel=0.006)
    for y in (bar / 2.0, length - bar / 2.0):
        add_box(bm, (size - 2 * bar, bar, depth), (0.0, y, depth / 2.0), bevel=0.006)
    add_box(bm, (0.3, 0.03, 0.02), (0.0, length - 0.015, depth + 0.01), bevel=0.006)  # handle, at the front
    frame = finish("FridgeLidFrame", bm)
    frame.location = FRIDGE_LID_HINGE

    bm = bmesh.new()
    add_box(bm, (size - 2 * bar, length - 2 * bar, 0.008), (0.0, length / 2.0, depth / 2.0))
    glass = finish("FridgeLidGlass", bm)
    glass.location = FRIDGE_LID_HINGE

    bm = bmesh.new()
    add_box(bm, (0.36, 0.34, 0.03), (0.0, 0.0, 0.015), bevel=0.006)
    basket = finish("FridgeBasket", bm)
    basket.location = (0.22, 0.2, FRIDGE_FLOOR)
    return [body, liner, frame, glass, basket]


# The shelf's boards are shallow and centred, so what's on the lower tier isn't hidden under the
# upper one from the camera's 55° view, whichever way the shelf is turned.
SHELF_BOARD_DEPTH = 0.4


def build_shelf():
    """An open rack (decision 2026-11-08): two tiers of two slots on shallow boards, open on every
    side, the upper tier's board its top, sacks on the bottom board."""
    half = SHELF_BOARD_DEPTH / 2.0
    bm = bmesh.new()
    for x in (-0.475, 0.475):
        for y in (-half + 0.02, half - 0.02):
            add_box(bm, (0.04, 0.04, SHELF_HEIGHT), (x, y, SHELF_HEIGHT / 2.0), bevel=0.008)
    for top in (0.06,) + TIER_TOPS:
        add_box(bm, (1.0, SHELF_BOARD_DEPTH, 0.04), (0.0, 0.0, top - 0.02), bevel=0.006)
    body = finish("ShelfBody", bm)

    bm = bmesh.new()
    add_box(bm, (0.34, 0.3, 0.26), (-0.22, 0.0, 0.19), bevel=0.06)
    add_box(bm, (0.3, 0.28, 0.22), (0.2, 0.0, 0.17), bevel=0.05)
    goods = finish("ShelfGoods", bm)

    bm = bmesh.new()
    add_box(bm, (0.3, 0.24, 0.03), (0.0, 0.0, 0.015), bevel=0.008)
    basket = finish("ShelfBasket", bm)
    basket.location = (0.0, 0.0, TIER_TOPS[1])
    return [body, goods, basket]


def build_chef():
    """A placeholder chef in two parts, so it can bend at the waist. Not final character art."""
    bm = bmesh.new()
    add_lathe(bm, [(0.0, 0.0), (0.28, 0.0), (0.33, 0.03), (0.35, 0.1), (0.35, 0.75),
                   (0.3, 0.88), (0.18, 0.94), (0.0, 0.95)], segments=32)
    legs = finish("ChefLegs", bm)

    # Its origin is the waist; it starts a little below, so no gap opens when it bends.
    bm = bmesh.new()
    add_lathe(bm, [(0.0, -0.05), (0.25, -0.05), (0.33, 0.0), (0.35, 0.08), (0.35, 0.55),
                   (0.33, 0.68), (0.26, 0.78), (0.14, 0.84), (0.0, 0.85)], segments=32)
    torso = finish("ChefTorso", bm)
    torso.location = (0.0, 0.0, CHEF_WAIST)
    return [legs, torso]


# Kitchenware. A plate is 0.32 m across (GreyboxKitchenware.PlateDiameter); its origin is the
# centre of its base. Food sits in its well at 0.012 m.
PLATE_RADIUS = 0.16
COUNTER_HEIGHT = 0.9


def build_plate():
    """A white plate with a shallow well and a gold rim (the rim is its own mesh and colour)."""
    bm = bmesh.new()
    add_lathe(bm, [
        (0.0, 0.0), (0.1, 0.0), (0.112, 0.004), (PLATE_RADIUS, 0.017), (PLATE_RADIUS + 0.001, 0.02),
        (0.15, 0.0205), (0.11, 0.012), (0.0, 0.012),
    ])
    body = finish("PlateBody", bm)

    bm = bmesh.new()
    add_lathe(bm, [(0.143, 0.0185), (0.15, 0.0215), (0.158, 0.0205), (0.1615, 0.018), (0.15, 0.017), (0.143, 0.0185)])
    rim = finish("PlateRim", bm)
    return [body, rim]


def add_rack(bm, length, width=0.36, height=0.2, wire=0.012):
    """A wire dish rack: an arch at each end and rails along the bottom. Plates stand in it
    facing the camera, one behind the other, from the back (Blender -Y) to the front (+Y)."""
    half_width = width / 2.0
    for y in (-length / 2.0, length / 2.0):
        for x in (-half_width, half_width):
            add_box(bm, (wire, wire, height), (x, y, height / 2.0), bevel=0.004)
        add_box(bm, (width + wire, wire, wire), (0.0, y, height), bevel=0.004)
    for x in (-half_width, half_width):
        add_box(bm, (wire, length, wire), (x, 0.0, wire / 2.0), bevel=0.004)
    for x in (-0.06, 0.06):  # the plates rest on these
        add_box(bm, (wire, length, wire), (x, 0.0, 0.02), bevel=0.004)


def build_plate_racks():
    """A rack for the plate stack (six plates) and a short one for the sink's drainer."""
    bm = bmesh.new()
    add_rack(bm, length=0.34)
    long_rack = finish("PlateRack", bm)
    bm = bmesh.new()
    add_rack(bm, length=0.16)
    short_rack = finish("PlateRackShort", bm)
    short_rack.location = (0.6, 0.0, 0.0)  # apart from the long one, for previews only
    return [long_rack, short_rack]


def build_sink():
    """A stainless sink unit: a basin on the game's left, a ribbed drainer on its right, a
    swan-neck tap at the back, open legs with a half shelf and a slatted bottom shelf."""
    bm = bmesh.new()
    top, thickness = COUNTER_HEIGHT, 0.04
    z = top - thickness / 2.0
    # The top around the basin's opening (Blender x 0..0.4, y -0.2..0.2).
    add_box(bm, (0.1, 1.0, thickness), (0.45, 0.0, z), bevel=0.008)
    add_box(bm, (0.5, 1.0, thickness), (-0.25, 0.0, z), bevel=0.008)
    add_box(bm, (0.4, 0.3, thickness), (0.2, 0.35, z))
    add_box(bm, (0.4, 0.3, thickness), (0.2, -0.35, z))
    add_box(bm, (1.0, 0.02, 0.06), (0.0, 0.49, top - 0.07))  # front apron
    add_box(bm, (1.0, 0.03, 0.1), (0.0, -0.485, top + 0.05), bevel=0.008)  # upstand
    # Basin, 0.14 deep: food and plates sit on its floor at 0.77.
    add_box(bm, (0.4, 0.4, 0.01), (0.2, 0.0, top - 0.135))
    for y in (-0.2, 0.2):
        add_box(bm, (0.4, 0.01, 0.14), (0.2, y, top - 0.07))
    for x in (0.0, 0.4):
        add_box(bm, (0.01, 0.4, 0.14), (x, 0.0, top - 0.07))
    add_lathe(bm, [(0.02, 0.0), (0.02, 0.21), (0.0, 0.21)], segments=12, centre=(0.2, 0.0, top - 0.35))  # drain
    # Drainer ribs.
    for x in (-0.1, -0.2, -0.3, -0.4):
        add_box(bm, (0.012, 0.6, 0.004), (x, 0.05, top + 0.002))
    # Swan-neck tap behind the basin.
    add_lathe(bm, [(0.03, 0.0), (0.03, 0.02), (0.016, 0.03), (0.016, 0.28), (0.0, 0.28)],
              segments=16, centre=(0.2, -0.4, top))
    spout = Matrix.Rotation(math.radians(-90.0), 4, 'X')  # along +Y, towards the front
    add_lathe(bm, [(0.014, 0.0), (0.014, 0.2), (0.0, 0.2)], segments=12, centre=(0.2, -0.4, top + 0.27), axis_matrix=spout)
    add_box(bm, (0.026, 0.026, 0.06), (0.2, -0.2, top + 0.24), bevel=0.006)
    add_box(bm, (0.08, 0.015, 0.015), (0.27, -0.4, top + 0.1), bevel=0.004)  # lever
    # Legs, a half shelf under the drainer and a slatted bottom shelf.
    for x in (-0.47, 0.47):
        for y in (-0.47, 0.47):
            add_box(bm, (0.04, 0.04, top - thickness), (x, y, (top - thickness) / 2.0), bevel=0.008)
    add_box(bm, (0.5, 0.9, 0.02), (-0.22, 0.0, 0.42), bevel=0.004)
    for y in (-0.47, 0.47):
        add_box(bm, (0.94, 0.03, 0.03), (0.0, y, 0.12))
    for x in (-0.47, 0.47):
        add_box(bm, (0.03, 0.94, 0.03), (x, 0.0, 0.12))
    for y in (-0.36, -0.18, 0.0, 0.18, 0.36):
        add_box(bm, (0.94, 0.04, 0.02), (0.0, y, 0.13))
    return [finish("SinkBody", bm)]


def build_board():
    """A bamboo cutting board with rounded corners and a juice groove round its edge."""
    width, depth, base, rise = 0.34, 0.28, 0.019, 0.006
    border, groove = 0.022, 0.008
    bm = bmesh.new()
    add_box(bm, (width, depth, base), (0.0, 0.0, base / 2.0), bevel=0.012)
    top = base + rise / 2.0
    add_box(bm, (width - 0.012, border, rise), (0.0, depth / 2.0 - border / 2.0 - 0.006, top), bevel=0.002)
    add_box(bm, (width - 0.012, border, rise), (0.0, -depth / 2.0 + border / 2.0 + 0.006, top), bevel=0.002)
    add_box(bm, (border, depth - 0.012, rise), (width / 2.0 - border / 2.0 - 0.006, 0.0, top), bevel=0.002)
    add_box(bm, (border, depth - 0.012, rise), (-width / 2.0 + border / 2.0 + 0.006, 0.0, top), bevel=0.002)
    inset = border + 0.006 + groove
    add_box(bm, (width - 2 * inset, depth - 2 * inset, rise), (0.0, 0.0, top), bevel=0.002)
    return [finish("BoardBody", bm)]


def export_fbx(objects, filename):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(MODELS_FOLDER, filename),
        use_selection=True,
        object_types={'MESH'},
        apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_UNITS',
        bake_space_transform=True,  # "Apply Transform" in the export dialog
        axis_forward='-Z',
        axis_up='Y',
        mesh_smooth_type='FACE',
        add_leaf_bones=False,
        bake_anim=False,
    )


def render_previews(models, folder):
    """Plain clay renders of each model at the game's 55° viewing angle."""
    os.makedirs(folder, exist_ok=True)
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.render.resolution_x = scene.render.resolution_y = 768
    scene.display.shading.show_cavity = True
    scene.display.shading.show_shadows = True

    camera = bpy.data.objects.new("PreviewCamera", bpy.data.cameras.new("PreviewCamera"))
    bpy.context.collection.objects.link(camera)
    scene.camera = camera
    camera.data.lens = 70.0

    for filename, objects in models.items():
        for other in bpy.data.objects:
            if other.type == 'MESH':
                other.hide_render = other not in objects

        corners = [obj.matrix_world @ Vector(c) for obj in objects for c in obj.bound_box]
        centre = sum(corners, Vector()) / len(corners)
        extent = max((c - centre).length for c in corners)
        # Looking from +Y (the game's south), pitched down 55°, as the game camera does.
        pitch = math.radians(55.0)
        camera.location = centre + Vector((0.0, math.cos(pitch), math.sin(pitch))) * extent * 3.6
        camera.rotation_euler = (Vector(centre) - camera.location).to_track_quat('-Z', 'Y').to_euler()

        scene.render.filepath = os.path.join(folder, filename.replace(".fbx", ".png"))
        bpy.ops.render.render(write_still=True)


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.unit_settings.system = 'METRIC'
    bpy.context.scene.unit_settings.scale_length = 1.0

    os.makedirs(MODELS_FOLDER, exist_ok=True)
    models = {
        "SM_Hob.fbx": build_hob(),
        "SM_Pan.fbx": build_pan(),
        "SM_Steak.fbx": build_steak(),
        "SM_Fridge.fbx": build_fridge(),
        "SM_Shelf.fbx": build_shelf(),
        "SM_Chef.fbx": build_chef(),
        "SM_Plate.fbx": build_plate(),
        "SM_PlateRack.fbx": build_plate_racks(),
        "SM_Sink.fbx": build_sink(),
        "SM_Board.fbx": build_board(),
    }
    for filename, objects in models.items():
        export_fbx(objects, filename)
        print(f"Exported {filename}")

    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if "--preview" in args:
        render_previews(models, args[args.index("--preview") + 1])


if __name__ == "__main__":
    main()
