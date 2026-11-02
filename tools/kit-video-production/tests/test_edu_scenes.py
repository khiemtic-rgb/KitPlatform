"""Smoke test: every edu_* template renders in tall and wide without a QA violation.

Run from tools/kit-video-production:  python -m unittest tests.test_edu_scenes
"""

import unittest
from pathlib import Path

from kit_video.canvas import FrameContext
from kit_video.templates import TEMPLATES

FONTS = [Path("/usr/share/fonts/opentype/inter/Inter-Regular.otf"), Path("C:/Windows/Fonts/segoeui.ttf")]
BOLD = [Path("/usr/share/fonts/opentype/inter/Inter-Bold.otf"), Path("C:/Windows/Fonts/segoeuib.ttf")]
PALETTE = dict(background=(14, 22, 38), paper=(24, 36, 58), ink=(240, 244, 250), muted=(160, 174, 196),
               accent=(94, 168, 255), good=(72, 200, 140), warn=(245, 190, 70), danger=(240, 100, 100))
SCENES = {
    "edu_title": dict(icon="shield", tag="A2", title="Vốn sống và vốn giao dịch", subtitle="Tách hai khoản tiền"),
    "edu_contrast": dict(title="Hai tâm lý", left=dict(label="Tiền sinh hoạt", tone="bad", big="-20%", text="Không dám cắt lỗ.", icon="flame"),
                         right=dict(label="Tiền có thể mất", big="-20%", text="Cắt lỗ đúng kế hoạch.", icon="shield")),
    "edu_steps": dict(title="Ba ngăn", steps=[dict(head="Sinh hoạt", text="Không đụng tới", icon="wallet"), dict(head="Dự phòng", icon="lock"), dict(head="Giao dịch", icon="candles")]),
    "edu_bignumber": dict(number="457 nghìn tỷ", label="Dư nợ margin", source="UBCKNN, 30/09/2026", chain=["Giá giảm", "Bị bán giải chấp"], icon="warn"),
    "edu_rules": dict(title="Ba quy tắc", rules=["Chỉ nạp tiền có thể mất", "Không vay", "Đặt mức lỗ trước"], icons=["coins", "cross", "gauge"]),
    "edu_flow": dict(title="Hai bể", tanks=[dict(label="Vốn sống", level=.8, note="Sinh hoạt"), dict(label="Vốn giao dịch", level=.3, note="Phần có thể mất")], valve="Không rút từ vốn sống"),
    "edu_chart": dict(title="Mức lỗ đặt trước", trend=-0.6, levels=[dict(at=.3, label="Mức lỗ đã định trước", tone="danger")]),
}


class EduScenes(unittest.TestCase):
    def test_render_without_violations(self):
        reg = next((p for p in FONTS if p.is_file()), None)
        bold = next((p for p in BOLD if p.is_file()), None)
        if reg is None or bold is None:
            self.skipTest("no font")
        style = dict(profileId="t", palette=PALETTE, typography=dict(fontRegular=str(reg), fontBold=str(bold)),
                     safeArea=dict(left=.07, right=.07, top=.07, bottom=.10))
        for name, params in SCENES.items():
            for w, h in ((1080, 1920), (1920, 1080)):
                ctx = FrameContext(dict(sceneId=name, params=params), style, w, h, 6.0, 8.0, {})
                TEMPLATES[name](ctx, {}, Path("."), Path("."))
                self.assertEqual(ctx.violations, [], f"{name} {w}x{h}")


if __name__ == "__main__":
    unittest.main()
