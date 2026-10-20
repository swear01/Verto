"""Draws PHASE's press art: the icon, the itch.io cover and the link-preview banner.

Everything is pixel art drawn at native size and enlarged without smoothing. The motif is the
game's phase shift: a light letterform trailed by red, blue and green echoes, on the game's
night-sky palette. Run from the repository root: python3 Verification/press_art.py
"""
import re
from PIL import Image

INK, RIM, LIGHT = (11, 15, 22, 255), (42, 57, 76, 255), (234, 223, 197, 255)
RED, BLUE, GREEN = (255, 143, 145, 255), (121, 201, 255, 255), (120, 216, 146, 255)
SKY = [(10, 16, 24, 255), (12, 19, 29, 255), (14, 21, 32, 255), (16, 26, 38, 255), (18, 30, 43, 255)]
STARS = [(42, 57, 76, 255), (60, 78, 100, 255)]
P_GLYPH = [
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


def title_glyph():
    """The in-game PHASE title art, read from PhaseTitle.cs so both stay identical."""
    source = open("Assets/Phase/Runtime/PhaseTitle.cs", encoding="utf-8").read()
    block = source[source.index("titleText = {"):]
    return re.findall(r'"([#.]+)"', block[:block.index("};")])


def spaced(glyph, extra):
    """Widens every empty column between letters so echoes stay out of the next letter."""
    gaps = [x for x in range(len(glyph[0])) if all(row[x] == "." for row in glyph)]
    return ["".join(c + "." * extra if x in gaps else c for x, c in enumerate(row)) for row in glyph]


def scaled(glyph, k):
    return ["".join(c * k for c in row) for row in glyph for _ in range(k)]


def solid(glyph):
    """The glyph with enclosed counters filled, so echoes never show inside a letter."""
    h, w = len(glyph), len(glyph[0])
    outside, todo = set(), [(x, y) for x in range(-1, w + 1) for y in (-1, h)] + [(x, y) for y in range(h) for x in (-1, w)]
    while todo:
        x, y = todo.pop()
        if (x, y) in outside or not (-1 <= x <= w and -1 <= y <= h):
            continue
        if 0 <= x < w and 0 <= y < h and glyph[y][x] == "#":
            continue
        outside.add((x, y))
        todo += [(x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)]
    return ["".join("#" if (x, y) not in outside else "." for x in range(w)) for y in range(h)]


def stamp(image, glyph, left, top, color):
    for y, row in enumerate(glyph):
        for x, cell in enumerate(row):
            if cell == "#" and 0 <= left + x < image.width and 0 <= top + y < image.height:
                image.putpixel((left + x, top + y), color)


def echo_mark(image, glyph, left, top, step):
    """Light glyph trailed down-left by green, blue and red echoes, `step` pixels apart."""
    for i, color in ((3, GREEN), (2, BLUE), (1, RED)):
        stamp(image, glyph, left - i * step, top + i * step, color)
    stamp(image, solid(glyph), left, top, INK)
    stamp(image, glyph, left, top, LIGHT)


def night(width, height):
    """The game's banded night sky with a few stars."""
    image = Image.new("RGBA", (width, height), SKY[0])
    for y in range(height):
        band = min(len(SKY) - 1, y * len(SKY) // height)
        for x in range(width):
            # Two dithered rows soften each band edge, like the in-game backdrop.
            edge = band > 0 and y < band * height // len(SKY) + 2 and (x + y) % 2
            image.putpixel((x, y), SKY[band - 1] if edge else SKY[band])
    seed = 7
    for i in range(width * height // 140):
        # Small LCG so the scatter is fixed but has no visible lattice at any canvas size.
        seed = (seed * 1103515245 + 12345) % 2 ** 31
        x, y = seed % width, (seed // width) % (height * 2 // 3)
        image.putpixel((x, y), STARS[i % 3 == 0])
    return image


def stripes(image, left, top, width):
    """Three phase-colored bars under the logo, in the order a jump cycles them."""
    for i, color in enumerate((RED, BLUE, GREEN)):
        for y in range(top, top + 2):
            for x in range(left + i * width // 3, left + (i + 1) * width // 3):
                image.putpixel((x, y), color)


def poster(width, height, scale, logo_top):
    image = night(width, height)
    glyph = scaled(spaced(title_glyph(), 1), scale)
    left = (width - len(glyph[0])) // 2 + 1
    echo_mark(image, glyph, left, logo_top, 1)
    stripes(image, left - 3, logo_top + len(glyph) + 7, len(glyph[0]) + 3)
    return image


def icon():
    size = 32
    cut = lambda y: [3, 2, 1][min(y, size - 1 - y)] if min(y, size - 1 - y) < 3 else 0
    inside = lambda x, y: 0 <= y < size and cut(y) <= x < size - cut(y)
    image = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    for y in range(size):
        for x in range(size):
            if inside(x, y):
                rim = not all(inside(x + dx, y + dy) for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))
                image.putpixel((x, y), RIM if rim else INK)
    echo_mark(image, P_GLYPH, 13, 4, 2)
    return image


def save(image, scale, path):
    image.resize((image.width * scale, image.height * scale), Image.NEAREST).save(path)
    print(path, image.width * scale, image.height * scale)


def main():
    mark = icon()
    save(mark, 8, "Documentation/press/icon.png")
    save(mark, 8, "Assets/Phase/Art/AppIcon.png")
    save(mark, 2, "Assets/WebGLTemplates/Phase/favicon.png")
    # 2:1 so itch's link card shows it unmatted; the logo stays inside the centre 1.25:1 crop used by game grids.
    save(poster(192, 96, 3, 33), 5, "Documentation/press/cover.png")
    save(poster(160, 80, 3, 25), 8, "Documentation/press/social.png")


if __name__ == "__main__":
    main()
