#!/usr/bin/env python3
"""Generate the ONI-Heart KAnim.

The heart is built as a low-poly gem: an angular heart silhouette, a crown of
large flat facets, a multi-faceted rhombic centre, a smoothly rounded lower
heart and a beveled girdle.  All shading is computed from flat facet normals so
the result reads as a cut and polished solid instead of a patterned silhouette.
"""
from __future__ import annotations

import math
import struct
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

FRAME = 512
COLS = 4
ROWS = 4
COUNT = COLS * ROWS
SS = 2  # supersampling: 1024 px render, downsampled to 512
R = FRAME * SS
OUT = Path(__file__).resolve().parents[1] / "anim" / "assets" / "oniheart"

# Low-poly heart outline.  Keeping the vertex count modest is what gives the
# stone its cut-gem corners instead of a smooth cartoon outline.
OUTLINE_POINTS = 22

# Ring profiles: (scale about the heart centre, z height in outline units).
# z is measured in the same units as x/y, so the normals below are correct.
RINGS = (
    (1.000, -0.012),  # girdle edge
    (0.970, 0.038),   # bright bevel band
    (0.860, 0.074),   # lower crown
    (0.720, 0.104),   # middle crown
    (0.560, 0.128),   # upper crown
    (0.410, 0.146),   # star facets
    (0.280, 0.158),   # inner star facets
)

# Warm/neutral white body with a faint blue-green undertone.
BASE = np.array([0.955, 0.972, 1.000], dtype=np.float32)


def sdbm_lower(s: str) -> int:
    h = 0
    for ch in s.lower():
        h = (ord(ch) + (h << 6) + (h << 16) - h) & 0xFFFFFFFF
    return (h ^ 0x80000000) - 0x80000000


def kstring(s: str) -> bytes:
    b = s.encode("utf-8")
    return struct.pack("<i", len(b)) + b


def heart_polygon(size: int, scale: float = 1.0) -> list[tuple[float, float]]:
    """A deliberately faceted heart: straight segments between 22 vertices."""
    t = np.linspace(0.0, 2.0 * math.pi, OUTLINE_POINTS, endpoint=False)
    x = 16.0 * np.sin(t) ** 3
    y = 13.0 * np.cos(t) - 5.0 * np.cos(2 * t) - 2.0 * np.cos(3 * t) - np.cos(4 * t)
    x = x / 21.0
    y = y / 17.5

    xmin, xmax = float(x.min()), float(x.max())
    ymin, ymax = float(y.min()), float(y.max())
    # Fit inside the frame with a little room for the outer glow / sparkles.
    target = size * 0.91 * scale
    sx = target / (xmax - xmin)
    sy = target / (ymax - ymin)
    cx = size / 2.0
    cy = size * 0.515
    pts = [
        (cx + (float(px) - (xmin + xmax) / 2.0) * sx,
         cy - (float(py) - (ymin + ymax) / 2.0) * sy)
        for px, py in zip(x, y)
    ]

    # Remove the lower side taper completely and replace it with a broad,
    # smoothly rounded bottom.  P8/P14 remain the natural lower side points;
    # everything between them becomes a single round bottom curve.
    bottom = OUTLINE_POINTS // 2
    ys = [p[1] for p in pts]
    height = max(ys) - min(ys)
    p0 = pts[bottom - 3]       # right lower side
    p2 = pts[bottom + 3]       # left lower side
    target_bottom = max(ys) - height * 0.060
    control = (cx, 2.0 * target_bottom - p0[1])
    for k, t in enumerate((0.20, 0.40, 0.50, 0.60, 0.80)):
        one = 1.0 - t
        pts[bottom - 2 + k] = (
            one * one * p0[0] + 2.0 * one * t * control[0] + t * t * p2[0],
            one * one * p0[1] + 2.0 * one * t * control[1] + t * t * p2[1],
        )

    # Keep the new silhouette centred on the gem axis.
    new_ys = [p[1] for p in pts]
    shift = cy - (min(new_ys) + max(new_ys)) / 2.0
    return [(x, y + shift) for x, y in pts]


def gem_ring(scale: float, blend: float, jitter: np.ndarray,
             cx: float, cy: float, heart_radius: np.ndarray,
             heart_angle: np.ndarray, table_angle: np.ndarray,
             max_radius: float) -> list[tuple[float, float]]:
    """One crown ring, morphed from the heart outline to a round table.

    The outer rings stay heart-shaped.  Moving inward the radius/angle blend
    towards a regular polygon, so the table reads as a cut face instead of a
    smaller heart-shaped hole.
    """
    target_radius = max_radius * scale
    out = []
    for i in range(len(heart_radius)):
        radius = heart_radius[i] * (1.0 - blend) + target_radius * blend
        radius *= 1.0 + jitter[i] * (1.0 - 0.6 * blend)
        angle = heart_angle[i] * (1.0 - blend) + table_angle[i] * blend
        out.append((cx + math.cos(angle) * radius, cy + math.sin(angle) * radius))
    return out


def gradient_layer(size, top, bottom, curve: float = 1.0) -> Image.Image:
    w, h = size
    t = (np.linspace(0.0, 1.0, h) ** curve).astype(np.float32)
    arr = np.empty((h, w, 4), dtype=np.uint8)
    for c in range(3):
        arr[..., c] = (top[c] * (1.0 - t) + bottom[c] * t)[:, None].clip(0, 255).astype(np.uint8)
    arr[..., 3] = 255
    return Image.fromarray(arr)


def ray_radius(poly, cx: float, cy: float, theta: float) -> float:
    """Distance from (cx, cy) to the polygon boundary along theta."""
    dx, dy = math.cos(theta), math.sin(theta)
    best = 0.0
    n = len(poly)
    for i in range(n):
        x1, y1 = poly[i]
        x2, y2 = poly[(i + 1) % n]
        ex, ey = x2 - x1, y2 - y1
        den = dx * ey - dy * ex
        if abs(den) < 1e-9:
            continue
        qx, qy = x1 - cx, y1 - cy
        t = (qx * ey - qy * ex) / den
        u = (qx * dy - qy * dx) / den
        if t > 0.0 and 0.0 <= u <= 1.0 and t > best:
            best = t
    if best <= 0.0:
        best = min(math.hypot(px - cx, py - cy) for px, py in poly)
    return best


def normal3(a, b, c) -> np.ndarray:
    va = np.array([b[0] - a[0], b[1] - a[1], b[2] - a[2]], dtype=np.float32)
    vb = np.array([c[0] - a[0], c[1] - a[1], c[2] - a[2]], dtype=np.float32)
    n = np.cross(va, vb)
    length = float(np.linalg.norm(n))
    if length < 1e-6:
        return np.array([0.0, 0.0, 1.0], dtype=np.float32)
    n /= length
    if n[2] < 0.0:
        n = -n
    return n


def hsv_tint(hue: float, saturation: float, value: float = 1.0) -> np.ndarray:
    hue = hue % 1.0
    i = int(hue * 6.0)
    f = hue * 6.0 - i
    p = value * (1.0 - saturation)
    q = value * (1.0 - saturation * f)
    t = value * (1.0 - saturation * (1.0 - f))
    r, g, b = (
        (value, t, p), (q, value, p), (p, value, t),
        (p, q, value), (t, p, value), (value, p, q),
    )[i % 6]
    return np.array([r, g, b], dtype=np.float32)


def lerp_color(a: np.ndarray, b: np.ndarray, t: float) -> np.ndarray:
    return a * (1.0 - t) + b * t


def facet_color(normal: np.ndarray, centroid: tuple[float, float], ring: int,
                phase: float, random_value: float) -> tuple[int, int, int, int]:
    # Two lights: a cool key from the upper-left and a warm bounce from below.
    key = np.array([-0.42, -0.55, 0.72], dtype=np.float32)
    key /= np.linalg.norm(key)
    bounce = np.array([0.38, 0.50, 0.58], dtype=np.float32)
    bounce /= np.linalg.norm(bounce)
    view = np.array([0.0, 0.0, 1.0], dtype=np.float32)
    half = key + view
    half /= np.linalg.norm(half)

    diffuse = max(0.0, float(np.dot(normal, key)))
    fill = max(0.0, float(np.dot(normal, bounce)))
    spec = max(0.0, float(np.dot(normal, half))) ** 34.0

    # Facets are flat but not perfectly uniform: a little hand-cut variance.
    variation = (random_value - 0.5) * 0.17
    ao = 0.93 - 0.045 * ring
    brightness = 0.31 + 0.60 * diffuse + 0.10 * fill + variation
    brightness *= ao
    brightness += 0.24 * spec
    brightness = max(0.15, min(1.10, brightness))

    # Thin-film / dispersion: most facets stay near white, a few catch colour.
    angle = math.atan2(centroid[1] - R * 0.5, centroid[0] - R * 0.5)
    hue = (angle / (2.0 * math.pi) + 0.11 * ring + 0.55 * phase + random_value * 0.10) % 1.0
    strength = 0.085 + 0.46 * (random_value ** 3)
    strength *= 0.65 + 0.35 * (1.0 - diffuse)
    tint = hsv_tint(hue, 0.72, 1.0)
    colour = lerp_color(BASE, BASE * tint, strength)

    colour = colour * brightness
    # A small specular core keeps every facet looking polished.
    colour = np.clip(colour + spec * 0.38, 0.0, 1.0)
    return (
        int(colour[0] * 255.0),
        int(colour[1] * 255.0),
        int(colour[2] * 255.0),
        255,
    )


def make_frame(index: int) -> Image.Image:
    phase = index / COUNT
    wobble = 0.5 + 0.5 * math.cos(2.0 * math.pi * phase)
    scale = 0.996 + 0.008 * wobble

    poly = heart_polygon(R, scale)
    cx = R / 2.0
    cy = R * 0.515

    outline_mask = Image.new("L", (R, R), 0)
    ImageDraw.Draw(outline_mask).polygon(poly, fill=255)

    # The lower side contour and facet seams fade out so the rounded bottom
    # reads as one polished surface, not two lines meeting at a point.
    poly_ys = [p[1] for p in poly]
    poly_top = min(poly_ys)
    poly_height = max(poly_ys) - poly_top
    fade_start = poly_top + poly_height * 0.52
    fade_end = poly_top + poly_height * 0.68

    # Soft glow escaping from inside the stone.
    glow_mask = outline_mask.filter(ImageFilter.GaussianBlur(R * 0.024))
    glow_np = np.zeros((R, R, 4), dtype=np.uint8)
    glow_np[..., 0] = 244
    glow_np[..., 1] = 250
    glow_np[..., 2] = 255
    glow_np[..., 3] = (np.asarray(glow_mask, dtype=np.float32) * 0.05).astype(np.uint8)
    out = Image.fromarray(glow_np)

    body = Image.new("RGBA", (R, R), (0, 0, 0, 0))
    draw = ImageDraw.Draw(body)

    # Build the concentric faceted rings.  A tiny per-vertex jitter keeps the
    # crown from looking like a mechanically perfect pie chart.
    rng = np.random.default_rng(0x0A17)
    half = len(poly) // 2
    heart_radius = np.array([math.hypot(x - cx, y - cy) for x, y in poly])
    heart_angle = np.unwrap(np.arctan2(
        np.array([y - cy for _, y in poly]),
        np.array([x - cx for x, _ in poly])))
    direction = 1.0 if heart_angle[-1] > heart_angle[0] else -1.0
    table_angle = heart_angle[0] + direction * np.linspace(
        0.0, 2.0 * math.pi, len(poly), endpoint=False)
    max_radius = float(heart_radius.max())
    table_scale = RINGS[-1][0]

    rings: list[list[tuple[float, float, float]]] = []
    for ri, (rs, rz) in enumerate(RINGS):
        # Mirror the jitter across the vertical axis so the cut stays
        # symmetrical, the way a real heart-cut stone would be indexed.
        base = (rng.random(half + 1) - 0.5) * (0.020 if ri < len(RINGS) - 1 else 0.0)
        jitter = np.array([base[min(i, len(poly) - i)] for i in range(len(poly))])
        # Keep the first two rings heart-shaped, then gradually open the
        # crown into the round table.
        blend = (0.90 - rs) / (0.90 - table_scale)
        blend = float(np.clip(blend, 0.0, 1.0) ** 1.15)
        flat = gem_ring(rs, blend, jitter, cx, cy, heart_radius,
                        heart_angle, table_angle, max_radius)
        rings.append([(x, y, rz * R) for x, y in flat])

    # Central polished facet cluster: a rhombic cut area, not a hole, not a
    # circle and not a smaller heart-shaped table.  The inner rhombus and the
    # four kites around it are real shaded facets.
    center_rx = max_radius * 0.165
    center_ry = max_radius * 0.215
    center_sides = 4
    center_z = (RINGS[-1][1] + 0.014) * R
    # A rhombic cut plane: just one polished facet, not a table or a hole.
    center_poly = [
        (cx + math.cos(-math.pi / 2 + 2.0 * math.pi * i / center_sides) * center_rx,
         cy + math.sin(-math.pi / 2 + 2.0 * math.pi * i / center_sides) * center_ry,
         center_z)
        for i in range(center_sides)
    ]
    center_poly2 = [(p[0], p[1]) for p in center_poly]
    inner_scale = 0.42
    inner_center_poly = [
        (cx + (p[0] - cx) * inner_scale,
         cy + (p[1] - cy) * inner_scale,
         center_z + R * 0.006)
        for p in center_poly
    ]
    star_outer = rings[-1]
    star_inner = []
    for i in range(len(star_outer)):
        angle = math.atan2(star_outer[i][1] - cy, star_outer[i][0] - cx)
        rr = ray_radius(center_poly2, cx, cy, angle)
        star_inner.append((cx + math.cos(angle) * rr,
                           cy + math.sin(angle) * rr,
                           center_z))

    # ---- crown / pavilion facets -----------------------------------------
    facets: list[tuple[list[tuple[float, float, float]], int]] = []
    quads: list[tuple[tuple[tuple[float, float, float], ...], int, int]] = []
    for ri in range(len(rings) - 1):
        outer = rings[ri]
        inner = rings[ri + 1]
        n = len(outer)
        for i in range(n):
            j = (i + 1) % n
            q = (outer[i], outer[j], inner[j], inner[i])
            quads.append((q, ri, i))
            if (i + ri) % 2 == 0:
                facets.append(([q[0], q[1], q[2]], ri))
                facets.append(([q[0], q[2], q[3]], ri))
            else:
                facets.append(([q[0], q[1], q[3]], ri))
                facets.append(([q[1], q[2], q[3]], ri))

    # Star facets from the last crown ring to the rhombic centre facet.
    star_ri = len(rings) - 1
    for i in range(len(star_outer)):
        j = (i + 1) % len(star_outer)
        q = (star_outer[i], star_outer[j], star_inner[j], star_inner[i])
        quads.append((q, star_ri, i))
        if i % 2 == 0:
            facets.append(([q[0], q[1], q[2]], star_ri))
            facets.append(([q[0], q[2], q[3]], star_ri))
        else:
            facets.append(([q[0], q[1], q[3]], star_ri))
            facets.append(([q[1], q[2], q[3]], star_ri))

    # The rhombic centre is itself cut into four raised kite facets around a
    # small flat inner rhombus.  It is therefore multi-faceted, not a flat white
    # disc and not a hollow table.
    centre_ring = 1
    for i in range(center_sides):
        j = (i + 1) % center_sides
        q = (center_poly[i], center_poly[j],
             inner_center_poly[j], inner_center_poly[i])
        quads.append((q, centre_ring, i))
        if i % 2 == 0:
            facets.append(([q[0], q[1], q[2]], centre_ring))
            facets.append(([q[0], q[2], q[3]], centre_ring))
        else:
            facets.append(([q[0], q[1], q[3]], centre_ring))
            facets.append(([q[1], q[2], q[3]], centre_ring))

    facets.append(([inner_center_poly[0], inner_center_poly[1],
                    inner_center_poly[2]], centre_ring))
    facets.append(([inner_center_poly[0], inner_center_poly[2],
                    inner_center_poly[3]], centre_ring))

    colours = []
    for fi, (tri, ri) in enumerate(facets):
        n = normal3(*tri)
        centroid = (
            sum(p[0] for p in tri) / 3.0,
            sum(p[1] for p in tri) / 3.0,
        )
        rv = float(rng.random())
        colours.append(facet_color(n, centroid, ri, phase, rv))

    # Paint outer-to-inner so the table always sits cleanly on top.
    for (tri, ri), col in zip(facets, colours):
        draw.polygon([(p[0], p[1]) for p in tri], fill=col)

    # Some whole facets catch dispersed light.  Unlike a thin coloured line,
    # tinting the full facet reads as refraction inside the stone.
    tint_layer = Image.new("RGBA", (R, R), (0, 0, 0, 0))
    tl = ImageDraw.Draw(tint_layer)
    for qi, (q, ri, i) in enumerate(quads):
        h = ((qi * 2654435761) & 0xFFFFFFFF) / 0xFFFFFFFF
        if h > 0.20:
            continue
        pulse = 0.5 + 0.5 * math.sin(2.0 * math.pi * (phase * 0.8 + h * 3.0))
        hue = (h * 3.7 + 0.30 * phase + 0.12 * ri) % 1.0
        col = hsv_tint(hue, 0.82, 1.0)
        alpha = int(24 + 72 * pulse)
        tl.polygon([(p[0], p[1]) for p in q],
                   fill=(int(col[0] * 255), int(col[1] * 255), int(col[2] * 255), alpha))
    body = Image.alpha_composite(body, tint_layer)

    # ---- central polish --------------------------------------------------
    cpts = [(p[0], p[1]) for p in center_poly]
    center_mask = Image.new("L", (R, R), 0)
    ImageDraw.Draw(center_mask).polygon(cpts, fill=255)

    # The centre is built from real facets above; this only adds a soft polish
    # and a little internal colour, without covering the cut structure.
    polish = Image.new("RGBA", (R, R), (0, 0, 0, 0))
    pd = ImageDraw.Draw(polish)
    pd.ellipse([cx - center_rx * 1.08, cy - center_ry * 1.18,
                cx + center_rx * 0.48, cy + center_ry * 0.06],
               fill=(255, 255, 255, 16))
    pd.ellipse([cx - center_rx * 0.30, cy + center_ry * 0.02,
                cx + center_rx * 1.02, cy + center_ry * 1.02],
               fill=(84, 112, 170, 10))
    polish = polish.filter(ImageFilter.GaussianBlur(R * 0.016))
    polish.putalpha(Image.composite(polish.getchannel("A"),
                                    Image.new("L", (R, R), 0), center_mask))
    body = Image.alpha_composite(body, polish)

    caustic = Image.new("RGBA", (R, R), (0, 0, 0, 0))
    cd = ImageDraw.Draw(caustic)
    for k in range(2):
        hue = (0.12 + 0.26 * k + 0.50 * phase) % 1.0
        col = hsv_tint(hue, 0.55, 1.0)
        radius = center_ry * (0.50 + 0.12 * k)
        ox = math.sin(2.0 * math.pi * (phase + k * 0.21)) * center_rx * 0.18
        oy = math.cos(2.0 * math.pi * (phase * 0.8 + k * 0.19)) * center_ry * 0.14
        cd.ellipse([cx - radius + ox, cy - radius + oy,
                    cx + radius + ox, cy + radius + oy],
                   fill=(int(col[0] * 255), int(col[1] * 255), int(col[2] * 255), 12))
    caustic = caustic.filter(ImageFilter.GaussianBlur(R * 0.012))
    caustic.putalpha(Image.composite(caustic.getchannel("A"),
                                     Image.new("L", (R, R), 0), center_mask))
    body = Image.alpha_composite(body, caustic)

    # ---- crisp facet seams ----------------------------------------------
    seams = Image.new("RGBA", (R, R), (0, 0, 0, 0))
    sd = ImageDraw.Draw(seams)
    light2 = np.array([-0.42, -0.55], dtype=np.float32)
    light2 /= np.linalg.norm(light2)

    # Facet boundaries only: ring edges and radial cut lines.  The triangle
    # split inside a quad is a shading detail, so it must NOT be outlined or
    # the stone turns into a wireframe mesh.
    edges: list[tuple[tuple[float, float, float], tuple[float, float, float], int]] = []
    n = len(poly)
    for r in range(len(rings)):
        for i in range(n):
            j = (i + 1) % n
            edges.append((rings[r][i], rings[r][j], r * n + i))
    for r in range(len(rings) - 1):
        for i in range(n):
            edges.append((rings[r][i], rings[r + 1][i], 5000 + r * n + i))
    for i in range(len(star_outer)):
        edges.append((star_outer[i], star_inner[i], 9000 + i))
    for i in range(center_sides):
        j = (i + 1) % center_sides
        edges.append((center_poly[i], center_poly[j], 12000 + i))
        edges.append((inner_center_poly[i], inner_center_poly[j], 12100 + i))
        edges.append((center_poly[i], inner_center_poly[i], 12200 + i))

    def edge_normal(a, b) -> tuple[float, float, float, float]:
        ex, ey = b[0] - a[0], b[1] - a[1]
        ln = math.hypot(ex, ey)
        if ln < 1e-6:
            return 0.0, 0.0, 0.0, 0.0
        nx, ny = ey / ln, -ex / ln
        mx, my = (a[0] + b[0]) / 2.0, (a[1] + b[1]) / 2.0
        if nx * (mx - cx) + ny * (my - cy) < 0.0:
            nx, ny = -nx, -ny
        return ex, ey, nx, ny

    for a, b, _ in edges:
        sd.line([(a[0], a[1]), (b[0], b[1])],
                fill=(9, 16, 28, 190), width=max(2, R // 430))

    for a, b, idx in edges:
        ex, ey, nx, ny = edge_normal(a, b)
        if nx == 0.0 and ny == 0.0:
            continue
        lit = nx * light2[0] + ny * light2[1]
        if lit > 0.06:
            sd.line([(a[0], a[1]), (b[0], b[1])],
                    fill=(255, 255, 255, 118), width=max(1, R // 700))
        # Sparse coloured caustics along cut edges, moving with the animation.
        if (idx * 37) % 43 < 4:
            hue = (idx / 211.0 + 0.45 * phase + 0.17) % 1.0
            col = hsv_tint(hue, 0.90, 1.0)
            sd.line([(a[0], a[1]), (b[0], b[1])],
                    fill=(int(col[0] * 255), int(col[1] * 255), int(col[2] * 255), 78),
                    width=max(1, R // 650))

    def fade_lower(layer):
        arr = np.array(layer)
        yy = np.arange(R, dtype=np.float32)[:, None]
        t = np.clip((yy - fade_start) / max(1.0, fade_end - fade_start), 0.0, 1.0)
        arr[..., 3] = (arr[..., 3].astype(np.float32) * (1.0 - t)).astype(np.uint8)
        return Image.fromarray(arr)

    body = Image.alpha_composite(body, fade_lower(seams))

    # ---- girdle: polished, darker outside edge with a bright cut line ----
    girdle = Image.new("RGBA", (R, R), (0, 0, 0, 0))
    gd = ImageDraw.Draw(girdle)
    outer = [(p[0], p[1]) for p in poly]
    gd.line(outer + [outer[0]], fill=(8, 13, 22, 225), width=max(3, R // 180))

    # Coloured rim, one segment at a time, so the light moves around the edge.
    for i in range(len(poly)):
        j = (i + 1) % len(poly)
        a, b = outer[i], outer[j]
        ex, ey = b[0] - a[0], b[1] - a[1]
        ln = math.hypot(ex, ey)
        if ln < 1e-6:
            continue
        nx, ny = ey / ln, -ex / ln
        # inward normal
        if (a[0] + b[0]) / 2.0 - cx > 0:
            nx, ny = -nx, -ny
        if (a[1] + b[1]) / 2.0 - cy > 0:
            nx, ny = -nx, -ny
        inset = R * 0.008
        hue = (i / len(poly) + 0.62 * phase) % 1.0
        col = hsv_tint(hue, 0.75, 1.0)
        alpha = int(115 + 65 * max(0.0, nx * light2[0] + ny * light2[1]))
        gd.line([(a[0] + nx * inset, a[1] + ny * inset),
                 (b[0] + nx * inset, b[1] + ny * inset)],
                fill=(int(col[0] * 255), int(col[1] * 255), int(col[2] * 255), alpha),
                width=max(2, R // 320))
        # Bright white bevel on the lit side of the girdle.
        if nx * light2[0] + ny * light2[1] > 0.05:
            gd.line([(a[0] + nx * inset * 1.9, a[1] + ny * inset * 1.9),
                     (b[0] + nx * inset * 1.9, b[1] + ny * inset * 1.9)],
                    fill=(255, 255, 255, 175),
                    width=max(1, R // 700))
    body = Image.alpha_composite(body, fade_lower(girdle))

    # ---- fine stone texture: grain, tiny inclusions, depth ---------------
    tex_rng = np.random.default_rng(0xC0FFEE + index)
    grain = tex_rng.normal(0.0, 1.0, (R // 2, R // 2)).astype(np.float32)
    grain_img = Image.fromarray(((grain * 20.0) + 128.0).clip(0, 255).astype(np.uint8), "L")
    grain_img = grain_img.resize((R, R), Image.BILINEAR).filter(ImageFilter.GaussianBlur(R * 0.0014))
    grain_np = np.asarray(grain_img, dtype=np.float32) - 128.0
    tex = np.zeros((R, R, 4), dtype=np.uint8)
    tex[..., 0] = np.clip(128 + grain_np * 1.3, 0, 255)
    tex[..., 1] = np.clip(128 + grain_np * 1.5, 0, 255)
    tex[..., 2] = np.clip(135 + grain_np * 1.7, 0, 255)
    tex[..., 3] = 34
    tex_layer = Image.fromarray(tex)
    tex_layer.putalpha(Image.composite(tex_layer.getchannel("A"),
                                       Image.new("L", (R, R), 0), outline_mask))
    body = Image.alpha_composite(body, tex_layer)

    # Tiny polished micro-facets / dust caught in the stone.  These are only a
    # few pixels wide at the final resolution, but they give the surface the
    # granular sparkle that a flat vector fill lacks.
    micro = Image.new("RGBA", (R, R), (0, 0, 0, 0))
    md = ImageDraw.Draw(micro)
    for _ in range(320):
        x = float(tex_rng.random()) * R
        y = float(tex_rng.random()) * R
        if outline_mask.getpixel((int(x), int(y))) == 0:
            continue
        size = R * (0.0015 + 0.0035 * tex_rng.random())
        angle = float(tex_rng.random()) * math.pi
        dx, dy = math.cos(angle) * size, math.sin(angle) * size
        alpha = int(18 + 38 * tex_rng.random())
        md.line([x - dx, y - dy, x + dx, y + dy],
                fill=(255, 255, 255, alpha), width=1)
    body = Image.alpha_composite(body, micro)

    # A few thin internal fracture lines, very subtle.
    inclusions = Image.new("RGBA", (R, R), (0, 0, 0, 0))
    idraw = ImageDraw.Draw(inclusions)
    for k in range(14):
        ang = tex_rng.random() * math.pi * 2.0
        rad = R * (0.12 + tex_rng.random() * 0.32)
        x0 = cx + math.cos(ang) * rad
        y0 = cy + math.sin(ang) * rad
        length = R * (0.02 + tex_rng.random() * 0.06)
        x1 = x0 + math.cos(ang + 1.2) * length
        y1 = y0 + math.sin(ang + 1.2) * length
        idraw.line([x0, y0, x1, y1], fill=(255, 255, 255, 18), width=1)
    inclusions.putalpha(Image.composite(inclusions.getchannel("A"),
                                        Image.new("L", (R, R), 0), outline_mask))
    body = Image.alpha_composite(body, inclusions)

    # Broad form light / shadow.  The facet normals handle the fine cut, this
    # adds the soft volume that keeps the big bright facets from going flat.
    volume = Image.new("RGBA", (R, R), (0, 0, 0, 0))
    vd = ImageDraw.Draw(volume)
    vd.ellipse([cx - R * 0.62, cy - R * 0.60, cx + R * 0.38, cy + R * 0.34],
               fill=(255, 255, 255, 30))
    vd.ellipse([cx - R * 0.10, cy + R * 0.02, cx + R * 0.62, cy + R * 0.70],
               fill=(10, 18, 32, 34))
    volume = volume.filter(ImageFilter.GaussianBlur(R * 0.06))
    volume.putalpha(Image.composite(volume.getchannel("A"),
                                    Image.new("L", (R, R), 0), outline_mask))
    body = Image.alpha_composite(body, volume)

    # ---- internal glow and table reflection ------------------------------
    inner = Image.new("RGBA", (R, R), (0, 0, 0, 0))
    ind = ImageDraw.Draw(inner)
    for k in range(3):
        r = R * (0.16 - 0.035 * k)
        ind.ellipse([cx - r, cy - r, cx + r, cy + r],
                    fill=(235, 248, 255, 20 + 7 * k))
    inner = inner.filter(ImageFilter.GaussianBlur(R * 0.035))
    body = Image.alpha_composite(body, inner)

    # ---- sparkles on cut corners ----------------------------------------
    sparkles = Image.new("RGBA", (R, R), (0, 0, 0, 0))
    sp = ImageDraw.Draw(sparkles)
    sparkle_indices = (2, 5, 9, 13, 16, 20)
    for k, si in enumerate(sparkle_indices):
        tw = 0.5 + 0.5 * math.sin(2.0 * math.pi * (phase + k / len(sparkle_indices)))
        if tw < 0.24:
            continue
        # Position on the bevel ring, not on the very silhouette.
        px = cx + (poly[si][0] - cx) * 0.955
        py = cy + (poly[si][1] - cy) * 0.955
        length = R * (0.008 + 0.020 * tw)
        width = max(1, int(R * 0.0018))
        hue = (si / len(poly) + 0.35 + phase) % 1.0
        col = hsv_tint(hue, 0.55, 1.0)
        alpha = int(90 + 130 * tw)
        sp.line([(px - length, py), (px + length, py)],
                fill=(int(col[0] * 255), int(col[1] * 255), int(col[2] * 255), alpha),
                width=width)
        sp.line([(px, py - length), (px, py + length)],
                fill=(int(col[0] * 255), int(col[1] * 255), int(col[2] * 255), alpha),
                width=width)
        sp.ellipse([px - width * 2, py - width * 2, px + width * 2, py + width * 2],
                   fill=(255, 255, 255, int(180 * tw)))
    body = Image.alpha_composite(body, sparkles)

    out = Image.alpha_composite(out, body)
    return out.resize((FRAME, FRAME), Image.LANCZOS)


def build_bytes() -> bytes:
    heart = sdbm_lower("heart")
    place = sdbm_lower("place")
    ui = sdbm_lower("ui")

    out = bytearray()
    out += b"BILD"
    out += struct.pack("<i", 10)
    out += struct.pack("<i", 3)  # symbol count
    out += struct.pack("<i", 0)
    out += kstring("oniheart")

    def symbol(name_hash: int, name: str,
               frames: list[tuple[int, tuple[float, float, float, float], tuple[float, float, float, float]]]):
        nonlocal out
        out += struct.pack("<i", name_hash)
        out += struct.pack("<i", name_hash)  # path id
        out += struct.pack("<i", 0)  # channels
        out += struct.pack("<i", 0)  # flags
        out += struct.pack("<i", len(frames))
        for seq, bounds, uv in frames:
            out += struct.pack("<i", seq)
            out += struct.pack("<i", 1)
            out += struct.pack("<i", 0)
            out += struct.pack("<4f", *bounds)
            out += struct.pack("<4f", *uv)

    frames = []
    for i in range(COUNT):
        col, row = i % COLS, i // COLS
        uv = (col / COLS, row / ROWS, (col + 1) / COLS, (row + 1) / ROWS)
        frames.append((i, (300.0, -400.0, 800.0, 800.0), uv))

    symbol(heart, "heart", frames)
    symbol(place, "place", [(0, (300.0, -400.0, 800.0, 800.0), frames[0][2])])
    # The build-menu icon uses this symbol's bounds as its native size. Keep it
    # close to a normal 1x1 icon instead of the 4x4 world footprint.
    symbol(ui, "ui", [(0, (0.0, 0.0, 200.0, 200.0), frames[0][2])])

    strings = [(heart, "heart"), (place, "place"), (ui, "ui")]
    out += struct.pack("<i", len(strings))
    for h, s in strings:
        out += struct.pack("<i", h)
        out += kstring(s)
    return bytes(out)


def anim_bytes() -> bytes:
    heart = sdbm_lower("heart")
    place = sdbm_lower("place")
    ui = sdbm_lower("ui")

    out = bytearray()
    out += b"ANIM"
    out += struct.pack("<i", 5)
    out += struct.pack("<i", 0)
    out += struct.pack("<i", 0)
    out += struct.pack("<i", 4)  # animations

    def element(symbol_hash: int, frame: int) -> bytes:
        b = bytearray()
        b += struct.pack("<i", symbol_hash)
        b += struct.pack("<i", frame)
        b += struct.pack("<i", symbol_hash)
        b += struct.pack("<i", 0)
        b += struct.pack("<4f", 1.0, 1.0, 1.0, 1.0)
        b += struct.pack("<6f", 1.0, 0.0, 0.0, 1.0, 0.0, 0.0)
        b += struct.pack("<f", 0.0)
        return bytes(b)

    def animation(name: str, frame_count: int, symbol_hash: int, use_frames: bool):
        nonlocal out
        out += kstring(name)
        out += struct.pack("<i", symbol_hash)
        out += struct.pack("<f", 6.0)
        out += struct.pack("<i", frame_count)
        for i in range(frame_count):
            out += struct.pack("<4f", 300.0, -400.0, 800.0, 800.0)
            out += struct.pack("<i", 1)
            out += element(symbol_hash, i if use_frames else 0)

    animation("idle", COUNT, heart, True)
    animation("off", 1, heart, False)
    animation("place", 1, place, False)
    animation("ui", 1, ui, False)

    out += struct.pack("<i", 0)
    strings = [(heart, "heart"), (place, "place"), (ui, "ui")]
    out += struct.pack("<i", len(strings))
    for h, s in strings:
        out += struct.pack("<i", h)
        out += kstring(s)
    return bytes(out)


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    atlas = Image.new("RGBA", (FRAME * COLS, FRAME * ROWS), (0, 0, 0, 0))
    for i in range(COUNT):
        frame = make_frame(i)
        atlas.paste(frame, ((i % COLS) * FRAME, (i // COLS) * FRAME))
    atlas.save(OUT / "oniheart_0.png")
    (OUT / "oniheart_build.bytes").write_bytes(build_bytes())
    (OUT / "oniheart_anim.bytes").write_bytes(anim_bytes())
    # small preview of the first frame (kept outside the anim folder so the
    # game does not treat it as a second texture atlas)
    preview = Path(__file__).resolve().parents[1] / "tools" / "oniheart_preview.png"
    atlas.crop((0, 0, FRAME, FRAME)).resize((512, 512), Image.LANCZOS).save(preview)

    # contact sheet of all 16 frames on a dark background, for quick review
    sheet = Image.new("RGBA", (FRAME * COLS, FRAME * ROWS), (20, 24, 30, 255))
    for i in range(COUNT):
        frame = atlas.crop(((i % COLS) * FRAME, (i // COLS) * FRAME,
                            (i % COLS + 1) * FRAME, (i // COLS + 1) * FRAME))
        sheet.alpha_composite(frame, ((i % COLS) * FRAME, (i // COLS) * FRAME))
    sheet.convert("RGB").save(Path(__file__).resolve().parents[1] / "tools" / "oniheart_frames.png")
    print("wrote", OUT)


if __name__ == "__main__":
    main()
