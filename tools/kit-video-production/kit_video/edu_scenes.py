"""Knowledge-education scenes: typography, numbers, diagrams. No photos, no faces.

Every scene reads content from params and colors from the project palette, so one
template serves any brand and both 9:16 and 16:9. Text is wrapped to the box and
the box is sized from the text, so nothing is clipped or shrunk below the role size.
"""

from __future__ import annotations

from pathlib import Path

from PIL import Image, ImageDraw

from kit_video.canvas import FrameContext, ease_out, lerp, paint_background, progress, rounded


def _c(ctx: FrameContext, name: str, fallback: str) -> tuple[int, int, int]:
    return ctx.color(name) if name in ctx.palette else ctx.color(fallback)


def _mix(a, b, k):
    return tuple(int(a[i] + (b[i] - a[i]) * k) for i in range(3))


def _wrap(ctx: FrameContext, text: str, size: int, bold: bool, width: int) -> list[str]:
    font = ctx.font(size, bold)
    probe = ImageDraw.Draw(ctx.image)
    lines, line = [], ""
    for word in str(text).split():
        trial = (line + " " + word).strip()
        if probe.textlength(trial, font=font) <= width - 8 or not line:
            line = trial
        else:
            lines.append(line)
            line = word
    if line:
        lines.append(line)
    return lines


def _para(ctx, text, x, y, width, size, fill, bold=False, opacity=1.0, gap=1.3, align="l") -> int:
    """Draw wrapped text; return the y below it."""
    for line in _wrap(ctx, text, size, bold, width):
        if align == "m":
            ctx.text(line, (x + width / 2, y), size, fill, bold=bold, anchor="mt", opacity=opacity)
        else:
            ctx.text(line, (x, y), size, fill, bold=bold, opacity=opacity)
        y += int(size * gap)
    return y


def _height(ctx, text, size, bold, width, gap=1.3) -> int:
    return int(len(_wrap(ctx, text, size, bold, width)) * size * gap)


def _at(ctx, i, step=0.5, first=0.3, length=0.5) -> float:
    s = first + i * step
    return ease_out(progress(ctx.t, s, s + length))


DIM = 0.42
GHOST = 0.14


def _sa(a):
    """Slide-in uses the reveal value; a ghost slot must not slide."""
    return 1.0 if a <= GHOST + 1e-6 else a


def _fs(v) -> set:
    if v is None:
        return set()
    if isinstance(v, (list, tuple)):
        return {int(x) for x in v}
    return {int(v)}


def _focus_state(ctx):
    """(focus list, position of the spoken line in the scene), or None when the scene is not line-driven."""
    f = ctx.params.get("focus")
    pos = getattr(ctx, "edu_pos", None)
    if not isinstance(f, list) or pos is None or not (0 <= pos < len(f)):
        return None
    return f, pos


def _vis(ctx, i, a):
    """Item i appears when the first line that points at it begins. Items no line points at keep their timed reveal."""
    st = _focus_state(ctx)
    if st is None:
        return a
    f, pos = st
    firsts = [k for k, v in enumerate(f) if i in _fs(v)]
    if not firsts:
        return a
    if pos < firsts[0]:
        return GHOST  # not spoken yet: the slot is outlined so the scene shows its structure
    if pos == firsts[0]:
        return ease_out(progress(getattr(ctx, "edu_lt", 1.0), 0.0, 0.5))
    return 1.0


def _emph(ctx, i):
    """The item named by the spoken line stays bright, the others dim, with a short cross-fade when the line changes."""
    st = _focus_state(ctx)
    if st is None:
        return 1.0
    f, pos = st
    cur = _fs(f[pos])
    if not cur:
        return 1.0
    prev = _fs(f[pos - 1]) if pos > 0 else set()
    k = ease_out(progress(getattr(ctx, "edu_lt", 1.0), 0.0, 0.4))
    now = 1.0 if i in cur else DIM
    was = 1.0 if (not prev or i in prev) else DIM
    return was + (now - was) * k


def _card(ctx, box, fill, outline=None, radius=None, opacity=1.0):
    w, h = box[2] - box[0], box[3] - box[1]
    if w < 2 or h < 2:
        return
    r = radius if radius is not None else max(12, int(min(w, h) * 0.08))
    ctx.blit(rounded((w, h), r, fill, outline), (box[0], box[1]), opacity)


def _fit(ctx, base, measure, avail, floor=0.72):
    """Largest scale k (1.0 down to floor) so measure(base*k) fits in avail.
    Below the floor the content is reported as a violation instead of being shrunk further."""
    k = 1.0
    while k > floor and measure(base * k) > avail:
        k -= 0.04
    if measure(base * k) > avail:
        ctx.violations.append({"text": str(ctx.scene.get("sceneId", "edu")), "bbox": [0, 0, 0, 0], "t": round(ctx.t, 3), "reason": "Nội dung dài hơn khung, cần rút gọn chữ."})
    return base * k


def _icon(ctx, name, x, y, size, opacity=1.0):
    """Draw a code-drawn prop at (x, y) top-left. Unknown names are ignored, never an error."""
    if not name:
        return
    from kit_video.edu_props import prop

    sprite = prop(str(name), int(size), {**ctx.palette, "background": ctx.palette["background"]})
    if sprite is not None:
        ctx.blit(sprite, (x, y), opacity)


def _mark(ctx, kind, cx, cy, size, color, opacity=1.0):
    """Drawn bullet or arrow so no font glyph is needed."""
    d = max(8, int(size))
    sprite = Image.new("RGBA", (d * 2, d * 2), (0, 0, 0, 0))
    dr = ImageDraw.Draw(sprite)
    w = max(3, d // 5)
    if kind == "arrow":
        dr.line((d * 0.2, d, d * 1.7, d), fill=(*color, 255), width=w)
        dr.line((d * 1.2, d * 0.5, d * 1.75, d), fill=(*color, 255), width=w)
        dr.line((d * 1.2, d * 1.5, d * 1.75, d), fill=(*color, 255), width=w)
    else:
        dr.ellipse((d * 0.55, d * 0.55, d * 1.45, d * 1.45), fill=(*color, 255))
    ctx.blit(sprite, (cx - d, cy - d), opacity)


def _frame(ctx):
    paint_background(ctx)
    l, t, r, b = ctx.safe()
    tall = ctx.height >= ctx.width
    base = ctx.height * (0.030 if tall else 0.050) * float(getattr(ctx, "edu_zoom", 1.0))
    dy = int(getattr(ctx, "edu_dy", 0))
    t += dy
    b += dy  # shifted as a whole: the room left for the scene stays the same
    return l, t, r, b, tall, base


def _kicker(ctx, l, t, base):
    k = ctx.params.get("kicker")
    if ctx.width > ctx.height and (ctx.params.get("title") or ctx.params.get("chain")):
        return t  # 16:9 has little height: the title carries the scene
    if isinstance(k, str) and k:
        ctx.text(k.upper(), (l, t), int(base * 0.8), _c(ctx, "accent", "ink"), bold=True, opacity=ease_out(progress(ctx.t, 0.1, 0.5)))
        return t + int(base * 1.9)
    return t


def edu_title(ctx, assets, repo_root, project_dir):
    """Series title card: episode tag, big title, one-line promise."""
    del assets, repo_root, project_dir
    l, t, r, b, tall, base = _frame(ctx)
    w = r - l
    tag = ctx.params.get("tag", "")
    title = ctx.params.get("title", "")
    sub = ctx.params.get("subtitle", "")
    isz = int(base * 6)
    tw = w - int(base * 6) if (ctx.params.get("icon") and not tall) else w
    top_pad = (isz + int(base * 0.8)) if (ctx.params.get("icon") and tall) else 0

    def total(sz):
        s2 = int(max(sz * 0.5, base * 1.3))
        return top_pad + int(base * 2.9) + _height(ctx, title, int(sz), True, tw, 1.2) + int(base * 1.4) + (_height(ctx, sub, s2, False, tw) if sub else 0)

    size = int(_fit(ctx, base * (2.5 if tall else 2.2), total, b - t, 0.55))
    block = total(size)
    y = t + max(top_pad, (b - t - block) // 2 + top_pad)
    a0 = ease_out(progress(ctx.t, 0.1, 0.6))
    if tag:
        tag_size = int(base * 0.95)
        pill_w = int(ImageDraw.Draw(ctx.image).textlength(tag, font=ctx.font(tag_size, True)) + base * 1.8)
        while pill_w > w - (isz + int(base * 0.5) if (ctx.params.get("icon") and not tall) else 0) and tag_size > 18:
            tag_size -= 2
            pill_w = int(ImageDraw.Draw(ctx.image).textlength(tag, font=ctx.font(tag_size, True)) + base * 1.8)
        _card(ctx, (l, y, l + pill_w, y + int(base * 1.9)), _c(ctx, "accent", "ink"), radius=int(base), opacity=a0)
        ctx.text(tag, (l + pill_w / 2, y + base * 0.95), tag_size, _c(ctx, "paper", "background"), bold=True, anchor="mm", opacity=a0)
    y += int(base * 2.9)
    a1 = ease_out(progress(ctx.t, 0.3, 0.9))
    if ctx.params.get("icon"):
        _icon(ctx, ctx.params["icon"], r - isz if tall else r - int(base * 5), t if tall else y, isz if tall else int(base * 5), a1)
    y = _para(ctx, title, l, y + int((1 - a1) * base), tw, size, _c(ctx, "ink", "ink"), True, a1, 1.2)
    bar = int(w * 0.24 * ease_out(progress(ctx.t, 0.7, 1.2)))
    ctx.image.paste(_c(ctx, "accent", "ink"), (l, y + int(base * 0.5), l + bar, y + int(base * 0.5) + max(4, int(base * 0.2))))
    if sub:
        _para(ctx, sub, l, y + int(base * 1.4), tw, int(max(size * 0.5, base * 1.3)), _c(ctx, "muted", "ink"), False, ease_out(progress(ctx.t, 1.0, 1.5)))


def _title(ctx, l, t, w, base):
    if ctx.params.get("title"):
        t = _para(ctx, ctx.params["title"], l, t, w, int(base * 1.7), _c(ctx, "ink", "ink"), True, ease_out(progress(ctx.t, 0.1, 0.5)), 1.2) + int(base * 0.7)
    return t


def edu_contrast(ctx, assets, repo_root, project_dir):
    """Two sides: the problem and the fix. Cards are sized from their text."""
    del assets, repo_root, project_dir
    l, t, r, b, tall, base = _frame(ctx)
    t = _kicker(ctx, l, t, base)
    w = r - l
    sides = [s for s in (ctx.params.get("left"), ctx.params.get("right")) if isinstance(s, dict)][:2]
    if not sides:
        return
    t0 = t
    tt = t0 + (_height(ctx, ctx.params.get("title", ""), int(base * 1.7), True, w, 1.2) + int(base * 0.7) if ctx.params.get("title") else 0)
    gap = int(base * 0.9)
    cw = w if tall else (w - gap) // 2

    def pad(bs):
        return int(bs * 1.1)

    def side_h(bs, s):
        iw = cw - 2 * pad(bs)
        h = pad(bs) * 2 + int(bs * 1.7)
        if s.get("big"):
            h += int(bs * 3.8)
        elif s.get("icon"):
            h += int(bs * 3.0)
        h += _height(ctx, s.get("text", ""), int(bs * 1.25), False, iw, 1.35)
        for it in s.get("items") or []:
            h += int(bs * 0.2) + _height(ctx, str(it), int(bs * 1.1), False, iw - int(bs * 1.2), 1.35)
        return h

    def need(bs):
        hs = [side_h(bs, x) for x in sides]
        return (sum(hs) + gap) if tall else max(hs)

    if need(base * 0.8) > b - tt:  # secondary bullets go first; the main text is never shrunk below the floor
        sides = [{**x, "items": []} for x in sides]
        ctx.dropped = True
    base = _fit(ctx, base, need, b - tt)
    t = _title(ctx, l, t0, w, base)
    bad, good = _c(ctx, "danger", "accent"), _c(ctx, "good", "accent")
    paper = _c(ctx, "paper", "background")
    hs = [side_h(base, x) for x in sides]
    top_y = t
    if tall:
        boxes = []
        for h in hs:
            boxes.append((l, top_y, r, top_y + h))
            top_y += h + gap
    else:
        h = max(hs)
        boxes = [(l + i * (cw + gap), t, l + i * (cw + gap) + cw, t + h) for i in range(len(sides))]
    for i, (s, box) in enumerate(zip(sides, boxes)):
        a = _vis(ctx, i, _at(ctx, i, 0.7, 0.4))
        want = s.get("tone", "bad" if i == 0 else "good")
        tone = bad if want == "bad" else good if want == "good" else (_c(ctx, want, "accent") if want in ctx.palette else good)
        shift = int((1 - _sa(a)) * base * 2)
        box = (box[0], box[1] + shift, box[2], box[3] + shift)
        a = a * _emph(ctx, i)
        _card(ctx, box, _mix(paper, tone, 0.10), tone, opacity=a)
        pd = pad(base)
        iw = cw - 2 * pd
        y = box[1] + pd
        isz = int(base * 3.4)
        _icon(ctx, s.get("icon"), box[2] - pd - isz, box[1] + pd, isz, a)
        ctx.text(str(s.get("label", "")).upper(), (box[0] + pd, y), int(base * 0.85), tone, bold=True, opacity=a)
        y += int(base * 1.7)
        if s.get("big"):
            ctx.text(str(s["big"]), (box[0] + pd, y), int(base * 3.0), tone, bold=True, opacity=a)
        if s.get("big"):
            y += int(base * 3.8)
        elif s.get("icon"):
            y += int(base * 3.0)
        y = _para(ctx, s.get("text", ""), box[0] + pd, y, iw, int(base * 1.25), _c(ctx, "ink", "ink"), False, a, 1.35)
        for item in s.get("items") or []:
            y += int(base * 0.2)
            _mark(ctx, "dot", box[0] + pd + base * 0.4, y + base * 0.75, base * 0.5, tone, a)
            y = _para(ctx, str(item), box[0] + pd + int(base * 1.2), y, iw - int(base * 1.2), int(base * 1.1), _c(ctx, "ink", "ink"), False, a, 1.35)


def edu_steps(ctx, assets, repo_root, project_dir):
    """Two to four numbered cards. Rows in 9:16 (2x2 grid for four), one row of columns in 16:9."""
    del assets, repo_root, project_dir
    l, t, r, b, tall, base = _frame(ctx)
    t = _kicker(ctx, l, t, base)
    p = ctx.params
    w = r - l
    items = [s for s in (p.get("steps") or []) if isinstance(s, dict)][:4]
    if not items:
        return
    n = len(items)
    t0 = t
    tt = t0 + (_height(ctx, p.get("title", ""), int(base * 1.7), True, w, 1.2) + int(base * 0.7) if p.get("title") else 0)
    gap = int(base * 0.8)
    grid = tall and n == 4
    cols = n if not tall else (2 if grid else 1)
    cw = (w - gap * (cols - 1)) // cols
    col_mode = (not tall) or grid  # number and icon on top, text below

    def card_h(bs, s):
        pd = int(bs * 1.0)
        d = int(bs * 2.4)
        if not col_mode:
            tw = cw - 2 * pd - d - int(bs * 0.8) - (int(bs * 3.4) if s.get("icon") else 0)
            h = max(d, _height(ctx, s.get("head", ""), int(bs * 1.35), True, tw, 1.25) + (int(bs * 0.3) + _height(ctx, s["text"], int(bs * 1.1), False, tw, 1.35) if s.get("text") else 0))
            return 2 * pd + h
        tw = cw - 2 * pd
        h = d + int(bs * 0.7) + _height(ctx, s.get("head", ""), int(bs * 1.35), True, tw, 1.25)
        if s.get("text"):
            h += int(bs * 0.3) + _height(ctx, s["text"], int(bs * 1.1), False, tw, 1.35)
        return 2 * pd + h

    def need(bs):
        hs = [card_h(bs, x) for x in items]
        if grid:
            return max(hs[0], hs[1]) + gap + max(hs[2], hs[3])
        return (sum(hs) + gap * (n - 1)) if tall else max(hs)

    base = _fit(ctx, base, need, b - tt)
    t = _title(ctx, l, t0, w, base)
    paper = _c(ctx, "paper", "background")
    tones = [_c(ctx, k2, "accent") for k2 in ("accent", "good", "warn", "danger")]
    hs = [card_h(base, x) for x in items]
    boxes = []
    if grid:
        row1, row2 = max(hs[0], hs[1]), max(hs[2], hs[3])
        for i in range(4):
            x0 = l + (i % 2) * (cw + gap)
            y0 = t if i < 2 else t + row1 + gap
            boxes.append((x0, y0, x0 + cw, y0 + (row1 if i < 2 else row2)))
    elif tall:
        y0 = t
        for h in hs:
            boxes.append((l, y0, r, y0 + h))
            y0 += h + gap
    else:
        h = max(hs)
        boxes = [(l + i * (cw + gap), t, l + i * (cw + gap) + cw, t + h) for i in range(n)]
    for i, (s, box) in enumerate(zip(items, boxes)):
        a = _vis(ctx, i, _at(ctx, i, 0.6, 0.4))
        tone = tones[i % len(tones)]
        shift = int((1 - _sa(a)) * base * 1.5)
        box = (box[0], box[1] + shift, box[2], box[3] + shift)
        a = a * _emph(ctx, i)
        _card(ctx, box, paper, _mix(paper, tone, 0.5), opacity=a)
        pd = int(base * 1.0)
        d = int(base * 2.4)
        _card(ctx, (box[0] + pd, box[1] + pd, box[0] + pd + d, box[1] + pd + d), tone, radius=d // 2, opacity=a)
        ctx.text(str(i + 1), (box[0] + pd + d / 2, box[1] + pd + d / 2), int(base * 1.4), paper, bold=True, anchor="mm", opacity=a)
        tx = box[0] + pd + (0 if col_mode else d + int(base * 0.8))
        ty = box[1] + pd + (d + int(base * 0.7) if col_mode else 0)
        tw = box[2] - tx - pd
        isz = int(base * 2.8)
        if s.get("icon"):
            if col_mode:
                _icon(ctx, s["icon"], box[2] - pd - isz, box[1] + pd - int(base * 0.2), isz, a)
            else:
                tw -= int(base * 3.4)
                _icon(ctx, s["icon"], box[2] - pd - isz, box[1] + pd, isz, a)
        ty = _para(ctx, s.get("head", ""), tx, ty, tw, int(base * 1.35), _c(ctx, "ink", "ink"), True, a, 1.25)
        if s.get("text"):
            _para(ctx, s["text"], tx, ty + int(base * 0.3), tw, int(base * 1.1), _c(ctx, "muted", "ink"), False, a, 1.35)


def edu_bignumber(ctx, assets, repo_root, project_dir):
    """One number, its dated source, and a chain of consequences."""
    del assets, repo_root, project_dir
    l, t, r, b, tall, base = _frame(ctx)
    t = _kicker(ctx, l, t, base)
    p = ctx.params
    w = r - l
    big = str(p.get("number", ""))
    chain = [str(x) for x in (p.get("chain") or [])][:5]
    label, source = str(p.get("label", "")), str(p.get("source", ""))

    icon = p.get("icon")

    def numsize(bs):
        size = int(bs * (5.2 if tall else 4.6))
        font = ctx.font(size, True)
        probe = ImageDraw.Draw(ctx.image)
        room = w - (int(bs * 5.6) if icon else 0)
        while size > bs and probe.textlength(big, font=font) > room:
            size = int(size * 0.92)
            font = ctx.font(size, True)
        return size

    def chain_w(bs):
        n = max(1, len(chain))
        return (w - int(bs * 2.4) * (n - 1)) // n

    def need(bs):
        h = max(int(numsize(bs) * 1.35), int(bs * 5.3) if icon else 0) + _height(ctx, label, int(bs * 1.35), True, w, 1.3)
        if source:
            h += int(bs * 0.4) + _height(ctx, "Nguồn: " + source, int(bs * 0.9), False, w, 1.3)
        h += int(bs * 1.2)
        if not tall:
            return h + (max(_height(ctx, x, int(bs * 1.2), False, chain_w(bs) - int(bs * 1.6)) for x in chain) + int(bs * 1.6) if chain else 0)
        for text in chain:
            h += max(int(bs * 3.2), _height(ctx, text, int(bs * 1.2), False, w - int(bs * 3.6)) + int(bs * 1.6)) + int(bs * 0.6)
        return h

    while tall and len(chain) > 1 and need(base * 0.8) > b - t:  # drop the last consequence rather than shrink type
        chain = chain[:-1]
        ctx.dropped = True
    base = _fit(ctx, base, need, b - t)
    a = ease_out(progress(ctx.t, 0.2, 0.9))
    size = numsize(base)
    tone = _c(ctx, p.get("tone", "accent"), "accent")
    ctx.text(big, (l, t + int((1 - a) * base)), size, tone, bold=True, opacity=a)
    _icon(ctx, icon, r - int(base * 5.0), t, int(base * 5.0), a)
    y = t + max(int(size * 1.35), int(base * 5.3) if icon else 0)
    if label:
        y = _para(ctx, label, l, y, w, int(base * 1.35), _c(ctx, "ink", "ink"), True, a, 1.3)
    if source:
        y = _para(ctx, "Nguồn: " + source, l, y + int(base * 0.4), w, int(base * 0.9), _c(ctx, "muted", "ink"), False, ease_out(progress(ctx.t, 0.8, 1.3)), 1.3)
    y += int(base * 1.2)
    paper = _c(ctx, "paper", "background")
    if not tall and chain:
        cwid = chain_w(base)
        gapw = int(base * 2.4)
        hh = max(_height(ctx, x, int(base * 1.2), False, cwid - int(base * 1.6)) for x in chain) + int(base * 1.6)
        for i, text in enumerate(chain):
            s = _vis(ctx, i, _at(ctx, i, 0.7, 1.4, 0.5)) * _emph(ctx, i)
            x0 = l + i * (cwid + gapw)
            _card(ctx, (x0, y, x0 + cwid, y + hh), _mix(paper, tone, 0.08), opacity=s)
            _para(ctx, text, x0 + int(base * 0.8), y + int(base * 0.8), cwid - int(base * 1.6), int(base * 1.2), _c(ctx, "ink", "ink"), False, s, 1.3)
            if i:
                _mark(ctx, "arrow", x0 - gapw / 2, y + hh / 2, base * 0.9, tone, s)
        return
    tw = w - int(base * 3.6)
    for i, text in enumerate(chain):
        s = _vis(ctx, i, _at(ctx, i, 0.7, 1.4, 0.5)) * _emph(ctx, i)
        th = _height(ctx, text, int(base * 1.2), False, tw)
        h = max(int(base * 3.2), th + int(base * 1.6))
        _card(ctx, (l, y, r, y + h), _mix(paper, tone, 0.08), opacity=s)
        _mark(ctx, "arrow" if i else "dot", l + base * 1.3, y + h / 2, base * 0.9, tone, s)
        _para(ctx, text, l + int(base * 2.6), y + (h - th) // 2, tw, int(base * 1.2), _c(ctx, "ink", "ink"), False, s, 1.3)
        y += h + int(base * 0.6)


def edu_rules(ctx, assets, repo_root, project_dir):
    """Numbered rules, one line each, large type."""
    del assets, repo_root, project_dir
    l, t, r, b, tall, base = _frame(ctx)
    t = _kicker(ctx, l, t, base)
    p = ctx.params
    w = r - l
    if p.get("title"):
        t = _para(ctx, p["title"], l, t, w, int(base * 1.7), _c(ctx, "ink", "ink"), True, ease_out(progress(ctx.t, 0.1, 0.5)), 1.2) + int(base * 1.0)
    rules = [str(x) for x in (p.get("rules") or [])][:6]
    icons0 = p.get("icons") or []

    def need(bs):
        tot = 0
        for i, tx in enumerate(rules):
            tw = w - int(bs * 3.4) - (int(bs * 3.2) if i < len(icons0) and icons0[i] else 0)
            tot += max(int(bs * 0.4) + _height(ctx, tx, int(bs * 1.45), True, tw, 1.28), int(bs * 3.6)) + int(bs * 0.8)
        return tot

    base = _fit(ctx, base, need, b - t)
    accent = _c(ctx, "accent", "ink")
    y = t
    for i, text in enumerate(rules):
        a = _vis(ctx, i, _at(ctx, i, 0.8, 0.5))
        sh = int((1 - _sa(a)) * base * 0.6)
        a = a * _emph(ctx, i)
        num = f"{i + 1}"
        ctx.text(num, (l, y - sh), int(base * 3.2), accent, bold=True, opacity=a)
        icons = ctx.params.get("icons") or []
        if i < len(icons) and icons[i]:
            _icon(ctx, icons[i], r - int(base * 2.8), y, int(base * 2.8), a)
        tw = w - int(base * 3.4) - (int(base * 3.2) if i < len(icons) and icons[i] else 0)
        yy = _para(ctx, text, l + int(base * 3.0), y + int(base * 0.4), tw, int(base * 1.45), _c(ctx, "ink", "ink"), True, a, 1.28)
        y = max(yy, y + int(base * 3.6)) + int(base * 0.8)
        if y > b:
            break


def edu_flow(ctx, assets, repo_root, project_dir):
    """Two containers with a labelled one-way valve between them."""
    del assets, repo_root, project_dir
    l, t, r, b, tall, base = _frame(ctx)
    t = _kicker(ctx, l, t, base)
    p = ctx.params
    w = r - l
    t = _title(ctx, l, t, w, base)
    tanks = [x for x in (p.get("tanks") or []) if isinstance(x, dict)][:2]
    if len(tanks) < 2:
        return
    paper = _c(ctx, "paper", "background")
    v = p.get("valve") if isinstance(p.get("valve"), str) else ""
    gap = int(base * 4.2) if tall else int(base * 11)
    if tall:
        th = (b - t - gap) // 2
        boxes = [(l, t, r, t + th), (l, t + th + gap, r, t + 2 * th + gap)]
    else:
        tw = (w - gap) // 2
        boxes = [(l, t, l + tw, b), (l + tw + gap, t, r, b)]
    tones = [_c(ctx, "good", "accent"), _c(ctx, "warn", "accent")]
    for i, (tk, box) in enumerate(zip(tanks, boxes)):
        a = _vis(ctx, i, _at(ctx, i, 0.6, 0.3)) * _emph(ctx, i)
        tone = tones[i]
        _card(ctx, box, paper, tone, opacity=a)
        lvl = max(0.05, min(1.0, float(tk.get("level", 0.6)))) * ease_out(progress(ctx.t, 0.5 + i * 0.4, 1.6 + i * 0.4))
        pad = 6
        fh = int((box[3] - box[1] - 2 * pad) * lvl)
        ip = int(base * 1.1)
        lw = box[2] - box[0] - 2 * ip
        lab_h = _height(ctx, tk.get("label", ""), int(base * 1.15), True, lw, 1.2)
        head_h = ip + lab_h + int(base * 0.4) + _height(ctx, tk.get("note", ""), int(base * 1.0), False, lw, 1.3) + ip
        fh = min(fh, box[3] - box[1] - 2 * pad - head_h) if lvl < 0.99 else fh
        if fh > 8:
            fill = Image.new("RGBA", (box[2] - box[0] - 2 * pad, fh), (*_mix(paper, tone, 0.35), 255))
            ctx.blit(fill, (box[0] + pad, box[3] - pad - fh), a)
        isz = int(base * 3.0)
        _icon(ctx, tk.get("icon"), box[2] - ip - isz, box[3] - ip - isz, isz, a)
        ny = _para(ctx, tk.get("label", ""), box[0] + ip, box[1] + ip, lw, int(base * 1.15), _c(ctx, "ink", "ink"), True, a, 1.2)
        _para(ctx, tk.get("note", ""), box[0] + ip, ny + int(base * 0.4), lw, int(base * 1.0), _c(ctx, "ink", "ink"), False, a, 1.3)
    if v:
        a = ease_out(progress(ctx.t, 1.8, 2.4))
        cx, cy = ((l + r) // 2, boxes[0][3] + gap // 2) if tall else ((boxes[0][2] + boxes[1][0]) // 2, (t + b) // 2)
        d = int(base * 0.9)
        mark = Image.new("RGBA", (d * 2 + 8, d * 2 + 8), (0, 0, 0, 0))
        md = ImageDraw.Draw(mark)
        ink = _c(ctx, "danger", "accent")
        md.line((4, 4, d * 2 + 4, d * 2 + 4), fill=(*ink, 255), width=max(6, d // 3))
        md.line((d * 2 + 4, 4, 4, d * 2 + 4), fill=(*ink, 255), width=max(6, d // 3))
        lw = (w - 2 * int(base * 2)) if tall else gap - int(base * 0.6)
        lh = _height(ctx, v, int(base * 0.9), True, lw, 1.25)
        top = cy - (mark.height + int(base * 0.3) + lh) / 2
        ctx.blit(mark, (cx - mark.width / 2, top), a)
        _para(ctx, v, cx - lw // 2, int(top + mark.height + base * 0.3), lw, int(base * 0.9), ink, True, a, 1.25, "m")


def _synthetic(seed, n, trend):
    import random

    rnd = random.Random(seed)
    v, rows = 100.0, []
    for _ in range(n):
        o = v
        v = max(5.0, v + rnd.uniform(-2.2, 2.2) + trend)
        rows.append({"o": o, "c": v, "h": max(o, v) + rnd.uniform(0.2, 1.6), "l": min(o, v) - rnd.uniform(0.2, 1.6)})
    return rows


def edu_chart(ctx, assets, repo_root, project_dir):
    """Candles in a panel. Synthetic data is always labelled; real data needs a stored OHLCV file and a source.

    params: title, trend (-1..1), seed, count, levels [{at 0..1, label, tone}], dataFile + start (real data), source.
    No entry, exit or profit wording is drawn here. Level labels come from the script and are neutral.
    """
    del assets
    l, t, r, b, tall, base = _frame(ctx)
    t = _kicker(ctx, l, t, base)
    p = ctx.params
    w = r - l
    t = _title(ctx, l, t, w, base)
    count = max(8, min(60, int(p.get("count", 28))))
    real = isinstance(p.get("dataFile"), str) and p["dataFile"]
    if real:
        from kit_video.chart import load_candles

        key = ("edu-ohlcv", p["dataFile"])
        series = ctx.cache.get(key)
        if series is None:
            base_dir = repo_root if p.get("dataBase") == "repo" else project_dir
            series = load_candles(Path(base_dir) / p["dataFile"])
            ctx.cache[key] = series
        start = int(p.get("start", 0))
        rows = [{"o": c["o"], "c": c["c"], "h": c["h"], "l": c["l"]} for c in series["candles"][start:start + count]]
        tag = "Nguồn: " + str(p.get("source") or series.get("source") or "")
        if not p.get("source"):
            ctx.violations.append({"text": "edu_chart", "bbox": [0, 0, 0, 0], "t": round(ctx.t, 3), "reason": "Biểu đồ dữ liệu thật phải ghi nguồn và ngày."})
    else:
        rows = _synthetic(int(p.get("seed", 7)), count, float(p.get("trend", 0)) * 1.4)
        tag = "Dữ liệu minh họa, không phải giá thị trường"
    paper = _c(ctx, "paper", "background")
    up, down = _c(ctx, "good", "accent"), _c(ctx, "danger", "accent")
    foot = int(base * (2.9 if tall else 1.7))
    box = (l, t, r, b - foot)
    a0 = ease_out(progress(ctx.t, 0.1, 0.5))
    _card(ctx, box, paper, _mix(paper, _c(ctx, "muted", "ink"), 0.4), opacity=a0)
    pad = int(base * 1.2)
    x0, x1, y0, y1 = box[0] + pad, box[2] - pad, box[1] + pad, box[3] - pad
    lo = min(c["l"] for c in rows)
    hi = max(c["h"] for c in rows)
    sy = lambda v: y1 - (v - lo) / (hi - lo + 1e-9) * (y1 - y0)
    n = len(rows)
    step = (x1 - x0) / n
    shown = n * ease_out(progress(ctx.t, 0.4, 0.4 + min(3.0, ctx.duration * 0.5)))
    layer = Image.new("RGBA", (ctx.width, ctx.height), (0, 0, 0, 0))
    dr = ImageDraw.Draw(layer)
    for i, c in enumerate(rows):
        if i + 1 > shown + 0.001:
            break
        cx = x0 + (i + 0.5) * step
        col = up if c["c"] >= c["o"] else down
        dr.line((cx, sy(c["h"]), cx, sy(c["l"])), fill=(*col, 255), width=max(2, int(step * 0.08)))
        top, bot = sorted((sy(c["o"]), sy(c["c"])))
        dr.rectangle((cx - step * 0.3, top, cx + step * 0.3, max(bot, top + 2)), fill=(*col, 255))
    ctx.image.alpha_composite(layer)
    for lv in p.get("levels") or []:
        if not isinstance(lv, dict):
            continue
        la = ease_out(progress(ctx.t, 1.6, 2.2))
        y = y1 - float(lv.get("at", 0.5)) * (y1 - y0)
        tone = _c(ctx, lv.get("tone", "warn"), "accent")
        for x in range(int(x0), int(x1), 28):
            ctx.image.paste(tone, (x, int(y), min(x + 16, int(x1)), int(y) + max(2, int(base * 0.12))))
        lab = str(lv.get("label", ""))
        fs = int(base * 0.95)
        wd = int(ImageDraw.Draw(ctx.image).textlength(lab, font=ctx.font(fs, True))) + int(base * 1.2)
        _card(ctx, (int(x1) - wd, int(y) - int(base * 1.9), int(x1), int(y) - int(base * 0.1)), _mix(paper, tone, 0.18), tone, radius=int(base * 0.4), opacity=la)
        ctx.text(lab, (int(x1) - wd / 2, int(y) - base * 1.0), fs, tone, True, "mm", la)
    _para(ctx, tag, l, b - foot + int(base * 0.4), w, int(base * 0.85), _c(ctx, "muted", "ink"), False, a0, 1.3)


TEMPLATES = {
    "edu_title": edu_title,
    "edu_contrast": edu_contrast,
    "edu_steps": edu_steps,
    "edu_bignumber": edu_bignumber,
    "edu_rules": edu_rules,
    "edu_flow": edu_flow,
    "edu_chart": edu_chart,
}
