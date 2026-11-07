using System.Collections.Generic;
using UnityEngine;

// Tunnels door een berg of heuvel. Ligt het terrein op een recht stuk van een gesleepte weg of pad overal minstens 6 m (de mantel
// erin) en ergens minstens 9 m boven de rechte lijn tussen de voet aan beide kanten (met Ongelijkvloers, U: 6 m, net genoeg voor de
// koker), dan gaat dat stuk erdoorheen
// in plaats van eroverheen. In de graaf is dat een tunnelverbinding (TunnelLinks, zoals een weg onder een brug door) van het laatste
// wegvak vóór de berg naar het eerste erna; de vakken ertussen zijn weer gewoon terrein met hun eigen hoogte (er kan op gebouwd
// worden). Getekend naar het model tunnel entrance.dae (hexgrid city concept): de zeshoekige koker en het portaal van de
// onderdoorgang, met eromheen een zeshoekige mantel (2,17 m boven en onder de koker, 3,75 m opzij, de zijden onder 30°) die uit de
// helling steekt. Het terrein binnen de mantel wordt weggesneden (BoreTri), de rest blijft staan. De weg in de tunnel helt recht van
// voet tot voet; het verkeer rijdt er vanzelf door (SurfaceH helt van hart tot hart naar de partner).
public static partial class PlanetCity
{
    const float BoreGrade = 0.15f;   // hooguit 15% helling in een tunnel
    static float BoreNeed => DragUnder ? 6f : 2f * LevelH;   // zoveel terrein moet ergens boven de weg liggen
    // en zoveel boven elk vak ertussen: de hele mantel (koker LevelH − DeckT, plus 2,17 m) in de berg, anders ligt er een grijze balk
    // over een laag heuveltje; met Ongelijkvloers (U) een half niveau, dan mag de mantel eruit steken
    static float BoreCover => DragUnder ? StepH : LevelH - DeckT + 2.17f + 0.4f;

    // Dalbruggen: hetzelfde, maar dan over een dal. Ligt het terrein op een recht stuk overal minstens SpanClear en ergens SpanNeed
    // onder de rechte lijn tussen de randen (en is het stuk niet langer dan SpanMax), dan gaat de weg er met een brug overheen: een
    // lange verbinding van landhoofd tot landhoofd (TunnelLinks plus SpanLinks), het dal eronder blijft terrein. Getekend als
    // betonnen dek met borstwering, landhoofden en zeshoekige pijlers tot op het maaiveld (SpanMesh).
    static float SpanClear => RoadH.Count > 0 ? 1.6f : 0.8f;   // nieuwe opbouw: het dek (1,3 m dik) blijft boven het terrein, dat wordt niet meer verlaagd
    const float SpanMax = 260f;
    static float SpanNeed => DragUnder ? StepH : LevelH;
    // stap k → k+1 van een rechte lijn: een buur, of schuin (90°) over een hoek
    static bool LineStep(int a, int b) => IsNeighbour(a, b) || System.Array.IndexOf(Planet.Cells[a].across, b) >= 0;
    // ligt c recht tussen p en q (ook op een schuine lijn)?
    static bool Straight(int p, int c, int q) { if (MidCell(p, q) == c) return true; Vector3 d = Planet.Cells[c].dir; return Vector3.Dot(Tan(Planet.Cells[c].dir - Planet.Cells[p].dir, d), Tan(Planet.Cells[q].dir - d, d)) > 0.985f; }
    public static readonly HashSet<long> SpanLinks = new HashSet<long>();   // de lange verbindingen in TunnelLinks die een brug zijn

    // is a–b een lange verbinding (een tunnel door een berg of een brug over een dal, over meer dan één vak)?
    public static bool IsBore(int a, int b) => TunnelLinks.Contains(Key(a, b)) && MidCell(a, b) < 0;
    // is a–b een brug over een dal?
    public static bool IsSpan(int a, int b) => SpanLinks.Contains(Key(a, b)) && IsBore(a, b);
    // begint in vak i een tunnel door een berg (een portaal)?
    static bool BorePortal(int i) { if (TunnelLinks.Count == 0) return false; foreach (int j in Partners(i)) if (IsBore(i, j)) return true; return false; }

    // De tunnels op een lijn: per recht stuk de langste waarbij elk vak ertussen minstens een half niveau boven de rechte weg ligt
    // (ground) en ergens BoreNeed, dan links en rechts daarvan opnieuw. plain: kan een portaal zijn; step: de lijn gaat recht naar de
    // buur; inner: mag de berg in; road: de hoogte van de weg in een portaalvak. Geeft de paren (portaal, portaal) als index in de lijn.
    // span: een dalbrug zoeken in plaats van een tunnel (het terrein ertussen ligt ónder de rechte weg, SpanClear en SpanNeed)
    static List<(int i, int j)> BoreSearch(List<int> line, System.Func<int, bool> plain, System.Func<int, bool> step, System.Func<int, bool> inner, System.Func<int, float> road, System.Func<int, float> ground, bool span = false)
    {
        var done = new List<(int i, int j)>();
        void Search(int lo, int hi)
        {
            int bi = -1, bj = -1; float best = 0f;
            for (int i = lo; i < hi; i++)
            {
                if (!plain(line[i])) continue;
                for (int j = i + 1; j <= hi; j++)
                {
                    if (!step(j - 1) || (j - 1 > i && !inner(j - 1))) break;
                    if (j - i < 3 || !plain(line[j])) continue;   // minstens twee vakken berg (één vak is een gewone brug)
                    float ga = road(line[i]), gb = road(line[j]), dist = (Planet.Cells[line[i]].dir - Planet.Cells[line[j]].dir).magnitude * Planet.R;
                    if (Mathf.Abs(gb - ga) > BoreGrade * dist || (span && dist > SpanMax)) continue;
                    float mn = float.MaxValue, mx = float.MinValue;
                    for (int k = i + 1; k < j; k++) { float cv = ground(line[k]) - Mathf.Lerp(ga, gb, (k - i) / (float)(j - i)); if (span) cv = -cv; mn = Mathf.Min(mn, cv); mx = Mathf.Max(mx, cv); }
                    if (span ? (mn < SpanClear || mx < SpanNeed) : (mn < BoreCover || mx < BoreNeed)) continue;
                    if (j - i > bj - bi || (j - i == bj - bi && mn > best)) { bi = i; bj = j; best = mn; }
                }
            }
            if (bi < 0) return; done.Add((bi, bj)); Search(lo, bi); Search(bj, hi);
        }
        if (line != null && line.Count >= 4) Search(0, line.Count - 1);
        return done;
    }
    // Na het leggen van een lijn op het maaiveld: de stukken die door een berg moeten, worden een tunnel. Alleen vakken die alleen bij
    // deze lijn horen (geen kruising, brug of weg op een niveau) gaan de berg in.
    public static int BoreLine(List<int> line)
    {
        if (line == null || line.Count < 4 || DragLevel != 0) return 0;
        bool Plain(int c) => IsWalk(c) && !Underpass(c) && !IsRot(c) && !preCarve.ContainsKey(c);
        System.Func<int, bool> Step = k => LineStep(line[k], line[k + 1]) && Linked(line[k], line[k + 1]);
        System.Func<int, bool> Inner = k => { int c = line[k], p = line[k - 1], q = line[k + 1]; var ps = Partners(c); return Plain(c) && ps.Count == 2 && ps.Contains(p) && ps.Contains(q) && Straight(p, c, q); };
        System.Func<int, float> Ground = c => preLevel.TryGetValue(c, out var h) ? h : G(c);   // het terrein van voor de weg (SetLevel zette hem op de hoogte van zijn buren)
        var done = BoreSearch(line, Plain, Step, Inner, G, Ground);
        float len = 0f;
        foreach (var (i, j) in done) { len += Span(line, i, j); SpanLinks.Remove(Key(line[i], line[j])); }
        // daarna de dalen: de rest van de lijn (de vakken in een tunnel zijn al geen weg meer)
        var dals = BoreSearch(line, Plain, Step, Inner, G, Ground, true);
        float dlen = 0f;
        foreach (var (i, j) in dals) { dlen += Span(line, i, j); SpanLinks.Add(Key(line[i], line[j])); }
        var notes = new List<string>();
        if (done.Count > 0) notes.Add((done.Count == 1 ? "Een tunnel" : done.Count + " tunnels") + " door de berg (" + Mathf.RoundToInt(len) + " m)");
        if (dals.Count > 0) notes.Add((dals.Count == 1 ? "Een brug" : dals.Count + " bruggen") + " over het dal (" + Mathf.RoundToInt(dlen) + " m)");
        if (notes.Count > 0) LastNote = string.Join(", ", notes);
        return done.Count + dals.Count;
    }
    // de vakken tussen line[i] en line[j] worden weer terrein; een lange verbinding van i naar j (tunnel of brug)
    static float Span(List<int> line, int i, int j)
    {
        int a = line[i], b = line[j], f = Flow(a, line[i + 1]);
        for (int k = i + 1; k < j; k++) Clear(line[k]);   // het stuk door de berg of over het dal is geen weg op het maaiveld meer
        for (int k = i; k < j; k++) if (!IsNeighbour(line[k], line[k + 1])) ClearHalves(line[k], line[k + 1]);   // schuin: de halve vakken ertussen ook
        LinkTunnel(a, b); if (f > 0) OneWay.Add(DKey(a, b)); else if (f < 0) OneWay.Add(DKey(b, a));
        Touch(a); Touch(b); return (Planet.Cells[a].dir - Planet.Cells[b].dir).magnitude * Planet.R - 2f * FlatL;
    }
    // Voor het voorbeeld tijdens het slepen: waar de geplande lijn een tunnel krijgt, met de hoogte van de weg bij de portalen (zoals
    // BoreLine na PlaceLine en SetLevel: een nieuw vak komt op de hoogte van zijn buren, de berg in gaan alleen nieuwe vakken)
    public static List<(int a, int b, float ha, float hb, bool span)> BorePlan(List<int> line, string type)
    {
        var o = new List<(int, int, float, float, bool)>(); if (line == null || line.Count < 4 || DragLevel != 0) return o;
        bool Plain(int c) => !Underpass(c) && !IsRot(c) && !preCarve.ContainsKey(c) && (IsWalk(c) || CanRoad(c, type));
        float Road(int c)   // een nieuw vak: het gemiddelde van zijn buren die geen weg of pad zijn en niet op de lijn liggen (PadGround na het leggen)
        {
            if (IsWalk(c)) return G(c); float s = 0f; int n = 0;
            foreach (int j in Planet.Cells[c].nb) if (!IsWalk(j) && !Underpass(j) && !line.Contains(j)) { s += G(j); n++; }
            return Mathf.Max(n > 0 ? s / n : G(c), DryRoad);   // nooit onder water (SetLevel)
        }
        System.Func<int, bool> Step = k => LineStep(line[k], line[k + 1]);
        var taken = new HashSet<int>();   // al in een tunnel: geen dal meer
        System.Func<int, bool> Inner = k => !IsWalk(line[k]) && Plain(line[k]) && Straight(line[k - 1], line[k], line[k + 1]) && !taken.Contains(k);
        void Add(int i, int j, bool span)
        {
            int a = line[i], b = line[j]; float ha = Road(a), hb = Road(b);
            if (a > b) { (a, b) = (b, a); (ha, hb) = (hb, ha); }   // zoals de tunnel straks getekend wordt: vanaf het kleinste vak
            o.Add((a, b, ha, hb, span)); for (int k = i + 1; k < j; k++) taken.Add(k);
        }
        foreach (var (i, j) in BoreSearch(line, Plain, Step, Inner, Road, G)) Add(i, j, false);
        foreach (var (i, j) in BoreSearch(line, Plain, Step, Inner, Road, G, true)) Add(i, j, true);
        return o;
    }

    // halve breedte van de koker: een weg 6 m (een brede weg of laan wat meer), een pad 4,5 m
    static float BoreHW(int a) => IsRoad(a) ? Mathf.Max(RoadHW, FlatHW(a)) : PathHW;
    // Het vlak van een tunnel: in het hart van a, e naar b, op de bol (gnomonisch: een rechte lijn in het vlak is een grote cirkel),
    // y = 0 een niveau boven de weg (het dek, zoals bij een brug) en hellend met de weg mee. p0, p1: de portalen, een half vak van
    // het hart van a en van b.
    static Frame BoreFrame(int a, int b, out Vector2 p0, out Vector2 p1, out float k)
    {
        Vector3 dA = Planet.Cells[a].dir, dB = Planet.Cells[b].dir;
        var f = new Frame(a, true).Flat(); f.e = Tan(dB - dA, dA); f.n = Vector3.Cross(f.u, f.e).normalized;
        float X(Vector3 d) => Vector3.Dot(d * (Planet.R / Vector3.Dot(d, dA)) - dA * Planet.R, f.e);
        float L = X(dB); k = (G(b) - G(a)) / L;
        p0 = new Vector2(Underground(a) ? 0f : FlatL, 0f); p1 = new Vector2(Underground(b) ? L : X((dB * Planet.R + Tan(dA - dB, dB) * FlatL).normalized), 0f);
        return f.Lift(LevelH).Incline(k);
    }
    // Een ondergronds kruispunt (nieuwe opbouw): een wegvak met een profiel diep onder het terrein (minstens BoreNeed) waarvan alle armen
    // tunnels zijn. Geen portaal en geen mantel die uit de berg steekt: de kokers lopen door tot het hart van het vak en komen daar samen.
    public static bool Underground(int c)
    {
        if (RoadH.Count == 0 || !IsWalk(c) || Terrain(c) < G(c) + BoreNeed) return false;
        int nb = 0; foreach (int j in Partners(c)) { if (!IsBore(c, j) || SpanLinks.Contains(Key(c, j))) return false; nb++; }
        return nb >= 2;
    }
    // de koker, de portalen en de mantel van elke tunnel die in a begint (de kleinste van de twee tekent)
    static void BoreMesh(Dictionary<string, MeshBuilder> mb, int a)
    {
        System.Func<string, MeshBuilder> M = key => { if (!mb.TryGetValue(key, out var y)) { y = new MeshBuilder(); mb[key] = y; } return y; };
        foreach (int b in Partners(a))
        {
            if (b < a || !IsBore(a, b)) continue;
            if (SpanLinks.Contains(Key(a, b))) { foreach (var q in spans) if (q.a == a && q.b == b) SpanMesh(M, q, Vector3.zero, false); continue; }   // een brug over een dal
            var f = BoreFrame(a, b, out var p0, out var p1, out _); tubeW = BoreHW(a); float fl = -LevelH, top = -DeckT;
            int ns = Mathf.Max(1, Mathf.CeilToInt((p1 - p0).magnitude / 6f));   // in stukken, zodat de koker de bol volgt
            for (int s = 0; s < ns; s++) TubeSeg(M, f, Vector2.Lerp(p0, p1, s / (float)ns), Vector2.Lerp(p0, p1, (s + 1) / (float)ns), fl, fl, top, false, false);
            if (!Underground(a)) Portal(M, f, p0, new Vector2(-1f, 0f), fl, top); if (!Underground(b)) Portal(M, f, p1, new Vector2(1f, 0f), fl, top);
            Mantle(M, f, p0, p1, fl, top);
            for (float s = p0.x + 3f; s < p1.x - 2f; s += 6f)   // tunnellichten: om de 6 m twee lichtbalken tegen het plafond, boven elke rijstrook
                foreach (float x in new[] { -tubeW * 0.45f, tubeW * 0.45f })
                    M("lamp").Quad(f.P(s - 0.9f, top - 0.03f, x - 0.18f), f.P(s + 0.9f, top - 0.03f, x - 0.18f), f.P(s + 0.9f, top - 0.03f, x + 0.18f), f.P(s - 0.9f, top - 0.03f, x + 0.18f), -f.u);
            tubeW = PathHW;
        }
    }

    // De zeshoekige mantel om de koker (tunnel entrance.dae): de grote zeshoek van het portaal (onder en boven 3,75 m buiten de koker,
    // 2,17 m onder de vloer en boven het plafond, op halve hoogte onder 30° breder) als buis van portaal tot portaal, in stukken van
    // 6 m zodat hij de bol volgt. De binnenkant is de koker (TubeSeg), de voorkant het portaal (Portal).
    static void Mantle(System.Func<string, MeshBuilder> M, Frame f, Vector2 p0, Vector2 p1, float fl, float top)
    {
        float yB = fl - 2.17f, yO = top + 2.17f, co = (yO - yB) * 0.5f * 0.577f, ym = (yB + yO) * 0.5f, wb = tubeW + 3.75f;
        var o = new[] { new Vector2(-wb, yB), new Vector2(wb, yB), new Vector2(wb + co, ym), new Vector2(wb, yO), new Vector2(-wb, yO), new Vector2(-wb - co, ym) };
        int n = Mathf.Max(1, Mathf.CeilToInt((p1.x - p0.x) / 6f));
        for (int q = 0; q < n; q++)
        {
            float sa = Mathf.Lerp(p0.x, p1.x, q / (float)n), sb = Mathf.Lerp(p0.x, p1.x, (q + 1) / (float)n); Vector3 axis = f.P((sa + sb) * 0.5f, ym, 0f);
            for (int v = 0; v < 6; v++)
            {
                int w = (v + 1) % 6; Vector3 a0 = f.P(sa, o[v].y, o[v].x), a1 = f.P(sa, o[w].y, o[w].x), b1 = f.P(sb, o[w].y, o[w].x), b0 = f.P(sb, o[v].y, o[v].x);
                M("steen").Quad(a0, a1, b1, b0, (a0 + a1 + b1 + b0) * 0.25f - axis);   // naar buiten gekeerd
            }
        }
    }

    // ---------- het terrein in de mantel weg ----------
    // Het terrein is een hoogteveld: het kan niet tegelijk boven en onder de koker liggen. Wat binnen de zeshoekige mantel valt (vooral
    // bij de portalen, waar de berg nog laag is) wordt weggesneden, iets binnen de buitenkant zodat het terrein er net in steekt; de
    // mantel is daar de buitenkant. De rest blijft zoals het is.
    struct BoreC { public int a; public Vector3 dA, e, n; public float s0, s1, h0, k, tw, yO, ym, wm, cs; }   // langs e van s0 tot s1; h0 + k·s: het dek; tw: halve breedte van de koker; de zeshoek: bovenkant yO, halve hoogte ym (halve breedte wm), zijden cs breder per meter lager
    static readonly List<BoreC> bores = new List<BoreC>();
    static readonly Dictionary<int, List<int>> boreNear = new Dictionary<int, List<int>>();   // vak → de tunnels eronder
    static string boreSig = "";
    static void CollectBores()
    {
        var old = new List<int>(boreNear.Keys); old.AddRange(spanNear.Keys); bores.Clear(); boreNear.Clear(); spans.Clear(); spanNear.Clear(); var sb = new System.Text.StringBuilder();
        SpanLinks.RemoveWhere(k => !TunnelLinks.Contains(k));
        foreach (long key in TunnelLinks)
        {
            int a = (int)(key >> 20), b = (int)(key & 0xFFFFF); if (MidCell(a, b) >= 0 || !IsWalk(a) || !IsWalk(b)) continue;
            if (SpanLinks.Contains(key))   // een brug over een dal
            {
                if (a > b) (a, b) = (b, a);
                var q = MakeSpan(a, b, G(a), G(b), SpanHW(a)); spans.Add(q);
                sb.Append('s').Append(a).Append('-').Append(b).Append(':').Append(G(a).ToString("F2")).Append(',').Append(G(b).ToString("F2")).Append(',').Append(q.hw).Append('|');
                foreach (int c in BoreRow(a, b)) foreach (int j in Planet.Cells[c].nb) foreach (int x in new[] { c, j })
                {
                    if (!spanNear.TryGetValue(x, out var l)) spanNear[x] = l = new List<int>(1);
                    if (!l.Contains(spans.Count - 1)) l.Add(spans.Count - 1);
                }
                continue;
            }
            BoreFrame(a, b, out var p0, out var p1, out float k); Vector3 dA = Planet.Cells[a].dir, e = Tan(Planet.Cells[b].dir - dA, dA);
            // de mantel, zoals Mantle (ten opzichte van het dek), 10 cm kleiner
            float fl = -LevelH, top = -DeckT, yB = fl - 2.17f, yO = top + 2.17f, co = (yO - yB) * 0.5f * 0.577f, ym = (yB + yO) * 0.5f;
            int idx = bores.Count;
            bores.Add(new BoreC { a = a, dA = dA, e = e, n = Vector3.Cross(dA, e).normalized, s0 = p0.x, s1 = p1.x, h0 = G(a) + LevelH, k = k, tw = BoreHW(a), yO = yO - 0.1f, ym = ym, wm = BoreHW(a) + 3.75f + co - 0.1f, cs = co / (yO - ym) });
            sb.Append(a).Append('-').Append(b).Append(':').Append(G(a).ToString("F2")).Append(',').Append(G(b).ToString("F2")).Append(',').Append(BoreHW(a)).Append('|');
            foreach (int c in BoreRow(a, b))
                foreach (int j in Planet.Cells[c].nb)
                    foreach (int x in new[] { c, j })
                    {
                        if (!boreNear.TryGetValue(x, out var l)) boreNear[x] = l = new List<int>(1);
                        if (!l.Contains(idx)) l.Add(idx);
                    }
        }
        string sig = sb.ToString(); if (sig == boreSig) return; boreSig = sig;
        foreach (int c in old) Touch(c); foreach (int c in boreNear.Keys) Touch(c); foreach (int c in spanNear.Keys) Touch(c);   // een tunnel kwam, ging of veranderde: het terrein erboven opnieuw
    }
    // de vakken op de rechte lijn van a naar b (a en b erbij)
    static List<int> BoreRow(int a, int b)
    {
        var o = new List<int> { a }; int cur = a;
        for (int s = 0; s < 400 && cur != b; s++)
        {
            Vector3 cd = Planet.Cells[cur].dir, to = Tan(Planet.Cells[b].dir - cd, cd); int best = -1; float bd = -2f;
            foreach (int j in Planet.Cells[cur].nb) { if (j == b) { best = b; break; } float d = Vector3.Dot(Tan(Planet.Cells[j].dir - cd, cd), to); if (d > bd) { bd = d; best = j; } }
            cur = best; o.Add(cur);
        }
        return o;
    }
    // een driehoekje van het maaiveld, zonder het deel dat in de mantel van een tunnel valt
    static void BoreTri(MeshBuilder m, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 up, List<int> near)
    {
        if (near == null) { m.Tri(p0, p1, p2, up); return; }
        BorePiece(m, new List<Vector3> { p0, p1, p2 }, up, near, 0);
    }
    static void BorePiece(MeshBuilder m, List<Vector3> poly, Vector3 up, List<int> near, int idx)
    {
        if (idx == near.Count) { for (int k = 1; k + 1 < poly.Count; k++) m.Tri(poly[0], poly[k], poly[k + 1], up); return; }
        var q = bores[near[idx]];
        // de mantel is de doorsnede van zeven halve ruimtes (g ≤ 0); wat buiten één ervan valt blijft staan, de rest gaat naar de volgende
        for (int f = 0; f < 7; f++)
        {
            var outP = ClipBore(poly, q, f, true); if (outP.Count >= 3) BorePiece(m, outP, up, near, idx + 1);
            poly = ClipBore(poly, q, f, false); if (poly.Count < 3) return;
        }
    }
    // In een tunnel (voor wie loopt, PlanetWalker): staat wereldpunt p in een koker onder het plafond, dan de hoogte van de vloer daar
    // (en op een brug over een dal: de hoogte van het dek; daar ook geen bermbomen of lantaarns, PlanetRoads)
    public static bool InBore(Vector3 p, out float floor)
    {
        floor = 0f; if (boreNear.Count == 0 && spanNear.Count == 0) return false;
        int near = Planet.Nearest(p); float r = p.magnitude; Vector3 d = p / r; bool hit = false;
        if (spanNear.TryGetValue(near, out var sl))
            foreach (int si in sl)
            {
                var q = spans[si]; SpanLocal(q, d, out float s, out float x); if (s < q.s0 - 1f || s > q.s1 + 1f || Mathf.Abs(x) > q.hw + 0.35f) continue;
                float deck = Mathf.Lerp(q.ha, q.hb, s / q.L) + AY; if (r - Planet.R < deck - 3f) continue;   // eronder, in het dal: het maaiveld
                if (!hit || deck > floor) floor = deck; hit = true;
            }
        if (!boreNear.TryGetValue(near, out var bl)) return hit;
        foreach (int bi in bl)
        {
            var q = bores[bi]; Vector3 v = d * (Planet.R / Vector3.Dot(d, q.dA)) - q.dA * Planet.R; float s = Vector3.Dot(v, q.e), x = Vector3.Dot(v, q.n);
            float road = q.h0 - LevelH + q.k * s;   // de weg: een niveau onder het dek
            if (s < q.s0 - 1.5f || s > q.s1 + 1.5f || Mathf.Abs(x) > q.tw + 1f || r - Planet.R > road + LevelH - DeckT) continue;   // net voor het portaal telt mee (de voorrand)
            if (!hit || road > floor) floor = road; hit = true;
        }
        return hit;
    }
    static float BoreG(BoreC q, Vector3 p, int f)
    {
        float r = p.magnitude; Vector3 d = p / r, v = d * (Planet.R / Vector3.Dot(d, q.dA)) - q.dA * Planet.R; float s = Vector3.Dot(v, q.e), x = Vector3.Dot(v, q.n);
        float h = r - Planet.R - (q.h0 + q.k * s);   // ten opzichte van het dek
        switch (f)
        {
            case 0: return q.s0 - s;                              // voor het portaal
            case 1: return s - q.s1;                              // na het andere portaal
            case 2: return h - q.yO;                              // boven de mantel
            case 3: return x + (h - q.ym) * q.cs - q.wm;          // de schuine zijden boven halve hoogte
            case 4: return -x + (h - q.ym) * q.cs - q.wm;
            case 5: return x - (h - q.ym) * q.cs - q.wm;          // en eronder
            default: return -x - (h - q.ym) * q.cs - q.wm;
        }
    }
    // het deel van een veelhoek met g > 0 (outside) of g ≤ 0
    static List<Vector3> ClipBore(List<Vector3> poly, BoreC q, int f, bool outside)
    {
        var o = new List<Vector3>(poly.Count + 2); int n = poly.Count; var g = new float[n];
        for (int k = 0; k < n; k++) { g[k] = BoreG(q, poly[k], f); if (outside) g[k] = -g[k]; }
        for (int k = 0; k < n; k++)
        {
            int w = (k + 1) % n; if (g[k] <= 0f) o.Add(poly[k]);
            if ((g[k] <= 0f) != (g[w] <= 0f)) o.Add(Vector3.Lerp(poly[k], poly[w], g[k] / (g[k] - g[w])));
        }
        return o;
    }

    // ---------- bruggen over een dal ----------
    // langs de grote cirkel van a naar b: s (m vanaf het hart van a), x (m opzij, langs n); het dek ligt op lerp(ha, hb, s / L)
    struct SpanC { public int a, b; public Vector3 dA, eA, n; public float L, ha, hb, hw, s0, s1; }
    static readonly List<SpanC> spans = new List<SpanC>();
    static readonly Dictionary<int, List<int>> spanNear = new Dictionary<int, List<int>>();   // vak → de bruggen erboven
    // halve breedte van de weg met stoep (of het pad) op de brug; de borstwering staat daar
    static float SpanHW(int a) { if (!IsRoad(a)) return PadHW((byte)(IsBike(a) ? 1 : 0)) + 0.6f; Dims(StyleOf(a), out _, out float sw); return sw; }
    static SpanC MakeSpan(int a, int b, float ha, float hb, float hw)
    {
        Vector3 dA = Planet.Cells[a].dir, dB = Planet.Cells[b].dir, n = Vector3.Cross(dA, dB).normalized;
        float L = Vector3.Angle(dA, dB) * Mathf.Deg2Rad * Planet.R;
        bool full = RoadH.Count > 0;   // nieuwe opbouw: het dek van hart tot hart, het landhoofd op het hart van het landhoofdvak (geen opgehoogde grond ervoor)
        return new SpanC { a = a, b = b, dA = dA, eA = Vector3.Cross(n, dA).normalized, n = n, L = L, ha = ha, hb = hb, hw = hw, s0 = full ? 0f : FlatL * 0.55f, s1 = full ? L : L - FlatL * 0.55f };
    }
    // ligt wereldpunt w onder het dek van een brug over een dal (voorbij de landhoofden)? Dan hooguit tot 20 cm onder de onderkant.
    static bool SpanBottom(Vector3 w, out float bottom)
    {
        bottom = 0f; if (RoadH.Count > 0 || !spanNear.TryGetValue(Planet.Nearest(w), out var sl)) return false; Vector3 d = w.normalized; bool hit = false;
        foreach (int si in sl)
        {
            var q = spans[si]; SpanLocal(q, d, out float s, out float x); if (s <= q.s0 || s >= q.s1 || Mathf.Abs(x) > q.hw + 0.6f) continue;
            float b = Mathf.Lerp(q.ha, q.hb, s / q.L) - 1.5f; if (!hit || b < bottom) bottom = b; hit = true;
        }
        return hit;
    }
    // ligt wereldpunt p boven het dek van een brug over een dal (tussen de landhoofden)? Daar zakt de zijkant van de stoep niet tot in het dal.
    static bool OverSpan(Vector3 p)
    {
        if (spanNear.Count == 0 || !spanNear.TryGetValue(Planet.Nearest(p), out var sl)) return false; Vector3 d = p.normalized;
        foreach (int si in sl) { var q = spans[si]; SpanLocal(q, d, out float s, out float x); if (s > q.s0 - 0.5f && s < q.s1 + 0.5f && Mathf.Abs(x) < q.hw + 1f) return true; }
        return false;
    }
    static void SpanLocal(SpanC q, Vector3 d, out float s, out float x)
    {
        s = Mathf.Atan2(Vector3.Dot(d, q.eA), Vector3.Dot(d, q.dA)) * Planet.R; x = Vector3.Dot(d, q.n) * Planet.R;
    }
    static Vector3 SpanP(SpanC q, float s, float x, float y)
    {
        float t = s / Planet.R; Vector3 d = q.dA * Mathf.Cos(t) + q.eA * Mathf.Sin(t);
        return (d * Planet.R + q.n * x).normalized * (Planet.R + Mathf.Lerp(q.ha, q.hb, s / q.L) + y);
    }
    // Het dek (onderkant 1,3 m onder de weg), de borstwering (1,05 m boven de weg, 35 cm dik), een landhoofd aan elk eind tot in de
    // grond en zeshoekige pijlers om de 24 m tot op het maaiveld (twee naast elkaar onder een brede weg). ghost: het voorbeeld tijdens
    // het slepen, met de weg erop (in de lijn ontbreekt dat stuk) en alles van beide kanten zichtbaar.
    static void SpanMesh(System.Func<string, MeshBuilder> M, SpanC q, Vector3 origin, bool ghost)
    {
        float ow = q.hw + 0.35f, yb = -1.3f, yp = 1.05f, s0 = q.s0, s1 = q.s1;
        Vector3 P(float s, float x, float y) => SpanP(q, s, x, y) - origin;
        void Q(string mat, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 nrm) { M(mat).Quad(a, b, c, d, nrm); if (ghost) M(mat).Quad(a, b, c, d, -nrm); }
        Vector3 Up(float s) => SpanP(q, s, 0f, 0f).normalized;
        Vector3 Along(float s) { Vector3 u = Up(s); Vector3 t = SpanP(q, s + 0.5f, 0f, 0f) - SpanP(q, s - 0.5f, 0f, 0f); return (t - u * Vector3.Dot(t, u)).normalized; }
        int n = Mathf.Max(1, Mathf.CeilToInt((s1 - s0) / 6f));
        for (int k = 0; k < n; k++)
        {
            float sa = Mathf.Lerp(s0, s1, k / (float)n), sb = Mathf.Lerp(s0, s1, (k + 1) / (float)n); Vector3 up = Up((sa + sb) * 0.5f);
            Q("steen", P(sa, -ow, yb), P(sb, -ow, yb), P(sb, ow, yb), P(sa, ow, yb), -up);   // onderkant
            if (ghost) Q("asfalt", P(sa, -q.hw, AY), P(sb, -q.hw, AY), P(sb, q.hw, AY), P(sa, q.hw, AY), up);
            for (int sg = -1; sg <= 1; sg += 2)
            {
                Vector3 side = q.n * sg;
                Q("steen", P(sa, sg * ow, yb), P(sb, sg * ow, yb), P(sb, sg * ow, yp), P(sa, sg * ow, yp), side);                      // buitenkant
                Q("steen", P(sa, sg * q.hw, yp), P(sb, sg * q.hw, yp), P(sb, sg * ow, yp), P(sa, sg * ow, yp), up);                    // bovenop de borstwering
                Q("steen", P(sa, sg * q.hw, SY - 0.05f), P(sb, sg * q.hw, SY - 0.05f), P(sb, sg * q.hw, yp), P(sa, sg * q.hw, yp), -side);   // binnenkant
            }
        }
        // landhoofden: een blok van 2 m dik aan elk eind, van het dek tot een halve meter in de grond
        foreach (var (se, dir) in new[] { (s0, -1f), (s1, 1f) })
        {
            float si = se + dir * 2f, sm = (se + si) * 0.5f;   // si: de kant naar het landhoofdvak
            float foot = Mathf.Min(TerrainAt(SpanP(q, se, 0f, 0f).normalized), TerrainAt(SpanP(q, sm, 0f, 0f).normalized)) - Mathf.Lerp(q.ha, q.hb, se / q.L) - 0.5f;
            foot = Mathf.Min(foot, yb - 0.5f); Vector3 al = Along(se) * -dir, up = Up(se);   // al: naar het dal
            Q("steen", P(se, -ow, foot), P(se, ow, foot), P(se, ow, SY), P(se, -ow, SY), al);   // de voorkant, naar het dal
            for (int sg = -1; sg <= 1; sg += 2) Q("steen", P(se, sg * ow, foot), P(si, sg * ow, foot), P(si, sg * ow, yp), P(se, sg * ow, yp), q.n * sg);
            for (int sg = -1; sg <= 1; sg += 2) Q("steen", P(se, sg * q.hw, yp), P(si, sg * q.hw, yp), P(si, sg * ow, yp), P(se, sg * ow, yp), up);
            Q("steen", P(se, -ow, yp), P(se, -q.hw, yp), P(se, -q.hw, SY), P(se, -ow, SY), al); Q("steen", P(se, q.hw, yp), P(se, ow, yp), P(se, ow, SY), P(se, q.hw, SY), al);
        }
        // pijlers
        int np = Mathf.Max(1, Mathf.RoundToInt((s1 - s0) / 24f));
        for (int k = 1; k < np; k++)
        {
            float s = Mathf.Lerp(s0, s1, k / (float)np), h = Mathf.Lerp(q.ha, q.hb, s / q.L);
            var xs = q.hw > 7f ? new[] { -q.hw * 0.45f, q.hw * 0.45f } : new[] { 0f }; float r = xs.Length > 1 ? 1.1f : 1.4f;
            foreach (float x in xs)
            {
                float foot = TerrainAt(SpanP(q, s, x, 0f).normalized) - h - 0.6f; if (foot > yb - 0.3f) continue;   // het maaiveld komt tot onder het dek
                Vector3 b = P(s, x, foot), up = Up(s); Prism3D(M("steen"), b, up, r, r, yb - foot + 0.05f);
            }
        }
    }
    // het voorbeeld van een brug over een dal tijdens het slepen (BorePlan met span)
    public static void SpanGhost(MeshBuilder mb, int a, int b, float ha, float hb, string type, Vector3 origin)
    {
        float hw; if (type == "weg") { Dims(DragStyle, out _, out hw); } else hw = PadHW((byte)(DragBike ? 1 : 0)) + 0.6f;
        SpanMesh(_ => mb, MakeSpan(a, b, ha, hb, hw), origin, true);
    }
}
