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
