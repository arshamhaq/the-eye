"""Build clean 1920x1080 dragon sprites from the original generated frames.

Usage:
    python tools/build_dragon_assets.py SOURCE_DIR OUTPUT_DIR

This is an asset-authoring helper, not a runtime dependency of EyeDragon.
"""

from __future__ import annotations

import argparse
from pathlib import Path

import cv2
import numpy as np
from PIL import Image
from rembg import new_session, remove
from scipy.ndimage import distance_transform_edt


SOURCES = {
    "idle.png": "exec-e0721cd0-f762-4a87-b196-ab710fc92ee6.png",
    "walk_01.png": "exec-bb9fc1f3-fb18-4522-84ef-ef8e73c22fb5.png",
    "walk_02.png": "exec-4c28b6e7-d0a6-486b-a6e7-0e23ec5bc911.png",
    "walk_03.png": "exec-7e69861a-3024-458c-98be-d1c335bff742.png",
    "warning.png": "exec-522267fe-65ba-4026-bc22-6d0c70eeb7c0.png",
    "countdown.png": "exec-7dbaca6c-9575-44ca-8175-2eebb0200bfe.png",
    "go_rest.png": "exec-bfeb4da3-c67c-46d1-b247-861a55b10d60.png",
    "resting.png": "exec-1c5e112a-ee8a-4cce-9460-edbe4569b02c.png",
    "happy.png": "exec-c06ca922-7f28-43d3-83ea-17360871e573.png",
}

CANVAS_SIZE = (1920, 1080)
MAX_SUBJECT_SIZE = (1040, 1040)


def clean_edge_colours(rgba: np.ndarray, original_rgb: np.ndarray) -> np.ndarray:
    """Replace translucent matte colours with the nearest solid subject colour."""
    alpha = rgba[:, :, 3]

    # Generated source files contain a literal gray/white checkerboard.  Rembg
    # can mistake enclosed checkerboard regions (inside a curled tail or between
    # a wing and torso) for foreground.  Remove those near-neutral bright pixels
    # before edge decontamination.  The dragon artwork is violet-tinted, so its
    # pale scales remain outside this deliberately narrow neutral threshold.
    channel_range = np.ptp(original_rgb.astype(np.int16), axis=2)
    brightness = original_rgb.mean(axis=2)
    baked_checker = (channel_range <= 10) & (brightness >= 155)
    alpha[baked_checker] = 0

    # The luminous tail flame in the source was composited over white, leaving
    # a pale matte immediately around the purple flame.  Restrict cleanup to a
    # dilated mask around strongly magenta flame pixels so pale chest scales and
    # eye highlights elsewhere remain intact.
    hsv = cv2.cvtColor(original_rgb, cv2.COLOR_RGB2HSV)
    flame_seed = (
        (hsv[:, :, 0] >= 130)
        & (hsv[:, :, 0] <= 175)
        & (hsv[:, :, 1] >= 145)
        & (hsv[:, :, 2] >= 205)
    ).astype(np.uint8)
    component_count, component_labels, stats, _ = cv2.connectedComponentsWithStats(flame_seed, 8)
    if component_count > 1:
        largest_flame = 1 + int(np.argmax(stats[1:, cv2.CC_STAT_AREA]))
        flame = component_labels == largest_flame
        near_flame = cv2.dilate(flame.astype(np.uint8), np.ones((35, 35), np.uint8)) > 0
        pale_matte = near_flame & (hsv[:, :, 1] <= 115) & (hsv[:, :, 2] >= 165)
        alpha[pale_matte] = 0
    alpha[alpha < 12] = 0
    solid = alpha >= 230
    if not solid.any():
        raise RuntimeError("Background removal produced no solid foreground.")

    nearest = distance_transform_edt(~solid, return_distances=False, return_indices=True)
    edge = (alpha > 0) & ~solid
    nearest_rgb = rgba[nearest[0], nearest[1], :3]
    rgba[edge, :3] = nearest_rgb[edge]
    rgba[alpha == 0, :3] = 0
    return rgba


def place_on_canvas(image: Image.Image) -> Image.Image:
    alpha = np.asarray(image.getchannel("A"))
    points = cv2.findNonZero((alpha > 0).astype(np.uint8))
    if points is None:
        raise RuntimeError("Sprite is completely transparent.")

    x, y, width, height = cv2.boundingRect(points)
    subject = image.crop((x, y, x + width, y + height))
    scale = min(MAX_SUBJECT_SIZE[0] / width, MAX_SUBJECT_SIZE[1] / height)
    size = (max(1, round(width * scale)), max(1, round(height * scale)))
    subject = subject.resize(size, Image.Resampling.LANCZOS)

    canvas = Image.new("RGBA", CANVAS_SIZE, (0, 0, 0, 0))
    left = (CANVAS_SIZE[0] - size[0]) // 2
    top = max(20, (CANVAS_SIZE[1] - size[1]) // 2)
    canvas.alpha_composite(subject, (left, top))
    return canvas


def build(source: Path, destination: Path, session: object) -> None:
    original = Image.open(source).convert("RGB")
    extracted = remove(
        original,
        session=session,
        alpha_matting=True,
        alpha_matting_foreground_threshold=235,
        alpha_matting_background_threshold=12,
        alpha_matting_erode_size=5,
        post_process_mask=True,
    ).convert("RGBA")
    rgba = clean_edge_colours(np.array(extracted), np.array(original))
    final = place_on_canvas(Image.fromarray(rgba, mode="RGBA"))
    destination.parent.mkdir(parents=True, exist_ok=True)
    final.save(destination, format="PNG", optimize=True)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("source_dir", type=Path)
    parser.add_argument("output_dir", type=Path)
    args = parser.parse_args()

    session = new_session("u2net")
    for output_name, source_name in SOURCES.items():
        source = args.source_dir / source_name
        destination = args.output_dir / output_name
        print(f"Building {output_name} from {source.name}...", flush=True)
        build(source, destination, session)


if __name__ == "__main__":
    main()
