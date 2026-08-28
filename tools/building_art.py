#!/usr/bin/env python3
"""Hand-painted machine art for the ONI-Heart buildings.

Everything is rendered at 4x (or 2x for the 2x2 terminal) and downsampled by the
caller.  The drawings deliberately use real machine parts -- cast plates, hex
bolts, flanges, sight glasses, gauges, vents and grime -- instead of flat UI
icons, so the buildings still read as physical objects at ONI's zoom level.
"""
from __future__ import annotations

import math
import random
from pathlib import Path

import numpy as np
from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont


# --------------------------------------------------------------------------
# palette
# --------------------------------------------------------------------------
STEEL_DARK = (17, 23, 33)
STEEL_SIDE = (24, 31, 42)
STEEL_MID = (55, 68, 83)
STEEL_LIGHT = (123, 142, 162)
STEEL_HI = (200, 218, 234)
BRASS_DARK = (74, 49, 24)
BRASS_MID = (168, 118, 56)
BRASS_LIGHT = (230, 186, 112)
INK = (8, 12, 18)
WHITE = (246, 250, 255)

ACCENTS = {
    "gas": (70, 226, 196),
    "liquid": (82, 158, 255),
    "power": (255, 194, 78),
    "decompose": (70, 232, 198),
    "solidify": (142, 212, 255),
    "liquefy": (90, 170, 255),
    "vaporize": (255, 170, 82),
}

FONT_PATH = "/System/Library/Fonts/Supplemental/Arial Bold.ttf"


# --------------------------------------------------------------------------
# low level helpers
# --------------------------------------------------------------------------
def new_canvas(w: int, h: int) -> Image.Image:
    return Image.new("RGBA", (w, h), (0, 0, 0, 0))


def comp(base: Image.Image, layer: Image.Image) -> Image.Image:
    return Image.alpha_composite(base, layer)


def clamp8(v: float) -> int:
    return max(0, min(255, int(v)))


def mix(a, b, t: float):
    return tuple(clamp8(a[i] * (1.0 - t) + b[i] * t) for i in range(3))


def gradient_layer(size, top, bottom, curve: float = 1.0) -> Image.Image:
    w, h = size
    t = (np.linspace(0.0, 1.0, h) ** curve).astype(np.float32)
    arr = np.empty((h, w, 4), dtype=np.uint8)
    for c in range(3):
        arr[..., c] = (top[c] * (1.0 - t) + bottom[c] * t)[:, None].clip(0, 255).astype(np.uint8)
    arr[..., 3] = 255
    return Image.fromarray(arr)


def mask_polygon(size, points, blur: float = 0.0) -> Image.Image:
    m = Image.new("L", size, 0)
    ImageDraw.Draw(m).polygon([(float(x), float(y)) for x, y in points], fill=255)
    if blur:
        m = m.filter(ImageFilter.GaussianBlur(blur))
    return m


def mask_ellipse(size, box, blur: float = 0.0) -> Image.Image:
    m = Image.new("L", size, 0)
    ImageDraw.Draw(m).ellipse(box, fill=255)
    if blur:
        m = m.filter(ImageFilter.GaussianBlur(blur))
    return m


def mask_round_rect(size, box, radius: float, blur: float = 0.0) -> Image.Image:
    m = Image.new("L", size, 0)
    ImageDraw.Draw(m).rounded_rectangle(box, radius=radius, fill=255)
    if blur:
        m = m.filter(ImageFilter.GaussianBlur(blur))
    return m


def fill_gradient(img, mask, top, bottom, curve: float = 1.0) -> Image.Image:
    layer = gradient_layer(img.size, top, bottom, curve)
    layer.putalpha(mask)
    return comp(img, layer)


def fill_solid(img, mask, color) -> Image.Image:
    layer = Image.new("RGBA", img.size, (*color[:3], 255))
    layer.putalpha(mask)
    return comp(img, layer)


def clip_layer(layer: Image.Image, mask: Image.Image) -> Image.Image:
    a = ImageChops.multiply(layer.getchannel("A"), mask)
    layer = layer.copy()
    layer.putalpha(a)
    return layer


def solidify_alpha(img: Image.Image, mask: Image.Image, min_alpha: int = 90) -> Image.Image:
    """Make the machine body opaque.

    ImageDraw writes semi-transparent colours by replacing the destination
    alpha instead of blending it.  Small highlight lines therefore leave alpha
    holes inside an otherwise solid sprite.  This promotes every reasonably
    opaque interior pixel to alpha 255 while leaving the soft shadow/glow
    outside the supplied mask untouched.
    """
    r, g, b, a = img.split()
    binary = a.point(lambda v: 255 if v >= min_alpha else 0)
    promote = ImageChops.multiply(mask, binary)
    a = ImageChops.lighter(a, promote)
    return Image.merge("RGBA", (r, g, b, a))


def noise_array(size, seed: int, cells: int = 20) -> np.ndarray:
    rng = np.random.default_rng(seed)
    small = rng.normal(0.0, 1.0, (cells, cells)).astype(np.float32)
    img = Image.fromarray(((small * 0.25 + 0.5) * 255).clip(0, 255).astype(np.uint8), "L")
    img = img.resize(size, Image.BICUBIC).filter(ImageFilter.GaussianBlur(0.8))
    return (np.asarray(img, dtype=np.float32) - 128.0) / 128.0


def add_noise(img, mask, seed: int, opacity: int = 20, cells: int = 24) -> Image.Image:
    n = noise_array(img.size, seed, cells)
    layer = np.zeros((img.size[1], img.size[0], 4), dtype=np.uint8)
    layer[..., 0] = (128 + n * 70).clip(0, 255)
    layer[..., 1] = (128 + n * 70).clip(0, 255)
    layer[..., 2] = (132 + n * 70).clip(0, 255)
    layer[..., 3] = (np.asarray(mask, dtype=np.float32) * (opacity / 255.0)).clip(0, 255)
    return comp(img, Image.fromarray(layer, "RGBA"))


def add_brushed(img, mask, seed: int, count: int = 260, vertical: bool = False,
                alpha: int = 16) -> Image.Image:
    rng = random.Random(seed)
    w, h = img.size
    layer = Image.new("RGBA", img.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    if vertical:
        for _ in range(count):
            x = rng.uniform(0, w)
            y0 = rng.uniform(0, h)
            y1 = y0 + rng.uniform(h * 0.04, h * 0.35)
            value = rng.randint(-12, 18)
            c = (128 + value, 128 + value, 132 + value, rng.randint(8, alpha))
            d.line([(x, y0), (x, y1)], fill=c, width=rng.choice((1, 1, 2)))
    else:
        for _ in range(count):
            y = rng.uniform(0, h)
            x0 = rng.uniform(0, w)
            x1 = x0 + rng.uniform(w * 0.04, w * 0.35)
            value = rng.randint(-12, 18)
            c = (128 + value, 128 + value, 132 + value, rng.randint(8, alpha))
            d.line([(x0, y), (x1, y)], fill=c, width=rng.choice((1, 1, 2)))
    layer.putalpha(ImageChops.multiply(layer.getchannel("A"), mask))
    return comp(img, layer)


def add_grime(img, mask, seed: int, strength: int = 1) -> Image.Image:
    rng = random.Random(seed)
    w, h = img.size
    layer = Image.new("RGBA", img.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    for _ in range(40 * strength):
        x = rng.uniform(w * 0.05, w * 0.95)
        y = rng.uniform(h * 0.05, h * 0.85)
        length = rng.uniform(h * 0.02, h * 0.15)
        wid = rng.uniform(1, 5)
        a = rng.randint(6, 24)
        d.line([(x, y), (x + rng.uniform(-4, 4), y + length)],
               fill=(8, 12, 16, a), width=int(wid))
    layer = layer.filter(ImageFilter.GaussianBlur(1.2))
    layer.putalpha(ImageChops.multiply(layer.getchannel("A"), mask))
    return comp(img, layer)


def edge_normal(a, b, center):
    ex, ey = b[0] - a[0], b[1] - a[1]
    ln = math.hypot(ex, ey)
    if ln < 1e-8:
        return 0.0, 0.0
    nx, ny = ey / ln, -ex / ln
    mx, my = (a[0] + b[0]) * 0.5, (a[1] + b[1]) * 0.5
    if nx * (mx - center[0]) + ny * (my - center[1]) < 0:
        nx, ny = -nx, -ny
    return nx, ny


def bevel_polygon(img, points, center=None, width: int = 6,
                  light=STEEL_HI, dark=INK, strength: int = 255,
                  light_dir=(-0.55, -0.83)) -> Image.Image:
    if center is None:
        center = (sum(p[0] for p in points) / len(points),
                  sum(p[1] for p in points) / len(points))
    d = ImageDraw.Draw(img)
    d.line(list(points) + [points[0]], fill=(*dark[:3], strength), width=width + 2)
    for i in range(len(points)):
        a, b = points[i], points[(i + 1) % len(points)]
        nx, ny = edge_normal(a, b, center)
        lit = nx * light_dir[0] + ny * light_dir[1]
        if lit > 0.12:
            d.line([a, b], fill=(*light[:3], strength), width=max(2, width - 2))
        elif lit < -0.30:
            d.line([a, b], fill=(*dark[:3], min(255, strength)), width=max(2, width - 1))
    return img


def draw_hex_bolt(img, cx: float, cy: float, r: float, seed: int = 0,
                  washer: bool = True) -> None:
    rng = random.Random(seed)
    d = ImageDraw.Draw(img)
    ang = math.pi / 6 + rng.uniform(-0.06, 0.06)
    pts = [(cx + math.cos(ang + i * math.pi / 3) * r,
            cy + math.sin(ang + i * math.pi / 3) * r) for i in range(6)]
    if washer:
        d.ellipse([cx - r * 1.32, cy - r * 1.32, cx + r * 1.32, cy + r * 1.32],
                  fill=(*STEEL_SIDE, 255), outline=(*INK, 235), width=max(2, int(r * 0.14)))
        d.arc([cx - r * 1.30, cy - r * 1.30, cx + r * 1.30, cy + r * 1.30],
              205, 330, fill=(*STEEL_LIGHT, 170), width=max(1, int(r * 0.10)))
    d.polygon(pts, fill=(*STEEL_MID, 255), outline=(*INK, 255))
    inner = [(cx + (x - cx) * 0.72, cy + (y - cy) * 0.72) for x, y in pts]
    d.polygon(inner, fill=(*mix(STEEL_MID, STEEL_LIGHT, 0.36), 255))
    # lit top edge
    d.line([pts[3], pts[4], pts[5]], fill=(*STEEL_HI, 170), width=max(1, int(r * 0.12)))
    # hex socket
    sock = [(cx + (x - cx) * 0.38, cy + (y - cy) * 0.38) for x, y in pts]
    d.polygon(sock, fill=(*INK, 255))
    d.line([sock[3], sock[4], sock[5]], fill=(60, 72, 86, 255), width=max(1, int(r * 0.08)))


def draw_screw(img, cx: float, cy: float, r: float, slot_angle: float = 0.6) -> None:
    d = ImageDraw.Draw(img)
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=(*STEEL_MID, 255),
              outline=(*INK, 230), width=max(1, int(r * 0.22)))
    d.arc([cx - r * 0.8, cy - r * 0.8, cx + r * 0.8, cy + r * 0.8],
          200, 320, fill=(*STEEL_HI, 170), width=max(1, int(r * 0.18)))
    dx, dy = math.cos(slot_angle) * r * 0.62, math.sin(slot_angle) * r * 0.62
    d.line([(cx - dx, cy - dy), (cx + dx, cy + dy)],
           fill=(*INK, 210), width=max(1, int(r * 0.20)))


def rounded_panel(img, box, radius, top, bottom, seed: int, outline=INK,
                  light=STEEL_HI, bevel: int = 5) -> Image.Image:
    mask = mask_round_rect(img.size, box, radius)
    img = fill_gradient(img, mask, top, bottom, 0.85)
    img = add_brushed(img, mask, seed, 120, alpha=13)
    img = add_noise(img, mask, seed + 1, 16, 18)
    d = ImageDraw.Draw(img)
    d.rounded_rectangle(box, radius=radius, outline=(*outline, 255), width=max(3, bevel + 1))
    x0, y0, x1, y1 = box
    d.line([(x0 + radius, y0 + bevel + 1), (x1 - radius, y0 + bevel + 1)],
           fill=(*light, 150), width=max(2, bevel - 2))
    d.line([(x0 + bevel + 1, y0 + radius), (x0 + bevel + 1, y1 - radius)],
           fill=(*light, 105), width=max(2, bevel - 3))
    d.line([(x0 + radius, y1 - bevel - 1), (x1 - radius, y1 - bevel - 1)],
           fill=(*INK, 190), width=max(2, bevel - 2))
    d.line([(x1 - bevel - 1, y0 + radius), (x1 - bevel - 1, y1 - radius)],
           fill=(*INK, 165), width=max(2, bevel - 3))
    return img


def draw_pipe(img, x0, y0, x1, y1, width: int, seed: int = 0,
              metal_top=STEEL_LIGHT, metal_bottom=STEEL_MID) -> Image.Image:
    d = ImageDraw.Draw(img)
    # cast shadow / outline
    d.line([(x0, y0), (x1, y1)], fill=(*INK, 255), width=width + 12)
    d.line([(x0, y0), (x1, y1)], fill=(*STEEL_SIDE, 255), width=width + 4)
    d.line([(x0, y0), (x1, y1)], fill=(*metal_bottom, 255), width=width)
    length = max(1e-6, math.hypot(x1 - x0, y1 - y0))
    nx, ny = -(y1 - y0) / length, (x1 - x0) / length
    off = max(2, width // 5)
    d.line([(x0 + nx * off, y0 + ny * off), (x1 + nx * off, y1 + ny * off)],
           fill=(*mix(metal_top, STEEL_HI, 0.25), 235), width=max(2, width // 3))
    d.line([(x0 + nx * off * 2, y0 + ny * off * 2),
            (x1 + nx * off * 2, y1 + ny * off * 2)],
           fill=(*metal_top, 170), width=max(1, width // 8))
    d.line([(x0 - nx * off * 2, y0 - ny * off * 2),
            (x1 - nx * off * 2, y1 - ny * off * 2)],
           fill=(*INK, 120), width=max(1, width // 7))
    # rounded end caps
    for x, y in ((x0, y0), (x1, y1)):
        d.ellipse([x - width / 2, y - width / 2, x + width / 2, y + width / 2],
                  fill=(*metal_bottom, 255), outline=(*INK, 255), width=max(2, width // 12))
        d.ellipse([x - width / 2 + off, y - width / 2 + off,
                   x + width / 2 - off, y + width / 2 - off],
                  fill=(*metal_top, 120))
    return img


def draw_flange(img, cx, cy, width, thickness, seed: int = 0) -> Image.Image:
    box = [cx - width / 2, cy - thickness / 2, cx + width / 2, cy + thickness / 2]
    img = rounded_panel(img, box, thickness * 0.42, (116, 134, 154), (44, 55, 69),
                        seed, bevel=4)
    # bolts on the flange
    for i in range(4):
        angle = math.pi * (0.15 + i * 0.233)
        bx = cx + math.cos(angle) * width * 0.36
        by = cy + math.sin(angle) * thickness * 0.18
        draw_hex_bolt(img, bx, by, thickness * 0.16, seed + i)
    d = ImageDraw.Draw(img)
    d.line([(cx - width * 0.40, cy - thickness * 0.20),
            (cx + width * 0.40, cy - thickness * 0.20)],
           fill=(*STEEL_HI, 120), width=2)
    return img


def draw_gauge(img, cx, cy, r, accent, value: float = 0.55, seed: int = 0) -> Image.Image:
    d = ImageDraw.Draw(img)
    d.ellipse([cx - r * 1.15, cy - r * 1.15, cx + r * 1.15, cy + r * 1.15],
              fill=(*STEEL_SIDE, 255), outline=(*INK, 255), width=max(3, int(r * 0.12)))
    d.ellipse([cx - r, cy - r, cx + r, cy + r],
              fill=(218, 221, 209, 255), outline=(*STEEL_DARK, 255), width=max(2, int(r * 0.10)))
    # tick marks
    for i in range(11):
        a = math.radians(150 + i * 24)
        x0 = cx + math.cos(a) * r * 0.72
        y0 = cy + math.sin(a) * r * 0.72
        x1 = cx + math.cos(a) * r * 0.88
        y1 = cy + math.sin(a) * r * 0.88
        d.line([(x0, y0), (x1, y1)], fill=(*INK, 220), width=max(1, int(r * 0.05)))
    # danger arc
    d.arc([cx - r * 0.76, cy - r * 0.76, cx + r * 0.76, cy + r * 0.76],
          305, 345, fill=(196, 70, 50, 255), width=max(2, int(r * 0.10)))
    a = math.radians(150 + value * 240)
    d.line([(cx, cy), (cx + math.cos(a) * r * 0.66, cy + math.sin(a) * r * 0.66)],
           fill=(*INK, 255), width=max(2, int(r * 0.07)))
    d.ellipse([cx - r * 0.12, cy - r * 0.12, cx + r * 0.12, cy + r * 0.12],
              fill=(*BRASS_MID, 255), outline=(*INK, 255), width=1)
    d.arc([cx - r * 0.82, cy - r * 0.82, cx + r * 0.82, cy + r * 0.82],
          195, 320, fill=(255, 255, 255, 115), width=max(2, int(r * 0.07)))
    d.arc([cx - r * 1.08, cy - r * 1.08, cx + r * 1.08, cy + r * 1.08],
          200, 300, fill=(*accent, 130), width=max(2, int(r * 0.06)))
    return img


def draw_vent(img, box, slots: int = 5, seed: int = 0) -> Image.Image:
    x0, y0, x1, y1 = box
    img = rounded_panel(img, box, max(4, (y1 - y0) * 0.15),
                        (38, 47, 58), (20, 26, 35), seed, bevel=3)
    d = ImageDraw.Draw(img)
    gap = (x1 - x0) / (slots + 1)
    for i in range(slots):
        x = x0 + gap * (i + 0.45)
        d.rounded_rectangle([x, y0 + (y1 - y0) * 0.28, x + gap * 0.55, y1 - (y1 - y0) * 0.28],
                            radius=max(2, gap * 0.18), fill=(*INK, 255))
        d.line([(x + 2, y1 - (y1 - y0) * 0.26), (x + gap * 0.55 - 2, y1 - (y1 - y0) * 0.26)],
               fill=(*STEEL_LIGHT, 90), width=1)
    return img


def draw_label(img, box, text: str, accent, seed: int = 0) -> Image.Image:
    x0, y0, x1, y1 = box
    img = rounded_panel(img, box, max(3, (y1 - y0) * 0.18),
                        (74, 83, 91), (34, 40, 47), seed, bevel=3)
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([x0 + 5, y0 + 5, x1 - 5, y1 - 5],
                        radius=max(2, (y1 - y0) * 0.12), outline=(*accent, 120), width=2)
    if text:
        try:
            font = ImageFont.truetype(FONT_PATH, max(10, int((y1 - y0) * 0.48)))
            bb = d.textbbox((0, 0), text, font=font)
            tw, th = bb[2] - bb[0], bb[3] - bb[1]
            d.text(((x0 + x1 - tw) / 2 - bb[0], (y0 + y1 - th) / 2 - bb[1]),
                   text, font=font, fill=(*WHITE, 220))
        except Exception:
            pass
    draw_screw(img, x0 + 10, y0 + 10, 4, 0.4)
    draw_screw(img, x1 - 10, y1 - 10, 4, 1.1)
    return img


def soft_glow(size, box, color, blur: float, alpha: int) -> Image.Image:
    layer = Image.new("RGBA", size, (0, 0, 0, 0))
    ImageDraw.Draw(layer).ellipse(box, fill=(*color[:3], alpha))
    return layer.filter(ImageFilter.GaussianBlur(blur))


def light_arc(img, box, start, end, color, width, alpha=255) -> Image.Image:
    layer = Image.new("RGBA", img.size, (0, 0, 0, 0))
    ImageDraw.Draw(layer).arc(box, start, end, fill=(*color[:3], alpha), width=width)
    return comp(img, layer)


# --------------------------------------------------------------------------
# element interface (1x1)
# --------------------------------------------------------------------------
def interface_image(kind: str) -> Image.Image:
    S = 1024
    img = new_canvas(S, S)
    d = ImageDraw.Draw(img)

    accent = ACCENTS["gas" if kind.startswith("gas") else
                     "liquid" if kind.startswith("liquid") else "power"]
    is_power = kind == "power"
    is_in = kind.endswith("_in")

    # contact shadow
    shadow = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    ImageDraw.Draw(shadow).ellipse([110, 800, 914, 1004], fill=(0, 0, 0, 120))
    img = comp(img, shadow.filter(ImageFilter.GaussianBlur(28)))

    # back plate with visible thickness
    face = [(176, 140), (848, 140), (910, 202), (910, 818),
            (848, 880), (176, 880), (114, 818), (114, 202)]
    side = [(x, y + 22) for x, y in face]
    img = fill_solid(img, mask_polygon((S, S), side), (12, 16, 23))
    img = fill_gradient(img, mask_polygon((S, S), face), (74, 88, 105), (25, 32, 42), 0.9)
    pmask = mask_polygon((S, S), face)
    img = add_brushed(img, pmask, 10, 420, alpha=15)
    img = add_noise(img, pmask, 11, 20, 22)
    img = bevel_polygon(img, face, width=7, light=(154, 176, 198), dark=(6, 10, 16), strength=235)

    # inner recessed line and painted accent border
    inner = [(196, 162), (828, 162), (886, 220), (886, 800),
             (828, 858), (196, 858), (138, 800), (138, 220)]
    d = ImageDraw.Draw(img)
    d.line(inner + [inner[0]], fill=(*INK, 160), width=4)
    d.line([(x + 4, y + 4) for x, y in inner] +
           [(inner[0][0] + 4, inner[0][1] + 4)],
           fill=(*STEEL_HI, 60), width=2)
    d.line(inner + [inner[0]], fill=(*accent, 120), width=2)

    # corner bolts and side rivets
    for i, (bx, by) in enumerate(((196, 224), (828, 224), (196, 796), (828, 796))):
        draw_hex_bolt(img, bx, by, 28, 30 + i)
    for by in (340, 470, 600):
        draw_screw(img, 160, by, 9, 0.2)
        draw_screw(img, 864, by, 9, 1.4)
    d = ImageDraw.Draw(img)

    # ---- top pipe / valve fitting ----------------------------------------
    img = draw_pipe(img, 512, 66, 512, 292, 90, 20)
    img = draw_flange(img, 512, 112, 168, 44, 21)
    img = draw_flange(img, 512, 244, 176, 48, 22)
    if is_in:
        # hand valve wheel on the input side, with a real axle into the pipe
        d = ImageDraw.Draw(img)
        d.rounded_rectangle([548, 148, 612, 172], radius=10, fill=(*STEEL_MID, 255),
                            outline=(*INK, 255), width=3)
        d.line([(554, 153), (606, 153)], fill=(*STEEL_HI, 130), width=3)
        cx, cy, r = 650, 160, 64
        d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=(*BRASS_DARK, 255),
                  outline=(*INK, 255), width=6)
        d.ellipse([cx - r * 0.78, cy - r * 0.78, cx + r * 0.78, cy + r * 0.78],
                  fill=(*BRASS_MID, 255))
        for k in range(4):
            a = k * math.pi / 2 + 0.4
            d.line([(cx, cy), (cx + math.cos(a) * r * 0.82, cy + math.sin(a) * r * 0.82)],
                   fill=(*BRASS_LIGHT, 255), width=12)
        d.ellipse([cx - 18, cy - 18, cx + 18, cy + 18], fill=(*BRASS_DARK, 255),
                  outline=(*INK, 255), width=4)
        d.arc([cx - r * 0.9, cy - r * 0.9, cx + r * 0.9, cy + r * 0.9],
              195, 320, fill=(*BRASS_LIGHT, 180), width=5)
    else:
        img = draw_gauge(img, 350, 160, 62, accent, 0.62, 23)
        d = ImageDraw.Draw(img)
        d.rounded_rectangle([382, 150, 474, 172], radius=8, fill=(*STEEL_MID, 255),
                            outline=(*INK, 255), width=3)
        d.line([(388, 155), (468, 155)], fill=(*STEEL_HI, 120), width=3)

    # ---- central port ring ------------------------------------------------
    cx, cy, r_out, r_in, r_glass = 512, 470, 300, 232, 202

    # cast shadow under the ring
    shadow = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    ImageDraw.Draw(shadow).ellipse([cx - r_out - 6, cy - r_out + 20,
                                    cx + r_out + 6, cy + r_out + 28],
                                   fill=(0, 0, 0, 130))
    img = comp(img, shadow.filter(ImageFilter.GaussianBlur(16)))
    d = ImageDraw.Draw(img)

    outer_mask = mask_ellipse((S, S), [cx - r_out, cy - r_out, cx + r_out, cy + r_out])
    inner_hole = mask_ellipse((S, S), [cx - r_in, cy - r_in, cx + r_in, cy + r_in])
    ring_mask = ImageChops.subtract(outer_mask, inner_hole)
    img = fill_gradient(img, ring_mask, (166, 184, 202), (52, 64, 78), 0.85)
    img = add_brushed(img, ring_mask, 31, 260, alpha=14)
    img = add_noise(img, ring_mask, 32, 22, 20)

    # ring grooves
    d = ImageDraw.Draw(img)
    for rr, alpha in ((r_out - 10, 155), (r_out - 24, 95), (r_in + 16, 110)):
        d.ellipse([cx - rr, cy - rr, cx + rr, cy + rr],
                  outline=(*INK, alpha), width=3)
        d.ellipse([cx - rr + 3, cy - rr + 3, cx + rr - 3, cy + rr - 3],
                  outline=(*STEEL_HI, 68), width=2)
    # 10 flange bolts
    for i in range(10):
        a = -math.pi / 2 + i * 2 * math.pi / 10
        bx = cx + math.cos(a) * (r_out - 44)
        by = cy + math.sin(a) * (r_out - 44)
        draw_hex_bolt(img, bx, by, 18, 40 + i)

    # bright lit arc on the upper-left of the ring
    img = light_arc(img, [cx - r_out + 8, cy - r_out + 8, cx + r_out - 8, cy + r_out - 8],
                    195, 310, (232, 243, 255), 10, 150)
    img = light_arc(img, [cx - r_out + 8, cy - r_out + 8, cx + r_out - 8, cy + r_out - 8],
                    105, 165, accent, 6, 90)
    d = ImageDraw.Draw(img)

    # dark recess behind the glass
    recess = mask_ellipse((S, S), [cx - r_in, cy - r_in, cx + r_in, cy + r_in])
    img = fill_gradient(img, recess, (25, 34, 46), (4, 7, 11), 1.0)
    img = add_noise(img, recess, 50, 24, 18)
    d = ImageDraw.Draw(img)
    d.ellipse([cx - r_in, cy - r_in, cx + r_in, cy + r_in],
              outline=(*INK, 255), width=10)
    d.ellipse([cx - r_in + 7, cy - r_in + 7, cx + r_in - 7, cy + r_in - 7],
              outline=(*accent, 210), width=5)
    d.arc([cx - r_in + 7, cy - r_in + 7, cx + r_in - 7, cy + r_in - 7],
          200, 320, fill=(255, 255, 255, 150), width=4)

    # glass disc
    glass_mask = mask_ellipse((S, S), [cx - r_glass, cy - r_glass,
                                       cx + r_glass, cy + r_glass])
    tint = (30, 118, 108) if kind.startswith("gas") else \
           (30, 78, 150) if kind.startswith("liquid") else (150, 92, 24)
    img = fill_gradient(img, glass_mask, mix(tint, (18, 24, 28), 0.45), (3, 7, 11), 1.0)

    # mechanism layer, clipped to the glass
    mech = new_canvas(S, S)
    md = ImageDraw.Draw(mech)
    if is_power:
        mech = _power_mechanism(mech, cx, cy, accent)
    elif kind.startswith("liquid"):
        mech = _liquid_mechanism(mech, cx, cy, accent)
    else:
        mech = _gas_mechanism(mech, cx, cy, accent)
    mech = clip_layer(mech, glass_mask)
    img = comp(img, mech)

    # glass reflections and rim
    d = ImageDraw.Draw(img)
    d.ellipse([cx - r_glass, cy - r_glass, cx + r_glass, cy + r_glass],
              outline=(*INK, 235), width=8)
    d.ellipse([cx - r_glass + 5, cy - r_glass + 5, cx + r_glass - 5, cy + r_glass - 5],
              outline=(230, 248, 255, 180), width=3)
    img = light_arc(img, [cx - r_glass + 16, cy - r_glass + 16,
                          cx + r_glass - 16, cy + r_glass - 16],
                    196, 292, (255, 255, 255), 12, 92)
    d = ImageDraw.Draw(img)
    d.arc([cx - r_glass + 24, cy - r_glass + 24, cx + r_glass - 24, cy + r_glass - 24],
          298, 345, fill=(*accent, 100), width=5)
    # small specular streak
    d.polygon([(cx - 122, cy - 156), (cx - 98, cy - 164), (cx - 38, cy - 116),
               (cx - 62, cy - 108)], fill=(255, 255, 255, 48))

    # ---- flow badge ------------------------------------------------------
    box = (356, 792, 668, 872)
    img = rounded_panel(img, box, 18, (92, 106, 122), (34, 43, 55), 60, bevel=4)
    d = ImageDraw.Draw(img)
    # cast chevrons: input points down, output points up, power is two-way
    if is_power:
        chevrons = ((484, "up"), (540, "down"))
    else:
        chevrons = tuple((452 + k * 54, "down" if is_in else "up") for k in range(3))
    for x, direction in chevrons:
        if direction == "down":
            pts = [(x - 20, 814), (x + 20, 814), (x + 20, 830), (x, 850), (x - 20, 830)]
        else:
            pts = [(x - 20, 850), (x + 20, 850), (x + 20, 834), (x, 814), (x - 20, 834)]
        d.polygon([(px + 2, py + 3) for px, py in pts], fill=(*INK, 180))
        d.polygon(pts, fill=(*accent, 235), outline=(*INK, 255))
        d.line([pts[0], pts[3], pts[1]], fill=(255, 255, 255, 140), width=3)
    draw_screw(img, 376, 832, 6, 0.3)
    draw_screw(img, 648, 832, 6, 1.2)

    # status lamp + engraved plate
    lamp = (790, 206)
    d = ImageDraw.Draw(img)
    glow_col = (255, 198, 88) if is_power else (94, 255, 140) if is_in else (255, 170, 72)
    img = comp(img, soft_glow((S, S), [lamp[0] - 46, lamp[1] - 46,
                                       lamp[0] + 46, lamp[1] + 46], glow_col, 22, 130))
    d = ImageDraw.Draw(img)
    d.ellipse([lamp[0] - 27, lamp[1] - 27, lamp[0] + 27, lamp[1] + 27],
              fill=(*STEEL_SIDE, 255), outline=(*INK, 255), width=4)
    d.ellipse([lamp[0] - 16, lamp[1] - 16, lamp[0] + 16, lamp[1] + 16],
              fill=(*glow_col, 255), outline=(20, 26, 28, 255), width=2)
    d.arc([lamp[0] - 13, lamp[1] - 13, lamp[0] + 13, lamp[1] + 13],
          200, 310, fill=(255, 255, 255, 210), width=3)
    img = draw_label(img, (240, 780, 320, 826), "E-04", accent, 70)

    # ---- weathering / edge wear -----------------------------------------
    d = ImageDraw.Draw(img)
    # scratches and paint chips
    rng = random.Random(90)
    for _ in range(22):
        x = rng.uniform(180, 840)
        y = rng.uniform(190, 840)
        if pmask.getpixel((int(x), int(y))) == 0:
            continue
        ln = rng.uniform(5, 22)
        a = math.radians(rng.uniform(-100, -20))
        d.line([(x, y), (x + math.cos(a) * ln, y + math.sin(a) * ln)],
               fill=(210, 224, 238, rng.randint(20, 55)), width=1)
    img = add_grime(img, pmask, 91, 1)
    img = add_noise(img, img.getchannel("A"), 92, 12, 40)

    # keep the final alpha clean inside the machine; the outer glow stays soft
    solid = mask_polygon((S, S), [(96, 0), (928, 0), (928, 900), (96, 900)])
    return solidify_alpha(img, solid, 90)


def _gas_mechanism(img: Image.Image, cx: float, cy: float, accent) -> Image.Image:
    d = ImageDraw.Draw(img)
    # gas swirl behind the impeller
    for k in range(5):
        r = 150 - k * 22
        off = k * 0.55
        d.arc([cx - r, cy - r * 0.82, cx + r, cy + r * 0.82],
              int(math.degrees(off)), int(math.degrees(off + 1.9)),
              fill=(*accent, 105 - k * 12), width=14 - k)
    for k in range(9):
        a = k * 2.4
        rr = 60 + (k * 31) % 120
        px = cx + math.cos(a) * rr
        py = cy + math.sin(a) * rr * 0.72
        d.ellipse([px - 16, py - 12, px + 16, py + 12],
                  fill=(*accent, 32))
    # brass impeller
    blades = 6
    for k in range(blades):
        a = k * 2 * math.pi / blades
        pts = []
        for px, py in ((14, -36), (34, -146), (62, -120), (46, -26)):
            x = math.cos(a) * px - math.sin(a) * py + cx
            y = math.sin(a) * px + math.cos(a) * py + cy
            pts.append((x, y))
        d.polygon(pts, fill=(*BRASS_MID, 255), outline=(*INK, 255))
        d.line([pts[0], pts[3]], fill=(*BRASS_LIGHT, 210), width=4)
        d.line([pts[1], pts[2]], fill=(*BRASS_DARK, 255), width=5)
    d.ellipse([cx - 54, cy - 54, cx + 54, cy + 54],
              fill=(*BRASS_MID, 255), outline=(*INK, 255), width=5)
    d.ellipse([cx - 34, cy - 34, cx + 34, cy + 34],
              fill=(*BRASS_LIGHT, 255), outline=(*BRASS_DARK, 255), width=3)
    d.ellipse([cx - 9, cy - 9, cx + 9, cy + 9], fill=(*INK, 255))
    d.arc([cx - 48, cy - 48, cx + 48, cy + 48], 195, 315,
          fill=(255, 248, 224, 190), width=5)
    # cage / guard ring
    d.arc([cx - 170, cy - 170, cx + 170, cy + 170], 205, 335,
          fill=(*STEEL_LIGHT, 110), width=5)
    d.arc([cx - 170, cy - 170, cx + 170, cy + 170], 20, 150,
          fill=(*INK, 120), width=3)
    return img


def _liquid_mechanism(img: Image.Image, cx: float, cy: float, accent) -> Image.Image:
    d = ImageDraw.Draw(img)
    # big brass valve wheel in the back
    vx, vy, vr = cx, cy - 122, 82
    d.ellipse([vx - vr, vy - vr, vx + vr, vy + vr],
              fill=(*BRASS_DARK, 255), outline=(*INK, 255), width=5)
    for k in range(4):
        a = k * math.pi / 2 + 0.35
        d.line([(vx, vy), (vx + math.cos(a) * vr * 0.80, vy + math.sin(a) * vr * 0.80)],
               fill=(*BRASS_MID, 255), width=15)
    d.ellipse([vx - 22, vy - 22, vx + 22, vy + 22],
              fill=(*BRASS_LIGHT, 255), outline=(*INK, 255), width=4)
    d.arc([vx - vr * 0.9, vy - vr * 0.9, vx + vr * 0.9, vy + vr * 0.9],
          200, 320, fill=(*BRASS_LIGHT, 180), width=4)

    # sight glass tube
    tube = (cx - 78, cy - 36, cx + 78, cy + 176)
    tm = mask_round_rect(img.size, tube, 30)
    img = fill_gradient(img, tm, (12, 24, 40), (5, 11, 20), 1.0)
    d = ImageDraw.Draw(img)
    # liquid body with a curved surface
    fill_y = cy + 62
    d.rounded_rectangle([tube[0] + 3, fill_y, tube[2] - 3, tube[3] - 4],
                        radius=24, fill=(*mix(accent, (10, 30, 60), 0.45), 235))
    # surface ellipse
    d.ellipse([tube[0] + 4, fill_y - 20, tube[2] - 4, fill_y + 20],
              fill=(*mix(accent, (230, 250, 255), 0.35), 245),
              outline=(255, 255, 255, 180), width=3)
    # bubbles
    rng = random.Random(7)
    for _ in range(12):
        bx = rng.uniform(tube[0] + 24, tube[2] - 24)
        by = rng.uniform(fill_y + 20, tube[3] - 22)
        br = rng.uniform(3, 10)
        d.ellipse([bx - br, by - br, bx + br, by + br],
                  outline=(225, 248, 255, 110), width=2)
    # float ball on the surface
    fx, fy, fr = cx, fill_y - 6, 25
    d.ellipse([fx - fr, fy - fr, fx + fr, fy + fr],
              fill=(*BRASS_MID, 255), outline=(*INK, 255), width=3)
    d.arc([fx - fr + 4, fy - fr + 4, fx + fr - 4, fy + fr - 4],
          195, 315, fill=(*BRASS_LIGHT, 220), width=4)
    # rod
    d.line([(cx, tube[1] + 6), (cx, fy - fr)],
           fill=(*STEEL_LIGHT, 255), width=7)
    d.line([(cx - 2, tube[1] + 6), (cx - 2, fy - fr)],
           fill=(255, 255, 255, 150), width=2)
    # tube outline and glass highlights
    d.rounded_rectangle(tube, radius=30, outline=(*INK, 255), width=7)
    d.rounded_rectangle([tube[0] + 5, tube[1] + 5, tube[2] - 5, tube[3] - 5],
                        radius=25, outline=(205, 238, 255, 150), width=3)
    d.line([(tube[0] + 18, tube[1] + 30), (tube[0] + 18, tube[3] - 26)],
           fill=(255, 255, 255, 70), width=8)
    d.line([(tube[2] - 22, tube[1] + 42), (tube[2] - 22, tube[3] - 34)],
           fill=(255, 255, 255, 38), width=4)
    # inlet/outlet collars
    d.rounded_rectangle([cx - 118, cy + 146, cx - 60, cy + 186], radius=14,
                        fill=(*STEEL_MID, 255), outline=(*INK, 255), width=3)
    d.rounded_rectangle([cx + 60, cy + 146, cx + 118, cy + 186], radius=14,
                        fill=(*STEEL_MID, 255), outline=(*INK, 255), width=3)
    return img


def _power_mechanism(img: Image.Image, cx: float, cy: float, accent) -> Image.Image:
    d = ImageDraw.Draw(img)
    # plasma haze
    glow = Image.new("RGBA", img.size, (0, 0, 0, 0))
    gd = ImageDraw.Draw(glow)
    gd.ellipse([cx - 170, cy - 160, cx + 170, cy + 160], fill=(*accent, 70))
    gd.ellipse([cx - 96, cy - 96, cx + 96, cy + 96], fill=(255, 236, 180, 85))
    glow = glow.filter(ImageFilter.GaussianBlur(30))
    img = comp(img, glow)
    d = ImageDraw.Draw(img)

    # ceramic insulator stacks
    for side in (-1, 1):
        bx = cx + side * 148
        for k in range(5):
            ry = cy - 96 + k * 48
            w = 58 - k * 2
            d.ellipse([bx - w, ry - 20, bx + w, ry + 20],
                      fill=(224, 218, 196, 255), outline=(*INK, 255), width=3)
            d.arc([bx - w + 3, ry - 17, bx + w - 3, ry + 17],
                  195, 315, fill=(255, 255, 245, 190), width=3)
        d.line([(bx, cy - 128), (bx, cy + 128)],
               fill=(*STEEL_DARK, 255), width=12)
    # copper bus bars
    d.line([(cx - 190, cy + 104), (cx - 74, cy + 78)],
           fill=(*BRASS_DARK, 255), width=22)
    d.line([(cx - 190, cy + 104), (cx - 74, cy + 78)],
           fill=(*BRASS_MID, 255), width=14)
    d.line([(cx + 190, cy + 104), (cx + 74, cy + 78)],
           fill=(*BRASS_DARK, 255), width=22)
    d.line([(cx + 190, cy + 104), (cx + 74, cy + 78)],
           fill=(*BRASS_MID, 255), width=14)
    # coil around the arc
    for k in range(4):
        r = 72 - k * 12
        d.arc([cx - r, cy - 70 + k * 8, cx + r, cy + 70 + k * 8],
              200, 340, fill=(*BRASS_LIGHT, 220), width=6)
    # central plasma arc
    d.line([(cx - 54, cy + 30), (cx - 20, cy - 30), (cx + 8, cy + 12), (cx + 46, cy - 52)],
           fill=(255, 255, 230, 255), width=9)
    d.line([(cx - 54, cy + 30), (cx - 20, cy - 30), (cx + 8, cy + 12), (cx + 46, cy - 52)],
           fill=(*accent, 210), width=16)
    d.line([(cx - 54, cy + 30), (cx - 20, cy - 30), (cx + 8, cy + 12), (cx + 46, cy - 52)],
           fill=(255, 255, 255, 240), width=4)
    # electrodes
    for ex in (cx - 82, cx + 74):
        d.rounded_rectangle([ex - 16, cy + 22, ex + 16, cy + 62], radius=8,
                            fill=(*STEEL_LIGHT, 255), outline=(*INK, 255), width=3)
    # small gauge above
    img = draw_gauge(img, cx, cy - 132, 42, accent, 0.78, 5)
    return img


# --------------------------------------------------------------------------
# manual bottle loader (1x2)
# --------------------------------------------------------------------------
def _bottle_path(cx: float, bottom: float, width: float, height: float):
    """Return a symmetric, classic glass-bottle outline as a list of points."""
    half = width / 2.0
    body_top = bottom - height * 0.55
    neck_base = bottom - height * 0.70
    neck_top = bottom - height * 0.90
    lip = bottom - height
    neck = half * 0.34
    pts = []
    # body sides and rounded bottom
    pts += [(cx - half, body_top)]
    pts += [(cx - half, bottom - 26), (cx - half + 14, bottom - 8),
            (cx - half + 34, bottom), (cx + half - 34, bottom),
            (cx + half - 14, bottom - 8), (cx + half, bottom - 26)]
    pts += [(cx + half, body_top)]
    # smooth shoulder into the neck
    pts += [(cx + half * 0.96, body_top - 34), (cx + half * 0.80, body_top - 64),
            (cx + half * 0.55, body_top - 84), (cx + neck + 8, neck_base - 20),
            (cx + neck, neck_base)]
    # neck and lip
    pts += [(cx + neck, neck_top), (cx + neck + 10, neck_top - 10),
            (cx + neck + 10, lip + 18), (cx + neck - 2, lip),
            (cx - neck + 2, lip), (cx - neck - 10, lip + 18),
            (cx - neck - 10, neck_top - 10), (cx - neck, neck_top)]
    # mirror shoulder
    pts += [(cx - neck, neck_base), (cx - neck - 8, neck_base - 20),
            (cx - half * 0.55, body_top - 84), (cx - half * 0.80, body_top - 64),
            (cx - half * 0.96, body_top - 34)]
    return pts


def bottler_image() -> Image.Image:
    W, H = 1024, 2048
    img = new_canvas(W, H)
    accent = ACCENTS["liquid"]
    teal = ACCENTS["gas"]
    d = ImageDraw.Draw(img)

    # ground shadow
    shadow = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    ImageDraw.Draw(shadow).ellipse([80, 1860, 944, 2050], fill=(0, 0, 0, 135))
    img = comp(img, shadow.filter(ImageFilter.GaussianBlur(32)))

    # feet
    for box in ((146, 1860, 366, 1984), (658, 1860, 878, 1984)):
        img = rounded_panel(img, box, 24, (68, 80, 92), (22, 28, 36), 3, bevel=5)
        draw_hex_bolt(img, (box[0] + box[2]) / 2, box[3] - 30, 15, 7)
    d = ImageDraw.Draw(img)

    # body: dark side then painted face
    side_box = (146, 190, 910, 1910)
    img = fill_solid(img, mask_round_rect((W, H), side_box, 104), (9, 13, 19))
    body_box = (130, 170, 894, 1890)
    body_mask = mask_round_rect((W, H), body_box, 104)
    img = fill_gradient(img, body_mask, (38, 86, 96), (14, 32, 41), 0.82)
    # broad painted centre light
    broad = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    ImageDraw.Draw(broad).rounded_rectangle([240, 210, 784, 1840], radius=80,
                                            fill=(96, 196, 196, 26))
    broad = broad.filter(ImageFilter.GaussianBlur(34))
    broad.putalpha(ImageChops.multiply(broad.getchannel("A"), body_mask))
    img = comp(img, broad)
    img = add_brushed(img, body_mask, 101, 520, vertical=True, alpha=13)
    img = add_noise(img, body_mask, 102, 20, 26)
    img = rounded_panel(img, body_box, 104, (0, 0, 0), (0, 0, 0), 103,
                        bevel=8) if False else img
    # body outline + bevel
    d = ImageDraw.Draw(img)
    d.rounded_rectangle(body_box, radius=104, outline=(*INK, 255), width=10)
    d.line([(240, 182), (784, 182)], fill=(*STEEL_HI, 100), width=4)
    d.line([(142, 300), (142, 1760)], fill=(*teal, 105), width=4)
    d.line([(884, 300), (884, 1760)], fill=(*INK, 170), width=4)
    # inset seams
    d.rounded_rectangle([164, 204, 860, 1856], radius=88,
                        outline=(10, 17, 23, 170), width=4)
    d.rounded_rectangle([172, 212, 852, 1848], radius=82,
                        outline=(*STEEL_LIGHT, 45), width=2)

    # top hatch / filler head
    img = rounded_panel(img, (300, 88, 724, 196), 42, (156, 174, 192), (58, 71, 86), 110, bevel=6)
    for bx in (340, 684):
        draw_hex_bolt(img, bx, 142, 18, 111)
    img = draw_pipe(img, 512, 38, 512, 190, 92, 112)
    img = draw_flange(img, 512, 92, 168, 42, 113)
    # top valve wheel
    d = ImageDraw.Draw(img)
    cx, cy, r = 650, 120, 55
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=(*BRASS_DARK, 255),
              outline=(*INK, 255), width=5)
    d.ellipse([cx - r * 0.76, cy - r * 0.76, cx + r * 0.76, cy + r * 0.76],
              fill=(*BRASS_MID, 255))
    for k in range(4):
        a = k * math.pi / 2 + 0.3
        d.line([(cx, cy), (cx + math.cos(a) * r * 0.8, cy + math.sin(a) * r * 0.8)],
               fill=(*BRASS_LIGHT, 255), width=10)
    d.ellipse([cx - 15, cy - 15, cx + 15, cy + 15], fill=(*BRASS_DARK, 255),
              outline=(*INK, 255), width=3)
    d.rounded_rectangle([552, 108, 606, 132], radius=8, fill=(*STEEL_MID, 255),
                        outline=(*INK, 255), width=3)
    # pressure gauge left of the filler
    img = draw_gauge(img, 374, 118, 50, teal, 0.54, 114)

    # ---- window frame ----------------------------------------------------
    frame_box = (196, 292, 828, 1216)
    img = rounded_panel(img, frame_box, 92, (142, 160, 178), (46, 58, 72), 120, bevel=8)
    # bolts around the frame
    for bx in (248, 776):
        for by in (350, 520, 690, 860, 1030, 1170):
            draw_hex_bolt(img, bx, by, 15, 121 + bx + by)
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([232, 328, 792, 1180], radius=70, outline=(*INK, 255), width=8)
    d.rounded_rectangle([238, 334, 786, 1174], radius=66,
                        outline=(*STEEL_HI, 80), width=3)

    # inner chamber
    cham_box = (244, 340, 780, 1168)
    cham_mask = mask_round_rect((W, H), cham_box, 62)
    img = fill_gradient(img, cham_mask, (15, 26, 34), (4, 7, 11), 1.0)
    img = add_noise(img, cham_mask, 130, 20, 18)
    # back ribs
    d = ImageDraw.Draw(img)
    for x in range(286, 760, 58):
        d.line([(x, 360), (x, 1148)], fill=(30, 43, 52, 130), width=5)
        d.line([(x + 3, 360), (x + 3, 1148)], fill=(90, 120, 132, 30), width=1)
    # interior top light
    light = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    ImageDraw.Draw(light).ellipse([300, 340, 724, 520], fill=(180, 240, 255, 40))
    light = light.filter(ImageFilter.GaussianBlur(30))
    light.putalpha(ImageChops.multiply(light.getchannel("A"), cham_mask))
    img = comp(img, light)

    # filling arm from the chamber ceiling to the bottle mouth
    feed_x = 512
    img = draw_pipe(img, feed_x, 350, feed_x, 472, 28, 139)
    img = draw_flange(img, feed_x, 392, 76, 22, 139)
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([feed_x + 14, 380, feed_x + 48, 402], radius=8,
                        fill=(*STEEL_MID, 255), outline=(*INK, 255), width=3)
    vx, vy, vr = feed_x + 68, 392, 24
    d.ellipse([vx - vr, vy - vr, vx + vr, vy + vr], fill=(*BRASS_DARK, 255),
              outline=(*INK, 255), width=4)
    for k in range(3):
        a = k * math.pi / 3 + 0.2
        d.line([(vx, vy), (vx + math.cos(a) * vr * 0.75, vy + math.sin(a) * vr * 0.75)],
               fill=(*BRASS_LIGHT, 255), width=5)
    d.ellipse([vx - 7, vy - 7, vx + 7, vy + 7], fill=(*BRASS_MID, 255),
              outline=(*INK, 255), width=2)

    # ---- bottle ----------------------------------------------------------
    bcx, bottom = 512, 1084
    bw, bh = 272, 520
    bottle_pts = _bottle_path(bcx, bottom, bw, bh)
    bmask = mask_polygon((W, H), bottle_pts)
    # glass body
    img = fill_gradient(img, bmask, (120, 206, 224), (24, 74, 96), 1.3)
    # liquid inside (clip a lower body rectangle to the bottle)
    liquid = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    ld = ImageDraw.Draw(liquid)
    lvl = 860
    ld.rounded_rectangle([bcx - bw / 2 + 6, lvl, bcx + bw / 2 - 6, bottom - 12],
                         radius=26, fill=(*mix(accent, (12, 44, 80), 0.42), 225))
    ld.ellipse([bcx - bw / 2 + 6, lvl - 25, bcx + bw / 2 - 6, lvl + 25],
               fill=(*mix(accent, (235, 252, 255), 0.36), 235),
               outline=(255, 255, 255, 210), width=4)
    # bubbles + current
    rng = random.Random(140)
    for _ in range(18):
        bx = rng.uniform(bcx - bw / 2 + 30, bcx + bw / 2 - 30)
        by = rng.uniform(lvl + 30, bottom - 40)
        br = rng.uniform(3, 9)
        ld.ellipse([bx - br, by - br, bx + br, by + br],
                   outline=(220, 250, 255, 120), width=2)
    for k in range(4):
        yy = lvl + 38 + k * 46
        ld.arc([bcx - 96 + k * 6, yy - 18, bcx + 20 + k * 6, yy + 18],
               200, 340, fill=(230, 255, 255, 42), width=4)
    liquid.putalpha(ImageChops.multiply(liquid.getchannel("A"), bmask))
    img = comp(img, liquid)
    # bottle outline, glass highlights and label
    d = ImageDraw.Draw(img)
    d.line(bottle_pts + [bottle_pts[0]], fill=(*INK, 255), width=9)
    d.line(bottle_pts + [bottle_pts[0]], fill=(230, 250, 255, 80), width=3)
    # left specular
    d.line([(bcx - bw / 2 + 22, 760), (bcx - bw / 2 + 22, 1030)],
           fill=(255, 255, 255, 95), width=14)
    d.line([(bcx - bw / 2 + 46, 760), (bcx - bw / 2 + 46, 1030)],
           fill=(255, 255, 255, 40), width=5)
    d.line([(bcx + bw / 2 - 26, 780), (bcx + bw / 2 - 26, 1040)],
           fill=(10, 30, 45, 120), width=10)
    # neck highlight
    d.rectangle([bcx - 28, 594, bcx - 16, 696], fill=(255, 255, 255, 70))
    d.rectangle([bcx + 18, 600, bcx + 26, 690], fill=(20, 60, 80, 110))
    # label
    lab = (bcx - 88, 880, bcx + 88, 1018)
    img = rounded_panel(img, lab, 12, (230, 226, 196), (174, 178, 152), 141, bevel=3)
    d = ImageDraw.Draw(img)
    d.rectangle([lab[0] + 10, lab[1] + 10, lab[2] - 10, lab[3] - 10],
                outline=(42, 72, 66, 160), width=2)
    d.polygon([(bcx, lab[1] + 28), (bcx - 22, lab[1] + 66), (bcx, lab[1] + 104),
               (bcx + 22, lab[1] + 66)],
              fill=(*teal, 220), outline=(24, 62, 62, 255), width=2)
    d.line([(bcx - 50, lab[3] - 24), (bcx + 50, lab[3] - 24)],
           fill=(52, 78, 72, 160), width=3)
    # cap
    img = rounded_panel(img, (bcx - 62, 486, bcx + 62, 558), 16,
                        (118, 210, 214), (30, 92, 104), 142, bevel=4)
    d = ImageDraw.Draw(img)
    for x in range(bcx - 42, bcx + 43, 12):
        d.line([(x, 500), (x, 544)], fill=(20, 64, 74, 130), width=2)
    # filling nozzle and drip
    d = ImageDraw.Draw(img)
    d.polygon([(bcx - 34, 420), (bcx + 34, 420), (bcx + 16, 500), (bcx - 16, 500)],
              fill=(*STEEL_LIGHT, 255), outline=(*INK, 255))
    d.rounded_rectangle([bcx - 22, 496, bcx + 22, 520], radius=8,
                        fill=(30, 36, 42, 255), outline=(*INK, 255), width=3)
    d.line([(bcx, 520), (bcx, 548)], fill=(*accent, 220), width=5)
    d.ellipse([bcx - 5, 548, bcx + 5, 558], fill=(*accent, 210))

    # filling stand
    img = rounded_panel(img, (bcx - 148, 1074, bcx + 148, 1132), 12,
                        (112, 128, 142), (36, 46, 58), 143, bevel=4)
    d = ImageDraw.Draw(img)
    for x in range(bcx - 120, bcx + 121, 24):
        d.line([(x, 1084), (x, 1122)], fill=(*INK, 160), width=5)
    d.line([(bcx - 154, 1080), (bcx - 154, 1160)], fill=(*STEEL_MID, 255), width=10)
    d.line([(bcx + 154, 1080), (bcx + 154, 1160)], fill=(*STEEL_MID, 255), width=10)
    draw_screw(img, bcx - 154, 1148, 8, 0.2)
    draw_screw(img, bcx + 154, 1148, 8, 1.3)

    # glass tint + reflections over the chamber
    glass = Image.new("RGBA", (W, H), (120, 220, 230, 20))
    glass.putalpha(ImageChops.multiply(glass.getchannel("A"), cham_mask))
    img = comp(img, glass)
    gl = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    gd = ImageDraw.Draw(gl)
    gd.polygon([(280, 366), (390, 350), (500, 1140), (390, 1158)], fill=(255, 255, 255, 14))
    gd.polygon([(330, 366), (378, 360), (470, 1120), (420, 1128)], fill=(255, 255, 255, 22))
    gd.arc([480, 380, 780, 700], 180, 260, fill=(255, 255, 255, 55), width=8)
    gl.putalpha(ImageChops.multiply(gl.getchannel("A"), cham_mask))
    img = comp(img, gl)
    # inner window edge shadow
    d = ImageDraw.Draw(img)
    d.rounded_rectangle(cham_box, radius=62, outline=(*INK, 220), width=10)
    d.rounded_rectangle([cham_box[0] + 8, cham_box[1] + 8, cham_box[2] - 8, cham_box[3] - 8],
                        radius=56, outline=(220, 248, 255, 90), width=3)

    # ---- control panel ---------------------------------------------------
    img = rounded_panel(img, (178, 1244, 846, 1588), 36,
                        (104, 120, 136), (38, 48, 60), 150, bevel=6)
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([196, 1262, 828, 1570], radius=28,
                        outline=(10, 17, 24, 180), width=3)
    # lever
    d.line([(300, 1520), (300, 1350)], fill=(*INK, 255), width=26)
    d.line([(300, 1510), (300, 1360)], fill=(*STEEL_MID, 255), width=14)
    d.line([(286, 1510), (286, 1360)], fill=(*STEEL_HI, 95), width=4)
    d.line([(300, 1400), (392, 1322)], fill=(*INK, 255), width=22)
    d.line([(300, 1400), (392, 1322)], fill=(*STEEL_LIGHT, 255), width=12)
    d.ellipse([370, 1298, 424, 1352], fill=(*ACCENTS["vaporize"], 255),
              outline=(*INK, 255), width=5)
    d.ellipse([382, 1311, 410, 1339], fill=(255, 230, 190, 180))
    d.ellipse([290, 1500, 330, 1540], fill=(*STEEL_SIDE, 255), outline=(*INK, 255), width=3)
    # gauges
    img = draw_gauge(img, 500, 1378, 62, teal, 0.58, 151)
    img = draw_gauge(img, 646, 1378, 58, accent, 0.42, 152)
    # buttons and lamps
    d = ImageDraw.Draw(img)
    for k, (col, label) in enumerate(((teal, "A"), (accent, "B"), (ACCENTS["vaporize"], "C"))):
        cx, cy = 760, 1318 + k * 76
        d.ellipse([cx - 24, cy - 24, cx + 24, cy + 24],
                  fill=(*STEEL_SIDE, 255), outline=(*INK, 255), width=4)
        d.ellipse([cx - 16, cy - 16, cx + 16, cy + 16],
                  fill=(*col, 230), outline=(20, 30, 34, 255), width=2)
        d.arc([cx - 13, cy - 13, cx + 13, cy + 13], 200, 310,
              fill=(255, 255, 255, 190), width=3)
    # etched label strip
    img = draw_label(img, (430, 1452, 710, 1500), "BOTTLE LOADER", teal, 153)
    # vents on the panel
    img = draw_vent(img, (218, 1478, 360, 1544), 7, 154)

    # ---- lower access panel ---------------------------------------------
    img = rounded_panel(img, (190, 1620, 834, 1840), 28,
                        (78, 92, 106), (28, 36, 46), 160, bevel=5)
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([208, 1638, 816, 1822], radius=22,
                        outline=(10, 16, 22, 180), width=3)
    for bx in (240, 784):
        draw_hex_bolt(img, bx, 1660, 12, 161)
        draw_hex_bolt(img, bx, 1802, 12, 162)
    # hazard stripe plate
    hz = (280, 1680, 460, 1750)
    img = rounded_panel(img, hz, 8, (214, 190, 88), (120, 94, 30), 163, bevel=3)
    d = ImageDraw.Draw(img)
    for k in range(-2, 8):
        x0 = hz[0] + k * 42
        d.line([(x0, hz[3] - 4), (x0 + 42, hz[1] + 4)],
               fill=(30, 30, 24, 120), width=12)
    d.rounded_rectangle(hz, radius=8, outline=(*INK, 200), width=2)
    # vents and drain
    img = draw_vent(img, (500, 1678, 700, 1762), 8, 164)
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([746, 1786, 806, 1822], radius=10,
                        fill=(*STEEL_MID, 255), outline=(*INK, 255), width=3)
    d.ellipse([760, 1790, 792, 1822], fill=(*BRASS_MID, 255),
              outline=(*INK, 255), width=2)

    # ---- side service pipe ----------------------------------------------
    img = draw_pipe(img, 884, 236, 884, 1710, 34, 170)
    for y in (300, 560, 900, 1240, 1580):
        box = (858, y - 18, 910, y + 18)
        img = rounded_panel(img, box, 8, (126, 144, 164), (44, 56, 70), y, bevel=3)
        draw_screw(img, 872, y, 5, 0.2)
        draw_screw(img, 900, y, 5, 1.1)
    img = draw_gauge(img, 884, 760, 46, teal, 0.64, 171)
    draw_hex_bolt(img, 884, 1710, 16, 172)

    # ---- weathering ------------------------------------------------------
    d = ImageDraw.Draw(img)
    rng = random.Random(180)
    for _ in range(46):
        x = rng.uniform(160, 870)
        y = rng.uniform(220, 1840)
        if not (0 <= x < W and 0 <= y < H):
            continue
        if img.getpixel((int(x), int(y)))[3] < 10:
            continue
        ln = rng.uniform(6, 30)
        a = math.radians(rng.uniform(-100, -20))
        d.line([(x, y), (x + math.cos(a) * ln, y + math.sin(a) * ln)],
               fill=(220, 235, 245, rng.randint(18, 48)), width=1)
    body_alpha = img.getchannel("A")
    img = add_grime(img, body_alpha, 181, 1)
    img = add_noise(img, body_alpha, 182, 10, 50)
    solid = mask_polygon((W, H), [(76, 0), (964, 0), (964, 2000), (76, 2000)])
    return solidify_alpha(img, solid, 90)


def bottler_icon(image: Image.Image) -> Image.Image:
    """A square close-up of the filling chamber for the build menu."""
    w, h = image.size
    # image is 256x512 in the final world atlas; take the window/top section
    crop = image.crop((0, int(h * 0.06), w, int(h * 0.06) + w))
    icon = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    crop.thumbnail((256, 256), Image.LANCZOS)
    icon.paste(crop, ((256 - crop.width) // 2, (256 - crop.height) // 2))
    return icon


# --------------------------------------------------------------------------
# element terminal (2x2)
# --------------------------------------------------------------------------
def _screen_glyph(img, box, kind: str, accent) -> Image.Image:
    x0, y0, x1, y1 = box
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    d = ImageDraw.Draw(img)

    # faint grid + scanlines (on a layer so they blend with the dark glass)
    grid = new_canvas(img.size[0], img.size[1])
    gd = ImageDraw.Draw(grid)
    for y in range(int(y0) + 6, int(y1), 5):
        gd.line([(x0 + 4, y), (x1 - 4, y)], fill=(120, 180, 200, 22), width=1)
    for x in range(int(x0) + 20, int(x1), 40):
        gd.line([(x, y0 + 4), (x, y1 - 4)], fill=(120, 180, 200, 15), width=1)
    img = comp(img, grid)
    d = ImageDraw.Draw(img)

    if kind == "decompose":
        # water molecule on the left, separated gases on the right
        def atom(px, py, r, col):
            d.ellipse([px - r, py - r, px + r, py + r],
                      fill=(*mix(col, (255, 255, 255), 0.20), 235),
                      outline=(220, 255, 255, 190), width=3)
            d.arc([px - r * 0.82, py - r * 0.82, px + r * 0.82, py + r * 0.82],
                  195, 315, fill=(255, 255, 255, 180), width=max(2, int(r * 0.18)))
        ox, oy = cx - 92, cy + 8
        atom(ox, oy, 34, (90, 170, 255))
        atom(ox - 46, oy - 42, 21, (220, 245, 255))
        atom(ox + 46, oy - 42, 21, (220, 245, 255))
        d.line([(ox - 34, oy - 32), (ox - 18, oy - 16)], fill=(190, 240, 255, 210), width=5)
        d.line([(ox + 34, oy - 32), (ox + 18, oy - 16)], fill=(190, 240, 255, 210), width=5)
        # split arrow
        ax = cx + 4
        d.line([(ax, cy - 34), (ax, cy + 48)], fill=(*accent, 220), width=5)
        d.polygon([(ax - 13, cy + 30), (ax + 13, cy + 30), (ax, cy + 50)],
                  fill=(*accent, 225))
        # products
        bx = cx + 96
        atom(bx, cy - 42, 18, (110, 230, 200))
        atom(bx + 38, cy - 8, 18, (110, 230, 200))
        d.ellipse([bx + 12, cy + 34, bx + 72, cy + 94], outline=(210, 240, 255, 170), width=4)
        d.arc([bx + 20, cy + 42, bx + 64, cy + 86], 200, 330, fill=(255, 255, 255, 100), width=3)
        # little bubbles
        for i, (px, py, r) in enumerate(((cx + 38, cy + 78, 7), (cx + 58, cy + 62, 4),
                                          (cx - 28, cy + 76, 5))):
            d.ellipse([px - r, py - r, px + r, py + r], outline=(190, 245, 255, 120), width=2)
    elif kind == "solidify":
        # faceted ice crystal
        pts = []
        for i in range(6):
            a = -math.pi / 2 + i * math.pi / 3
            pts.append((cx + math.cos(a) * 82, cy + math.sin(a) * 82))
        d.polygon(pts, fill=(*mix(accent, (255, 255, 255), 0.25), 120),
                  outline=(235, 250, 255, 235))
        for i in range(6):
            a = -math.pi / 2 + i * math.pi / 3
            px, py = cx + math.cos(a) * 82, cy + math.sin(a) * 82
            d.line([(cx, cy), (px, py)], fill=(225, 248, 255, 150), width=4)
            for t in (0.48, 0.72):
                mx = cx + math.cos(a) * 82 * t
                my = cy + math.sin(a) * 82 * t
                d.line([(mx, my), (mx + math.cos(a + 1.0) * 24, my + math.sin(a + 1.0) * 24)],
                       fill=(225, 248, 255, 150), width=3)
                d.line([(mx, my), (mx + math.cos(a - 1.0) * 24, my + math.sin(a - 1.0) * 24)],
                       fill=(225, 248, 255, 150), width=3)
        d.ellipse([cx - 14, cy - 14, cx + 14, cy + 14], fill=(255, 255, 255, 220))
        # frost speckles
        rng = random.Random(46)
        for _ in range(36):
            px = rng.uniform(x0 + 20, x1 - 20)
            py = rng.uniform(y0 + 20, y1 - 20)
            r = rng.uniform(1, 3)
            d.ellipse([px - r, py - r, px + r, py + r], fill=(235, 250, 255, 110))
    elif kind == "liquefy":
        # gas cloud at the top, condenser coil, droplet at the bottom
        for i, (px, py, r) in enumerate(((cx - 48, cy - 70, 30), (cx - 6, cy - 84, 24),
                                          (cx + 36, cy - 68, 27), (cx + 72, cy - 48, 18))):
            d.ellipse([px - r, py - r, px + r, py + r],
                      fill=(160, 210, 240, 55), outline=(210, 240, 255, 80), width=2)
        # copper coil
        for k in range(4):
            ry = cy - 22 + k * 18
            d.arc([cx - 84, ry - 12, cx + 84, ry + 12], 200, 340,
                  fill=(*BRASS_MID, 210), width=8)
        # falling droplets
        for px, py, r in ((cx - 40, cy + 40, 8), (cx + 24, cy + 66, 6)):
            d.polygon([(px, py - r * 2), (px - r, py + r), (px, py + r * 1.6),
                       (px + r, py + r)], fill=(*accent, 220), outline=(220, 250, 255, 200))
        # pool
        d.ellipse([cx - 100, cy + 86, cx + 100, cy + 122],
                  fill=(*mix(accent, (10, 40, 70), 0.35), 210),
                  outline=(220, 250, 255, 170), width=3)
        d.arc([cx - 92, cy + 90, cx + 92, cy + 118], 200, 340,
              fill=(255, 255, 255, 110), width=3)
    else:  # vaporize
        # boiling pool with heating coils and rising steam
        d.ellipse([cx - 108, cy + 56, cx + 108, cy + 120],
                  fill=(*mix(accent, (80, 30, 10), 0.45), 235),
                  outline=(255, 226, 180, 200), width=4)
        for k in range(4):
            ry = cy + 106 - k * 12
            d.arc([cx - 84, ry - 10, cx + 84, ry + 10], 200, 340,
                  fill=(255, 150, 62, 220 - k * 20), width=6)
        rng = random.Random(47)
        for k in range(6):
            bx = cx - 76 + k * 30
            by = cy + 70 - (k % 3) * 6
            r = rng.uniform(4, 10)
            d.ellipse([bx - r, by - r, bx + r, by + r], outline=(255, 220, 160, 120), width=2)
        # steam curls
        for k in range(4):
            x = cx - 66 + k * 44
            for s in range(3):
                yy = cy + 14 - s * 34
                d.arc([x - 26 + s * 3, yy - 32, x + 26 + s * 3, yy + 16],
                      195, 330, fill=(255, 242, 220, 120 - s * 26), width=5)
        # heat waves
        for k in range(3):
            d.arc([cx - 132 + k * 10, cy - 52, cx - 52 + k * 10, cy + 32],
                  190, 330, fill=(*accent, 90), width=4)
            d.arc([cx + 52 + k * 10, cy - 52, cx + 132 + k * 10, cy + 32],
                  190, 330, fill=(*accent, 90), width=4)
    return img


def terminal_image(kind: str) -> Image.Image:
    S = 1024
    img = new_canvas(S, S)
    accent = ACCENTS[kind]
    d = ImageDraw.Draw(img)

    # ground shadow
    shadow = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    ImageDraw.Draw(shadow).ellipse([70, 850, 954, 1020], fill=(0, 0, 0, 135))
    img = comp(img, shadow.filter(ImageFilter.GaussianBlur(28)))

    # ---- base plinth -----------------------------------------------------
    img = rounded_panel(img, (86, 806, 938, 946), 34,
                        (112, 128, 146), (34, 43, 55), 200, bevel=7)
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([108, 826, 916, 926], radius=26,
                        outline=(10, 16, 22, 180), width=3)
    for bx in (142, 882):
        draw_hex_bolt(img, bx, 862, 15, 201)
        draw_hex_bolt(img, bx, 912, 15, 202)
    # feet
    for box in ((108, 922, 254, 986), (770, 922, 916, 986)):
        img = rounded_panel(img, box, 14, (66, 78, 92), (22, 28, 36), 203, bevel=4)
        draw_screw(img, (box[0] + box[2]) / 2, box[3] - 18, 5, 0.4)

    # ---- left reaction tank ---------------------------------------------
    # side thickness
    img = fill_solid(img, mask_round_rect((S, S), (150, 264, 470, 832), 82), (9, 13, 19))
    tank_mask = mask_round_rect((S, S), (130, 232, 452, 820), 76)
    dome = mask_ellipse((S, S), [130, 178, 452, 306])
    tank_mask = ImageChops.lighter(tank_mask, dome)
    img = fill_gradient(img, tank_mask, (68, 96, 108), (20, 34, 44), 0.85)
    img = add_brushed(img, tank_mask, 210, 320, vertical=True, alpha=12)
    img = add_noise(img, tank_mask, 211, 20, 22)
    # tank bevel
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([130, 232, 452, 820], radius=76, outline=(*INK, 255), width=9)
    d.arc([132, 180, 450, 304], 185, 355, fill=(*STEEL_HI, 115), width=5)
    d.line([(146, 280), (146, 760)], fill=(*accent, 80), width=5)
    d.line([(438, 280), (438, 760)], fill=(*INK, 160), width=5)
    d.rounded_rectangle([150, 252, 432, 800], radius=62,
                        outline=(10, 17, 23, 160), width=3)

    # metal straps
    for y in (330, 500, 670, 790):
        img = rounded_panel(img, (118, y, 464, y + 34), 14,
                            (138, 156, 174), (46, 58, 72), y, bevel=4)
        draw_hex_bolt(img, 142, y + 17, 10, y + 1)
        draw_hex_bolt(img, 440, y + 17, 10, y + 2)

    # tank window
    frame_box = (172, 356, 410, 728)
    img = rounded_panel(img, frame_box, 46, (150, 168, 186), (48, 60, 74), 220, bevel=6)
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([188, 372, 394, 712], radius=36, outline=(*INK, 255), width=7)
    win_mask = mask_round_rect((S, S), (194, 378, 388, 706), 32)
    img = fill_gradient(img, win_mask, (10, 24, 34), (3, 7, 12), 1.0)
    img = add_noise(img, win_mask, 221, 18, 16)

    # contents clipped to the window
    contents = new_canvas(S, S)
    cd = ImageDraw.Draw(contents)
    if kind == "decompose":
        # water, two electrodes, bubbles
        cd.rounded_rectangle([210, 500, 370, 696], radius=18,
                             fill=(*mix(ACCENTS["liquid"], (10, 40, 70), 0.35), 230))
        cd.ellipse([210, 480, 370, 520], fill=(110, 190, 240, 230),
                   outline=(225, 250, 255, 190), width=3)
        for x in (248, 332):
            cd.rounded_rectangle([x - 7, 410, x + 7, 690], radius=7,
                                 fill=(*STEEL_LIGHT, 255), outline=(*INK, 255), width=2)
            cd.ellipse([x - 12, 400, x + 12, 424], fill=(*BRASS_MID, 255),
                       outline=(*INK, 255), width=2)
        rng = random.Random(58)
        for _ in range(24):
            bx = rng.uniform(218, 362)
            by = rng.uniform(520, 686)
            br = rng.uniform(2, 8)
            cd.ellipse([bx - br, by - br, bx + br, by + br],
                       outline=(200, 245, 255, 135), width=2)
        # gas bubbles rising above the water
        for i in range(8):
            bx = 232 + (i * 41) % 130
            by = 430 + (i * 29) % 62
            br = 5 + (i * 3) % 9
            cd.ellipse([bx - br, by - br, bx + br, by + br],
                       outline=(160, 240, 220, 120), width=2)
        cd.line([(289, 420), (289, 690)], fill=(30, 50, 60, 120), width=3)
    elif kind == "solidify":
        # frost, ice blocks, cooling coils
        cd.rounded_rectangle([204, 470, 378, 700], radius=20,
                             fill=(*mix(ACCENTS["solidify"], (18, 60, 92), 0.45), 220))
        rng = random.Random(59)
        for _ in range(9):
            bx = rng.uniform(220, 360)
            by = rng.uniform(440, 690)
            sz = rng.uniform(18, 42)
            pts = [(bx + math.cos(-math.pi / 2 + k * math.pi / 3) * sz,
                    by + math.sin(-math.pi / 2 + k * math.pi / 3) * sz) for k in range(6)]
            cd.polygon(pts, fill=(210, 242, 255, 150), outline=(245, 253, 255, 220))
            cd.line([pts[0], pts[3]], fill=(255, 255, 255, 120), width=2)
        for k in range(4):
            x = 212 + k * 44
            cd.line([(x, 400), (x, 700)], fill=(*BRASS_MID, 200), width=7)
        # frost at the edges
        for _ in range(50):
            px = rng.uniform(198, 384)
            py = rng.uniform(382, 702)
            r = rng.uniform(1, 3)
            cd.ellipse([px - r, py - r, px + r, py + r], fill=(240, 252, 255, 100))
    elif kind == "liquefy":
        # vapor top, copper coil, condensing droplets, liquid bottom
        for i, (px, py, r) in enumerate(((250, 430, 32), (300, 408, 26), (342, 432, 28))):
            cd.ellipse([px - r, py - r, px + r, py + r],
                       fill=(170, 215, 245, 55), outline=(220, 245, 255, 80), width=2)
        for k in range(5):
            ry = 470 + k * 24
            cd.arc([206, ry - 14, 376, ry + 14], 200, 340,
                   fill=(*BRASS_MID, 210), width=8)
        for i in range(6):
            px = 242 + (i * 37) % 120
            py = 560 + (i * 31) % 80
            r = 5 + (i % 3) * 3
            cd.polygon([(px, py - r * 2), (px - r, py + r), (px, py + r * 1.5),
                        (px + r, py + r)], fill=(*accent, 220),
                       outline=(225, 250, 255, 180))
        cd.rounded_rectangle([202, 646, 380, 702], radius=16,
                             fill=(*mix(accent, (12, 44, 80), 0.35), 235))
        cd.ellipse([202, 632, 380, 668], fill=(160, 215, 250, 230),
                   outline=(230, 250, 255, 190), width=3)
    else:  # vaporize
        cd.rounded_rectangle([202, 574, 380, 704], radius=18,
                             fill=(*mix(accent, (90, 36, 10), 0.50), 240))
        cd.ellipse([202, 558, 380, 594], fill=(255, 190, 120, 200),
                   outline=(255, 232, 190, 190), width=3)
        for k in range(4):
            ry = 684 - k * 16
            cd.arc([216, ry - 10, 366, ry + 10], 200, 340,
                   fill=(255, 150, 62, 230 - k * 18), width=7)
        rng = random.Random(61)
        for _ in range(18):
            bx = rng.uniform(216, 366)
            by = rng.uniform(580, 690)
            br = rng.uniform(2, 7)
            cd.ellipse([bx - br, by - br, bx + br, by + br],
                       outline=(255, 228, 170, 120), width=2)
        for k in range(5):
            x = 220 + k * 38
            for s in range(3):
                yy = 530 - s * 44
                cd.arc([x - 22 + s * 4, yy - 30, x + 22 + s * 4, yy + 16],
                       195, 330, fill=(255, 244, 225, 120 - s * 24), width=5)
    contents = clip_layer(contents, win_mask)
    img = comp(img, contents)

    # glass reflections + rim
    refl = new_canvas(S, S)
    rd = ImageDraw.Draw(refl)
    rd.polygon([(210, 390), (250, 384), (330, 700), (290, 704)],
               fill=(255, 255, 255, 16))
    rd.polygon([(266, 390), (288, 386), (352, 690), (330, 694)],
               fill=(255, 255, 255, 24))
    rd.arc([262, 400, 380, 560], 185, 275, fill=(255, 255, 255, 55), width=7)
    refl = clip_layer(refl, win_mask)
    img = comp(img, refl)
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([194, 378, 388, 706], radius=32,
                        outline=(*INK, 220), width=9)
    d.rounded_rectangle([200, 384, 382, 700], radius=28,
                        outline=(220, 248, 255, 80), width=3)

    # tank gauge + valve wheel on the dome
    img = draw_gauge(img, 304, 286, 46, accent, 0.56, 230)
    d = ImageDraw.Draw(img)
    cx, cy, r = 190, 286, 40
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=(*BRASS_DARK, 255),
              outline=(*INK, 255), width=4)
    for k in range(4):
        a = k * math.pi / 2 + 0.35
        d.line([(cx, cy), (cx + math.cos(a) * r * 0.78, cy + math.sin(a) * r * 0.78)],
               fill=(*BRASS_LIGHT, 255), width=8)
    d.ellipse([cx - 12, cy - 12, cx + 12, cy + 12], fill=(*BRASS_MID, 255),
              outline=(*INK, 255), width=3)

    # ---- console body ----------------------------------------------------
    img = fill_solid(img, mask_round_rect((S, S), (486, 316, 926, 846), 46), (9, 13, 19))
    con_mask = mask_round_rect((S, S), (470, 300, 910, 830), 44)
    img = fill_gradient(img, con_mask, (60, 76, 92), (20, 28, 38), 0.9)
    img = add_brushed(img, con_mask, 240, 220, alpha=12)
    img = add_noise(img, con_mask, 241, 18, 22)
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([470, 300, 910, 830], radius=44, outline=(*INK, 255), width=9)
    d.line([(492, 322), (888, 322)], fill=(*STEEL_HI, 105), width=4)
    d.line([(492, 808), (888, 808)], fill=(*INK, 180), width=4)
    d.rounded_rectangle([488, 318, 892, 812], radius=34,
                        outline=(10, 17, 23, 160), width=3)

    # screen frame
    img = rounded_panel(img, (500, 330, 880, 640), 30, (150, 168, 186), (46, 58, 72), 250, bevel=6)
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([518, 348, 862, 622], radius=20, outline=(*INK, 255), width=6)
    screen_mask = mask_round_rect((S, S), (524, 354, 856, 616), 16)
    img = fill_gradient(img, screen_mask, (9, 20, 28), (3, 7, 12), 1.0)
    img = add_noise(img, screen_mask, 251, 16, 18)
    # screen glow border
    glow = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    ImageDraw.Draw(glow).rounded_rectangle([520, 350, 860, 620], radius=18,
                                           outline=(*accent, 150), width=5)
    img = comp(img, glow.filter(ImageFilter.GaussianBlur(8)))
    # screen content
    img = _screen_glyph(img, (530, 360, 850, 610), kind, accent)
    # screen glass reflection + scan corner brackets
    refl = new_canvas(S, S)
    rd = ImageDraw.Draw(refl)
    rd.polygon([(548, 360), (610, 356), (556, 616), (500, 616)],
               fill=(190, 235, 255, 22))
    rd.polygon([(590, 358), (616, 357), (564, 612), (538, 612)],
               fill=(255, 255, 255, 20))
    rd.arc([540, 366, 760, 500], 185, 250, fill=(255, 255, 255, 55), width=6)
    refl = refl.filter(ImageFilter.GaussianBlur(5))
    refl = clip_layer(refl, screen_mask)
    img = comp(img, refl)
    d = ImageDraw.Draw(img)
    for bx, by, dx, dy in ((538, 368, 1, 1), (842, 368, -1, 1),
                            (538, 602, 1, -1), (842, 602, -1, -1)):
        d.line([(bx, by), (bx + dx * 26, by)], fill=(*accent, 255), width=3)
        d.line([(bx, by), (bx, by + dy * 26)], fill=(*accent, 255), width=3)
    # small readout bars
    bars = new_canvas(S, S)
    bd = ImageDraw.Draw(bars)
    for i in range(3):
        bd.rounded_rectangle([548, 574 + i * 10, 548 + 60 + i * 24, 580 + i * 10],
                             radius=3, fill=(*accent, 110 + i * 35))
    img = comp(img, clip_layer(bars, screen_mask))
    d = ImageDraw.Draw(img)

    # ---- control deck ----------------------------------------------------
    img = rounded_panel(img, (496, 660, 884, 812), 24,
                        (96, 112, 128), (32, 41, 52), 260, bevel=5)
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([510, 674, 870, 798], radius=18,
                        outline=(10, 16, 22, 170), width=3)
    # keyboard
    for row in range(3):
        for col in range(6):
            kx = 526 + col * 31
            ky = 690 + row * 30
            d.rounded_rectangle([kx, ky, kx + 25, ky + 22], radius=5,
                                fill=(44, 56, 70, 255), outline=(*INK, 230), width=2)
            d.line([(kx + 3, ky + 3), (kx + 22, ky + 3)],
                   fill=(*STEEL_HI, 80), width=1)
    # big buttons + lamps
    for k, col in enumerate((accent, ACCENTS["vaporize"])):
        bx, by = 740 + k * 58, 706
        d.ellipse([bx - 24, by - 24, bx + 24, by + 24],
                  fill=(*STEEL_SIDE, 255), outline=(*INK, 255), width=4)
        d.ellipse([bx - 16, by - 16, bx + 16, by + 16],
                  fill=(*col, 230), outline=(20, 30, 34, 255), width=2)
        d.arc([bx - 13, by - 13, bx + 13, by + 13], 200, 310,
              fill=(255, 255, 255, 180), width=3)
    # lever on the right
    d.line([(832, 780), (846, 712)], fill=(*INK, 255), width=18)
    d.line([(832, 780), (846, 712)], fill=(*STEEL_LIGHT, 255), width=10)
    d.ellipse([832, 696, 870, 734], fill=(*accent, 255),
              outline=(*INK, 255), width=4)
    d.ellipse([843, 707, 859, 723], fill=(255, 255, 255, 160))
    # engraved name plate
    label = {"decompose": "DECOMPOSE", "solidify": "SOLIDIFY",
             "liquefy": "LIQUEFY", "vaporize": "VAPORIZE"}[kind]
    img = draw_label(img, (620, 748, 800, 792), label, accent, 261)
    # vents under the deck
    img = draw_vent(img, (530, 756, 600, 796), 3, 262)

    # ---- top equipment ---------------------------------------------------
    d = ImageDraw.Draw(img)
    # main feed pipe from tank to console
    img = draw_pipe(img, 448, 600, 500, 600, 38, 270)
    img = draw_flange(img, 470, 600, 56, 74, 271)
    # tank top outlet
    img = draw_pipe(img, 290, 184, 290, 96, 54, 272)
    img = draw_flange(img, 290, 154, 130, 34, 273)
    if kind == "decompose":
        # two separated gas outlets
        for x, col in ((214, ACCENTS["gas"]), (366, ACCENTS["solidify"])):
            img = draw_pipe(img, x, 180, x, 66, 38, 274 + x)
            img = rounded_panel(img, (x - 40, 52, x + 40, 92), 12,
                                (150, 168, 186), (48, 60, 74), 275 + x, bevel=4)
            dd = ImageDraw.Draw(img)
            dd.line([(x - 26, 72), (x + 26, 72)], fill=(*col, 220), width=8)
    elif kind == "solidify":
        # cooling radiator / fan housing
        img = rounded_panel(img, (178, 116, 398, 226), 26,
                            (150, 172, 192), (48, 64, 80), 280, bevel=5)
        dd = ImageDraw.Draw(img)
        for k in range(4):
            dd.line([(200, 142 + k * 18), (376, 142 + k * 18)],
                    fill=(*INK, 150), width=4)
            dd.line([(200, 145 + k * 18), (376, 145 + k * 18)],
                    fill=(*STEEL_HI, 80), width=2)
        dd.arc([238, 116, 338, 216], 200, 340, fill=(*accent, 160), width=6)
    elif kind == "liquefy":
        # copper condenser coil above the tank
        for k in range(5):
            dd = ImageDraw.Draw(img)
            dd.arc([184, 96 + k * 18, 396, 196 + k * 18], 200, 340,
                   fill=(*BRASS_MID, 230), width=12)
            dd.arc([190, 100 + k * 18, 390, 192 + k * 18], 200, 340,
                   fill=(*BRASS_LIGHT, 120), width=3)
    else:
        # boiler chimney with heat glow
        img = draw_pipe(img, 290, 200, 290, 72, 72, 281)
        img = rounded_panel(img, (238, 46, 342, 104), 16,
                            (150, 168, 186), (48, 60, 74), 282, bevel=4)
        glow = soft_glow((S, S), [216, 80, 364, 230], ACCENTS["vaporize"], 26, 80)
        img = comp(img, glow)
        dd = ImageDraw.Draw(img)
        for k in range(3):
            dd.arc([230 + k * 14, 70 - k * 20, 350 + k * 14, 180 - k * 20],
                   195, 330, fill=(255, 240, 215, 60 - k * 14), width=5)

    # console beacon
    d = ImageDraw.Draw(img)
    beacon = (868, 306)
    img = comp(img, soft_glow((S, S), [beacon[0] - 46, beacon[1] - 46,
                                       beacon[0] + 46, beacon[1] + 46], accent, 20, 120))
    d = ImageDraw.Draw(img)
    d.line([(beacon[0], 320), (beacon[0], 286)], fill=(*STEEL_MID, 255), width=8)
    d.ellipse([beacon[0] - 13, beacon[1] - 20, beacon[0] + 13, beacon[1] + 6],
              fill=(*accent, 235), outline=(*INK, 255), width=3)
    d.arc([beacon[0] - 10, beacon[1] - 17, beacon[0] + 10, beacon[1] + 3],
          200, 310, fill=(255, 255, 255, 190), width=3)

    # ---- weathering ------------------------------------------------------
    alpha = img.getchannel("A")
    rng = random.Random(290)
    d = ImageDraw.Draw(img)
    for _ in range(55):
        x = rng.uniform(110, 920)
        y = rng.uniform(200, 940)
        if img.getpixel((int(x), int(y)))[3] < 10:
            continue
        ln = rng.uniform(5, 26)
        a = math.radians(rng.uniform(-100, -20))
        d.line([(x, y), (x + math.cos(a) * ln, y + math.sin(a) * ln)],
               fill=(220, 235, 245, rng.randint(16, 46)), width=1)
    img = add_grime(img, alpha, 291, 1)
    img = add_noise(img, alpha, 292, 9, 55)
    solid = mask_polygon((S, S), [(56, 0), (964, 0), (964, 1000), (56, 1000)])
    return solidify_alpha(img, solid, 90)


def terminal_icon(image: Image.Image) -> Image.Image:
    """Crop to the machine and fit it into a square build-menu icon."""
    bbox = image.getbbox()
    if bbox:
        crop = image.crop(bbox)
    else:
        crop = image.copy()
    icon = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    crop.thumbnail((248, 248), Image.LANCZOS)
    icon.paste(crop, ((256 - crop.width) // 2, (256 - crop.height) // 2))
    return icon
