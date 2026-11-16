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
    // Nieuwe opbouw, stap 1: eerst het profiel van alle wegen samen (hooguit MaxGrade, ook over kruispunten heen, zo min mogelijk
    // van het terrein af; alleen vakken onder of naast een brug vast), dan per lijn de kunstwerken aan dat profiel: een tunnel waar
    // het terrein er ver boven ligt, een dalbrug waar het er ver onder ligt (dezelfde maten als BoreLine). De overige wegvakken komen
    // op het profiel (voorlopig nog via LevelTerr; in stap 5 leest alles de weghoogte uit RoadH en blijft het terrein onaangeroerd).
    public static readonly Dictionary<int, float> RoadH = new Dictionary<int, float>();
    // Het profiel in het spel (Terrein v2, nieuwe opbouw): na elke wijziging opnieuw over alle wegen en paden. line: een net gelegde lijn,
    // daarop zoekt het tunnels en dalbruggen (BoreLine en DipUnder slaat het spel dan over). Terrein v2 uit: het profiel weg, de oude opbouw.
    public static List<int> ProfileNewLine;   // de lijn van de laatste klik (PlanetBuilder), voor Reprofile in Changed
    public static float LastProfileMs, LastProfileOnlyMs;   // het hele profiel met UpdateCuts, en alleen ProfileAll
    public static void Reprofile(List<int> line = null)
    {
        if (!Ground2) { if (RoadH.Count > 0) { foreach (var kv in RoadH) { Touch(kv.Key); foreach (int j in Planet.Cells[kv.Key].nb) Touch(j); } RoadH.Clear(); UpdateCuts(); } return; }
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var ls = new List<List<int>>(); if (line != null && line.Count >= 4) ls.Add(line);
        ProfileAll(ls); LastProfileOnlyMs = sw.ElapsedMilliseconds; UpdateCuts(); LastProfileMs = sw.ElapsedMilliseconds;
    }
    // Een net gesleept voetpad (line): waar het met de bestaande wegen en paden op hun hoogte niet binnen MaxGrade past, wordt de verbinding
    // een trap. De nieuwe vakken van de lijn schuiven eerst nog naar een helling (zoals het profiel dat doet); wat dan nog te steil is
    // (hooguit StairGrade) krijgt treden. Aanroepen na PlaceLine en voor Reprofile: RoadH is dan nog het profiel van voor de lijn.
    public const float StairGrade = 1.2f;
    public static int MarkStairs(List<int> line)
    {
        if (line == null || line.Count < 2) return 0; int n = line.Count; var h = new float[n]; var fix = new bool[n]; var d = new float[n];
        for (int k = 0; k < n; k++) { fix[k] = RoadH.TryGetValue(line[k], out float v); h[k] = fix[k] ? v : Terrain(line[k]); if (k + 1 < n) d[k] = CellDist(line[k], line[k + 1]); }
        for (int it = 0; it < 400; it++)
        {
            bool any = false;
            for (int k = 0; k + 1 < n; k++)
            {
                float e = h[k + 1] - h[k], lim = MaxGrade * d[k]; if (Mathf.Abs(e) <= lim + 1e-4f || (fix[k] && fix[k + 1])) continue; any = true;
                float over = (Mathf.Abs(e) - lim) * Mathf.Sign(e);
                if (fix[k]) h[k + 1] -= over; else if (fix[k + 1]) h[k] += over; else { h[k] += over * 0.5f; h[k + 1] -= over * 0.5f; }
            }
            if (!any) break;
        }
        int made = 0;
        for (int k = 0; k + 1 < n; k++)
        {
            int a = line[k], b = line[k + 1]; if (!Linked(a, b) || Underpass(a) || Underpass(b)) continue; float g = Mathf.Abs(h[k + 1] - h[k]) / Mathf.Max(1f, d[k]);
            if (g > MaxGrade + 0.02f && g <= StairGrade && Stairs.Add(Key(a, b))) made++;
        }
        return made;
    }
    // voor de test: per vak van een net gelegde lijn wat de zoekers ervan vinden
    public static string LineDebug(List<int> line)
    {
        var sb = new System.Text.StringBuilder();
        for (int k = 0; k < line.Count; k++)
        {
            int c = line[k]; sb.Append("   ").Append(c).Append(IsWalk(c) ? " weg" : " GEEN WEG").Append(Underpass(c) ? " onderdoorgang" : "").Append(IsRot(c) ? " rotonde" : "").Append(preCarve.ContainsKey(c) ? " preCarve" : "").Append(preLevel.TryGetValue(c, out var pl) ? " preLevel " + pl.ToString("0.0") : "").Append(levelled.Contains(c) ? " niveau" : "").Append(" terrein ").Append(Terrain(c).ToString("0.0")).Append(" partners");
            foreach (int p in Partners(c)) sb.Append(' ').Append(p);
            if (k + 1 < line.Count) sb.Append(LineStep(c, line[k + 1]) ? "" : " GEEN STAP").Append(Linked(c, line[k + 1]) ? "" : " NIET VERBONDEN");
            if (k > 0 && k + 1 < line.Count) sb.Append(Straight(line[k - 1], c, line[k + 1]) ? "" : " niet recht");
            sb.AppendLine();
        }
        return sb.ToString();
    }
    public static string ProfileAll(List<List<int>> lines)
    {
        if (Stairs.Count > 0) Stairs.RemoveWhere(k => !Links.Contains(k));   // een trap verdwijnt met zijn verbinding
        // recht door de berg (StraightSearch) en van hoog naar laag: de afdalende brug (DropSearch), voor het profiel: de lange verbinding telt daarin mee
        int drop = 0, recht = 0, water = 0;
        foreach (var line in lines)
        {
            if (line == null || line.Count < 4) continue;
            System.Func<int, bool> Step = k => LineStep(line[k], line[k + 1]) && Linked(line[k], line[k + 1]);
            System.Func<int, bool> Inner = k => { int c = line[k], p = line[k - 1], q = line[k + 1]; var ps = Partners(c); return Plain(c) && ps.Count == 2 && ps.Contains(p) && ps.Contains(q) && Straight(p, c, q); };
            System.Func<int, bool> Free = k => { int c = line[k]; if (!Plain(c)) return false; foreach (int j in Planet.Cells[c].nb) if (Underpass(j)) return false; foreach (int p in Partners(c)) if (!line.Contains(p)) return false; return true; };
            float Land(int c) => preLevel.TryGetValue(c, out var pl) ? pl : Terrain(c);   // het terrein van voor de weg (SetLevel legde over water een dam)
            var ext = WaterLine(line, out _);   // met het bestaande wegeind waar de lijn recht op aansluit erbij
            System.Func<int, bool> StepW = k => LineStep(ext[k], ext[k + 1]) && Linked(ext[k], ext[k + 1]);
            System.Func<int, bool> InnerW = k => { int c = ext[k], p = ext[k - 1], q = ext[k + 1]; var ps = Partners(c); return Plain(c) && ps.Count == 2 && ps.Contains(p) && ps.Contains(q) && Straight(p, c, q); };
            foreach (var (i, j) in WaterSearch(ext, Plain, StepW, InnerW, k => !Underpass(ext[k]), Land, out var hw))   // over het water: een brug
            { RoadBase[ext[i]] = hw[i]; RoadBase[ext[j]] = hw[j]; Span(ext, i, j); SpanLinks.Add(Key(ext[i], ext[j])); water++; }
            foreach (var (i, j) in StraightSearch(line, Plain, Step, Inner, Free, out var hs))
            {
                for (int k = i; k >= 0 && Free(k) && (k == i || hs[k] < Terrain(line[k]) - 0.05f); k--) RoadBase[line[k]] = hs[k];   // de insnijding voor het portaal: tot waar de rechte lijn het terrein raakt
                for (int k = j; k < line.Count && Free(k) && (k == j || hs[k] < Terrain(line[k]) - 0.05f); k++) RoadBase[line[k]] = hs[k];
                Span(line, i, j); SpanLinks.Remove(Key(line[i], line[j])); recht++;
            }
            foreach (var (i, j) in DropSearch(line, Plain, Step, Inner, Free, out _)) { Span(line, i, j); SpanLinks.Add(Key(line[i], line[j])); drop++; }
        }
        if (drop > 0) LastNote = drop == 1 ? "Een brug van hoog naar laag" : drop + " bruggen van hoog naar laag";
        if (water > 0) LastNote = water == 1 ? "Een brug over het water" : water + " bruggen over het water";
        if (recht > 0) LastNote = recht == 1 ? "Een tunnel recht door de berg" : recht + " tunnels recht door de berg";
        if (RoadBase.Count > 0) foreach (int c in new List<int>(RoadBase.Keys)) if (!IsWalk(c)) RoadBase.Remove(c);
        var idx = new Dictionary<int, int>(); var cells = new List<int>();
        foreach (var kv in Cells) if (IsWalk(kv.Key)) cells.Add(kv.Key);
        cells.Sort(); for (int k = 0; k < cells.Count; k++) idx[cells[k]] = k;   // vaste volgorde: het rechttrekken hangt van de volgorde af, en na laden lag Cells anders (vier vakken bij een dalbrug kwamen tot 0,3 m anders uit)
        int n = cells.Count; if (n == 0) return "geen wegen"; var h = new float[n]; var fix = new bool[n];
        // vast: onder of naast een brug (de hoogte komt uit de brug), en een kruispunt (dat blijft op het terrein: anders komt het in de
        // berg te liggen en eindigen drie tunnels met hun portalen op elkaar; de armen krijgen de tunnels en bruggen)
        for (int k = 0; k < n; k++) { int c = cells[k]; h[k] = RoadBase.TryGetValue(c, out float rb) ? rb : Terrain(c); if (Underpass(c)) fix[k] = true; foreach (int j in Planet.Cells[c].nb) if (Underpass(j)) fix[k] = true; }
        var junction = new bool[n]; for (int k = 0; k < n; k++) junction[k] = Partners(cells[k]).Count >= 3;
        var links = new List<(int a, int b, float d)>(); var seen = new HashSet<long>();
        foreach (int c in cells) foreach (int p in Partners(c))
        {
            if (!idx.ContainsKey(p) || !seen.Add(Key(Mathf.Min(c, p), Mathf.Max(c, p)))) continue;
            if (IsStair(c, p)) continue;   // een trap mag steil zijn: de einden blijven waar ze zijn
            links.Add((idx[c], idx[p], Vector3.Angle(Planet.Cells[c].dir, Planet.Cells[p].dir) * Mathf.Deg2Rad * Planet.R));
        }
        links.Sort((x, y) => { int ax = Mathf.Min(x.a, x.b), ay = Mathf.Min(y.a, y.b); if (ax != ay) return ax.CompareTo(ay); int bx = Mathf.Max(x.a, x.b), by = Mathf.Max(y.a, y.b); if (bx != by) return bx.CompareTo(by); return x.a.CompareTo(y.a); });
        for (int it = 0; it < 4000; it++)
        {
            bool any = false;
            foreach (var l in links)
            {
                float e = h[l.b] - h[l.a], lim = MaxGrade * l.d; if (Mathf.Abs(e) <= lim + 1e-4f) continue; any = true;
                float over = (Mathf.Abs(e) - lim) * Mathf.Sign(e);
                if (fix[l.a] && fix[l.b]) continue;
                if (fix[l.a]) h[l.b] -= over; else if (fix[l.b]) h[l.a] += over; else { h[l.a] += over * 0.5f; h[l.b] -= over * 0.5f; }
            }
            // (geprobeerd: een kruispunt bij het terrein houden, hooguit 9 m eronder of 6 m erboven, zodat het niet in de berg komt te liggen
            // met drie portalen op elkaar; dat gaf 45 te steile stukken tussen kruispunten die dicht bij elkaar liggen. Nu: een kruispunt
            // in de berg is een ondergronds kruispunt, dat hoort als één koker met een splitsing getekend te worden, stap 4c)
            if (!any) break;
        }
        var oldH = new Dictionary<int, float>(RoadH);   // (om alleen de vakken aan te raken die veranderen: op een volle planeet scheelt dat een hele herbouw per klik)
        RoadH.Clear(); for (int k = 0; k < n; k++) RoadH[cells[k]] = h[k];
        // de kunstwerken per lijn, aan het profiel
        bool Plain(int c) => IsWalk(c) && !Underpass(c) && !IsRot(c) && !preCarve.ContainsKey(c);
        float Ground(int c) => Terrain(c);
        float Road(int c) => RoadH.TryGetValue(c, out var v) ? v : G(c);
        int tun = 0, brug = 0;
        foreach (var line in lines)
        {
            if (line == null || line.Count < 4) continue;
            System.Func<int, bool> Step = k => LineStep(line[k], line[k + 1]) && Linked(line[k], line[k + 1]);
            System.Func<int, bool> Inner = k => { int c = line[k], p = line[k - 1], q = line[k + 1]; var ps = Partners(c); return Plain(c) && ps.Count == 2 && ps.Contains(p) && ps.Contains(q) && Straight(p, c, q); };
            var done = BoreSearch(line, Plain, Step, Inner, Road, Ground);
            foreach (var (i, j) in done) { Span(line, i, j); SpanLinks.Remove(Key(line[i], line[j])); tun++; }
            var dals = BoreSearch(line, Plain, Step, Inner, Road, Ground, true);
            foreach (var (i, j) in dals) { Span(line, i, j); SpanLinks.Add(Key(line[i], line[j])); brug++; }
        }
        // het terrein blijft; de wegvakken lezen hun hoogte uit RoadH (G), de hoogtefunctie legt het wegdek en het talud erop
        foreach (int c in new List<int>(RoadH.Keys)) if (!IsWalk(c)) RoadH.Remove(c);   // (de vakken in een tunnel of onder een brug zijn weer terrein)
        void TouchAround(int c) { Touch(c); foreach (int j in Planet.Cells[c].nb) Touch(j); }
        foreach (var kv in RoadH) if (!oldH.TryGetValue(kv.Key, out var o) || Mathf.Abs(o - kv.Value) > 1e-3f) TouchAround(kv.Key);
        foreach (var kv in oldH) if (!RoadH.ContainsKey(kv.Key)) TouchAround(kv.Key);
        int stapel = 0, onder = 0; foreach (var kv in RoadH) { int np = 0; foreach (int j in Partners(kv.Key)) if (IsBore(kv.Key, j)) np++; if (np >= 3) stapel++; if (np >= 2 && Terrain(kv.Key) > kv.Value + 9f) onder++; }
        return RoadH.Count + " wegvakken op hooguit " + Mathf.RoundToInt(MaxGrade * 100f) + "%, " + (tun + recht) + " tunnels, " + (brug + drop + water) + " bruggen, " + stapel + " kruispunten met drie portalen, " + onder + " vakken met twee portalen diep in de berg";
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
                if (!seen.Add(Key(Mathf.Min(a, b), Mathf.Max(a, b))) || IsStair(a, b)) continue; total++;   // (een trap telt niet als helling)
                float dist = Vector3.Angle(Planet.Cells[a].dir, Planet.Cells[b].dir) * Mathf.Deg2Rad * Planet.R; if (dist < 1f) continue;
                float g = Mathf.Abs(G(a) - G(b)) / dist; if (g > max) max = g;
                if (g > MaxGrade + 0.005f) { over++; detail?.Append("  steil ").Append(a).Append('-').Append(b).Append(' ').Append((g * 100f).ToString("0")).Append("% ").Append(IsBore(a, b) ? (SpanLinks.Contains(Key(a, b)) ? "dalbrug" : "tunnel") : IsNeighbour(a, b) ? "recht" : "schuin").Append(" G ").Append(G(a).ToString("0.0")).Append('/').Append(G(b).ToString("0.0")).Append(" niveau ").Append(levelled.Contains(a) || levelled.Contains(b)).Append(" partners ").Append(Partners(a).Count).Append('/').Append(Partners(b).Count).AppendLine(); }
            }
        }
        return (max, over, total);
    }
}
