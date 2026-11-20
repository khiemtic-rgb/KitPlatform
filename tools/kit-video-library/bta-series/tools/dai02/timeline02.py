"""Dòng thời gian video dài tập 02 từ 3 phần giọng ElevenLabs (mốc từng ký tự có sẵn trong *.timing.json).
Ra: timeline.json (mốc câu, chữ, cảnh trên video) và voice_full.wav (giọng đã chèn khoảng nghỉ)."""
import json, re, subprocess, numpy as np
SR = 48000
def load(f):
    raw = subprocess.run(['ffmpeg', '-v', 'error', '-i', f, '-ac', '1', '-ar', str(SR), '-f', 'f32le', '-'], capture_output=True, check=True).stdout
    return np.frombuffer(raw, np.float32).copy()
AUD = {n: load(f'{n}.mp3') for n in (1, 2, 3)}
# câu hiển thị (phụ đề) và cảnh, theo file kịch bản
shown, scene_of, sc = [], [], -1
for l in open('02-tet-vietnamese-way.txt', encoding='utf-8').read().split('\n'):
    if l.startswith('Cảnh '): sc += 1; continue
    if l.strip() and not l.startswith('#'): shown.append(l.strip()); scene_of.append(sc)
# câu đọc (có thẻ diễn xuất) theo phần
g = open('02-tet-vietnamese-way.giong-v4.txt', encoding='utf-8').read().split('\n')
parts, cur = {}, 0
for l in g:
    m = re.match(r'^#\s*=+\s*PHẦN\s+(\d+)', l.strip())
    if m: cur = int(m.group(1)); parts[cur] = []; continue
    if cur and l.strip() and not l.lstrip().startswith('#'): parts[cur].append(l)
L = []
for n in (1, 2, 3):
    a = json.load(open(f'{n}.timing.json'))['alignment']
    s = ''.join(a['characters']); st = a['character_start_times_seconds']; en = a['character_end_times_seconds']
    pos = 0
    for line in parts[n]:
        i0 = s.index(line, pos); pos = i0 + len(line)
        body = re.sub(r'\[[^\]]*\]\s*', lambda m: ' ' * len(m.group(0)), line)  # thẻ diễn xuất -> khoảng trắng, giữ vị trí ký tự
        words = [(i0 + m.start(), i0 + m.end() - 1) for m in re.finditer(r'\S+', body)]
        wt = [[st[a_], en[b_]] for a_, b_ in words]
        L.append({'part': n, 'spoken': line, 's2': wt[0][0], 'e2': wt[-1][1], 'words': wt})
assert len(L) == len(shown), (len(L), len(shown))
for o, t, k in zip(L, shown, scene_of): o['shown'] = t; o['scene'] = k
GAP_IN, GAP_SCENE, EXTRA_DOTS, LEAD0 = 0.50, 1.05, 0.25, 0.8
pieces, scenes, t = [], [{'scene': 0, 'start': 0.0}], LEAD0
for i, o in enumerate(L):
    prev = L[i - 1] if i else None
    a = max(0.0, o['s2'] - 0.06); b = min(len(AUD[o['part']]) / SR, o['e2'] + 0.15)
    if prev and prev['part'] == o['part']: a = max(a, (prev['e2'] + o['s2']) / 2)
    if i + 1 < len(L) and L[i + 1]['part'] == o['part']: b = min(b, (o['e2'] + L[i + 1]['s2']) / 2)
    if prev:
        gap = GAP_SCENE if o['scene'] != prev['scene'] else GAP_IN
        if prev['shown'].rstrip().endswith('...'): gap += EXTRA_DOTS
        t += gap
        if o['scene'] != prev['scene']: scenes.append({'scene': o['scene'], 'start': t - 0.45})
    pieces.append((t, o['part'], a, b))
    sh = t - a
    o['out_s'] = o['s2'] + sh; o['out_e'] = o['e2'] + sh; o['wout'] = [[x + sh, y + sh] for x, y in o['words']]
    t += b - a
VOICE_END = t; END_CARD = 12.0; TOTAL = VOICE_END + 1.2 + END_CARD
for k, s in enumerate(scenes): s['end'] = scenes[k + 1]['start'] if k + 1 < len(scenes) else VOICE_END + 1.2
v = np.zeros(int(TOTAL * SR) + SR, np.float32)
for ot, part, a, b in pieces:
    x = AUD[part][int(a * SR):int(b * SR)].copy(); f = int(0.012 * SR)
    x[:f] *= np.linspace(0, 1, f); x[-f:] *= np.linspace(1, 0, f)
    i = int(ot * SR); v[i:i + len(x)] += x
subprocess.run(['ffmpeg', '-v', 'error', '-y', '-f', 'f32le', '-ar', str(SR), '-ac', '1', '-i', '-', 'voice_full.wav'], input=v[:int(TOTAL * SR)].tobytes(), check=True)
json.dump({'lines': L, 'scenes': scenes, 'voice_end': VOICE_END, 'end_card': [VOICE_END + 1.2, TOTAL], 'total': TOTAL}, open('timeline.json', 'w'), ensure_ascii=False, indent=1)
print('VOICE_END', round(VOICE_END, 2), 'TOTAL', round(TOTAL, 2), 'lines', len(L))
for s in scenes: print(s['scene'] + 1, round(s['start'], 2), round(s['end'], 2), round(s['end'] - s['start'], 1))
