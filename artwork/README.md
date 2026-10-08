# Artwork provenance and resolution

## Active high-quality artwork

The user's two new attachments are preserved unchanged:

- `originals/triangle-standing-hq.png`: 912 x 1120, standing pose on white.
- `originals/triangle-meditation-hq.png`: 912 x 1120, meditation on pink/ivory clouds.

These depict the user's chosen existing character; ownership is unchanged and
the images are not covered by the application's MIT code license.

`tools/compose_triangle.py` performs the explicitly requested deterministic
segmentation/compositing. It does not ask a generator to redraw the character.
Run with Python, Pillow, NumPy, OpenCV and SciPy (the local `.asset-tools`
environment includes them). Outputs in `src/TheEye/Pets/Triangle/Assets`:

- `float.png`: 912 x 1120 RGBA. Native pixels, no resize or crop. The white eye
  remains opaque; background becomes transparent. White-matte subtraction on
  translucent edges retains the golden aura without a baked white fringe.
- `resting.png`: opaque 2240 x 1260 landscape. The complete meditation source
  is inserted at (1250,70), unchanged and at 1:1 pixel scale. Only the surrounding
  background is extended. The script asserts exact equality of the whole insert.
- `meditation-original.png`: byte-for-byte copy of the meditation attachment.
- `main.png`: 2240 x 1260 composed mountain scene with the native standing
  cutout. The mountain background alone is interpolated to fit this canvas;
  no extra source detail is claimed. The actual main UI instead layers the
  native mountain background and full-resolution sprite independently.

The main UI displays the character about 1.8 times larger than before. Taskbar
size and animation timing are unchanged; its aspect ratio now follows the source.
Only Windows icon derivatives are reduced to required icon sizes. Display/DPI
scaling never alters the saved artwork. Dark/white alpha-review composites are
written to `artifacts/artwork-review`.

## Gaming lock screen

`originals/triangle-gaming-landscape.png` is the user's clean 1280 x 720
landscape attachment. `src/TheEye/Pets/Triangle/Assets/gaming-rest.png` is a
byte-for-byte copy, already in the same 16:9 ratio as the meditation screen.
The PNG has fully opaque alpha. There is no saved resize, extension or redraw;
only normal WPF display scaling. Tests verify the dimensions, opacity and
exact file equality. The earlier 912 x 1120 portrait remains in
`originals/triangle-gaming-hq.png` for provenance; the previous local background
extension and its composition script have been superseded by the clean image.

## App icon

The app/tray icon uses the separate user-supplied `originals/eye-icon-reference.png`
(512 x 512 RGBA), unchanged. `tools/build_icon.py` packages it at the Windows
icon sizes from 16 through 256 pixels, preserving alpha. Artwork regeneration
also calls that script so it cannot restore the older full-character icon.

## Mountain background

`mountains.png` is the previously generated, unchanged native 1672 x 941
mountain/pine-forest illustration. It was made with the built-in image tool.
Prompt: layered grayscale mountain peaks and tall pine forest silhouettes in
pale atmospheric mist; painterly illustration, charcoal/silver and ivory fog;
light left side for dark UI text, detailed forest on the right; no characters,
symbols, text or UI; landscape, highest native quality.

## Historical sources

The earlier 404 x 495 reference and its cutout remain under `originals` only as
historical sources. They are no longer used by any active UI/animation frame.
Earlier character-generation requests were rejected by the image service;
the new supplied attachments replace that unsuccessful generation workflow.
