using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace CodexContinue;

// Faithful C# port of morphicons v2.0.2 (ISC) — spring + interpPolar + Procrustes
// pairing semantics transcribed verbatim from the dist source (see
// _research/morphicons-truth.md for the line-by-line ground truth). Stroke-only,
// imperative hover morphs: region/fill/pop/seek machinery is intentionally out.

/// <summary>JS Math.round semantics (half toward +∞) on scaled integers — banker's
/// rounding would shift every quantized coordinate by one ulp vs the original.</summary>
static class Fmt
{
    public static double Q(double v, double scale) => Math.Floor(v * scale + 0.5) / scale;
}
public sealed class MorphSpring
{
    public double X = 1, V = 0, K = 250, C = 24;

    public void Config(double k, double c) { K = k; C = c; }

    /// <summary>Starts (or restarts mid-flight) preserving velocity, clamped ±14.</summary>
    public void Start()
    {
        X = 0;
        if (V > 14) V = 14;
        if (V < -14) V = -14;
    }

    /// <summary>Semi-implicit Euler, 1/240s target substeps clamped 1..16.
    /// Returns true on settle (|1−x| &lt; 0.001 ∧ |v| &lt; 0.02).</summary>
    public bool Step(double dt)
    {
        int steps = Math.Max(1, Math.Min(16, (int)Math.Ceiling(dt / (1.0 / 240))));
        double s = dt / steps;
        for (int i = 0; i < steps; i++)
        {
            double a = K * (1 - X) - C * V;
            V += a * s;
            X += V * s;
        }
        return Math.Abs(1 - X) < 0.001 && Math.Abs(V) < 0.02;
    }
}

// ---------- icon geometry: SVG parse → cubic chains (normalize.ts semantics) ----------
public sealed class CubicPath
{
    public readonly double[] Pts;   // [p0, c1, c2, p1, …] shared endpoints, 2·(3m+1)
    public readonly bool Closed;
    public CubicPath(double[] pts, bool closed) { Pts = pts; Closed = closed; }
}

public sealed class Sampled
{
    public readonly double[] Pts;   // 2N interleaved, N = 64
    public readonly bool Closed;
    public Sampled(double[] pts, bool closed) { Pts = pts; Closed = closed; }
}

/// <summary>One icon = a list of (tag, attrs); our lucide set needs path + circle only.</summary>
public sealed class IconDef
{
    public readonly (string Tag, string Attr)[] Elems;
    public IconDef(params (string, string)[] elems) { Elems = elems; }
}

static class IconGeom
{
    const double Kappa = 4.0 / 3.0 * 0.41421356237309503; // 4/3·tan(π/8) — Math.Tan below for parity
    const double Tau = Math.PI * 2;

    sealed class Builder
    {
        readonly List<double> _pts;
        public double Cx, Cy;
        public Builder(double x0, double y0) { _pts = new List<double> { x0, y0 }; Cx = x0; Cy = y0; }

        public void Cubic(double x1, double y1, double x2, double y2, double x, double y)
        {
            _pts.Add(x1); _pts.Add(y1); _pts.Add(x2); _pts.Add(y2); _pts.Add(x); _pts.Add(y);
            Cx = x; Cy = y;
        }

        // zero-length segments vanish (1e-12 per-axis) — shifts cubic counts if dropped
        public void Line(double x, double y)
        {
            if (Math.Abs(x - Cx) < 1e-12 && Math.Abs(y - Cy) < 1e-12) return;
            Cubic(Cx + (x - Cx) / 3, Cy + (y - Cy) / 3, Cx + 2 * (x - Cx) / 3, Cy + 2 * (y - Cy) / 3, x, y);
        }

        public void Quad(double x1, double y1, double x, double y) =>
            Cubic(Cx + 2.0 / 3 * (x1 - Cx), Cy + 2.0 / 3 * (y1 - Cy), x + 2.0 / 3 * (x1 - x), y + 2.0 / 3 * (y1 - y), x, y);

        public void Arc(double rx0, double ry0, double rotDeg, int large, int sweep, double x, double y)
        {
            double x1 = Cx, y1 = Cy;
            if (Math.Abs(x - x1) < 1e-12 && Math.Abs(y - y1) < 1e-12) return;
            double rx = Math.Abs(rx0), ry = Math.Abs(ry0);
            if (rx < 1e-12 || ry < 1e-12) { Line(x, y); return; }
            double phi = rotDeg * Math.PI / 180;
            double cosP = Math.Cos(phi), sinP = Math.Sin(phi);
            double hx = (x1 - x) / 2, hy = (y1 - y) / 2;
            double x1p = cosP * hx + sinP * hy;
            double y1p = -sinP * hx + cosP * hy;
            double rx2 = rx * rx, ry2 = ry * ry;
            double xp2 = x1p * x1p, yp2 = y1p * y1p;
            double lam = xp2 / rx2 + yp2 / ry2;
            if (lam > 1) { double s = Math.Sqrt(lam); rx *= s; ry *= s; rx2 = rx * rx; ry2 = ry * ry; }
            double rad = (rx2 * ry2 - rx2 * yp2 - ry2 * xp2) / (rx2 * yp2 + ry2 * xp2);
            if (rad < 0) rad = 0;
            double co = (large == sweep ? -1 : 1) * Math.Sqrt(rad);
            double cxp = co * rx * y1p / ry;
            double cyp = -co * ry * x1p / rx;
            double ccx = cosP * cxp - sinP * cyp + (x1 + x) / 2;
            double ccy = sinP * cxp + cosP * cyp + (y1 + y) / 2;
            double th1 = Math.Atan2((y1p - cyp) / ry, (x1p - cxp) / rx);
            double dth = Math.Atan2((-y1p - cyp) / ry, (-x1p - cxp) / rx) - th1;
            if (sweep == 0 && dth > 0) dth -= Tau;
            else if (sweep == 1 && dth < 0) dth += Tau;
            // the −1e-9 keeps exact quarter arcs at one slice (load-bearing)
            int slices = Math.Max(1, (int)Math.Ceiling(Math.Abs(dth) / (Math.PI / 2) - 1e-9));
            double delta = dth / slices;
            double alpha = 4.0 / 3 * Math.Tan(delta / 4);
            double Ex(double t) => ccx + rx * Math.Cos(t) * cosP - ry * Math.Sin(t) * sinP;
            double Ey(double t) => ccy + rx * Math.Cos(t) * sinP + ry * Math.Sin(t) * cosP;
            double Dx(double t) => -rx * Math.Sin(t) * cosP - ry * Math.Cos(t) * sinP;
            double Dy(double t) => -rx * Math.Sin(t) * sinP + ry * Math.Cos(t) * cosP;
            double t0 = th1, p0x = x1, p0y = y1;
            for (int s = 1; s <= slices; s++)
            {
                double t1 = th1 + delta * s;
                double p1x = s == slices ? x : Ex(t1);
                double p1y = s == slices ? y : Ey(t1);
                Cubic(p0x + alpha * Dx(t0), p0y + alpha * Dy(t0), p1x - alpha * Dx(t1), p1y - alpha * Dy(t1), p1x, p1y);
                t0 = t1; p0x = p1x; p0y = p1y;
            }
        }

        public CubicPath? Finish(bool closed)
        {
            if (closed) Line(_pts[0], _pts[1]);
            if (_pts.Count < 8) return null;
            return new CubicPath(_pts.ToArray(), closed);
        }
    }

    /// <summary>Full SVG path parser (MmLlHhVvCcSsQqTtAaZz) lowered to cubic chains.</summary>
    public static List<CubicPath> ParsePath(string d)
    {
        var subs = new List<CubicPath>();
        int i = 0;
        void Skip()
        {
            while (i < d.Length && (d[i] is ' ' or '\t' or '\n' or '\r' or '\f' or ',')) i++;
        }
        bool HasNum()
        {
            Skip();
            return i < d.Length && (char.IsDigit(d[i]) || d[i] is '+' or '-' or '.');
        }
        double Num()
        {
            Skip();
            int st = i;
            if (i < d.Length && (d[i] is '+' or '-')) i++;
            bool any = false;
            while (i < d.Length && char.IsDigit(d[i])) { i++; any = true; }
            if (i < d.Length && d[i] == '.')
            {
                i++;
                while (i < d.Length && char.IsDigit(d[i])) { i++; any = true; }
            }
            if (i < d.Length && (d[i] is 'e' or 'E'))
            {
                // exponent is consumed only when followed by ≥1 digit, else rolled back
                int e = i + 1;
                if (e < d.Length && (d[e] is '+' or '-')) e++;
                if (e < d.Length && char.IsDigit(d[e]))
                {
                    i = e;
                    while (i < d.Length && char.IsDigit(d[i])) i++;
                }
            }
            if (!any) throw new FormatException("morphicons: expected number");
            return double.Parse(d.Substring(st, i - st), System.Globalization.CultureInfo.InvariantCulture);
        }
        int Flag()
        {
            Skip();
            if (i >= d.Length || (d[i] != '0' && d[i] != '1')) throw new FormatException("morphicons: expected arc flag (0|1)");
            return d[i++] - '0';
        }

        List<double> cur = null;   // non-null while a subpath is open for drawing
        var rawSegs = new List<object[]>();
        double cx = 0, cy = 0, sx = 0, sy = 0;
        double px = 0, py = 0;
        string prev = "";
        List<(double x, double y, List<object[]> segs, bool closed)> records = new();

        void Open()
        {
            if (cur == null)
            {
                cur = new List<double>();
                rawSegs = new List<object[]>();
                records.Add((cx, cy, rawSegs, false));
            }
        }

        char cmd = '\0';
        Skip();
        while (i < d.Length)
        {
            Skip();
            if (i >= d.Length) break;
            if (char.IsLetter(d[i]) && d[i] is not ('e' or 'E'))
            {
                cmd = d[i++];
                if (cmd is 'Z' or 'z')
                {
                    // close subpath: closed=true, next draw opens a new one at the start point
                    if (records.Count > 0 && cur != null)
                    {
                        var r = records[^1];
                        records[^1] = (r.x, r.y, r.segs, true);
                    }
                    cur = null;
                    cx = sx; cy = sy;
                    prev = "";
                    continue;
                }
            }
            else if (cmd == '\0') throw new FormatException("morphicons: path must start with M/m");

            bool rel = cmd >= 'a';
            char C = char.ToUpper(cmd);
            switch (C)
            {
                case 'M':
                {
                    double x = Num(), y = Num();
                    if (rel) { x += cx; y += cy; }
                    cx = x; cy = y; sx = x; sy = y;
                    cur = new List<double>();
                    rawSegs = new List<object[]>();
                    records.Add((x, y, rawSegs, false));
                    prev = "";
                    cmd = rel ? 'l' : 'L'; // implicit repetition M→L
                    break;
                }
                case 'L':
                {
                    Open();
                    double x = Num(), y = Num();
                    if (rel) { x += cx; y += cy; }
                    rawSegs.Add(new object[] { 'L', x, y });
                    cx = x; cy = y; prev = "";
                    break;
                }
                case 'H':
                {
                    Open();
                    double x = Num();
                    if (rel) x += cx;
                    rawSegs.Add(new object[] { 'L', x, cy });
                    cx = x; prev = "";
                    break;
                }
                case 'V':
                {
                    Open();
                    double y = Num();
                    if (rel) y += cy;
                    rawSegs.Add(new object[] { 'L', cx, y });
                    cy = y; prev = "";
                    break;
                }
                case 'C':
                case 'S':
                {
                    Open();
                    double x1, y1;
                    if (C == 'C') { x1 = Num(); y1 = Num(); if (rel) { x1 += cx; y1 += cy; } }
                    else { x1 = prev == "C" ? 2 * cx - px : cx; y1 = prev == "C" ? 2 * cy - py : cy; }
                    double x2 = Num(), y2 = Num(), x = Num(), y = Num();
                    if (rel) { x2 += cx; y2 += cy; x += cx; y += cy; }
                    rawSegs.Add(new object[] { 'C', x1, y1, x2, y2, x, y });
                    px = x2; py = y2; cx = x; cy = y; prev = "C";
                    break;
                }
                case 'Q':
                case 'T':
                {
                    Open();
                    double x1, y1;
                    if (C == 'Q') { x1 = Num(); y1 = Num(); if (rel) { x1 += cx; y1 += cy; } }
                    else { x1 = prev == "Q" ? 2 * cx - px : cx; y1 = prev == "Q" ? 2 * cy - py : cy; }
                    double x = Num(), y = Num();
                    if (rel) { x += cx; y += cy; }
                    rawSegs.Add(new object[] { 'Q', x1, y1, x, y });
                    px = x1; py = y1; cx = x; cy = y; prev = "Q";
                    break;
                }
                case 'A':
                {
                    Open();
                    double rx = Num(), ry = Num(), rot = Num();
                    int laf = Flag(), sf = Flag();
                    double x = Num(), y = Num();
                    if (rel) { x += cx; y += cy; }
                    rawSegs.Add(new object[] { 'A', rx, ry, rot, (double)laf, (double)sf, x, y });
                    cx = x; cy = y; prev = "";
                    break;
                }
                default:
                    throw new FormatException($"morphicons: unsupported command \"{cmd}\"");
            }
        }

        var result = new List<CubicPath>();
        foreach (var (x0, y0, segs, closed) in records)
        {
            if (segs.Count == 0) continue;
            var bld = new Builder(x0, y0);
            foreach (var s in segs)
            {
                switch ((char)s[0])
                {
                    case 'L': bld.Line((double)s[1], (double)s[2]); break;
                    case 'C': bld.Cubic((double)s[1], (double)s[2], (double)s[3], (double)s[4], (double)s[5], (double)s[6]); break;
                    case 'Q': bld.Quad((double)s[1], (double)s[2], (double)s[3], (double)s[4]); break;
                    case 'A': bld.Arc((double)s[1], (double)s[2], (double)s[3], (int)(double)s[4], (int)(double)s[5], (double)s[6], (double)s[7]); break;
                }
            }
            var f = bld.Finish(closed);
            if (f != null) result.Add(f);
        }
        return result;
    }

    static CubicPath? Ellipse(double cx, double cy, double rx, double ry)
    {
        if (rx < 1e-12 || ry < 1e-12) return null;
        double kappa = 4.0 / 3 * Math.Tan(Math.PI / 8);
        double kx = kappa * rx, ky = kappa * ry;
        double e = cx + rx, w = cx - rx, s = cy + ry, n = cy - ry;
        var b = new Builder(e, cy);
        b.Cubic(e, cy + ky, cx + kx, s, cx, s);
        b.Cubic(cx - kx, s, w, cy + ky, w, cy);
        b.Cubic(w, cy - ky, cx - kx, n, cx, n);
        b.Cubic(cx + kx, n, e, cy - ky, e, cy);
        return b.Finish(true);
    }

    static double AttrNum(string attr, string key, double fallback = 0)
    {
        foreach (var part in attr.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=');
            if (kv.Length == 2 && kv[0] == key)
                return double.TryParse(kv[1], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var v) && !double.IsNaN(v) ? v : fallback;
        }
        return fallback;
    }

    public static List<CubicPath> ToCubics(IconDef def)
    {
        var res = new List<CubicPath>();
        foreach (var (tag, attr) in def.Elems)
        {
            if (tag == "path") res.AddRange(ParsePath(attr));
            else if (tag == "circle")
            {
                var e = Ellipse(AttrNum(attr, "cx"), AttrNum(attr, "cy"), AttrNum(attr, "r"), AttrNum(attr, "r"));
                if (e != null) res.Add(e);
            }
            else throw new NotSupportedException($"morphicons: unsupported tag <{tag}>");
        }
        return res;
    }
}

// ---------- resample (resample.ts semantics) ----------
static class Resampler
{
    const int N = 64;
    static readonly double[] Gx = { .18343464249564978, .525532409916329, .7966664774136267, .9602898564975363 };
    static readonly double[] Gw = { .362683783378362, .31370664587788727, .22238103445337448, .10122853629037626 };

    static double Speed(double[] p, int k, double t)
    {
        int i = 6 * k;
        double u = 1 - t;
        double c0 = 3 * u * u, c1 = 6 * u * t, c2 = 3 * t * t;
        double dx = c0 * (p[i + 2] - p[i]) + c1 * (p[i + 4] - p[i + 2]) + c2 * (p[i + 6] - p[i + 4]);
        double dy = c0 * (p[i + 3] - p[i + 1]) + c1 * (p[i + 5] - p[i + 3]) + c2 * (p[i + 7] - p[i + 5]);
        return Math.Sqrt(dx * dx + dy * dy);
    }

    static double SegLen(double[] p, int k, double t1 = 1)
    {
        double half = t1 / 2, s = 0;
        for (int j = 0; j < 4; j++) s += Gw[j] * (Speed(p, k, half + half * Gx[j]) + Speed(p, k, half - half * Gx[j]));
        return s * half;
    }

    static double Invert(double[] p, int k, double s, double ls)
    {
        if (s <= 0) return 0;
        if (s >= ls) return 1;
        double lo = 0, hi = 1, t = s / ls;
        for (int it = 0; it < 12; it++)
        {
            double f = SegLen(p, k, t) - s;
            if (Math.Abs(f) < 1e-10 * ls + 1e-14) break;
            if (f > 0) hi = t; else lo = t;
            double sp = Speed(p, k, t);
            double nt = sp > 1e-12 ? t - f / sp : (lo + hi) / 2;
            if (!(nt > lo && nt < hi)) nt = (lo + hi) / 2;
            t = nt;
        }
        return t;
    }

    static void Point(double[] p, int k, double t, double[] o, int off)
    {
        int i = 6 * k;
        double u = 1 - t;
        double b0 = u * u * u, b1 = 3 * u * u * t, b2 = 3 * u * t * t, b3 = t * t * t;
        o[off] = b0 * p[i] + b1 * p[i + 2] + b2 * p[i + 4] + b3 * p[i + 6];
        o[off + 1] = b0 * p[i + 1] + b1 * p[i + 3] + b2 * p[i + 5] + b3 * p[i + 7];
    }

    static double[] Tangent(double[] p, int k, bool atEnd)
    {
        int i = 6 * k;
        int bIdx = atEnd ? i + 6 : i;
        double s = atEnd ? -1 : 1;
        int[] probes = atEnd ? new[] { 4, 2, 0 } : new[] { 2, 4, 6 };
        foreach (int j in probes)
        {
            double dx = s * (p[i + j] - p[bIdx]);
            double dy = s * (p[i + j + 1] - p[bIdx + 1]);
            if (dx * dx + dy * dy > 1e-18) return new[] { dx, dy };
        }
        return null;
    }

    static int[] DetectCorners(double[] p, double threshold)
    {
        int m = (p.Length / 2 - 1) / 3;
        var active = new List<int>();
        for (int k = 0; k < m; k++) if (SegLen(p, k) > 1e-9) active.Add(k);
        if (active.Count == 0) return Array.Empty<int>();
        var corners = new SortedSet<int>();
        void Test(int a, int b)
        {
            var u = Tangent(p, a, true);
            var v = Tangent(p, b, false);
            if (u == null || v == null) return;
            if (Math.Abs(Math.Atan2(u[0] * v[1] - u[1] * v[0], u[0] * v[0] + u[1] * v[1])) > threshold) corners.Add(b);
        }
        for (int j = 0; j + 1 < active.Count; j++) Test(active[j], active[j + 1]);
        if (active.Count > 1) Test(active[^1], active[0]);
        return corners.ToArray();
    }

    public static Sampled Resample(CubicPath path)
    {
        double[] p = path.Pts;
        int m = (p.Length / 2 - 1) / 3;
        double[] outp = new double[2 * N];
        double[] lens = new double[m];
        double total = 0;
        for (int k = 0; k < m; k++) { lens[k] = SegLen(p, k); total += lens[k]; }
        if (m < 1 || total < 1e-12)
        {
            for (int i = 0; i < N; i++) { outp[2 * i] = p[0]; outp[2 * i + 1] = p[1]; }
            return new Sampled(outp, path.Closed);
        }

        int[] cs = DetectCorners(p, Math.PI / 8);
        int[] anchors;
        if (path.Closed) anchors = cs.Length > 0 ? cs : new[] { 0 };
        else anchors = new[] { 0 }.Concat(cs).Append(m).Distinct().OrderBy(a => a).ToArray();

        var runs = new List<(int A, int B)>();
        if (path.Closed)
        {
            for (int j = 0; j < anchors.Length; j++)
            {
                int a = anchors[j];
                int b = j + 1 < anchors.Length ? anchors[j + 1] : anchors[0] + m;
                runs.Add((a, b));
            }
        }
        else for (int j = 0; j + 1 < anchors.Length; j++) runs.Add((anchors[j], anchors[j + 1]));

        int intervals = path.Closed ? N : N - 1;
        if (runs.Count > intervals) throw new InvalidOperationException($"morphicons: N={N} too small ({runs.Count} runs)");

        double[] rl = new double[runs.Count];
        for (int r = 0; r < runs.Count; r++)
        {
            double s = 0;
            for (int k = runs[r].A; k < runs[r].B; k++) s += lens[k % m];
            rl[r] = s;
        }

        double tot = rl.Sum();
        if (tot <= 0) tot = 1;
        double[] ideal = rl.Select(l => intervals * l / tot).ToArray();
        int[] counts = ideal.Select(q => Math.Max(1, (int)Math.Floor(q))).ToArray();
        int R = intervals - counts.Sum();
        if (R > 0)
        {
            var order = ideal.Select((q, idx) => ((long)Math.Round((q - Math.Floor(q)) * 1e9), idx))
                .OrderByDescending(t => t.Item1).ThenBy(t => t.idx).ToArray();
            for (int j = 0; j < R; j++) counts[order[j % counts.Length].idx]++;
        }
        while (R < 0)
        {
            int bi = 0;
            for (int idx = 1; idx < counts.Length; idx++) if (counts[idx] > counts[bi]) bi = idx;
            if (counts[bi] <= 1) break;
            counts[bi]--;
            R++;
        }

        int w = 0;
        for (int r = 0; r < runs.Count; r++)
        {
            int k0 = runs[r].A, k1 = runs[r].B;
            double Lr = rl[r];
            int cnt = counts[r];
            Point(p, k0 % m, 0, outp, 2 * w);
            w++;
            int seg = k0;
            double acc = 0;
            for (int j = 1; j < cnt; j++)
            {
                double target = Lr * j / cnt;
                while (seg < k1 - 1 && acc + lens[seg % m] < target) { acc += lens[seg % m]; seg++; }
                int k = seg % m;
                double ls = lens[k];
                Point(p, k, ls > 1e-12 ? Invert(p, k, target - acc, ls) : 0, outp, 2 * w);
                w++;
            }
        }
        if (!path.Closed)
        {
            int vi = 6 * m;
            outp[2 * w] = p[vi];
            outp[2 * w + 1] = p[vi + 1];
        }
        return new Sampled(outp, path.Closed);
    }
}

// ---------- plan (plan.ts semantics) ----------
public sealed class PlanItem
{
    public double[] A, Ac, Bt, Bo;
    public double Ca0, Ca1, Cb0, Cb1;
    public double Theta, LnSigma, Res;
    public bool Closed;
    public bool HasBlock;
    public double Ox, Oy, Dx, Dy; // block off/drift
}

public sealed class MorphPlan
{
    public PlanItem[] Items;
    public int N;
}

static class Planner
{
    const double LenWeight = 0.35, Lambda = 0.05, GlobalEps = 0.005;
    const int PermMax = 8;
    const double SurjMax = 1e5;

    static (double X, double Y) Centroid(double[] p)
    {
        int n = p.Length / 2;
        double x = 0, y = 0;
        for (int i = 0; i < n; i++) { x += p[2 * i]; y += p[2 * i + 1]; }
        return (x / n, y / n);
    }

    static double PolyLen(double[] p)
    {
        int n = p.Length / 2;
        double L = 0;
        for (int i = 1; i < n; i++)
            L += Math.Sqrt((p[2 * i] - p[2 * i - 2]) * (p[2 * i] - p[2 * i - 2]) + (p[2 * i + 1] - p[2 * i - 1]) * (p[2 * i + 1] - p[2 * i - 1]));
        return L;
    }

    static double[][] CostMatrix(List<double[]> A, List<double[]> B)
    {
        var cbs = B.Select(Centroid).ToArray();
        var lbs = B.Select(PolyLen).ToArray();
        var C = new double[A.Count][];
        for (int i = 0; i < A.Count; i++)
        {
            var (ca0, ca1) = Centroid(A[i]);
            double la = PolyLen(A[i]);
            C[i] = new double[B.Count];
            for (int j = 0; j < B.Count; j++)
                C[i][j] = Math.Sqrt((ca0 - cbs[j].X) * (ca0 - cbs[j].X) + (ca1 - cbs[j].Y) * (ca1 - cbs[j].Y)) + LenWeight * Math.Abs(la - lbs[j]);
        }
        return C;
    }

    static int[] BestPermutation(double[][] C)
    {
        int n = C.Length;
        if (n > PermMax)
        {
            var pairs = new List<(double Cost, int I, int J)>();
            for (int i = 0; i < n; i++) for (int j = 0; j < n; j++) pairs.Add((C[i][j], i, j));
            pairs.Sort((x, y) => x.Cost.CompareTo(y.Cost));
            var outp = Enumerable.Repeat(-1, n).ToArray();
            var used = new bool[n];
            foreach (var (_, i, j) in pairs) if (outp[i] < 0 && !used[j]) { outp[i] = j; used[j] = true; }
            return outp;
        }
        int[] best = Enumerable.Range(0, n).ToArray();
        double bc = double.PositiveInfinity;
        int[] arr = Enumerable.Range(0, n).ToArray();
        void Perm(int k, double acc)
        {
            if (acc >= bc) return;
            if (k == n) { bc = acc; best = (int[])arr.Clone(); return; }
            for (int i = k; i < n; i++)
            {
                (arr[k], arr[i]) = (arr[i], arr[k]);
                Perm(k + 1, acc + C[k][arr[k]]);
                (arr[k], arr[i]) = (arr[i], arr[k]);
            }
        }
        Perm(0, 0);
        return best;
    }

    static int[] BestSurjection(double[][] C)
    {
        int B = C.Length, S = C[0].Length;
        if (B < S) throw new InvalidOperationException("morphicons: no valid surjection (B < S)");
        if (Math.Pow(S, B) > SurjMax)
        {
            var f = new int[B];
            var mult = new int[S];
            for (int i = 0; i < B; i++)
            {
                int m = 0;
                for (int j = 1; j < S; j++) if (C[i][j] < C[i][m]) m = j;
                f[i] = m; mult[m]++;
            }
            for (int s = 0; s < S; s++)
            {
                if (mult[s] > 0) continue;
                int bi = -1; double bestExtra = double.PositiveInfinity;
                for (int i = 0; i < B; i++)
                {
                    if (mult[f[i]] < 2) continue;
                    double extra = C[i][s] - C[i][f[i]];
                    if (extra < bestExtra) { bestExtra = extra; bi = i; }
                }
                if (bi >= 0) { mult[f[bi]]--; f[bi] = s; mult[s]++; }
            }
            return f;
        }
        int[] fmap = Enumerable.Repeat(-1, B).ToArray();
        int[] mult2 = new int[S];
        int covered = 0;
        int[] bestF = null;
        double bc = double.PositiveInfinity;
        void Dfs(int i, double acc)
        {
            if (acc >= bc || S - covered > B - i) return;
            if (i == B) { bc = acc; bestF = (int[])fmap.Clone(); return; }
            for (int s = 0; s < S; s++)
            {
                bool wasCovered = mult2[s] > 0;
                fmap[i] = s; mult2[s]++;
                if (!wasCovered) covered++;
                Dfs(i + 1, acc + C[i][s]);
                mult2[s]--;
                if (!wasCovered) covered--;
                fmap[i] = -1;
            }
        }
        Dfs(0, 0);
        return bestF ?? throw new InvalidOperationException("morphicons: no valid surjection (B < S)");
    }

    static (int Si, int Di)[] PairUp(List<double[]> A, List<double[]> B)
    {
        int p = A.Count, q = B.Count;
        var pairs = new List<(int, int)>();
        if (p == q)
        {
            var perm = BestPermutation(CostMatrix(A, B));
            for (int i = 0; i < p; i++) pairs.Add((i, perm[i]));
        }
        else if (p < q)
        {
            var f = BestSurjection(CostMatrix(B, A));
            for (int j = 0; j < q; j++) pairs.Add((f[j], j));
        }
        else
        {
            var f = BestSurjection(CostMatrix(A, B));
            for (int i = 0; i < p; i++) pairs.Add((i, f[i]));
        }
        return pairs.ToArray();
    }

    static (double Theta, double Sigma, double Res) Procrustes(double[] a, double[] b, (double X, double Y) ca, (double X, double Y) cb)
    {
        int n = a.Length / 2;
        double sxx = 0, sxy = 0, syx = 0, syy = 0, na = 0, nb = 0;
        for (int i = 0; i < n; i++)
        {
            double ax = a[2 * i] - ca.X, ay = a[2 * i + 1] - ca.Y;
            double bx = b[2 * i] - cb.X, by = b[2 * i + 1] - cb.Y;
            sxx += ax * bx; syy += ay * by; sxy += ax * by; syx += ay * bx;
            na += ax * ax + ay * ay; nb += bx * bx + by * by;
        }
        double theta = Math.Atan2(sxy - syx, sxx + syy);
        double num = Math.Cos(theta) * (sxx + syy) + Math.Sin(theta) * (sxy - syx);
        double sigma = na > 1e-12 ? num / na : 1;
        if (!(sigma > 1e-6)) sigma = 1e-6;
        double res2 = Math.Max(0, sigma * sigma * na - 2 * sigma * num + nb);
        double res = nb > 1e-12 ? Math.Sqrt(res2 / nb) : 0;
        return (theta, sigma, res);
    }

    static double[] ReversePts(double[] p)
    {
        int n = p.Length / 2;
        var o = new double[p.Length];
        for (int i = 0; i < n; i++) { o[2 * i] = p[2 * (n - 1 - i)]; o[2 * i + 1] = p[2 * (n - 1 - i) + 1]; }
        return o;
    }

    static double[] RotatePts(double[] p, int off)
    {
        int n = p.Length / 2;
        var o = new double[p.Length];
        for (int i = 0; i < n; i++)
        {
            int s = (i + off) % n;
            o[2 * i] = p[2 * s]; o[2 * i + 1] = p[2 * s + 1];
        }
        return o;
    }

    static PlanItem MakeItem(double[] aPts, double[] bPts, bool aClosed, bool bClosed)
    {
        int n = aPts.Length / 2;
        var ca = Centroid(aPts);
        var cb = Centroid(bPts);
        bool varyA = aClosed && !bClosed;
        double[] baseP = varyA ? aPts : bPts;
        int offs = aClosed || bClosed ? n : 1;
        double bestScore = double.PositiveInfinity;
        double[] best = baseP;
        var sim = (Theta: 0.0, Sigma: 1.0, Res: 0.0);
        for (int dir = 0; dir < 2; dir++)
        {
            double[] walk = dir == 1 ? ReversePts(baseP) : baseP;
            for (int off = 0; off < offs; off++)
            {
                double[] cand = off > 0 ? RotatePts(walk, off) : walk;
                var s = varyA ? Procrustes(cand, bPts, ca, cb) : Procrustes(aPts, cand, ca, cb);
                double score = s.Res + Lambda * Math.Abs(s.Theta) / Math.PI;
                if (score < bestScore) { bestScore = score; best = cand; sim = s; }
            }
        }
        double[] a = varyA ? best : aPts;
        double[] b = varyA ? bPts : best;
        var item = new PlanItem
        {
            A = (double[])a.Clone(),
            Ac = new double[2 * n],
            Bt = new double[2 * n],
            Bo = (double[])b.Clone(),
            Ca0 = ca.X, Ca1 = ca.Y, Cb0 = cb.X, Cb1 = cb.Y,
            Theta = sim.Theta,
            LnSigma = Math.Log(sim.Sigma),
            Res = sim.Res,
            Closed = aClosed && bClosed,
        };
        double cos = Math.Cos(-sim.Theta), sin = Math.Sin(-sim.Theta);
        for (int i = 0; i < n; i++)
        {
            item.Ac[2 * i] = a[2 * i] - ca.X;
            item.Ac[2 * i + 1] = a[2 * i + 1] - ca.Y;
            double bx = b[2 * i] - cb.X, by = b[2 * i + 1] - cb.Y;
            item.Bt[2 * i] = (bx * cos - by * sin) / sim.Sigma;
            item.Bt[2 * i + 1] = (bx * sin + by * cos) / sim.Sigma;
        }
        return item;
    }

    static void ApplyGlobal(PlanItem[] items, int n)
    {
        int T = items.Length * n;
        var ga = new double[2 * T];
        var gb = new double[2 * T];
        for (int k = 0; k < items.Length; k++)
        {
            Array.Copy(items[k].A, 0, ga, 2 * n * k, 2 * n);
            Array.Copy(items[k].Bo, 0, gb, 2 * n * k, 2 * n);
        }
        var gca = Centroid(ga);
        var gcb = Centroid(gb);
        var g = Procrustes(ga, gb, gca, gcb);
        if (g.Res >= GlobalEps) return;
        double cos = Math.Cos(-g.Theta), sin = Math.Sin(-g.Theta);
        double rc = Math.Cos(g.Theta), rs = Math.Sin(g.Theta);
        foreach (var it in items)
        {
            double e2 = 0, nb = 0;
            for (int i = 0; i < n; i++)
            {
                double bx = it.Bo[2 * i] - it.Cb0, by = it.Bo[2 * i + 1] - it.Cb1;
                it.Bt[2 * i] = (bx * cos - by * sin) / g.Sigma;
                it.Bt[2 * i + 1] = (bx * sin + by * cos) / g.Sigma;
                double ex = g.Sigma * (rc * it.Ac[2 * i] - rs * it.Ac[2 * i + 1]) - bx;
                double ey = g.Sigma * (rs * it.Ac[2 * i] + rc * it.Ac[2 * i + 1]) - by;
                e2 += ex * ex + ey * ey; nb += bx * bx + by * by;
            }
            it.Theta = g.Theta;
            it.LnSigma = Math.Log(g.Sigma);
            it.Res = nb > 1e-12 ? Math.Sqrt(e2 / nb) : 0;
            double s1 = Math.Exp(it.LnSigma);
            double c1 = Math.Cos(it.Theta) * s1;
            double n1 = Math.Sin(it.Theta) * s1;
            double ox = it.Ca0 - gca.X, oy = it.Ca1 - gca.Y;
            double rx = ox * c1 - oy * n1 - ox;
            double ry = ox * n1 + oy * c1 - oy;
            it.HasBlock = true;
            it.Ox = ox; it.Oy = oy;
            it.Dx = it.Cb0 - it.Ca0 - rx;
            it.Dy = it.Cb1 - it.Ca1 - ry;
        }
    }

    public static MorphPlan BuildPlan(List<Sampled> src, List<Sampled> dst)
    {
        if (src.Count == 0 || dst.Count == 0) throw new InvalidOperationException("morphicons: icon has no subpaths");
        var A = src.Select(s => s.Pts).ToList();
        var B = dst.Select(s => s.Pts).ToList();
        var pairs = PairUp(A, B);
        var items = pairs.Select(pr => MakeItem(A[pr.Si], B[pr.Di], src[pr.Si].Closed, dst[pr.Di].Closed)).ToArray();
        if (items.Length > 1) ApplyGlobal(items, A[0].Length / 2);
        return new MorphPlan { Items = items, N = A[0].Length / 2 };
    }
}

// ---------- interpPolar (interpolate.ts verbatim; 'half' absent in stroke plans → m = t) ----------
static class Interp
{
    public static void Eval(MorphPlan plan, double t, double[][] outs)
    {
        foreach (var (it, o) in plan.Items.Zip(outs, (a, b) => (a, b)))
        {
            int n = plan.N;
            double s = Math.Exp(it.LnSigma * t);
            double ang = it.Theta * t;
            double cos = Math.Cos(ang) * s;
            double sin = Math.Sin(ang) * s;
            double cx, cy;
            if (it.HasBlock)
            {
                cx = it.Ca0 + it.Dx * t + (it.Ox * cos - it.Oy * sin - it.Ox);
                cy = it.Ca1 + it.Dy * t + (it.Ox * sin + it.Oy * cos - it.Oy);
            }
            else
            {
                cx = it.Ca0 + (it.Cb0 - it.Ca0) * t;
                cy = it.Ca1 + (it.Cb1 - it.Ca1) * t;
            }
            double m = t;
            for (int i = 0; i < n; i++)
            {
                double px = it.Ac[2 * i] + (it.Bt[2 * i] - it.Ac[2 * i]) * m;
                double py = it.Ac[2 * i + 1] + (it.Bt[2 * i + 1] - it.Ac[2 * i + 1]) * m;
                o[2 * i] = cx + px * cos - py * sin;
                o[2 * i + 1] = cy + px * sin + py * cos;
            }
        }
    }
}

// ---------- shared ticker (dom.js semantics: first frame after idle = dt 0, clamp 0.1) ----------
static class MorphTicker
{
    static readonly List<Action<double>> Subs = new();
    static bool _running;
    static double _last = -1;
    static readonly Stopwatch Sw = Stopwatch.StartNew();

    public static void Add(Action<double> tick)
    {
        Subs.Add(tick);
        if (!_running)
        {
            _last = -1;
            System.Windows.Media.CompositionTarget.Rendering += OnRender;
            _running = true;
        }
    }

    public static void Remove(Action<double> tick)
    {
        Subs.Remove(tick);
        if (Subs.Count == 0 && _running)
        {
            System.Windows.Media.CompositionTarget.Rendering -= OnRender;
            _running = false;
            _last = -1;
        }
    }

    static void OnRender(object sender, EventArgs e)
    {
        double now = Sw.Elapsed.TotalSeconds;
        double dt = _last < 0 ? 0 : Math.Min(Math.Max(now - _last, 0), 0.1);
        _last = now;
        foreach (var t in Subs.ToArray()) t(dt);
    }
}

// ---------- per-icon cache + canonical geometry ----------
public static class MorphIcons
{
    // lucide v1.64.0 (ISC) — settings ↔ settings-2, scroll-text ↔ file-text
    public const int Settings = 0, Settings2 = 1, ScrollText = 2, FileText = 3;

    static readonly IconDef[] Defs =
    {
        new IconDef(
            ("path", "M9.671 4.136a2.34 2.34 0 0 1 4.659 0 2.34 2.34 0 0 0 3.319 1.915 2.34 2.34 0 0 1 2.33 4.033 2.34 2.34 0 0 0 0 3.831 2.34 2.34 0 0 1-2.33 4.033 2.34 2.34 0 0 0-3.319 1.915 2.34 2.34 0 0 1-4.659 0 2.34 2.34 0 0 0-3.32-1.915 2.34 2.34 0 0 1-2.33-4.033 2.34 2.34 0 0 0 0-3.831A2.34 2.34 0 0 1 6.35 6.051a2.34 2.34 0 0 0 3.319-1.915"),
            ("circle", "cx=12 cy=12 r=3")),
        new IconDef(
            ("path", "M14 17H5"),
            ("path", "M19 7h-9"),
            ("circle", "cx=17 cy=17 r=3"),
            ("circle", "cx=7 cy=7 r=3")),
        new IconDef(
            ("path", "M15 12h-5"),
            ("path", "M15 8h-5"),
            ("path", "M19 17V5a2 2 0 0 0-2-2H4"),
            ("path", "M8 21h12a2 2 0 0 0 2-2v-1a1 1 0 0 0-1-1H11a1 1 0 0 0-1 1v1a2 2 0 1 1-4 0V5a2 2 0 1 0-4 0v2a1 1 0 0 0 1 1h3")),
        new IconDef(
            ("path", "M6 22a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h8a2.4 2.4 0 0 1 1.704.706l3.588 3.588A2.4 2.4 0 0 1 20 8v12a2 2 0 0 1-2 2z"),
            ("path", "M14 2v5a1 1 0 0 0 1 1h5"),
            ("path", "M10 9H8"),
            ("path", "M16 13H8"),
            ("path", "M16 17H8")),
    };

    static readonly List<Sampled>[] SampledCache = new List<Sampled>[Defs.Length];
    static readonly List<CubicPath>[] CubicCache = new List<CubicPath>[Defs.Length];

    public static List<Sampled> SampledOf(int icon)
    {
        if (SampledCache[icon] == null)
            SampledCache[icon] = IconGeom.ToCubics(Defs[icon]).Select(Resampler.Resample).ToList();
        return SampledCache[icon];
    }

    public static List<CubicPath> CubicsOf(int icon)
    {
        if (CubicCache[icon] == null)
            CubicCache[icon] = IconGeom.ToCubics(Defs[icon]);
        return CubicCache[icon];
    }

    /// <summary>Startup sanity: resample shape, spring settle, plan pairing counts.</summary>
    public static void SelfCheck()
    {
        var g = SampledOf(Settings);
        Debug.Assert(g.Count == 2 && g.All(s => s.Pts.Length == 128), "resample: 2 subpaths × 64 pts");
        var (cx, cy) = (g[1].Pts.Where((_, i) => i % 2 == 0).Average(), g[1].Pts.Where((_, i) => i % 2 == 1).Average());
        Debug.Assert(Math.Abs(cx - 12) < 0.1 && Math.Abs(cy - 12) < 0.1, "circle resample centroid");
        var sp = new MorphSpring { K = 420, C = 30 };
        sp.Start();
        bool settled = false;
        for (int f = 0; f < 240 && !settled; f++) settled = sp.Step(1.0 / 60);
        Debug.Assert(settled && Math.Abs(1 - sp.X) < 0.001, "spring settles");
        var fwd = Planner.BuildPlan(SampledOf(Settings), SampledOf(Settings2));
        Debug.Assert(fwd.Items.Length == 4, "settings→settings-2 = max(2,4) items");
        var rev = Planner.BuildPlan(SampledOf(ScrollText), SampledOf(FileText));
        Debug.Assert(rev.Items.Length == 5, "scroll-text→file-text = max(4,5) items");
    }
}

// ---------- imperative hover driver (dom.js morphTo semantics, stroke-only) ----------
public sealed class MorphRunner
{
    static readonly Dictionary<(int, int), MorphPlan> PlanCache = new();

    readonly System.Windows.Shapes.Path _el;
    readonly int _iconA, _iconB;
    readonly MorphSpring _spring = new() { K = 420, C = 30 }; // snappy = API default
    int _target;
    bool _rest = true, _flying, _drawn;
    MorphPlan _plan;
    double[][] _out;
    bool[] _closed;
    double _t;

    public MorphRunner(System.Windows.Shapes.Path el, int iconA, int iconB)
    {
        _el = el;
        _iconA = iconA;
        _iconB = iconB;
        _target = iconA;
        el.Data = Canonical(iconA);
    }

    public void ToB() => MorphTo(_iconB);
    public void ToA() => MorphTo(_iconA);

    public void MorphTo(int icon, bool animate = true)
    {
        if (!animate)
        {
            Stop();
            _target = icon;
            Settle();
            return;
        }
        if (icon == _target && (_rest || _flying)) return;
        _spring.Config(420, 30);
        Retarget(icon);
        _spring.Start();
        if (!_flying) { _flying = true; MorphTicker.Add(Tick); }
    }

    void Retarget(int icon)
    {
        if (!_rest && _plan != null && _out != null && !_drawn)
        {
            _t = 0;
            Interp.Eval(_plan, 0, _out);
        }
        if (_rest)
        {
            var key = (_target, icon);
            if (!PlanCache.TryGetValue(key, out _plan))
            {
                _plan = Planner.BuildPlan(MorphIcons.SampledOf(_target), MorphIcons.SampledOf(icon));
                PlanCache[key] = _plan;
            }
        }
        else
        {
            _plan = Planner.BuildPlan(Snapshot(), MorphIcons.SampledOf(icon));
        }
        _out = _plan.Items.Select(it => new double[2 * _plan.N]).ToArray();
        _drawn = false;
        _closed = _plan.Items.Select(it => it.Closed).ToArray();
        _target = icon;
        _rest = false;
    }

    List<Sampled> Snapshot()
    {
        if (_rest || _plan == null || _out == null) return MorphIcons.SampledOf(_target);
        var list = new List<Sampled>();
        for (int k = 0; k < _plan.Items.Length; k++)
            list.Add(new Sampled((double[])_out[k].Clone(), _plan.Items[k].Closed));
        return list;
    }

    void Tick(double dt)
    {
        bool settled = _spring.Step(dt);
        Render(_spring.X);
        if (settled) { Stop(); Settle(); }
    }

    void Stop()
    {
        if (!_flying) return;
        _flying = false;
        MorphTicker.Remove(Tick);
    }

    void Settle()
    {
        _rest = true;
        _plan = null; _out = null; _closed = null;
        _t = 1;
        _spring.X = 1;
        _spring.V = 0;
        _el.Data = Canonical(_target);
    }

    void Render(double tt)
    {
        var p = _plan; var o = _out;
        if (p == null || o == null) return;
        _t = tt;
        Interp.Eval(p, tt, o);
        _drawn = true;
        var g = new PathGeometry();
        for (int k = 0; k < p.Items.Length; k++)
        {
            var buf = o[k];
            var fig = new PathFigure
            {
                StartPoint = new Point(Fmt.Q(buf[0], 100), Fmt.Q(buf[1], 100)),
                IsClosed = _closed[k],
                IsFilled = false,
            };
            var seg = new PolyLineSegment();
            for (int i = 1; i < p.N; i++)
                seg.Points.Add(new Point(Fmt.Q(buf[2 * i], 100), Fmt.Q(buf[2 * i + 1], 100)));
            fig.Segments.Add(seg);
            g.Figures.Add(fig);
        }
        _el.Data = g;
    }

    static Geometry Canonical(int icon)
    {
        var g = new PathGeometry();
        foreach (var sub in MorphIcons.CubicsOf(icon))
        {
            var fig = new PathFigure
            {
                StartPoint = new Point(Fmt.Q(sub.Pts[0], 1e4), Fmt.Q(sub.Pts[1], 1e4)),
                IsClosed = sub.Closed,
                IsFilled = false,
            };
            var seg = new PolyBezierSegment();
            for (int i = 1; i < sub.Pts.Length / 2 - 1; i += 3)
            {
                seg.Points.Add(new Point(Fmt.Q(sub.Pts[2 * i], 1e4), Fmt.Q(sub.Pts[2 * i + 1], 1e4)));
                seg.Points.Add(new Point(Fmt.Q(sub.Pts[2 * i + 2], 1e4), Fmt.Q(sub.Pts[2 * i + 3], 1e4)));
                seg.Points.Add(new Point(Fmt.Q(sub.Pts[2 * i + 4], 1e4), Fmt.Q(sub.Pts[2 * i + 5], 1e4)));
            }
            fig.Segments.Add(seg);
            g.Figures.Add(fig);
        }
        return g;
    }
}
