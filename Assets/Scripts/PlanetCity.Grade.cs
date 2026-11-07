using System.Collections.Generic;
using UnityEngine;

// Hellingbegrenzer: een weg of pad op het maaiveld wordt nooit steiler dan MaxGrade. Na het leggen (en na de tunnels en dalbruggen
// van BoreLine) worden de hoogtes van de vakken langs de lijn bijgesteld: een top wordt afgegraven, een kom opgehoogd, zo min
// mogelijk (de helft van het teveel aan elke kant, herhaald tot het klopt). Het terrein van het vak gaat mee (LevelTerr, met de oude
// hoogte onthouden) en de hoogtefunctie langs wegen maakt er een talud bij. Een vak dat al aan een andere weg vastzit (een kruising
// met een bestaande weg) houdt zijn hoogte: anders ging die andere weg scheef. Tunnels en dalbruggen hebben hun eigen regels.
public static partial class PlanetCity
{
    public static float MaxGrade = 0.10f, MaxEarth = 12f;   // MaxEarth: hooguit zoveel afgraven of ophogen voor de helling (het talud reikt 14 m)   // 10%: steiler is in het echt een uitzondering (tunnels mogen 15%, BoreSearch)
    public static int LimitGrade(List<int> line)
    {
        if (line == null || line.Count < 2) return 0; int changed = 0;
        // de stukken op het maaiveld: van vak tot vak, gebroken bij een tunnel of dalbrug
        int s0 = 0;
        for (int i = 1; i <= line.Count; i++)
        {
            bool brk = i == line.Count || IsBore(line[i - 1], line[i]);
            if (brk) { if (i - s0 >= 2) changed += LimitRun(line, s0, i - 1); s0 = i; }
        }
        return changed;
    }
    static int LimitRun(List<int> line, int a, int b)
    {
        int n = b - a + 1; var h = new float[n]; var d = new float[n]; var fix = new bool[n];
        for (int k = 0; k < n; k++)
        {
            int c = line[a + k]; h[k] = G(c);
            if (k + 1 < n) d[k] = Vector3.Angle(Planet.Cells[c].dir, Planet.Cells[line[a + k + 1]].dir) * Mathf.Deg2Rad * Planet.R;
            // vast: een vak met een wegbuur buiten deze lijn (een bestaande weg), of op een niveau
            foreach (int p in Partners(c)) if (line.IndexOf(p) < 0) { fix[k] = true; break; }
            if (levelled.Contains(c)) fix[k] = true;
        }
        for (int it = 0; it < 400; it++)
        {
            bool any = false;
            for (int k = 0; k + 1 < n; k++)
            {
                float e = h[k + 1] - h[k], lim = MaxGrade * d[k]; if (Mathf.Abs(e) <= lim + 1e-4f) continue; any = true;
                float over = (Mathf.Abs(e) - lim) * Mathf.Sign(e);   // zoveel te steil (met teken)
                if (fix[k] && fix[k + 1]) continue;
                if (fix[k]) h[k + 1] -= over; else if (fix[k + 1]) h[k] += over; else { h[k] += over * 0.5f; h[k + 1] -= over * 0.5f; }
            }
            if (!any) break;
        }
        int changed = 0;
        for (int k = 0; k < n; k++) { int c = line[a + k]; if (Mathf.Abs(h[k] - G(c)) > 0.02f) { LevelTerr(c, h[k]); changed++; } }
        return changed;
    }
    // Alle wegen en paden tegelijk (de hoogtetest, Hele planeet vol): elke verbinding op het maaiveld (geen tunnel of dalbrug) naar
    // hooguit MaxGrade, ook over kruispunten heen. Vast: een vak pal naast een brugvak (de bodem van een onderdoorgang: die hoogte
    // komt uit de brug) en de brugvakken zelf.
    public static int LimitGradeAll()
    {
        var idx = new Dictionary<int, int>(); var cells = new List<int>();
        foreach (var kv in Cells) if (IsWalk(kv.Key)) { idx[kv.Key] = cells.Count; cells.Add(kv.Key); }
        int n = cells.Count; if (n == 0) return 0; var h = new float[n]; var fix = new bool[n];
        for (int k = 0; k < n; k++)
        {
            int c = cells[k]; h[k] = G(c); if (Underpass(c) || BorePortal(c)) fix[k] = true;   // ook een portaal van een tunnel of dalbrug: de koker en het dek liggen op die hoogte
            foreach (int j in Planet.Cells[c].nb) if (Underpass(j)) fix[k] = true;
        }
        var links = new List<(int a, int b, float d)>(); var seen = new HashSet<long>();
        foreach (int c in cells) foreach (int p in Partners(c))
        {
            if (!idx.ContainsKey(p) || IsBore(c, p) || !seen.Add(Key(Mathf.Min(c, p), Mathf.Max(c, p)))) continue;
            links.Add((idx[c], idx[p], Vector3.Angle(Planet.Cells[c].dir, Planet.Cells[p].dir) * Mathf.Deg2Rad * Planet.R));
        }
        var orig = new float[n]; for (int k = 0; k < n; k++) orig[k] = preLevel.TryGetValue(cells[k], out var o) ? o : G(cells[k]);   // het terrein van voor de weg
        for (int it = 0; it < 2000; it++)
        {
            bool any = false;
            foreach (var l in links)
            {
                float e = h[l.b] - h[l.a], lim = MaxGrade * l.d; if (Mathf.Abs(e) <= lim + 1e-4f) continue; any = true;
                float over = (Mathf.Abs(e) - lim) * Mathf.Sign(e);
                if (fix[l.a] && fix[l.b]) continue;
                if (fix[l.a]) h[l.b] -= over; else if (fix[l.b]) h[l.a] += over; else { h[l.a] += over * 0.5f; h[l.b] -= over * 0.5f; }
            }
            for (int k = 0; k < n; k++) if (!fix[k]) h[k] = Mathf.Clamp(h[k], orig[k] - MaxEarth, orig[k] + MaxEarth);   // hooguit MaxEarth afgraven of ophogen (dieper is een tunnel of brug)
            if (!any) break;
        }
        int changed = 0;
        for (int k = 0; k < n; k++) if (Mathf.Abs(h[k] - G(cells[k])) > 0.02f) { LevelTerr(cells[k], h[k]); changed++; }
        return changed;
    }
    // Eerst het wegprofiel, dan de kunstwerken: het profiel langs de lijn wordt begrensd op MaxGrade (zo min mogelijk af van het
    // terrein), en waar het terrein daarna ver boven de weg ligt komt een tunnel, ver eronder een dalbrug (dezelfde regels als
    // BoreLine, maar gemeten aan het begrensde profiel in plaats van aan de rechte lijn van hart tot hart). De rest van de vakken
    // gaat met het profiel mee (afgraven of ophogen, met het talud erbij). In plaats van BoreLine + LimitGrade.
    public static int ProfileLine(List<int> line)
    {
        if (line == null || line.Count < 2 || DragLevel != 0) return 0; int n = line.Count;
        bool Plain(int c) => IsWalk(c) && !Underpass(c) && !IsRot(c) && !preCarve.ContainsKey(c);
        System.Func<int, bool> Step = k => LineStep(line[k], line[k + 1]) && Linked(line[k], line[k + 1]);
        System.Func<int, bool> Inner = k => { int c = line[k], p = line[k - 1], q = line[k + 1]; var ps = Partners(c); return Plain(c) && ps.Count == 2 && ps.Contains(p) && ps.Contains(q) && Straight(p, c, q); };
        float Ground(int c) => preLevel.TryGetValue(c, out var h) ? h : G(c);
        // het begrensde profiel
        var h = new float[n]; var d = new float[n]; var fix = new bool[n];
        for (int k = 0; k < n; k++)
        {
            int c = line[k]; h[k] = G(c);
            if (k + 1 < n) d[k] = Vector3.Angle(Planet.Cells[c].dir, Planet.Cells[line[k + 1]].dir) * Mathf.Deg2Rad * Planet.R;
            foreach (int p in Partners(c)) if (line.IndexOf(p) < 0) { fix[k] = true; break; }   // een kruising met een bestaande weg: die blijft liggen
            if (Underpass(c)) fix[k] = true; foreach (int j in Planet.Cells[c].nb) if (Underpass(j)) fix[k] = true;   // onder of naast een brug: de hoogte komt uit de brug
        }
        for (int it = 0; it < 400; it++)
        {
            bool any = false;
            for (int k = 0; k + 1 < n; k++)
            {
                float e = h[k + 1] - h[k], lim = MaxGrade * d[k]; if (Mathf.Abs(e) <= lim + 1e-4f) continue; any = true;
                float over = (Mathf.Abs(e) - lim) * Mathf.Sign(e);
                if (fix[k] && fix[k + 1]) continue;
                if (fix[k]) h[k + 1] -= over; else if (fix[k + 1]) h[k] += over; else { h[k] += over * 0.5f; h[k + 1] -= over * 0.5f; }
            }
            if (!any) break;
        }
        var hp = new Dictionary<int, float>(); for (int k = 0; k < n; k++) hp[line[k]] = h[k];
        float Road(int c) => hp.TryGetValue(c, out var v) ? v : G(c);
        // de kunstwerken: tunnels waar het terrein ver boven het profiel ligt, dalbruggen waar het er ver onder ligt
        var done = BoreSearch(line, Plain, Step, Inner, Road, Ground); float len = 0f;
        foreach (var (i, j) in done) { len += Span(line, i, j); SpanLinks.Remove(Key(line[i], line[j])); }
        var dals = BoreSearch(line, Plain, Step, Inner, Road, Ground, true); float dlen = 0f;
        foreach (var (i, j) in dals) { dlen += Span(line, i, j); SpanLinks.Add(Key(line[i], line[j])); }
        // de rest van de vakken op het profiel
        int changed = 0;
        for (int k = 0; k < n; k++) { int c = line[k]; if (!IsWalk(c) || fix[k]) continue; if (Mathf.Abs(h[k] - G(c)) > 0.02f) { LevelTerr(c, h[k]); changed++; } }
        var notes = new List<string>();
        if (done.Count > 0) notes.Add((done.Count == 1 ? "Een tunnel" : done.Count + " tunnels") + " door de berg (" + Mathf.RoundToInt(len) + " m)");
        if (dals.Count > 0) notes.Add((dals.Count == 1 ? "Een brug" : dals.Count + " bruggen") + " over het dal (" + Mathf.RoundToInt(dlen) + " m)");
        if (changed > 0) notes.Add(changed + " vakken op hooguit " + Mathf.RoundToInt(MaxGrade * 100f) + "% helling");
        if (notes.Count > 0) LastNote = string.Join(", ", notes);
        return done.Count + dals.Count;
    }
    // voor de test: de steilste helling tussen twee verbonden wegvakken (als fractie), en hoeveel verbindingen steiler zijn dan MaxGrade
    public static (float max, int over, int total) GradeStats() => GradeStats(null);
    public static (float max, int over, int total) GradeStats(System.Text.StringBuilder detail)
    {
        float max = 0f; int over = 0, total = 0; var seen = new HashSet<long>();
        foreach (var kv in Cells)
        {
            int a = kv.Key; if (!IsWalk(a)) continue;
            foreach (int b in Partners(a))
            {
                if (!seen.Add(Key(Mathf.Min(a, b), Mathf.Max(a, b)))) continue; total++;
                float dist = Vector3.Angle(Planet.Cells[a].dir, Planet.Cells[b].dir) * Mathf.Deg2Rad * Planet.R; if (dist < 1f) continue;
                float g = Mathf.Abs(G(a) - G(b)) / dist; if (g > max) max = g;
                if (g > MaxGrade + 0.005f) { over++; detail?.Append("  steil ").Append(a).Append('-').Append(b).Append(' ').Append((g * 100f).ToString("0")).Append("% ").Append(IsBore(a, b) ? (SpanLinks.Contains(Key(a, b)) ? "dalbrug" : "tunnel") : IsNeighbour(a, b) ? "recht" : "schuin").Append(" G ").Append(G(a).ToString("0.0")).Append('/').Append(G(b).ToString("0.0")).Append(" niveau ").Append(levelled.Contains(a) || levelled.Contains(b)).Append(" partners ").Append(Partners(a).Count).Append('/').Append(Partners(b).Count).AppendLine(); }
            }
        }
        return (max, over, total);
    }
}
