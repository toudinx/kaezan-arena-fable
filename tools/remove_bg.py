#!/usr/bin/env python3
"""
remove_bg.py — AI-based batch background removal for Kaezan kaeli idle frames.

Uses rembg with the isnet-anime model, which is trained specifically on
anime-style artwork. Handles fine hair strands, wing feathers, and sheer
fabrics far better than colour/pattern-based approaches.

INSTALLATION
------------
CPU (recommended to start):
    pip install rembg onnxruntime Pillow numpy scipy

GPU (much faster if you have CUDA):
    pip install "rembg[gpu]" onnxruntime-gpu Pillow numpy scipy

The model (~170 MB) is downloaded automatically on first run and cached in
~/.u2net/.

USAGE
-----
    # Process all idle-*.png files, write results to ./output/
    python tools/remove_bg.py

    # Custom input/output directories
    python tools/remove_bg.py --input path/to/kaelis --output path/to/out

    # Process every PNG (not just idle-*)
    python tools/remove_bg.py --glob "*.png"

    # Try a different model
    python tools/remove_bg.py --model u2net_human_seg

    # Overwrite originals in-place (asks for confirmation)
    python tools/remove_bg.py --inplace

    # Save a 3-panel before/after comparison image next to each output
    python tools/remove_bg.py --preview

    # Skip interior-hole validation (faster)
    python tools/remove_bg.py --no-validate
"""

from __future__ import annotations

import argparse
import sys
import textwrap
from dataclasses import dataclass, field
from pathlib import Path

import numpy as np
from PIL import Image

# ---------------------------------------------------------------------------
# Optional scipy for hole detection
# ---------------------------------------------------------------------------
try:
    from scipy.ndimage import label as _scipy_label

    def _label(arr):
        return _scipy_label(arr)

    _HAS_SCIPY = True
except ImportError:
    _HAS_SCIPY = False

# ---------------------------------------------------------------------------
# Defaults
# ---------------------------------------------------------------------------
DEFAULT_INPUT = Path(__file__).parent.parent / "frontend/public/assets/kaelis"
DEFAULT_OUTPUT = Path(__file__).parent.parent / "frontend/public/assets/kaelis_nobg"
DEFAULT_GLOB = "idle-*.png"
DEFAULT_MODEL = "isnet-anime"

# ---------------------------------------------------------------------------
# Data classes
# ---------------------------------------------------------------------------
@dataclass
class AlphaStats:
    n_holes: int = 0
    hole_pixels: int = 0
    opaque_pixels: int = 0
    total_pixels: int = 0

    @property
    def coverage_pct(self) -> float:
        if self.total_pixels == 0:
            return 0.0
        return self.opaque_pixels / self.total_pixels * 100

@dataclass
class ProcessResult:
    src: Path
    dst: Path
    ok: bool
    stats: AlphaStats | None = None
    error: str = ""


# ---------------------------------------------------------------------------
# Core functions
# ---------------------------------------------------------------------------

def collect_images(input_dir: Path, glob_pattern: str) -> list[Path]:
    """Return all matching images under input_dir, sorted."""
    return sorted(input_dir.rglob(glob_pattern))


def validate_alpha(alpha_arr: np.ndarray) -> AlphaStats:
    """
    Detect interior holes in an alpha mask.

    A "hole" is a connected region of transparent pixels that does not
    touch any edge of the image — i.e., it is surrounded by opaque pixels
    and is almost certainly an error (e.g. the model cut out part of the
    character's body).
    """
    stats = AlphaStats()
    stats.total_pixels = alpha_arr.size
    stats.opaque_pixels = int((alpha_arr > 64).sum())

    if not _HAS_SCIPY:
        return stats

    h, w = alpha_arr.shape
    transparent = alpha_arr <= 64  # True where pixel is transparent

    # Flood-fill from all four borders to find "outside" transparent regions
    outside = np.zeros_like(transparent)
    outside[0, :] = transparent[0, :]
    outside[-1, :] = transparent[-1, :]
    outside[:, 0] = transparent[:, 0]
    outside[:, -1] = transparent[:, -1]

    # Grow the outside mask: any transparent pixel connected to a known-outside
    # pixel is also outside. Iterate until stable (BFS via label).
    labeled, _ = _scipy_label(transparent)
    border_labels = set(labeled[outside].tolist()) - {0}

    interior_mask = transparent & ~np.isin(labeled, list(border_labels))
    interior_labeled, n_holes = _scipy_label(interior_mask)

    stats.n_holes = int(n_holes)
    stats.hole_pixels = int(interior_mask.sum())
    return stats


def save_preview(original: Image.Image, result: Image.Image, dst: Path) -> Path:
    """
    Save a 3-panel comparison: original | alpha mask (greyscale) | result on
    a visible checker background.
    """
    w, h = original.size
    panel_w = w * 3
    checker_tile = 32

    canvas = Image.new("RGBA", (panel_w, h), (255, 255, 255, 255))

    # Panel 1 — original (convert to RGBA so paste works uniformly)
    canvas.paste(original.convert("RGBA"), (0, 0))

    # Panel 2 — alpha channel as greyscale (shows mask quality)
    if result.mode == "RGBA":
        alpha_grey = result.getchannel("A").convert("RGB")
    else:
        alpha_grey = Image.new("RGB", (w, h), (255, 255, 255))
    canvas.paste(alpha_grey.convert("RGBA"), (w, 0))

    # Panel 3 — result composited over a visible pink/white checker
    checker = Image.new("RGBA", (w, h))
    checker_arr = np.zeros((h, w, 4), dtype=np.uint8)
    for y in range(h):
        for x in range(w):
            tile_x = (x // checker_tile) % 2
            tile_y = (y // checker_tile) % 2
            if (tile_x + tile_y) % 2 == 0:
                checker_arr[y, x] = [255, 192, 203, 255]  # pink
            else:
                checker_arr[y, x] = [255, 255, 255, 255]  # white
    checker = Image.fromarray(checker_arr, "RGBA")
    if result.mode == "RGBA":
        checker.paste(result, (0, 0), result)
    else:
        checker.paste(result.convert("RGBA"), (0, 0))
    canvas.paste(checker, (w * 2, 0))

    preview_path = dst.parent / (dst.stem + "_compare.png")
    canvas.save(preview_path, "PNG")
    return preview_path


def process_image(
    src: Path,
    dst: Path,
    session,
    validate: bool,
    preview: bool,
) -> ProcessResult:
    """Remove background from src, save RGBA PNG to dst."""
    from rembg import remove as rembg_remove

    try:
        with open(src, "rb") as f:
            in_bytes = f.read()

        out_bytes = rembg_remove(in_bytes, session=session)

        dst.parent.mkdir(parents=True, exist_ok=True)
        dst.write_bytes(out_bytes)

        result_img = Image.open(dst)
        stats: AlphaStats | None = None

        if validate:
            if result_img.mode == "RGBA":
                alpha_arr = np.array(result_img.getchannel("A"))
                stats = validate_alpha(alpha_arr)
            else:
                stats = AlphaStats(total_pixels=result_img.width * result_img.height)

        if preview:
            original_img = Image.open(src)
            save_preview(original_img, result_img, dst)

        return ProcessResult(src=src, dst=dst, ok=True, stats=stats)

    except Exception as exc:
        return ProcessResult(src=src, dst=dst, ok=False, error=str(exc))


# ---------------------------------------------------------------------------
# Reporting
# ---------------------------------------------------------------------------

def print_row(
    rel: str, status: str, coverage: str, holes: str, hole_px: str
) -> None:
    print(f"  {rel:<40}  {status:<6}  {coverage:>8}  {holes:>6}  {hole_px:>8}")


def print_report(results: list[ProcessResult], input_dir: Path) -> None:
    print()
    print_row("FILE", "STATUS", "COVERAGE", "HOLES", "HOLE PX")
    print("  " + "-" * 78)

    errors = 0
    warnings = 0
    for r in results:
        rel = str(r.src.relative_to(input_dir))
        if not r.ok:
            print_row(rel, "ERROR", "-", "-", "-")
            print(f"    └─ {r.error}")
            errors += 1
            continue

        status = "OK"
        s = r.stats
        if s is None:
            print_row(rel, status, "-", "-", "-")
            continue

        coverage = f"{s.coverage_pct:.1f}%"
        holes = str(s.n_holes)
        hole_px = str(s.hole_pixels)

        if s.n_holes > 0:
            status = "WARN"
            warnings += 1
        print_row(rel, status, coverage, holes, hole_px)

    print()
    total = len(results)
    ok = total - errors
    print(f"  Processed: {ok}/{total}  |  Errors: {errors}  |  Warnings (holes): {warnings}")
    if not _HAS_SCIPY:
        print()
        print("  NOTE: scipy not installed — hole detection skipped.")
        print("        pip install scipy  to enable it.")
    print()


# ---------------------------------------------------------------------------
# CLI
# ---------------------------------------------------------------------------

def parse_args() -> argparse.Namespace:
    p = argparse.ArgumentParser(
        description="AI background removal for kaeli idle frames",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=textwrap.dedent("""\
            Models available in rembg:
              isnet-anime       — anime artwork, best for hair/feathers (DEFAULT)
              u2net             — general purpose
              u2net_human_seg   — human figures, robust silhouette
              silueta           — fast, lower quality
        """),
    )
    p.add_argument(
        "--input", "-i",
        type=Path,
        default=DEFAULT_INPUT,
        help=f"Folder containing kaeli subfolders (default: {DEFAULT_INPUT})",
    )
    p.add_argument(
        "--output", "-o",
        type=Path,
        default=DEFAULT_OUTPUT,
        help=f"Destination folder (default: {DEFAULT_OUTPUT})",
    )
    p.add_argument(
        "--glob", "-g",
        default=DEFAULT_GLOB,
        help=f"Glob pattern to match files (default: {DEFAULT_GLOB!r})",
    )
    p.add_argument(
        "--model", "-m",
        default=DEFAULT_MODEL,
        help=f"rembg model name (default: {DEFAULT_MODEL})",
    )
    p.add_argument(
        "--inplace",
        action="store_true",
        help="Overwrite originals instead of writing to --output",
    )
    p.add_argument(
        "--preview",
        action="store_true",
        help="Save a 3-panel before/after/mask image next to each output",
    )
    p.add_argument(
        "--no-validate",
        action="store_true",
        help="Skip interior-hole detection",
    )
    return p.parse_args()


def main() -> int:
    args = parse_args()

    try:
        from rembg import new_session
    except ImportError:
        print("ERROR: rembg is not installed.")
        print("  pip install rembg onnxruntime Pillow numpy scipy")
        return 1

    input_dir: Path = args.input.resolve()
    if not input_dir.exists():
        print(f"ERROR: input directory does not exist: {input_dir}")
        return 1

    images = collect_images(input_dir, args.glob)
    if not images:
        print(f"No files matching {args.glob!r} found under {input_dir}")
        return 0

    if args.inplace:
        output_dir = None
        print(f"\n  !! --inplace will OVERWRITE {len(images)} original file(s) !!")
        confirm = input("  Type 'yes' to continue: ").strip().lower()
        if confirm != "yes":
            print("Aborted.")
            return 0
    else:
        output_dir = args.output.resolve()

    validate = not args.no_validate

    print(f"\n  Model  : {args.model}")
    print(f"  Input  : {input_dir}")
    print(f"  Output : {'(in-place)' if args.inplace else output_dir}")
    print(f"  Files  : {len(images)}")
    print(f"  Validate holes : {validate}")
    print(f"  Preview        : {args.preview}")
    print()
    print(f"  Loading model {args.model!r}  (first run downloads ~170 MB) …")

    session = new_session(args.model)

    print(f"  Model ready. Processing {len(images)} image(s) …\n")

    results: list[ProcessResult] = []
    for i, src in enumerate(images, 1):
        rel = str(src.relative_to(input_dir))
        print(f"  [{i:2}/{len(images)}] {rel} …", end="", flush=True)

        if args.inplace:
            dst = src
        else:
            dst = output_dir / src.relative_to(input_dir)

        result = process_image(
            src=src,
            dst=dst,
            session=session,
            validate=validate,
            preview=args.preview,
        )
        results.append(result)

        if result.ok:
            s = result.stats
            if s and s.n_holes > 0:
                print(f" WARN ({s.n_holes} interior hole(s), {s.hole_pixels} px)")
            else:
                print(" OK")
        else:
            print(f" ERROR: {result.error}")

    print_report(results, input_dir)
    return 0 if all(r.ok for r in results) else 1


if __name__ == "__main__":
    sys.exit(main())
