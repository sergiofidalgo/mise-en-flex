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
import random
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
# Cartoony (decision 2026-11-08): exaggerated, chunky shapes with fat rounded edges read better in
# a rush than realistic proportions. Sizes the game relies on (footprints, counter height, where
# things rest, hinges, slots) stay exact; everything else is pushed.
BEVEL_SEGMENTS = 3
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


def add_pipe(bm, points, radius, segments=16):
    """A round pipe through the points, with a ball at each joint so bends stay smooth."""
    points = [Vector(p) for p in points]
    for a, b in zip(points, points[1:]):
        direction = b - a
        rotation = direction.to_track_quat('Z', 'Y').to_matrix().to_4x4()
        add_lathe(bm, [(radius, 0.0), (radius, direction.length)], segments=segments, centre=a, axis_matrix=rotation)
    for joint in points:
        add_lathe(bm, [(0.0, -radius), (radius * 0.7, -radius * 0.7), (radius, 0.0), (radius * 0.7, radius * 0.7), (0.0, radius)],
                  segments=segments, centre=joint)


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


def finish(name, bm, recalc=True):
    """Turns `bm` into an object with outward normals and hard/soft edges decided by angle.
    `recalc` off keeps the faces as made, for a lone flat face whose way out can't be worked out."""
    if recalc:
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
    """A stainless range (decision 2026-11-08): a rounded body under a thick top, one big heat
    knob on the top and an oven door on the front. Parts are separate meshes so each gets its own
    flat colour."""
    bm = bmesh.new()
    plinth = 0.08
    body_top = HOB_HEIGHT - 0.08
    add_box(bm, (0.96, 0.92, body_top - plinth), (0.0, -0.04, plinth + (body_top - plinth) / 2.0), bevel=0.08)
    add_box(bm, (0.8, 0.76, plinth), (0.0, -0.04, plinth / 2.0), bevel=0.03)
    body = finish("HobBody", bm)

    # The top: a steel rim round a recessed spill tray (the user's note, 2026-11-08: room for a
    # grate with presence), wide at the front as a control strip for the knob, and at the back
    # for the heat pips.
    rim_z, rim_t = HOB_HEIGHT - 0.02, 0.04
    bm = bmesh.new()
    add_box(bm, (1.0, 0.2, rim_t), (0.0, 0.4, rim_z), bevel=0.015)     # front strip (+Y)
    add_box(bm, (1.0, 0.13, rim_t), (0.0, -0.435, rim_z), bevel=0.015)  # back
    for x in (-0.45, 0.45):
        add_box(bm, (0.1, 0.71, rim_t), (x, -0.035, rim_z), bevel=0.015)
    top = finish("HobTop", bm)
    bm = bmesh.new()
    add_box(bm, (0.82, 0.69, 0.03), (0.0, -0.035, TRAY_TOP - 0.015))
    tray = finish("HobTray", bm)

    # One big heat knob, set on the top by the game (StyleTest.KnobPosition) at the front corner,
    # where the chef reaching for it would find it. Built round its own origin so it turns in place
    # with the heat; its grip points at the camera (+Y) when off.
    bm = bmesh.new()
    add_lathe(bm, [(0.075, 0.0), (0.075, 0.03), (0.06, 0.045), (0.0, 0.045)], segments=24)
    add_box(bm, (0.032, 0.13, 0.035), (0.0, 0.0, 0.06), bevel=0.012)  # the grip, across the knob
    dial = finish("HobDial", bm)
    # The pointer: a red stripe along the top of the grip, towards its back end, so when off it
    # points to the top of the hob (the user's notes, 2026-11-08); the knob's one red part.
    bm = bmesh.new()
    add_box(bm, (0.018, 0.07, 0.006), (0.0, -0.025, 0.0805), bevel=0.002)
    mark = finish("HobDialMark", bm)

    # The burner head on the tray (HobView tints it with the heat), under the grate.
    bm = bmesh.new()
    add_lathe(bm, [(0.0, TRAY_TOP), (0.095, TRAY_TOP), (0.095, TRAY_TOP + 0.008), (0.085, TRAY_TOP + 0.014),
                   (0.03, TRAY_TOP + 0.016), (0.0, TRAY_TOP + 0.016)], segments=24)
    burner = finish("HobBurner", bm)

    # The grate (the user's note, 2026-11-08: chunky, with personality): a heavy cast-iron frame
    # standing on the tray with knuckled corners, four thick arms reaching in on the diagonals,
    # and pan supports rising from them. The supports' tops are the pan's resting height, 0.91.
    bm = bmesh.new()
    rest = HOB_HEIGHT + 0.01
    hx, hy, bar = 0.36, 0.3, 0.04
    frame_z = TRAY_TOP + 0.02
    for y in (-hy, hy):
        add_box(bm, (2 * hx, bar, 0.034), (0.0, y, frame_z), bevel=0.013)
    for x in (-hx, hx):
        add_box(bm, (bar, 2 * hy, 0.034), (x, 0.0, frame_z), bevel=0.013)
    for x in (-hx, hx):
        for y in (-hy, hy):
            add_ball(bm, (x, y, frame_z), rest - frame_z)             # knuckles, level with the supports
            add_lathe(bm, [(0.0, TRAY_TOP), (0.026, TRAY_TOP), (0.022, TRAY_TOP + 0.01), (0.0, TRAY_TOP + 0.01)],
                      segments=12, centre=(x, y, 0.0))                 # feet
            # A thick arm from the corner in towards the burner, and two supports on it.
            inner = (x * 0.24, y * 0.24)
            add_pipe(bm, [(x, y, frame_z), (inner[0], inner[1], frame_z)], radius=0.02, segments=10)
            for t in (0.42, 0.78):
                px, py = x * (1 - t) + inner[0] * t, y * (1 - t) + inner[1] * t
                add_pipe(bm, [(px, py, frame_z), (px, py, rest - 0.014)], radius=0.014, segments=10)
                add_ball(bm, (px, py, rest - 0.014), 0.014)
    grate = finish("HobGrate", bm)

    # One flame tongue, its origin at its base; the game rings the burner with them and grows them
    # with the heat (HobView).
    bm = bmesh.new()
    add_lathe(bm, [(0.0, 0.0), (0.022, 0.01), (0.028, 0.03), (0.02, 0.06), (0.008, 0.09), (0.0, 0.1)], segments=12)
    flame = finish("HobFlame", bm)
    flame.location = (1.2, 0.0, 0.0)  # apart, for previews only

    # The oven door on the front (+Y, towards the camera), which says "range" at a glance: a
    # panel with a dark window and a long bar handle, all inside the footprint.
    front = -0.04 + 0.46  # the body's front face
    bm = bmesh.new()
    add_box(bm, (0.78, 0.03, 0.5), (0.0, front + 0.015, 0.42), bevel=0.02)
    door = finish("HobDoor", bm)
    bm = bmesh.new()
    add_box(bm, (0.52, 0.01, 0.2), (0.0, front + 0.032, 0.46), bevel=0.004)
    window = finish("HobWindow", bm)
    bm = bmesh.new()
    handle_y = front + 0.07
    add_pipe(bm, [(-0.3, handle_y, 0.62), (0.3, handle_y, 0.62)], radius=0.014, segments=10)
    for x in (-0.27, 0.27):
        add_pipe(bm, [(x, front + 0.03, 0.62), (x, handle_y, 0.62)], radius=0.01, segments=8)
    handle = finish("HobHandle", bm)
    return [body, top, tray, dial, mark, burner, door, window, handle, grate, flame]
def build_pan():
    """Style test: mostly rim and handle. A shallow pan with a fat rolled rim and a long, chunky
    handle with a ball on the end, towards the camera (+Y here)."""
    bm = bmesh.new()
    add_lathe(bm, [
        (0.0, 0.0), (0.18, 0.0), (0.2, 0.014), (0.206, 0.05), (0.22, 0.072), (0.214, 0.09),
        (0.196, 0.094), (0.184, 0.08), (0.18, PAN_FLOOR + 0.01), (0.17, PAN_FLOOR), (0.0, PAN_FLOOR),
    ])
    body = finish("PanBody", bm)

    bm = bmesh.new()
    # Long enough for both of a chef's hands side by side (the user's reference, 2026-11-08), rising a
    # little from the rim, not inside the pan, with a ball on the end.
    add_pipe(bm, [(0.0, 0.215, 0.082), (0.0, 0.42, 0.1)], radius=0.032)
    add_lathe(bm, [(0.0, -0.048), (0.035, -0.04), (0.048, 0.0), (0.035, 0.04), (0.0, 0.048)], segments=20,
              centre=(0.0, 0.455, 0.102))  # the ball
    handle = finish("PanHandle", bm)
    return [body, handle]
def steak_outline(angle):
    """A slightly lopsided rounded shape, so it reads as a cut of meat rather than a disc."""
    half_x, half_y = 0.14, 0.11  # fits the pan at its largest (StorageArt/IngredientView size variation)
    cos_a, sin_a = math.cos(angle), math.sin(angle)
    # A superellipse with an exponent above 2 is squarer than an ellipse: plumper, more meat-like.
    exponent = 2.6
    radius = (abs(cos_a / half_x) ** exponent + abs(sin_a / half_y) ** exponent) ** (-1.0 / exponent)
    radius *= 1.0 + 0.05 * math.sin(2.0 * angle + 0.6) + 0.025 * math.sin(3.0 * angle + 2.0)
    return radius * cos_a, radius * sin_a


def build_steak():
    """One mesh. The shader colours it by height and normal, so no separate bands are needed.
    Puffy and thick, with soft rounded edges (cartoony, decision 2026-11-08)."""
    thickness = 0.06
    segments = 64
    # (outline scale, height), bottom to top: rounded at both edges, with flat faces that end in
    # a smaller inner ring so the top keeps its soft rim when shaded smooth.
    profile = [
        (0.0, 0.0), (0.70, 0.0), (0.88, 0.0), (0.97, 0.008), (1.0, 0.02), (1.0, thickness * 0.5),
        (1.0, thickness - 0.02), (0.97, thickness - 0.008), (0.88, thickness), (0.70, thickness), (0.0, thickness),
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
# Under a sink the cabinet's body stops here, below the basin; the sink's lip comes down to it.
SINK_CABINET_TOP = 0.66
# The range's spill tray, recessed under the rim; the burner and grate stand on it.
# Must match StyleTest.TrayTop.
TRAY_TOP = 0.865
# A chef's arm hangs from its shoulder: the hand's centre is this far below. Must match
# ChefArmsView's arm length in the player builder.
CHEF_ARM_LENGTH = 0.38
# Where the knife sheath hangs, in the game's axes (right, up, forward) from the chef's feet: the
# mouth where the knife's bolster rests. Must match ChefArmsView's belt pose.
CHEF_SHEATH = (0.31, 0.82, -0.08)
# The chef bends at the waist: the torso's origin is there. Must match the player builder.
CHEF_WAIST = 0.85


def build_fridge():
    """A chest fridge at counter height (decision 2026-11-08), chunky and rounded: a white box with
    fat edges and a pale inside, four baskets on one level near the top, and a glass lid in a thick
    white frame with a big handle, so the stock shows from above whichever way it's turned."""
    wall = 0.06
    bm = bmesh.new()
    add_box(bm, (0.94, 0.94, 0.12), (0.0, 0.0, 0.06), bevel=0.03)  # plinth
    for x in (-0.47, 0.47):
        add_box(bm, (wall, 1.0, FRIDGE_HEIGHT - 0.12), (x, 0.0, 0.12 + (FRIDGE_HEIGHT - 0.12) / 2.0), bevel=0.028)
    for y in (-0.47, 0.47):
        add_box(bm, (1.0, wall, FRIDGE_HEIGHT - 0.12), (0.0, y, 0.12 + (FRIDGE_HEIGHT - 0.12) / 2.0), bevel=0.028)
    body = finish("FridgeBody", bm)

    # The pale inside: a floor the baskets stand on, and walls down to it.
    bm = bmesh.new()
    add_box(bm, (0.9, 0.9, 0.02), (0.0, 0.0, FRIDGE_FLOOR - 0.01))
    for x in (-0.445, 0.445):
        add_box(bm, (0.01, 0.9, FRIDGE_HEIGHT - FRIDGE_FLOOR), (x, 0.0, (FRIDGE_HEIGHT + FRIDGE_FLOOR) / 2.0))
    for y in (-0.445, 0.445):
        add_box(bm, (0.9, 0.01, FRIDGE_HEIGHT - FRIDGE_FLOOR), (0.0, y, (FRIDGE_HEIGHT + FRIDGE_FLOOR) / 2.0))
    liner = finish("FridgeLiner", bm)

    # The lid: a thick frame round glass, lying shut over the top, towards the front from its hinge.
    # As thick as before, so it clears the footprint while it swings (StorageArtTests); just chunkier bars.
    size, length, bar, depth = 0.96, 0.9, 0.085, 0.03
    bm = bmesh.new()
    for x in (-(size - bar) / 2.0, (size - bar) / 2.0):
        add_box(bm, (bar, length, depth), (x, length / 2.0, depth / 2.0), bevel=0.012)
    for y in (bar / 2.0, length - bar / 2.0):
        add_box(bm, (size - 2 * bar, bar, depth), (0.0, y, depth / 2.0), bevel=0.012)
    add_box(bm, (0.4, 0.03, 0.02), (0.0, length - 0.015, depth + 0.01), bevel=0.008)  # handle, at the front
    frame = finish("FridgeLidFrame", bm)
    frame.location = FRIDGE_LID_HINGE

    bm = bmesh.new()
    add_box(bm, (size - 2 * bar, length - 2 * bar, 0.01), (0.0, length / 2.0, depth / 2.0))
    glass = finish("FridgeLidGlass", bm)
    glass.location = FRIDGE_LID_HINGE

    bm = bmesh.new()
    # Big enough for a steak, gathered towards the middle (StorageArt.SlotPosition) with a little
    # room to the walls, so stock shows from every side.
    add_box(bm, (0.32, 0.26, 0.04), (0.0, 0.0, 0.02), bevel=0.014)
    basket = finish("FridgeBasket", bm)
    basket.location = (0.18, 0.15, FRIDGE_FLOOR)
    return [body, liner, frame, glass, basket]
# The shelf's boards are shallow and centred, so what's on the lower tier isn't hidden under the
# upper one from the camera's 55° view, whichever way the shelf is turned.
SHELF_BOARD_DEPTH = 0.4


def build_shelf():
    """An open rack (decision 2026-11-08), chunky: two tiers of two slots on thick shallow boards,
    open on every side, the upper tier's board its top, fat posts, plump sacks on the bottom."""
    half = SHELF_BOARD_DEPTH / 2.0
    bm = bmesh.new()
    for x in (-0.465, 0.465):
        for y in (-half + 0.035, half - 0.035):
            add_box(bm, (0.07, 0.07, SHELF_HEIGHT), (x, y, SHELF_HEIGHT / 2.0), bevel=0.022)
    for top in (0.08,) + TIER_TOPS:
        add_box(bm, (1.0, SHELF_BOARD_DEPTH, 0.06), (0.0, 0.0, top - 0.03), bevel=0.02)
    body = finish("ShelfBody", bm)

    bm = bmesh.new()
    add_box(bm, (0.36, 0.32, 0.27), (-0.22, 0.0, 0.215), bevel=0.09)
    add_box(bm, (0.32, 0.3, 0.24), (0.2, 0.0, 0.2), bevel=0.08)
    goods = finish("ShelfGoods", bm)

    bm = bmesh.new()
    add_box(bm, (0.3, 0.24, 0.04), (0.0, 0.0, 0.02), bevel=0.014)
    basket = finish("ShelfBasket", bm)
    basket.location = (0.0, 0.0, TIER_TOPS[1])
    return [body, goods, basket]
def add_ball(bm, centre, radius, segments=16):
    """A small sphere, for heads, hands, eyes and buttons."""
    add_lathe(bm, [(0.0, -radius), (radius * 0.7, -radius * 0.7), (radius, 0.0), (radius * 0.7, radius * 0.7), (0.0, radius)],
              segments=segments, centre=centre)


def add_round_ball(bm, centre, radius, rings=12, segments=24):
    """A smooth sphere, for big round shapes (a tree's crown). add_ball's five-point profile is
    an octagon from the side, which shows on anything large."""
    profile = [(radius * math.sin(math.pi * k / rings), -radius * math.cos(math.pi * k / rings)) for k in range(rings + 1)]
    profile[0] = (0.0, -radius)
    profile[-1] = (0.0, radius)
    add_lathe(bm, profile, segments=segments, centre=centre)


def build_chef():
    """A person-shaped chef (decision 2026-11-08, the art overhaul), chunky and readable from the
    camera: white jacket with buttons, dark trousers and shoes, skin-coloured head and hands with
    eyes and a nose, a toque, and an apron and neckerchief that the game tints in the player's
    colour. It faces -Y here (the game's forward). Two frames, so it can bend at the waist: the
    lower body's origin is the feet, the upper body's (and the hat's) is the waist."""
    objects = []

    # Lower body, from the feet.
    bm = bmesh.new()  # trousers and shoes; the hips go up past the waist, so no gap opens when bending
    for x in (-0.11, 0.11):
        add_box(bm, (0.14, 0.24, 0.08), (x, -0.03, 0.04), bevel=0.03)
        add_pipe(bm, [(x, 0.0, 0.09), (x, 0.0, 0.6)], radius=0.09, segments=12)  # its end ball rests on the floor
    add_lathe(bm, [(0.0, 0.5), (0.25, 0.5), (0.26, 0.9), (0.0, 0.9)], segments=24)
    objects.append(finish("ChefLegs", bm))
    bm = bmesh.new()  # the jacket's skirt over the hips
    add_lathe(bm, [(0.0, 0.48), (0.25, 0.48), (0.3, 0.53), (0.3, 0.92), (0.0, 0.92)], segments=28)
    objects.append(finish("ChefSkirt", bm))
    bm = bmesh.new()  # apron: a panel over the front and a band round the waist (player colour)
    add_box(bm, (0.4, 0.03, 0.44), (0.0, -0.31, 0.68), bevel=0.012)
    add_pipe(bm, [(0.305 * math.cos(2 * math.pi * k / 20), 0.305 * math.sin(2 * math.pi * k / 20), 0.88) for k in range(21)],
             radius=0.016, segments=6)
    objects.append(finish("ChefApron", bm))

    # Upper body, from the waist (CHEF_WAIST); the head's top is 0.85 above it, 1.7 m in all.
    upper = []
    bm = bmesh.new()  # jacket and sleeves
    add_lathe(bm, [(0.0, -0.05), (0.27, -0.05), (0.29, 0.03), (0.29, 0.3), (0.26, 0.4), (0.16, 0.46),
                   (0.08, 0.48), (0.0, 0.48)], segments=28)
    upper.append(finish("ChefTorso", bm))
    bm = bmesh.new()  # head, nose and hands
    add_ball(bm, (0.0, 0.0, 0.66), 0.19, segments=24)
    add_ball(bm, (0.0, -0.19, 0.63), 0.035)
    upper.append(finish("ChefHead", bm))
    bm = bmesh.new()  # eyes and jacket buttons
    for side in (-1.0, 1.0):
        add_ball(bm, (side * 0.07, -0.172, 0.69), 0.026, segments=10)
        for z in (0.08, 0.18, 0.28):
            add_ball(bm, (side * 0.08, -0.288, z), 0.018, segments=8)
    upper.append(finish("ChefFace", bm))
    bm = bmesh.new()  # neckerchief (player colour)
    add_pipe(bm, [(0.11 * math.cos(2 * math.pi * k / 16), 0.11 * math.sin(2 * math.pi * k / 16), 0.47) for k in range(17)],
             radius=0.04, segments=8)
    add_box(bm, (0.08, 0.04, 0.08), (0.0, -0.13, 0.42), bevel=0.02)  # the knot
    upper.append(finish("ChefScarf", bm))
    bm = bmesh.new()  # the toque, its band over the top of the head
    add_lathe(bm, [(0.0, 0.78), (0.16, 0.78), (0.17, 0.86), (0.21, 0.9), (0.25, 0.98), (0.25, 1.06),
                   (0.2, 1.12), (0.1, 1.15), (0.0, 1.155)], segments=32)
    upper.append(finish("ChefHat", bm))
    for obj in upper:
        obj.location = (0.0, 0.0, CHEF_WAIST)

    # An arm (the user's note, 2026-11-08): a chunky sleeve with a cuff, its origin at the shoulder
    # it turns about and hanging straight down from it, and a mitten hand at its end. The game
    # hangs one from each shoulder (CHEF_SHOULDER) and swings and raises them (ChefArmsView).
    bm = bmesh.new()
    add_lathe(bm, [(0.0, 0.05), (0.06, 0.04), (0.088, 0.0), (0.09, -0.08), (0.08, -0.26), (0.085, -0.28),
                   (0.085, -0.31), (0.07, -0.32), (0.0, -0.32)], segments=16)
    arm = finish("ChefArm", bm)
    bm = bmesh.new()
    add_ball(bm, (0.0, 0.0, -CHEF_ARM_LENGTH), 0.09, segments=16)  # the mitten, big enough to read
    add_ball(bm, (0.0, -0.068, -CHEF_ARM_LENGTH + 0.035), 0.038, segments=10)  # thumb, to the front
    hand = finish("ChefHand", bm)
    arm.location, hand.location = (1.0, 0.0, 1.0), (1.0, 0.0, 1.0)  # apart, for previews only

    # A leather knife sheath on the right hip, a little behind so a swinging arm clears it (the
    # user's choice, 2026-11-08): the knife rides in it blade down, only its handle showing.
    # Blender -x is the chef's right, +y behind. Must match the knife's belt pose (ChefArmsView).
    sx, sy = -CHEF_SHEATH[0], -CHEF_SHEATH[2]
    top, bottom, thick = CHEF_SHEATH[1] + 0.02, CHEF_SHEATH[1] - 0.37, 0.05
    bm = bmesh.new()
    points = []
    # Shaped round the blade, which stays wide almost to its tip, so no steel shows through.
    for z, half in ((top, 0.054), (top - 0.05, 0.054), (bottom + 0.1, 0.05), (bottom + 0.035, 0.036), (bottom, 0.012)):
        for dy in (-half, half):
            for dx in (-thick / 2.0, thick / 2.0):
                points.append(bm.verts.new((sx + dx, sy + dy, z)))
    bmesh.ops.convex_hull(bm, input=points)
    sheath = finish("ChefSheath", bm)
    bm = bmesh.new()
    add_box(bm, (thick + 0.012, 0.12, 0.035), (sx, sy, top - 0.012), bevel=0.006)  # the mouth's band
    add_box(bm, (0.05, 0.03, 0.03), (sx + 0.03, sy, top - 0.012), bevel=0.006)  # the loop to the belt
    rim = finish("ChefSheathRim", bm)
    return objects + upper + [arm, hand, sheath, rim]


# ---- Characters (concepts v9.2 → v16, 2026-11-09) -----------------------------------------
# One shared body for every person, built in frames the game moves: the barrel's bottom from the
# feet, each leg from its hip (so it can swing), the upper body (head, hair, hats, face) from the
# waist, and each arm from its shoulder. After playtesting v9.2 (the user's notes): head and hat
# are about 38% of the height (v16), the body is slimmer and the legs longer so walking shows,
# and the arms are long enough for the hands to reach what's carried.
# Head parts are authored at CHAR_HEAD_RADIUS and scaled by CHAR_HEAD_SCALE about the head's
# centre, so hair, hats and features keep fitting the head together.
# Must match CharacterArt in Unity.
CHAR_WAIST = 0.65
CHAR_HIP = (0.1, 0.48)             # each leg hangs from here: (out to the side, height)
CHAR_SHOULDER = (0.25, 0.38)       # (out to the side, up from the waist)
CHAR_ARM_LENGTH = 0.48             # shoulder to the hand's centre: reaches the carry point
CHAR_HEAD_CENTRE = 0.71            # above the waist
CHAR_HEAD_RADIUS = 0.31            # as authored; 0.27 once scaled
CHAR_HEAD_SCALE = 0.87
# Nothing a person wears may reach further out than this from their centre, or it would show
# through walls and counters the 0.35 m body capsule stands against (CharacterProportionTests).
CHAR_REACH = 0.38


def add_ellipsoid(bm, centre, radii, rings=10, segments=20):
    """A smooth sphere stretched along each axis, for hair masses, cheeks and puffs."""
    created = bmesh.ops.create_uvsphere(bm, u_segments=segments, v_segments=rings, radius=1.0)
    transform = Matrix.Translation(centre) @ Matrix.Diagonal((*radii, 1.0))
    bmesh.ops.transform(bm, matrix=transform, verts=created["verts"])


def add_cone(bm, base, tip, radius, segments=12):
    """A rounded cone from `base` to `tip`, for horns."""
    base, tip = Vector(base), Vector(tip)
    direction = tip - base
    rotation = direction.to_track_quat('Z', 'Y').to_matrix().to_4x4()
    length = direction.length
    add_lathe(bm, [(0.0, -radius * 0.3), (radius, 0.0), (radius * 0.75, length * 0.45), (radius * 0.4, length * 0.8),
                   (0.0, length)], segments=segments, centre=base, axis_matrix=rotation)


def add_arc_panel(bm, radius, thickness, half_angle, z0, z1, segments=14):
    """A curved panel round the front (-Y) of a body: `radius` in, `thickness` thick, spanning
    `half_angle` either side of the front, from height z0 to z1. For aprons on round bellies."""
    columns = []
    for i in range(segments + 1):
        a = -half_angle + 2.0 * half_angle * i / segments
        columns.append([bm.verts.new((math.sin(a) * r, -math.cos(a) * r, z))
                        for r, z in ((radius, z0), (radius + thickness, z0), (radius + thickness, z1), (radius, z1))])
    for a, b in zip(columns, columns[1:]):
        for k in range(4):
            bm.faces.new([a[k], a[(k + 1) % 4], b[(k + 1) % 4], b[k]])
    bm.faces.new(columns[0])
    bm.faces.new(list(reversed(columns[-1])))


# The torso's profile (radius by height above the waist), to lay garment details on its front.
TORSO_PROFILE = [(-0.06, 0.265), (0.06, 0.275), (0.24, 0.27), (0.36, 0.245), (0.43, 0.18), (0.46, 0.09)]


def torso_radius(z):
    for (z0, r0), (z1, r1) in zip(TORSO_PROFILE, TORSO_PROFILE[1:]):
        if z <= z1:
            t = max(0.0, (z - z0) / (z1 - z0))
            return r0 + (r1 - r0) * t
    return TORSO_PROFILE[-1][1]


def add_front_strip(bm, rows, lift=0.008, thickness=0.012):
    """A thin piece lying on the torso's front: `rows` are (height, left x, right x) from the
    bottom up, each laid on the surface `lift` out, `thickness` thick. For shirt fronts, lapels."""
    def point(x, z, out):
        r = torso_radius(z) + out
        return (x, -math.sqrt(max(r * r - x * x, 1e-6)), z)
    front, back = [], []
    for z, x0, x1 in rows:
        front.append([bm.verts.new(point(x, z, lift + thickness)) for x in (x0, (x0 + x1) / 2.0, x1)])
        back.append([bm.verts.new(point(x, z, lift)) for x in (x0, (x0 + x1) / 2.0, x1)])
    for layer in (front, back):
        for a, b in zip(layer, layer[1:]):
            for i in range(2):
                bm.faces.new([a[i], a[i + 1], b[i + 1], b[i]])
    for a, b in ((front[0], back[0]), (front[-1], back[-1])):
        for i in range(2):
            bm.faces.new([a[i], a[i + 1], b[i + 1], b[i]])
    for i in (0, 2):
        for k in range(len(rows) - 1):
            bm.faces.new([front[k][i], front[k + 1][i], back[k + 1][i], back[k][i]])


def add_ellipsoid_sector(bm, centre, radii, a0, a1, rings=10, steps=6):
    """A wedge of an ellipsoid between azimuths a0 and a1 (radians, from +x towards +y), closed on
    its two cut faces: for a cap sewn from coloured panels."""
    cx, cy, cz = centre
    top = bm.verts.new((cx, cy, cz + radii[2]))
    bottom = bm.verts.new((cx, cy, cz - radii[2]))
    grid = []
    for i in range(1, rings):
        polar = math.pi * i / rings
        row = []
        for j in range(steps + 1):
            a = a0 + (a1 - a0) * j / steps
            row.append(bm.verts.new((cx + radii[0] * math.sin(polar) * math.cos(a),
                                     cy + radii[1] * math.sin(polar) * math.sin(a),
                                     cz + radii[2] * math.cos(polar))))
        grid.append(row)
    for j in range(steps):
        bm.faces.new([top, grid[0][j + 1], grid[0][j]])
        bm.faces.new([bottom, grid[-1][j], grid[-1][j + 1]])
        for i in range(len(grid) - 1):
            bm.faces.new([grid[i][j], grid[i][j + 1], grid[i + 1][j + 1], grid[i + 1][j]])
    for edge in (0, steps):  # the two cut faces, fanned from the axis
        column = [top] + [row[edge] for row in grid] + [bottom]
        bm.faces.new(column if edge == 0 else list(reversed(column)))


def add_loft(bm, points, widths, thickness, segments=10):
    """A flat, tapering blade along a curve (a feather's vane): an ellipse across each point,
    `widths[i]` wide and `thickness` thick, side to side along x, joined into one smooth piece."""
    rings = []
    for i, point in enumerate(points):
        a = Vector(points[max(i - 1, 0)])
        b = Vector(points[min(i + 1, len(points) - 1)])
        tangent = (b - a).normalized()
        across = Vector((1.0, 0.0, 0.0))
        across = (across - tangent * across.dot(tangent)).normalized()
        flat = tangent.cross(across).normalized()
        if widths[i] <= 1e-4:
            rings.append([bm.verts.new(point)])
            continue
        rings.append([bm.verts.new(Vector(point) + across * (widths[i] * math.cos(2 * math.pi * k / segments))
                                   + flat * (thickness * math.sin(2 * math.pi * k / segments))) for k in range(segments)])
    connect_rings(bm, rings)


def head_surface(x, dz):
    """How far forward (Blender -Y) the authored head's surface is at a point `x` across and `dz`
    above its centre."""
    return -math.sqrt(max(CHAR_HEAD_RADIUS ** 2 - x * x - dz * dz, 0.0))


def add_head_shell(bm, c, front, side, back, thick, rings=14, segments=40, tuck=0.025):
    """Hair or a snug hat that follows the head (radius CHAR_HEAD_RADIUS, centre height c) at an
    offset, instead of an ellipsoid cutting into it: where two near-equal curves cross, the line
    wanders over the facets and looks ragged (the user's note, 2026-11-10). It covers the crown
    down to an edge whose elevation (degrees) is `front` over the brow, `side` over the ears and
    `back` at the nape, and ends in a lip tucked into the head, so the edge is one crisp line.
    `thick(elevation)` is the offset in metres at an elevation in radians."""
    R = CHAR_HEAD_RADIUS
    a_plus_d = math.radians(front + back) / 2.0
    b = math.radians(front - back) / 2.0
    d = (a_plus_d - math.radians(side)) / 2.0
    a = a_plus_d - d

    def edge(phi):  # phi 0 at the front (Blender -Y), pi at the back
        return a + b * math.cos(phi) + d * math.cos(2.0 * phi)

    def point(elevation, phi, radius):
        return (radius * math.cos(elevation) * math.sin(phi), -radius * math.cos(elevation) * math.cos(phi),
                c + radius * math.sin(elevation))

    top = bm.verts.new(point(math.pi / 2.0, 0.0, R + thick(math.pi / 2.0)))
    columns = []
    for j in range(segments):
        phi = 2.0 * math.pi * j / segments
        low = edge(phi)
        column = []
        for i in range(1, rings + 1):
            elevation = math.pi / 2.0 + (low - math.pi / 2.0) * i / rings
            column.append(bm.verts.new(point(elevation, phi, R + thick(elevation))))
        column.append(bm.verts.new(point(low, phi, R - tuck)))  # the lip, tucked in
        columns.append(column)
    for j in range(segments):
        here, there = columns[j], columns[(j + 1) % segments]
        bm.faces.new([top, there[0], here[0]])
        for i in range(len(here) - 1):
            bm.faces.new([here[i], there[i], there[i + 1], here[i + 1]])


def add_tuft(bm, c, elevation, phi, radii, lift=0.0, twist=0.0, rings=8, segments=12):
    """A lock of hair lying on the head: an ellipsoid at `elevation` up from the head's equator and
    `phi` round from the front (degrees; + towards Blender +x, 180 the back), its centre `lift` off
    the head. `radii` are (across, down the head, out from it); `twist` turns it about the way out,
    so a lock can sweep to one side. A smooth shell alone reads as a swimming cap (the user's note,
    2026-11-10): locks break the outline and the hairline, and give the light something to catch.
    Keep `lift` well under the way-out radius: a lock that sits low meets the head at a steep
    angle, which is a crisp line, where one that barely touches it wanders."""
    e, p = math.radians(elevation), math.radians(phi)
    out = Vector((math.cos(e) * math.sin(p), -math.cos(e) * math.cos(p), math.sin(e)))
    across = Vector((math.cos(p), math.sin(p), 0.0))
    down = out.cross(across)
    frame = Matrix((across, down, out)).transposed().to_4x4()
    centre = Vector((0.0, 0.0, c)) + out * (CHAR_HEAD_RADIUS + lift)
    transform = (Matrix.Translation(centre) @ frame @ Matrix.Rotation(math.radians(twist), 4, 'Z')
                 @ Matrix.Diagonal((*radii, 1.0)))
    created = bmesh.ops.create_uvsphere(bm, u_segments=segments, v_segments=rings, radius=1.0)
    bmesh.ops.transform(bm, matrix=transform, verts=created["verts"])


def add_lock_through(bm, centres, outs, sizes, segments=12, steps=5):
    """A lock of hair through `centres`, `outs` the way out from the head at each and `sizes` the
    lock's (half-width across, half-depth out) there; (0, 0) at an end closes it to a point. The
    few points given are joined by a smooth curve and the lock swells and tapers smoothly along
    it: drawn straight from point to point it comes out as a crystal, not a lock."""
    count = len(centres)

    def at(values, i):
        return values[min(max(i, 0), count - 1)]

    def spline(values, i, t):  # Catmull-Rom between values[i] and values[i + 1]
        p0, p1, p2, p3 = at(values, i - 1), at(values, i), at(values, i + 1), at(values, i + 2)
        return 0.5 * ((2.0 * p1) + (p2 - p0) * t + (2.0 * p0 - 5.0 * p1 + 4.0 * p2 - p3) * t * t
                      + (3.0 * p1 - p0 - 3.0 * p2 + p3) * t * t * t)

    samples = []
    for i in range(count - 1):
        for k in range(steps):
            t = k / steps
            ease = t * t * (3.0 - 2.0 * t)
            # A closed end rounds off like a fingertip rather than running to a needle.
            w0, w1 = Vector(sizes[i]), Vector(sizes[i + 1])
            if w0.length <= 1e-5:
                size = w1 * math.sqrt(max(t, 0.0)) if k > 0 else w0
            elif w1.length <= 1e-5:
                size = w0 * math.sqrt(max(1.0 - t, 0.0))
            else:
                size = w0.lerp(w1, ease)
            samples.append((spline(centres, i, t), outs[i].lerp(outs[i + 1], t).normalized(), size))
    samples.append((centres[-1], outs[-1], Vector(sizes[-1])))
    rings = []
    for i, (centre, out, size) in enumerate(samples):
        if size.length <= 1e-5:
            rings.append([bm.verts.new(centre)])
            continue
        tangent = (samples[min(i + 1, len(samples) - 1)][0] - samples[max(i - 1, 0)][0]).normalized()
        across = tangent.cross(out).normalized()
        rings.append([bm.verts.new(centre + across * (size.x * math.cos(2 * math.pi * k / segments))
                                   + out * (size.y * math.sin(2 * math.pi * k / segments))) for k in range(segments)])
    connect_rings(bm, rings)


def add_lock(bm, c, path, sizes, segments=12):
    """A lock of hair lying along the head, from its root to its tip: `path` gives (elevation, phi,
    lift) at each step (degrees, degrees, metres off the head), `sizes` the lock's (half-width
    across, half-depth out from the head) there; (0, 0) closes an end. Locks that sweep across the
    brow and end in points are what tell hair from a cap (the user's note, 2026-11-10)."""
    centres, outs = [], []
    for elevation, phi, lift in path:
        e, p = math.radians(elevation), math.radians(phi)
        out = Vector((math.cos(e) * math.sin(p), -math.cos(e) * math.cos(p), math.sin(e)))
        centres.append(Vector((0.0, 0.0, c)) + out * (CHAR_HEAD_RADIUS + lift))
        outs.append(out)
    add_lock_through(bm, centres, outs, sizes, segments=segments)


def add_hanging_lock(bm, c, phi, top, bottom, width, depth, lean=0.0, segments=12):
    """A long lock hanging beside or behind the head at `phi` (degrees round from the front), from
    `top` to `bottom` (metres above the head's centre): it follows the head down to its widest,
    falls straight from there, and turns in under at its end. `lean` (degrees) sweeps the end round
    the head. For hair long enough to show under a hat."""
    R = CHAR_HEAD_RADIUS
    centres, outs, sizes = [], [], []
    stops = 5
    for i in range(stops):
        v = i / (stops - 1)
        dz = top + (bottom - top) * v
        p = math.radians(phi + lean * v * v)
        around = math.sqrt(max(R * R - dz * dz, 0.0)) if dz > 0.0 else R
        lift = 0.012 + 0.03 * math.sin(math.pi * min(v * 1.2, 1.0)) - 0.03 * max(0.0, v - 0.75) / 0.25
        out = Vector((math.sin(p), -math.cos(p), 0.0))
        centres.append(Vector((0.0, 0.0, c + dz)) + out * (around + lift))
        outs.append(out)
        swell = math.sin(math.pi * (0.12 + 0.76 * v)) if 0 < i < stops - 1 else 0.0
        sizes.append((width * swell, depth * swell))
    add_lock_through(bm, centres, outs, sizes, segments=segments)


def build_characters():
    """The body kit, chunky and soft-edged like the kitchen's props: legs that swing from the
    hips, a barrel body, sleeves with mitten hands, and a big round head with a friendly dot-eyed
    face. Hair, hats and accessories are separate pieces on the same head, each within
    CHAR_REACH. Faces -Y (the game's forward)."""
    c = CHAR_HEAD_CENTRE
    objects = []

    def part(name, bm, frame, head=False):
        if head:
            bmesh.ops.scale(bm, vec=(CHAR_HEAD_SCALE,) * 3, space=Matrix.Translation((0.0, 0.0, -c)),
                            verts=bm.verts)
        obj = finish(name, bm)
        if frame == "waist":
            obj.location = (0.0, 0.0, CHAR_WAIST)
        elif frame == "apart":
            obj.location = (1.0, 0.0, 1.0)  # its origin is a joint; apart, for previews only
        objects.append(obj)

    def swing(name, bm, pivot):
        """A loose part of a hat that sways (HatSway): authored in the head's place like the rest,
        then moved so its origin is the pivot it swings about, and scaled with the head. The game
        hangs it at CharacterArt.HeadPoint(pivot)."""
        px, py, pdz = pivot
        bmesh.ops.translate(bm, vec=(-px, -py, -(c + pdz)), verts=bm.verts)
        bmesh.ops.scale(bm, vec=(CHAR_HEAD_SCALE,) * 3, verts=bm.verts)
        part(name, bm, "apart")

    # ---- From the feet: the barrel's bottom over the hips, a dress, the apron.
    bm = bmesh.new()  # in the top's colour
    add_lathe(bm, [(0.0, 0.4), (0.22, 0.4), (0.26, 0.44), (0.275, 0.52), (0.275, 0.7), (0.0, 0.7)], segments=28)
    part("CharHips", bm, "feet")
    bm = bmesh.new()  # a dress's flared skirt over the hips, down to the knees
    add_lathe(bm, [(0.0, 0.3), (0.32, 0.3), (0.34, 0.33), (0.3, 0.5), (0.28, 0.72), (0.0, 0.72)], segments=28)
    part("CharDress", bm, "feet")
    bm = bmesh.new()  # apron (player colour): curved round the belly, and a band round the waist
    add_arc_panel(bm, 0.282, 0.022, math.radians(52.0), 0.38, 0.72)
    add_pipe(bm, [(0.288 * math.cos(2 * math.pi * k / 24), 0.288 * math.sin(2 * math.pi * k / 24), 0.72) for k in range(25)],
             radius=0.016, segments=6)
    part("CharApron", bm, "feet")

    # ---- A leg, from its hip joint, hanging straight down to the floor; the game swings it.
    bm = bmesh.new()
    add_pipe(bm, [(0.0, 0.0, -0.02), (0.0, 0.0, -CHAR_HIP[1] + 0.12)], radius=0.085, segments=12)
    part("CharLeg", bm, "apart")
    bm = bmesh.new()
    add_box(bm, (0.15, 0.25, 0.1), (0.0, -0.035, -CHAR_HIP[1] + 0.05), bevel=0.04)
    part("CharShoe", bm, "apart")

    # ---- From the waist: the body and what's worn on it.
    bm = bmesh.new()  # the barrel: widest at the belly, rounding into the shoulders
    add_lathe(bm, [(0.0, -0.06), (0.265, -0.06), (0.275, 0.06), (0.27, 0.24), (0.245, 0.36), (0.18, 0.43),
                   (0.09, 0.46), (0.0, 0.46)], segments=28)
    part("CharTorso", bm, "waist")
    bm = bmesh.new()  # a chef's double row of buttons
    for side in (-1.0, 1.0):
        for z in (0.08, 0.18, 0.28):
            add_ball(bm, (side * 0.09, -math.sqrt(0.272 ** 2 - 0.0081) - 0.005, z), 0.02, segments=8)
    part("CharButtons", bm, "waist")
    bm = bmesh.new()  # neckerchief (player colour), hiding where the head meets the body
    add_pipe(bm, [(0.12 * math.cos(2 * math.pi * k / 16), 0.12 * math.sin(2 * math.pi * k / 16), 0.45) for k in range(17)],
             radius=0.045, segments=8)
    add_box(bm, (0.09, 0.045, 0.09), (0.0, -0.15, 0.4), bevel=0.022)  # the knot
    part("CharScarf", bm, "waist")
    bm = bmesh.new()  # a tie, for suits
    # It sits proud of the shirt front an open jacket or vest shows (CharShirtV).
    add_box(bm, (0.07, 0.03, 0.26), (0.0, -0.292, 0.25), bevel=0.012)
    add_box(bm, (0.065, 0.035, 0.055), (0.0, -0.262, 0.41), bevel=0.012)  # the knot
    part("CharTie", bm, "waist")
    bm = bmesh.new()  # a camera on its strap, for the tourist
    add_box(bm, (0.15, 0.08, 0.1), (0.0, -0.29, 0.2), bevel=0.02)
    add_lathe(bm, [(0.0, 0.0), (0.033, 0.0), (0.033, 0.05), (0.0, 0.05)], segments=12, centre=(0.0, -0.33, 0.2),
              axis_matrix=Matrix.Rotation(math.radians(90.0), 4, 'X'))
    part("CharCamera", bm, "waist")

    # ---- The head and everything on it, authored at full size and scaled down together.
    bm = bmesh.new()  # the head, with ears; smooth, as it's the biggest round shape on screen
    add_round_ball(bm, (0.0, 0.0, c), CHAR_HEAD_RADIUS, rings=14, segments=28)
    for x in (-CHAR_HEAD_RADIUS, CHAR_HEAD_RADIUS):
        add_ball(bm, (x * 0.97, 0.02, c - 0.02), 0.06, segments=12)
    part("CharHead", bm, "waist", head=True)
    bm = bmesh.new()  # a round button nose
    add_ball(bm, (0.0, head_surface(0.0, -0.04) - 0.02, c - 0.04), 0.055, segments=12)
    part("CharNoseButton", bm, "waist", head=True)
    bm = bmesh.new()  # a big bulb of a nose
    add_ellipsoid(bm, (0.0, head_surface(0.0, -0.05) - 0.035, c - 0.06), (0.075, 0.07, 0.08))
    part("CharNoseBig", bm, "waist", head=True)
    bm = bmesh.new()  # dot eyes, set wide
    for x in (-0.115, 0.115):
        add_ellipsoid(bm, (x, head_surface(x, 0.04) + 0.008, c + 0.04), (0.04, 0.03, 0.05), rings=8, segments=12)
    part("CharEyes", bm, "waist", head=True)
    bm = bmesh.new()  # a catch light in each eye, so the face reads friendly, not blank
    for x in (-0.115, 0.115):
        add_ball(bm, (x + 0.012, head_surface(x, 0.06) - 0.012, c + 0.06), 0.012, segments=8)
    part("CharEyeShine", bm, "waist", head=True)
    bm = bmesh.new()  # soft brows, in the hair's colour
    for x in (-0.12, 0.12):
        add_box(bm, (0.1, 0.035, 0.03), (x, head_surface(x, 0.13) + 0.005, c + 0.13), bevel=0.012)
    part("CharBrows", bm, "waist", head=True)
    bm = bmesh.new()  # a small smile
    points = []
    for k in range(7):
        x = -0.06 + 0.02 * k
        dz = -0.13 + 0.03 * (x / 0.06) ** 2
        points.append((x, head_surface(x, dz) + 0.004, c + dz))
    add_pipe(bm, points, radius=0.012, segments=6)
    part("CharSmile", bm, "waist", head=True)
    bm = bmesh.new()  # a walrus moustache over the mouth
    for x in (-0.055, 0.055):
        add_ellipsoid(bm, (x, head_surface(x, -0.09) - 0.012, c - 0.09), (0.07, 0.04, 0.035), rings=8, segments=12)
    part("CharMoustache", bm, "waist", head=True)
    bm = bmesh.new()  # round glasses: two rings and a bridge
    for x in (-0.115, 0.115):
        y = head_surface(x, 0.04) - 0.035
        add_pipe(bm, [(x + 0.06 * math.cos(2 * math.pi * k / 14), y, c + 0.04 + 0.055 * math.sin(2 * math.pi * k / 14))
                      for k in range(15)], radius=0.011, segments=6)
    add_pipe(bm, [(-0.055, head_surface(0.0, 0.05) - 0.035, c + 0.05), (0.055, head_surface(0.0, 0.05) - 0.035, c + 0.05)],
             radius=0.01, segments=6)
    part("CharGlasses", bm, "waist", head=True)
    bm = bmesh.new()  # sunglasses: two dark lenses on a bar
    for x in (-0.11, 0.11):
        add_box(bm, (0.13, 0.03, 0.08), (x, head_surface(x, 0.04) - 0.03, c + 0.04), bevel=0.02)
    add_box(bm, (0.36, 0.025, 0.025), (0.0, head_surface(0.0, 0.07) - 0.035, c + 0.07), bevel=0.008)
    part("CharSunglasses", bm, "waist", head=True)

    # Hair (reshaped 2026-11-10, the user's note: a smooth shell that follows the head reads as a
    # silicone swimming cap). Every style is a thick mass with a lip, plus locks (add_tuft) that
    # break its outline and its hairline. Under a hat only the back shows (HairBack); bare heads
    # add a top (HairCrop or HairAfro), and long hair hangs below either (HairLengths).
    def smooth(t):
        t = min(max(t, 0.0), 1.0)
        return t * t * (3.0 - 2.0 * t)

    def back_thick(e):  # thin under a hat's band, standing off the nape below any hat
        return 0.012 + 0.026 * smooth((math.radians(-8.0) - e) / math.radians(26.0))

    bm = bmesh.new()  # the back and sides, down to the nape: always there, and all a hat leaves
    add_head_shell(bm, c, front=60.0, side=8.0, back=-38.0, thick=back_thick)
    # The hair's ends: locks down the nape, uneven, so the edge isn't a cap's.
    for phi, reach, width in ((180.0, -46.0, 0.1), (146.0, -42.0, 0.095), (214.0, -43.0, 0.095), (116.0, -36.0, 0.08), (244.0, -37.0, 0.08)):
        lean = (phi - 180.0) * 0.1
        add_lock(bm, c, [(-4.0, phi, 0.012), (-18.0, phi, 0.032), (-31.0, phi + lean, 0.032), (reach, phi + lean * 2.0, 0.026)],
                 [(0.0, 0.0), (width, 0.034), (width * 0.8, 0.03), (0.0, 0.0)])
    for side in (1.0, -1.0):  # and a short lock down in front of each ear
        add_lock(bm, c, [(13.0, side * 70.0, 0.006), (3.0, side * 71.0, 0.016), (-8.0, side * 72.0, 0.012), (-16.0, side * 73.0, 0.006)],
                 [(0.0, 0.0), (0.034, 0.018), (0.028, 0.016), (0.0, 0.0)], segments=8)
    part("HairBack", bm, "waist", head=True)

    def crop_thick(e):  # a head of hair: full on the crown, and always clear of HairBack beneath it
        return 0.034 + 0.046 * max(0.0, math.sin(e)) ** 1.5 + 0.02 * smooth((math.radians(-8.0) - e) / math.radians(26.0))

    bm = bmesh.new()  # short hair over the top: parted on one side, the fringe swept across the brow
    add_head_shell(bm, c, front=33.0, side=10.0, back=-38.0, thick=crop_thick)
    lock = [(0.0, 0.0), (0.085, 0.05), (0.09, 0.052), (0.06, 0.036), (0.0, 0.0)]
    # The fringe: three locks from the parting across the brow, each ending in a point.
    add_lock(bm, c, [(66.0, 34.0, 0.04), (55.0, 10.0, 0.056), (43.0, -12.0, 0.054), (32.0, -32.0, 0.034), (24.0, -45.0, 0.014)], lock)
    add_lock(bm, c, [(70.0, 8.0, 0.045), (59.0, -22.0, 0.058), (46.0, -46.0, 0.05), (32.0, -62.0, 0.03), (21.0, -71.0, 0.012)], lock)
    add_lock(bm, c, [(62.0, 30.0, 0.04), (50.0, 20.0, 0.054), (39.0, 8.0, 0.046), (30.0, -3.0, 0.022)],
             [(0.0, 0.0), (0.07, 0.046), (0.055, 0.034), (0.0, 0.0)])
    # The short side of the parting, combed the other way, down to the temple.
    add_lock(bm, c, [(64.0, 46.0, 0.04), (52.0, 58.0, 0.052), (38.0, 67.0, 0.04), (24.0, 73.0, 0.014)],
             [(0.0, 0.0), (0.08, 0.046), (0.062, 0.036), (0.0, 0.0)])
    # Back over the top from the parting, either side of it, then down the back of the head.
    add_lock(bm, c, [(68.0, 52.0, 0.045), (78.0, 115.0, 0.066), (62.0, 160.0, 0.06), (42.0, 176.0, 0.04)],
             [(0.0, 0.0), (0.1, 0.05), (0.1, 0.048), (0.0, 0.0)])
    add_lock(bm, c, [(74.0, -4.0, 0.05), (80.0, -95.0, 0.068), (62.0, -148.0, 0.06), (42.0, -166.0, 0.04)],
             [(0.0, 0.0), (0.1, 0.05), (0.1, 0.048), (0.0, 0.0)])
    for side in (1.0, -1.0):
        add_lock(bm, c, [(58.0, 180.0 + side * 14.0, 0.05), (38.0, 180.0 + side * 22.0, 0.05), (16.0, 180.0 + side * 28.0, 0.046),
                         (-4.0, 180.0 + side * 31.0, 0.04)],
                 [(0.0, 0.0), (0.11, 0.042), (0.105, 0.04), (0.0, 0.0)])
    part("HairCrop", bm, "waist", head=True)
    bm = bmesh.new()  # a bun on the crown
    add_ellipsoid(bm, (0.0, 0.12, c + 0.35), (0.13, 0.13, 0.115), rings=10, segments=16)
    part("HairBun", bm, "waist", head=True)

    bm = bmesh.new()  # an afro: a cloud of curls over a full head of hair, clear of the face
    add_head_shell(bm, c, front=33.0, side=4.0, back=-36.0,
                   thick=lambda e: 0.05 + 0.05 * max(0.0, math.sin(e)))
    curls = 34
    for k in range(curls):  # spread evenly over the top of a sphere (a golden-angle spiral)
        elevation = math.degrees(math.asin(1.0 - 1.25 * (k + 0.5) / curls))
        phi = math.degrees(k * math.pi * (3.0 - math.sqrt(5.0)))
        edge = 34.0 if abs((phi + 180.0) % 360.0 - 180.0) < 62.0 else -24.0  # the hairline: high over the face
        if elevation < edge:
            continue
        up = max(0.0, math.sin(math.radians(elevation)))
        # Bigger curls on top, where nothing limits them (CHAR_REACH at the sides); no two the same.
        size = (0.09 + 0.04 * up) * (0.92 + 0.16 * ((k * 0.618) % 1.0))
        add_tuft(bm, c, elevation, phi, (size, size, size), lift=0.012 + 0.045 * up, rings=7, segments=12)
    part("HairAfro", bm, "waist", head=True)

    bm = bmesh.new()  # a retiree's fringe round a bald head: a band round the back, a puff over each ear
    add_ellipsoid(bm, (0.0, 0.12, c - 0.03), (0.32, 0.22, 0.15), rings=10, segments=24)
    for side in (1.0, -1.0):
        add_tuft(bm, c, 6.0, side * 104.0, (0.085, 0.085, 0.055), lift=0.012)
    part("HairFringe", bm, "waist", head=True)

    bm = bmesh.new()  # long hair: locks to the jaw round the back and sides; they show under any hat
    for k in range(9):
        phi = 68.0 + (360.0 - 136.0) * k / 8.0
        side = 1.0 if phi < 180.0 else -1.0
        beside_face = abs(phi - 180.0) > 90.0
        add_hanging_lock(bm, c, phi, top=0.16, bottom=-0.27 - (0.03 if k % 2 else 0.0) - (0.03 if beside_face else 0.0),
                         width=0.135, depth=0.042, lean=0.0 if abs(phi - 180.0) < 1.0 else -side * 5.0)
    part("HairLengths", bm, "waist", head=True)

    # Hats, each with a part the game colours: the player's colour on a player, anything on a guest.
    bm = bmesh.new()  # the toque's puff: short and round, a mushroom over the band (v16), kept
    # small so what a chef carries shows past it (the user's playtest)
    add_lathe(bm, [(0.0, c + 0.24), (0.27, c + 0.24), (0.31, c + 0.28), (0.33, c + 0.32), (0.33, c + 0.37),
                   (0.29, c + 0.41), (0.18, c + 0.435), (0.0, c + 0.44)], segments=32)
    part("HatToque", bm, "waist", head=True)
    bm = bmesh.new()  # its band, round the head
    add_lathe(bm, [(0.0, c + 0.13), (0.29, c + 0.13), (0.3, c + 0.15), (0.3, c + 0.25), (0.29, c + 0.27), (0.0, c + 0.27)],
              segments=32)
    part("HatToqueBand", bm, "waist", head=True)
    bm = bmesh.new()  # a cap's crown, sitting low on the head, with the button on top
    add_head_shell(bm, c, front=18.0, side=10.0, back=-2.0, thick=lambda e: 0.032 + 0.025 * math.sin(e))
    add_ball(bm, (0.0, 0.0, c + 0.37), 0.035, segments=10)
    part("HatCap", bm, "waist", head=True)
    bm = bmesh.new()  # its peak: a big rounded bill out over the eyes, so a cap reads as one from above
    add_ellipsoid(bm, (0.0, -0.29, c + 0.13), (0.2, 0.15, 0.022), rings=6, segments=24)
    part("HatCapPeak", bm, "waist", head=True)
    bm = bmesh.new()  # a bandana: a snug cover over the top and a knot at the back
    add_head_shell(bm, c, front=24.0, side=8.0, back=-15.0, thick=lambda e: 0.026 + 0.01 * math.sin(e))
    add_ball(bm, (0.0, 0.31, c + 0.04), 0.065, segments=12)
    part("HatBandana", bm, "waist", head=True)
    bm = bmesh.new()  # its two tails, hanging from the knot; they flutter as the chef moves
    add_box(bm, (0.055, 0.025, 0.16), (0.03, 0.335, c - 0.06), bevel=0.012)
    add_box(bm, (0.05, 0.025, 0.13), (-0.035, 0.33, c - 0.045), bevel=0.012)
    swing("HatBandanaTails", bm, (0.0, 0.31, 0.04))
    bm = bmesh.new()  # a headband, which carries the colour when there's no hat
    add_pipe(bm, [(0.285 * math.cos(2 * math.pi * k / 28), 0.285 * math.sin(2 * math.pi * k / 28), c + 0.13) for k in range(29)],
             radius=0.035, segments=8)
    part("HatHeadband", bm, "waist", head=True)
    bm = bmesh.new()  # devil horns, curving up and out from the headband
    for side in (-1.0, 1.0):
        add_cone(bm, (side * 0.16, -0.08, c + 0.23), (side * 0.27, -0.12, c + 0.45), 0.065)
    part("HatHorns", bm, "waist", head=True)
    # Crazy hats for the tailor (decision 2026-11-09), earned before they're worn.
    bm = bmesh.new()  # a gold crown: a band round the head with five points
    add_lathe(bm, [(0.0, c + 0.2), (0.3, c + 0.2), (0.31, c + 0.22), (0.31, c + 0.31), (0.29, c + 0.32), (0.0, c + 0.32)],
                   segments=32)
    for k in range(5):
        a = 2 * math.pi * k / 5 + math.pi / 2
        add_cone(bm, (0.27 * math.cos(a), 0.27 * math.sin(a), c + 0.3), (0.29 * math.cos(a), 0.29 * math.sin(a), c + 0.46), 0.06)
    part("HatCrown", bm, "waist", head=True)
    bm = bmesh.new()  # its gems, one at the foot of each point (the player's colour)
    for k in range(5):
        a = 2 * math.pi * k / 5 + math.pi / 2
        add_ball(bm, (0.315 * math.cos(a), 0.315 * math.sin(a), c + 0.26), 0.035, segments=10)
    part("HatCrownGems", bm, "waist", head=True)
    bm = bmesh.new()  # a party hat: a cone tipped forward a little (the player's colour)
    add_lathe(bm, [(0.0, 0.0), (0.16, 0.0), (0.0, 0.36)], segments=24, centre=(0.0, 0.02, c + 0.27),
              axis_matrix=Matrix.Rotation(math.radians(8.0), 4, 'X'))  # tip forward, under the pompom
    swing("HatPartyCone", bm, (0.0, 0.02, 0.27))  # it wobbles on its elastic
    bm = bmesh.new()  # its pompom, on the tip
    add_ball(bm, (0.0, -0.03, c + 0.64), 0.06, segments=12)
    swing("HatPartyPompom", bm, (0.0, 0.02, 0.27))
    bm = bmesh.new()  # the elastic under the chin
    add_pipe(bm, [(0.15, 0.0, c + 0.28), (0.2, -0.12, c - 0.1), (0.0, -0.22, c - 0.27), (-0.2, -0.12, c - 0.1), (-0.15, 0.0, c + 0.28)],
             radius=0.008, segments=6)
    part("HatPartyStrap", bm, "waist", head=True)

    # More crazy hats from the user's references (2026-11-09): a captain's cap, a propeller cap,
    # headphones and a conical straw hat.
    bm = bmesh.new()  # a captain's cap: a white crown that flares out flat over a band
    add_lathe(bm, [(0.0, c + 0.14), (0.3, c + 0.14), (0.31, c + 0.25), (0.37, c + 0.33), (0.38, c + 0.37), (0.33, c + 0.4),
                   (0.0, c + 0.41)], segments=32)
    part("HatCaptain", bm, "waist", head=True)
    bm = bmesh.new()  # its band (the player's colour)
    add_lathe(bm, [(0.0, c + 0.15), (0.318, c + 0.15), (0.318, c + 0.25), (0.0, c + 0.25)], segments=32)
    part("HatCaptainBand", bm, "waist", head=True)
    bm = bmesh.new()  # its glossy black peak
    add_ellipsoid(bm, (0.0, -0.29, c + 0.13), (0.22, 0.15, 0.024), rings=6, segments=24)
    part("HatCaptainPeak", bm, "waist", head=True)
    bm = bmesh.new()  # gold braid round the band and across the peak, and an anchor badge
    add_pipe(bm, [(0.324 * math.cos(2 * math.pi * k / 32), 0.324 * math.sin(2 * math.pi * k / 32), c + 0.25) for k in range(33)],
             radius=0.022, segments=6)
    add_pipe(bm, [(-0.18, -0.33, c + 0.17), (0.0, -0.4, c + 0.17), (0.18, -0.33, c + 0.17)], radius=0.018, segments=6)
    ay = -0.37  # a big anchor badge on the crown's front, leaning back with it so it shows from above
    add_pipe(bm, [(0.0, ay, c + 0.395), (0.0, ay + 0.02, c + 0.27)], radius=0.016, segments=6)    # shank
    add_ball(bm, (0.0, ay - 0.005, c + 0.405), 0.022, segments=8)                                # ring
    add_pipe(bm, [(-0.055, ay + 0.004, c + 0.365), (0.055, ay + 0.004, c + 0.365)], radius=0.013, segments=6)  # stock
    add_pipe(bm, [(-0.07, ay + 0.012, c + 0.31), (-0.045, ay + 0.018, c + 0.275), (0.0, ay + 0.02, c + 0.262),
                  (0.045, ay + 0.018, c + 0.275), (0.07, ay + 0.012, c + 0.31)], radius=0.013, segments=6)  # arms
    part("HatCaptainBraid", bm, "waist", head=True)

    for k, name in enumerate(("HatPropellerPanelA", "HatPropellerPanelB", "HatPropellerPanelC", "HatPropellerPanelD")):
        bm = bmesh.new()  # a propeller cap sewn from four coloured panels
        a0 = math.pi / 4 + k * math.pi / 2
        add_ellipsoid_sector(bm, (0.0, 0.02, c + 0.1), (0.33, 0.33, 0.29), a0, a0 + math.pi / 2)
        part(name, bm, "waist", head=True)
    bm = bmesh.new()  # its peak (the player's colour)
    add_ellipsoid(bm, (0.0, -0.27, c + 0.13), (0.17, 0.11, 0.02), rings=6, segments=24)
    part("HatPropellerPeak", bm, "waist", head=True)
    bm = bmesh.new()  # the button on top, a stem and beads up to the propeller
    add_ball(bm, (0.0, 0.02, c + 0.39), 0.035, segments=10)
    add_pipe(bm, [(0.0, 0.02, c + 0.39), (0.0, 0.02, c + 0.52)], radius=0.012, segments=6)
    for z in (c + 0.43, c + 0.47):
        add_ball(bm, (0.0, 0.02, z), 0.025, segments=10)
    part("HatPropellerStem", bm, "waist", head=True)
    # The blades spin, so they're built round their own axis (origin at the hub) at the head's
    # scale; the game hangs them at the top of the stem (CharacterArt.PropellerHub) and turns them.
    bm = bmesh.new()
    s_head = CHAR_HEAD_SCALE
    for side in (-1.0, 1.0):
        add_ellipsoid(bm, (side * 0.16 * s_head, 0.0, 0.0), (0.16 * s_head, 0.035 * s_head, 0.01 * s_head), rings=6, segments=14)
    add_ball(bm, (0.0, 0.0, 0.0), 0.022 * s_head, segments=8)
    part("HatPropellerBlades", bm, "apart")

    # Big DJ headphones (the user found the first ones looked broken: their band hugged the skull,
    # so most hair swallowed it): a fat padded band arching clear of every hair style, afro
    # included, down to big cups over the ears.
    bm = bmesh.new()
    add_pipe(bm, [(0.37 * math.cos(math.pi * k / 20), 0.03, c + 0.53 * math.sin(math.pi * k / 20)) for k in range(21)],
             radius=0.045, segments=8)
    for side in (-1.0, 1.0):  # the sliders, from the band's ends into the cups
        add_box(bm, (0.04, 0.05, 0.12), (side * 0.37, 0.03, c + 0.02), bevel=0.012)
    part("HatHeadphonesBand", bm, "waist", head=True)
    bm = bmesh.new()  # big round ear cups (the player's colour)
    for side in (-1.0, 1.0):
        add_ellipsoid(bm, (side * 0.355, 0.03, c - 0.03), (0.085, 0.14, 0.15), rings=8, segments=16)
    part("HatHeadphonesCups", bm, "waist", head=True)
    bm = bmesh.new()  # dark cushions where the cups meet the head, so they read as headphones side on
    for side in (-1.0, 1.0):
        add_ellipsoid(bm, (side * 0.3, 0.03, c - 0.03), (0.03, 0.12, 0.13), rings=8, segments=16)
    part("HatHeadphonesPads", bm, "waist", head=True)

    brim = CHAR_REACH / CHAR_HEAD_SCALE  # the widest brims keep the full reach
    # A cowboy hat (the user's idea, 2026-11-09): a dented crown and a brim curling up at the sides.
    bm = bmesh.new()
    add_lathe(bm, [(0.0, c + 0.16), (brim - 0.02, c + 0.16), (brim, c + 0.175), (brim - 0.02, c + 0.19), (0.3, c + 0.2),
                   (0.3, c + 0.36), (0.26, c + 0.43), (0.12, c + 0.45), (0.0, c + 0.41)], segments=40)
    for v in bm.verts:
        r = math.hypot(v.co.x, v.co.y)
        if r > 0.3:  # the brim curls up hard at the sides, more the further out
            v.co.z += 0.26 * ((r - 0.3) / (brim - 0.3)) ** 2 * (v.co.x / r) ** 2
        elif v.co.z > c + 0.3:  # the crown's crease down the middle, front to back, and pinches at the front
            v.co.z -= 0.07 * math.exp(-(v.co.x / 0.07) ** 2) * min(1.0, (v.co.z - c - 0.3) / 0.12)
            if v.co.y < 0.0:
                v.co.x *= 1.0 - 0.25 * min(1.0, -v.co.y / 0.3) * min(1.0, (v.co.z - c - 0.3) / 0.12)
    part("HatCowboy", bm, "waist", head=True)
    bm = bmesh.new()  # its band (the player's colour)
    add_lathe(bm, [(0.0, c + 0.2), (0.305, c + 0.2), (0.305, c + 0.25), (0.0, c + 0.25)], segments=32)
    part("HatCowboyBand", bm, "waist", head=True)

    # A plumed hat (the user asked for feathers): a soft wide brim and a big feather that sways.
    bm = bmesh.new()
    add_lathe(bm, [(0.0, c + 0.16), (brim - 0.015, c + 0.15), (brim, c + 0.165), (brim - 0.02, c + 0.18), (0.29, c + 0.2),
                   (0.3, c + 0.32), (0.25, c + 0.39), (0.0, c + 0.4)], segments=40)
    for v in bm.verts:  # the brim pinned up on the left, a musketeer's swagger
        r = math.hypot(v.co.x, v.co.y)
        if r > 0.29 and v.co.x > 0.0:
            v.co.z += 0.18 * ((r - 0.29) / (brim - 0.29)) * (v.co.x / r) ** 3
    part("HatPlumed", bm, "waist", head=True)
    bm = bmesh.new()  # its band (the player's colour)
    add_lathe(bm, [(0.0, c + 0.2), (0.295, c + 0.2), (0.295, c + 0.26), (0.0, c + 0.26)], segments=32)
    part("HatPlumedBand", bm, "waist", head=True)
    bm = bmesh.new()  # the feather: a quill, and one broad vane curling up and back to a soft tip
    base = Vector((0.2, 0.1, c + 0.27))
    steps = 14
    path = [tuple(base + Vector((-0.05 * t * t, 0.2 * t * t, 0.46 * t - 0.06 * t * t))) for t in (k / steps for k in range(steps + 1))]
    add_pipe(bm, path[:4], radius=0.009, segments=6)  # the bare quill at the bottom
    widths = [0.0 if k == 0 else 0.075 * math.sin(math.pi * (0.1 + 0.85 * k / steps)) ** 0.6 for k in range(steps + 1)]
    widths[-1] = 0.0
    add_loft(bm, path[1:], widths[1:], 0.012)
    swing("HatPlumedFeather", bm, (0.2, 0.1, 0.27))

    # A feathered headdress (the user's call, 2026-11-09, after the concern was raised): a beaded
    # browband in the player's colour, rosettes at the temples, and a fan of long feathers round
    # the back that each sway (the game places the feather below on the band, many times over).
    bm = bmesh.new()
    add_lathe(bm, [(0.0, c + 0.1), (0.3, c + 0.1), (0.305, c + 0.12), (0.305, c + 0.22), (0.3, c + 0.24), (0.0, c + 0.24)],
              segments=32)
    part("HatHeaddressBand", bm, "waist", head=True)
    bm = bmesh.new()  # a row of beads round the band
    for k in range(18):
        a = 2 * math.pi * k / 18
        add_ball(bm, (0.31 * math.cos(a), 0.31 * math.sin(a), c + 0.17), 0.022, segments=8)
    part("HatHeaddressBeads", bm, "waist", head=True)
    bm = bmesh.new()  # a rosette over each temple
    for side in (-1.0, 1.0):
        add_ellipsoid(bm, (side * 0.315, -0.02, c + 0.17), (0.025, 0.075, 0.075), rings=6, segments=16)
    part("HatHeaddressRosettes", bm, "waist", head=True)
    s_head = CHAR_HEAD_SCALE
    quill = [(0.0, 0.0, z * s_head) for z in (0.0, 0.06, 0.12)]
    bm = bmesh.new()  # one feather, standing up from its foot (the origin), at the head's scale
    add_pipe(bm, quill, radius=0.008 * s_head, segments=5)
    steps = 10
    path = [(0.0, 0.0, (0.08 + 0.3 * k / steps) * s_head) for k in range(steps + 1)]
    widths = [0.045 * s_head * math.sin(math.pi * (0.15 + 0.85 * k / steps)) ** 0.5 for k in range(steps + 1)]
    widths[0] = 0.0
    add_loft(bm, path, widths, 0.008 * s_head)
    part("HatHeaddressFeather", bm, "apart")
    bm = bmesh.new()  # its dark tip
    tip = [(0.0, 0.0, (0.36 + 0.1 * k / 5) * s_head) for k in range(6)]
    tip_widths = [0.04 * s_head * (1.0 - k / 5) ** 0.6 for k in range(6)]
    tip_widths[-1] = 0.0
    add_loft(bm, tip, tip_widths, 0.009 * s_head)
    part("HatHeaddressFeatherTip", bm, "apart")

    # A music note the headphones send floating up (a particle's mesh, about its middle).
    bm = bmesh.new()
    add_ellipsoid(bm, (0.0, 0.0, -0.035), (0.03, 0.02, 0.022), rings=6, segments=12)
    add_pipe(bm, [(0.026, 0.0, -0.035), (0.026, 0.0, 0.05)], radius=0.006, segments=5)
    add_pipe(bm, [(0.026, 0.0, 0.05), (0.05, 0.0, 0.03), (0.055, 0.0, 0.005)], radius=0.007, segments=5)
    part("FxMusicNote", bm, "apart")
    bm = bmesh.new()  # a conical straw hat: a shallow cone from a point to a wide rim
    add_lathe(bm, [(0.0, c + 0.47), (brim - 0.01, c + 0.19), (brim, c + 0.17), (brim - 0.03, c + 0.165), (0.26, c + 0.25),
                   (0.0, c + 0.29)], segments=40)
    part("HatStraw", bm, "waist", head=True)
    bm = bmesh.new()  # its ribs, tip to rim, so the cone reads from above (the reference's woven spokes)
    for k in range(16):
        a = 2 * math.pi * k / 16
        rim = brim - 0.02
        add_pipe(bm, [(0.0, 0.0, c + 0.475), (rim * math.cos(a), rim * math.sin(a), c + 0.2 + 0.01)], radius=0.008, segments=5)
    add_pipe(bm, [((brim - 0.005) * math.cos(2 * math.pi * k / 40), (brim - 0.005) * math.sin(2 * math.pi * k / 40), c + 0.18)
                  for k in range(41)], radius=0.012, segments=6)  # the bound rim
    part("HatStrawRibs", bm, "waist", head=True)
    bm = bmesh.new()  # its chin strap (the player's colour)
    add_pipe(bm, [(0.25, 0.0, c + 0.22), (0.27, -0.08, c - 0.08), (0.12, -0.2, c - 0.27), (0.0, -0.23, c - 0.3),
                  (-0.12, -0.2, c - 0.27), (-0.27, -0.08, c - 0.08), (-0.25, 0.0, c + 0.22)], radius=0.012, segments=6)
    part("HatStrawStrap", bm, "waist", head=True)

    bm = bmesh.new()  # a sun hat's brim and its crown
    add_lathe(bm, [(0.0, c + 0.17), (brim - 0.005, c + 0.17), (brim, c + 0.19), (0.0, c + 0.2)], segments=40)
    add_lathe(bm, [(0.0, c + 0.17), (0.28, c + 0.17), (0.28, c + 0.33), (0.24, c + 0.38), (0.0, c + 0.39)], segments=32)
    part("HatSunHat", bm, "waist", head=True)
    bm = bmesh.new()  # its ribbon
    add_lathe(bm, [(0.0, c + 0.2), (0.285, c + 0.2), (0.285, c + 0.25), (0.0, c + 0.25)], segments=32)
    part("HatSunHatBand", bm, "waist", head=True)

    # ---- An arm, from the shoulder, hanging straight down: a sleeve and a mitten hand. The game
    # hangs one from each shoulder (CHAR_SHOULDER) and aims them (ChefArmsView).
    bm = bmesh.new()
    add_lathe(bm, [(0.0, 0.05), (0.065, 0.04), (0.085, 0.0), (0.085, -0.08), (0.075, -0.33), (0.08, -0.35),
                   (0.08, -0.38), (0.065, -0.39), (0.0, -0.39)], segments=16)
    part("CharArm", bm, "apart")
    bm = bmesh.new()
    add_ball(bm, (0.0, 0.0, -CHAR_ARM_LENGTH), 0.08, segments=16)
    add_ball(bm, (0.0, -0.06, -CHAR_ARM_LENGTH + 0.03), 0.034, segments=10)  # thumb, to the front
    part("CharHand", bm, "apart")

    # ---- Garments (the user's note, 2026-11-09: jackets, vests, tank tops): what changes is the
    # torso's colour, a front showing the shirt under it, and the sleeves.
    def v_rows(bottom, top, half_width):
        steps = 6
        return [(bottom + (top - bottom) * k / steps, -max(0.004, half_width * k / steps), max(0.004, half_width * k / steps))
                for k in range(steps + 1)]
    bm = bmesh.new()  # an open jacket: the shirt in the V of its front
    add_front_strip(bm, v_rows(0.14, 0.44, 0.1))
    part("CharShirtV", bm, "waist")
    bm = bmesh.new()  # the jacket's lapels along the V's edges
    for side in (-1.0, 1.0):
        rows = [(z, side * (w - 0.004) if side > 0 else -w - 0.035, side * (w + 0.035) if side > 0 else -(w - 0.004))
                for z, _, w in v_rows(0.14, 0.44, 0.1)]
        add_front_strip(bm, rows, lift=0.016, thickness=0.012)
    part("CharLapels", bm, "waist")
    bm = bmesh.new()  # a vest's deeper V, the shirt showing down to the belly
    add_front_strip(bm, v_rows(0.22, 0.44, 0.09))
    part("CharVestV", bm, "waist")
    bm = bmesh.new()  # the vest's buttons, below the V
    for z in (0.06, 0.13, 0.2):
        add_ball(bm, (0.0, -torso_radius(z) - 0.012, z), 0.018, segments=8)
    part("CharVestButtons", bm, "waist")
    bm = bmesh.new()  # a hoodie's hood, bunched behind the neck
    add_ellipsoid(bm, (0.0, 0.16, 0.42), (0.19, 0.11, 0.09), rings=8, segments=16)
    part("CharHood", bm, "waist")
    bm = bmesh.new()  # its front pocket
    add_arc_panel(bm, 0.276, 0.018, math.radians(32.0), 0.02, 0.17)
    part("CharHoodiePocket", bm, "waist")
    bm = bmesh.new()  # its drawstrings
    for x in (-0.045, 0.045):
        add_pipe(bm, [(x, -0.2, 0.44), (x, -0.25, 0.35), (x * 1.2, -0.265, 0.27)], radius=0.009, segments=6)
    part("CharHoodStrings", bm, "waist")
    bm = bmesh.new()  # bare shoulders for a tank top, rounding out of the torso
    for x in (-0.19, 0.19):
        add_ellipsoid(bm, (x, 0.0, 0.37), (0.085, 0.12, 0.06), rings=8, segments=16)
    part("CharTankShoulders", bm, "waist")
    bm = bmesh.new()  # a bare arm, for a tank top: slimmer than a sleeve, down to the mitten
    add_lathe(bm, [(0.0, 0.03), (0.05, 0.02), (0.06, -0.02), (0.055, -0.2), (0.05, -0.38), (0.0, -0.41)], segments=12)
    part("CharArmBare", bm, "apart")

    # ---- Bodyguards (the user's note): each wears his own shades, and a big coiled earpiece.
    bm = bmesh.new()  # aviators: teardrop lenses
    for x in (-0.115, 0.115):
        add_ellipsoid(bm, (x, head_surface(x, 0.03) - 0.035, c + 0.025), (0.072, 0.014, 0.06), rings=6, segments=16)
    part("CharAviators", bm, "waist", head=True)
    bm = bmesh.new()  # their thin gold frames and a double bridge
    for x in (-0.115, 0.115):
        y = head_surface(x, 0.03) - 0.04
        add_pipe(bm, [(x + 0.075 * math.cos(2 * math.pi * k / 16), y, c + 0.025 + 0.062 * math.sin(2 * math.pi * k / 16))
                      for k in range(17)], radius=0.008, segments=6)
    for dz in (0.06, 0.075):
        add_pipe(bm, [(-0.045, head_surface(0.0, dz) - 0.04, c + dz), (0.045, head_surface(0.0, dz) - 0.04, c + dz)],
                 radius=0.007, segments=6)
    part("CharAviatorFrames", bm, "waist", head=True)
    # The earpiece is laid out in the scaled head's own place (not scaled with it): a bud in the
    # right ear and a fat coiled wire down into the collar, exaggerated so it reads from above.
    s_ear = CHAR_HEAD_SCALE
    ear = (-CHAR_HEAD_RADIUS * 0.97 * s_ear - 0.015, 0.02 * s_ear, c - 0.02 * s_ear)  # Blender -x: the right
    bm = bmesh.new()
    add_ball(bm, ear, 0.04, segments=12)
    coil = []
    top, bottom = ear[2] - 0.05, 0.4
    turns = 7
    for k in range(turns * 8 + 1):
        t = k / (turns * 8)
        angle = 2 * math.pi * turns * t
        cx = ear[0] + (-0.17 - ear[0]) * t
        cy = ear[1] + (0.08 - ear[1]) * t
        coil.append((cx + 0.028 * math.cos(angle), cy + 0.028 * math.sin(angle), top + (bottom - top) * t))
    add_pipe(bm, [(ear[0], ear[1], ear[2] - 0.03)] + coil, radius=0.011, segments=6)
    part("CharEarpiece", bm, "waist")

    # ---- Customers' props (the user's note, 2026-11-09: each archetype has its own). Things held
    # hang from the right shoulder like the hand, so they swing with the arm; things worn are on
    # the body, from the waist.
    hand = -CHAR_ARM_LENGTH
    shoulder_height = CHAR_WAIST + CHAR_SHOULDER[1]
    bm = bmesh.new()  # a walking cane: a crook in the hand, the shaft down to the floor
    add_pipe(bm, [(0.0, -0.04, hand + 0.02), (0.0, -0.06, -shoulder_height + 0.02)], radius=0.022, segments=8)
    add_pipe(bm, [(0.0, -0.04, hand + 0.02), (0.0, -0.04, hand + 0.1), (0.0, 0.03, hand + 0.13), (0.0, 0.09, hand + 0.09)],
             radius=0.022, segments=8)
    add_ball(bm, (0.0, -0.06, -shoulder_height + 0.02), 0.03, segments=8)  # the rubber tip
    part("PropCane", bm, "apart")
    bm = bmesh.new()  # a water bottle, gripped upright
    add_lathe(bm, [(0.0, hand - 0.11), (0.042, hand - 0.11), (0.045, hand - 0.08), (0.045, hand + 0.08), (0.03, hand + 0.11),
                   (0.0, hand + 0.11)], segments=16, centre=(0.0, -0.06, 0.0))
    part("PropBottle", bm, "apart")
    bm = bmesh.new()  # its cap
    add_lathe(bm, [(0.0, hand + 0.1), (0.028, hand + 0.1), (0.028, hand + 0.15), (0.0, hand + 0.15)], segments=12,
              centre=(0.0, -0.06, 0.0))
    part("PropBottleCap", bm, "apart")
    bm = bmesh.new()  # a rolled newspaper, held like a baton
    add_lathe(bm, [(0.0, -0.17), (0.04, -0.17), (0.045, 0.0), (0.04, 0.17), (0.0, 0.17)], segments=12,
              centre=(0.0, -0.04, hand), axis_matrix=Matrix.Rotation(math.radians(90.0), 4, 'X'))
    part("PropNewspaper", bm, "apart")
    bm = bmesh.new()  # a handbag hanging from the hand by its handle
    add_box(bm, (0.17, 0.07, 0.13), (0.0, 0.0, hand - 0.17), bevel=0.025)
    add_pipe(bm, [(-0.05, 0.0, hand - 0.1), (-0.04, 0.0, hand - 0.03), (0.04, 0.0, hand - 0.03), (0.05, 0.0, hand - 0.1)],
             radius=0.012, segments=6)
    part("PropHandbag", bm, "apart")
    bm = bmesh.new()  # a backpack on the back, with straps over the shoulders
    add_box(bm, (0.26, 0.1, 0.32), (0.0, 0.3, 0.24), bevel=0.04)
    add_box(bm, (0.2, 0.04, 0.12), (0.0, 0.35, 0.14), bevel=0.02)  # the front pocket
    for x in (-0.12, 0.12):
        add_pipe(bm, [(x, 0.28, 0.38), (x, 0.1, 0.48), (x, -0.12, 0.45), (x * 1.08, -0.25, 0.3)], radius=0.02, segments=6)
    part("PropBackpack", bm, "waist")
    bm = bmesh.new()  # a leather satchel at the left hip, its strap across the body
    add_box(bm, (0.08, 0.26, 0.2), (-0.31, 0.0, 0.02), bevel=0.025)
    add_pipe(bm, [(-0.29, -0.08, 0.1), (0.0, -0.29, 0.25), (0.17, -0.16, 0.43)], radius=0.016, segments=6)
    add_pipe(bm, [(-0.29, 0.08, 0.1), (0.0, 0.29, 0.25), (0.17, 0.16, 0.43)], radius=0.016, segments=6)
    part("PropSatchel", bm, "waist")
    return objects


# Kitchenware. A plate is 0.32 m across (GreyboxKitchenware.PlateDiameter); its origin is the
# centre of its base. Food sits in its well at 0.012 m.
PLATE_RADIUS = 0.16
COUNTER_HEIGHT = 0.9


def build_plate():
    """A toy-like plate: thick, with a deep well and a big rolled rim (gold, its own mesh)."""
    bm = bmesh.new()
    add_lathe(bm, [
        (0.0, 0.0), (0.085, 0.0), (0.1, 0.008), (0.14, 0.026), (PLATE_RADIUS - 0.012, 0.03),
        (0.13, 0.03), (0.1, 0.02), (0.088, 0.016), (0.0, 0.016),
    ])
    body = finish("PlateBody", bm)

    # A fat ring round the edge: the rim's tube is 0.012 thick, its outside at the plate's radius.
    bm = bmesh.new()
    ring_radius, tube = PLATE_RADIUS - 0.012, 0.012
    profile = [(ring_radius + tube * math.cos(a), 0.03 + tube * math.sin(a))
               for a in [2.0 * math.pi * i / 12 for i in range(13)]]
    add_lathe(bm, profile)
    rim = finish("PlateRim", bm)
    return [body, rim]
# The upright plate rack (decision 2026-11-08): plates stand on edge, this far apart, on rails
# whose tops are this high. Must match KitchenwareArt.RackSpacing and RackRailHeight.
RACK_WIRE = 0.016
RACK_SPACING = 0.08
RACK_PLATES = 6


def build_plate_racks():
    """A simple wire dish rack, sized to the counter like the references: plates stand on edge
    facing the camera, one behind the other, between short tines. Rounded wires, thin enough that
    the plates read first and the rack second."""
    bm = bmesh.new()
    r = RACK_WIRE / 2.0
    length = RACK_PLATES * RACK_SPACING + 0.02
    half_width, height = PLATE_RADIUS + 0.04, 0.22
    for y in (-length / 2.0, length / 2.0):
        # An arch at each end, its feet on the counter.
        add_pipe(bm, [(-half_width, y, r), (-half_width, y, height - 0.06), (-half_width + 0.06, y, height),
                      (half_width - 0.06, y, height), (half_width, y, height - 0.06), (half_width, y, r)], radius=r, segments=10)
    for x in (-half_width, half_width):
        add_pipe(bm, [(x, -length / 2.0, r), (x, length / 2.0, r)], radius=r, segments=10)
    for x in (-0.07, 0.07):  # the plates rest on these, held apart by short tines
        add_pipe(bm, [(x, -length / 2.0, r), (x, length / 2.0, r)], radius=r, segments=10)
        for k in range(RACK_PLATES + 1):
            y = (k - RACK_PLATES / 2.0) * RACK_SPACING
            add_pipe(bm, [(x, y, r), (x, y, 0.07)], radius=r * 0.8, segments=8)
    return [finish("PlateRack", bm)]


def build_sink():
    """A stainless sink top set into the shared wooden cabinet (decision 2026-11-08, the art
    overhaul), like the user's reference: one big basin filling most of the counter, a swan-neck
    tap rising from the back, and a steel lip down to the cabinet's lowered body (CabinetBodyLow),
    so the basin has room underneath. It only washes, with no room for plates."""
    bm = bmesh.new()
    top, thickness = COUNTER_HEIGHT, 0.05
    z = top - thickness / 2.0
    hole_x, hole_y = 0.38, 0.32  # half the basin's opening: most of the counter, like the references
    # The top around the basin's opening.
    add_box(bm, (0.5 - hole_x, 1.0, thickness), ((0.5 + hole_x) / 2.0, 0.0, z), bevel=0.014)
    add_box(bm, (0.5 - hole_x, 1.0, thickness), (-(0.5 + hole_x) / 2.0, 0.0, z), bevel=0.014)
    add_box(bm, (2 * hole_x, 0.5 - hole_y, thickness), (0.0, (0.5 + hole_y) / 2.0, z), bevel=0.014)
    add_box(bm, (2 * hole_x, 0.5 - hole_y, thickness), (0.0, -(0.5 + hole_y) / 2.0, z), bevel=0.014)
    add_box(bm, (1.0, 0.04, 0.1), (0.0, -0.48, top + 0.05), bevel=0.015)  # upstand along the back
    # A steel lip from the top down to the cabinet body below, so the sides stay closed.
    lip_top, lip_bottom = top - thickness, SINK_CABINET_TOP
    lip_z, lip_h = (lip_top + lip_bottom) / 2.0, lip_top - lip_bottom
    for y in (-0.46, 0.46):
        add_box(bm, (0.98, 0.02, lip_h), (0.0, y, lip_z))
    for x in (-0.48, 0.48):
        add_box(bm, (0.02, 0.94, lip_h), (x, 0.0, lip_z))
    body = finish("SinkBody", bm)

    # The parts in their own tones, so the sink's shape reads (the user's note, 2026-11-08): a
    # raised rolled rim round the opening, a darker basin, a dark drain and a bright tap.
    bm = bmesh.new()
    rim = 0.014
    corners = [(-hole_x, -hole_y), (hole_x, -hole_y), (hole_x, hole_y), (-hole_x, hole_y), (-hole_x, -hole_y)]
    add_pipe(bm, [(x, y, top + 0.004) for x, y in corners], radius=rim, segments=10)
    rim_mesh = finish("SinkRim", bm)

    # The basin, deep enough to read from the camera (the user's note, 2026-11-08): its floor,
    # where the plate being washed sits, at 0.68, and its walls darker than the floor (SinkBasinWall).
    depth = 0.23
    bm = bmesh.new()
    add_box(bm, (2 * hole_x, 2 * hole_y, 0.02), (0.0, 0.0, top - depth - 0.01 + 0.01), bevel=0.006)
    basin = finish("SinkBasin", bm)
    bm = bmesh.new()
    for y in (-hole_y, hole_y):
        add_box(bm, (2 * hole_x, 0.02, depth), (0.0, y, top - depth / 2.0))
    for x in (-hole_x, hole_x):
        add_box(bm, (0.02, 2 * hole_y, depth), (x, 0.0, top - depth / 2.0))
    basin_wall = finish("SinkBasinWall", bm)
    bm = bmesh.new()
    add_lathe(bm, [(0.0, 0.0), (0.045, 0.0), (0.045, 0.004), (0.03, 0.006), (0.0, 0.006)], segments=20,
              centre=(0.0, 0.0, top - depth + 0.01))
    drain = finish("SinkDrain", bm)

    # A swan-neck tap rising from the middle of the back edge and arching forward over the basin,
    # so water comes from the back of the sink.
    bm = bmesh.new()
    add_lathe(bm, [(0.05, 0.0), (0.05, 0.025), (0.035, 0.045), (0.0, 0.045)], segments=20, centre=(0.0, -0.4, top))
    arch_centre, arch_radius = Vector((0.0, -0.3, top + 0.28)), 0.1
    neck = [(0.0, -0.4, top + 0.04)]
    for k in range(9):
        angle = math.pi * k / 8
        neck.append((0.0, arch_centre.y - arch_radius * math.cos(angle), arch_centre.z + arch_radius * math.sin(angle)))
    neck.append((0.0, -0.2, top + 0.2))
    add_pipe(bm, neck, radius=0.022)
    add_lathe(bm, [(0.03, 0.0), (0.03, 0.025), (0.0, 0.025)], segments=16, centre=(0.0, -0.2, top + 0.18))  # nozzle
    add_pipe(bm, [(0.0, -0.4, top + 0.1), (0.0, -0.46, top + 0.17)], radius=0.013)  # lever, at the back
    tap = finish("SinkTap", bm)
    return [body, rim_mesh, basin, basin_wall, drain, tap]


def build_cabinet():
    """The counter every plain station shares (decision 2026-11-08, the art overhaul): a wooden
    cabinet with a light wood top, a recessed dark base, and a drawer over two doors on both working
    sides (+Y towards the camera, and -Y), plain panelled ends. Parts are separate meshes so each
    gets its own colour; all stay inside the 1 m footprint, the top at counter height."""
    top_z, slab = COUNTER_HEIGHT, 0.05
    kick = 0.09
    body_top = top_z - slab

    # The carcass and base run the full width, square-edged, so side by side counters read as
    # one run of cabinets (the user's note, 2026-11-08).
    bm = bmesh.new()  # the carcass, set back a little from the fronts
    add_box(bm, (1.0, 0.94, body_top - kick), (0.0, 0.0, kick + (body_top - kick) / 2.0))
    body = finish("CabinetBody", bm)

    bm = bmesh.new()  # the recessed base
    add_box(bm, (1.0, 0.88, kick), (0.0, 0.0, kick / 2.0))
    base = finish("CabinetKick", bm)

    # The top, in sixteen versions: rounded along each side open to the room, square and flush
    # to the footprint along each side that meets another counter, so joined tops make one
    # continuous surface. Bits, in the game's axes: 1 +x, 2 -x, 4 +z, 8 -z joined (CounterJoins).
    # Blender's x is the game's -x, and its y the game's -z.
    tops = []
    r = slab / 2.0
    zc = top_z - r
    for mask in range(16):
        joined_bx_pos, joined_bx_neg = bool(mask & 2), bool(mask & 1)
        joined_by_pos, joined_by_neg = bool(mask & 8), bool(mask & 4)
        x0 = -0.5 if joined_bx_neg else -0.5 + r
        x1 = 0.5 if joined_bx_pos else 0.5 - r
        y0 = -0.5 if joined_by_neg else -0.5 + r
        y1 = 0.5 if joined_by_pos else 0.5 - r
        bm = bmesh.new()
        add_box(bm, (x1 - x0, y1 - y0, slab), ((x0 + x1) / 2.0, (y0 + y1) / 2.0, zc))
        if not joined_bx_pos:
            add_pipe(bm, [(x1, y0, zc), (x1, y1, zc)], radius=r, segments=12)
        if not joined_bx_neg:
            add_pipe(bm, [(x0, y0, zc), (x0, y1, zc)], radius=r, segments=12)
        if not joined_by_pos:
            add_pipe(bm, [(x0, y1, zc), (x1, y1, zc)], radius=r, segments=12)
        if not joined_by_neg:
            add_pipe(bm, [(x0, y0, zc), (x1, y0, zc)], radius=r, segments=12)
        top = finish(f"CabinetTop_{mask}", bm)
        top.location = (1.2 * (mask + 2), 0.0, 0.0)  # apart, for previews only
        tops.append(top)

    # Fronts: a drawer under the top and two doors below it, proud of the carcass, with gaps
    # between them that read as seams from the camera.
    drawer_h, gap = 0.15, 0.02
    drawer_z = body_top - gap - drawer_h / 2.0
    door_top = body_top - 2 * gap - drawer_h
    door_bottom = kick + gap
    door_h = door_top - door_bottom
    bm = bmesh.new()
    knobs = bmesh.new()
    for side in (1.0, -1.0):
        y = side * 0.48
        add_box(bm, (0.92, 0.024, drawer_h), (0.0, y, drawer_z), bevel=0.008)
        add_lathe(knobs, [(0.0, 0.0), (0.012, 0.0), (0.012, 0.012), (0.022, 0.022), (0.016, 0.03), (0.0, 0.032)],
                  segments=12, centre=(0.0, side * 0.492, drawer_z),
                  axis_matrix=Matrix.Rotation(math.radians(-90.0 * side), 4, 'X'))
        for x in (-0.235, 0.235):
            add_box(bm, (0.45, 0.024, door_h), (x, y, door_bottom + door_h / 2.0), bevel=0.008)
            knob_x = x - math.copysign(0.18, x)  # by the doors' meeting edge
            add_lathe(knobs, [(0.0, 0.0), (0.012, 0.0), (0.012, 0.012), (0.022, 0.022), (0.016, 0.03), (0.0, 0.032)],
                      segments=12, centre=(knob_x, side * 0.492, door_top - 0.07),
                      axis_matrix=Matrix.Rotation(math.radians(-90.0 * side), 4, 'X'))
    fronts = finish("CabinetFronts", bm)
    handles = finish("CabinetKnobs", knobs)

    # Under a sink the body stops lower, leaving room for the basin; the sink's lip closes the gap.
    bm = bmesh.new()
    add_box(bm, (1.0, 0.94, SINK_CABINET_TOP - kick), (0.0, 0.0, kick + (SINK_CABINET_TOP - kick) / 2.0))
    low = finish("CabinetBodyLow", bm)
    low.location = (1.2, 0.0, 0.0)  # apart, for previews only
    return [body, base, fronts, handles, low] + tops


def build_bin():
    """A round kitchen bin (the user's note and references, 2026-11-08): a grey tub, a little wider
    at the top, with ribs and two side handles, and a black bag folded over its rim. 0.54 m across,
    so it sits snug against walls and stations within its 1 m slot (SnugFit)."""
    bottom_r, top_r, height = 0.24, 0.26, 0.72
    bm = bmesh.new()
    # An open tub: the outside up to the rim, then the inside wall back down to a floor, so it
    # looks deep and empty from above (the user's note, 2026-11-08).
    add_lathe(bm, [(0.0, 0.0), (bottom_r - 0.02, 0.0), (bottom_r, 0.02), (bottom_r, 0.3),
                   (bottom_r + 0.01, 0.32), (top_r - 0.005, 0.62), (top_r, height - 0.04),
                   (top_r - 0.02, height - 0.04), (bottom_r - 0.03, 0.06), (0.0, 0.06)], segments=32)
    for k in range(6):  # ribs down the sides
        a = 2 * math.pi * k / 6 + math.pi / 6
        add_box(bm, (0.03, 0.03, 0.28), (bottom_r * math.cos(a), bottom_r * math.sin(a), 0.16), bevel=0.01)
    for side in (-1.0, 1.0):  # handles
        add_box(bm, (0.04, 0.14, 0.06), (side * (top_r + 0.025), 0.0, 0.56), bevel=0.015)
    body = finish("BinBody", bm)
    bm = bmesh.new()  # the bag: folded over the rim, and lining the tub right down to its floor
    add_lathe(bm, [(top_r - 0.02, height - 0.2), (top_r + 0.012, height - 0.12), (top_r + 0.022, height - 0.03),
                   (top_r + 0.01, height), (top_r - 0.025, height - 0.005), (top_r - 0.035, height - 0.08),
                   (bottom_r - 0.04, 0.09), (bottom_r - 0.06, 0.075), (0.0, 0.075)], segments=24)
    bag = finish("BinBag", bm)

    # What's been thrown away: a lumpy mound, its origin at its base, that the game raises up
    # the bag as the bin fills (BinFillView).
    bm = bmesh.new()
    add_lathe(bm, [(0.0, 0.0), (0.18, 0.0), (0.17, 0.03), (0.1, 0.06), (0.0, 0.07)], segments=20)
    for k, (r, a, size) in enumerate([(0.0, 0.0, 0.06), (0.11, 0.4, 0.05), (0.12, 1.9, 0.045), (0.1, 3.2, 0.055),
                                      (0.12, 4.5, 0.04), (0.08, 5.6, 0.05)]):
        add_ball(bm, (r * math.cos(a), r * math.sin(a), 0.05 + 0.01 * (k % 2)), size, segments=10)
    trash = finish("BinTrash", bm)
    trash.location = (0.8, 0.0, 0.0)  # apart, for previews only
    return [body, bag, trash]


SEAT_HEIGHT = 0.44  # the top of a chair or stool seat; must match the builder's ChairScale (0.22 * 2)


def build_dining():
    """The dining room's furniture (decision 2026-11-08, the art overhaul): a wooden table on four
    legs, a panelled bar with its own top, a chair with a back (its front, towards the table, is -Y
    here) and a round bar stool, plus a framed picture for the walls."""
    objects = []

    bm = bmesh.new()  # table: 1 m station, 0.75 m high
    add_box(bm, (0.96, 0.96, 0.05), (0.0, 0.0, 0.725), bevel=0.016)
    for x in (-0.38, 0.38):
        for y in (-0.38, 0.38):
            add_box(bm, (0.06, 0.06, 0.7), (x, y, 0.35), bevel=0.012)
    for x in (-0.38, 0.38):
        add_box(bm, (0.03, 0.7, 0.07), (x, 0.0, 0.65))
    for y in (-0.38, 0.38):
        add_box(bm, (0.7, 0.03, 0.07), (0.0, y, 0.65))
    objects.append(finish("TableBody", bm))

    bm = bmesh.new()  # bar: 1 m wide, 0.5 m deep, 1 m high; panels on the dining side (+Y)
    add_box(bm, (0.98, 0.42, 0.93), (0.0, -0.02, 0.465), bevel=0.012)
    for x in (-0.32, 0.0, 0.32):
        add_box(bm, (0.26, 0.02, 0.62), (x, 0.2, 0.46), bevel=0.008)
    add_box(bm, (0.94, 0.02, 0.06), (0.0, 0.2, 0.06))  # kick rail
    objects.append(finish("BarBody", bm))
    bm = bmesh.new()
    add_box(bm, (1.0, 0.5, 0.05), (0.0, 0.0, 0.975), bevel=0.016)
    objects.append(finish("BarTop", bm))

    bm = bmesh.new()  # chair: seat at SEAT_HEIGHT, back on +Y (away from the table)
    add_box(bm, (0.4, 0.4, 0.04), (0.0, 0.0, SEAT_HEIGHT - 0.02), bevel=0.012)
    for x in (-0.17, 0.17):
        for y in (-0.17, 0.17):
            add_box(bm, (0.04, 0.04, SEAT_HEIGHT - 0.04), (x, y, (SEAT_HEIGHT - 0.04) / 2.0), bevel=0.008)
    for x in (-0.17, 0.17):
        add_box(bm, (0.04, 0.04, 0.42), (x, 0.17, SEAT_HEIGHT + 0.21), bevel=0.008)
    add_box(bm, (0.38, 0.03, 0.16), (0.0, 0.17, SEAT_HEIGHT + 0.32), bevel=0.01)
    objects.append(finish("DiningChair", bm))

    bm = bmesh.new()  # stool: a round seat on four legs with a foot ring
    add_lathe(bm, [(0.0, SEAT_HEIGHT - 0.05), (0.17, SEAT_HEIGHT - 0.05), (0.18, SEAT_HEIGHT - 0.03),
                   (0.17, SEAT_HEIGHT), (0.0, SEAT_HEIGHT)], segments=20)
    for k in range(4):
        a = math.pi / 4 + k * math.pi / 2
        add_pipe(bm, [(0.15 * math.cos(a), 0.15 * math.sin(a), 0.0),
                      (0.11 * math.cos(a), 0.11 * math.sin(a), SEAT_HEIGHT - 0.05)], radius=0.018, segments=8)
    ring = [(0.13 * math.cos(2 * math.pi * k / 16), 0.13 * math.sin(2 * math.pi * k / 16), 0.15) for k in range(17)]
    add_pipe(bm, ring, radius=0.01, segments=6)
    objects.append(finish("Stool", bm))

    bm = bmesh.new()  # a framed picture, facing +Y, its origin at the middle of its back
    for x in (-0.2, 0.2):
        add_box(bm, (0.04, 0.03, 0.32), (x, 0.015, 0.0), bevel=0.006)
    for z in (-0.14, 0.14):
        add_box(bm, (0.44, 0.03, 0.04), (0.0, 0.015, z), bevel=0.006)
    objects.append(finish("PictureFrame", bm))
    bm = bmesh.new()
    add_box(bm, (0.36, 0.01, 0.24), (0.0, 0.01, 0.0))
    objects.append(finish("PictureCanvas", bm))

    for i, obj in enumerate(objects):
        obj.location = (1.2 * i, 0.0, 0.0)  # apart, for previews only
    return objects


def add_prism(bm, outline, z0, z1):
    """A flat shape: the (x, y) outline, counter-clockwise, extruded from z0 to z1."""
    bottom = [bm.verts.new((x, y, z0)) for x, y in outline]
    top = [bm.verts.new((x, y, z1)) for x, y in outline]
    bm.faces.new(list(reversed(bottom)))
    bm.faces.new(top)
    n = len(outline)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new([bottom[i], bottom[j], top[j], top[i]])


# A knife lies flat with its blade towards the game's +z (Blender -y) from its origin, where the
# blade meets the handle, and its handle back the other way. Must match the knife block's slots
# (KnifeRackArt): the blade goes into the block.
KNIFE_THICKNESS = 0.014


def build_knife():
    """A chef's knife (the user's reference, 2026-11-08), a little chunky but not a sword: the
    spine runs straight from the heel right to the tip and only the edge curves up to meet it, a
    steel bolster, and a black handle with three silver rivets."""
    t = KNIFE_THICKNESS
    # Outline in (across, along): the spine straight along +across, the edge curving up to the tip.
    # Blender x is the game's -x and y the game's -z, so "along" (towards the tip) is -y here.
    outline = [(0.03, 0.0), (0.03, -0.26), (0.012, -0.235), (-0.012, -0.19), (-0.03, -0.12),
               (-0.036, -0.05), (-0.034, 0.0)]
    bm = bmesh.new()
    add_prism(bm, list(reversed(outline)), 0.0, t)
    blade = finish("KnifeBlade", bm)

    bm = bmesh.new()
    add_box(bm, (0.075, 0.02, 0.026), (-0.002, 0.01, t / 2.0), bevel=0.007)  # bolster (steel)
    bolster = finish("KnifeBolster", bm)

    bm = bmesh.new()
    add_box(bm, (0.044, 0.13, 0.036), (0.0, 0.085, t / 2.0), bevel=0.014)  # handle
    handle = finish("KnifeHandle", bm)

    bm = bmesh.new()
    for y in (0.045, 0.085, 0.125):
        add_ball(bm, (0.0, y, t / 2.0 + 0.018), 0.007, segments=10)
    rivets = finish("KnifeRivets", bm)
    return [blade, bolster, handle, rivets]


def build_knife_block():
    """A chunky wooden knife block (the user's reference, 2026-11-08), set at the back of its
    counter: low at the front (+Y, towards the camera) and tall at the back, with four slots in its
    sloping face that the knives stand in, handles up and out. Each slot is a dark opening framed by
    a brass collar standing proud of the face, so an empty slot reads as clearly as a full one (the
    user's notes, 2026-11-08). Origin at its base, centred."""
    w, d, front_h, back_h = 0.46, 0.28, 0.16, 0.34
    bm = bmesh.new()
    # Side profile (y, z) extruded across x: the front low, the back tall.
    profile = [(d / 2.0, 0.0), (d / 2.0, front_h), (-d / 2.0, back_h), (-d / 2.0, 0.0)]
    bottom = [bm.verts.new((-w / 2.0, y, z)) for y, z in profile]
    top = [bm.verts.new((w / 2.0, y, z)) for y, z in profile]
    bm.faces.new(bottom)
    bm.faces.new(list(reversed(top)))
    for i in range(4):
        j = (i + 1) % 4
        bm.faces.new([bottom[j], bottom[i], top[i], top[j]])
    block = finish("KnifeBlockBody", bm)

    bm = bmesh.new()
    # Dark slots on the sloping face, running up the slope, just proud of it.
    slope = Vector((0.0, -d, back_h - front_h)).normalized()
    normal = Vector((0.0, back_h - front_h, d)).normalized()
    mid = Vector((0.0, 0.0, (front_h + back_h) / 2.0))
    half_w, half_l = 0.014, 0.065  # the opening, across and up the slope
    for x in KNIFE_SLOTS_X:
        centre = mid + Vector((x, 0.0, 0.0)) + normal * 0.004
        corners = [centre + slope * dy + Vector((dx, 0.0, 0.0)) for dx, dy in
                   [(-half_w, -half_l), (half_w, -half_l), (half_w, half_l), (-half_w, half_l)]]
        verts = [bm.verts.new(c) for c in corners]
        face = bm.faces.new(verts)
        face.normal_update()
        if face.normal.dot(normal) < 0.0:
            face.normal_flip()  # out of the face, towards the camera; a lone face can't work it out
    slots = finish("KnifeBlockSlots", bm, recalc=False)

    bm = bmesh.new()  # a brass collar round each slot, proud of the face
    lip, rise = 0.012, 0.012
    turn = Matrix((Vector((1.0, 0.0, 0.0)), slope, normal)).transposed().to_4x4()  # x across, y up the slope, z out
    for x in KNIFE_SLOTS_X:
        centre = mid + Vector((x, 0.0, 0.0))
        before = set(bm.verts)
        for size, offset in (((2 * (half_w + lip), lip, rise), (0.0, half_l + lip / 2.0)),
                             ((2 * (half_w + lip), lip, rise), (0.0, -half_l - lip / 2.0)),
                             ((lip, 2 * half_l, rise), (half_w + lip / 2.0, 0.0)),
                             ((lip, 2 * half_l, rise), (-half_w - lip / 2.0, 0.0))):
            add_box(bm, size, (offset[0], offset[1], rise / 2.0 - 0.003), bevel=0.003)
        new = [v for v in bm.verts if v not in before]
        bmesh.ops.transform(bm, matrix=Matrix.Translation(centre) @ turn, verts=new)
    rims = finish("KnifeBlockSlotRims", bm)
    return [block, slots, rims]


# Where the knife block's slots are across its face. Must match KnifeRackArt.SlotsX.
KNIFE_SLOTS_X = (-0.15, -0.05, 0.05, 0.15)


def build_onion():
    """A cartoony onion (the user's references, 2026-11-08): a fat, coppery bulb drawn up into a
    pointed neck, a few fine darker stripes, a dry straw tuft at the neck and a little root tuft.
    It lies on its side with its neck tilted up, as onions do, so from the overhead camera it shows
    an onion's teardrop rather than a round top (which read as a pumpkin). Still too pumpkin-like
    (the user's note, 2026-11-08), so it's slimmer with a longer neck, and its stripes are few,
    faint and uneven instead of even ribs from pole to pole. Parts are separate meshes for their
    own colours; origin at its base."""
    h = ONION_PROFILE[-1][1]
    profile = ONION_PROFILE
    # On its side: the bulb's axis turned from up to along +x, then tilted up a little.
    lie = Matrix.Rotation(math.radians(72.0), 4, 'Y')

    def place(bm, lift):
        bmesh.ops.transform(bm, matrix=Matrix.Translation((0.0, 0.0, lift)) @ lie, verts=bm.verts)

    bm = bmesh.new()
    add_lathe(bm, profile, segments=28)
    bmesh.ops.transform(bm, matrix=lie, verts=bm.verts)
    lift = -min(v.co.z for v in bm.verts)
    bmesh.ops.translate(bm, vec=(0.0, 0.0, lift), verts=bm.verts)
    body = finish("OnionBody", bm)

    bm = bmesh.new()  # a few faint papery lines, unevenly spaced and of different lengths
    for a_deg, first, last in ((10, 2, 7), (95, 3, 8), (160, 2, 6), (230, 3, 7), (300, 2, 8)):
        a = math.radians(a_deg)
        pts = [((rad + 0.0015) * math.cos(a), (rad + 0.0015) * math.sin(a), z) for rad, z in profile[first:last]]
        add_pipe(bm, pts, radius=0.0012, segments=5)
    place(bm, lift)
    lines = finish("OnionLines", bm)

    bm = bmesh.new()  # the dry tuft at the neck: longer, splaying and curling out
    for k in range(6):
        a = 2 * math.pi * k / 6
        add_pipe(bm, [(0.0, 0.0, h - 0.015), (0.015 * math.cos(a), 0.015 * math.sin(a), h + 0.045),
                      (0.04 * math.cos(a + 0.4), 0.04 * math.sin(a + 0.4), h + 0.085)], radius=0.005, segments=5)
    place(bm, lift)
    tuft = finish("OnionTuft", bm)

    bm = bmesh.new()  # the root plate and its wispy roots
    add_lathe(bm, [(0.0, 0.0), (0.03, 0.0), (0.035, 0.006), (0.0, 0.01)], segments=12, centre=(0.0, 0.0, -0.008))
    for k in range(7):
        a = 2 * math.pi * k / 7 + 0.3
        add_pipe(bm, [(0.012 * math.cos(a), 0.012 * math.sin(a), -0.006),
                      (0.035 * math.cos(a), 0.035 * math.sin(a), -0.03)], radius=0.003, segments=4)
    place(bm, lift)
    root = finish("OnionRoot", bm)
    return [body, lines, tuft, root]


# An onion's side profile (radius, height): a slim teardrop drawn up into a long pointed neck,
# shared by the whole onion and its halves.
ONION_PROFILE = [(0.0, 0.0), (0.05, 0.004), (0.1, 0.03), (0.13, 0.09), (0.128, 0.15), (0.1, 0.21),
                 (0.055, 0.26), (0.025, 0.31), (0.012, 0.34), (0.0, 0.36)]


def onion_dice():
    """Where each piece of a diced onion lies: a little heap, the bottom layer first, so pieces can
    appear one by one while cutting and end as the cut onion's heap (decision 2026-11-08)."""
    rng = random.Random(7)
    size = 0.042
    spots = []
    for k in range(8):  # bottom ring
        a = 2 * math.pi * k / 8 + 0.2
        spots.append((0.068 * math.cos(a), 0.068 * math.sin(a), size / 2.0))
    spots.append((0.0, 0.0, size / 2.0))
    for k in range(4):  # second layer
        a = 2 * math.pi * k / 4 + 0.6
        spots.append((0.034 * math.cos(a), 0.034 * math.sin(a), size * 1.4))
    spots.append((0.0, 0.0, size * 2.25))  # on top
    dice = []
    for x, y, z in spots:
        turn = Matrix.Rotation(rng.uniform(0, math.pi), 4, 'Z') @ Matrix.Rotation(rng.uniform(-0.25, 0.25), 4, 'X')
        dice.append((Vector((x + rng.uniform(-0.006, 0.006), y + rng.uniform(-0.006, 0.006), z)), turn, size * rng.uniform(0.85, 1.1)))
    return dice


def add_die(bm, centre, turn, size):
    """One chunky, bevelled piece of onion, turned and placed."""
    before = set(bm.verts)
    add_box(bm, (size, size, size * 0.8), (0.0, 0.0, 0.0), bevel=size * 0.18)
    new = [v for v in bm.verts if v not in before]
    bmesh.ops.transform(bm, matrix=Matrix.Translation(centre) @ turn, verts=new)


# Onion rings lying in a loose fan: (centre x, centre y, radius). Bigger rings underneath.
ONION_RINGS = [(-0.045, 0.03, 0.075), (0.045, 0.035, 0.068), (0.0, -0.035, 0.062), (0.06, -0.04, 0.05), (-0.055, -0.045, 0.045)]


def add_ring(bm, k):
    """One onion ring, lying flat and a little tilted, stacked on the ones before it."""
    x, y, radius = ONION_RINGS[k]
    thick = 0.014
    before = set(bm.verts)
    add_pipe(bm, [(radius * math.cos(2 * math.pi * i / 18), radius * math.sin(2 * math.pi * i / 18), 0.0) for i in range(19)],
             radius=thick, segments=6)
    new = [v for v in bm.verts if v not in before]
    tilt = Matrix.Rotation(math.radians(6.0 * (1 if k % 2 else -1)), 4, 'X') @ Matrix.Rotation(math.radians(4.0 * k), 4, 'Y')
    bmesh.ops.transform(bm, matrix=Matrix.Translation((x, y, thick + 0.008 * k)) @ tilt, verts=new)


def build_onion_rings():
    """Onion rings (decision 2026-11-08): the first cut of an onion, a loose fan of fat cream
    rings, kept by stopping before cutting on into dice. One mesh, so it browns as one. Origin at
    its base."""
    bm = bmesh.new()
    for k in range(len(ONION_RINGS)):
        add_ring(bm, k)
    return [finish("OnionRingsBody", bm)]


def build_diced_onion():
    """A cut onion: a little heap of chunky cream dice (the user's note, 2026-11-08). One mesh, so
    it browns as one as it cooks. Origin at its base."""
    bm = bmesh.new()
    for centre, turn, size in onion_dice():
        add_die(bm, centre, turn, size)
    return [finish("DicedOnionBody", bm)]


def build_onion_cut():
    """An onion part way through cutting (the user's notes, 2026-11-08): a half lying cut face up
    with its rings showing, the rings one by one, and the heap's dice one by one, so the cut grows
    chop by chop: halves, then rings, then dice."""
    segments = 16
    # Half the bulb (angles 0..pi), then the flat cut face across it, then turned cut face up.
    bm = bmesh.new()
    rings = []
    for radius, height in ONION_PROFILE:
        if radius <= 0.0:
            rings.append([bm.verts.new((0.0, 0.0, height))])
        else:
            rings.append([bm.verts.new((radius * math.cos(math.pi * i / segments), radius * math.sin(math.pi * i / segments), height))
                          for i in range(segments + 1)])
    for lower, upper in zip(rings, rings[1:]):
        for i in range(segments):
            if len(lower) == 1:
                bm.faces.new([lower[0], upper[i + 1], upper[i]])
            elif len(upper) == 1:
                bm.faces.new([lower[i], lower[i + 1], upper[0]])
            else:
                bm.faces.new([lower[i], lower[i + 1], upper[i + 1], upper[i]])
    skin_verts = list(bm.verts)
    face_up = Matrix.Rotation(math.radians(-90.0), 4, 'X')
    bmesh.ops.transform(bm, matrix=face_up, verts=skin_verts)
    drop = -min(v.co.z for v in bm.verts)
    bmesh.ops.translate(bm, vec=(0.0, 0.0, drop), verts=bm.verts)
    skin = finish("OnionHalfSkin", bm)

    # The cut face: the profile's outline on both sides of the axis, in the plane of the cut.
    def outline(scale):
        centre_h = 0.15
        side = [(r * scale, centre_h + (h - centre_h) * scale) for r, h in ONION_PROFILE]
        return [(r, 0.0, h) for r, h in side] + [(-r, 0.0, h) for r, h in reversed(side[1:-1])]

    bm = bmesh.new()
    cut = bm.faces.new([bm.verts.new(p) for p in outline(0.99)])
    bmesh.ops.transform(bm, matrix=Matrix.Translation((0.0, 0.0, drop)) @ face_up, verts=bm.verts)
    cut.normal_update()
    if cut.normal.z < 0.0:
        cut.normal_flip()  # facing up, to the camera
    face = finish("OnionHalfFace", bm, recalc=False)

    bm = bmesh.new()  # rings, drawn just above the face
    for scale in (0.8, 0.6, 0.4, 0.2):
        pts = outline(scale)
        add_pipe(bm, [(x, -0.003, z) for x, _, z in pts + pts[:1]], radius=0.0035, segments=5)
    bmesh.ops.transform(bm, matrix=Matrix.Translation((0.0, 0.0, drop)) @ face_up, verts=bm.verts)
    rings_obj = finish("OnionHalfRings", bm)

    pieces = []
    for k in range(len(ONION_RINGS)):  # the rings one by one, to go as they're diced
        bm = bmesh.new()
        add_ring(bm, k)
        pieces.append(finish(f"OnionRing_{k:02d}", bm))
    for k, (centre, turn, size) in enumerate(onion_dice()):
        bm = bmesh.new()
        add_die(bm, centre, turn, size)
        pieces.append(finish(f"OnionDie_{k:02d}", bm))
    return [skin, face, rings_obj] + pieces


def rounded_rect(bm, width, depth, radius, z, per_corner=4):
    """A ring of vertices round a rounded rectangle, centred, at height z."""
    radius = min(radius, width / 2.0 - 1e-4, depth / 2.0 - 1e-4)
    hx, hy = width / 2.0 - radius, depth / 2.0 - radius
    ring = []
    for cx, cy, start in ((hx, hy, 0.0), (-hx, hy, 90.0), (-hx, -hy, 180.0), (hx, -hy, 270.0)):
        for k in range(per_corner + 1):
            a = math.radians(start + 90.0 * k / per_corner)
            ring.append(bm.verts.new((cx + radius * math.cos(a), cy + radius * math.sin(a), z)))
    return ring


def add_steel_pan(bm, width, depth, height, taper, flange, wall=0.006, radius=0.03):
    """A pressed steel pan (the user's references, 2026-11-08): a floor, walls leaning out by
    `taper`, and a flat rim sticking out by `flange` all round, with thickness, so it reads from
    above with its inside showing. Its footprint at the rim is width x depth; origin at its base."""
    bottom_w, bottom_d = width - 2 * taper, depth - 2 * taper
    rings = [
        [bm.verts.new((0.0, 0.0, 0.0))],
        rounded_rect(bm, bottom_w, bottom_d, radius - taper / 2, 0.0),
        rounded_rect(bm, width, depth, radius, height),
        rounded_rect(bm, width + 2 * flange, depth + 2 * flange, radius + flange, height),
        rounded_rect(bm, width + 2 * flange, depth + 2 * flange, radius + flange, height + wall),
        rounded_rect(bm, width - 2 * wall, depth - 2 * wall, radius - wall, height + wall),
        rounded_rect(bm, bottom_w - 2 * wall, bottom_d - 2 * wall, radius - taper / 2 - wall, wall),
        [bm.verts.new((0.0, 0.0, wall))],
    ]
    connect_rings(bm, rings)


# A sheet tray and a gastronorm container, their footprints the items' (GreyboxKitchenware).
TRAY_SIZE = (0.72, 0.56, 0.03)
# A rectangular pan, low enough to read as a container rather than a bin (the user's notes,
# 2026-11-08: 0.32 and 0.2 m deep were too high, and nearly square looked wrong).
CONTAINER_SIZE = (0.36, 0.22, 0.14)
# The tray rack's shelves: four trays, each on runners this far above the last.
TRAY_RACK_LEVELS = (0.04, 0.14, 0.24, 0.34)
TRAY_RACK_HEIGHT = 0.44


def build_tray():
    """A sheet tray: shallow, with a rolled-looking flat rim, like the user's reference."""
    bm = bmesh.new()
    add_steel_pan(bm, TRAY_SIZE[0], TRAY_SIZE[1], TRAY_SIZE[2], taper=0.012, flange=0.012, radius=0.04)
    return [finish("TrayBody", bm)]


def build_container():
    """A gastronorm container: a deep, tapered steel pan with a flat rim, made to nest in a stack.
    The taper is steep enough that a pan nests about 2 cm into the one below."""
    bm = bmesh.new()
    add_steel_pan(bm, CONTAINER_SIZE[0], CONTAINER_SIZE[1], CONTAINER_SIZE[2], taper=0.045, flange=0.016, radius=0.03)
    return [finish("ContainerBody", bm)]


def build_tray_rack():
    """A steel rack the trays slide into from the front (the user's reference): square posts at
    the corners, rails round the top, and a runner each side at every shelf."""
    tube = 0.022
    w = TRAY_SIZE[0] + 2 * 0.012 + 2 * tube + 0.01  # the tray and its rim, between the posts
    d = TRAY_SIZE[1] + 2 * 0.012 + 0.02
    bm = bmesh.new()
    for sx in (-1.0, 1.0):
        for sy in (-1.0, 1.0):
            add_box(bm, (tube, tube, TRAY_RACK_HEIGHT), (sx * (w / 2 - tube / 2), sy * (d / 2 - tube / 2), TRAY_RACK_HEIGHT / 2), bevel=0.003)
    for z in (TRAY_RACK_HEIGHT - tube / 2, tube / 2):
        for sy in (-1.0, 1.0):
            add_box(bm, (w, tube, tube), (0.0, sy * (d / 2 - tube / 2), z), bevel=0.003)
        for sx in (-1.0, 1.0):
            add_box(bm, (tube, d, tube), (sx * (w / 2 - tube / 2), 0.0, z), bevel=0.003)
    for z in TRAY_RACK_LEVELS:  # runners the trays' rims rest on
        for sx in (-1.0, 1.0):
            add_box(bm, (0.018, d - 2 * tube, 0.008), (sx * (w / 2 - tube - 0.009), 0.0, z + TRAY_SIZE[2] - 0.004), bevel=0.002)
    return [finish("TrayRackFrame", bm)]


def add_extrude_yz(bm, profile, x0, x1):
    """A shape drawn in (y, z), counter-clockwise seen from +x, extruded along x from x0 to x1."""
    left = [bm.verts.new((x0, y, z)) for y, z in profile]
    right = [bm.verts.new((x1, y, z)) for y, z in profile]
    bm.faces.new(list(reversed(left)))
    bm.faces.new(right)
    n = len(profile)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new([left[i], left[j], right[j], right[i]])


def add_cylinder_y(bm, centre, radius, length, segments=16):
    """A closed cylinder lying along y (an axle's direction), starting at centre and running +y."""
    turn = Matrix.Rotation(math.radians(-90.0), 4, 'X')  # its z becomes +y
    add_lathe(bm, [(0.0, 0.0), (radius, 0.0), (radius, length), (0.0, length)], segments=segments,
              centre=centre, axis_matrix=turn)


def add_profile_y(bm, profile, half_width, y_centre=0.0):
    """A side profile, (x, z) points going round counter-clockwise seen from -y, extruded across y
    from y_centre - half_width to y_centre + half_width. Convex profiles only (one face per side)."""
    near = [bm.verts.new((x, y_centre - half_width, z)) for x, z in profile]
    far = [bm.verts.new((x, y_centre + half_width, z)) for x, z in profile]
    bm.faces.new(near)
    bm.faces.new(list(reversed(far)))
    n = len(profile)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new([near[i], far[i], far[j], near[j]])


# The street's cars (the user's notes, 2026-11-09: "the cars could be a little more refined", and
# 2026-11-11, of the slab-and-wedge cars that followed: "awful"). Third pass: toy cars. Short, tall
# and wide, with a body lofted through rounded sections from nose to tail (so the bonnet falls away,
# the corners turn in and nothing is a flat slab), fat fenders bulging over big wheels, a cabin of
# glass all the way round that leans in to a separate rounded roof, round headlights, wrapped
# bumpers and number plates. Along x with the front at +x, origin on the ground under the middle.
# Each is split by colour: Body (the paint), Glass, Trim (bumpers, grille, door lines, handles),
# Tyres, Hubs (and anything else pale: plates), Headlights, Taillights; the taxi has a Sign.
CAR_KINDS = {
    # shell: (x, half width, bottom, top, corner radius) from nose to tail.
    # cabin: (z, rear x, front x, half width, corner radius) at the belt, the roof's edge, and its crown.
    # wheels: the axles' x.
    "Sedan": {
        "shell": [(2.12, 0.72, 0.42, 0.76, 0.16), (2.0, 0.84, 0.32, 0.85, 0.2), (1.62, 0.94, 0.26, 0.93, 0.22),
                  (0.95, 0.96, 0.25, 1.0, 0.22), (-0.95, 0.96, 0.25, 1.0, 0.22), (-1.6, 0.94, 0.26, 0.98, 0.22),
                  (-2.0, 0.86, 0.32, 0.92, 0.2), (-2.12, 0.74, 0.46, 0.84, 0.16)],
        "cabin": [(0.96, -1.3, 1.0, 0.87, 0.3), (1.47, -0.92, 0.42, 0.74, 0.26), (1.56, -0.74, 0.24, 0.58, 0.22)],
        "wheels": (-1.3, 1.3),
    },
    "Hatch": {
        "shell": [(1.82, 0.72, 0.42, 0.76, 0.16), (1.7, 0.84, 0.32, 0.86, 0.2), (1.35, 0.93, 0.26, 0.94, 0.22),
                  (0.8, 0.95, 0.25, 1.0, 0.22), (-1.35, 0.95, 0.25, 1.0, 0.22), (-1.7, 0.9, 0.3, 0.99, 0.2),
                  (-1.82, 0.78, 0.44, 0.92, 0.16)],
        "cabin": [(0.96, -1.66, 0.85, 0.86, 0.3), (1.5, -1.5, 0.3, 0.74, 0.26), (1.59, -1.3, 0.12, 0.58, 0.22)],
        "wheels": (-1.14, 1.14),
    },
    "Van": {
        "shell": [(2.3, 0.8, 0.42, 0.9, 0.16), (2.18, 0.9, 0.32, 1.0, 0.2), (1.85, 1.0, 0.26, 1.08, 0.22),
                  (1.3, 1.02, 0.25, 1.12, 0.22), (-1.9, 1.02, 0.25, 1.12, 0.22), (-2.22, 0.96, 0.3, 1.1, 0.2),
                  (-2.3, 0.92, 0.44, 1.04, 0.16)],
        "cabin": [(1.08, -2.24, 1.7, 0.95, 0.28), (1.98, -2.2, 1.12, 0.88, 0.26), (2.08, -2.05, 0.92, 0.72, 0.22)],
        "wheels": (-1.45, 1.5),
    },
}
CAR_KINDS["Taxi"] = CAR_KINDS["Sedan"]
CAR_WHEEL_RADIUS = 0.41
CAR_TYRE_WIDTH = 0.3


def car_section(bm, x, half_width, bottom, top, radius, per_corner=5):
    """A ring of verts round a rounded rectangle standing across the car (in y and z) at `x`."""
    half_height = (top - bottom) / 2.0
    radius = min(radius, half_width, half_height)
    middle = bottom + half_height
    ring = []
    for k, (sy, sz) in enumerate(((1.0, 1.0), (-1.0, 1.0), (-1.0, -1.0), (1.0, -1.0))):
        for i in range(per_corner + 1):
            angle = math.radians(90.0 * k + 90.0 * i / per_corner)
            ring.append(bm.verts.new((x, sy * (half_width - radius) + radius * math.cos(angle),
                                      middle + sz * (half_height - radius) + radius * math.sin(angle))))
    return ring


def car_plan(bm, z, rear, front, half_width, radius, per_corner=5):
    """A ring of verts round a rounded rectangle lying flat (in x and y) at height `z`."""
    half_length = (front - rear) / 2.0
    radius = min(radius, half_width, half_length)
    middle = rear + half_length
    ring = []
    for k, (sx, sy) in enumerate(((1.0, 1.0), (-1.0, 1.0), (-1.0, -1.0), (1.0, -1.0))):
        for i in range(per_corner + 1):
            angle = math.radians(90.0 * k + 90.0 * i / per_corner)
            ring.append(bm.verts.new((middle + sx * (half_length - radius) + radius * math.cos(angle),
                                      sy * (half_width - radius) + radius * math.sin(angle), z)))
    return ring


def car_skin(bm, rings):
    """Joins rings of equal length into a closed solid, capped at both ends."""
    n = len(rings[0])
    for a, b in zip(rings, rings[1:]):
        for i in range(n):
            j = (i + 1) % n
            bm.faces.new([a[i], a[j], b[j], b[i]])
    bm.faces.new(rings[0])
    bm.faces.new(list(reversed(rings[-1])))


def car_cabin_at(cabin, z):
    """The cabin's plan (rear, front, half width, radius) at height `z`, between its keys."""
    for (z0, *low), (z1, *high) in zip(cabin, cabin[1:]):
        if z <= z1 or (z1, *high) == tuple(cabin[-1]):
            t = max(0.0, min(1.0, (z - z0) / (z1 - z0)))
            return [a + (b - a) * t for a, b in zip(low, high)]
    return list(cabin[-1][1:])


def add_car_cabin(bm, cabin, z_low, z_high, x_low=None, x_high=None, grow=0.0, steps=1):
    """A slice of the cabin between two heights (and, optionally, two x): the glass, a pillar, the
    roof. `grow` stands it that far proud of the cabin's own surface, so a pillar shows over glass."""
    rings = []
    for step in range(steps + 1):
        z = z_low + (z_high - z_low) * step / steps
        rear, front, half_width, radius = car_cabin_at(cabin, z)
        # A slice keeps the cabin's surface where it isn't cut, standing `grow` proud of it.
        rear = max(rear, x_low) if x_low is not None else rear - grow
        front = min(front, x_high) if x_high is not None else front + grow
        rings.append(car_plan(bm, z, rear, max(front, rear + 0.04), half_width + grow, radius + grow))
    car_skin(bm, rings)


def build_cars():
    objects = []
    for kind, spec in CAR_KINDS.items():
        shell, cabin, wheels = spec["shell"], spec["cabin"], spec["wheels"]
        nose, tail = shell[0][0], shell[-1][0]
        half_width = max(s[1] for s in shell)
        belt, roof_edge, crown = cabin[0][0], cabin[1][0], cabin[2][0]
        cabin_rear, cabin_front = cabin[0][1], cabin[0][2]
        van = kind == "Van"

        body = bmesh.new()
        car_skin(body, [car_section(body, *station) for station in shell])
        # Fenders: a fat bulge over each wheel, which the tyre stands just proud of.
        for x in wheels:
            for side in (-1.0, 1.0):
                add_ellipsoid(body, (x, side * (half_width - 0.07), 0.6), (0.64, 0.17, 0.43), rings=8, segments=16)
        # The roof: from the glass's top edge, a touch wider so it overhangs, up to a flat crown.
        roof = [car_plan(body, roof_edge - 0.02, *[v + g for v, g in zip(car_cabin_at(cabin, roof_edge), (-0.03, 0.05, 0.03, 0.03))]),
                car_plan(body, roof_edge + 0.05, *[v + g for v, g in zip(car_cabin_at(cabin, roof_edge), (-0.02, 0.03, 0.02, 0.02))]),
                car_plan(body, crown, *car_cabin_at(cabin, crown))]
        car_skin(body, roof)
        # A pillar over the glass behind the front doors; a van's whole back is panelled in.
        pillar = (cabin_rear + cabin_front) / 2.0 + (0.5 if van else -0.05)
        add_car_cabin(body, cabin, belt - 0.02, roof_edge, pillar - 0.07, pillar + 0.07, grow=0.015)
        if van:
            add_car_cabin(body, cabin, belt - 0.02, roof_edge, None, pillar - 0.07, grow=0.015)
        # Mirrors at the front of the doors.
        for side in (-1.0, 1.0):
            add_box(body, (0.14, 0.2, 0.13), (cabin_front - 0.34, side * (half_width + 0.04), belt + 0.12), bevel=0.04)
        objects.append(finish(f"Car{kind}Body", body))

        # The glass: the cabin itself, all the way round, from the belt up to the roof's edge.
        glass = bmesh.new()
        add_car_cabin(glass, cabin, belt - 0.02, roof_edge)
        objects.append(finish(f"Car{kind}Glass", glass))

        trim = bmesh.new()
        # Bumpers wrapped round the nose and the tail, a grille between the headlights.
        for x, station in ((nose - 0.1, shell[1]), (tail + 0.1, shell[-2])):
            add_box(trim, (0.3, station[1] * 2.0 + 0.1, 0.2), (x, 0.0, 0.42), bevel=0.09)
        add_box(trim, (0.08, 0.62, 0.16), (nose - 0.02, 0.0, 0.62), bevel=0.03)
        # Door lines and handles down each side, a sill under the doors.
        doors = (cabin_front - 0.18, pillar) if van else (cabin_front - 0.18, pillar, cabin_rear + 0.42)
        for side in (-1.0, 1.0):
            y = side * (half_width + 0.004)
            for x in doors:
                add_box(trim, (0.025, 0.02, belt - 0.5), (x, y, 0.3 + (belt - 0.3) / 2.0))
            for x in doors[1:] if not van else (pillar,):
                add_box(trim, (0.2, 0.05, 0.05), (x + 0.2, y, belt - 0.14))
            add_box(trim, (wheels[1] - wheels[0] - 1.3, 0.05, 0.07), ((wheels[0] + wheels[1]) / 2.0, side * (half_width - 0.01), 0.3))
        if kind == "Taxi":
            for side in (-1.0, 1.0):  # the checker band along the doors: two rows of squares, alternating
                for k in range(14):
                    for row in (0, 1):
                        if (k + row) % 2 == 0:
                            add_box(trim, (0.1, 0.02, 0.1), (-0.65 + k * 0.1, side * (half_width + 0.008), 0.66 + row * 0.1))
        objects.append(finish(f"Car{kind}Trim", trim))

        # Wheels: fat tyres standing a little out of the fenders, a domed hub cap in each.
        tyres = bmesh.new()
        hubs = bmesh.new()
        for x in wheels:
            for side in (-1.0, 1.0):
                outer = side * (half_width + 0.11)
                start = outer - CAR_TYRE_WIDTH if side > 0 else outer
                add_cylinder_y(tyres, (x, start, CAR_WHEEL_RADIUS), CAR_WHEEL_RADIUS, CAR_TYRE_WIDTH, segments=24)
                add_ellipsoid(hubs, (x, outer - side * 0.02, CAR_WHEEL_RADIUS), (0.25, 0.06, 0.25), rings=6, segments=16)
        # Number plates, front and back.
        add_box(hubs, (0.04, 0.46, 0.12), (nose + 0.05, 0.0, 0.43))
        add_box(hubs, (0.04, 0.46, 0.12), (tail - 0.01, 0.0, 0.66))
        objects.append(finish(f"Car{kind}Tyres", tyres))
        objects.append(finish(f"Car{kind}Hubs", hubs))

        # Round headlights set into the nose's corners; tail lamps across the back's.
        lights = bmesh.new()
        for side in (-1.0, 1.0):
            add_ellipsoid(lights, (nose - 0.03, side * (shell[0][1] - 0.2), shell[0][3] - 0.13), (0.08, 0.17, 0.13), rings=8, segments=16)
        objects.append(finish(f"Car{kind}Headlights", lights))
        tail_lights = bmesh.new()
        for side in (-1.0, 1.0):
            if van:  # tall lamps up the back corners
                add_box(tail_lights, (0.1, 0.14, 0.4), (tail + 0.03, side * (shell[-1][1] - 0.1), 0.8), bevel=0.03)
            else:
                add_box(tail_lights, (0.1, 0.36, 0.14), (tail + 0.03, side * (shell[-1][1] - 0.2), shell[-1][3] - 0.12), bevel=0.04)
        objects.append(finish(f"Car{kind}Taillights", tail_lights))

        if kind == "Taxi":
            sign = bmesh.new()
            add_box(sign, (0.4, 0.86, 0.2), ((cabin[2][1] + cabin[2][2]) / 2.0, 0.0, crown + 0.09), bevel=0.06)
            objects.append(finish("CarTaxiSign", sign))
    return objects


def add_box_at(bm, size, local, frame, bevel=0.0):
    """A box in a turned frame (see frame_at): `size`, centred at `local` within the frame."""
    transform = frame @ Matrix.Translation(local) @ Matrix.Diagonal((*size, 1.0))
    created = bmesh.ops.create_cube(bm, size=1.0, matrix=transform)
    if bevel > 0.0:
        verts = created["verts"]
        edges = list({edge for vert in verts for edge in vert.link_edges})
        bmesh.ops.bevel(bm, geom=edges, offset=bevel, segments=BEVEL_SEGMENTS, profile=0.5,
                        affect='EDGES', clamp_overlap=True)


def frame_at(centre, turn=(0.0, 0.0, 0.0)):
    """A frame at `centre`, turned by Euler angles in degrees (X, then Y, then Z)."""
    from mathutils import Euler
    return Matrix.Translation(centre) @ Euler([math.radians(a) for a in turn], 'XYZ').to_matrix().to_4x4()


def add_torus(bm, centre, major, minor, axis_matrix=None, segments=28, ring=10):
    """A ring of tube: `major` to the tube's middle, `minor` the tube's radius, round the lathe's axis."""
    profile = [(major + minor * math.cos(2.0 * math.pi * k / ring), minor * math.sin(2.0 * math.pi * k / ring))
               for k in range(ring + 1)]
    add_lathe(bm, profile, segments=segments, centre=centre, axis_matrix=axis_matrix)


# A lathe's axis turned to point at the front (-y), along +y, or along +x.
TO_FRONT = Matrix.Rotation(math.radians(90.0), 4, 'X')
TO_BACK = Matrix.Rotation(math.radians(-90.0), 4, 'X')
TO_SIDE = Matrix.Rotation(math.radians(90.0), 4, 'Y')


def build_street_kit():
    """Street furniture for the opening's street and the market outside the restaurant (the user's
    notes, 2026-11-10: every object as crisp as the kitchen's appliances; improve the vendor
    stalls). The kitchen's chunky, bevelled style, each piece split by colour. Fronts face -Y (the
    game's +z); origins on the ground under the middle unless a piece says otherwise.

    The market stall fits the game's stall station: a 1.8 x 0.8 m counter 0.9 m high at the origin,
    posts and a striped canopy over it and the stallholder's space behind (+y)."""
    objects = []

    # ---- The market stall ----
    slope = 0.4 / 1.72  # the canopy rises from 2.2 m at the front edge to 2.6 m at the back
    bm = bmesh.new()
    for x in (-1.05, 1.05):
        add_box(bm, (0.07, 0.07, 2.26), (x, -0.3, 1.13), bevel=0.012)   # front posts
        add_box(bm, (0.07, 0.07, 2.5), (x, 0.78, 1.25), bevel=0.012)    # back posts
        add_box(bm, (0.05, 1.08, 0.05), (x, 0.24, 1.98), bevel=0.01)    # side ties
        add_box_at(bm, (0.05, 1.2, 0.05), (0.0, 0.0, 0.0), frame_at((x, 0.24, 2.37), (math.degrees(math.atan(slope)), 0.0, 0.0)), bevel=0.01)  # rafters
    add_box(bm, (2.17, 0.05, 0.05), (0.0, -0.3, 2.2), bevel=0.01)       # front rail, the bulbs hang from it
    add_box(bm, (2.17, 0.05, 0.05), (0.0, 0.78, 2.46), bevel=0.01)      # back rail
    add_box(bm, (2.17, 0.05, 0.05), (0.0, 0.78, 1.0), bevel=0.01)       # a rail to lean on, at the back
    for x in (-0.55, 0.55):
        add_box(bm, (0.012, 0.012, 0.2), (x, -0.3, 2.08))               # the bulbs' cords
    objects.append(finish("StallFrame", bm))

    for name, phase in (("StallCanopyA", 0), ("StallCanopyB", 1)):     # the canopy, in stripes of two colours
        bm = bmesh.new()
        stripes = 8
        width = 2.5 / stripes
        for k in range(stripes):
            if k % 2 != phase:
                continue
            x0 = -1.25 + k * width
            add_extrude_yz(bm, [(-0.62, 2.2), (1.1, 2.6), (1.1, 2.65), (-0.62, 2.25)], x0, x0 + width)
            drop = 0.27 if phase == 0 else 0.19                         # the valance, one colour hanging lower
            add_extrude_yz(bm, [(-0.66, 2.25 - drop), (-0.62, 2.25 - drop), (-0.62, 2.25), (-0.66, 2.25)], x0, x0 + width)
        objects.append(finish(name, bm))

    bm = bmesh.new()  # the counter: a panelled body
    add_box(bm, (1.8, 0.8, 0.84), (0.0, 0.0, 0.42), bevel=0.02)
    for k in range(5):
        add_box(bm, (0.3, 0.02, 0.64), (-0.68 + k * 0.34, -0.405, 0.45), bevel=0.01)
    add_box(bm, (1.7, 0.03, 0.07), (0.0, -0.41, 0.05))
    objects.append(finish("StallCounter", bm))
    bm = bmesh.new()
    add_box(bm, (1.9, 0.9, 0.06), (0.0, 0.0, 0.87), bevel=0.02)
    objects.append(finish("StallCounterTop", bm))

    # A crate tipped towards the customer, and what goes in it. Their origin is on the counter top
    # under the crate's middle, so the builder lines up as many as the stall sells.
    crate = frame_at((0.0, 0.0, 0.11), (14.0, 0.0, 0.0))
    bm = bmesh.new()
    add_box_at(bm, (0.44, 0.34, 0.02), (0.0, 0.0, -0.07), crate)
    for y in (-0.16, 0.16):
        add_box_at(bm, (0.44, 0.02, 0.14), (0.0, y, 0.0), crate, bevel=0.004)
    for x in (-0.21, 0.21):
        add_box_at(bm, (0.02, 0.34, 0.14), (x, 0.0, 0.0), crate, bevel=0.004)
    objects.append(finish("StallCrate", bm))
    bm = bmesh.new()  # round produce: a heap of onions, tomatoes, oranges
    heap = [(-0.13, -0.09, 0.0), (0.0, -0.09, 0.0), (0.13, -0.09, 0.0), (-0.13, 0.05, 0.0), (0.0, 0.05, 0.0), (0.13, 0.05, 0.0),
            (-0.065, -0.02, 0.085), (0.065, -0.02, 0.085)]
    for local in heap:
        add_round_ball(bm, crate @ Vector(local), 0.07, rings=8, segments=14)
    objects.append(finish("StallProduceRound", bm))
    bm = bmesh.new()  # cuts of meat, or fish: slabs laid across each other
    for i, (x, y, turn) in enumerate([(-0.11, -0.06, 12.0), (0.1, -0.07, -8.0), (-0.08, 0.07, -15.0), (0.11, 0.06, 20.0)]):
        add_box_at(bm, (0.17, 0.12, 0.035), (0.0, 0.0, 0.0), crate @ frame_at((x, y, -0.03 + 0.012 * i), (0.0, 0.0, turn)), bevel=0.014)
    objects.append(finish("StallProduceSlabs", bm))

    bm = bmesh.new()
    for x in (-0.55, 0.55):
        add_ball(bm, (x, -0.3, 1.9), 0.06, segments=12)
    objects.append(finish("StallBulbs", bm))
    bm = bmesh.new()
    for x in (-0.55, 0.55):
        add_lathe(bm, [(0.02, 2.0), (0.05, 1.99), (0.15, 1.91), (0.155, 1.89), (0.14, 1.895), (0.04, 1.975), (0.0, 1.98)],
                  segments=16, centre=(x, -0.3, 0.0))
    objects.append(finish("StallShades", bm))

    bm = bmesh.new()  # a price slate leaning on the counter's front
    add_box_at(bm, (0.46, 0.02, 0.34), (0.0, 0.0, 0.0), frame_at((0.55, -0.47, 0.19), (12.0, 0.0, 0.0)), bevel=0.008)
    objects.append(finish("StallSlate", bm))

    bm = bmesh.new()  # the scales
    add_box(bm, (0.2, 0.16, 0.05), (0.72, 0.2, 0.925), bevel=0.012)
    add_box(bm, (0.04, 0.03, 0.18), (0.72, 0.27, 1.03), bevel=0.008)
    add_lathe(bm, [(0.0, 0.0), (0.1, 0.02), (0.11, 0.035), (0.1, 0.03), (0.0, 0.012)], segments=18, centre=(0.72, 0.18, 0.97))
    add_lathe(bm, [(0.0, 0.0), (0.055, 0.0), (0.055, 0.012), (0.0, 0.012)], segments=14, centre=(0.72, 0.27, 1.11), axis_matrix=TO_FRONT)
    objects.append(finish("StallScale", bm))

    bm = bmesh.new()  # closed: a cloth over the counter
    add_box(bm, (1.96, 0.96, 0.42), (0.0, 0.0, 0.72), bevel=0.05)
    objects.append(finish("StallCover", bm))

    # ---- Queue posts and their rope (1.4 m to the next post, along +x) ----
    bm = bmesh.new()
    add_lathe(bm, [(0.0, 0.0), (0.17, 0.0), (0.17, 0.025), (0.12, 0.05), (0.035, 0.09), (0.025, 0.12), (0.025, 0.86),
                   (0.045, 0.88), (0.045, 0.92), (0.025, 0.94), (0.0, 0.94)], segments=20)
    add_ball(bm, (0.0, 0.0, 0.97), 0.05, segments=14)
    objects.append(finish("StanchionPost", bm))
    bm = bmesh.new()
    add_pipe(bm, [(1.4 * k / 8.0, 0.0, 0.9 - 0.17 * math.sin(math.pi * k / 8.0)) for k in range(9)], radius=0.022, segments=10)
    objects.append(finish("StanchionRope", bm))

    # ---- A barrier across a closed street ----
    bm = bmesh.new()
    for x in (-0.95, 0.95):
        for side in (1.0, -1.0):
            add_box_at(bm, (0.06, 0.05, 1.09), (0.0, 0.0, 0.0), frame_at((x, side * 0.13, 0.525), (side * 13.9, 0.0, 0.0)), bevel=0.01)
    add_box(bm, (2.2, 0.04, 0.2), (0.0, 0.0, 0.92), bevel=0.012)
    add_box(bm, (2.2, 0.04, 0.12), (0.0, 0.0, 0.5), bevel=0.012)
    objects.append(finish("BarrierFrame", bm))
    bm = bmesh.new()
    for k in range(5):
        for y in (-0.022, 0.022):
            add_box(bm, (0.22, 0.006, 0.2), (-0.88 + k * 0.44, y, 0.92))
    objects.append(finish("BarrierStripes", bm))

    # ---- A delivery scooter with its box, and the rider's bag ----
    bm = bmesh.new()
    add_box(bm, (0.3, 0.62, 0.07), (0.0, -0.02, 0.27), bevel=0.025)                                      # footboard
    add_box_at(bm, (0.38, 0.08, 0.56), (0.0, 0.0, 0.0), frame_at((0.0, -0.4, 0.56), (-14.0, 0.0, 0.0)), bevel=0.035)  # leg shield
    add_box(bm, (0.16, 0.36, 0.1), (0.0, -0.64, 0.46), bevel=0.045)                                      # front mudguard
    add_box(bm, (0.36, 0.66, 0.3), (0.0, 0.44, 0.5), bevel=0.11)                                         # the body over the engine
    objects.append(finish("ScooterBody", bm))
    bm = bmesh.new()
    add_box(bm, (0.3, 0.62, 0.09), (0.0, 0.38, 0.7), bevel=0.04)
    objects.append(finish("ScooterSeat", bm))
    bm = bmesh.new()
    for y in (-0.64, 0.6):
        add_lathe(bm, [(0.0, -0.055), (0.17, -0.055), (0.22, -0.03), (0.22, 0.03), (0.17, 0.055), (0.0, 0.055)],
                  segments=20, centre=(0.0, y, 0.22), axis_matrix=TO_SIDE)
    objects.append(finish("ScooterTyres", bm))
    bm = bmesh.new()
    add_pipe(bm, [(0.0, -0.62, 0.3), (0.0, -0.46, 0.84), (0.0, -0.42, 1.0)], radius=0.03, segments=10)   # the steering column
    add_pipe(bm, [(-0.3, -0.4, 1.0), (0.3, -0.4, 1.0)], radius=0.022, segments=10)                       # handlebar
    for x in (-0.3, 0.3):
        add_pipe(bm, [(x * 0.8, -0.4, 1.0), (x, -0.36, 1.2)], radius=0.01, segments=6)                   # mirrors
        add_lathe(bm, [(0.0, 0.0), (0.05, 0.0), (0.05, 0.012), (0.0, 0.012)], segments=12, centre=(x, -0.36, 1.2), axis_matrix=TO_FRONT)
    for y in (-0.64, 0.6):
        add_lathe(bm, [(0.0, -0.062), (0.1, -0.062), (0.1, 0.062), (0.0, 0.062)], segments=12, centre=(0.0, y, 0.22), axis_matrix=TO_SIDE)
    add_pipe(bm, [(0.12, 0.1, 0.24), (0.2, 0.16, 0.02)], radius=0.015, segments=6)                       # kickstand
    add_box(bm, (0.3, 0.4, 0.03), (0.0, 0.8, 0.765), bevel=0.01)                                         # rack
    objects.append(finish("ScooterTrim", bm))
    bm = bmesh.new()
    add_lathe(bm, [(0.0, 0.0), (0.07, 0.0), (0.075, 0.02), (0.05, 0.05), (0.0, 0.06)], segments=14, centre=(0.0, -0.46, 0.86), axis_matrix=TO_FRONT)
    objects.append(finish("ScooterLight", bm))
    bm = bmesh.new()
    add_box(bm, (0.46, 0.44, 0.4), (0.0, 0.82, 0.98), bevel=0.035)
    add_box(bm, (0.48, 0.46, 0.05), (0.0, 0.82, 1.17), bevel=0.02)
    objects.append(finish("ScooterBox", bm))

    bm = bmesh.new()
    add_box(bm, (0.42, 0.3, 0.44), (0.0, 0.0, 0.22), bevel=0.04)
    objects.append(finish("DeliveryBag", bm))
    bm = bmesh.new()
    add_box(bm, (0.43, 0.31, 0.05), (0.0, 0.0, 0.4), bevel=0.015)
    add_box(bm, (0.2, 0.012, 0.2), (0.0, -0.152, 0.2), bevel=0.02)
    for x in (-0.12, 0.12):
        add_box(bm, (0.05, 0.02, 0.36), (x, 0.155, 0.22), bevel=0.008)
    objects.append(finish("DeliveryBagTrim", bm))

    # ---- In hands: a phone and a camera ----
    # The phone (the user's note, 2026-11-10: "properly model a mobile phone so it's noticeable and
    # clear what they are holding and from where the flash lights are coming"): bigger and chunkier
    # than life, as the characters are, 11 x 21 cm. Origin at its middle, screen towards -y. A case
    # in any colour, a lit screen with an app on it (a header, a photo, a row of dots), and on the
    # back a camera island with two lenses and the flash's LED beside them.
    bm = bmesh.new()
    add_box(bm, (0.112, 0.02, 0.212), (0.0, 0.0, 0.0), bevel=0.009)
    objects.append(finish("Phone", bm))
    bm = bmesh.new()
    add_box(bm, (0.096, 0.003, 0.192), (0.0, -0.0105, 0.0))
    objects.append(finish("PhoneScreen", bm))
    bm = bmesh.new()  # the app's header and its dots
    add_box(bm, (0.096, 0.003, 0.03), (0.0, -0.0112, 0.081))
    for x in (-0.026, 0.0, 0.026):
        add_lathe(bm, [(0.0, 0.0), (0.008, 0.0), (0.008, 0.002), (0.0, 0.002)], segments=10, centre=(x, -0.012, -0.072), axis_matrix=TO_FRONT)
    objects.append(finish("PhoneUiA", bm))
    bm = bmesh.new()  # the photo in the feed
    add_box(bm, (0.082, 0.003, 0.094), (0.0, -0.0112, 0.006))
    objects.append(finish("PhoneUiB", bm))
    bm = bmesh.new()  # the camera island, top corner of the back
    add_box(bm, (0.05, 0.008, 0.056), (0.022, 0.013, 0.066), bevel=0.003)
    for dz in (-0.013, 0.013):
        add_lathe(bm, [(0.0, 0.0), (0.0095, 0.0), (0.0095, 0.004), (0.0, 0.004)], segments=12, centre=(0.014, 0.017, 0.066 + dz), axis_matrix=TO_BACK)
    objects.append(finish("PhoneCamera", bm))
    bm = bmesh.new()  # the flash's LED
    add_lathe(bm, [(0.0, 0.0), (0.007, 0.0), (0.007, 0.004), (0.0, 0.004)], segments=10, centre=(0.035, 0.017, 0.066), axis_matrix=TO_BACK)
    objects.append(finish("PhoneLed", bm))
    bm = bmesh.new()
    add_box(bm, (0.15, 0.07, 0.1), (0.0, 0.0, 0.0), bevel=0.014)
    add_box(bm, (0.04, 0.09, 0.1), (0.058, -0.005, 0.0), bevel=0.014)
    add_box(bm, (0.06, 0.05, 0.035), (0.0, 0.0, 0.066), bevel=0.01)
    objects.append(finish("CameraBody", bm))
    bm = bmesh.new()
    add_lathe(bm, [(0.0, 0.0), (0.04, 0.0), (0.046, 0.015), (0.046, 0.075), (0.04, 0.085), (0.03, 0.08), (0.0, 0.075)],
              segments=16, centre=(-0.01, -0.035, 0.0), axis_matrix=TO_FRONT)
    objects.append(finish("CameraLens", bm))

    # ---- A ring light on a tripod ----
    bm = bmesh.new()
    for k in range(3):
        a = math.pi / 2 + k * 2 * math.pi / 3
        add_pipe(bm, [(0.0, 0.0, 0.55), (0.38 * math.cos(a), 0.38 * math.sin(a), 0.0)], radius=0.014, segments=6)
    add_pipe(bm, [(0.0, 0.0, 0.5), (0.0, 0.0, 1.38)], radius=0.016, segments=8)
    add_box(bm, (0.09, 0.02, 0.03), (0.0, 0.0, 1.38), bevel=0.006)
    objects.append(finish("RingLightStand", bm))
    bm = bmesh.new()
    add_torus(bm, (0.0, 0.0, 1.62), 0.24, 0.035, axis_matrix=TO_FRONT)
    objects.append(finish("RingLightRing", bm))

    # ---- Tables and a chair for terraces and bars ----
    for name, height in (("Bistro", 0.72), ("HighTable", 1.05)):
        bm = bmesh.new()
        add_lathe(bm, [(0.0, 0.0), (0.22, 0.0), (0.22, 0.025), (0.06, 0.06), (0.03, 0.1), (0.03, height - 0.03), (0.07, height), (0.0, height)], segments=20)
        objects.append(finish(f"{name}Base", bm))
        bm = bmesh.new()
        add_lathe(bm, [(0.0, height), (0.34, height), (0.36, height + 0.012), (0.36, height + 0.03), (0.34, height + 0.042), (0.0, height + 0.042)], segments=28)
        objects.append(finish(f"{name}Top", bm))
    bm = bmesh.new()  # a bistro chair: round seat, splayed legs, a slatted back (+y)
    add_lathe(bm, [(0.0, 0.43), (0.18, 0.43), (0.19, 0.445), (0.18, 0.46), (0.0, 0.46)], segments=20)
    for sx in (-1.0, 1.0):
        for sy in (-1.0, 1.0):
            add_pipe(bm, [(sx * 0.13, sy * 0.13, 0.44), (sx * 0.17, sy * 0.17, 0.0)], radius=0.012, segments=6)
        add_pipe(bm, [(sx * 0.15, 0.16, 0.44), (sx * 0.16, 0.2, 0.86)], radius=0.012, segments=6)
    add_pipe(bm, [(-0.16, 0.2, 0.86), (0.0, 0.215, 0.9), (0.16, 0.2, 0.86)], radius=0.012, segments=6)
    for x in (-0.07, 0.0, 0.07):
        add_box(bm, (0.035, 0.012, 0.34), (x, 0.195, 0.68), bevel=0.004)
    objects.append(finish("BistroChair", bm))

    # ---- The bar: a bottle, and a pendant lamp (origin at the shade's rim, its cord 1.2 m up) ----
    bm = bmesh.new()
    add_lathe(bm, [(0.0, 0.0), (0.038, 0.0), (0.04, 0.01), (0.04, 0.19), (0.016, 0.26), (0.014, 0.32), (0.019, 0.325), (0.019, 0.345), (0.0, 0.345)], segments=14)
    objects.append(finish("Bottle", bm))
    bm = bmesh.new()
    add_lathe(bm, [(0.0, 0.3), (0.03, 0.3), (0.05, 0.27), (0.2, 0.07), (0.21, 0.02), (0.195, 0.02), (0.185, 0.07), (0.04, 0.255), (0.0, 0.26)], segments=24)
    add_pipe(bm, [(0.0, 0.0, 0.3), (0.0, 0.0, 1.2)], radius=0.008, segments=6)
    objects.append(finish("PendantShade", bm))
    bm = bmesh.new()
    add_ball(bm, (0.0, 0.0, 0.1), 0.07, segments=12)
    objects.append(finish("PendantBulb", bm))

    # ---- The restaurant: a table under a white cloth laid for two (places along x), a waiter's tray, a lamp over a sign ----
    bm = bmesh.new()
    add_lathe(bm, [(0.0, 0.0), (0.2, 0.0), (0.2, 0.02), (0.04, 0.05), (0.03, 0.4), (0.0, 0.4)], segments=16)
    objects.append(finish("DiningTableBase", bm))
    bm = bmesh.new()
    add_lathe(bm, [(0.0, 0.74), (0.46, 0.74), (0.475, 0.728), (0.5, 0.5), (0.52, 0.36), (0.5, 0.36), (0.465, 0.7), (0.0, 0.72)], segments=28)
    objects.append(finish("DiningTableCloth", bm))
    bm = bmesh.new()
    for sx in (-1.0, 1.0):
        add_lathe(bm, [(0.0, 0.742), (0.11, 0.742), (0.13, 0.755), (0.125, 0.76), (0.1, 0.75), (0.0, 0.75)], segments=18, centre=(sx * 0.24, 0.0, 0.0))
    objects.append(finish("TableSetPlates", bm))
    bm = bmesh.new()
    for sx in (-1.0, 1.0):
        add_ellipsoid(bm, (sx * 0.24, 0.0, 0.775), (0.06, 0.05, 0.025))
    objects.append(finish("TableSetFood", bm))
    bm = bmesh.new()
    for sx in (-1.0, 1.0):
        add_lathe(bm, [(0.0, 0.742), (0.03, 0.742), (0.004, 0.75), (0.004, 0.82), (0.03, 0.84), (0.034, 0.9), (0.03, 0.9), (0.026, 0.845), (0.0, 0.83)],
                  segments=12, centre=(sx * 0.2, 0.17, 0.0))
    objects.append(finish("TableSetGlasses", bm))
    bm = bmesh.new()
    add_lathe(bm, [(0.0, 0.742), (0.025, 0.742), (0.025, 0.8), (0.0, 0.8)], segments=10, centre=(0.0, -0.12, 0.0))
    objects.append(finish("TableCandle", bm))
    bm = bmesh.new()
    add_ellipsoid(bm, (0.0, -0.12, 0.83), (0.012, 0.012, 0.028))
    objects.append(finish("TableFlame", bm))
    bm = bmesh.new()  # a tray (origin under its middle) and a cloche on it
    add_lathe(bm, [(0.0, 0.0), (0.2, 0.0), (0.21, 0.012), (0.2, 0.02), (0.0, 0.014)], segments=22)
    objects.append(finish("Tray", bm))
    bm = bmesh.new()
    add_lathe(bm, [(0.0, 0.02), (0.14, 0.02), (0.14, 0.04), (0.12, 0.1), (0.07, 0.15), (0.0, 0.165)], segments=20)
    add_ball(bm, (0.0, 0.0, 0.18), 0.02, segments=8)
    objects.append(finish("TrayCloche", bm))
    bm = bmesh.new()  # a gooseneck lamp over a sign (origin at its wall mount, out towards -y, lighting down)
    add_pipe(bm, [(0.0, 0.0, 0.0), (0.0, -0.05, 0.25), (0.0, -0.3, 0.4), (0.0, -0.5, 0.32)], radius=0.015, segments=8)
    add_lathe(bm, [(0.0, 0.03), (0.03, 0.03), (0.1, -0.07), (0.09, -0.07), (0.0, 0.0)], segments=14, centre=(0.0, -0.5, 0.32))
    objects.append(finish("SignLamp", bm))
    bm = bmesh.new()
    add_ball(bm, (0.0, -0.5, 0.275), 0.035, segments=10)
    objects.append(finish("SignLampBulb", bm))

    # ---- An entrance canopy with bulbs round its edge (origin at the wall, on the ground, out towards -y) ----
    bm = bmesh.new()
    add_box(bm, (3.6, 3.2, 0.24), (0.0, -1.6, 3.34), bevel=0.07)
    objects.append(finish("CanopyTop", bm))
    bm = bmesh.new()
    add_box(bm, (3.68, 3.26, 0.07), (0.0, -1.61, 3.2), bevel=0.02)
    for x in (-1.62, 1.62):
        add_lathe(bm, [(0.0, 0.0), (0.13, 0.0), (0.13, 0.05), (0.07, 0.1), (0.05, 0.18), (0.05, 3.0), (0.08, 3.08), (0.1, 3.17), (0.0, 3.17)],
                  segments=16, centre=(x, -3.0, 0.0))
    objects.append(finish("CanopyTrim", bm))
    bm = bmesh.new()
    for k in range(7):
        add_ball(bm, (-1.5 + k * 0.5, -3.27, 3.2), 0.055, segments=10)
    for k in range(5):
        for x in (-1.87, 1.87):
            add_ball(bm, (x, -0.5 - k * 0.6, 3.2), 0.055, segments=10)
    objects.append(finish("CanopyBulbs", bm))

    # ---- A door's frame (a 1.6 x 2.7 m opening) and one leaf (origin at its hinge, 0.78 m along +x) ----
    bm = bmesh.new()
    for x in (-0.86, 0.86):
        add_box(bm, (0.12, 0.16, 2.76), (x, 0.0, 1.38), bevel=0.02)
    add_box(bm, (1.84, 0.16, 0.12), (0.0, 0.0, 2.76), bevel=0.02)
    objects.append(finish("DoorFrame", bm))
    bm = bmesh.new()
    for x in (0.03, 0.75):
        add_box(bm, (0.06, 0.05, 2.62), (x, 0.0, 1.34), bevel=0.012)
    add_box(bm, (0.78, 0.05, 0.08), (0.39, 0.0, 2.63), bevel=0.012)
    add_box(bm, (0.78, 0.05, 0.22), (0.39, 0.0, 0.14), bevel=0.012)
    for y in (-0.07, 0.07):
        add_pipe(bm, [(0.66, y, 0.85), (0.66, y, 1.75)], radius=0.02, segments=8)
    for z in (0.95, 1.65):
        add_pipe(bm, [(0.66, -0.07, z), (0.66, 0.07, z)], radius=0.012, segments=6)
    objects.append(finish("DoorLeaf", bm))
    bm = bmesh.new()
    add_box(bm, (0.66, 0.02, 2.3), (0.39, 0.0, 1.4))
    objects.append(finish("DoorLeafGlass", bm))

    # ---- Pavement pieces: a planter with a hedge, a hydrant, a bollard, a bench, a chalk board, a menu on a stand ----
    bm = bmesh.new()
    add_box(bm, (1.6, 0.5, 0.46), (0.0, 0.0, 0.23), bevel=0.04)
    add_box(bm, (1.68, 0.58, 0.07), (0.0, 0.0, 0.47), bevel=0.02)
    objects.append(finish("PlanterBox", bm))
    bm = bmesh.new()
    for k in range(5):
        add_round_ball(bm, (-0.56 + k * 0.28, 0.02 * (-1) ** k, 0.72), 0.26, rings=8, segments=14)
    objects.append(finish("PlanterHedge", bm))
    bm = bmesh.new()
    add_lathe(bm, [(0.0, 0.0), (0.16, 0.0), (0.16, 0.04), (0.11, 0.07), (0.1, 0.5), (0.13, 0.53), (0.13, 0.57), (0.1, 0.6),
                   (0.07, 0.7), (0.03, 0.74), (0.03, 0.78), (0.0, 0.78)], segments=18)
    for sx in (-1.0, 1.0):
        add_lathe(bm, [(0.0, 0.0), (0.055, 0.0), (0.055, 0.14), (0.07, 0.14), (0.07, 0.17), (0.0, 0.17)], segments=12,
                  centre=(0.0, 0.0, 0.42), axis_matrix=Matrix.Rotation(math.radians(sx * 90.0), 4, 'Y'))
    add_lathe(bm, [(0.0, 0.0), (0.07, 0.0), (0.07, 0.15), (0.085, 0.15), (0.085, 0.18), (0.0, 0.18)], segments=12, centre=(0.0, 0.0, 0.34), axis_matrix=TO_FRONT)
    objects.append(finish("Hydrant", bm))
    bm = bmesh.new()
    add_lathe(bm, [(0.0, 0.0), (0.09, 0.0), (0.09, 0.05), (0.07, 0.08), (0.065, 0.8), (0.08, 0.83), (0.08, 0.87), (0.05, 0.93), (0.0, 0.95)], segments=16)
    objects.append(finish("Bollard", bm))
    bm = bmesh.new()
    for x in (-0.75, 0.75):
        add_box(bm, (0.06, 0.5, 0.06), (x, 0.0, 0.03), bevel=0.012)
        add_box(bm, (0.06, 0.06, 0.42), (x, -0.18, 0.21), bevel=0.012)
        add_box_at(bm, (0.06, 0.06, 0.86), (0.0, 0.0, 0.0), frame_at((x, 0.2, 0.43), (-8.0, 0.0, 0.0)), bevel=0.012)
        add_box(bm, (0.06, 0.46, 0.05), (x, 0.0, 0.6), bevel=0.012)
    objects.append(finish("BenchFrame", bm))
    bm = bmesh.new()
    for k in range(4):
        add_box(bm, (1.7, 0.09, 0.03), (0.0, -0.18 + k * 0.11, 0.44), bevel=0.008)
    for k in range(3):
        add_box_at(bm, (1.7, 0.03, 0.09), (0.0, 0.0, 0.0), frame_at((0.0, 0.215 + k * 0.017, 0.56 + k * 0.12), (-8.0, 0.0, 0.0)), bevel=0.008)
    objects.append(finish("BenchSlats", bm))
    bm = bmesh.new()
    for side in (1.0, -1.0):
        add_box_at(bm, (0.6, 0.035, 0.98), (0.0, 0.0, 0.0), frame_at((0.0, side * 0.14, 0.47), (side * 15.0, 0.0, 0.0)), bevel=0.012)
    objects.append(finish("ChalkFrame", bm))
    bm = bmesh.new()
    for side in (1.0, -1.0):
        add_box_at(bm, (0.5, 0.01, 0.8), (0.0, side * 0.02, 0.02), frame_at((0.0, side * 0.14, 0.47), (side * 15.0, 0.0, 0.0)))
    objects.append(finish("ChalkFace", bm))
    stand = frame_at((0.0, -0.02, 1.32), (-20.0, 0.0, 0.0))
    bm = bmesh.new()
    add_lathe(bm, [(0.0, 0.0), (0.2, 0.0), (0.2, 0.03), (0.05, 0.07), (0.025, 0.1), (0.025, 1.2), (0.0, 1.2)], segments=16)
    add_box_at(bm, (0.44, 0.03, 0.58), (0.0, 0.0, 0.0), stand, bevel=0.012)
    objects.append(finish("MenuStandPost", bm))
    bm = bmesh.new()
    add_box_at(bm, (0.38, 0.006, 0.52), (0.0, -0.018, 0.0), stand)
    objects.append(finish("MenuStandFace", bm))

    # ---- The frame round a 9.6 x 5.4 m screen (origin at the wall under its middle, out towards -y) ----
    bm = bmesh.new()
    width, height, bar, deep = 9.6, 5.4, 0.28, 0.4
    add_box(bm, (width + 2 * bar, deep, bar), (0.0, -deep / 2, bar / 2), bevel=0.06)
    add_box(bm, (width + 2 * bar, deep, bar), (0.0, -deep / 2, height + 1.5 * bar), bevel=0.06)
    for x in (-(width + bar) / 2, (width + bar) / 2):
        add_box(bm, (bar, deep, height), (x, -deep / 2, bar + height / 2), bevel=0.06)
    add_box(bm, (width, 0.1, height), (0.0, -0.05, bar + height / 2))
    objects.append(finish("ScreenBezel", bm))
    return objects


def add_rounded_loft(bm, sections, radius, per_corner=4):
    """A closed, rounded-rectangle solid through (width, depth, z) sections, bottom to top."""
    rings = [rounded_rect(bm, w, d, radius, z, per_corner) for w, d, z in sections]
    connect_rings(bm, rings)
    bm.faces.new(list(reversed(rings[0])))
    bm.faces.new(rings[-1])


# The bin's four castors: where each wheel's axle sits under it, in Blender (x, y); axles are
# 0.12 m up. GreyboxStreet places the wheels at the same spots (Unity x = -x, z = -y).
WHEELIE_BIN_WHEELS = [(-0.6, -0.3), (-0.6, 0.3), (0.6, -0.3), (0.6, 0.3)]


def build_wheelie_bin():
    """The street's communal wheelie bin (the user's reference photo, 2026-11-11): a big
    four-wheeled container with a tapered, flared body, a chunky moulded rim with V ribs under it,
    side grips and push handles, black latch blocks, a closed domed lid with two panels and a block
    handle, and castor wheels on steel forks. 1.82 m wide, 1.25 m deep with its handles, 1.34 m
    tall, front (-y) to the street. Split by colour: Body (and the moulded rim, ribs and grips),
    Lid, LidPanels, Trim (latches and hinges), Brackets, and one Wheel and Hub that the game
    places four times and turns as the bin rolls."""
    objects = []

    body = bmesh.new()
    base_z, rim_z = 0.29, 1.06

    def half_depth(z):  # the body's front face flares out on the way up
        return 0.43 + (0.49 - 0.43) * (z - 0.37) / (rim_z - 0.37)

    add_rounded_loft(body, [(1.40, 0.76, base_z), (1.50, 0.86, 0.37), (1.64, 0.98, rim_z)], 0.1)
    # The moulded rim: a fat lip all round, wider than the body and the lid, which closes inside it.
    add_rounded_loft(body, [(1.62, 0.96, rim_z - 0.03), (1.82, 1.16, rim_z + 0.01), (1.82, 1.16, 1.16), (1.76, 1.10, 1.18)], 0.1)
    # V ribs under the rim, front and back: stiffeners that taper to a point down the body.
    for x in [-0.6 + 0.2 * k for k in range(7)]:
        for side in (-1.0, 1.0):
            top, bottom = 1.04, 0.84
            inner_top, inner_bottom = half_depth(top) - 0.02, half_depth(bottom) - 0.02
            profile = [(side * inner_top, top), (side * (inner_top + 0.07), top), (side * inner_bottom, bottom)]
            add_extrude_yz(body, profile, x - 0.03, x + 0.03)
    # Side grips on the ends, and push handles along the back (against the wall).
    for side in (-1.0, 1.0):
        add_box(body, (0.06, 0.46, 0.07), (side * 0.97, 0.0, 1.11), bevel=0.025)
        for y in (-0.19, 0.19):
            add_box(body, (0.1, 0.06, 0.06), (side * 0.93, y, 1.11), bevel=0.02)
        add_box(body, (0.34, 0.06, 0.06), (side * 0.55, 0.62, 1.12), bevel=0.025)
        for x in (side * 0.42, side * 0.68):
            add_box(body, (0.06, 0.08, 0.06), (x, 0.59, 1.12), bevel=0.02)
    objects.append(finish("WheelieBinBody", body))

    lid = bmesh.new()
    # Closed, and domed: an overhanging lip, then a rise to a flat crown.
    add_rounded_loft(lid, [(1.74, 1.08, 1.17), (1.74, 1.08, 1.23), (1.66, 1.00, 1.26), (1.46, 0.82, 1.31)], 0.1)
    # The block handle at the front of the crown, to lift it by.
    add_box(lid, (0.38, 0.13, 0.07), (0.0, -0.35, 1.33), bevel=0.025)
    objects.append(finish("WheelieBinLid", lid))

    panels = bmesh.new()  # two moulded panels on the crown, a shade darker so they read as recessed
    for x in (-0.36, 0.36):
        add_box(panels, (0.58, 0.44, 0.02), (x, 0.08, 1.31), bevel=0.008)
    objects.append(finish("WheelieBinLidPanels", panels))

    trim = bmesh.new()
    for side in (-1.0, 1.0):
        # A latch block on each end, under the rim near the front, with a round knob.
        add_box(trim, (0.09, 0.18, 0.15), (side * 0.82, -0.18, 0.95), bevel=0.02)
        add_lathe(trim, [(0.0, 0.0), (0.035, 0.0), (0.035, 0.07), (0.0, 0.07)], segments=12,
                  centre=(side * 0.86 if side > 0 else -0.93, -0.18, 0.95), axis_matrix=TO_SIDE)
        # The lid's hinges along the back.
        add_box(trim, (0.16, 0.08, 0.07), (side * 0.25, 0.56, 1.2), bevel=0.015)
    objects.append(finish("WheelieBinTrim", trim))

    # The castors' steel: a fork either side of each wheel and the swivel plate it hangs from.
    brackets = bmesh.new()
    for x, y in WHEELIE_BIN_WHEELS:
        for fork in (-1.0, 1.0):
            add_box(brackets, (0.09, 0.018, 0.18), (x, y + fork * 0.052, 0.19), bevel=0.005)
        add_box(brackets, (0.16, 0.14, 0.03), (x, y, 0.28), bevel=0.008)  # the swivel plate
    objects.append(finish("WheelieBinBrackets", brackets))

    # One wheel, centred on its axle (along y), placed four times by the game and turned as the bin
    # rolls: a black tyre, and a steel hub with a cross on each face so the turning shows.
    wheel = bmesh.new()
    add_cylinder_y(wheel, (0.0, -0.04, 0.0), 0.12, 0.08, segments=16)
    objects.append(finish("WheelieBinWheel", wheel))
    hub = bmesh.new()
    add_cylinder_y(hub, (0.0, -0.05, 0.0), 0.05, 0.1, segments=10)
    for size in ((0.15, 0.1, 0.025), (0.025, 0.1, 0.15)):
        add_box(hub, size, (0.0, 0.0, 0.0), bevel=0.006)
    objects.append(finish("WheelieBinHub", hub))
    for part in objects[-2:]:
        part.location = (1.3, 0.0, 0.12)  # apart, for previews only: the mesh stays centred on its axle
    return objects


def build_floor_kit():
    """Furniture for the floors of the boss's tower, seen through its glass as the opening climbs
    (the user's notes, 2026-11-10: every object as crisp as the kitchen's appliances; the floors are
    the boss's empire: test kitchens, a photo studio, a social-media room, a call centre, the
    accountants, a canteen). Fronts face -Y; origins on the floor under the middle unless said."""
    objects = []

    # ---- A work desk (the chair's side is +y, so the screen glows towards whoever sits there) ----
    bm = bmesh.new()
    add_box(bm, (1.5, 0.75, 0.04), (0.0, 0.0, 0.73), bevel=0.014)
    objects.append(finish("WorkDeskTop", bm))
    bm = bmesh.new()
    for x in (-0.7, 0.7):
        add_box(bm, (0.04, 0.68, 0.71), (x, 0.0, 0.355), bevel=0.012)
    add_box(bm, (1.36, 0.03, 0.34), (0.0, -0.3, 0.52), bevel=0.01)
    add_box(bm, (0.4, 0.5, 0.56), (0.46, 0.05, 0.3), bevel=0.015)
    for z in (0.14, 0.32, 0.5):
        add_box(bm, (0.34, 0.02, 0.15), (0.46, 0.305, z), bevel=0.006)
    objects.append(finish("WorkDeskLegs", bm))
    bm = bmesh.new()  # the monitor on its stand, a keyboard and a mouse
    add_box(bm, (0.58, 0.035, 0.36), (0.0, -0.14, 1.06), bevel=0.012)
    add_box(bm, (0.05, 0.04, 0.16), (0.0, -0.16, 0.85), bevel=0.01)
    add_box(bm, (0.24, 0.17, 0.016), (0.0, -0.15, 0.758), bevel=0.006)
    add_box(bm, (0.42, 0.14, 0.02), (0.0, 0.14, 0.76), bevel=0.006)
    add_ellipsoid(bm, (0.33, 0.15, 0.765), (0.03, 0.05, 0.018))
    objects.append(finish("DeskSetDark", bm))
    bm = bmesh.new()
    add_box(bm, (0.54, 0.004, 0.32), (0.0, -0.1205, 1.06))
    objects.append(finish("DeskSetScreen", bm))

    # ---- The photo studio: a softbox on a stand, a tripod, a paper backdrop ----
    bm = bmesh.new()
    for k in range(3):
        a = math.pi / 2 + k * 2 * math.pi / 3
        add_pipe(bm, [(0.0, 0.0, 0.6), (0.45 * math.cos(a), 0.45 * math.sin(a), 0.0)], radius=0.016, segments=6)
    add_pipe(bm, [(0.0, 0.0, 0.55), (0.0, 0.1, 1.9)], radius=0.018, segments=8)
    objects.append(finish("SoftboxStand", bm))
    bm = bmesh.new()  # the hood: a pyramid opening to the front, closed by the diffuser
    back = [bm.verts.new((x, 0.3, 1.9 + z)) for x, z in ((-0.08, -0.08), (0.08, -0.08), (0.08, 0.08), (-0.08, 0.08))]
    front = [bm.verts.new((x, -0.12, 1.9 + z)) for x, z in ((-0.39, -0.51), (0.39, -0.51), (0.39, 0.51), (-0.39, 0.51))]
    bm.faces.new(back)
    bm.faces.new(list(reversed(front)))
    for i in range(4):
        j = (i + 1) % 4
        bm.faces.new([back[i], front[i], front[j], back[j]])
    objects.append(finish("SoftboxHood", bm))
    bm = bmesh.new()
    add_box(bm, (0.74, 0.012, 0.98), (0.0, -0.126, 1.9))
    objects.append(finish("SoftboxFace", bm))
    bm = bmesh.new()
    for k in range(3):
        a = math.pi / 2 + k * 2 * math.pi / 3
        add_pipe(bm, [(0.0, 0.0, 1.25), (0.4 * math.cos(a), 0.4 * math.sin(a), 0.0)], radius=0.016, segments=6)
    add_pipe(bm, [(0.0, 0.0, 1.2), (0.0, 0.0, 1.36)], radius=0.02, segments=8)
    add_box(bm, (0.1, 0.08, 0.02), (0.0, 0.0, 1.37), bevel=0.005)
    objects.append(finish("Tripod", bm))
    bm = bmesh.new()
    for x in (-1.55, 1.55):
        add_pipe(bm, [(x, 0.5, 0.0), (x, 0.5, 2.6)], radius=0.022, segments=8)
        add_box(bm, (0.06, 0.5, 0.03), (x, 0.5, 0.015), bevel=0.008)
    add_pipe(bm, [(-1.6, 0.5, 2.6), (1.6, 0.5, 2.6)], radius=0.03, segments=8)
    objects.append(finish("BackdropStand", bm))
    bm = bmesh.new()  # the paper: down from the bar, round a curve, out across the floor
    add_box(bm, (2.8, 0.012, 2.17), (0.0, 0.47, 1.505))
    for k in range(6):
        a = (k + 0.5) / 6.0 * math.pi / 2
        add_box_at(bm, (2.8, 0.012, 0.125), (0.0, 0.0, 0.0), frame_at((0.0, 0.07 + 0.4 * math.cos(a), 0.42 - 0.4 * math.sin(a)), (-math.degrees(a), 0.0, 0.0)))
    add_box(bm, (2.8, 1.18, 0.012), (0.0, -0.52, 0.02))
    objects.append(finish("BackdropPaper", bm))

    # ---- The social-media room: six screens on a wall board (origin under its middle, at its foot) ----
    bm = bmesh.new()
    add_box(bm, (3.0, 0.06, 1.3), (0.0, 0.0, 0.65), bevel=0.02)
    for c in range(3):
        for r in range(2):
            add_box(bm, (0.94, 0.03, 0.56), (-0.98 + c * 0.98, -0.04, 0.34 + r * 0.62), bevel=0.012)
    objects.append(finish("ScreenWallFrame", bm))
    for name, cells in (("ScreenWallA", ((0, 0), (2, 1))), ("ScreenWallB", ((1, 0), (0, 1))), ("ScreenWallC", ((2, 0), (1, 1)))):
        bm = bmesh.new()
        for c, r in cells:
            add_box(bm, (0.88, 0.004, 0.5), (-0.98 + c * 0.98, -0.057, 0.34 + r * 0.62))
        objects.append(finish(name, bm))

    # ---- The call centre: a partition between desks ----
    bm = bmesh.new()
    add_box(bm, (1.5, 0.05, 1.15), (0.0, 0.0, 0.6), bevel=0.015)
    objects.append(finish("CubiclePanel", bm))
    bm = bmesh.new()
    add_box(bm, (1.54, 0.07, 0.04), (0.0, 0.0, 1.19), bevel=0.012)
    for x in (-0.6, 0.6):
        add_box(bm, (0.06, 0.4, 0.03), (x, 0.0, 0.015), bevel=0.008)
    objects.append(finish("CubicleTrim", bm))

    # ---- For any floor: a water cooler, a vending machine, a whiteboard, a ceiling light, a coffee machine ----
    bm = bmesh.new()
    add_box(bm, (0.32, 0.32, 0.98), (0.0, 0.0, 0.49), bevel=0.03)
    add_box(bm, (0.2, 0.03, 0.16), (0.0, -0.165, 0.74), bevel=0.01)
    for x in (-0.05, 0.05):
        add_box(bm, (0.03, 0.05, 0.04), (x, -0.185, 0.78), bevel=0.008)
    objects.append(finish("CoolerBody", bm))
    bm = bmesh.new()
    add_lathe(bm, [(0.0, 0.98), (0.06, 0.98), (0.06, 1.03), (0.13, 1.07), (0.13, 1.34), (0.1, 1.4), (0.0, 1.4)], segments=18)
    objects.append(finish("CoolerBottle", bm))
    bm = bmesh.new()
    add_box(bm, (0.95, 0.8, 1.85), (0.0, 0.0, 0.925), bevel=0.03)
    objects.append(finish("VendingBody", bm))
    bm = bmesh.new()
    add_box(bm, (0.56, 0.012, 1.2), (-0.14, -0.402, 1.08))
    objects.append(finish("VendingWindow", bm))
    bm = bmesh.new()
    add_box(bm, (0.2, 0.02, 0.5), (0.32, -0.405, 1.2), bevel=0.008)
    add_box(bm, (0.56, 0.02, 0.16), (-0.14, -0.405, 0.3), bevel=0.008)
    for r in range(4):  # shelves of snacks behind the window
        add_box(bm, (0.54, 0.02, 0.02), (-0.14, -0.41, 0.56 + r * 0.29))
    objects.append(finish("VendingTrim", bm))
    bm = bmesh.new()
    for r in range(4):
        for c in range(4):
            add_box(bm, (0.09, 0.02, 0.17), (-0.335 + c * 0.13, -0.412, 0.67 + r * 0.29), bevel=0.006)
    objects.append(finish("VendingGoods", bm))
    bm = bmesh.new()  # a whiteboard (origin at the wall under its middle)
    add_box(bm, (1.8, 0.04, 1.1), (0.0, -0.02, 0.55), bevel=0.012)
    add_box(bm, (1.6, 0.07, 0.03), (0.0, -0.06, 0.03), bevel=0.008)
    objects.append(finish("WhiteboardFrame", bm))
    bm = bmesh.new()
    add_box(bm, (1.7, 0.006, 1.0), (0.0, -0.042, 0.55))
    objects.append(finish("WhiteboardFace", bm))
    bm = bmesh.new()  # a ceiling light (origin on the ceiling)
    add_box(bm, (1.24, 0.64, 0.05), (0.0, 0.0, -0.025), bevel=0.012)
    objects.append(finish("CeilingPanelFrame", bm))
    bm = bmesh.new()
    add_box(bm, (1.14, 0.54, 0.012), (0.0, 0.0, -0.052))
    objects.append(finish("CeilingPanelGlow", bm))
    bm = bmesh.new()  # an espresso machine for a counter (origin under it)
    add_box(bm, (0.52, 0.4, 0.36), (0.0, 0.0, 0.24), bevel=0.03)
    add_box(bm, (0.54, 0.42, 0.05), (0.0, 0.0, 0.03), bevel=0.012)
    add_box(bm, (0.5, 0.36, 0.03), (0.0, 0.02, 0.435), bevel=0.01)
    objects.append(finish("CoffeeMachine", bm))
    bm = bmesh.new()
    for x in (-0.13, 0.13):
        add_lathe(bm, [(0.0, 0.0), (0.035, 0.0), (0.035, 0.05), (0.0, 0.05)], segments=10, centre=(x, -0.16, 0.2))
        add_pipe(bm, [(x, -0.16, 0.22), (x + 0.1, -0.26, 0.22)], radius=0.012, segments=6)
    add_pipe(bm, [(0.22, -0.2, 0.3), (0.26, -0.26, 0.14)], radius=0.01, segments=6)
    objects.append(finish("CoffeeTrim", bm))

    # ---- The accountants: the boss's safe and what's in it ----
    bm = bmesh.new()
    add_box(bm, (1.0, 0.8, 1.3), (0.0, 0.0, 0.65), bevel=0.045)
    add_box(bm, (0.84, 0.05, 1.14), (0.0, -0.41, 0.65), bevel=0.02)
    for z in (0.3, 1.0):
        add_lathe(bm, [(0.0, -0.08), (0.03, -0.08), (0.03, 0.08), (0.0, 0.08)], segments=10, centre=(-0.44, -0.43, z))
    objects.append(finish("SafeBody", bm))
    bm = bmesh.new()
    add_torus(bm, (0.08, -0.5, 0.68), 0.15, 0.02, axis_matrix=TO_FRONT, segments=24, ring=8)
    add_lathe(bm, [(0.0, 0.0), (0.045, 0.0), (0.045, 0.07), (0.0, 0.07)], segments=12, centre=(0.08, -0.43, 0.68), axis_matrix=TO_FRONT)
    for k in range(4):
        a = k * math.pi / 2 + math.pi / 4
        add_pipe(bm, [(0.08, -0.5, 0.68), (0.08 + 0.15 * math.cos(a), -0.5, 0.68 + 0.15 * math.sin(a))], radius=0.014, segments=6)
    objects.append(finish("SafeWheel", bm))
    bm = bmesh.new()
    stacks = [(-0.19, 0.0, 3), (0.0, 0.02, 4), (0.19, -0.01, 2), (-0.09, -0.11, 2), (0.1, -0.1, 3)]
    for x, y, count in stacks:
        for k in range(count):
            add_box(bm, (0.16, 0.075, 0.035), (x, y, 0.02 + k * 0.038), bevel=0.006)
    objects.append(finish("MoneyNotes", bm))
    bm = bmesh.new()
    for x, y, count in stacks:
        for k in range(count):
            add_box(bm, (0.04, 0.078, 0.037), (x, y, 0.02 + k * 0.038))
    objects.append(finish("MoneyBands", bm))
    return objects


def build_city():
    """The main menu's night city (the user's brief, 2026-11-08), in the kitchen's chunky toon style.
    Building parts the menu builder stretches to each building: a bevelled block, a cornice for the
    roofline, a window frame with a sill and a shopfront frame (their glass is the builder's glowing
    quads), and a striped awning module. Street furniture at full size: a lantern lamppost, a
    rounded cartoon car with lights, a tree and a roof water tower. Blender +y is the game's -z,
    towards the camera: fronts face +y. Origins at the base."""
    objects = []

    bm = bmesh.new()  # a unit block, stretched to each building
    add_box(bm, (1.0, 1.0, 1.0), (0.0, 0.0, 0.5), bevel=0.012)
    objects.append(finish("CityBlock", bm))

    bm = bmesh.new()  # the roofline's cornice, stretched over the top
    add_box(bm, (1.0, 1.0, 0.35), (0.0, 0.0, 0.175), bevel=0.04)
    add_box(bm, (0.96, 0.96, 0.12), (0.0, 0.0, -0.04), bevel=0.02)
    objects.append(finish("CityCornice", bm))

    bm = bmesh.new()  # a window's frame round a 0.9 x 1.2 m opening, standing out from the wall
    w, h, bar, d = 0.9, 1.2, 0.12, 0.14
    add_box(bm, (w + 2 * bar, d, bar), (0.0, d / 2.0, h / 2.0 + bar / 2.0), bevel=0.02)
    add_box(bm, (w + 2 * bar, d, bar), (0.0, d / 2.0, -h / 2.0 - bar / 2.0), bevel=0.02)
    add_box(bm, (bar, d, h), (w / 2.0 + bar / 2.0, d / 2.0, 0.0), bevel=0.02)
    add_box(bm, (bar, d, h), (-w / 2.0 - bar / 2.0, d / 2.0, 0.0), bevel=0.02)
    add_box(bm, (bar * 0.6, d * 0.6, h), (0.0, d * 0.3, 0.0), bevel=0.01)  # the mullion
    objects.append(finish("CityWindowFrame", bm))
    bm = bmesh.new()
    add_box(bm, (w + 0.4, 0.28, 0.1), (0.0, 0.14, -h / 2.0 - bar - 0.05), bevel=0.025)
    objects.append(finish("CityWindowSill", bm))

    bm = bmesh.new()  # a shopfront's frame round a 2.4 x 1.8 m window, with a stall riser below
    w, h, bar, d = 2.4, 1.8, 0.16, 0.16
    add_box(bm, (w + 2 * bar, d, bar), (0.0, d / 2.0, h / 2.0 + bar / 2.0), bevel=0.025)
    add_box(bm, (w + 2 * bar, d + 0.04, 0.5), (0.0, (d + 0.04) / 2.0, -h / 2.0 - 0.25), bevel=0.04)
    add_box(bm, (bar, d, h), (w / 2.0 + bar / 2.0, d / 2.0, 0.0), bevel=0.025)
    add_box(bm, (bar, d, h), (-w / 2.0 - bar / 2.0, d / 2.0, 0.0), bevel=0.025)
    add_box(bm, (bar * 0.6, d * 0.6, h), (0.0, d * 0.3, 0.0), bevel=0.015)
    objects.append(finish("CityShopFrame", bm))

    # An awning module, 1 m wide, two stripes: sloping out from the wall with a flap at the front.
    profile = [(0.0, 0.0), (0.0, -0.08), (1.04, -0.56), (1.04, -0.8), (1.1, -0.8), (1.1, -0.5)]
    for name, x0, x1 in (("CityAwningA", -0.5, 0.0), ("CityAwningB", 0.0, 0.5)):
        bm = bmesh.new()
        add_extrude_yz(bm, profile, x0, x1)
        objects.append(finish(name, bm))

    # A lantern lamppost, 4.9 m: a stepped base, a slim post, a glowing glass lantern and a cap.
    bm = bmesh.new()
    add_lathe(bm, [(0.0, 0.0), (0.3, 0.0), (0.3, 0.16), (0.2, 0.28), (0.13, 0.55), (0.0, 0.55)], segments=16)
    add_pipe(bm, [(0.0, 0.0, 0.5), (0.0, 0.0, 4.0)], radius=0.08, segments=10)
    add_lathe(bm, [(0.0, 3.92), (0.2, 3.92), (0.2, 4.06), (0.0, 4.06)], segments=16)  # the collar
    add_lathe(bm, [(0.0, 4.52), (0.36, 4.52), (0.3, 4.62), (0.06, 4.86), (0.0, 4.88)], segments=16)  # the cap
    add_ball(bm, (0.0, 0.0, 4.95), 0.08, segments=10)
    objects.append(finish("CityLampPost", bm))
    bm = bmesh.new()
    add_lathe(bm, [(0.0, 4.05), (0.17, 4.05), (0.26, 4.5), (0.0, 4.53)], segments=16)
    objects.append(finish("CityLampGlass", bm))

    # A rounded cartoon car, 4.2 m long, along x: body, cabin, glass, wheels and lights.
    bm = bmesh.new()
    add_box(bm, (4.2, 1.9, 0.78), (0.0, 0.0, 0.66), bevel=0.3)
    add_box(bm, (2.2, 1.66, 0.72), (-0.25, 0.0, 1.28), bevel=0.24)
    objects.append(finish("CityCarBody", bm))
    bm = bmesh.new()
    add_box(bm, (2.26, 1.72, 0.42), (-0.25, 0.0, 1.32), bevel=0.1)
    objects.append(finish("CityCarGlass", bm))
    bm = bmesh.new()
    for x in (-1.35, 1.35):
        add_cylinder_y(bm, (x, -0.99, 0.38), 0.38, 0.26)
        add_cylinder_y(bm, (x, 0.73, 0.38), 0.38, 0.26)
    objects.append(finish("CityCarWheels", bm))
    bm = bmesh.new()
    for y in (-0.6, 0.6):
        add_ball(bm, (2.07, y, 0.72), 0.14, segments=12)
    objects.append(finish("CityCarHeadlights", bm))
    bm = bmesh.new()
    for y in (-0.65, 0.65):
        add_ball(bm, (-2.08, y, 0.74), 0.11, segments=10)
    objects.append(finish("CityCarTaillights", bm))

    # A street tree: a slim trunk and a cloud of round crowns.
    bm = bmesh.new()
    add_lathe(bm, [(0.0, 0.0), (0.24, 0.0), (0.17, 0.3), (0.13, 2.1), (0.0, 2.1)], segments=12)
    objects.append(finish("CityTreeTrunk", bm))
    bm = bmesh.new()
    for centre, radius in (((0.0, 0.0, 2.8), 1.2), ((0.65, 0.3, 3.35), 0.9), ((-0.6, -0.25, 3.3), 0.85), ((0.1, -0.55, 3.75), 0.72)):
        add_round_ball(bm, centre, radius)  # smooth, round crowns
    objects.append(finish("CityTreeCrown", bm))

    # A roof water tower: four legs, a wooden tank with two bands, and a cone roof.
    bm = bmesh.new()
    for sx in (-1.0, 1.0):
        for sy in (-1.0, 1.0):
            add_pipe(bm, [(sx * 0.85, sy * 0.85, 0.0), (sx * 0.7, sy * 0.7, 1.6)], radius=0.07, segments=8)
    add_pipe(bm, [(-0.78, -0.78, 0.8), (0.78, 0.78, 0.8)], radius=0.04, segments=6)
    add_pipe(bm, [(-0.78, 0.78, 0.8), (0.78, -0.78, 0.8)], radius=0.04, segments=6)
    objects.append(finish("CityTowerLegs", bm))
    bm = bmesh.new()
    add_lathe(bm, [(0.0, 1.6), (1.0, 1.6), (1.06, 2.65), (0.0, 2.65)], segments=20)
    objects.append(finish("CityTowerTank", bm))
    bm = bmesh.new()
    add_lathe(bm, [(0.0, 2.6), (1.18, 2.6), (1.12, 2.7), (0.0, 3.4)], segments=20)
    for z in (1.85, 2.4):
        r = 1.0 + 0.06 * (z - 1.6) / 1.05 + 0.025
        add_pipe(bm, [(r * math.cos(2 * math.pi * k / 20), r * math.sin(2 * math.pi * k / 20), z) for k in range(21)],
                 radius=0.03, segments=5)
    objects.append(finish("CityTowerRoof", bm))
    objects += build_plaza()
    return objects


def add_lathe_wedge(bm, profile, a0, a1, steps=3):
    """A closed slice of a lathe between angles a0 and a1 (radians): a parasol's stripe."""
    rings = []
    for k in range(steps + 1):
        a = a0 + (a1 - a0) * k / steps
        rings.append([bm.verts.new((r * math.cos(a), r * math.sin(a), z)) for r, z in profile])
    n = len(profile)
    for ring, nxt in zip(rings, rings[1:]):
        for i in range(n):
            j = (i + 1) % n
            bm.faces.new([ring[i], ring[j], nxt[j], nxt[i]])
    bm.faces.new(rings[0])
    bm.faces.new(list(reversed(rings[-1])))


def build_plaza():
    """The plaza in front of the menu city, the strip below the menu's panels: flagstone paving,
    a bench between flower planters, and cafe tables under striped parasols with candle lanterns
    (a nod to the restaurant game). Fronts face +y, towards the camera; origins at the base."""
    objects = []

    # A 24 x 22 m patch of staggered 1.5 m flagstones, each a touch higher or lower than the next,
    # so the moonlight and the ink pick out the joints. The builder tiles patches side by side.
    bm = bmesh.new()
    rng = random.Random(7)
    stone, gap, width, depth = 1.5, 0.08, 24.0, 22.0
    rows = int(depth / stone)
    for row in range(rows):
        y = -depth / 2.0 + stone * (row + 0.5)
        x = -width / 2.0 - (stone / 2.0 if row % 2 else 0.0)
        while x < width / 2.0:
            x0, x1 = max(x, -width / 2.0), min(x + stone, width / 2.0)
            if x1 - x0 > 0.3:
                h = 0.05 + rng.uniform(-0.012, 0.012)
                add_box(bm, (x1 - x0 - gap, stone - gap, h), ((x0 + x1) / 2.0, y, h / 2.0), bevel=0.02)
            x += stone
    objects.append(finish("CityPaving", bm))

    # A park bench, 1.8 m: wooden slats on cast-iron side frames, facing the camera.
    bm = bmesh.new()
    for y in (0.13, 0.0, -0.13):
        add_box(bm, (1.8, 0.11, 0.05), (0.0, y, 0.46), bevel=0.015)
    for z in (0.66, 0.84):
        add_box(bm, (1.8, 0.05, 0.12), (0.0, -0.25, z), bevel=0.015)
    objects.append(finish("CityBenchWood", bm))
    bm = bmesh.new()
    for x in (-0.72, 0.72):
        add_pipe(bm, [(x, 0.17, 0.0), (x, 0.17, 0.43)], radius=0.035, segments=8)
        add_pipe(bm, [(x, -0.2, 0.0), (x, -0.22, 0.43), (x, -0.28, 0.92)], radius=0.035, segments=8)
        add_pipe(bm, [(x, -0.23, 0.62), (x, 0.2, 0.62), (x, 0.2, 0.47)], radius=0.03, segments=8)
    objects.append(finish("CityBenchIron", bm))

    # A wooden planter box with a round shrub and dots of flowers.
    bm = bmesh.new()
    add_box(bm, (1.2, 0.6, 0.5), (0.0, 0.0, 0.25), bevel=0.04)
    add_box(bm, (1.28, 0.68, 0.07), (0.0, 0.0, 0.5), bevel=0.02)
    objects.append(finish("CityPlanterBox", bm))
    bm = bmesh.new()
    for centre, radius in (((-0.28, 0.0, 0.62), 0.3), ((0.26, 0.02, 0.62), 0.32), ((0.0, -0.04, 0.8), 0.3)):
        add_round_ball(bm, centre, radius, rings=8, segments=16)
    objects.append(finish("CityPlanterShrub", bm))
    bm = bmesh.new()
    for k in range(14):
        a = 2.0 * math.pi * k / 14 + rng.uniform(-0.2, 0.2)
        cx = 0.42 * math.cos(a)
        z = 0.72 + 0.18 * math.sin(3 * a) + rng.uniform(0.0, 0.12)
        add_ball(bm, (cx, 0.27 * math.sin(a) + (0.06 if math.sin(a) > 0 else -0.06), z), 0.07, segments=8)
    objects.append(finish("CityPlanterFlowers", bm))

    # A cafe table: a cast-iron foot and column, a round marble top, and a candle lantern on it.
    bm = bmesh.new()
    add_lathe(bm, [(0.0, 0.0), (0.26, 0.0), (0.26, 0.04), (0.08, 0.1), (0.0, 0.1)], segments=16)
    add_pipe(bm, [(0.0, 0.0, 0.08), (0.0, 0.0, 0.72)], radius=0.05, segments=8)
    objects.append(finish("CityCafeTable", bm))
    bm = bmesh.new()
    add_lathe(bm, [(0.0, 0.71), (0.36, 0.71), (0.38, 0.74), (0.36, 0.77), (0.0, 0.77)], segments=20)
    objects.append(finish("CityCafeTop", bm))
    bm = bmesh.new()
    add_lathe(bm, [(0.0, 0.77), (0.06, 0.77), (0.07, 0.88), (0.0, 0.9)], segments=10)
    objects.append(finish("CityCafeCandle", bm))

    # A bistro chair, seat facing +y: four legs, a round seat and a hoop back.
    bm = bmesh.new()
    for sx in (-1.0, 1.0):
        for sy in (-1.0, 1.0):
            add_pipe(bm, [(sx * 0.17, sy * 0.17, 0.0), (sx * 0.15, sy * 0.15, 0.44)], radius=0.035, segments=8)
    hoop = [(-0.17, -0.19, 0.46)] + [(-0.17 * math.cos(math.pi * k / 8), -0.2, 0.66 + 0.2 * math.sin(math.pi * k / 8)) for k in range(9)] + [(0.17, -0.19, 0.46)]
    add_pipe(bm, hoop, radius=0.04, segments=8)
    add_lathe(bm, [(0.0, 0.42), (0.23, 0.42), (0.25, 0.48), (0.0, 0.5)], segments=16)
    objects.append(finish("CityCafeChair", bm))

    # A parasol: a pole through the table and a canopy of eight stripes in two meshes, so each
    # takes its own colour, sloping out with a short flap at the rim.
    bm = bmesh.new()
    add_pipe(bm, [(0.0, 0.0, 0.1), (0.0, 0.0, 2.55)], radius=0.025, segments=8)
    objects.append(finish("CityParasolPole", bm))
    canopy = [(0.001, 2.5), (1.3, 2.02), (1.3, 1.9), (1.24, 1.92), (0.001, 2.42)]
    for name, first in (("CityParasolA", 0), ("CityParasolB", 1)):
        bm = bmesh.new()
        for k in range(first, 8, 2):
            add_lathe_wedge(bm, canopy, 2.0 * math.pi * k / 8, 2.0 * math.pi * (k + 1) / 8)
        objects.append(finish(name, bm))
    return objects


def build_office():
    """The boss's floor's furniture (docs/office-scene.md), in the kitchen's chunky, bevelled style.
    Everything that the kitchen already has (counters, hob, fridge, shelf, tables, chairs, stools,
    pictures) and the city has (trees, lamps, towers) is reused from those models instead. Fronts
    face -Y here, the game's forward, like the chef: the builder turns each piece to face the room."""
    objects = []

    # A bevelled 1 m block, centred on its base: the builder scales it for plinths, lecterns, desks
    # and frames, so even simple shapes catch the light on their edges like the modelled props.
    bm = bmesh.new()
    add_box(bm, (1.0, 1.0, 1.0), (0.0, 0.0, 0.5), bevel=0.04)
    objects.append(finish("OfficeBlock", bm))

    # Sofa: 2.1 m, low and deep, with rolled arms; frame and cushions apart for two colours.
    bm = bmesh.new()
    add_box(bm, (2.1, 0.9, 0.3), (0.0, 0.0, 0.2), bevel=0.05)          # base
    add_box(bm, (2.1, 0.24, 0.5), (0.0, 0.33, 0.55), bevel=0.08)       # back, behind (+Y)
    for x in (-0.95, 0.95):
        add_box(bm, (0.22, 0.9, 0.32), (x, 0.0, 0.5), bevel=0.09)      # arms
    for x in (-0.9, 0.9):
        for y in (-0.36, 0.36):
            add_box(bm, (0.06, 0.06, 0.06), (x, y, 0.03), bevel=0.015)  # feet
    objects.append(finish("OfficeSofaFrame", bm))
    bm = bmesh.new()
    for x in (-0.42, 0.42):
        add_box(bm, (0.82, 0.62, 0.14), (x, -0.06, 0.42), bevel=0.05)   # seat cushions
        add_box(bm, (0.8, 0.16, 0.42), (x, 0.17, 0.66), bevel=0.06)     # back cushions
    objects.append(finish("OfficeSofaCushions", bm))

    # Armchair: the sofa's single seat.
    bm = bmesh.new()
    add_box(bm, (0.95, 0.88, 0.3), (0.0, 0.0, 0.2), bevel=0.05)
    add_box(bm, (0.95, 0.22, 0.52), (0.0, 0.33, 0.56), bevel=0.08)
    for x in (-0.38, 0.38):
        add_box(bm, (0.2, 0.88, 0.32), (x, 0.0, 0.5), bevel=0.08)
    objects.append(finish("OfficeArmchairFrame", bm))
    bm = bmesh.new()
    add_box(bm, (0.56, 0.62, 0.14), (0.0, -0.06, 0.42), bevel=0.05)
    objects.append(finish("OfficeArmchairCushion", bm))

    # Executive desk: two drawer pedestals under a thick top with a leather writing inset.
    bm = bmesh.new()
    add_box(bm, (2.4, 1.05, 0.07), (0.0, 0.0, 0.725), bevel=0.025)     # top
    for x in (-0.82, 0.82):
        add_box(bm, (0.68, 0.95, 0.69), (x, 0.0, 0.345), bevel=0.02)  # pedestals
        for z in (0.17, 0.4, 0.6):
            add_box(bm, (0.58, 0.02, 0.17), (x, 0.48, z), bevel=0.008)  # drawer fronts, chair side (+Y)
    add_box(bm, (0.98, 0.04, 0.5), (0.0, -0.44, 0.44), bevel=0.015)    # modesty panel at the front
    objects.append(finish("OfficeDeskBody", bm))
    bm = bmesh.new()
    add_box(bm, (1.5, 0.66, 0.012), (0.0, 0.03, 0.765), bevel=0.004)
    objects.append(finish("OfficeDeskLeather", bm))
    bm = bmesh.new()
    for x in (-0.82, 0.82):
        for z in (0.17, 0.4, 0.6):
            add_box(bm, (0.16, 0.03, 0.03), (x, 0.505, z), bevel=0.01)
    objects.append(finish("OfficeDeskHandles", bm))

    # Office chair: a high, buttoned leather back on a five-star base.
    bm = bmesh.new()
    add_box(bm, (0.62, 0.6, 0.12), (0.0, 0.0, 0.48), bevel=0.05)       # seat
    add_box(bm, (0.64, 0.16, 0.9), (0.0, 0.3, 0.98), bevel=0.07)       # high back
    for x in (-0.33, 0.33):
        add_box(bm, (0.08, 0.46, 0.08), (x, 0.0, 0.66), bevel=0.03)    # arm pads
    objects.append(finish("OfficeChairSeat", bm))
    bm = bmesh.new()
    add_pipe(bm, [(0.0, 0.0, 0.08), (0.0, 0.0, 0.43)], radius=0.035, segments=12)
    for k in range(5):
        a = math.pi / 2 + k * 2 * math.pi / 5
        add_pipe(bm, [(0.0, 0.0, 0.08), (0.3 * math.cos(a), 0.3 * math.sin(a), 0.05)], radius=0.025, segments=8)
        add_ball(bm, (0.3 * math.cos(a), 0.3 * math.sin(a), 0.03), 0.03, segments=8)
    for x in (-0.33, 0.33):
        add_pipe(bm, [(x, 0.0, 0.5), (x, 0.0, 0.62)], radius=0.02, segments=8)
    objects.append(finish("OfficeChairBase", bm))

    # Filing cabinet: four drawers with brass handles and label plates.
    bm = bmesh.new()
    add_box(bm, (0.55, 0.62, 1.32), (0.0, 0.0, 0.66), bevel=0.02)
    for z in (0.18, 0.5, 0.82, 1.14):
        add_box(bm, (0.49, 0.02, 0.28), (0.0, -0.31, z), bevel=0.008)
    objects.append(finish("OfficeFilingBody", bm))
    bm = bmesh.new()
    for z in (0.18, 0.5, 0.82, 1.14):
        add_box(bm, (0.18, 0.04, 0.035), (0.0, -0.33, z - 0.04), bevel=0.012)
        add_box(bm, (0.12, 0.012, 0.05), (0.0, -0.322, z + 0.06), bevel=0.003)
    objects.append(finish("OfficeFilingHandles", bm))

    # Potted plant: a tapered pot and a bushy crown of round clumps.
    bm = bmesh.new()
    add_lathe(bm, [(0.0, 0.0), (0.17, 0.0), (0.24, 0.42), (0.27, 0.44), (0.27, 0.5), (0.22, 0.5), (0.22, 0.46), (0.0, 0.46)], segments=24)
    objects.append(finish("OfficePlantPot", bm))
    bm = bmesh.new()
    rng = random.Random(9)
    for k in range(7):
        a = k * 2 * math.pi / 7 + rng.uniform(-0.2, 0.2)
        r = 0.16 if k else 0.0
        add_round_ball(bm, (r * math.cos(a), r * math.sin(a), 0.82 + rng.uniform(-0.08, 0.14)), rng.uniform(0.2, 0.26), rings=8, segments=14)
    add_round_ball(bm, (0.0, 0.0, 1.05), 0.22, rings=8, segments=14)
    objects.append(finish("OfficePlantLeaves", bm))

    # Floor lamp: a brass stem with a drum shade.
    bm = bmesh.new()
    add_lathe(bm, [(0.0, 0.0), (0.18, 0.0), (0.18, 0.03), (0.04, 0.05), (0.0, 0.05)], segments=24)
    add_pipe(bm, [(0.0, 0.0, 0.05), (0.0, 0.0, 1.45)], radius=0.018, segments=10)
    objects.append(finish("OfficeLampStand", bm))
    bm = bmesh.new()
    add_lathe(bm, [(0.0, 1.32), (0.2, 1.32), (0.15, 1.62), (0.0, 1.62)], segments=24)
    objects.append(finish("OfficeLampShade", bm))

    # Trophy: a cup on a stepped base.
    bm = bmesh.new()
    add_lathe(bm, [(0.0, 0.0), (0.11, 0.0), (0.11, 0.05), (0.08, 0.05), (0.08, 0.09), (0.025, 0.11), (0.025, 0.2),
                   (0.06, 0.22), (0.11, 0.3), (0.12, 0.4), (0.105, 0.4), (0.0, 0.33)], segments=24)
    for side in (-1.0, 1.0):
        add_pipe(bm, [(side * 0.11, 0.0, 0.37), (side * 0.17, 0.0, 0.34), (side * 0.15, 0.0, 0.26), (side * 0.09, 0.0, 0.25)], radius=0.012, segments=6)
    objects.append(finish("OfficeTrophy", bm))

    # The boss's cowboy hat, sized for the chef's head (its origin is the chef's waist, like the toque).
    bm = bmesh.new()
    add_lathe(bm, [(0.0, 0.8), (0.4, 0.8), (0.42, 0.83), (0.4, 0.84), (0.2, 0.83), (0.19, 0.83), (0.0, 0.83)], segments=32)
    objects.append(finish("OfficeHatBrim", bm))
    bm = bmesh.new()
    add_lathe(bm, [(0.0, 0.82), (0.2, 0.82), (0.2, 0.9), (0.185, 1.0), (0.15, 1.04), (0.06, 1.0), (0.0, 1.01)], segments=32)
    objects.append(finish("OfficeHatCrown", bm))
    bm = bmesh.new()
    add_lathe(bm, [(0.0, 0.84), (0.205, 0.84), (0.205, 0.88), (0.0, 0.88)], segments=32)
    objects.append(finish("OfficeHatBand", bm))
    for obj in objects[-3:]:
        obj.location = (0.0, 0.0, CHEF_WAIST)

    # Tailor's plinth: a low round platform with a brass rim, low enough to step onto.
    bm = bmesh.new()
    add_lathe(bm, [(0.0, 0.0), (0.62, 0.0), (0.62, 0.06), (0.58, 0.1), (0.0, 0.1)], segments=40)
    objects.append(finish("OfficePlinthTop", bm))
    bm = bmesh.new()
    add_lathe(bm, [(0.0, 0.0), (0.66, 0.0), (0.66, 0.04), (0.0, 0.04)], segments=40)
    objects.append(finish("OfficePlinthRim", bm))

    # Clothes rail: two uprights on feet and a bar.
    bm = bmesh.new()
    for x in (-0.8, 0.8):
        add_pipe(bm, [(x, 0.0, 0.0), (x, 0.0, 1.6)], radius=0.025, segments=10)
        add_pipe(bm, [(x, -0.25, 0.02), (x, 0.25, 0.02)], radius=0.025, segments=10)
    add_pipe(bm, [(-0.8, 0.0, 1.6), (0.8, 0.0, 1.6)], radius=0.022, segments=10)
    objects.append(finish("OfficeRail", bm))
    bm = bmesh.new()  # a jacket on a hanger, its origin at the rail
    add_box(bm, (0.48, 0.14, 0.72), (0.0, 0.0, -0.5), bevel=0.05)
    add_pipe(bm, [(-0.22, 0.0, -0.13), (0.0, 0.0, -0.05), (0.22, 0.0, -0.13)], radius=0.015, segments=6)
    objects.append(finish("OfficeJacket", bm))

    # Patio umbrella and a sun lounger for the terrace.
    bm = bmesh.new()
    add_pipe(bm, [(0.0, 0.0, 0.0), (0.0, 0.0, 2.3)], radius=0.03, segments=10)
    add_lathe(bm, [(0.0, 0.0), (0.3, 0.0), (0.3, 0.06), (0.0, 0.06)], segments=20)
    objects.append(finish("OfficeUmbrellaPole", bm))
    bm = bmesh.new()
    add_lathe(bm, [(0.0, 2.1), (1.35, 2.1), (1.4, 2.06), (1.3, 2.13), (0.06, 2.42), (0.0, 2.44)], segments=8)
    objects.append(finish("OfficeUmbrellaCanopy", bm))
    bm = bmesh.new()
    add_box(bm, (0.72, 1.9, 0.08), (0.0, 0.0, 0.3), bevel=0.02)
    for x in (-0.3, 0.3):
        for y in (-0.8, 0.8):
            add_box(bm, (0.05, 0.05, 0.28), (x, y, 0.14), bevel=0.01)
    objects.append(finish("OfficeLoungerFrame", bm))
    bm = bmesh.new()
    add_box(bm, (0.66, 1.3, 0.1), (0.0, -0.25, 0.39), bevel=0.04)
    back = bmesh.new()
    add_box(back, (0.66, 0.1, 0.7), (0.0, 0.0, 0.35), bevel=0.04)
    bmesh.ops.rotate(back, verts=back.verts, cent=(0.0, 0.0, 0.0), matrix=Matrix.Rotation(math.radians(-35.0), 3, 'X'))
    bmesh.ops.translate(back, verts=back.verts, vec=(0.0, 0.48, 0.36))
    tmp = bpy.data.meshes.new("tmp")
    back.to_mesh(tmp)
    back.free()
    bm.from_mesh(tmp)
    bpy.data.meshes.remove(tmp)
    objects.append(finish("OfficeLoungerCushion", bm))

    # The boss's helicopter: a rounded cabin, a tail boom with a fin, skids and a tied-down rotor.
    # Its nose is -Y.
    bm = bmesh.new()
    cabin = [(0.0, -1.3), (0.45, -1.15), (0.75, -0.7), (0.82, 0.0), (0.7, 0.6), (0.4, 1.0), (0.0, 1.1)]
    add_lathe(bm, cabin, segments=24, centre=(0.0, 0.0, 1.0), axis_matrix=Matrix.Rotation(math.radians(-90.0), 4, 'X'))
    add_lathe(bm, [(0.16, 0.0), (0.1, 2.4), (0.0, 2.45)], segments=12, centre=(0.0, 0.9, 1.15),
              axis_matrix=Matrix.Rotation(math.radians(-90.0), 4, 'X'))
    add_box(bm, (0.06, 0.5, 0.7), (0.0, 3.25, 1.4), bevel=0.02)
    add_lathe(bm, [(0.0, 0.0), (0.16, 0.0), (0.12, 0.22), (0.0, 0.24)], segments=16, centre=(0.0, 0.0, 1.78))
    objects.append(finish("OfficeHeliBody", bm))
    bm = bmesh.new()
    glass = [(0.0, -1.32), (0.43, -1.16), (0.66, -0.75), (0.7, -0.35), (0.0, -0.3)]
    add_lathe(bm, glass, segments=24, centre=(0.0, 0.0, 1.12), axis_matrix=Matrix.Rotation(math.radians(-90.0), 4, 'X'))
    objects.append(finish("OfficeHeliGlass", bm))
    bm = bmesh.new()
    for x in (-0.6, 0.6):
        add_pipe(bm, [(x, -1.1, 0.12), (x, -0.95, 0.04), (x, 1.0, 0.04), (x, 1.15, 0.1)], radius=0.035, segments=8)
        for y in (-0.5, 0.5):
            add_pipe(bm, [(x, y, 0.04), (x * 0.6, y, 0.4)], radius=0.03, segments=8)
    objects.append(finish("OfficeHeliSkids", bm))
    bm = bmesh.new()  # the rotor, two crossed blades; the builder turns it so it doesn't line up with the body
    add_box(bm, (5.2, 0.2, 0.04), (0.0, 0.0, 2.02), bevel=0.015)
    add_box(bm, (0.2, 5.2, 0.04), (0.0, 0.0, 2.06), bevel=0.015)
    objects.append(finish("OfficeHeliRotor", bm))

    # A bird for the city's life (user's note, 2026-11-09): a plump pigeon, its origin at its
    # feet, beak towards -Y (the game's forward). The builder makes gulls of it, bigger and white.
    bm = bmesh.new()
    add_lathe(bm, [(0.0, -0.13), (0.06, -0.1), (0.085, 0.0), (0.07, 0.1), (0.035, 0.17), (0.0, 0.2)], segments=16,
              centre=(0.0, 0.0, 0.13), axis_matrix=Matrix.Rotation(math.radians(-90.0), 4, 'X'))
    add_ball(bm, (0.0, -0.13, 0.21), 0.05, segments=12)  # head
    for x in (-0.03, 0.03):
        add_pipe(bm, [(x, 0.0, 0.0), (x, 0.0, 0.07)], radius=0.008, segments=6)  # legs
    objects.append(finish("OfficeBirdBody", bm))
    bm = bmesh.new()
    add_prism(bm, [(-0.012, -0.17), (0.012, -0.17), (0.0, -0.215)], 0.195, 0.215)  # beak
    objects.append(finish("OfficeBirdBeak", bm))
    bm = bmesh.new()  # one wing, from its shoulder at the origin out along +X; the builder mirrors it
    add_prism(bm, [(0.0, -0.06), (0.24, -0.02), (0.30, 0.03), (0.0, 0.07)], -0.008, 0.008)
    objects.append(finish("OfficeBirdWing", bm))

    # Three fan blades for the roof air conditioners, which spin.
    bm = bmesh.new()
    for k in range(3):
        a = k * 2 * math.pi / 3
        ca, sa = math.cos(a), math.sin(a)
        blade = [(0.05, -0.05), (0.27, -0.07), (0.28, 0.06), (0.05, 0.04)]
        add_prism(bm, [(x * ca - y * sa, x * sa + y * ca) for x, y in blade], 0.0, 0.02)
    add_lathe(bm, [(0.0, 0.0), (0.07, 0.0), (0.07, 0.04), (0.0, 0.05)], segments=12)
    objects.append(finish("OfficeFanBlades", bm))

    # A windsock for the neighbour's heliport: a striped cone on a hoop, along +X from its mast.
    bm = bmesh.new()
    add_lathe(bm, [(0.0, 0.0), (0.16, 0.0), (0.16, 0.02), (0.07, 0.9), (0.0, 0.9)], segments=12,
              axis_matrix=Matrix.Rotation(math.radians(90.0), 4, 'Y'))
    objects.append(finish("OfficeWindsock", bm))

    for i, obj in enumerate(objects):
        obj.location = (obj.location.x + 2.6 * i, obj.location.y, obj.location.z)  # apart, for previews only
    return objects


def build_board():
    """A big, thick cutting board using most of its counter (the user's notes, 2026-11-08): a plain
    slab with rounded edges and a low, narrow lip round its top, a finished edge without the deep
    rim and inset panel that read as a picture frame. Its top inside the lip is the cutting
    surface. The wood's colour is its material's (Game/Toon), without a grain."""
    width, depth, height = 0.88, 0.75, 0.07
    lip, lip_height = 0.03, 0.008
    bm = bmesh.new()
    add_box(bm, (width, depth, height), (0.0, 0.0, height / 2.0), bevel=0.025)
    z = height + lip_height / 2.0 - 0.002  # sunk a little into the slab, so it joins without a seam
    inset = 0.012
    add_box(bm, (width - 2 * inset, lip, lip_height + 0.004), (0.0, depth / 2.0 - inset - lip / 2.0, z), bevel=0.004)
    add_box(bm, (width - 2 * inset, lip, lip_height + 0.004), (0.0, -depth / 2.0 + inset + lip / 2.0, z), bevel=0.004)
    add_box(bm, (lip, depth - 2 * inset, lip_height + 0.004), (width / 2.0 - inset - lip / 2.0, 0.0, z), bevel=0.004)
    add_box(bm, (lip, depth - 2 * inset, lip_height + 0.004), (-width / 2.0 + inset + lip / 2.0, 0.0, z), bevel=0.004)
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
        "SM_Characters.fbx": build_characters(),
        "SM_Plate.fbx": build_plate(),
        "SM_PlateRack.fbx": build_plate_racks(),
        "SM_Sink.fbx": build_sink(),
        "SM_Board.fbx": build_board(),
        "SM_Cabinet.fbx": build_cabinet(),
        "SM_Bin.fbx": build_bin(),
        "SM_Dining.fbx": build_dining(),
        "SM_Knife.fbx": build_knife(),
        "SM_KnifeBlock.fbx": build_knife_block(),
        "SM_Onion.fbx": build_onion(),
        "SM_DicedOnion.fbx": build_diced_onion(),
        "SM_OnionRings.fbx": build_onion_rings(),
        "SM_OnionCut.fbx": build_onion_cut(),
        "SM_Tray.fbx": build_tray(),
        "SM_Container.fbx": build_container(),
        "SM_TrayRack.fbx": build_tray_rack(),
        "SM_City.fbx": build_city(),
        "SM_Cars.fbx": build_cars(),
        "SM_StreetKit.fbx": build_street_kit(),
        "SM_WheelieBin.fbx": build_wheelie_bin(),
        "SM_FloorKit.fbx": build_floor_kit(),
        "SM_Office.fbx": build_office(),
    }
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    # `--only SM_Office.fbx` exports just that model, so the others' files aren't rewritten.
    only = args[args.index("--only") + 1] if "--only" in args else None
    for filename, objects in models.items():
        if only is not None and filename != only:
            continue
        export_fbx(objects, filename)
        print(f"Exported {filename}")
    if only is not None:
        models = {only: models[only]}

    if "--preview" in args:
        render_previews(models, args[args.index("--preview") + 1])


if __name__ == "__main__":
    main()
