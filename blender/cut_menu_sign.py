"""Cuts the main menu's logo and the like pill's heart out of the Art Director's sign picture
(TEX_Menu_Sign.png, v11 mockup), so the game can draw the sign's board itself and animate what
sits on it (the user's note, 2026-11-10: no polaroid or review stuck to the sign, the REC dot
blinking, the like count changing).

Run: blender -b -P blender/cut_menu_sign.py

Each cut-out is keyed against the flat colour behind it ("colour to alpha"), so its soft edges sit
on any background. Blender is used only for its Python with numpy and its PNG reading and writing.
"""
import os

import bpy
import numpy as np

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
FOLDER = os.path.join(REPO_ROOT, "Assets", "_Project", "Art", "UI", "Menu")
BOARD = np.array([43, 33, 27]) / 255.0   # the board's fill
PILL = np.array([232, 71, 106]) / 255.0  # the like pill's pink


def load(name):
    image = bpy.data.images.load(os.path.join(FOLDER, name))
    image.colorspace_settings.name = 'Non-Color'
    width, height = image.size
    pixels = np.empty(width * height * 4, dtype=np.float32)
    image.pixels.foreach_get(pixels)
    return pixels.reshape(height, width, 4)[::-1].copy()  # row 0 at the top


def save(pixels, name):
    height, width, _ = pixels.shape
    image = bpy.data.images.new(name, width, height, alpha=True)
    image.colorspace_settings.name = 'Non-Color'
    image.alpha_mode = 'STRAIGHT'
    image.pixels.foreach_set(pixels[::-1].astype(np.float32).ravel())
    image.filepath_raw = os.path.join(FOLDER, name)
    image.file_format = 'PNG'
    image.save()
    print("Wrote", name, width, "x", height)


def key_out(pixels, background):
    """Colour to alpha: the least opaque colour that, laid over `background`, gives each pixel."""
    rgb = pixels[..., :3]
    lighter = np.where(rgb > background, (rgb - background) / np.maximum(1.0 - background, 1e-6), 0.0)
    darker = np.where(rgb < background, (background - rgb) / np.maximum(background, 1e-6), 0.0)
    alpha = np.clip(np.maximum(lighter, darker).max(axis=-1), 0.0, 1.0)
    alpha[alpha < 0.02] = 0.0
    colour = np.where(alpha[..., None] > 0.0, (rgb - background) / np.maximum(alpha[..., None], 1e-6) + background, 0.0)
    return np.dstack([np.clip(colour, 0.0, 1.0), alpha * pixels[..., 3]])


def crop_to_content(pixels):
    rows = np.where(pixels[..., 3].max(axis=1) > 0.0)[0]
    columns = np.where(pixels[..., 3].max(axis=0) > 0.0)[0]
    return pixels[rows.min():rows.max() + 1, columns.min():columns.max() + 1], (columns.min(), rows.min())


sign = load("TEX_Menu_Sign.png")

# The logo: "Mise en", FLEX and its crossed spoon and knife, inside the board, clear of the polaroid
# (left), the REC light (top right) and the review card (bottom right).
x0, y0, x1, y1 = 356, 232, 1300, 626
logo = key_out(sign[y0:y1, x0:x1], BOARD)
logo[: 300 - y0, 1180 - x0:, 3] = 0.0  # REC and its dot
logo[616 - y0:, 1100 - x0:, 3] = 0.0  # the review card's top edge, under the knife's handle
logo, offset = crop_to_content(logo)
print("Logo at", x0 + offset[0], y0 + offset[1])
save(logo, "TEX_Menu_Logo.png")

# The like pill's heart, white on the pink.
x0, y0, x1, y1 = 1262, 78, 1346, 156
heart = key_out(sign[y0:y1, x0:x1], PILL)
heart, offset = crop_to_content(heart)
print("Heart at", x0 + offset[0], y0 + offset[1])
save(heart, "TEX_Menu_Heart.png")
