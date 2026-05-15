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


def make_stone_wall(path: Path, seed: int = 42) -> None:
    """Hewn fantasy dungeon wall — irregular stone blocks in wide mortar."""
    rng = random.Random(seed)
    mortar = (108, 98, 86)
    img = Image.new("RGB", (SIZE, SIZE), mortar)
    draw = ImageDraw.Draw(img)

    cols, rows = 7, 9
    for row in range(-1, rows + 1):
        for col in range(-1, cols + 1):
            cell_w = SIZE / cols
            cell_h = SIZE / rows
            cx = col * cell_w + cell_w * 0.5 + rng.uniform(-cell_w * 0.22, cell_w * 0.22)
            cy = row * cell_h + cell_h * 0.5 + rng.uniform(-cell_h * 0.22, cell_h * 0.22)
            w = cell_w * rng.uniform(0.78, 1.05)
            h = cell_h * rng.uniform(0.75, 1.02)
            pts = _irregular_quad(cx, cy, w, h, rng)
            base = _stone_color(rng, wall=True)
            fill = jitter(base, 20, rng)
            draw.polygon(pts, fill=fill, outline=jitter(mortar, 8, rng))

            # chiselled top-left highlight
            hx = sum(p[0] for p in pts) / 4
            hy = sum(p[1] for p in pts) / 4
            draw.line(
                [(pts[0][0], pts[0][1]), (pts[1][0], pts[1][1])],
                fill=jitter(fill, 28, rng),
                width=rng.randint(2, 4),
            )
            draw.line(
                [(pts[0][0], pts[0][1]), (pts[3][0], pts[3][1])],
                fill=jitter(fill, 22, rng),
                width=rng.randint(1, 3),
            )

    # deeper mortar grain
    px = img.load()
    for _ in range(8000):
        x, y = rng.randint(0, SIZE - 1), rng.randint(0, SIZE - 1)
        r, g, b = px[x, y]
        d = rng.randint(-14, 10)
        px[x, y] = (clamp(r + d), clamp(g + d), clamp(b + d))

    # moss in creases
    moss = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    md = ImageDraw.Draw(moss)
    for _ in range(22):
        cx = rng.randint(0, SIZE)
        cy = rng.randint(0, SIZE)
        r = rng.randint(14, 40)
        md.ellipse(
            [cx - r, cy - r, cx + r, cy + r],
            fill=(62, 88, 54, rng.randint(30, 75)),
        )
    moss = moss.filter(ImageFilter.GaussianBlur(radius=5))
    img = Image.alpha_composite(img.convert("RGBA"), moss).convert("RGB")

    cd = ImageDraw.Draw(img)
    for _ in range(55):
        x, y = rng.randint(0, SIZE), rng.randint(0, SIZE)
        length = rng.randint(10, 55)
        angle = rng.uniform(0, math.pi)
        cd.line(
            [(x, y), (x + math.cos(angle) * length, y + math.sin(angle) * length)],
            fill=(78, 66, 56),
            width=1,
        )

    img = img.filter(ImageFilter.GaussianBlur(radius=0.55))
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


if __name__ == "__main__":
    main()
