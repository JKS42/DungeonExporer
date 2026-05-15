#!/usr/bin/env python3
"""Procedural tileable albedo maps for DungeonExporer (cosy fantasy palette)."""

from __future__ import annotations

import math
import random
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parents[1]
ART = ROOT / "Assets" / "Art" / "Environment"
SIZE = 1024


def clamp(v: int) -> int:
    return max(0, min(255, v))


def jitter(rgb: tuple[int, int, int], amount: int, rng: random.Random) -> tuple[int, int, int]:
    return tuple(clamp(c + rng.randint(-amount, amount)) for c in rgb)


def darken(rgb: tuple[int, int, int], amount: int, rng: random.Random) -> tuple[int, int, int]:
    return tuple(clamp(c - rng.randint(amount // 2, amount)) for c in rgb)


def _stone_color(rng: random.Random, wall: bool = True) -> tuple[int, int, int]:
    """Warm fantasy dungeon stone — cocoa / grey-brown, not cold concrete."""
    if wall:
        return (
            rng.randint(128, 172),
            rng.randint(108, 142),
            rng.randint(88, 118),
        )
    return (
        rng.randint(138, 182),
        rng.randint(118, 152),
        rng.randint(98, 128),
    )


def _irregular_quad(
    cx: float, cy: float, w: float, h: float, rng: random.Random
) -> list[tuple[float, float]]:
    points = []
    for i in range(4):
        angle = i * (math.pi / 2) + rng.uniform(-0.45, 0.45)
        rx = w * 0.5 * rng.uniform(0.72, 1.08)
        ry = h * 0.5 * rng.uniform(0.72, 1.08)
        points.append((cx + math.cos(angle) * rx, cy + math.sin(angle) * ry))
    return points


def _wall_stone_fill(rng: random.Random) -> tuple[int, int, int]:
    """Warm grey / taupe blocks like stylized dungeon art."""
    palette = [
        (168, 158, 142),
        (152, 142, 128),
        (178, 168, 152),
        (140, 132, 118),
        (162, 150, 136),
    ]
    return jitter(palette[rng.randint(0, len(palette) - 1)], 14, rng)


def _draw_stylized_stone(draw: ImageDraw.ImageDraw, pts: list[tuple[float, float]], fill: tuple[int, int, int], rng: random.Random) -> None:
    """Cartoon stone: fill, dark outline, top-left highlight, bottom-right shade, pits, chips."""
    outline = (32, 28, 24)
    draw.polygon(pts, fill=fill, outline=outline)

    minx = min(p[0] for p in pts)
    maxx = max(p[0] for p in pts)
    miny = min(p[1] for p in pts)
    maxy = max(p[1] for p in pts)
    w = maxx - minx
    h = maxy - miny

    hi = jitter(fill, 38, rng)
    sh = darken(fill, 36, rng)
    draw.line([(minx, miny), (minx + w * 0.82, miny)], fill=hi, width=rng.randint(4, 6))
    draw.line([(minx, miny), (minx, miny + h * 0.82)], fill=hi, width=rng.randint(3, 5))
    draw.line([(maxx, maxy), (minx + w * 0.18, maxy)], fill=sh, width=rng.randint(3, 5))
    draw.line([(maxx, maxy), (maxx, miny + h * 0.18)], fill=sh, width=rng.randint(3, 5))

    cx = (minx + maxx) * 0.5
    cy = (miny + maxy) * 0.5
    for _ in range(rng.randint(2, 7)):
        px = cx + rng.uniform(-w * 0.35, w * 0.35)
        py = cy + rng.uniform(-h * 0.35, h * 0.35)
        r = rng.randint(3, 9)
        draw.ellipse([px - r, py - r, px + r, py + r], fill=(48, 42, 36))

    if rng.random() > 0.35:
        x0 = cx + rng.uniform(-w * 0.25, w * 0.25)
        y0 = cy + rng.uniform(-h * 0.25, h * 0.25)
        length = rng.randint(18, 55)
        angle = rng.uniform(0, math.pi)
        draw.line(
            [
                (x0, y0),
                (x0 + math.cos(angle) * length, y0 + math.sin(angle) * length),
            ],
            fill=(58, 50, 44),
            width=rng.randint(2, 3),
        )

    for _ in range(rng.randint(0, 2)):
        chip_x = rng.choice([minx, maxx])
        chip_y = rng.uniform(miny, maxy)
        draw.line(
            [(chip_x, chip_y), (chip_x + rng.randint(-12, 12), chip_y + rng.randint(-8, 8))],
            fill=sh,
            width=2,
        )


def make_stone_wall(path: Path, seed: int = 42) -> None:
    """Stylized fantasy wall — large irregular blocks, dark mortar (reference-inspired)."""
    rng = random.Random(seed)
    mortar = (34, 30, 26)
    img = Image.new("RGB", (SIZE, SIZE), mortar)
    draw = ImageDraw.Draw(img)

    cols, rows = 5, 6
    cell_w = SIZE / cols
    cell_h = SIZE / rows
    mortar_gap = 16

    for row in range(-1, rows + 1):
        row_offset = (cell_w * 0.48) if row % 2 else 0.0
        for col in range(-1, cols + 1):
            cx = col * cell_w + cell_w * 0.5 + row_offset + rng.uniform(-22, 22)
            cy = row * cell_h + cell_h * 0.5 + rng.uniform(-18, 18)
            w = cell_w * rng.uniform(0.82, 1.08) - mortar_gap
            h = cell_h * rng.uniform(0.8, 1.06) - mortar_gap
            pts = _irregular_quad(cx, cy, w, h, rng)
            fill = _wall_stone_fill(rng)
            _draw_stylized_stone(draw, pts, fill, rng)

    # subtle mortar noise (keep gaps dark)
    px = img.load()
    for _ in range(5000):
        x, y = rng.randint(0, SIZE - 1), rng.randint(0, SIZE - 1)
        r, g, b = px[x, y]
        if r < 60:
            d = rng.randint(-8, 8)
            px[x, y] = (clamp(r + d), clamp(g + d), clamp(b + d))

    img = img.filter(ImageFilter.GaussianBlur(radius=0.35))
    img.save(path, "PNG")
    print(f"wrote {path}")


def make_stone_floor(path: Path, seed: int = 77) -> None:
    """Large irregular floor slabs — worn fantasy dungeon paving."""
    rng = random.Random(seed)
    grout = (98, 90, 78)
    img = Image.new("RGB", (SIZE, SIZE), grout)
    draw = ImageDraw.Draw(img)

    slab_count = 32
    for _ in range(slab_count):
        cx = rng.uniform(0, SIZE)
        cy = rng.uniform(0, SIZE)
        w = rng.uniform(SIZE * 0.14, SIZE * 0.28)
        h = rng.uniform(SIZE * 0.12, SIZE * 0.26)
        if rng.random() > 0.5:
            w, h = h, w
        pts = _irregular_quad(cx, cy, w, h, rng)
        base = _stone_color(rng, wall=False)
        fill = jitter(base, 18, rng)
        draw.polygon(pts, fill=fill, outline=jitter(grout, 6, rng))

        # worn centre lighter patch
        mx = sum(p[0] for p in pts) / 4
        my = sum(p[1] for p in pts) / 4
        r = min(w, h) * 0.22
        draw.ellipse(
            [mx - r, my - r, mx + r, my + r],
            fill=jitter(fill, 16, rng),
        )

    img = img.filter(ImageFilter.GaussianBlur(radius=0.45))

    # fine grit + scuffs
    px = img.load()
    for _ in range(12000):
        x, y = rng.randint(0, SIZE - 1), rng.randint(0, SIZE - 1)
        r, g, b = px[x, y]
        d = rng.randint(-16, 14)
        px[x, y] = (clamp(r + d), clamp(g + d), clamp(b + d))

    # subtle cracks between slabs
    cd = ImageDraw.Draw(img)
    for _ in range(35):
        x, y = rng.randint(0, SIZE), rng.randint(0, SIZE)
        length = rng.randint(20, 90)
        angle = rng.uniform(0, math.pi)
        cd.line(
            [(x, y), (x + math.cos(angle) * length, y + math.sin(angle) * length)],
            fill=(72, 64, 54),
            width=rng.randint(1, 2),
        )

    img.save(path, "PNG")
    print(f"wrote {path}")


def make_spike_trap(path: Path, seed: int = 13) -> None:
    rng = random.Random(seed)
    size = 512
    img = Image.new("RGB", (size, size), (48, 42, 46))
    draw = ImageDraw.Draw(img)

    for y in range(0, size, 32):
        draw.line([(0, y), (size, y)], fill=(62, 52, 50), width=3)
    for x in range(0, size, 32):
        draw.line([(x, 0), (x, size)], fill=(62, 52, 50), width=3)

    cols, rows = 4, 4
    cw, ch = size // cols, size // rows
    for row in range(rows):
        for col in range(cols):
            cx = col * cw + cw // 2
            cy = row * ch + ch // 2
            spread = cw // 3
            for sx in (-spread, 0, spread):
                for sy in (-spread // 2, spread // 2):
                    bx = cx + sx + rng.randint(-4, 4)
                    by = cy + sy + rng.randint(-4, 4)
                    tip = by - rng.randint(26, 38)
                    half = rng.randint(7, 11)
                    metal = (168, 172, 178) if rng.random() > 0.25 else (140, 138, 132)
                    draw.polygon(
                        [(bx - half, by), (bx + half, by), (bx, tip)],
                        fill=metal,
                    )
                    draw.line([(bx, by), (bx, tip)], fill=(96, 92, 88), width=1)

    overlay = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    od = ImageDraw.Draw(overlay)
    od.rectangle([0, 0, size - 1, size - 1], outline=(120, 48, 42, 90), width=8)
    img = Image.alpha_composite(img.convert("RGBA"), overlay).convert("RGB")
    img = img.filter(ImageFilter.GaussianBlur(radius=0.25))
    img.save(path, "PNG")
    print(f"wrote {path}")


def main() -> None:
    brick_dir = ART / "DungeonBrick"
    floor_dir = ART / "DungeonFloor"
    spike_dir = ART / "SpikeTrap"
    floor_dir.mkdir(parents=True, exist_ok=True)
    spike_dir.mkdir(parents=True, exist_ok=True)

    make_stone_wall(brick_dir / "DungeonBrick_Albedo.png")
    make_stone_floor(floor_dir / "DungeonFloor_Albedo.png")
    make_spike_trap(spike_dir / "SpikeTrap_Albedo.png")


def regenerate_wall_only() -> None:
    """Regenerate wall albedo only (keeps existing floor)."""
    make_stone_wall(ART / "DungeonBrick" / "DungeonBrick_Albedo.png")


if __name__ == "__main__":
    main()
