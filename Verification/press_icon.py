"""Draws the PHASE icon: a pixel "P" with red, blue and green phase echoes on a dark tile.

Writes the 256px icon, the Unity Player icon and the 64px Web favicon from one 32x32 design.
Run from the repository root: python3 Verification/press_icon.py
"""
from PIL import Image

INK, EDGE, LIGHT = (11, 15, 22, 255), (42, 57, 76, 255), (234, 223, 197, 255)
ECHOES = [(-6, 6, (120, 216, 146, 255)), (-4, 4, (121, 201, 255, 255)), (-2, 2, (255, 143, 145, 255))]
GLYPH = [
    "###########...",
    "############..",
    "#############.",
    "##############",
    "####......####",
    "####......####",
    "####......####",
    "####......####",
    "##############",
    "#############.",
    "############..",
    "###########...",
] + ["####.........."] * 8
LEFT, TOP, SIZE = 13, 4, 32


def tile():
    """Rounded dark square with a one-pixel lighter rim."""
    cut = lambda y: [3, 2, 1][min(y, SIZE - 1 - y)] if min(y, SIZE - 1 - y) < 3 else 0
    inside = lambda x, y: 0 <= y < SIZE and cut(y) <= x < SIZE - cut(y)
    image = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    for y in range(SIZE):
        for x in range(SIZE):
            if inside(x, y):
                rim = not all(inside(x + dx, y + dy) for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))
                image.putpixel((x, y), EDGE if rim else INK)
    return image


# The letter's outline with its counter filled, so echoes only show outside the P.
SOLID = [row[:row.rfind("#") + 1].replace(".", "#") + row[row.rfind("#") + 1:] for row in GLYPH]


def stamp(image, dx, dy, color, glyph=GLYPH):
    for y, row in enumerate(glyph):
        for x, cell in enumerate(row):
            if cell == "#":
                image.putpixel((LEFT + dx + x, TOP + dy + y), color)


def main():
    image = tile()
    for dx, dy, color in ECHOES:
        stamp(image, dx, dy, color)
    stamp(image, 0, 0, INK, SOLID)
    stamp(image, 0, 0, LIGHT)
    for path, scale in [("Documentation/press/icon.png", 8), ("Assets/Phase/Art/AppIcon.png", 8), ("Assets/WebGLTemplates/Phase/favicon.png", 2)]:
        image.resize((SIZE * scale, SIZE * scale), Image.NEAREST).save(path)
        print(path, SIZE * scale)


if __name__ == "__main__":
    main()
