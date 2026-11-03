# -*- coding: utf-8 -*-
"""Generate matched polyline pairs from lucide SVGs for WPF GeometryAnimation morphing.
Output: src/MorphIcons.g.cs (static double[] arrays, figure-major)."""
import math, re, sys

PER = 72  # points per figure


def parse_path(d):
    """Flatten SVG path data to a list of subpaths (lists of (x,y))."""
    toks = re.findall(r'[MmLlHhVvCcSsQqTtAaZz]|-?\d*\.?\d+(?:e-?\d+)?', d)
    subs, cur = [], []
    x = y = sx = sy = 0.0
    i = 0
    cmd = None
    def num():
        nonlocal i
        v = float(toks[i]); i += 1
        return v
    while i < len(toks):
        if toks[i].isalpha():
            cmd = toks[i]; i += 1
        if cmd in 'Zz':
            if cur:
                cur.append((sx, sy)); subs.append(cur); cur = []
            x, y = sx, sy
            continue
        if cmd in 'Mm':
            nx, ny = num(), num()
            if cmd == 'm': nx += x; ny += y
            if cur: subs.append(cur)
            cur = [(nx, ny)]
            x, y = sx, sy = nx, ny
            cmd = 'Ll' if cmd == 'M' else 'll'
        elif cmd in 'Ll':
            nx, ny = num(), num()
            if cmd == 'l': nx += x; ny += y
            cur.append((nx, ny)); x, y = nx, ny
        elif cmd in 'Hh':
            nx = num()
            if cmd == 'h': nx += x
            cur.append((nx, y)); x = nx
        elif cmd in 'Vv':
            ny = num()
            if cmd == 'v': ny += y
            cur.append((x, ny)); y = ny
        elif cmd in 'Cc':
            p = [num() for _ in range(6)]
            if cmd == 'c':
                p = [p[0]+x, p[1]+y, p[2]+x, p[3]+y, p[4]+x, p[5]+y]
            for k in range(1, 9):
                t = k / 8
                mt = 1 - t
                cx = mt**3*x + 3*mt*mt*t*p[0] + 3*mt*t*t*p[2] + t**3*p[4]
                cy = mt**3*y + 3*mt*mt*t*p[1] + 3*mt*t*t*p[3] + t**3*p[5]
                cur.append((cx, cy))
            x, y = p[4], p[5]
        elif cmd in 'Ss':
            p = [num() for _ in range(4)]
            if cmd == 's':
                p = [p[0]+x, p[1]+y, p[2]+x, p[3]+y]
            x1, y1 = 2*x - cur[-2][0] if len(cur) > 1 else x, 2*y - cur[-2][1] if len(cur) > 1 else y
            for k in range(1, 9):
                t = k / 8
                mt = 1 - t
                cx = mt**3*x + 3*mt*mt*t*x1 + 3*mt*t*t*p[0] + t**3*p[2]
                cy = mt**3*y + 3*mt*mt*t*y1 + 3*mt*t*t*p[1] + t**3*p[3]
                cur.append((cx, cy))
            x, y = p[2], p[3]
        elif cmd in 'Aa':
            rx, ry, rot, laf, sf, nx, ny = (num() for _ in range(7))
            if cmd == 'a': nx += x; ny += y
            for pt in arc_to(x, y, rx, ry, rot, laf, sf, nx, ny):
                cur.append(pt)
            x, y = nx, ny
        elif cmd in 'QqTt':
            # lucide doesn't use these; approximate with line
            if cmd in 'Qq':
                num(); num()
            nx, ny = num(), num()
            if cmd in 'qTt': pass
            if cmd in 'qt': nx += x if cmd == 'q' else 0; ny += y if cmd == 'q' else 0
            cur.append((nx, ny)); x, y = nx, ny
        else:
            i += 1
    if cur: subs.append(cur)
    return subs


def arc_to(x1, y1, rx, ry, phi, laf, sf, x2, y2):
    """SVG arc -> point list (endpoint parameterization)."""
    if rx == 0 or ry == 0:
        return [(x2, y2)]
    phi = math.radians(phi)
    cosp, sinp = math.cos(phi), math.sin(phi)
    dx, dy = (x1 - x2) / 2, (y1 - y2) / 2
    x1p = cosp * dx + sinp * dy
    y1p = -sinp * dx + cosp * dy
    rx, ry = abs(rx), abs(ry)
    lam = x1p**2 / rx**2 + y1p**2 / ry**2
    if lam > 1:
        s = math.sqrt(lam); rx *= s; ry *= s
    num = rx**2 * ry**2 - rx**2 * y1p**2 - ry**2 * x1p**2
    den = rx**2 * y1p**2 + ry**2 * x1p**2
    co = math.sqrt(max(0.0, num / den))
    if laf == sf: co = -co
    cxp = co * rx * y1p / ry
    cyp = -co * ry * x1p / rx
    cx = cosp * cxp - sinp * cyp + (x1 + x2) / 2
    cy = sinp * cxp + cosp * cyp + (y1 + y2) / 2

    def ang(ux, uy, vx, vy):
        d = math.hypot(ux, uy) * math.hypot(vx, vy)
        c = max(-1, min(1, (ux * vx + uy * vy) / d))
        a = math.acos(c)
        return -a if ux * vy - uy * vx < 0 else a
    th1 = ang(1, 0, (x1p - cxp) / rx, (y1p - cyp) / ry)
    dth = ang((x1p - cxp) / rx, (y1p - cyp) / ry, (-x1p - cxp) / rx, (-y1p - cyp) / ry)
    if not sf and dth > 0: dth -= 2 * math.pi
    elif sf and dth < 0: dth += 2 * math.pi
    pts = []
    n = max(4, int(abs(dth) / 0.35))
    for k in range(1, n + 1):
        t = th1 + dth * k / n
        px = rx * math.cos(t); py = ry * math.sin(t)
        pts.append((cosp * px - sinp * py + cx, sinp * px + cosp * py + cy))
    return pts


def resample(pts, n):
    if pts[0] == pts[-1] and len(pts) > 2:
        pts = pts[:-1]  # closed: drop duplicate end
    closed = math.hypot(pts[0][0] - pts[-1][0], pts[0][1] - pts[-1][1]) < 0.6
    if closed:
        pts = pts + [pts[0]]
    cum = [0.0]
    for a, b in zip(pts, pts[1:]):
        cum.append(cum[-1] + math.hypot(b[0] - a[0], b[1] - a[1]))
    total = cum[-1]
    out = []
    j = 0
    for k in range(n):
        d = total * k / n
        while j < len(cum) - 2 and cum[j + 1] < d:
            j += 1
        seg = cum[j + 1] - cum[j] or 1
        f = (d - cum[j]) / seg
        out.append((pts[j][0] + (pts[j + 1][0] - pts[j][0]) * f,
                    pts[j][1] + (pts[j + 1][1] - pts[j][1]) * f))
    return out


def load_icon(path):
    svg = open(path, encoding='utf-8').read()
    figs = []
    for d in re.findall(r'\sd="([^"]+)"', svg):
        for sub in parse_path(d):
            if len(sub) >= 2:
                figs.append(sub)
    # primitive elements (my parser only eats d= attributes)
    for cx, cy, r in re.findall(r'<circle[^>]*cx="([\d.-]+)"[^>]*cy="([\d.-]+)"[^>]*r="([\d.-]+)"', svg):
        cx, cy, r = float(cx), float(cy), float(r)
        figs.append([(cx + r * math.cos(2 * math.pi * k / 48), cy + r * math.sin(2 * math.pi * k / 48)) for k in range(48)])
    for x1, y1, x2, y2 in re.findall(r'<line[^>]*x1="([\d.-]+)"[^>]*y1="([\d.-]+)"[^>]*x2="([\d.-]+)"[^>]*y2="([\d.-]+)"', svg):
        figs.append([(float(x1), float(y1)), (float(x2), float(y2))])
    return figs


def split_longest(figs):
    """Split the longest figure at its midpoint index -> counts += 1."""
    idx = max(range(len(figs)), key=lambda i: len(figs[i]))
    f = figs[idx]
    mid = len(f) // 2
    figs[idx] = f[:mid + 1]
    figs.insert(idx + 1, f[mid:])
    return figs


def centroid(f):
    return (sum(p[0] for p in f) / len(f), sum(p[1] for p in f) / len(f))


def align_pair(fa, fb):
    """Pad to equal figure count; resample to PER; align phase & direction."""
    while len(fa) < len(fb): fa = split_longest(fa)
    while len(fb) < len(fa): fb = split_longest(fb)
    # match figures by nearest centroid (greedy)
    pairs = []
    used = set()
    for i, a in enumerate(fa):
        ca = centroid(a)
        bj = min((j for j in range(len(fb)) if j not in used),
                 key=lambda j: math.hypot(centroid(fb[j])[0] - ca[0], centroid(fb[j])[1] - ca[1]))
        used.add(bj)
        pairs.append((i, bj))
    A, B = [], []
    for i, j in pairs:
        ra = resample(fa[i], PER)
        rb = resample(fb[j], PER)
        best = None
        for rev in (False, True):
            bb = list(reversed(rb)) if rev else rb
            for sh in range(PER):
                cand = bb[sh:] + bb[:sh]
                err = sum((p[0] - q[0])**2 + (p[1] - q[1])**2 for p, q in zip(ra, cand))
                if best is None or err < best[0]:
                    best = (err, cand)
        A.append(ra); B.append(best[1])
    return A, B


def emit(name, pairA, pairB, out):
    fa = align_pair(load_icon(pairA), load_icon(pairB))
    figs = len(fa[0])
    def fmt(polys):
        vals = []
        for poly in polys:
            for x, y in poly:
                vals.append(f'{x:.2f}')
                vals.append(f'{y:.2f}')
        return ', '.join(vals)
    out.write(f'    public static readonly MorphPair {name} = new(\n')
    out.write(f'        new double[] {{ {fmt(fa[0])} }},\n')
    out.write(f'        new double[] {{ {fmt(fa[1])} }},\n')
    out.write(f'        {figs}, {PER});\n\n')


if __name__ == '__main__':
    base = r'C:\temp'
    out = open(r'D:\Desktop\Codex-Continue\src\MorphIcons.g.cs', 'w', encoding='utf-8')
    out.write('// generated by _tools/genicons.py from lucide (ISC) — do not hand-edit\n')
    out.write('namespace CodexContinue;\n\npublic sealed record MorphPair(double[] From, double[] To, int Figures, int PerFigure);\n\npublic static class MorphIcons\n{\n')
    emit('Settings', rf'{base}\settings.svg', rf'{base}\settings-2.svg', out)
    emit('Log', rf'{base}\scroll-text.svg', rf'{base}\file-text.svg', out)
    out.write('}\n')
    out.close()
    print('done')
