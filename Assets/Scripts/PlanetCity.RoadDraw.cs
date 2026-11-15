using System.Collections.Generic;
using UnityEngine;

// Wegen tekenen: maten per stijl, stoeprand, stoep, middenberm, borstwering, palen en de wegknoop (RoadNode).
public static partial class PlanetCity
{
    // ---------- wegen ----------
    // Afmetingen per stijl: halve breedte van het asfalt en van asfalt + stoep (m). 0 = gewone weg, 1 = brede weg, 2 = laan (met middenberm)
    static float hwA = 2.5f, hwS = 4.5f; static byte styleNow; static bool bridgeNow;
    static void Dims(byte style, out float a, out float s) { if (style == 1) { a = 4.2f; s = 6.2f; } else if (style == 2) { a = 4.4f; s = 6.0f; } else { a = 2.5f; s = 4.5f; } }
    // Profiel zoals in de webversie: asfalt net boven de grond, een schuine stoeprand van 30 cm en de stoep 25 cm hoog,
    // met een rand die tot onder de grond doorloopt. De kern van een kruising ligt 5 mm lager dan de armen.
    const float AY = 0.10f, ACY = 0.095f, SY = 0.25f, KW = 0.3f, LY = 0.11f, SKB = -0.35f;
    // Zijkant van stoep en pad tot onder het maaiveld: dieper waar het vak in een kom ligt of de weg helt
    static float skb = SKB;
    static float SkirtDepth(int i)
    {
        if (Underpass(i)) return SKB;   // een brugdek: geen rok tot in de sleuf
        float g = G(i), s = 0f; foreach (int j in Partners(i)) if (IsWalk(j) && !UnderLink(i, j)) s = Mathf.Max(s, Mathf.Abs(LevelFor(j, i) - g) * 0.5f);
        return SKB - Mathf.Max(0f, g - MinCorner(i)) - s;
    }
    // schuine stoeprand van de rand van het asfalt (a0-a1, hoogte ya) naar de stoep (b0-b1, hoogte SY)
    static void Kerb(MeshBuilder m, Frame fr, Vector2 a0, Vector2 a1, Vector2 b0, Vector2 b1, float ya)
    {
        Vector2 o = (b0 + b1 - a0 - a1) * 0.5f;
        m.Quad(fr.P(a0.x, ya, a0.y), fr.P(a1.x, ya, a1.y), fr.P(b1.x, SY, b1.y), fr.P(b0.x, SY, b0.y), fr.D(-o.x, KW, -o.y));
    }
    // stuk stoep tussen een binnenlijn (i0-i1) en een buitenlijn (o0-o1), met een zijkant omlaag langs de buitenlijn
    static void Walk(MeshBuilder m, Frame fr, Vector2 i0, Vector2 i1, Vector2 o0, Vector2 o1)
    {
        m.Quad(fr.P(i0.x, SY, i0.y), fr.P(i1.x, SY, i1.y), fr.P(o1.x, SY, o1.y), fr.P(o0.x, SY, o0.y), fr.u);
        Vector2 w = (o0 + o1 - i0 - i1) * 0.5f;
        m.Quad(fr.P(o0.x, skb, o0.y), fr.P(o1.x, skb, o1.y), fr.P(o1.x, SY, o1.y), fr.P(o0.x, SY, o0.y), fr.D(w.x, 0, w.y));
    }
    public static float LaneOffset(int i, int lane = -1) { byte s = StyleOf(i); return s == 1 ? (lane < 0 ? 2.5f : lane == 0 ? 1.05f : 3.15f) : s == 2 ? 2.9f : 1.35f; }   // rijbaan van de auto, rechts van het midden; een brede weg heeft twee stroken per richting (0 links, 1 rechts)
    public static float WalkOffset(int i) { byte s = StyleOf(i); return s == 0 ? 3.5f : 5.2f; }                       // looplijn van voetgangers op de stoep
    static Vector2 PoleAt(Vector2 e, Vector2 d) { Vector2 r = new Vector2(d.y, -d.x); return e - d * 0.8f + r * (hwS - 0.6f); }   // verkeerslicht: rechts op de stoep van de arm, net voor de stopstreep (buiten de kruising, anders staat hij bij een scherpe hoek op het asfalt van een andere arm)
    // wereldpositie (bovenkant) van het verkeerslicht bij vak i voor verkeer dat uit buur p komt
    public static bool PolePos(int i, int p, out Vector3 pos)
    {
        pos = Vector3.zero; if (!IsRoad(i)) return false;
        var fr = new Frame(i).At(NodeDir(i)); float a, s; Dims(ArmStyle(i, p), out a, out s); float old = hwS; hwS = s;
        Vector3 d3 = NodeDir(p) * Planet.R - fr.o; Vector2 q = new Vector2(Vector3.Dot(d3, fr.e), Vector3.Dot(d3, fr.n)); q = q.normalized * NodeTrim(i, p, RoadNb(i).Count); Vector2 d = (-q).normalized;   // armen van een kruising lopen naar het hart; de arm begint waar de knoop ophoudt (zoals RoadNode)
        Vector2 pp = PoleAt(q, d); hwS = old; pos = fr.P(pp.x, 3.75f, pp.y); return true;
    }

    // pijl op het asfalt die langs d wijst (eenrichtingsverkeer)
    static void Arrow(System.Func<string, MeshBuilder> M, Frame fr, Vector2 c, Vector2 d, float len)
    {
        d = d.normalized; Vector2 r = new Vector2(d.y, -d.x); const float y = LY + 0.001f;
        Vector2 b0 = c - d * len * 0.5f, b1 = c + d * len * 0.1f, tip = c + d * len * 0.5f;
        M("lijn").Quad(fr.P(b0.x - r.x * 0.14f, y, b0.y - r.y * 0.14f), fr.P(b0.x + r.x * 0.14f, y, b0.y + r.y * 0.14f), fr.P(b1.x + r.x * 0.14f, y, b1.y + r.y * 0.14f), fr.P(b1.x - r.x * 0.14f, y, b1.y - r.y * 0.14f), fr.u);
        M("lijn").Tri(fr.P(b1.x - r.x * 0.55f, y, b1.y - r.y * 0.55f), fr.P(b1.x + r.x * 0.55f, y, b1.y + r.y * 0.55f), fr.P(tip.x, y, tip.y), fr.u);
    }
    // middenberm van een laan met bomen, van a naar b
    static void Median(System.Func<string, MeshBuilder> M, Frame fr, Vector2 a, Vector2 b)
    {
        Vector2 d = b - a; float len = d.magnitude; if (len < 0.05f) return; d /= len; Vector2 r = new Vector2(d.y, -d.x); const float w = 1.1f, wr = w + 0.2f;
        M("rand").Quad(fr.P(a.x - r.x * wr, 0.2f, a.y - r.y * wr), fr.P(a.x + r.x * wr, 0.2f, a.y + r.y * wr), fr.P(b.x + r.x * wr, 0.2f, b.y + r.y * wr), fr.P(b.x - r.x * wr, 0.2f, b.y - r.y * wr), fr.u);
        M("gras").Quad(fr.P(a.x - r.x * w, 0.23f, a.y - r.y * w), fr.P(a.x + r.x * w, 0.23f, a.y + r.y * w), fr.P(b.x + r.x * w, 0.23f, b.y + r.y * w), fr.P(b.x - r.x * w, 0.23f, b.y - r.y * w), fr.u);
        foreach (int sg in new[] { -1, 1 }) { Vector2 pa = a + r * (sg * wr), pb = b + r * (sg * wr); M("rand").Quad(fr.P(pa.x, AY, pa.y), fr.P(pb.x, AY, pb.y), fr.P(pb.x, 0.2f, pb.y), fr.P(pa.x, 0.2f, pa.y), fr.D(r.x * sg, 0, r.y * sg)); }   // de zijkanten
        // de kop rond: een halve cirkel om b, met de rand, het gras en de zijkant
        const int nk = 10;
        for (int k = 0; k < nk; k++)
        {
            float t0 = Mathf.PI * k / nk, t1 = Mathf.PI * (k + 1) / nk;
            Vector2 u0 = r * Mathf.Cos(t0) + d * Mathf.Sin(t0), u1 = r * Mathf.Cos(t1) + d * Mathf.Sin(t1);
            Vector2 o0 = b + u0 * wr, o1 = b + u1 * wr, g0 = b + u0 * w, g1 = b + u1 * w;
            M("rand").Quad(fr.P(g0.x, 0.2f, g0.y), fr.P(g1.x, 0.2f, g1.y), fr.P(o1.x, 0.2f, o1.y), fr.P(o0.x, 0.2f, o0.y), fr.u);
            M("gras").Tri(fr.P(b.x, 0.23f, b.y), fr.P(g0.x, 0.23f, g0.y), fr.P(g1.x, 0.23f, g1.y), fr.u);
            Vector2 um = (u0 + u1) * 0.5f; M("rand").Quad(fr.P(o0.x, AY, o0.y), fr.P(o1.x, AY, o1.y), fr.P(o1.x, 0.2f, o1.y), fr.P(o0.x, 0.2f, o0.y), fr.D(um.x, 0, um.y));
        }
        for (float s = Mathf.Min(3.5f, len * 0.5f); len >= 2f && s < len - 1.5f; s += 7f)
        {
            Vector2 p = a + d * s; Cone(M("stam"), fr, p.x, p.y, 0.2f, 0.2f, 2.3f, 0.15f); Cone(M("blad"), fr, p.x, p.y, 1.5f, 1.9f, 4.8f, 0f);
        }
    }
    // balustrade langs een brug van a naar b: een muurtje van 24 cm dik en 1,05 m hoog op de lijn a-b, met een bovenkant en
    // kopse kanten (geen wand zonder dikte)
    static void Parapet(System.Func<string, MeshBuilder> M, Frame fr, Vector2 a, Vector2 b, Vector2 outward)
    {
        Vector2 d = b - a; if (d.sqrMagnitude < 1e-4f) return; d.Normalize();
        Vector2 o = outward.sqrMagnitude > 1e-6f ? outward.normalized * 0.12f : new Vector2(d.y, -d.x) * 0.12f;
        const float top = 1.05f;
        Vector2 ao = a + o, bo = b + o, ai = a - o, bi = b - o;
        Vector3 Pt(Vector2 q, float y) => fr.P(q.x, y, q.y);
        M("rand").Quad(Pt(ao, SY), Pt(bo, SY), Pt(bo, top), Pt(ao, top), fr.D(o.x, 0, o.y));       // buitenkant
        M("rand").Quad(Pt(ai, SY), Pt(bi, SY), Pt(bi, top), Pt(ai, top), fr.D(-o.x, 0, -o.y));     // binnenkant
        M("rand").Quad(Pt(ai, top), Pt(bi, top), Pt(bo, top), Pt(ao, top), fr.u);                   // bovenkant
        M("rand").Quad(Pt(ai, SY), Pt(ao, SY), Pt(ao, top), Pt(ai, top), fr.D(-d.x, 0, -d.y));     // kopse kant bij a
        M("rand").Quad(Pt(bi, SY), Pt(bo, SY), Pt(bo, top), Pt(bi, top), fr.D(d.x, 0, d.y));       // kopse kant bij b
    }
    // de stijlen en brug die bij het hele wegvak horen: pijler onder een brug
    static void BridgePillar(System.Func<string, MeshBuilder> M, Frame fr, int i)
    {
        if (Underpass(i)) return;   // het pad loopt eronderdoor
        float depth = Under.TryGetValue(i, out var u) && u == "kuil" ? -3f : -1.5f;
        Cone(M("steen"), fr, 0f, 0f, 1.1f, depth, -0.3f, 1.1f);
    }

    // Een knoop van het wegennet (kruising, knik, doodlopend eind of los vak). ends: per arm het punt op de as waar het lint
    // van die arm ophoudt (NodeTrim van het hart, in de richting van de buur ids[a]); de armen lopen naar het hart, zoals in de
    // webversie alle wegen door de knooppunten gaan. Binnen die afstand tekent de knoop alles: asfalt, stoepranden en stoep.
    static void RoadNode(Dictionary<string, MeshBuilder> mb, Frame fr, int i, List<Vector2> ends, List<int> ids)
    {
        System.Func<string, MeshBuilder> M = k => { if (!mb.TryGetValue(k, out var x)) { x = new MeshBuilder(); mb[k] = x; } return x; };
        styleNow = StyleOf(i); Dims(styleNow, out hwA, out hwS); bridgeNow = Under.ContainsKey(i) && !Underpass(i);   // een onderdoorgang heeft geen balustrade: de portalen liggen gelijk met het maaiveld
        skb = SkirtDepth(i);
        int na = ends.Count; var dirs = new List<Vector2>(); for (int a = 0; a < na; a++) dirs.Add((-ends[a]).normalized);
        if (na == 0)   // los wegvak: een rond pleintje asfalt met stoeprand en stoep eromheen
        {
            float R0 = hwA + 2.1f; var c0 = Circle(Vector2.zero, R0, 0f, Mathf.PI * 2f, 40); var c1 = Circle(Vector2.zero, R0 + KW, 0f, Mathf.PI * 2f, 40); var c2 = Circle(Vector2.zero, R0 + hwS - hwA, 0f, Mathf.PI * 2f, 40);
            for (int k = 0; k < 40; k++) { M("asfalt").Tri(fr.P(0, AY, 0), fr.P(c0[k].x, AY, c0[k].y), fr.P(c0[k + 1].x, AY, c0[k + 1].y), fr.u); Kerb(M("rand"), fr, c0[k], c0[k + 1], c1[k], c1[k + 1], AY); Walk(M("stoep"), fr, c1[k], c1[k + 1], c2[k], c2[k + 1]); }
            return;
        }
        // elke arm zijn eigen stijl en breedte (die van de lijn erachter): een brede weg die op een gewone aansluit, versmalt
        // in de stoephoeken van de knoop
        var stA = new byte[na]; var wA = new float[na]; var wS = new float[na];
        for (int a = 0; a < na; a++) { stA[a] = ids != null && a < ids.Count ? ArmStyle(i, ids[a]) : styleNow; Dims(stA[a], out wA[a], out wS[a]); }
        System.Action<int> Use = a => { hwA = wA[a]; hwS = wS[a]; };   // de maten van arm a voor SideWalk, PoleAt en de rest
        // laag: 0 rand van het asfalt, 1 bovenkant stoeprand, 2 buitenkant stoep, 3 plek van de lantaarn
        System.Func<int, int, float> Lat = (a, layer) => layer == 0 ? wA[a] : layer == 1 ? wA[a] + KW : layer == 2 ? wS[a] : wS[a] - 0.6f;
        // Kruising, doodlopend eind of scherpe knik, zoals in de webversie: alle armen samen zijn één vlak asfalt. Tussen twee
        // opeenvolgende armen (tegen de klok in) loopt de stoep door met een stoephoek: een kwadratische boog die aan beide
        // stoepranden raakt, met hun snijpunt als trekpunt; recht als de randen (bijna) in elkaars verlengde liggen; rond om
        // het hart als de hoek groter is dan 200° (buitenkant van een knik) of bij een doodlopend eind (een ronde kop).
        bool signals = Lights && na >= 3;
        var order = new List<int>(); for (int a = 0; a < na; a++) order.Add(a);
        System.Func<int, float> angOf = a => Mathf.Atan2(-dirs[a].y, -dirs[a].x);
        order.Sort((x, y) => angOf(x).CompareTo(angOf(y)));
        // punt op arm a: lat opzij (+ = naar de volgende arm, tegen de klok in), s de arm in vanaf de vakrand
        System.Func<int, float, float, Vector2> P = (a, lat, s) =>
        {
            Vector2 e = ends[a], d = dirs[a], r = new Vector2(d.y, -d.x), en = e.normalized, p0 = e + r * lat; float den = Vector2.Dot(d, en);
            return p0 + d * ((Mathf.Abs(den) < 1e-4f ? 0f : Vector2.Dot(e - p0, en) / den) + s);
        };
        // per hoek (van arm order[c] naar arm order[c+1]): soort (0 boog, 1 recht, 2 rond), waar de stoepranden ophouden, middelpunt
        // en straal van een ronde hoek (rb + lat: de cirkel die op afstand lat aan de arm raakt)
        bool taper = false; var nArc = new int[na]; var kind = new int[na]; var sA = new float[na]; var sB = new float[na]; var cc = new Vector2[na]; var rb = new float[na];
        float maxPerp = 0f; for (int a = 0; a < na; a++) maxPerp = Mathf.Max(maxPerp, Mathf.Abs(Vector2.Dot(ends[a], new Vector2(dirs[a].y, -dirs[a].x))));
        System.Func<int, float, Vector2, float, float> hitS = (a, lat, c, rad) =>   // waar de lijn op afstand lat van arm a de cirkel (c, rad) voor het eerst raakt
        {
            Vector2 B = P(a, lat, 0f) - c; float bd = Vector2.Dot(B, dirs[a]), disc = bd * bd - B.sqrMagnitude + rad * rad;
            return Mathf.Max(0.3f, -bd - (disc > 0f ? Mathf.Sqrt(disc) : 0f));
        };
        for (int c = 0; c < na; c++)
        {
            int a = order[c], b = order[(c + 1) % na];
            float phi = na == 1 ? Mathf.PI * 2f : Mathf.Repeat(angOf(b) - angOf(a), Mathf.PI * 2f);
            if (na == 1 || phi > Mathf.PI * 200f / 180f)
            {
                kind[c] = 2;
                if (na == 1) { cc[c] = P(a, 0f, Vector2.Dot(-P(a, 0f, 0f), dirs[a])); rb[c] = stA[a] == 0 ? 2.1f : 0.3f; }   // ronde kop op de as, waar de weg het dichtst bij het hart komt
                else { cc[c] = Vector2.zero; rb[c] = maxPerp; }   // buitenkant van een knik: een cirkel om het hart die precies aan beide stoepranden raakt (vecBend)
                // de rechte stoep houdt op waar de eerste laag de cirkel raakt (de buitenkant); elke laag loopt daarvandaan recht door tot
                // zijn eigen cirkel (anders blijft er een driehoekje gras tussen de rechte en de ronde stoep)
                sA[c] = Mathf.Min(hitS(a, wA[a], cc[c], rb[c] + wA[a]), hitS(a, wS[a], cc[c], rb[c] + wS[a])); sB[c] = Mathf.Min(hitS(b, -wA[b], cc[c], rb[c] + wA[b]), hitS(b, -wS[b], cc[c], rb[c] + wS[b]));
                Vector2 h0 = P(a, wA[a], hitS(a, wA[a], cc[c], rb[c] + wA[a])) - cc[c], h1 = P(b, -wA[b], hitS(b, -wA[b], cc[c], rb[c] + wA[b])) - cc[c];
                float g0 = Mathf.Atan2(h0.y, h0.x), g1 = Mathf.Atan2(h1.y, h1.x); if (g1 <= g0 + 0.01f) g1 += Mathf.PI * 2f; nArc[c] = Mathf.Max(6, Mathf.CeilToInt((g1 - g0) / 0.15f));   // alle lagen evenveel punten
                continue;
            }
            Vector2 pa = P(a, wA[a], 0f), pb = P(b, -wA[b], 0f); float cr = Cross(dirs[a], dirs[b]);
            float t = Mathf.Abs(cr) > 0.05f ? Cross(pb - pa, dirs[b]) / cr : -1f, u = Mathf.Abs(cr) > 0.05f ? Cross(pb - pa, dirs[a]) / cr : -1f;   // snijpunt van de twee stoepranden
            if (t > 0.4f && u > 0.4f && t < 30f && u < 30f) { kind[c] = 0; sA[c] = Mathf.Max(0.4f, t - 3f); sB[c] = Mathf.Max(0.4f, u - 3f); }
            else { kind[c] = 1; sA[c] = Mathf.Max(0.4f, Vector2.Dot(-pa, dirs[a])); sB[c] = Mathf.Max(0.4f, Vector2.Dot(-pb, dirs[b])); }
            if (kind[c] == 1 && Mathf.Abs(wA[a] - wA[b]) + Mathf.Abs(wS[a] - wS[b]) > 0.01f) { sA[c] = sB[c] = 0.3f; taper = true; }   // andere breedte in elkaars verlengde: de rand loopt schuin over het hele knoopvak
        }
        // de stoephoek van hoek c in een laag (zie Lat); bij armen van verschillende breedte loopt de hoek van de ene maat naar de andere
        System.Func<int, int, Vector2[]> corner = (c, layer) =>
        {
            int a = order[c], b = order[(c + 1) % na]; float la = Lat(a, layer), lb = Lat(b, layer);
            if (kind[c] == 2)
            {
                float ra = rb[c] + la, rq = rb[c] + lb;
                Vector2 p = P(a, la, hitS(a, la, cc[c], ra)), q = P(b, -lb, hitS(b, -lb, cc[c], rq));
                float a0 = Mathf.Atan2(p.y - cc[c].y, p.x - cc[c].x), a1 = Mathf.Atan2(q.y - cc[c].y, q.x - cc[c].x); if (a1 <= a0 + 0.01f) a1 += Mathf.PI * 2f;
                int n = nArc[c]; var arc = new Vector2[n + 3];
                for (int k = 0; k <= n; k++) { float f = k / (float)n, an = Mathf.Lerp(a0, a1, f); arc[k + 1] = cc[c] + new Vector2(Mathf.Cos(an), Mathf.Sin(an)) * Mathf.Lerp(ra, rq, f); }
                arc[1] = p; arc[n + 1] = q; arc[0] = P(a, la, sA[c]); arc[n + 2] = P(b, -lb, sB[c]); return arc;   // eerst recht door vanaf het eind van de rechte stoep
            }
            Vector2 P0 = P(a, la, sA[c]), Q0 = P(b, -lb, sB[c]); var pts = new Vector2[11];
            float crs = Cross(dirs[a], dirs[b]), tt = Mathf.Abs(crs) > 0.05f ? Cross(Q0 - P0, dirs[b]) / crs : -1f, uu = Mathf.Abs(crs) > 0.05f ? Cross(Q0 - P0, dirs[a]) / crs : -1f;
            bool curve = kind[c] == 0 && tt > 0.2f && uu > 0.2f && tt < 25f && uu < 25f, crossed = kind[c] == 0 && Mathf.Abs(crs) > 0.05f && tt < 0f && uu < 0f; Vector2 X = P0 + dirs[a] * tt;
            // crossed: de twee randen kruisen elkaar al vóór de hoek (scherpe hoek, buitenkant stoep): de hoek krimpt tot dat kruispunt
            for (int k = 0; k <= 10; k++) { float f = k / 10f, g = 1f - f; pts[k] = curve ? P0 * (g * g) + X * (2f * g * f) + Q0 * (f * f) : crossed ? X : Vector2.Lerp(P0, Q0, f); }
            return pts;
        };
        // asfalt: één waaier vanuit het hart (bij een doodlopend eind vanuit het midden van de kop) langs de hele omtrek
        Vector2 hub = na == 1 ? cc[0] : Vector2.zero; var rim = new List<Vector2>();
        for (int c = 0; c < na; c++) { int a = order[c]; rim.Add(P(a, -wA[a], 0f)); rim.Add(P(a, wA[a], 0f)); rim.AddRange(corner(c, 0)); }
        // in stukjes van hooguit 3 m: een lange platte driehoek zakt op de bol onder het oppervlak (bij een spitse hoek is de knoop tot 22 m lang)
        for (int k = 0; k < rim.Count; k++)
        {
            Vector2 p = rim[k], q = rim[(k + 1) % rim.Count]; int nt = Mathf.Max(1, Mathf.CeilToInt((q - p).magnitude / 3f));
            for (int t = 0; t < nt; t++)
            {
                Vector2 p1 = Vector2.Lerp(p, q, t / (float)nt), q1 = Vector2.Lerp(p, q, (t + 1) / (float)nt);
                int nr = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max((p1 - hub).magnitude, (q1 - hub).magnitude) / 3f));
                for (int r = 0; r < nr; r++)
                {
                    float f0 = r / (float)nr, f1 = (r + 1) / (float)nr; Vector2 a0 = Vector2.Lerp(hub, p1, f0), b0 = Vector2.Lerp(hub, q1, f0), a1 = Vector2.Lerp(hub, p1, f1), b1 = Vector2.Lerp(hub, q1, f1);
                    if (r == 0) M("asfalt").Tri(fr.P(hub.x, AY, hub.y), fr.P(a1.x, AY, a1.y), fr.P(b1.x, AY, b1.y), fr.u);
                    else M("asfalt").Quad(fr.P(a0.x, AY, a0.y), fr.P(a1.x, AY, a1.y), fr.P(b1.x, AY, b1.y), fr.P(b0.x, AY, b0.y), fr.u);
                }
            }
        }
        // stoeprand en stoep: langs de zijkanten van elke arm tot waar de hoek begint, dan om de hoek heen
        float bestPhi = -1f; Vector2 lampAt = Vector2.zero;
        for (int c = 0; c < na; c++)
        {
            int a = order[c], b = order[(c + 1) % na];
            Use(a); SideWalk(M, fr, P, a, 1f, sA[c]); Use(b); SideWalk(M, fr, P, b, -1f, sB[c]);
            var k0 = corner(c, 0); var k1 = corner(c, 1); var k2 = corner(c, 2);
            int nk = Mathf.Min(k0.Length, Mathf.Min(k1.Length, k2.Length));
            for (int k = 0; k + 1 < nk; k++) { Kerb(M("rand"), fr, k0[k], k0[k + 1], k1[k], k1[k + 1], AY); Walk(M("stoep"), fr, k1[k], k1[k + 1], k2[k], k2[k + 1]); }
            float phi = na == 1 ? 7f : Mathf.Repeat(angOf(b) - angOf(a), Mathf.PI * 2f);
            if (phi > bestPhi) { bestPhi = phi; var lc = corner(c, 3); lampAt = lc[lc.Length / 2]; }
        }
        if (na != 2 && PropBegin(PropPaal, fr.P(lampAt.x, SY, lampAt.y), fr.u, 0.14f, 5.35f)) { Cone(M("paal"), fr, lampAt.x, lampAt.y, 0.08f, SY, 5.2f, 0.06f); Cone(M("lamp"), fr, lampAt.x, lampAt.y, 0.28f, 4.9f, 5.35f, 0.2f); PropEnd(); }   // lantaarn op de breedste hoek (kruising of kop)
        // per arm: markering, pijlen, middenberm, zebrapaden, verkeerslichten en brugleuningen
        for (int c = 0; c < na; c++)
        {
            int a = order[c], cp = (c + na - 1) % na; Vector2 e = ends[a], d = dirs[a], r = new Vector2(d.y, -d.x); byte st = stA[a]; Use(a);
            float L = Mathf.Min(sA[c], sB[cp]);   // zo ver loopt de arm recht door (tot de eerste stoephoek)
            if (na == 1) L = Vector2.Dot(cc[0] - P(a, 0f, 0f), d);
            if (na == 2 && taper) L = Vector2.Dot(-e, d);   // overgang: de markering loopt door tot het hart
            int flow = Flow(i, ids[a]);   // +1: verkeer verlaat dit vak langs deze arm, -1: komt erlangs binnen
            if (flow != 0) Arrow(M, fr, e + d * Mathf.Min(L * 0.5f, 4.5f), flow > 0 ? -d : d, 2.2f);
            else if (na <= 2 && st != 2) for (float s0 = 0.6f; s0 + 1.2f < L - 0.5f; s0 += 2.6f) { Vector2 a0 = e + d * s0, a1 = e + d * (s0 + 1.4f); M("lijn").Quad(fr.P(a0.x - r.x * 0.07f, LY, a0.y - r.y * 0.07f), fr.P(a0.x + r.x * 0.07f, LY, a0.y + r.y * 0.07f), fr.P(a1.x + r.x * 0.07f, LY, a1.y + r.y * 0.07f), fr.P(a1.x - r.x * 0.07f, LY, a1.y - r.y * 0.07f), fr.u); }
            if (st == 1 && na <= 2) for (int ln = -1; ln <= 1; ln += 2) for (float s0 = 0.6f; s0 + 1.2f < L - 0.5f; s0 += 2.6f) { Vector2 off = r * (ln * hwA * 0.5f); Vector2 a0 = e + d * s0 + off, a1 = e + d * (s0 + 1.4f) + off; M("lijn").Quad(fr.P(a0.x - r.x * 0.05f, LY, a0.y - r.y * 0.05f), fr.P(a0.x + r.x * 0.05f, LY, a0.y + r.y * 0.05f), fr.P(a1.x + r.x * 0.05f, LY, a1.y + r.y * 0.05f), fr.P(a1.x - r.x * 0.05f, LY, a1.y - r.y * 0.05f), fr.u); }
            bool medEnd = na == 2 && stA[order[(c + 1) % na]] != 2;   // een laan die in de knoop overgaat in een smallere weg: de berm houdt daar op
            if (st == 2 && (na == 1 || na >= 3 || medEnd)) Median(M, fr, e, e + d * Mathf.Max(0.2f, na >= 3 ? Mathf.Min(L - 1f, 1.2f) : L - 1f));   // op een kruising houdt de berm op bij de monding: daar slaat het verkeer af
            // zebrapaden, stopstrepen en haaientanden staan op de arm buiten de knoop (ChainMesh: daar is de weg recht, ook bij scherpe hoeken)
            if (signals && CanDrive(ids[a], i)) { Vector2 pp = PoleAt(e, d); if (PropBegin(PropPaal, fr.P(pp.x, SY, pp.y), fr.u, 0.14f, 3.95f)) { Cone(M("paal"), fr, pp.x, pp.y, 0.08f, SY, 3.9f, 0.06f); Cone(M("paal"), fr, pp.x, pp.y, 0.24f, 3.3f, 3.95f, 0.24f); PropEnd(); } }
            if (bridgeNow) { Parapet(M, fr, P(a, -hwS, 0f), P(a, -hwS, sB[cp]), -r); Parapet(M, fr, P(a, hwS, 0f), P(a, hwS, sA[c]), r); }
        }
    }
    static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    static Vector2[] Circle(Vector2 c, float rad, float a0, float a1, int n)
    {
        var p = new Vector2[n + 1]; for (int k = 0; k <= n; k++) { float a = Mathf.Lerp(a0, a1, k / (float)n); p[k] = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rad; }
        return p;
    }
    // stoeprand en stoep langs één zijkant (side +1: naar de volgende arm, -1: naar de vorige) van arm a, van de vakrand tot s1
    static void SideWalk(System.Func<string, MeshBuilder> M, Frame fr, System.Func<int, float, float, Vector2> P, int a, float side, float s1)
    {
        for (float s0 = 0f; s0 < s1 - 1e-3f; s0 += 0.5f)
        {
            float s2 = Mathf.Min(s1, s0 + 0.5f);
            Kerb(M("rand"), fr, P(a, side * hwA, s0), P(a, side * hwA, s2), P(a, side * (hwA + KW), s0), P(a, side * (hwA + KW), s2), AY);
            Walk(M("stoep"), fr, P(a, side * (hwA + KW), s0), P(a, side * (hwA + KW), s2), P(a, side * hwS, s0), P(a, side * hwS, s2));
        }
    }
}
