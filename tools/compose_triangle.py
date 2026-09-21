"""Composite the supplied reference without downsampling or inventing detail.

The reference is 404x495. Scenes are native 1920x1080; the original character
pixels are retained 1:1. This is not an AI upscale or a newly generated image.
"""
from pathlib import Path
import cv2
import numpy as np
from PIL import Image, ImageFilter

ROOT = Path(__file__).resolve().parents[1]
source = Image.open(ROOT / 'artwork/originals/triangle-reference.png').convert('RGB')
rgb = np.array(source)
h, w = rgb.shape[:2]
mask = np.zeros((h, w), np.uint8)
# Preserve the eye, bow, and brick pattern inside the exact body silhouette.
cv2.fillPoly(mask, [np.array([(63,357),(208,149),(216,144),(222,150),(309,381),(308,393),(299,398),(69,366)])], 255)
gray = cv2.cvtColor(rgb, cv2.COLOR_RGB2GRAY)
# The reference's black hat and limbs are spatially isolated from the forest.
regions = [ [(185,121),(207,125),(226,17),(254,20),(232,130),(257,134),(256,145),(185,133)],
[(14,281),(38,277),(107,290),(96,309),(34,297),(42,309),(79,331),(78,348),(52,346),(59,332),(26,313)],
[(283,319),(315,330),(327,321),(326,280),(322,240),(329,228),(340,244),(350,232),(369,229),(370,244),(348,253),(350,322),(341,345),(320,352),(291,343)],
[(65,388),(87,370),(136,370),(122,388),(92,433),(83,442),(72,435),(61,407)],
[(150,403),(163,398),(168,427),(203,384),(219,384),(183,447),(174,463),(163,461),(150,438)] ]
for polygon in regions:
    region = np.zeros_like(mask)
    cv2.fillPoly(region,[np.array(polygon)],255)
    mask[(region > 0) & (gray < 105)] = 255
mask = cv2.morphologyEx(mask, cv2.MORPH_CLOSE, np.ones((3,3),np.uint8))
# Edge coverage comes from the source; golden aura is a separate translucent
# layer, removing grayscale forest contamination without a white matte.
coverage = cv2.GaussianBlur(mask.astype(np.float32)/255,(3,3),0.45)
glow = np.asarray(Image.fromarray(mask).filter(ImageFilter.GaussianBlur(9))).astype(float)/255
glow = np.clip(glow*0.78,0,0.85) * (1-coverage)
alpha = coverage + glow*(1-coverage)
color = (rgb.astype(float)*coverage[:,:,None] + np.array([255,235,100])*glow[:,:,None]*(1-coverage[:,:,None])) / np.maximum(alpha[:,:,None],0.0001)
rgba = np.dstack((np.clip(color,0,255),alpha*255)).astype(np.uint8)
sprite = Image.fromarray(rgba)
assets = ROOT/'src/TheEye/Pets/Triangle/Assets'
assets.mkdir(parents=True,exist_ok=True)
canvas = Image.new('RGBA',(1920,1080))
canvas.alpha_composite(sprite,(758,292))
canvas.save(assets/'float.png')
# Keep all originals. Never resize the source to manufacture resolution.
for name, upper, lower in [('main',(246,245,235),(214,219,211)),('resting',(255,255,252),(237,233,215))]:
    yy=np.linspace(0,1,1080)[:,None,None]
    arr=np.broadcast_to(np.array(upper)*(1-yy)+np.array(lower)*yy,(1080,1920,3)).astype(np.uint8).copy()
    scene=Image.fromarray(arr).convert('RGBA')
    scene.alpha_composite(sprite,(1290,260))
    scene.convert('RGB').save(assets/f'{name}.png')
sprite.save(ROOT/'artwork/originals/triangle-cutout.png')
sprite.save(ROOT/'src/TheEye/Assets/TheEye.ico',sizes=[(16,16),(32,32),(48,48),(64,64),(128,128),(256,256)])
print('Wrote three 1920x1080 composites; reference preserved at native 404x495.')
