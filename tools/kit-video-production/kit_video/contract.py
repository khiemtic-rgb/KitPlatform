"""Design contract the renderer actually measures with.

Colors, type, and safe area come from the project style. This module does not
name a brand or a film. A second project with another palette gets another contract.
"""

from __future__ import annotations

from dataclasses import dataclass


ROLES = {
    "display": 0.062,
    "title": 0.048,
    "subtitle": 0.034,
    "body": 0.030,
    "caption": 0.024,
    "micro": 0.020,
}
SPACE = {"xs": 0.012, "sm": 0.02, "md": 0.04, "lg": 0.06}
SUPPORTED_BOARDS = (
    "hero_still",
    "portrait_split",
    "brand_center",
    "brand_split",
    "end_lockup",
    "app_screen",
    "manifesto",
    "workspace",
    "lanes",
    "catalog",
    "dashboard",
    "triptych",
)


def _decision(status: str, code: str, kind: str, kept: int | None, omitted: list[str], reason: str) -> dict:
    return {"status": status, "code": code, "kind": kind, "kept": kept, "omitted": omitted, "reason": reason}


def _primary_count(kind: str, params: dict) -> int | None:
    if kind == "workspace":
        table = params.get("table")
        if not isinstance(table, dict):
            return 0
        return len([row for row in (table.get("rows") or []) if isinstance(row, list)])
    if kind == "lanes":
        return len([item for item in (params.get("lanes") or []) if isinstance(item, dict)])
    if kind == "catalog":
        return len([item for item in (params.get("tiles") or []) if isinstance(item, dict)])
    return None


def _secondary_overflow(kind: str, params: dict, room: int) -> list[str]:
    if kind != "workspace":
        return []
    activity = [item for item in (params.get("activity") or []) if isinstance(item, str) and item]
    if len(activity) <= room:
        return []
    return activity[room:]


def _channel(value: float) -> float:
    value = value / 255
    if value <= 0.03928:
        return value / 12.92
    return ((value + 0.055) / 1.055) ** 2.4


def luminance(color: tuple[int, int, int]) -> float:
    red, green, blue = (_channel(color[0]), _channel(color[1]), _channel(color[2]))
    return 0.2126 * red + 0.7152 * green + 0.0722 * blue


def contrast_ratio(foreground: tuple[int, int, int], background: tuple[int, int, int]) -> float:
    light = luminance(foreground)
    dark = luminance(background)
    lighter = max(light, dark)
    darker = min(light, dark)
    return (lighter + 0.05) / (darker + 0.05)


@dataclass(frozen=True)
class DesignContract:
    profile_id: str
    width: int
    height: int
    safe: tuple[float, float, float, float]
    palette: dict

    @classmethod
    def from_frame(cls, style: dict, width: int, height: int) -> "DesignContract":
        area = style["safeArea"]
        return cls(
            str(style.get("profileId") or ""),
            int(width),
            int(height),
            (float(area["top"]), float(area["right"]), float(area["bottom"]), float(area["left"])),
            dict(style["palette"]),
        )

    @property
    def min_px(self) -> int:
        return max(8, int(self.height * ROLES["micro"]))

    def px(self, role: str | float) -> int:
        ratio = ROLES[role] if isinstance(role, str) else float(role)
        return max(self.min_px, int(self.height * ratio))

    def space(self, name: str) -> int:
        return max(4, int(self.height * SPACE[name]))

    def supports(self, kind: str) -> bool:
        return kind in SUPPORTED_BOARDS

    def contrast(self, foreground: str, background: str) -> float:
        return contrast_ratio(self._color(foreground), self._color(background))

    def readable_pair(self, foreground: str, background: str, minimum: float = 4.5) -> bool:
        return self.contrast(foreground, background) >= minimum

    def place_block(self, top: int, bottom: int, block: int) -> int:
        """Center a short block. A long block stays at the top instead of being shrunk."""
        span = max(0, bottom - top)
        if block <= 0 or block >= span * 0.55:
            return top
        return top + (span - block) // 2

    def content_span(self) -> int:
        top = int(self.height * self.safe[0])
        bottom = int(self.height * (1 - self.safe[2]))
        return max(0, bottom - top)

    def primary_capacity(self, kind: str) -> int:
        """How many primary rows fit without going below the body role."""
        span = self.content_span()
        if kind in {"workspace", "lanes", "catalog", "dashboard"}:
            span -= int(self.height * 0.08)
        span -= self.px("title") + self.space("sm")
        minimum = self.px("body") + self.space("xs")
        if span <= 0 or minimum <= 0:
            return 0
        return max(1, span // minimum)

    def fit_board(self, kind: str, params: dict) -> dict:
        """Accept a board, choose one, or refuse. Refusal does not shrink the type."""
        requested = kind.strip()
        if not requested:
            requested = self.choose_board(params)
            if not requested:
                return _decision("blocked", "layout_choice", "", 0, [], "params không đủ để chọn một bố cục đã đăng ký.")
        if not self.supports(requested):
            return _decision("blocked", "board_kind", requested, 0, [], "kind chưa đăng ký. Không có bố cục riêng cho một phim.")
        count = _primary_count(requested, params)
        if count is None:
            return _decision("ok", "", requested, None, [], "Bố cục này không có danh sách chính.")
        capacity = self.primary_capacity(requested)
        if count > capacity:
            return _decision(
                "blocked",
                "layout_density",
                requested,
                capacity,
                [],
                f"{requested} chứa được {capacity} mục ở cỡ chữ tối thiểu. Params có {count}. Không thu nhỏ chữ.",
            )
        omitted = _secondary_overflow(requested, params, capacity - count)
        reason = "Nội dung chính vừa khung ở cỡ chữ tối thiểu."
        if omitted:
            reason = "Giữ nội dung chính. Bỏ chi tiết phụ không còn chỗ."
        return _decision("ok", "", requested, count, omitted, reason)

    def choose_board(self, params: dict) -> str:
        if isinstance(params.get("table"), dict):
            return "workspace"
        if isinstance(params.get("lanes"), list):
            return "lanes"
        if isinstance(params.get("tiles"), list):
            return "catalog"
        if isinstance(params.get("columns"), list):
            return "triptych"
        if isinstance(params.get("metrics"), list) or isinstance(params.get("chart"), dict):
            return "dashboard"
        if isinstance(params.get("lines"), list) and params.get("assetId"):
            return "portrait_split"
        if params.get("logoAssetId") and params.get("url"):
            return "end_lockup"
        if params.get("logoAssetId"):
            return "brand_center"
        if params.get("assetId"):
            return "hero_still"
        return ""

    def row_height(self, count: int, available: int, role: str = "body", cap: float = 0.08) -> tuple[int, int]:
        """Fit rows without going below the type role. Extra rows are omitted, not squeezed."""
        if count <= 0 or available <= 0:
            return self.px(role), 0
        minimum = self.px(role) + self.space("xs")
        maximum = max(minimum, int(self.height * cap))
        if count * minimum <= available:
            natural = available // count
            return min(maximum, max(minimum, natural)), count
        kept = max(1, available // minimum)
        return minimum, min(count, kept)

    def summary(self) -> dict:
        return {
            "profileId": self.profile_id,
            "frame": [self.width, self.height],
            "minTypePx": self.min_px,
            "roles": {name: self.px(name) for name in ROLES},
            "space": {name: self.space(name) for name in SPACE},
            "safe": {"top": self.safe[0], "right": self.safe[1], "bottom": self.safe[2], "left": self.safe[3]},
            "boards": list(SUPPORTED_BOARDS),
        }

    def _color(self, name: str) -> tuple[int, int, int]:
        value = self.palette[name]
        return int(value[0]), int(value[1]), int(value[2])
