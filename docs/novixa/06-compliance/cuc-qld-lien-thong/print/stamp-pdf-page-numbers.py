"""Stamp page numbers into bottom margin of a PDF (no full-bleed chrome lines)."""
from __future__ import annotations

import sys
from pathlib import Path

import pymupdf


def main() -> int:
    if len(sys.argv) < 2:
        print("Usage: stamp-pdf-page-numbers.py <file.pdf>", file=sys.stderr)
        return 2
    pdf_path = Path(sys.argv[1])
    doc = pymupdf.open(pdf_path)
    total = doc.page_count
    for i, page in enumerate(doc, start=1):
        rect = page.rect
        label = f"NVX-CQD-UAT-01  ·  Trang {i}/{total}"
        # Bottom margin band, inset from left/right
        page.insert_text(
            pymupdf.Point(rect.x0 + 70, rect.y1 - 28),
            label,
            fontname="times-roman",
            fontsize=9,
            color=(0.25, 0.25, 0.25),
        )
    out_tmp = pdf_path.with_suffix(".stamped.pdf")
    doc.save(out_tmp)
    doc.close()
    out_tmp.replace(pdf_path)
    print(f"Stamped {total} page number(s)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
