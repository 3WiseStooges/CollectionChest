"""
Thunderstore icon for CollectionChest.

Drawn procedurally rather than resized from source art, so there is no binary master to keep
track of and the icon can be regenerated from this repo alone. Thunderstore wants exactly
256x256 PNG; this renders at 4x and downsamples so the diagonals come out clean.

The motif is the mod: bars falling through a funnel into an iron-bound chest, under
the warm glow of the smelter they came out of.

    python tools/make-icon.py
"""
import os

from PIL import Image, ImageDraw

SS = 4                      # supersampling factor
SIZE = 256
S = SIZE * SS
C = S / 2.0

BG_IN = (58, 36, 22)        # forge glow at the centre
BG_OUT = (14, 11, 10)
GOLD = (219, 184, 108)
GOLD_DIM = (138, 111, 58)
WOOD = (122, 78, 44)
WOOD_DARK = (84, 52, 29)
WOOD_LINE = (60, 36, 20)
IRON = (74, 78, 84)
IRON_LIGHT = (128, 134, 142)
COPPER = (214, 124, 64)
COPPER_LIGHT = (240, 170, 110)

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(os.path.dirname(HERE), "icon.png")


def px(v):
    return v * S


def background(img):
    """Radial falloff, bright where the smelter glows and dark at the edges."""
    draw = ImageDraw.Draw(img)
    steps = int(C)
    cy = C * 0.92
    for i in range(steps, 0, -1):
        t = i / steps
        colour = tuple(int(BG_OUT[c] * t + BG_IN[c] * (1 - t)) for c in range(3))
        draw.ellipse([C - i * 1.1, cy - i * 1.1, C + i * 1.1, cy + i * 1.1], fill=colour)


def chest(draw):
    """Iron-bound chest: body, lid, bands, lock plate."""
    left, right = px(0.20), px(0.80)
    lid_top, lid_bottom = px(0.585), px(0.665)
    body_bottom = px(0.875)
    r = px(0.022)

    # Body and lid, with a dark seam between them.
    draw.rounded_rectangle([left, lid_bottom, right, body_bottom], radius=r, fill=WOOD)
    draw.rounded_rectangle([left, lid_top, right, lid_bottom], radius=r, fill=WOOD_DARK)
    draw.rectangle([left, lid_bottom - px(0.006), right, lid_bottom + px(0.006)], fill=WOOD_LINE)

    # Plank lines on the body.
    for y in (0.735, 0.805):
        draw.line([left + px(0.01), px(y), right - px(0.01), px(y)], fill=WOOD_LINE, width=int(px(0.006)))

    # Iron bands down each side and a lock plate in the middle.
    for x in (0.27, 0.73):
        draw.rectangle([px(x) - px(0.028), lid_top, px(x) + px(0.028), body_bottom], fill=IRON)
        draw.rectangle([px(x) - px(0.028), lid_top, px(x) - px(0.016), body_bottom], fill=IRON_LIGHT)

    draw.rounded_rectangle([C - px(0.05), lid_bottom - px(0.035), C + px(0.05), lid_bottom + px(0.06)],
                           radius=px(0.012), fill=IRON)
    draw.ellipse([C - px(0.016), lid_bottom - px(0.002), C + px(0.016), lid_bottom + px(0.03)], fill=BG_OUT)

    # Gold trim along the lid, so the chest reads against the dark background.
    draw.rounded_rectangle([left, lid_top, right, body_bottom], radius=r,
                           outline=GOLD_DIM, width=int(px(0.008)))


def funnel(draw):
    """The funnel: a wide mouth narrowing to a spout that points into the chest."""
    top, neck, spout_bottom = px(0.30), px(0.47), px(0.56)
    mouth_half, neck_half = px(0.25), px(0.055)

    draw.polygon([(C - mouth_half, top), (C + mouth_half, top), (C + neck_half, neck), (C - neck_half, neck)],
                 fill=IRON)
    # Lit left face, so it reads as a cone rather than a flat trapezoid.
    draw.polygon([(C - mouth_half, top), (C - mouth_half + px(0.07), top), (C - neck_half + px(0.012), neck),
                  (C - neck_half, neck)], fill=IRON_LIGHT)
    draw.rectangle([C - neck_half, neck, C + neck_half, spout_bottom], fill=IRON)
    draw.rectangle([C - neck_half, neck, C - neck_half + px(0.016), spout_bottom], fill=IRON_LIGHT)

    # Gold rim on the mouth.
    draw.rounded_rectangle([C - mouth_half - px(0.012), top - px(0.022), C + mouth_half + px(0.012), top + px(0.012)],
                           radius=px(0.01), fill=GOLD)


def bar(draw, cx, cy, w, h, tilt):
    """A smelted bar, drawn as a slanted trapezoid with a bright top face."""
    hw, hh = w / 2, h / 2
    draw.polygon([(cx - hw + tilt, cy - hh), (cx + hw + tilt, cy - hh), (cx + hw - tilt, cy + hh),
                  (cx - hw - tilt, cy + hh)], fill=COPPER)
    draw.polygon([(cx - hw + tilt, cy - hh), (cx + hw + tilt, cy - hh), (cx + hw * 0.8 + tilt * 0.4, cy - hh * 0.1),
                  (cx - hw * 0.8 + tilt * 0.4, cy - hh * 0.1)], fill=COPPER_LIGHT)


def main():
    img = Image.new("RGB", (S, S), BG_OUT)
    background(img)

    overlay = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    draw = ImageDraw.Draw(overlay)

    # Bars dropping in, the lowest one already inside the mouth of the funnel.
    bar(draw, C - px(0.13), px(0.13), px(0.13), px(0.05), px(0.012))
    bar(draw, C + px(0.10), px(0.19), px(0.13), px(0.05), -px(0.012))
    bar(draw, C - px(0.01), px(0.265), px(0.13), px(0.05), px(0.008))

    funnel(draw)
    chest(draw)

    # Frame, so the icon still reads as a tile against a pale page.
    draw.rounded_rectangle([px(0.02), px(0.02), px(0.98), px(0.98)], radius=px(0.06),
                           outline=GOLD_DIM + (150,), width=int(px(0.012)))

    img = Image.alpha_composite(img.convert("RGBA"), overlay)
    img.convert("RGB").resize((SIZE, SIZE), Image.LANCZOS).save(OUT)
    print(f"wrote {OUT} ({SIZE}x{SIZE})")


if __name__ == "__main__":
    main()
