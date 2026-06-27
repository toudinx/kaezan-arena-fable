from __future__ import annotations

import argparse
import json
import subprocess
from pathlib import Path

import cv2
import numpy as np


def read_image(path: Path) -> np.ndarray:
    data = np.fromfile(str(path), dtype=np.uint8)
    image = cv2.imdecode(data, cv2.IMREAD_COLOR)
    if image is None:
        raise FileNotFoundError(f"Could not read image: {path}")
    return cv2.cvtColor(image, cv2.COLOR_BGR2RGB)


def write_image(path: Path, image_rgb: np.ndarray) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    image_bgr = cv2.cvtColor(image_rgb, cv2.COLOR_RGB2BGR)
    ok, encoded = cv2.imencode(".png", image_bgr)
    if not ok:
        raise RuntimeError(f"Could not encode poster: {path}")
    encoded.tofile(str(path))


def gaussian(xx: np.ndarray, yy: np.ndarray, cx: float, cy: float, sx: float, sy: float) -> np.ndarray:
    return np.exp(-(((xx - cx) / sx) ** 2 + ((yy - cy) / sy) ** 2) * 0.5).astype(np.float32)


def draw_additive_glow(
    overlay: np.ndarray,
    center: tuple[int, int],
    radius: int,
    color: tuple[int, int, int],
    strength: float,
) -> None:
    cv2.circle(overlay, center, radius, tuple(float(c) * strength for c in color), -1, lineType=cv2.LINE_AA)


class IdleRenderer:
    def __init__(self, source: np.ndarray, size: int, fps: int, duration: float) -> None:
        self.size = size
        self.fps = fps
        self.frames = int(round(fps * duration))

        self.base = cv2.resize(source, (size, size), interpolation=cv2.INTER_AREA).astype(np.float32)
        yy, xx = np.mgrid[0:size, 0:size].astype(np.float32)
        self.xx = xx
        self.yy = yy

        w = h = float(size)
        self.torso = gaussian(xx, yy, 0.56 * w, 0.73 * h, 0.42 * w, 0.23 * h)
        self.shoulders = gaussian(xx, yy, 0.52 * w, 0.67 * h, 0.45 * w, 0.16 * h)
        chest_gate = 1.0 / (1.0 + np.exp(-(yy - 0.68 * h) / (0.035 * h)))
        bottom_gate = 1.0 / (1.0 + np.exp((yy - 0.94 * h) / (0.040 * h)))
        self.breast_left = (
            gaussian(xx, yy, 0.47 * w, 0.80 * h, 0.125 * w, 0.125 * h) * chest_gate * bottom_gate
        ).astype(np.float32)
        self.breast_right = (
            gaussian(xx, yy, 0.68 * w, 0.80 * h, 0.130 * w, 0.125 * h) * chest_gate * bottom_gate
        ).astype(np.float32)
        self.hair_left = gaussian(xx, yy, 0.30 * w, 0.61 * h, 0.23 * w, 0.34 * h)
        self.hair_right = gaussian(xx, yy, 0.78 * w, 0.58 * h, 0.20 * w, 0.31 * h)
        self.bangs = gaussian(xx, yy, 0.42 * w, 0.36 * h, 0.17 * w, 0.14 * h)
        self.jewelry = np.maximum(
            gaussian(xx, yy, 0.48 * w, 0.58 * h, 0.08 * w, 0.11 * h),
            gaussian(xx, yy, 0.62 * w, 0.67 * h, 0.16 * w, 0.08 * h),
        )

        self.face_guard = gaussian(xx, yy, 0.54 * w, 0.40 * h, 0.26 * w, 0.17 * h)
        self.lower_guard = 1.0 - np.clip(self.face_guard * 0.9, 0.0, 0.92)

        dist = np.sqrt(((xx - 0.52 * w) / (0.72 * w)) ** 2 + ((yy - 0.50 * h) / (0.78 * h)) ** 2)
        self.vignette = np.clip(1.08 - dist * 0.22, 0.82, 1.04)[..., None].astype(np.float32)

        rng = np.random.default_rng(7331)
        self.particles = {
            "x": rng.uniform(0.02 * w, 0.98 * w, 78),
            "y": rng.uniform(0.12 * h, 0.96 * h, 78),
            "r": rng.uniform(1.0, 3.4, 78),
            "speed": rng.uniform(14.0, 42.0, 78),
            "phase": rng.uniform(0.0, np.pi * 2.0, 78),
            "alpha": rng.uniform(0.22, 0.78, 78),
        }

    def frame(self, index: int) -> np.ndarray:
        phase = (index / self.frames) * np.pi * 2.0
        breath = np.sin(phase - np.pi * 0.5)
        breath_soft = 0.5 - 0.5 * np.cos(phase)
        spring = 0.72 * np.sin(phase - 0.62) + 0.22 * np.sin(phase * 2.0 - 1.35)
        sway = 0.82 * np.sin(phase + 1.0) + 0.18 * np.sin(phase * 2.0 + 0.35)

        map_x = self.xx.copy()
        map_y = self.yy.copy()

        torso_dy = -4.2 * breath
        shoulder_dy = -1.8 * breath
        torso_dx = 1.4 * np.sin(phase + 0.25)
        map_x -= torso_dx * self.torso * self.lower_guard
        map_y -= torso_dy * self.torso * self.lower_guard
        map_y -= shoulder_dy * self.shoulders * self.lower_guard

        hair_mask = (0.75 * self.hair_left + 0.56 * self.hair_right + 0.34 * self.bangs)
        map_x -= (5.8 * sway) * hair_mask
        map_y -= (1.6 * np.sin(phase + 1.8)) * hair_mask

        map_x -= (2.2 * np.sin(phase + 1.4)) * self.jewelry
        map_y -= (2.8 * np.sin(phase + 2.0)) * self.jewelry

        warped = cv2.remap(
            self.base,
            map_x,
            map_y,
            interpolation=cv2.INTER_CUBIC,
            borderMode=cv2.BORDER_REFLECT_101,
        )

        breast_follow = -1.7 * breath
        breast_jiggle = 4.8 * spring
        left_dy = breast_follow + breast_jiggle
        right_dy = breast_follow + 4.35 * (0.78 * np.sin(phase - 0.70) + 0.20 * np.sin(phase * 2.0 - 1.55))
        left_dx = -0.9 * spring
        right_dx = 0.75 * spring

        left_layer = cv2.remap(
            self.base,
            np.asarray(map_x - left_dx, dtype=np.float32),
            np.asarray(map_y - left_dy, dtype=np.float32),
            interpolation=cv2.INTER_CUBIC,
            borderMode=cv2.BORDER_REFLECT_101,
        )
        right_layer = cv2.remap(
            self.base,
            np.asarray(map_x - right_dx, dtype=np.float32),
            np.asarray(map_y - right_dy, dtype=np.float32),
            interpolation=cv2.INTER_CUBIC,
            borderMode=cv2.BORDER_REFLECT_101,
        )
        left_alpha = np.clip(self.breast_left * 1.85, 0.0, 0.92)[..., None]
        right_alpha = np.clip(self.breast_right * 1.85, 0.0, 0.92)[..., None]
        warped = warped * (1.0 - left_alpha) + left_layer * left_alpha
        warped = warped * (1.0 - right_alpha) + right_layer * right_alpha

        camera_scale = 1.006 + 0.0018 * breath_soft
        camera_angle = 0.06 * np.sin(phase + 0.55)
        matrix = cv2.getRotationMatrix2D((self.size / 2, self.size / 2), camera_angle, camera_scale)
        matrix[0, 2] += 0.6 * np.sin(phase + 0.7)
        matrix[1, 2] += -0.8 * breath
        warped = cv2.warpAffine(
            warped,
            matrix,
            (self.size, self.size),
            flags=cv2.INTER_CUBIC,
            borderMode=cv2.BORDER_REFLECT_101,
        )

        glow = np.zeros_like(warped, dtype=np.float32)
        pulse = 0.62 + 0.38 * np.sin(phase + 0.35)
        pink = (255, 34, 134)
        rose = (255, 92, 182)
        gold = (255, 178, 66)
        s = self.size

        for center, radius, color, strength in (
            ((int(0.43 * s), int(0.39 * s)), int(0.030 * s), rose, 0.35 + 0.22 * pulse),
            ((int(0.57 * s), int(0.38 * s)), int(0.028 * s), rose, 0.35 + 0.22 * pulse),
            ((int(0.61 * s), int(0.66 * s)), int(0.040 * s), pink, 0.50 + 0.45 * pulse),
            ((int(0.33 * s), int(0.77 * s)), int(0.048 * s), gold, 0.18 + 0.18 * pulse),
        ):
            draw_additive_glow(glow, center, radius, color, strength)

        progress = index / self.frames
        for x, y, r, speed, p, a in zip(
            self.particles["x"],
            self.particles["y"],
            self.particles["r"],
            self.particles["speed"],
            self.particles["phase"],
            self.particles["alpha"],
        ):
            py = (y - speed * progress * 6.0) % self.size
            px = x + np.sin(phase + p) * 5.0
            flicker = 0.45 + 0.55 * np.sin(phase * 1.7 + p) ** 2
            color = (255.0, 28.0 + 80.0 * flicker, 122.0 + 70.0 * flicker)
            cv2.circle(
                glow,
                (int(px), int(py)),
                max(1, int(r)),
                tuple(c * a * flicker for c in color),
                -1,
                lineType=cv2.LINE_AA,
            )

        glow = cv2.GaussianBlur(glow, (0, 0), sigmaX=self.size * 0.008)
        shaded = warped * self.vignette
        shaded = np.clip(shaded + glow * 0.34, 0, 255)
        return shaded.astype(np.uint8)


def encode_video(renderer: IdleRenderer, output: Path, kind: str) -> None:
    output.parent.mkdir(parents=True, exist_ok=True)

    common = [
        "ffmpeg",
        "-y",
        "-f",
        "rawvideo",
        "-pix_fmt",
        "rgb24",
        "-s",
        f"{renderer.size}x{renderer.size}",
        "-r",
        str(renderer.fps),
        "-i",
        "-",
        "-an",
    ]
    if kind == "mp4":
        args = common + [
            "-c:v",
            "libx264",
            "-preset",
            "medium",
            "-crf",
            "18",
            "-pix_fmt",
            "yuv420p",
            str(output),
        ]
    elif kind == "webm":
        args = common + [
            "-c:v",
            "libvpx-vp9",
            "-b:v",
            "0",
            "-crf",
            "30",
            "-row-mt",
            "1",
            "-pix_fmt",
            "yuv420p",
            str(output),
        ]
    else:
        raise ValueError(f"Unsupported video kind: {kind}")

    proc = subprocess.Popen(args, stdin=subprocess.PIPE)
    assert proc.stdin is not None
    try:
        for index in range(renderer.frames):
            proc.stdin.write(renderer.frame(index).tobytes())
    finally:
        proc.stdin.close()

    code = proc.wait()
    if code != 0:
        raise RuntimeError(f"ffmpeg failed with exit code {code} while writing {output}")


def main() -> None:
    parser = argparse.ArgumentParser(description="Create a subtle Kaeli idle breathing loop from one portrait.")
    parser.add_argument("--input", required=True, type=Path)
    parser.add_argument("--out-dir", required=True, type=Path)
    parser.add_argument("--slug", default="rin")
    parser.add_argument("--name", default=None)
    parser.add_argument("--size", default=768, type=int)
    parser.add_argument("--fps", default=24, type=int)
    parser.add_argument("--duration", default=6.0, type=float)
    args = parser.parse_args()

    source = read_image(args.input)
    renderer = IdleRenderer(source, size=args.size, fps=args.fps, duration=args.duration)

    name = args.name or args.slug
    mp4_path = args.out_dir / f"{name}-idle-breathing-preview.mp4"
    webm_path = args.out_dir / f"{name}-idle-loop.webm"
    poster_path = args.out_dir / f"{name}-idle-poster.png"
    recipe_path = args.out_dir / f"{name}-idle.recipe.json"

    write_image(poster_path, renderer.frame(renderer.frames // 4))
    encode_video(renderer, mp4_path, "mp4")
    encode_video(renderer, webm_path, "webm")

    recipe = {
        "input": str(args.input),
        "outputs": {
            "mp4": str(mp4_path),
            "webm": str(webm_path),
            "poster": str(poster_path),
        },
        "size": args.size,
        "fps": args.fps,
        "duration": args.duration,
        "notes": "Single-image local idle: torso breathing, rigid masked bust follow-through, hair sway, glow, deterministic particles.",
    }
    recipe_path.write_text(json.dumps(recipe, indent=2), encoding="utf-8")
    print(json.dumps(recipe, indent=2))


if __name__ == "__main__":
    main()
