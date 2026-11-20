"""Quy tắc người dẫn BTA (chủ kênh chốt 14/11/2026, áp dụng từ tập 03):
1. Người dẫn to hơn: mọi mục `host` tối thiểu s=115 (cũ 80–90), đứng bên phải khung (x=82, y=60).
2. Khung trống phải có người dẫn: câu nào không có ảnh (photo/focus_pan/photo_flash) và không có người dẫn thì chèn người dẫn;
   nhiều câu trống liền nhau dùng chung một người dẫn. Nội dung ở giữa khung được dời sang trái và thu nhỏ để không đè lên người dẫn.
Dùng:  python them_nguoi_dan.py <tap>.hinh.json [ra.json]   (không ghi đè file gốc nếu có tham số ra)."""
import json, sys
PHOTO = {'photo', 'focus_pan', 'photo_flash'}
HOST_S, HOST_X, HOST_Y = 115, 82, 60
MOODS = [('wink', 'point'), ('smile', 'hold'), ('curious', 'point'), ('think', 'think'), ('surprise', 'shrug'), ('smile', 'point')]

def active(it, pos):
    if int(it.get('line', 0)) > pos: return False
    hide = (it.get('hide') or {}).get('line')
    return hide is None or int(hide) > pos

def scene_lines(items):
    n = 0
    for it in items:
        n = max(n, int(it.get('line', 0)) + 1, int((it.get('hide') or {}).get('line', 0)))
    return n

def apply(data):
    added, moved = 0, 0
    k = 0
    for sc in data['scenes']:
        items = sc['params']['items']
        for it in items:
            if it['art'] == 'host':
                it['s'] = max(int(it.get('s', 0)), HOST_S); it['x'] = HOST_X; it['y'] = HOST_Y
        n = scene_lines(items)
        empty = [p for p in range(n) if not any(active(it, p) and (it['art'] in PHOTO or it['art'] == 'host') for it in items)]
        runs = []
        for p in empty:
            if runs and runs[-1][1] == p: runs[-1][1] = p + 1
            else: runs.append([p, p + 1])
        for a, b in runs:
            mood, pose = MOODS[k % len(MOODS)]; k += 1
            host = {'art': 'host', 'x': HOST_X, 'y': HOST_Y, 's': HOST_S, 'line': a, 'from': 'right',
                    'args': {'mood': mood, 'pose': pose, 'talk': True}, '_tuDong': 'quy tắc khung trống'}
            if b < n: host['hide'] = {'line': b}
            items.append(host); added += 1
            for it in items:
                if it is host or it['art'] in PHOTO or it['art'] == 'host': continue
                if any(active(it, p) for p in range(a, b)) and 30 <= float(it.get('x', 50)) <= 70 and float(it.get('s', 0)) >= 30 and not it.get('_doiTrai'):
                    it['x'] = round(float(it.get('x', 50)) - 12, 1)
                    if float(it['s']) >= 60: it['s'] = round(float(it['s']) * 0.85, 1)  # chữ nhỏ chỉ dời, không thu thêm
                    it['_doiTrai'] = True; moved += 1
    return added, moved

if __name__ == '__main__':
    src = sys.argv[1]; dst = sys.argv[2] if len(sys.argv) > 2 else src
    data = json.load(open(src, encoding='utf-8'))
    a, m = apply(data)
    json.dump(data, open(dst, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    print(f'chèn {a} người dẫn, dời {m} mục sang trái')
