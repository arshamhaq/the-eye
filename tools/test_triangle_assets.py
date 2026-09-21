"""Regression checks for the supplied artwork, alpha and native resolution."""
from pathlib import Path
import unittest
import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / 'src/TheEye/Pets/Triangle/Assets'
ORIGINALS = ROOT / 'artwork/originals'


class TriangleArtworkTests(unittest.TestCase):
    def test_native_sprite_resolution(self):
        with Image.open(ASSETS / 'float.png') as sprite:
            self.assertEqual(sprite.size, (912, 1120))
            self.assertEqual(sprite.mode, 'RGBA')

    def test_white_eye_is_not_segmented_away(self):
        with Image.open(ASSETS / 'float.png') as sprite:
            self.assertEqual(sprite.getpixel((440, 570)), (255, 255, 255, 255))
            self.assertEqual(sprite.getpixel((515, 540))[3], 255)

    def test_background_and_glow_have_real_alpha(self):
        with Image.open(ASSETS / 'float.png') as sprite:
            rgba = np.array(sprite)
        self.assertEqual(int(rgba[0, 0, 3]), 0)
        self.assertEqual(int(rgba[-1, -1, 3]), 0)
        self.assertLess(int(rgba[700, 120, 3]), 200)  # open gap inside bent arm
        translucent = (rgba[:, :, 3] > 5) & (rgba[:, :, 3] < 200)
        golden = (rgba[:, :, 0].astype(int) - rgba[:, :, 2].astype(int)) > 60
        self.assertGreater(int(np.count_nonzero(translucent & golden)), 20000)

    def test_meditation_original_is_unchanged(self):
        self.assertEqual((ASSETS / 'meditation-original.png').read_bytes(),
                         (ORIGINALS / 'triangle-meditation-hq.png').read_bytes())
        with Image.open(ORIGINALS / 'triangle-meditation-hq.png') as original:
            source = np.array(original.convert('RGB'))
        with Image.open(ASSETS / 'resting.png') as rest:
            self.assertEqual(rest.size, (2240, 1260))
            self.assertEqual(rest.mode, 'RGB')
            insert = np.array(rest.crop((1250, 70, 2162, 1190)))
        np.testing.assert_array_equal(source, insert)

    def test_white_composite_matches_supplied_standing_art(self):
        with Image.open(ASSETS / 'float.png') as sprite:
            white = Image.new('RGBA', sprite.size, 'white')
            white.alpha_composite(sprite)
        with Image.open(ORIGINALS / 'triangle-standing-hq.png') as original:
            difference = np.abs(np.array(white.convert('RGB')).astype(float) -
                                np.array(original.convert('RGB')).astype(float))
        self.assertLess(float(difference.mean()), 1.0)


if __name__ == '__main__':
    unittest.main(verbosity=2)
