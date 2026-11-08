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
    """A stainless range (decision 2026-11-08): a rounded body under a thick dark top, with one
    big heat knob on the top. Parts are separate meshes so each gets its own flat colour."""
    bm = bmesh.new()
    plinth = 0.08
    body_top = HOB_HEIGHT - 0.08
    add_box(bm, (0.96, 0.92, body_top - plinth), (0.0, -0.04, plinth + (body_top - plinth) / 2.0), bevel=0.08)
    add_box(bm, (0.8, 0.76, plinth), (0.0, -0.04, plinth / 2.0), bevel=0.03)
    body = finish("HobBody", bm)

    bm = bmesh.new()
    add_box(bm, (1.0, 1.0, 0.1), (0.0, 0.0, HOB_HEIGHT - 0.05), bevel=0.045)
    top = finish("HobTop", bm)

    # One big heat knob, set on the top by the game (StyleTest.KnobPosition) at the front corner,
    # where the chef reaching for it would find it. Built round its own origin so it turns in place
    # with the heat; its grip points at the camera (+Y) when off.
    bm = bmesh.new()
    add_lathe(bm, [(0.075, 0.0), (0.075, 0.03), (0.06, 0.045), (0.0, 0.045)], segments=24)
    add_box(bm, (0.032, 0.13, 0.035), (0.0, 0.0, 0.06), bevel=0.012)  # the grip, across the knob
    add_box(bm, (0.02, 0.02, 0.012), (0.0, 0.055, 0.08))  # a pointer at the grip's front end
    dial = finish("HobDial", bm)

    # The burner glows as one piece (HobView tints it): a fat rounded ring. Its surface stays below
    # the pan's resting height of 0.91, so the pan never sinks into it.
    bm = bmesh.new()
    add_lathe(bm, [
        (0.34, HOB_HEIGHT), (0.345, HOB_HEIGHT + 0.004), (0.335, HOB_HEIGHT + 0.009), (0.3, HOB_HEIGHT + 0.0095),
        (0.235, HOB_HEIGHT + 0.009), (0.22, HOB_HEIGHT + 0.005), (0.2, HOB_HEIGHT + 0.003), (0.0, HOB_HEIGHT + 0.003),
    ])
    burner = finish("HobBurner", bm)
    return [body, top, dial, burner]
def build_pan():
    """Style test: mostly rim and handle. A shallow pan with a fat rolled rim and a short, stubby
    handle with a big ball on the end, towards the camera (+Y here)."""
    bm = bmesh.new()
    add_lathe(bm, [
        (0.0, 0.0), (0.18, 0.0), (0.2, 0.014), (0.206, 0.05), (0.22, 0.072), (0.214, 0.09),
        (0.196, 0.094), (0.184, 0.08), (0.18, PAN_FLOOR + 0.01), (0.17, PAN_FLOOR), (0.0, PAN_FLOOR),
    ])
    body = finish("PanBody", bm)

    bm = bmesh.new()
    add_pipe(bm, [(0.0, 0.215, 0.082), (0.0, 0.3, 0.088)], radius=0.032)  # from the rim, not inside the pan
    add_lathe(bm, [(0.0, -0.055), (0.04, -0.045), (0.055, 0.0), (0.04, 0.045), (0.0, 0.055)], segments=20,
              centre=(0.0, 0.34, 0.09))  # the ball
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
def build_chef():
    """A placeholder chef in two parts, so it can bend at the waist: a bean, wide at the bottom with
    a round top, and a puffy white toque so a chef reads as a chef. Not final character art."""
    bm = bmesh.new()
    add_lathe(bm, [(0.0, 0.0), (0.3, 0.0), (0.36, 0.04), (0.38, 0.15), (0.37, 0.7),
                   (0.32, 0.88), (0.18, 0.95), (0.0, 0.96)], segments=32)
    legs = finish("ChefLegs", bm)

    # Its origin is the waist; it starts a little below, so no gap opens when it bends.
    bm = bmesh.new()
    add_lathe(bm, [(0.0, -0.05), (0.27, -0.05), (0.35, 0.0), (0.36, 0.1), (0.34, 0.5),
                   (0.3, 0.66), (0.22, 0.77), (0.12, 0.83), (0.0, 0.85)], segments=32)
    torso = finish("ChefTorso", bm)
    torso.location = (0.0, 0.0, CHEF_WAIST)

    # The toque: a band and a puffed top, sitting on the torso's crown. Origin at the waist too.
    bm = bmesh.new()
    add_lathe(bm, [(0.0, 0.76), (0.16, 0.76), (0.17, 0.86), (0.2, 0.9), (0.24, 0.98), (0.24, 1.05),
                   (0.2, 1.1), (0.1, 1.13), (0.0, 1.135)], segments=32)
    hat = finish("ChefHat", bm)
    hat.location = (0.0, 0.0, CHEF_WAIST)
    return [legs, torso, hat]
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
    """A chunky stainless sink unit with one big basin and no room for plates: it only washes
    (decision 2026-11-08). A thick rounded top, an oversized swan-neck tap at the back, and open
    legs with a slatted bottom shelf."""
    bm = bmesh.new()
    top, thickness = COUNTER_HEIGHT, 0.06
    z = top - thickness / 2.0
    hole_x, hole_y = 0.38, 0.32  # half the basin's opening: most of the counter, like the references
    # The top around the basin's opening.
    add_box(bm, (0.5 - hole_x, 1.0, thickness), ((0.5 + hole_x) / 2.0, 0.0, z), bevel=0.02)
    add_box(bm, (0.5 - hole_x, 1.0, thickness), (-(0.5 + hole_x) / 2.0, 0.0, z), bevel=0.02)
    add_box(bm, (2 * hole_x, 0.5 - hole_y, thickness), (0.0, (0.5 + hole_y) / 2.0, z), bevel=0.02)
    add_box(bm, (2 * hole_x, 0.5 - hole_y, thickness), (0.0, -(0.5 + hole_y) / 2.0, z), bevel=0.02)
    add_box(bm, (1.0, 0.05, 0.14), (0.0, -0.475, top + 0.07), bevel=0.022)  # upstand
    # The basin, 0.15 deep: the plate being washed sits on its floor at 0.75.
    add_box(bm, (2 * hole_x, 2 * hole_y, 0.02), (0.0, 0.0, top - 0.16), bevel=0.008)
    for y in (-hole_y, hole_y):
        add_box(bm, (2 * hole_x, 0.02, 0.16), (0.0, y, top - 0.08))
    for x in (-hole_x, hole_x):
        add_box(bm, (0.02, 2 * hole_y, 0.16), (x, 0.0, top - 0.08))
    add_lathe(bm, [(0.03, 0.0), (0.03, 0.2), (0.0, 0.2)], segments=12, centre=(0.0, 0.0, top - 0.36))  # drain
    # An oversized gooseneck tap: it rises from the middle of the back edge and arches forward over
    # the basin, so water always comes from the back of the sink, whichever way it's turned.
    add_lathe(bm, [(0.06, 0.0), (0.06, 0.03), (0.04, 0.05), (0.0, 0.05)], segments=20, centre=(0.0, -0.4, top))
    arch_centre, arch_radius = Vector((0.0, -0.28, top + 0.3)), 0.12
    neck = [(0.0, -0.4, top + 0.04)]
    for i in range(9):
        angle = math.pi * i / 8  # from the back, over the top, to the front
        neck.append((0.0, arch_centre.y - arch_radius * math.cos(angle), arch_centre.z + arch_radius * math.sin(angle)))
    neck.append((0.0, -0.16, top + 0.2))
    add_pipe(bm, neck, radius=0.032)
    add_lathe(bm, [(0.042, 0.0), (0.042, 0.03), (0.0, 0.03)], segments=16, centre=(0.0, -0.16, top + 0.18))  # nozzle
    add_pipe(bm, [(0.0, -0.4, top + 0.12), (0.0, -0.47, top + 0.2)], radius=0.018)  # a small lever, at the back
    # Fat legs and a slatted bottom shelf.
    for x in (-0.46, 0.46):
        for y in (-0.46, 0.46):
            add_box(bm, (0.07, 0.07, top - thickness), (x, y, (top - thickness) / 2.0), bevel=0.02)
    for y in (-0.46, 0.46):
        add_box(bm, (0.92, 0.04, 0.04), (0.0, y, 0.14), bevel=0.012)
    for x in (-0.46, 0.46):
        add_box(bm, (0.04, 0.92, 0.04), (x, 0.0, 0.14), bevel=0.012)
    for y in (-0.3, -0.1, 0.1, 0.3):
        add_box(bm, (0.92, 0.07, 0.03), (0.0, y, 0.15), bevel=0.01)
    return [finish("SinkBody", bm)]
def build_cabinet():
    """The counter every plain station shares (decision 2026-11-08, the art overhaul): a wooden
    cabinet with a light wood top, a recessed dark base, and a drawer over two doors on both working
    sides (+Y towards the camera, and -Y), plain panelled ends. Parts are separate meshes so each
    gets its own colour; all stay inside the 1 m footprint, the top at counter height."""
    top_z, slab = COUNTER_HEIGHT, 0.05
    kick = 0.09
    body_top = top_z - slab

    bm = bmesh.new()  # the carcass, set back a little from the fronts
    add_box(bm, (0.98, 0.94, body_top - kick), (0.0, 0.0, kick + (body_top - kick) / 2.0), bevel=0.012)
    body = finish("CabinetBody", bm)

    bm = bmesh.new()  # the recessed base
    add_box(bm, (0.92, 0.88, kick), (0.0, 0.0, kick / 2.0))
    base = finish("CabinetKick", bm)

    bm = bmesh.new()  # the top: a slab with rounded edges, flush with the footprint
    add_box(bm, (1.0, 1.0, slab), (0.0, 0.0, top_z - slab / 2.0), bevel=0.014)
    worktop = finish("CabinetTop", bm)

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
    return [body, base, worktop, fronts, handles]


def build_board():
    """A big, thick wooden cutting board using most of its counter, with fat rounded corners and a
    deep juice groove. The wood grain is drawn by its material (Game/Toon)."""
    width, depth, base, rise = 0.7, 0.55, 0.03, 0.01
    border, groove = 0.045, 0.018
    bm = bmesh.new()
    add_box(bm, (width, depth, base), (0.0, 0.0, base / 2.0), bevel=0.03)
    top = base + rise / 2.0
    add_box(bm, (width - 0.03, border, rise), (0.0, depth / 2.0 - border / 2.0 - 0.015, top), bevel=0.005)
    add_box(bm, (width - 0.03, border, rise), (0.0, -depth / 2.0 + border / 2.0 + 0.015, top), bevel=0.005)
    add_box(bm, (border, depth - 0.03, rise), (width / 2.0 - border / 2.0 - 0.015, 0.0, top), bevel=0.005)
    add_box(bm, (border, depth - 0.03, rise), (-width / 2.0 + border / 2.0 + 0.015, 0.0, top), bevel=0.005)
    inset = border + 0.015 + groove
    add_box(bm, (width - 2 * inset, depth - 2 * inset, rise), (0.0, 0.0, top), bevel=0.005)
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
    }
    for filename, objects in models.items():
        export_fbx(objects, filename)
        print(f"Exported {filename}")

    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if "--preview" in args:
        render_previews(models, args[args.index("--preview") + 1])


if __name__ == "__main__":
    main()
