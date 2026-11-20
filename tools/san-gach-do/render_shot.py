"""Render một hoặc nhiều cảnh từ file dữ liệu.

  blender --background --python tools/san-gach-do/render_shot.py -- canh/tap03_c08_bo.json [canh/khac.json ...]
  thêm  --chi-anh        chỉ render khung tĩnh (thử nhanh)
        --khung 1,36     các khung tĩnh cần xem
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from sgd_core import shot  # noqa: E402

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
still_only = "--chi-anh" in argv
stills = None
files = []
i = 0
while i < len(argv):
    a = argv[i]
    if a == "--khung":
        stills = [int(x) for x in argv[i + 1].split(",")]
        i += 2
        continue
    if not a.startswith("--"):
        files.append(a)
    i += 1
if not files:
    raise SystemExit("Thiếu file cảnh. Ví dụ: ... render_shot.py -- canh/tap03_c08_bo.json")
for f in files:
    shot.run_file(f, stills=stills, still_only=still_only)
