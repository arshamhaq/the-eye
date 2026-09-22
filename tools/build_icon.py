"""Package the supplied transparent eye PNG into Windows icon sizes."""
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]


def build_icon():
    with Image.open(ROOT / 'artwork/originals/eye-icon-reference.png') as source:
        assert source.size == (512, 512) and source.mode == 'RGBA'
        source.save(ROOT / 'src/TheEye/Assets/TheEye.ico',
                    sizes=[(n, n) for n in (16, 20, 24, 32, 40, 48, 64, 96, 128, 256)])
    print('Packaged the supplied eye, preserving transparency, in 10 Windows icon sizes.')


if __name__ == '__main__':
    build_icon()
