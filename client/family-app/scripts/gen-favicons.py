"""Generate Famixa favicons from the official logo PNG."""
from __future__ import annotations

from pathlib import Path

from PIL import Image

SRC = Path(
    r"C:\Users\Admin\.cursor\projects\e-KitPlatform\assets"
    r"\c__Users_Admin_AppData_Roaming_Cursor_User_workspaceStorage_empty-window_images"
    r"_logo-new-f76361a9-e417-4956-b49e-908a9b698613.png"
)
OUT = Path(__file__).resolve().parents[1] / "public"
BRAND = OUT / "brand"


def is_bg(px, x: int, y: int) -> bool:
    r, g, b, a = px[x, y]
    if a < 8:
        return True
    return r > 245 and g > 245 and b > 245


def content_bbox(img: Image.Image) -> tuple[int, int, int, int]:
    px = img.load()
    w, h = img.size
    minx, miny, maxx, maxy = w, h, 0, 0
    for y in range(h):
        for x in range(w):
            if not is_bg(px, x, y):
                minx = min(minx, x)
                miny = min(miny, y)
                maxx = max(maxx, x)
                maxy = max(maxy, y)
    return minx, miny, maxx, maxy


def save_size(square: Image.Image, size: int, path: Path) -> None:
    canvas = Image.new("RGBA", (size, size), (255, 255, 255, 255))
    thumb = square.copy()
    thumb.thumbnail((size, size), Image.Resampling.LANCZOS)
    x = (size - thumb.size[0]) // 2
    y = (size - thumb.size[1]) // 2
    canvas.paste(thumb, (x, y), thumb)
    rgb = Image.new("RGB", (size, size), (255, 255, 255))
    rgb.paste(canvas, mask=canvas.split()[-1])
    rgb.save(path, format="PNG", optimize=True)
    print(f"wrote {path.name} {size}x{size} bytes={path.stat().st_size}")


def main() -> None:
    BRAND.mkdir(exist_ok=True)
    img = Image.open(SRC).convert("RGBA")
    w, h = img.size
    print(f"source={w}x{h}")
    img.save(BRAND / "famixa-logo.png", optimize=True)

    minx, miny, maxx, maxy = content_bbox(img)
    print(f"content_bbox=({minx},{miny})-({maxx},{maxy})")
    content_h = maxy - miny + 1
    emblem_bottom = miny + int(content_h * 0.58)
    emblem = img.crop((minx, miny, maxx + 1, emblem_bottom))
    ew, eh = emblem.size
    print(f"emblem_raw={ew}x{eh}")

    side = max(ew, eh)
    square = Image.new("RGBA", (side, side), (255, 255, 255, 255))
    square.paste(emblem, ((side - ew) // 2, (side - eh) // 2), emblem)
    pad = max(1, int(side * 0.06))
    padded = Image.new("RGBA", (side + 2 * pad, side + 2 * pad), (255, 255, 255, 255))
    padded.paste(square, (pad, pad), square)
    square = padded
    print(f"square={square.size[0]}x{square.size[1]}")

    save_size(square, 32, OUT / "favicon-32.png")
    save_size(square, 48, OUT / "favicon-48.png")
    save_size(square, 180, OUT / "apple-touch-icon.png")
    save_size(square, 192, OUT / "icon-192.png")
    save_size(square, 512, OUT / "icon-512.png")

    mark = Image.new("RGB", square.size, (255, 255, 255))
    mark.paste(square, mask=square.split()[-1])
    mark.save(BRAND / "famixa-mark.png", optimize=True)

    # Prefer PNG favicon in browsers; keep favicon.svg as redirect-style note via small PNG link only.
    print("done")


if __name__ == "__main__":
    main()
