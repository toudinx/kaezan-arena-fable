#!/usr/bin/env python3
"""
upscale_anime.py — AI super-resolution (Real-ESRGAN) for anime-style kaeli art.

Uses RealESRGAN_x4plus_anime_6B: 6-block RRDB network trained specifically on
anime illustrations. Recovers fine hair strands, feather barbs, lace, embroidery,
and jewel settings that simple bicubic upscaling blurs.

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
INSTALLATION
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
CPU (any machine):
    pip install realesrgan basicsr Pillow numpy opencv-python-headless

GPU (NVIDIA + CUDA — 10-20x faster):
    pip install realesrgan basicsr Pillow numpy opencv-python-headless torch torchvision --index-url https://download.pytorch.org/whl/cu121

The model weights (~17 MB) are downloaded automatically on first run and
cached in ./weights/ next to this script.

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
PIPELINE ORDER (important)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
ALWAYS upscale FIRST, then run remove_bg.py on the upscaled output.

Reasons:
  1. Real-ESRGAN operates on RGB — it would discard or mishandle a pre-existing
     alpha channel anyway, so removing the background first adds no benefit.
  2. rembg/isnet-anime produces sharper transparency masks when given a higher-
     resolution source: fine hair strands and wing-feather tips that were
     sub-pixel at 896 px become detectable at 1792 px.
  3. The burned checkerboard in the originals is a low-frequency texture that
     Real-ESRGAN handles well; it does not propagate into the upscaled output.

Recommended pipeline:
    python tools/upscale_anime.py                    # step 1: 896→1792 px, RGB
    python tools/remove_bg.py --input <upscaled_dir> # step 2: add transparency

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
USAGE
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    # Process all idle-*.png files with the default 2x scale
    python tools/upscale_anime.py

    # Custom input/output directories
    python tools/upscale_anime.py --input path/to/kaelis --output path/to/out

    # 4x upscale (use only if you need print-quality output)
    python tools/upscale_anime.py --scale 4

    # Process every PNG (not just idle-*)
    python tools/upscale_anime.py --glob "*.png"

    # Skip comparison crops (faster)
    python tools/upscale_anime.py --no-compare

    # Use larger tiles (faster on high-VRAM GPU, may OOM on low VRAM)
    python tools/upscale_anime.py --tile 1024
"""

from __future__ import annotations

import argparse
import sys
import textwrap
import time
import urllib.request
from dataclasses import dataclass
from pathlib import Path

import cv2
import numpy as np
from PIL import Image, ImageDraw, ImageFont

# ---------------------------------------------------------------------------
# Defaults
# ---------------------------------------------------------------------------
_TOOLS_DIR = Path(__file__).parent
DEFAULT_INPUT = _TOOLS_DIR.parent / "frontend/public/assets/kaelis"
DEFAULT_OUTPUT = _TOOLS_DIR.parent / "frontend/public/assets/kaelis_upscaled"
DEFAULT_GLOB = "idle-*.png"
DEFAULT_SCALE = 2
DEFAULT_TILE = 512

MODEL_NAME = "RealESRGAN_x4plus_anime_6B"
MODEL_URL = (
    "https://github.com/xinntao/Real-ESRGAN/releases/download/"
    "v0.2.2.4/RealESRGAN_x4plus_anime_6B.pth"
)
WEIGHTS_DIR = _TOOLS_DIR / "weights"

# ---------------------------------------------------------------------------
# Data classes
# ---------------------------------------------------------------------------
@dataclass
class UpscaleResult:
    src: Path
    dst: Path
    ok: bool
    orig_size: tuple[int, int] = (0, 0)
    out_size: tuple[int, int] = (0, 0)
    elapsed: float = 0.0
    error: str = ""


# ---------------------------------------------------------------------------
# GPU detection
# ---------------------------------------------------------------------------
def detect_device() -> tuple[bool, str]:
    """Return (use_cuda, description)."""
    try:
        import torch
        if torch.cuda.is_available():
            name = torch.cuda.get_device_name(0)
            vram = torch.cuda.get_device_properties(0).total_memory // (1024 ** 2)
            return True, f"CUDA — {name} ({vram} MB VRAM)"
        return False, "CPU (CUDA not available)"
    except ImportError:
        return False, "CPU (torch not installed)"


# ---------------------------------------------------------------------------
# Model setup
# ---------------------------------------------------------------------------
def ensure_weights() -> Path:
    """Download model weights if not already cached. Returns path to .pth file."""
    WEIGHTS_DIR.mkdir(parents=True, exist_ok=True)
    dst = WEIGHTS_DIR / f"{MODEL_NAME}.pth"
    if dst.exists():
        return dst

    print(f"  Downloading {MODEL_NAME}.pth (~17 MB) …", end="", flush=True)
    try:
        urllib.request.urlretrieve(MODEL_URL, dst)
        print(" done.")
    except Exception as exc:
        print(f"\n  ERROR downloading weights: {exc}")
        print(f"  Manual download: {MODEL_URL}")
        print(f"  Save to: {dst}")
        sys.exit(1)
    return dst


def build_upsampler(weights: Path, use_cuda: bool, tile: int):
    """Instantiate and return a RealESRGANer."""
    try:
        from basicsr.archs.rrdbnet_arch import RRDBNet
        from realesrgan import RealESRGANer
    except ImportError as exc:
        print(f"\nERROR: {exc}")
        print("Install dependencies:")
        print("  pip install realesrgan basicsr opencv-python-headless")
        sys.exit(1)

    # anime_6B uses 6 RRDB blocks — lighter and faster than the 23-block general model,
    # while producing cleaner lines on flat-shaded anime art.
    model = RRDBNet(
        num_in_ch=3, num_out_ch=3, num_feat=64,
        num_block=6, num_grow_ch=32, scale=4,
    )
    device = "cuda" if use_cuda else "cpu"
    return RealESRGANer(
        scale=4,
        model_path=str(weights),
        model=model,
        tile=tile,
        tile_pad=10,
        pre_pad=0,
        half=use_cuda,   # fp16 on GPU for speed; fp32 on CPU for compatibility
        device=device,
    )


# ---------------------------------------------------------------------------
# Image processing
# ---------------------------------------------------------------------------
def upscale_image(
    upsampler,
    src: Path,
    dst: Path,
    out_scale: int,
) -> UpscaleResult:
    """
    Run super-resolution on src and write to dst.

    Handles both RGB (3-channel) and RGBA (4-channel) images: the alpha channel
    is extracted before processing, upscaled separately with LANCZOS (it's a
    binary/near-binary mask — SR adds no value there), then recombined.
    """
    t_start = time.perf_counter()
    try:
        img_bgr = cv2.imread(str(src), cv2.IMREAD_UNCHANGED)
        if img_bgr is None:
            raise ValueError(f"cv2 could not open file: {src.name}")

        h, w = img_bgr.shape[:2]
        has_alpha = img_bgr.ndim == 3 and img_bgr.shape[2] == 4

        if has_alpha:
            alpha = img_bgr[:, :, 3]
            img_bgr = img_bgr[:, :, :3]

        output_bgr, _ = upsampler.enhance(img_bgr, outscale=out_scale)

        if has_alpha:
            new_h, new_w = output_bgr.shape[:2]
            alpha_up = cv2.resize(alpha, (new_w, new_h), interpolation=cv2.INTER_LANCZOS4)
            output_bgr = cv2.merge([*cv2.split(output_bgr), alpha_up])

        dst.parent.mkdir(parents=True, exist_ok=True)
        cv2.imwrite(str(dst), output_bgr)

        out_h, out_w = output_bgr.shape[:2]
        elapsed = time.perf_counter() - t_start
        return UpscaleResult(
            src=src, dst=dst, ok=True,
            orig_size=(w, h), out_size=(out_w, out_h),
            elapsed=elapsed,
        )
    except Exception as exc:
        return UpscaleResult(src=src, dst=dst, ok=False, error=str(exc),
                             elapsed=time.perf_counter() - t_start)


# ---------------------------------------------------------------------------
# Comparison crop
# ---------------------------------------------------------------------------
def _try_load_font(size: int) -> ImageFont.ImageFont:
    for name in ("arial.ttf", "DejaVuSans.ttf", "Helvetica.ttf"):
        try:
            return ImageFont.truetype(name, size)
        except (OSError, IOError):
            pass
    return ImageFont.load_default()


def save_comparison(src: Path, dst: Path, comp_path: Path) -> None:
    """
    Save a side-by-side crop comparison:
      LEFT  — original pixel data zoomed up with nearest-neighbour (shows blur)
      RIGHT — same region from the upscaled output (shows SR quality)

    The crop targets the upper-centre third of the image, which covers the
    face, hair, and hair accessories — the area most sensitive to blur.
    """
    orig = Image.open(src).convert("RGB")
    upsc = Image.open(dst).convert("RGB")

    ow, oh = orig.size
    uw, uh = upsc.size
    scale_x = uw / ow
    scale_y = uh / oh

    # Crop region: centre horizontally, top 10–45 % vertically (face band)
    cx1 = int(ow * 0.30)
    cy1 = int(oh * 0.08)
    cx2 = int(ow * 0.70)
    cy2 = int(oh * 0.42)

    orig_crop = orig.crop((cx1, cy1, cx2, cy2))

    ucx1 = int(cx1 * scale_x)
    ucy1 = int(cy1 * scale_y)
    ucx2 = int(cx2 * scale_x)
    ucy2 = int(cy2 * scale_y)
    upsc_crop = upsc.crop((ucx1, ucy1, ucx2, ucy2))

    # Scale orig crop to match upscaled size using nearest-neighbour so the
    # actual pixel blur is preserved and visible — not hidden by smooth upscaling.
    panel_w, panel_h = upsc_crop.size
    orig_zoomed = orig_crop.resize((panel_w, panel_h), Image.NEAREST)

    LABEL_H = 44
    MARGIN = 8
    DIVIDER = 4
    bg_color = (20, 20, 20)
    total_w = panel_w * 2 + MARGIN * 3 + DIVIDER
    total_h = panel_h + LABEL_H + MARGIN * 2

    canvas = Image.new("RGB", (total_w, total_h), bg_color)
    canvas.paste(orig_zoomed, (MARGIN, LABEL_H + MARGIN))
    # thin divider line
    div = Image.new("RGB", (DIVIDER, panel_h), (80, 80, 80))
    canvas.paste(div, (MARGIN + panel_w + MARGIN, LABEL_H + MARGIN))
    canvas.paste(upsc_crop, (MARGIN + panel_w + MARGIN + DIVIDER + MARGIN, LABEL_H + MARGIN))

    draw = ImageDraw.Draw(canvas)
    font = _try_load_font(22)
    draw.text((MARGIN + 6, 10),
              f"ORIGINAL  {ow}×{oh}  (zoomed ×{scale_x:.0f}, nearest-neighbour)",
              fill=(255, 190, 80), font=font)
    draw.text((MARGIN + panel_w + MARGIN + DIVIDER + MARGIN + 6, 10),
              f"REAL-ESRGAN  {uw}×{uh}  (actual pixels)",
              fill=(90, 220, 120), font=font)

    comp_path.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(str(comp_path), "JPEG", quality=92)


# ---------------------------------------------------------------------------
# Reporting
# ---------------------------------------------------------------------------
def _fmt_size(w: int, h: int) -> str:
    return f"{w}×{h}"


def print_report(results: list[UpscaleResult], input_dir: Path) -> None:
    col_file = 42
    print()
    print(f"  {'FILE':<{col_file}}  {'STATUS':<6}  {'ORIGINAL':>11}  {'OUTPUT':>11}  {'TIME':>6}")
    print("  " + "─" * (col_file + 44))

    errors = 0
    total_time = 0.0
    for r in results:
        try:
            rel = str(r.src.relative_to(input_dir))
        except ValueError:
            rel = r.src.name
        status = "OK" if r.ok else "ERROR"
        orig = _fmt_size(*r.orig_size) if r.ok else "-"
        out  = _fmt_size(*r.out_size)  if r.ok else "-"
        t    = f"{r.elapsed:.1f}s"
        print(f"  {rel:<{col_file}}  {status:<6}  {orig:>11}  {out:>11}  {t:>6}")
        if not r.ok:
            print(f"    └─ {r.error}")
            errors += 1
        total_time += r.elapsed

    ok = len(results) - errors
    print()
    print(f"  Processed: {ok}/{len(results)}  |  Errors: {errors}  |  Total time: {total_time:.1f}s")
    print()


# ---------------------------------------------------------------------------
# CLI
# ---------------------------------------------------------------------------
def parse_args() -> argparse.Namespace:
    p = argparse.ArgumentParser(
        description="AI super-resolution for kaeli anime art (Real-ESRGAN)",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=textwrap.dedent("""\
            Pipeline order:
              1. python tools/upscale_anime.py          ← run this first
              2. python tools/remove_bg.py --input <upscaled_dir>

            Scale recommendation:
              --scale 2  (default) — 896×1344 → 1792×2688.  Best for web/game use.
              --scale 4            — 896×1344 → 3584×5376.  Only if printing.
        """),
    )
    p.add_argument("--input",  "-i", type=Path, default=DEFAULT_INPUT,
                   help=f"Source folder (default: {DEFAULT_INPUT})")
    p.add_argument("--output", "-o", type=Path, default=DEFAULT_OUTPUT,
                   help=f"Destination folder (default: {DEFAULT_OUTPUT})")
    p.add_argument("--glob",   "-g", default=DEFAULT_GLOB,
                   help=f"File glob pattern (default: {DEFAULT_GLOB!r})")
    p.add_argument("--scale",  "-s", type=int, choices=[2, 4], default=DEFAULT_SCALE,
                   help=f"Output upscale factor (default: {DEFAULT_SCALE})")
    p.add_argument("--tile",   "-t", type=int, default=DEFAULT_TILE,
                   help=f"Tile size for VRAM-limited GPUs in px (default: {DEFAULT_TILE}). "
                        "Set 0 to disable tiling (needs a lot of VRAM).")
    p.add_argument("--no-compare", action="store_true",
                   help="Skip generating before/after comparison crops")
    return p.parse_args()


def collect_images(input_dir: Path, glob_pattern: str) -> list[Path]:
    return sorted(input_dir.rglob(glob_pattern))


def main() -> int:
    args = parse_args()

    input_dir: Path = args.input.resolve()
    if not input_dir.exists():
        print(f"ERROR: input directory does not exist: {input_dir}")
        return 1

    images = collect_images(input_dir, args.glob)
    if not images:
        print(f"No files matching {args.glob!r} found under {input_dir}")
        return 0

    output_dir: Path = args.output.resolve()
    compare_dir: Path = output_dir / "comparisons"

    use_cuda, device_desc = detect_device()

    print(f"\n  Model  : {MODEL_NAME}  (4x internal, {args.scale}x output)")
    print(f"  Device : {device_desc}")
    print(f"  Tile   : {args.tile if args.tile > 0 else 'disabled (full image)'}")
    print(f"  Input  : {input_dir}")
    print(f"  Output : {output_dir}")
    print(f"  Files  : {len(images)}")
    print(f"  Scale  : {args.scale}x")
    print()

    weights = ensure_weights()
    print(f"  Loading model …", end="", flush=True)
    upsampler = build_upsampler(weights, use_cuda, args.tile)
    print(" ready.\n")

    results: list[UpscaleResult] = []
    for i, src in enumerate(images, 1):
        rel = src.relative_to(input_dir)
        dst = output_dir / rel.with_suffix(".png")
        comp = compare_dir / (rel.stem + "_upscale_compare.jpg")

        print(f"  [{i:2}/{len(images)}] {rel.name} …", end="", flush=True)

        result = upscale_image(upsampler, src, dst, args.scale)
        results.append(result)

        if result.ok:
            print(f"  {_fmt_size(*result.orig_size)} → {_fmt_size(*result.out_size)}"
                  f"  ({result.elapsed:.1f}s)", end="")
            if not args.no_compare:
                try:
                    save_comparison(src, dst, comp)
                    print(f"  [compare saved]", end="")
                except Exception as exc:
                    print(f"  [compare WARN: {exc}]", end="")
            print()
        else:
            print(f"  ERROR: {result.error}")

    print_report(results, input_dir)

    if not args.no_compare and any(r.ok for r in results):
        print(f"  Comparison crops: {compare_dir}")
        print()

    print("  Next step:")
    print(f"    python tools/remove_bg.py --input \"{output_dir}\" --glob \"*.png\"")
    print()

    return 0 if all(r.ok for r in results) else 1


if __name__ == "__main__":
    sys.exit(main())
