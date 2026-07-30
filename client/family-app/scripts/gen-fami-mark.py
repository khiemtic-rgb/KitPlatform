"""Generate the small transparent Fami mark used in the Morning Brief eyebrow.

The source logo sits on a solid white plate, so the background is stripped by
flooding from the border: enclosed near-white pixels (eye highlights, teeth)
stay opaque because they are never reached from the edge.
"""
from __future__ import annotations

from collections import deque
from pathlib import Path

from PIL import Image

SRC = Path(
    r"C:\Users\Admin\.cursor\projects\e-KitPlatform\assets"
    r"\c__Users_Admin_AppData_Roaming_Cursor_User_workspaceStorage_empty-window_images"
    r"_logo-new-f76361a9-e417-4956-b49e-908a9b698613.png"
)
BRAND = Path(__file__).resolve().parents[1] / "public" / "brand"
WHITE_CUTOFF = 238
# The wordmark starts below this fraction of the artwork; keep the emblem only.
EMBLEM_RATIO = 0.58


def strip_white_background(img: Image.Image) -> Image.Image:
    w, h = img.size
    px = img.load()
    seen = bytearray(w * h)
    queue: deque[tuple[int, int]] = deque()

    def consider(x: int, y: int) -> None:
        idx = y * w + x
        if seen[idx]:
            return
        r, g, b, a = px[x, y]
        if a != 0 and not (r >= WHITE_CUTOFF and g >= WHITE_CUTOFF and b >= WHITE_CUTOFF):
            return
        seen[idx] = 1
        queue.append((x, y))

    for x in range(w):
        consider(x, 0)
        consider(x, h - 1)
    for y in range(h):
        consider(0, y)
        consider(w - 1, y)

    while queue:
        x, y = queue.popleft()
        px[x, y] = (255, 255, 255, 0)
        if x > 0:
            consider(x - 1, y)
        if x < w - 1:
            consider(x + 1, y)
        if y > 0:
            consider(x, y - 1)
        if y < h - 1:
            consider(x, y + 1)

    return img


def main() -> None:
    BRAND.mkdir(parents=True, exist_ok=True)
    img = strip_white_background(Image.open(SRC).convert("RGBA"))

    box = img.getbbox()
    if box is None:
        raise SystemExit("logo is fully transparent")
    minx, miny, maxx, maxy = box
    emblem = img.crop((minx, miny, maxx, miny + int((maxy - miny) * EMBLEM_RATIO)))
    emblem = emblem.crop(emblem.getbbox())
    ew, eh = emblem.size
    print(f"emblem={ew}x{eh}")

    side = max(ew, eh)
    pad = max(1, int(side * 0.04))
    square = Image.new("RGBA", (side + 2 * pad, side + 2 * pad), (255, 255, 255, 0))
    square.paste(emblem, (pad + (side - ew) // 2, pad + (side - eh) // 2), emblem)

    for size in (48, 64, 96):
        out = square.copy()
        out.thumbnail((size, size), Image.Resampling.LANCZOS)
        canvas = Image.new("RGBA", (size, size), (255, 255, 255, 0))
        canvas.paste(out, ((size - out.size[0]) // 2, (size - out.size[1]) // 2), out)
        path = BRAND / f"fami-mark-{size}.png"
        canvas.save(path, format="PNG", optimize=True)
        print(f"wrote {path.name} bytes={path.stat().st_size}")


if __name__ == "__main__":
    main()
