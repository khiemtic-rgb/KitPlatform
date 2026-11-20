"""Measure one A-pose candidate. Prints a table. Writes crops next to the image."""

from __future__ import annotations

import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont

PINK = np.array([0xF8, 0x06, 0xB4], dtype=np.int16)
ROOT = Path(__file__).resolve().parent
STAND = Path(r"E:\KitPlatform\src\Packs\Content\KitPlatform.Packs.Content.Application\VisualIntelligence\Masters\lan-stand.jpg")
CROPS = ROOT / "crops"


def load_rgb(path: Path) -> np.ndarray:
    return np.asarray(Image.open(path).convert("RGB"))


def fit(image: np.ndarray) -> np.ndarray:
    height, width = image.shape[:2]
    target = (1728, 2304) if min(width, height) >= 1600 else (864, 1152)
    if (width, height) == target:
        return image
    return np.asarray(Image.fromarray(image).resize(target, Image.Resampling.LANCZOS))


def mask_of(image: np.ndarray) -> np.ndarray:
    delta = np.abs(image.astype(np.int16) - PINK)
    return np.any(delta > 8, axis=2)


def background_stats(image: np.ndarray, character: np.ndarray) -> dict:
    background = image[~character].astype(np.float32)
    if background.size == 0:
        return {"percent": 0.0, "std": 999.0}
    spread = background.std(axis=0)
    return {
        "percent": round(100.0 * (~character).mean(), 2),
        "std": round(float(spread.max()), 2),
        "stdChannels": [round(float(v), 2) for v in spread],
    }


def margin(character: np.ndarray, minimum: int) -> dict:
    height, width = character.shape
    touch = {
        "top": int(character[:minimum].any()),
        "bottom": int(character[-minimum:].any()),
        "left": int(character[:, :minimum].any()),
        "right": int(character[:, -minimum:].any()),
    }
    rows = np.where(character.any(axis=1))[0]
    cols = np.where(character.any(axis=0))[0]
    if len(rows) == 0:
        return {"pass": False, "touch": touch, "px": [0, 0, 0, 0]}
    edges = [int(cols[0]), int(rows[0]), int(width - 1 - cols[-1]), int(height - 1 - rows[-1])]
    return {"pass": min(edges) >= minimum and not any(touch.values()), "touch": touch, "px": edges}


def gap_at(character: np.ndarray, y: int, minimum: int) -> dict:
    row = character[y]
    runs = []
    start = None
    for x, on in enumerate(row.tolist()):
        if on and start is None:
            start = x
        elif not on and start is not None:
            runs.append((start, x - 1))
            start = None
    if start is not None:
        runs.append((start, len(row) - 1))
    if len(runs) < 2:
        return {"gapLeft": 0, "gapRight": 0, "pass": False, "runs": len(runs)}
    center = len(row) / 2
    body = min(runs, key=lambda run: abs((run[0] + run[1]) / 2 - center))
    left = [run for run in runs if run[1] < body[0]]
    right = [run for run in runs if run[0] > body[1]]
    gap_left = body[0] - left[-1][1] - 1 if left else 0
    gap_right = right[0][0] - body[1] - 1 if right else 0
    return {
        "gapLeft": int(gap_left),
        "gapRight": int(gap_right),
        "pass": gap_left >= minimum and gap_right >= minimum,
        "runs": len(runs),
    }


def centroid(character: np.ndarray) -> dict:
    ys, xs = np.where(character)
    width = character.shape[1]
    if len(xs) == 0:
        return {"offset": 1.0, "pass": False}
    offset = abs(float(xs.mean()) - width / 2) / width
    return {"offset": round(offset, 4), "pass": offset <= 0.03}


def head_box(character: np.ndarray) -> tuple[int, int, int, int]:
    ys, xs = np.where(character)
    top, bottom = int(ys.min()), int(ys.max())
    left, right = int(xs.min()), int(xs.max())
    height = max(1, bottom - top)
    width = max(1, right - left)
    cx = (left + right) // 2
    half = max(8, int(width * 0.28))
    return cx - half, top, cx + half, top + int(height * 0.22)


def ssim(a: np.ndarray, b: np.ndarray) -> float:
    a = a.astype(np.float64)
    b = b.astype(np.float64)
    kernel = 7
    pad = kernel // 2
    c1 = (0.01 * 255) ** 2
    c2 = (0.03 * 255) ** 2

    def blur(image: np.ndarray) -> np.ndarray:
        padded = np.pad(image, pad, mode="reflect")
        total = np.zeros_like(image)
        for dy in range(kernel):
            for dx in range(kernel):
                total += padded[dy : dy + image.shape[0], dx : dx + image.shape[1]]
        return total / (kernel * kernel)

    mu_a, mu_b = blur(a), blur(b)
    mu_a2, mu_b2, mu_ab = mu_a * mu_a, mu_b * mu_b, mu_a * mu_b
    sigma_a = blur(a * a) - mu_a2
    sigma_b = blur(b * b) - mu_b2
    sigma_ab = blur(a * b) - mu_ab
    score = ((2 * mu_ab + c1) * (2 * sigma_ab + c2)) / ((mu_a2 + mu_b2 + c1) * (sigma_a + sigma_b + c2))
    return float(score.mean())


def face_score(original: np.ndarray, candidate: np.ndarray) -> dict:
    box_a = head_box(mask_of(original))
    box_b = head_box(mask_of(candidate))
    crop_a = original[box_a[1] : box_a[3], box_a[0] : box_a[2]]
    crop_b = candidate[box_b[1] : box_b[3], box_b[0] : box_b[2]]
    gray_a = np.asarray(Image.fromarray(crop_a).convert("L").resize((160, 180), Image.Resampling.LANCZOS))
    gray_b = np.asarray(Image.fromarray(crop_b).convert("L").resize((160, 180), Image.Resampling.LANCZOS))
    score = ssim(gray_a, gray_b)
    label = "ĐẠT" if score >= 0.75 else "CẦN NGƯỜI XEM" if score >= 0.6 else "TRƯỢT"
    return {"ssim": round(score, 4), "label": label, "pass": score >= 0.75, "box": box_b}


def median_region(image: np.ndarray, character: np.ndarray, y0: float, y1: float, kind: str) -> np.ndarray | None:
    height, width = character.shape
    ys, xs = np.where(character)
    if len(ys) == 0:
        return None
    top, bottom = int(ys.min()), int(ys.max())
    left, right = int(xs.min()), int(xs.max())
    span = max(1, bottom - top)
    body = max(1, right - left)
    y_a = top + int(span * y0)
    y_b = top + int(span * y1)
    x_a = left + int(body * 0.35)
    x_b = left + int(body * 0.65)
    patch = image[y_a:y_b, x_a:x_b]
    local = character[y_a:y_b, x_a:x_b]
    pixels = patch[local]
    if kind == "shirt":
        keep = (pixels[:, 2] > pixels[:, 0] + 10) & (pixels[:, 2] > pixels[:, 1])
    else:
        keep = (pixels[:, 1] > pixels[:, 2] + 8) & (pixels[:, 1] + 15 > pixels[:, 0])
    chosen = pixels[keep]
    if len(chosen) < 40:
        return None
    return np.median(chosen, axis=0)


def color_score(original: np.ndarray, candidate: np.ndarray) -> dict:
    original_mask = mask_of(original)
    candidate_mask = mask_of(candidate)
    result = {}
    passed = True
    measured = True
    for name, band, kind in (("shirt", (0.22, 0.46), "shirt"), ("pants", (0.52, 0.78), "pants")):
        a = median_region(original, original_mask, band[0], band[1], kind)
        b = median_region(candidate, candidate_mask, band[0], band[1], kind)
        if a is None or b is None:
            result[name] = None
            measured = False
            passed = False
            continue
        delta = np.abs(a - b)
        result[name] = {
            "original": [int(v) for v in a],
            "candidate": [int(v) for v in b],
            "delta": [int(v) for v in delta],
            "pass": bool(delta.max() <= 18),
        }
        passed = passed and bool(delta.max() <= 18)
    result["pass"] = passed and measured
    result["measured"] = measured
    return result


def save_zoom(image: np.ndarray, box: tuple[int, int, int, int], path: Path) -> None:
    x0, y0, x1, y1 = box
    height, width = image.shape[:2]
    x0, y0 = max(0, x0), max(0, y0)
    x1, y1 = min(width, x1), min(height, y1)
    if x1 <= x0 or y1 <= y0:
        return
    crop = Image.fromarray(image[y0:y1, x0:x1])
    crop.resize((crop.width * 3, crop.height * 3), Image.Resampling.NEAREST).save(path)


def write_crops(image: np.ndarray, character: np.ndarray, tag: str, face: tuple[int, int, int, int]) -> None:
    CROPS.mkdir(parents=True, exist_ok=True)
    height, width = character.shape
    scale = height / 1152
    half = int(36 * scale)
    for name, frac in (("armpit", 0.24), ("hip", 0.42)):
        y = int(height * frac)
        row = character[y]
        runs = []
        start = None
        for x, on in enumerate(row.tolist()):
            if on and start is None:
                start = x
            elif not on and start is not None:
                runs.append((start, x - 1))
                start = None
        if start is not None:
            runs.append((start, len(row) - 1))
        center = width / 2
        body = min(runs, key=lambda run: abs((run[0] + run[1]) / 2 - center)) if runs else (width // 2, width // 2)
        left = [run for run in runs if run[1] < body[0]]
        right = [run for run in runs if run[0] > body[1]]
        if left:
            seam = (left[-1][1] + body[0]) // 2
            save_zoom(image, (seam - half, y - half, seam + half, y + half), CROPS / f"{tag}-{name}-left.png")
        if right:
            seam = (body[1] + right[0][0]) // 2
            save_zoom(image, (seam - half, y - half, seam + half, y + half), CROPS / f"{tag}-{name}-right.png")
    save_zoom(image, face, CROPS / f"{tag}-face.png")
    # Hand crops: outermost character pixels in the lower half of each side.
    ys, xs = np.where(character)
    if len(xs) == 0:
        return
    left, right = int(xs.min()), int(xs.max())
    mid_y = int(np.median(ys))
    for side, x_limit in (("left", left + (right - left) // 3), ("right", right - (right - left) // 3)):
        if side == "left":
            pick = np.where(character[:, :x_limit])
        else:
            pick = np.where(character[:, x_limit:])
            pick = (pick[0], pick[1] + x_limit)
        if len(pick[0]) == 0:
            continue
        # The hand of an A-pose is the outer, lower end of the arm, not the foot.
        arm = [(int(y), int(x)) for y, x in zip(pick[0], pick[1]) if y < mid_y + int(height * 0.08)]
        if not arm:
            continue
        if side == "left":
            point = min(arm, key=lambda p: p[1])
        else:
            point = max(arm, key=lambda p: p[1])
        save_zoom(image, (point[1] - half, point[0] - half, point[1] + half, point[0] + half), CROPS / f"{tag}-hand-{side}.png")


def measure(path: Path) -> dict:
    raw = load_rgb(path)
    image = fit(raw)
    character = mask_of(image)
    height = image.shape[0]
    scale = height / 1152
    gap_min = max(10, int(round(10 * scale)))
    edge_min = max(20, int(round(20 * scale)))
    stand = fit(load_rgb(STAND))
    background = background_stats(image, character)
    edges = margin(character, edge_min)
    gaps = {name: gap_at(character, int(height * frac), gap_min) for name, frac in (("shoulder", 0.24), ("chest", 0.32), ("hip", 0.42))}
    center = centroid(character)
    face = face_score(stand, image)
    color = color_score(stand, image)
    checks = {
        "background": bool(background["percent"] >= 70 and background["std"] < 6),
        "margin": bool(edges["pass"]),
        "gaps": bool(all(item["pass"] for item in gaps.values())),
        "symmetry": bool(center["pass"]),
        "face": bool(face["pass"]),
        "color": bool(color["pass"]),
    }
    passed = sum(1 for item in checks.values() if item)
    write_crops(image, character, path.stem, tuple(face["box"]))
    return {
        "file": path.name,
        "native": list(raw.shape[:2][::-1]) if False else [raw.shape[1], raw.shape[0]],
        "measured": [image.shape[1], image.shape[0]],
        "background": background,
        "margin": edges,
        "gaps": gaps,
        "symmetry": center,
        "face": {k: face[k] for k in ("ssim", "label", "pass")},
        "color": color,
        "hands": "CHƯA KIỂM",
        "checks": checks,
        "passed": passed,
        "of": 6,
    }


def contact(paths: list[Path], dest: Path) -> None:
    stand = Image.open(STAND).convert("RGB")
    tiles = [stand.resize((320, 427), Image.Resampling.LANCZOS)]
    labels = ["lan-stand.jpg"]
    for path in paths:
        tiles.append(Image.open(path).convert("RGB").resize((320, 427), Image.Resampling.LANCZOS))
        labels.append(path.name)
    sheet = Image.new("RGB", (len(tiles) * 320, 427 + 28), (246, 246, 244))
    draw = ImageDraw.Draw(sheet)
    font = ImageFont.truetype("C:/Windows/Fonts/segoeui.ttf", 16)
    for index, tile in enumerate(tiles):
        draw.text((index * 320 + 8, 4), labels[index], fill=(16, 28, 48), font=font)
        sheet.paste(tile, (index * 320, 28))
    sheet.save(dest)


def main() -> None:
    target = Path(sys.argv[1])
    result = measure(target)
    print(json.dumps(result, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
