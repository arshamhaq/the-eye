"""Deterministic white-matte extraction of the user-supplied HQ artwork.

Never resize the character sources. Only the separate mountain backdrop is
enlarged for the optional composed landscape. Runtime layers it separately.
"""
from pathlib import Path
import shutil
import cv2
import numpy as np
from PIL import Image
from scipy.ndimage import distance_transform_edt
from build_icon import build_icon

ROOT = Path(__file__).resolve().parents[1]
ORIGINALS = ROOT / 'artwork/originals'
ASSETS = ROOT / 'src/TheEye/Pets/Triangle/Assets'


def extract_sprite(source):
    rgb = np.asarray(source.convert('RGB')).astype(np.float64)
    # Known opaque dark ink / saturated yellow. The white eye is foreground,
    # unlike the white gaps between limbs. Keep the complete eye inside its ink.
    core = rgb.min(axis=2) < 125
    _, regions = cv2.connectedComponents((~core).astype(np.uint8))
    eye_region = regions[570, 440]
    assert eye_region != regions[0, 0], 'Eye boundary must be closed.'
    core |= regions == eye_region
    core = cv2.erode(core.astype(np.uint8), np.ones((3, 3), np.uint8)).astype(bool)
    distance, nearest = distance_transform_edt(~core, return_indices=True)
    foreground = rgb[nearest[0], nearest[1]]
    deficit = 255 - rgb.min(axis=2)
    # Smooth propagated edge colors to prevent nearest-neighbor Voronoi rays
    # in the very soft aura. The original solid pixels remain unchanged.
    foreground_deficit = cv2.GaussianBlur(np.maximum(255 - foreground.min(axis=2), 1), (0, 0), 10)
    alpha = np.clip(deficit / foreground_deficit, 0, 1)
    alpha[core] = 1
    # Remove near-white compression noise, not the golden glow.
    chroma = rgb.max(axis=2) - rgb.min(axis=2)
    alpha[(distance > 3) & (chroma < 3)] = 0
    alpha[alpha < 2 / 255] = 0
    # Undo the white matte: merely setting alpha leaves white fringes on dark.
    unmatte = (rgb - 255 * (1 - alpha[..., None])) / np.maximum(alpha[..., None], 1e-8)
    rgba = np.dstack((np.clip(unmatte, 0, 255), alpha * 255))
    return Image.fromarray(np.rint(rgba).astype(np.uint8), 'RGBA')


def extend_clouds(source):
    # Native 912x1120 insert on a 2240x1260 (16:9) canvas. No character
    # downsampling, cropping, generative replacement or eye/pose modification.
    rgb = np.asarray(source.convert('RGB'))
    x, y = 1250, 70
    height, width = rgb.shape[:2]
    rows = np.clip(np.arange(1260) - y, 0, height - 1)
    left_edge = rgb[rows, 0].astype(float)
    weight = np.clip(np.arange(2240) / x, 0, 1)[None, :, None]
    cream = np.array([255, 251, 235])
    smooth_edge = cv2.GaussianBlur(left_edge[:, None, :], (1, 0), sigmaX=0, sigmaY=55)
    # Blend back to the exact edge only near the source, so clouds do not
    # become long horizontal stripes throughout the text area.
    cloud_edge = smooth_edge * (1 - weight ** 12) + left_edge[:, None, :] * weight ** 12
    scene = cream * (1 - weight) + cloud_edge * weight
    scene[:, x + width:] = rgb[rows, -1, None]
    scene[:y, x:x + width] = rgb[0]
    scene[y + height:, x:x + width] = rgb[-1]
    canvas = Image.fromarray(np.rint(scene).astype(np.uint8))
    canvas.paste(source.convert('RGB'), (x, y))
    return canvas


def main():
    standing = Image.open(ORIGINALS / 'triangle-standing-hq.png')
    meditation = Image.open(ORIGINALS / 'triangle-meditation-hq.png')
    assert standing.size == meditation.size == (912, 1120)
    sprite = extract_sprite(standing)
    sprite.save(ASSETS / 'float.png')
    shutil.copyfile(ORIGINALS / 'triangle-meditation-hq.png', ASSETS / 'meditation-original.png')
    extend_clouds(meditation).save(ASSETS / 'resting.png')
    mountain = Image.open(ASSETS / 'mountains.png').convert('RGBA')
    mountain = mountain.resize((2240, 1260), Image.Resampling.LANCZOS)
    mountain.alpha_composite(sprite, (1230, 70))
    mountain.convert('RGB').save(ASSETS / 'main.png')
    # Icon sizes are platform-required derivatives, not animation sources.
    build_icon()
    preview = ROOT / 'artifacts/artwork-review'
    preview.mkdir(parents=True, exist_ok=True)
    for label, color in [('dark', '#141823'), ('white', '#ffffff')]:
        base = Image.new('RGBA', sprite.size, color)
        base.alpha_composite(sprite)
        base.convert('RGB').save(preview / f'sprite-on-{label}.png')
    assert sprite.getpixel((0, 0))[3] == 0
    assert sprite.getpixel((440, 570))[3] == 255
    restored = Image.open(ASSETS / 'resting.png').crop((1250,70,2162,1190))
    assert np.array_equal(np.array(restored), np.array(meditation.convert('RGB')))
    print('Native 912x1120 sprite; original meditation preserved 1:1 in 2240x1260 rest scene.')


if __name__ == '__main__':
    main()
