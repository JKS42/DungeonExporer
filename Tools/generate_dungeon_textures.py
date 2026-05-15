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


def make_brick_wall(path: Path, seed: int = 42) -> None:
    """Flat running-bond brick wall (original layout, no moss/highlights/cracks)."""
    rng = random.Random(seed)
    mortar = (198, 186, 168)
    img = Image.new("RGB", (SIZE, SIZE), mortar)
    draw = ImageDraw.Draw(img)

    joint = 5
    rows = 14
    row_h = SIZE // rows
    for row in range(rows):
        offset = (row_h // 3) if row % 2 else 0
        cols = 9
        col_w = (SIZE + offset) // cols
        y0 = row * row_h + joint
        y1 = (row + 1) * row_h - joint
        for col in range(-1, cols + 1):
            x0 = col * col_w - offset + joint
            x1 = x0 + col_w - joint * 2
            if x1 <= x0 or y1 <= y0:
                continue
            base = (
                rng.randint(118, 155),
                rng.randint(78, 108),
                rng.randint(62, 92),
            )
            draw.rectangle([x0, y0, x1, y1], fill=jitter(base, 8, rng))

    img.save(path, "PNG")
    print(f"wrote {path}")


def make_flagstone_floor(path: Path, seed: int = 77) -> None:
    """Flat flagstone grid floor (original layout, no wear arcs/speckle)."""
    rng = random.Random(seed)
    grout = (142, 128, 112)
    img = Image.new("RGB", (SIZE, SIZE), grout)
    draw = ImageDraw.Draw(img)

    gap = 6
    tiles = 8
    cell = SIZE // tiles
    for ty in range(tiles):
        for tx in range(tiles):
            inset = rng.randint(4, 10)
            x0 = tx * cell + gap + inset
            y0 = ty * cell + gap + inset
            x1 = (tx + 1) * cell - gap - inset
            y1 = (ty + 1) * cell - gap - inset
            stone = jitter(
                (rng.randint(150, 178), rng.randint(132, 158), rng.randint(108, 132)),
                8,
                rng,
            )
            draw.rounded_rectangle(
                [x0, y0, x1, y1],
                radius=rng.randint(6, 14),
                fill=stone,
                outline=grout,
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

    make_brick_wall(brick_dir / "DungeonBrick_Albedo.png")
    make_flagstone_floor(floor_dir / "DungeonFloor_Albedo.png")
    make_spike_trap(spike_dir / "SpikeTrap_Albedo.png")


def regenerate_wall_only() -> None:
    make_brick_wall(ART / "DungeonBrick" / "DungeonBrick_Albedo.png")


if __name__ == "__main__":
    main()
