# Artwork provenance and resolution

The supplied character reference is preserved byte-for-byte in
`originals/triangle-reference.png` (404 x 495 pixels). It depicts the user's
chosen existing character; it is not original project artwork and is not
licensed under the code's MIT license.

The built-in image generation service rejected the requested new illustration
set. No successful high-resolution regenerated character is claimed here.

`tools/compose_triangle.py` extracts the provided pose and composites it at
native pixel size. It produces three 1920 x 1080 files in
`src/TheEye/Pets/Triangle/Assets`: main.png, resting.png, float.png.
The first two have opaque ivory gradient backgrounds. float.png has real alpha,
including a soft golden aura. The original image is never downsampled.
1920 x 1080 describes the canvas; the character still contains only the source
image's detail. The rest artwork uses the original pose, not a generated
meditation pose.

The sprite's transparent padding is cropped by the manifest's sourceRect at
load time, preserving every retained source pixel. UI size and Windows DPI
scaling affect display only, not source asset resolution.

The mountain background was successfully generated using the built-in tool
and is saved unchanged at native 1672 x 941 resolution in
`src/TheEye/Pets/Triangle/Assets/mountains.png`. The main view layers the
unchanged approved sprite over it; float.png was not edited.

Final landscape prompt: layered grayscale mountain peaks and tall pine forest
silhouettes in pale atmospheric mist; painterly illustration, charcoal/silver
and ivory fog; light left side for dark UI text, detailed forest on the right;
no characters, symbols, text or UI; landscape, highest native quality.

The requested meditation edit was attempted using the built-in tool with the
original reference, preserving its 2D style, triangle body, eye, hat, bow tie,
limbs and glow, changing only to a closed-eye cross-legged levitation pose on
an ivory background. The service returned output moderation_blocked / other,
request b7d22d57-8943-4707-8601-08304194dd9b. No specific cause was provided.
The existing rest image remains in use; no generated meditation image is claimed.

Attempted built-in prompt set: use the attached yellow triangle identity,
preserve its eye, brick pattern, hat, bow tie, black limbs and golden glow;
make a misty forest main illustration, an ivory resting illustration, and one
transparent floating sprite at the highest native resolution available.
