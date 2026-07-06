from __future__ import annotations

import argparse
import math
import subprocess
from pathlib import Path

import cv2
import numpy as np


def gaussian(value: np.ndarray, center: float, sigma: float) -> np.ndarray:
    return np.exp(-0.5 * ((value - center) / sigma) ** 2)


def sigmoid(value: np.ndarray, softness: float) -> np.ndarray:
    return 1.0 / (1.0 + np.exp(-value / softness))


def render_frame(
    image: np.ndarray,
    x_grid: np.ndarray,
    y_grid: np.ndarray,
    nx: np.ndarray,
    ny: np.ndarray,
    t: float,
    strength: float,
    sparkles: np.ndarray,
    blink: bool,
) -> np.ndarray:
    height, width = image.shape[:2]
    cx = width * 0.515
    cy = height * 0.53

    breath = math.sin(math.tau * t)
    breath_peak = 0.5 + 0.5 * breath
    # Shorter elastic follow-through than V1. Left/right are intentionally not identical; a single
    # shared transform makes the bust read like one rubber surface.
    left_jiggle = math.sin(math.tau * 1.86 * t + 0.45) + 0.18 * math.sin(math.tau * 3.75 * t + 1.0)
    right_jiggle = math.sin(math.tau * 1.86 * t + 0.92) + 0.16 * math.sin(math.tau * 3.75 * t + 1.55)
    sway = math.sin(math.tau * t + 0.8)

    lower_gate = sigmoid(ny - 0.67, 0.03)
    upper_gate = sigmoid(0.97 - ny, 0.04)
    face_lock = gaussian(nx, 0.52, 0.17) * gaussian(ny, 0.38, 0.15)
    head_block = gaussian(nx, 0.51, 0.22) * gaussian(ny, 0.36, 0.21)

    left_bust = gaussian(nx, 0.385, 0.075) * gaussian(ny, 0.825, 0.075) * lower_gate * upper_gate
    right_bust = gaussian(nx, 0.655, 0.075) * gaussian(ny, 0.825, 0.075) * lower_gate * upper_gate
    sternum = gaussian(nx, 0.52, 0.13) * gaussian(ny, 0.76, 0.14) * lower_gate
    glove = gaussian(nx, 0.455, 0.10) * gaussian(ny, 0.735, 0.09)

    # Smaller, local motion. Mostly vertical with tiny separate lateral response; shoulders are
    # excluded by the lower_gate so they do not stretch with the bust.
    left_bust_y = strength * (3.3 * breath + 4.6 * left_jiggle) * left_bust
    right_bust_y = strength * (3.3 * breath + 4.2 * right_jiggle) * right_bust
    left_bust_x = strength * (-1.2 * breath_peak + 1.1 * left_jiggle) * left_bust
    right_bust_x = strength * (1.2 * breath_peak - 1.0 * right_jiggle) * right_bust
    torso_y = strength * (2.2 * breath) * sternum
    glove_y = strength * (1.4 * breath + 1.4 * left_jiggle) * glove

    left_hair = gaussian(nx, 0.20, 0.15) * gaussian(ny, 0.54, 0.30)
    right_hair = gaussian(nx, 0.81, 0.18) * gaussian(ny, 0.53, 0.32)
    hair = np.clip((left_hair + right_hair) * sigmoid(ny - 0.14, 0.04), 0.0, 1.0)
    hair_wave = np.sin(math.tau * t + ny * 8.5) + 0.45 * np.sin(math.tau * 2.0 * t + ny * 5.2)
    hair_x = strength * 8.0 * hair_wave * hair
    hair_y = strength * 2.8 * np.cos(math.tau * t + ny * 6.0) * hair

    halo = gaussian(nx, 0.52, 0.34) * gaussian(ny, 0.13, 0.09)
    halo_x = strength * 3.4 * sway * halo
    halo_y = strength * 1.8 * math.cos(math.tau * t + 0.7) * halo

    zoom = 1.0 + 0.0018 * breath
    map_x = cx + (x_grid - cx) / zoom
    map_y = cy + (y_grid - cy) / zoom

    head_y = strength * 1.2 * breath * head_block
    dx = left_bust_x + right_bust_x + hair_x + halo_x
    dy = left_bust_y + right_bust_y + torso_y + glove_y + hair_y + halo_y + head_y
    lock = 1.0 - 0.9 * face_lock
    map_x = (map_x - dx * lock).astype(np.float32)
    map_y = (map_y - dy * lock).astype(np.float32)

    frame = cv2.remap(image, map_x, map_y, cv2.INTER_CUBIC, borderMode=cv2.BORDER_REFLECT_101)
    if blink:
        add_blink(frame, t, strength)
    add_sparkles(frame, sparkles, t, strength)
    return frame


def blink_amount(t: float) -> float:
    # One clean gacha idle blink per 4s loop plus a tiny soft half-blink. The close/open curve is
    # intentionally fast; slow eyelids look sleepy and can smear the eye art.
    centers = (0.64, 0.17)
    widths = (0.055, 0.032)
    amounts = (1.0, 0.28)
    value = 0.0
    for center, width, amount in zip(centers, widths, amounts):
        dist = abs((t - center + 0.5) % 1.0 - 0.5)
        if dist < width:
            value = max(value, amount * (0.5 + 0.5 * math.cos(math.pi * dist / width)))
    return value


def add_blink(frame: np.ndarray, t: float, strength: float) -> None:
    amount = blink_amount(t)
    if amount <= 0.01:
        return

    height, width = frame.shape[:2]
    overlay = frame.copy()
    # Coordinates tuned for Eloa's current thumb crop.
    eyes = [
        (0.405, 0.366, 0.039, 0.012, -6),
        (0.548, 0.363, 0.038, 0.012, 6),
    ]
    for ex, ey, ew, eh, angle in eyes:
        cx = int(ex * width)
        cy = int(ey * height)
        axes = (max(8, int(ew * width)), max(3, int(eh * height * (0.45 + 0.45 * amount))))
        line_axes = (axes[0], max(2, int(axes[1] * (0.42 + 0.1 * amount))))
        line_color = (78, 38, 50)
        glint_color = (228, 170, 188)
        lid_y = cy + int(amount * height * 0.006)
        cv2.ellipse(
            overlay,
            (cx, lid_y - max(1, int(height * 0.004))),
            line_axes,
            angle,
            10,
            170,
            glint_color,
            max(1, int(1 + amount)),
            lineType=cv2.LINE_AA,
        )
        cv2.ellipse(
            overlay,
            (cx, lid_y),
            line_axes,
            angle,
            8,
            172,
            line_color,
            max(1, int(1 + 3 * amount)),
            lineType=cv2.LINE_AA,
        )

    alpha = min(0.8, 0.1 + amount * 0.7)
    cv2.addWeighted(overlay, alpha, frame, 1.0 - alpha, 0, dst=frame)


def sample_skin(frame: np.ndarray, cx: int, cy: int, width: int, height: int) -> tuple[int, int, int]:
    x0 = max(0, cx - int(width * 0.018))
    x1 = min(width, cx + int(width * 0.018))
    y0 = min(height - 1, cy + int(height * 0.032))
    y1 = min(height, y0 + int(height * 0.026))
    patch = frame[y0:y1, x0:x1]
    base = np.array([226.0, 186.0, 179.0], dtype=np.float32)
    if patch.size == 0:
        return tuple(int(v) for v in base)
    color = np.median(patch.reshape(-1, 3), axis=0)
    if color.mean() < 130 or color[0] < 155:
        color = base
    else:
        color = color * 0.35 + base * 0.65
    color = np.clip(color * 1.02 + np.array([3, 1, 1]), 0, 255)
    return tuple(int(v) for v in color)


def add_sparkles(frame: np.ndarray, sparkles: np.ndarray, t: float, strength: float) -> None:
    height, width = frame.shape[:2]
    overlay = np.zeros_like(frame, dtype=np.float32)
    for sx, sy, phase, size, amp in sparkles:
        pulse = max(0.0, math.sin(math.tau * t + phase)) ** 2
        if pulse < 0.08:
            continue
        x = int(sx * width)
        y = int(sy * height)
        radius = max(1, int(size * width))
        color = np.array([255.0, 205.0, 226.0], dtype=np.float32) * (0.18 + 0.2 * strength) * amp * pulse
        cv2.circle(overlay, (x, y), radius, color.tolist(), -1, lineType=cv2.LINE_AA)
        cv2.circle(overlay, (x, y), max(1, radius // 3), (255.0, 246.0, 232.0), -1, lineType=cv2.LINE_AA)
    np.clip(frame.astype(np.float32) + overlay, 0, 255, out=overlay)
    frame[:] = overlay.astype(np.uint8)


def make_sparkles(count: int) -> np.ndarray:
    rng = np.random.default_rng(6117)
    points: list[tuple[float, float, float, float, float]] = []
    while len(points) < count:
        x = float(rng.uniform(0.05, 0.95))
        y = float(rng.uniform(0.04, 0.58))
        # Avoid the face; keep sparkles mostly in cathedral light and jewelry space.
        if 0.34 < x < 0.70 and 0.22 < y < 0.52:
            continue
        points.append((
            x,
            y,
            float(rng.uniform(0, math.tau)),
            float(rng.uniform(0.0016, 0.0034)),
            float(rng.uniform(0.45, 1.0)),
        ))
    return np.array(points, dtype=np.float32)


def encode_webm(
    input_path: Path,
    output_path: Path,
    seconds: float,
    fps: int,
    strength: float,
    max_size: int,
    blink: bool,
) -> None:
    bgr = cv2.imread(str(input_path), cv2.IMREAD_COLOR)
    if bgr is None:
        raise SystemExit(f"Could not read input image: {input_path}")
    image = cv2.cvtColor(bgr, cv2.COLOR_BGR2RGB)
    height, width = image.shape[:2]
    longest = max(width, height)
    if max_size > 0 and longest > max_size:
        scale = max_size / longest
        image = cv2.resize(
            image,
            (int(round(width * scale)), int(round(height * scale))),
            interpolation=cv2.INTER_AREA,
        )
    height, width = image.shape[:2]

    x_grid, y_grid = np.meshgrid(
        np.arange(width, dtype=np.float32),
        np.arange(height, dtype=np.float32),
    )
    nx = x_grid / max(1, width - 1)
    ny = y_grid / max(1, height - 1)
    sparkles = make_sparkles(34)

    total_frames = max(2, int(round(seconds * fps)))
    output_path.parent.mkdir(parents=True, exist_ok=True)
    command = [
        "ffmpeg",
        "-y",
        "-f",
        "rawvideo",
        "-pix_fmt",
        "rgb24",
        "-s",
        f"{width}x{height}",
        "-r",
        str(fps),
        "-i",
        "pipe:0",
        "-an",
        "-c:v",
        "libvpx",
        "-deadline",
        "realtime",
        "-cpu-used",
        "8",
        "-pix_fmt",
        "yuv420p",
        "-crf",
        "10",
        "-b:v",
        "1800k",
        str(output_path),
    ]
    with subprocess.Popen(command, stdin=subprocess.PIPE) as proc:
        assert proc.stdin is not None
        for frame_index in range(total_frames):
            t = frame_index / total_frames
            frame = render_frame(image, x_grid, y_grid, nx, ny, t, strength, sparkles, blink)
            proc.stdin.write(frame.tobytes())
        proc.stdin.close()
        if proc.wait() != 0:
            raise SystemExit("ffmpeg failed while encoding the loop")


def main() -> None:
    parser = argparse.ArgumentParser(description="Animate a Kaeli thumb into a breathing/jiggle WebM loop.")
    parser.add_argument("--input", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--seconds", type=float, default=4.0)
    parser.add_argument("--fps", type=int, default=30)
    parser.add_argument("--strength", type=float, default=1.28)
    parser.add_argument("--max-size", type=int, default=960)
    parser.add_argument("--blink", action="store_true", help="Experimental procedural eyelid line blink.")
    args = parser.parse_args()
    encode_webm(args.input, args.output, args.seconds, args.fps, args.strength, args.max_size, args.blink)


if __name__ == "__main__":
    main()
