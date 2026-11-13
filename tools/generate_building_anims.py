#!/usr/bin/env python3
"""Generate the custom KAnim art for every ONI-Heart building.

The actual drawing lives in building_art.py; this script only down-samples the
rendered paintings, packs the world sprite plus build-menu icon into an atlas
and writes the Klei .bytes files.
"""
from __future__ import annotations

import math
import struct
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

from building_art import (
    bottler_image,
    bottler_icon,
    interface_image,
    terminal_icon,
    terminal_image,
)

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "anim" / "assets"
UI_HASH = 7675188  # Hash.SDBMLower("ui")


def sdbm_lower(s: str) -> int:
    h = 0
    for ch in s.lower():
        h = (ord(ch) + (h << 6) + (h << 16) - h) & 0xFFFFFFFF
    return (h ^ 0x80000000) - 0x80000000


def kstring(s: str) -> bytes:
    b = s.encode("utf-8")
    return struct.pack("<i", len(b)) + b


def kanim(folder: str, name: str, image: Image.Image,
          bounds: tuple[float, float, float, float],
          ui_bounds: tuple[float, float, float, float] | None = None,
          ui_image: Image.Image | None = None) -> None:
    out = ASSETS / folder
    out.mkdir(parents=True, exist_ok=True)

    def write_symbol(buf: bytearray, symbol_hash: int,
                     frames: list[tuple[tuple[float, float, float, float],
                                        tuple[float, float, float, float]]]) -> None:
        buf += struct.pack("<i", symbol_hash)
        buf += struct.pack("<i", symbol_hash)
        buf += struct.pack("<i", 0)  # channels
        buf += struct.pack("<i", 0)  # flags
        buf += struct.pack("<i", len(frames))
        for frame_bounds, uv in frames:
            buf += struct.pack("<i", 0)  # seq
            buf += struct.pack("<i", 1)  # duration
            buf += struct.pack("<i", 0)  # texture image
            buf += struct.pack("<4f", *frame_bounds)
            buf += struct.pack("<4f", *uv)

    def write_animation(buf: bytearray, anim_name: str, symbol_hash: int,
                        frame_bounds: tuple[float, float, float, float]) -> None:
        buf += kstring(anim_name)
        buf += struct.pack("<i", symbol_hash)
        buf += struct.pack("<f", 1.0)
        buf += struct.pack("<i", 1)
        buf += struct.pack("<4f", *frame_bounds)
        buf += struct.pack("<i", 1)
        buf += struct.pack("<i", symbol_hash)
        buf += struct.pack("<i", 0)
        buf += struct.pack("<i", symbol_hash)
        buf += struct.pack("<i", 0)
        buf += struct.pack("<4f", 1.0, 1.0, 1.0, 1.0)
        buf += struct.pack("<6f", 1.0, 0.0, 0.0, 1.0, 0.0, 0.0)
        buf += struct.pack("<f", 0.0)

    if ui_image is None:
        # Compatibility with single-symbol assets: the ui symbol is used for both world rendering and the build-menu icon.
        image.save(out / f"{name}_0.png")
        world_uv = (0.0, 0.0, 1.0, 1.0)

        build = bytearray()
        build += b"BILD"
        build += struct.pack("<i", 10)
        build += struct.pack("<i", 1)  # symbols
        build += struct.pack("<i", 0)
        build += kstring(name)
        write_symbol(build, UI_HASH, [(bounds, world_uv)])
        build += struct.pack("<i", 1)
        build += struct.pack("<i", UI_HASH)
        build += kstring("ui")
        (out / f"{name}_build.bytes").write_bytes(build)

        anim = bytearray()
        anim += b"ANIM"
        anim += struct.pack("<i", 5)
        anim += struct.pack("<i", 0)
        anim += struct.pack("<i", 0)
        anim += struct.pack("<i", 4)
        for anim_name in ("idle", "off", "place", "ui"):
            write_animation(anim, anim_name, UI_HASH, bounds)
        anim += struct.pack("<i", 0)
        anim += struct.pack("<i", 1)
        anim += struct.pack("<i", UI_HASH)
        anim += kstring("ui")
        (out / f"{name}_anim.bytes").write_bytes(anim)
        return

    # Two-symbol assets: the world model uses the object symbol and the build-menu icon uses a separate ui symbol;
    # both share the same atlas so they can have different display sizes.
    world_hash = sdbm_lower("object")
    w, h = image.size
    uw, uh = ui_image.size
    atlas_w = w + uw
    atlas_h = max(h, uh)
    atlas = Image.new("RGBA", (atlas_w, atlas_h), (0, 0, 0, 0))
    atlas.paste(image, (0, 0))
    atlas.paste(ui_image, (w, 0))
    atlas.save(out / f"{name}_0.png")

    world_uv = (0.0, 0.0, w / atlas_w, h / atlas_h)
    ui_uv = (w / atlas_w, 0.0, (w + uw) / atlas_w, uh / atlas_h)
    ui_frame_bounds = ui_bounds if ui_bounds is not None else bounds

    build = bytearray()
    build += b"BILD"
    build += struct.pack("<i", 10)
    build += struct.pack("<i", 2)  # symbols
    build += struct.pack("<i", 0)
    build += kstring(name)
    write_symbol(build, world_hash, [(bounds, world_uv)])
    write_symbol(build, UI_HASH, [(ui_frame_bounds, ui_uv)])
    build += struct.pack("<i", 2)
    build += struct.pack("<i", world_hash)
    build += kstring("object")
    build += struct.pack("<i", UI_HASH)
    build += kstring("ui")
    (out / f"{name}_build.bytes").write_bytes(build)

    anim = bytearray()
    anim += b"ANIM"
    anim += struct.pack("<i", 5)
    anim += struct.pack("<i", 0)
    anim += struct.pack("<i", 0)
    anim += struct.pack("<i", 4)
    for anim_name in ("idle", "off", "place"):
        write_animation(anim, anim_name, world_hash, bounds)
    write_animation(anim, "ui", UI_HASH, ui_frame_bounds)
    anim += struct.pack("<i", 0)
    anim += struct.pack("<i", 2)
    anim += struct.pack("<i", world_hash)
    anim += kstring("object")
    anim += struct.pack("<i", UI_HASH)
    anim += kstring("ui")
    (out / f"{name}_anim.bytes").write_bytes(anim)


def make_preview(paths: list[tuple[str, Path, int]], out: Path) -> None:
    """Small contact sheet for eyeballing the generated art after a build.

    Each entry carries the width of the world sprite inside the atlas so the
    build-menu icon half is not shown next to the machine.
    """
    cell_w, cell_h = 180, 230
    cols = 5
    rows = math.ceil(len(paths) / cols)
    sheet = Image.new("RGB", (cols * cell_w, rows * cell_h), (18, 22, 28))
    d = ImageDraw.Draw(sheet)
    try:
        font = ImageFont.truetype("/System/Library/Fonts/Supplemental/Arial.ttf", 13)
    except Exception:
        font = ImageFont.load_default()
    for i, (label, p, world_w) in enumerate(paths):
        im = Image.open(p).convert("RGBA")
        im = im.crop((0, 0, world_w, im.height))
        im.thumbnail((cell_w - 12, cell_h - 34), Image.LANCZOS)
        x = (i % cols) * cell_w + (cell_w - im.width) // 2
        y = (i // cols) * cell_h + 24 + (cell_h - 34 - im.height) // 2
        tile = Image.new("RGBA", (cell_w - 8, cell_h - 8), (27, 32, 40, 255))
        tile.alpha_composite(im, ((cell_w - 8 - im.width) // 2,
                                  (cell_h - 8 - im.height) // 2))
        sheet.paste(tile.convert("RGB"), ((i % cols) * cell_w + 4,
                                           (i // cols) * cell_h + 4))
        d.text(((i % cols) * cell_w + 8, (i // cols) * cell_h + 7),
               label, font=font, fill=(218, 232, 244))
    out.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(out)


def main():
    specs = {
        "oniheart_gas_in": ("oniheart_gas_in", "gas_in"),
        "oniheart_gas_out": ("oniheart_gas_out", "gas_out"),
        "oniheart_liquid_in": ("oniheart_liquid_in", "liquid_in"),
        "oniheart_liquid_out": ("oniheart_liquid_out", "liquid_out"),
        "oniheart_power": ("oniheart_power", "power"),
    }
    for folder, (name, kind) in specs.items():
        image = interface_image(kind).resize((256, 256), Image.LANCZOS)
        kanim(folder, name, image, (0.0, -100.0, 200.0, 200.0))
        print("wrote", folder)

    bottler_world = bottler_image().resize((256, 512), Image.LANCZOS)
    kanim(
        "oniheart_bottler",
        "oniheart_bottler",
        bottler_world,
        (0.0, -200.0, 200.0, 400.0),
        ui_bounds=(0.0, 0.0, 200.0, 200.0),
        ui_image=bottler_icon(bottler_world),
    )
    print("wrote oniheart_bottler")

    terminal_specs = {
        "oniheart_terminal_decompose": ("oniheart_terminal_decompose", "decompose"),
        "oniheart_terminal_solidify": ("oniheart_terminal_solidify", "solidify"),
        "oniheart_terminal_liquefy": ("oniheart_terminal_liquefy", "liquefy"),
        "oniheart_terminal_vaporize": ("oniheart_terminal_vaporize", "vaporize"),
        "oniheart_terminal_thermostat": ("oniheart_terminal_thermostat", "thermostat"),
    }
    for folder, (name, kind) in terminal_specs.items():
        world = terminal_image(kind).resize((512, 512), Image.LANCZOS)
        kanim(
            folder,
            name,
            world,
            (0.0, -200.0, 400.0, 400.0),
            ui_bounds=(0.0, 0.0, 200.0, 200.0),
            ui_image=terminal_icon(world),
        )
        print("wrote", folder)

    make_preview(
        [
            ("gas in", ASSETS / "oniheart_gas_in" / "oniheart_gas_in_0.png", 256),
            ("gas out", ASSETS / "oniheart_gas_out" / "oniheart_gas_out_0.png", 256),
            ("liquid in", ASSETS / "oniheart_liquid_in" / "oniheart_liquid_in_0.png", 256),
            ("liquid out", ASSETS / "oniheart_liquid_out" / "oniheart_liquid_out_0.png", 256),
            ("power", ASSETS / "oniheart_power" / "oniheart_power_0.png", 256),
            ("bottler", ASSETS / "oniheart_bottler" / "oniheart_bottler_0.png", 256),
            ("decompose", ASSETS / "oniheart_terminal_decompose" / "oniheart_terminal_decompose_0.png", 512),
            ("solidify", ASSETS / "oniheart_terminal_solidify" / "oniheart_terminal_solidify_0.png", 512),
            ("liquefy", ASSETS / "oniheart_terminal_liquefy" / "oniheart_terminal_liquefy_0.png", 512),
            ("vaporize", ASSETS / "oniheart_terminal_vaporize" / "oniheart_terminal_vaporize_0.png", 512),
            ("thermostat", ASSETS / "oniheart_terminal_thermostat" / "oniheart_terminal_thermostat_0.png", 512),
        ],
        ROOT / "tools" / "building_anims_preview.png",
    )
    print("wrote preview")


if __name__ == "__main__":
    main()
