using System.Collections.Generic;
using UnityEngine;

// Wegen en paden op een niveau: WalkLevel, SetLevel, DipUnder, ongelijkvloerse kruisingen, UpdateCuts en SurfaceH.
public static partial class PlanetCity
{
    // de hoogte van het maaiveld rond een pad: het gemiddelde van de buren die geen weg of pad zijn (null: er zijn er geen)
    static float? PadGround(int i) { float s = 0f; int n = 0; foreach (int j in Planet.Cells[i].nb) if (!IsWalk(j) && !Underpass(j)) { s += G(j); n++; } return n > 0 ? s / n : (float?)null; }
    // het niveau van een weg- of padvak ten opzichte van het maaiveld eromheen, in halve niveaus (0: gewoon, of een vak dat een geul omlaag zette)
    static int WalkLevel(int i)
    {
        if (!IsWalk(i) || Underpass(i) || preCarve.ContainsKey(i) || BorePortal(i)) return 0; var pg = PadGround(i); if (!pg.HasValue) return 0;   // een portaal van een tunnel ligt naast de berg, niet in een geul (de bak tot het portaal: BoreSegs)
        float d = (G(i) - pg.Value) / StepH; return d <= -0.5f ? Mathf.Min(-1, Mathf.RoundToInt(d)) : d >= 0.5f ? Mathf.Max(1, Mathf.RoundToInt(d)) : 0;
    }
    // Wegen en paden op een niveau zetten (Page Up/Down): lv halve niveaus (StepH) boven of onder het maaiveld eromheen. Lager: wat de
    // lijn kruist blijft liggen en de nieuwe weg of het pad gaat eronderdoor (een vak dat al onder een weg door ging, zakt mee).
    // Hoger: een bestaande weg die de lijn recht kruist, gaat onder de nieuwe weg door. Zo past één niveau lager of hoger altijd
    // precies onder of over een weg op het maaiveld. Een half niveau is te weinig om onder of over een weg te gaan: daar wordt het
    // een kruispunt (de weg helt er naartoe).
    public static float LevelTarget(int c, int lv) { float t = (PadGround(c) ?? G(c)) + lv * StepH; return lv >= 0 ? Mathf.Max(t, DryRoad) : t; }
    public static float DryRoad => SeaLevel + 0.6f;   // de laagste hoogte van een weg of pad op het maaiveld (60 cm boven de zee)   // de hoogte van vak c op niveau lv (voor het voorbeeld)
    // Voor het voorbeeld tijdens het slepen: wordt vak c van de lijn een brug? +1: de nieuwe weg gaat erover (dek op deck), -1: de
    // nieuwe gaat eronderdoor (de bestaande weg is het dek), 0: niets. Dezelfde regels als SetLevel, Place en PlaceLine.
    public static int CrossingPreview(int c, List<int> line, int lv, string type, out float deck)
    {
        deck = G(c); if (!IsWalk(c) || Underpass(c)) return 0; bool road = type == "weg";
        var other = new List<int>(); foreach (int j in Partners(c)) { if (!IsWalk(j) || line.Contains(j)) continue; int n = 0; foreach (int x in Partners(j)) if (x != c) n++; if (n >= 1) other.Add(j); }
        if (other.Count < 2) return 0;
        float target = LevelTarget(c, lv), diff = target - G(c);
        if (diff <= -(LevelH - 0.1f)) return -1;
        if (diff >= LevelH - 0.1f) { if (!road) return 0; deck = target; return 1; }
        if (road && IsPath(c)) { deck = target; return 1; }   // een weg over een pad: het pad gaat eronderdoor
        if (!road && IsRoad(c)) return -1;                    // een pad onder een weg door
        return 0;
    }
    // Een los geklikt vak op een niveau: kan dat hier? Niet op een weg of pad dat op een andere hoogte ligt, en niet naast een weg
    // op een andere hoogte die daar gewoon doorloopt (een kruising maak je door te slepen, dan gaat de weg eronderdoor of eroverheen).
    // Naast het eind van een weg mag wel: dat wordt de helling.
    public static string LevelClickProblem(int c, int lv, string type)
    {
        float target = LevelTarget(c, lv);
        if (IsWalk(c) && !Underpass(c) && Mathf.Abs(G(c) - target) > 0.3f) return "Dit vak is al een weg of pad op een andere hoogte: sleep de nieuwe eroverheen, dan gaat die eronderdoor of eroverheen";
        if (Underpass(c)) return "Dit is al een brug of onderdoorgang";
        foreach (int j in ClickLinks(c))
        {
            if (!IsWalk(j) || Underpass(j) || Mathf.Abs(G(j) - target) <= StepH + 0.05f) continue;
            int n = 0; foreach (int k in Partners(j)) if (k != c) n++;
            if (n >= 2) return "De weg hiernaast ligt op een andere hoogte en loopt door: sleep om eronderdoor of eroverheen te gaan";
        }
        return null;
    }
    // Wegen en paden die net gelegd zijn op hun niveau zetten (Page Up/Down): lv halve niveaus (StepH) boven of onder het
    // maaiveld eromheen, ook lv 0. Waar de lijn een bestaande weg kruist, beslist het hoogteverschil tussen de nieuwe en de
    // bestaande weg: minstens een heel niveau lager → de nieuwe gaat eronderdoor, minstens een heel niveau hoger → de bestaande
    // gaat eronderdoor (als die recht kruist), minder → een kruispunt op de hoogte van de bestaande weg (de nieuwe helt ernaartoe).
    // Een bestaande weg wordt dus nooit verzet. Een pad onder een weg door regelt Place al (onderdoorgang); een pad hoger dan een
    // weg kan nog niet (loopbrug).
    // Ongelijkvloers op het maaiveld (U, of een pad dat onder een weg door gaat): de lijn duikt alleen bij een kruising onder de
    // andere weg door, als een weg op een niveau (zodat de bak, de koker en het betonblok van het model overpass_path gelden in plaats
    // van de oude portaalgeul): de vakken naast de kruising een niveau lager, de vakken daarnaast een half niveau (de helling)
    public static void DipUnder(List<int> line)
    {
        if (line == null || line.Count < 3) return;
        var depth = new Dictionary<int, int>();   // index in de lijn → halve niveaus omlaag (2 = een niveau)
        bool Own(int c) { foreach (int j in Partners(c)) if (!line.Contains(j)) return false; return true; }   // hoort het vak ook bij een andere weg (kruispunt, aansluiting), dan blijft het liggen
        void Mark(int k, int d) { if (k < 0 || k >= line.Count || Underpass(line[k]) || !Own(line[k])) return; depth[k] = Mathf.Max(depth.TryGetValue(k, out var o) ? o : 0, d); }
        for (int k = 0; k < line.Count; k++)
        {
            int c = line[k];
            bool cross = Underpass(c) && ((PadUnder(c) && ((k > 0 && IsPath(line[k - 1])) || (k + 1 < line.Count && IsPath(line[k + 1])))) || (k > 0 && k + 1 < line.Count && TunnelLinks.Contains(Key(line[k - 1], line[k + 1]))));
            if (cross)
            {
                Mark(k - 1, 2); Mark(k + 1, 2); Mark(k - 2, 1); Mark(k + 2, 1);
                Low[c] = Mathf.Min(Low.TryGetValue(c, out var lo) ? lo : G(c), G(c) - LevelH); Touch(c);
            }
            if (k > 0 && LowerLinks.Contains(Key(line[k - 1], c))) { Mark(k - 1, 2); Mark(k, 2); Mark(k - 2, 1); Mark(k + 1, 1); }   // schuin onder een weg door
        }
        foreach (var kv in depth)
        {
            int c = line[kv.Key]; if (!IsWalk(c)) continue; float b = PadGround(c) ?? G(c);
            LevelTerr(c, b - kv.Value * StepH); Touch(c); foreach (int j in Partners(c)) Touch(j);
        }
    }
    public static void SetLevel(List<int> line, int lv, string type)
    {
        if (line == null) return; float carry = float.NaN; bool road = type == "weg";
        for (int k = 0; k < line.Count; k++)
        {
            int c = line[k]; if (!IsWalk(c)) continue;
            float? pg = PadGround(c); float b = pg ?? (float.IsNaN(carry) ? G(c) : carry); carry = b; float target = b + lv * StepH;
            if (lv >= 0) target = Mathf.Max(target, DryRoad);   // een weg of pad op het maaiveld ligt nooit onder water: over de zee een dam (of een brug, BoreLine)
            if (Underpass(c))   // een kruising die het leggen net maakte (of die er al was): de nieuwe weg of het pad eronder op zijn niveau
            {
                bool padUnder = PadUnder(c);
                if (lv < 0 && road && padUnder) LastNote = "Een weg lager dan een pad: het pad gaat er nog onderdoor (een loopbrug kan nog niet)";
                else if (lv > 0 && !road && padUnder) LastNote = "Een pad hoger dan het maaiveld gaat nog onder een weg door (een loopbrug kan nog niet)";
                else if (lv > 0 && road && target > G(c) + 0.1f)   // het dek omhoog; eronder blijft de weg of het pad op zijn eigen hoogte (de geul wordt ondieper)
                {
                    float under = UnderLevel(c); LevelTerr(c, target); Low[c] = Mathf.Min(under, G(c) - UnderClear); Touch(c);
                    foreach (int j in Partners(c)) Touch(j);
                }
                else if (Low.TryGetValue(c, out var lo) && target < G(c) - LevelH + 0.1f) { Low[c] = Mathf.Min(lo, target); Touch(c); }
                continue;
            }
            // een bestaande weg (of pad) die hier gekruist wordt: zijn eigen verbindingen, niet die van de lijn
            // (een buur telt mee als die zelf verder loopt; een vak dat alleen aan c hangt is het vorige vak van een weg die vak voor
            // vak gebouwd wordt). Eén zo'n buur: de lijn sluit aan op het eind van een weg, dit vak wordt de helling. Twee of meer:
            // een weg loopt hier door. Aan het begin of eind van de lijn is dat een aansluiting op die weg, op zijn hoogte.
            var other = new List<int>();
            foreach (int j in Partners(c))
            {
                if (!IsWalk(j) || line.Contains(j)) continue; int n = 0; foreach (int x in Partners(j)) if (x != c) n++;
                if (n >= 1 || Underpass(j)) other.Add(j);
            }
            bool existing = other.Count >= 2 && Mathf.Abs(G(c) - target) > 0.3f;
            if (!existing) { if (Mathf.Abs(G(c) - target) > 0.005f || preCarve.ContainsKey(c)) LevelTerr(c, target); Touch(c); continue; }   // ook als de geul van een brug het vak al op die hoogte had gezet: dan is het een vak op een niveau, geen uitgegraven vak
            if (k == 0 || k == line.Count - 1) { if (Mathf.Abs(G(c) - target) >= LevelH - 0.1f) LastNote = "De weg sluit aan op een weg op een andere hoogte: hij helt ernaartoe"; continue; }
            float diff = target - G(c);
            if (diff <= -(LevelH - 0.1f))   // de nieuwe gaat onder de bestaande door
            {
                if (!road) continue;   // een pad onder een weg: Place maakte al een onderdoorgang, anders blijft het een kruising
                int p = k > 0 ? line[k - 1] : -1, q = k + 1 < line.Count ? line[k + 1] : -1;
                if (p < 0 || q < 0 || MidCell(p, q) != c || Under.ContainsKey(c) || other.Count != 2 || MidCell(other[0], other[1]) != c) {   // ook de bestaande weg recht door c (geen brug in een bocht)
                     LastNote = "Hier kon de weg niet onder de bestaande door (sleep er in een rechte lijn overheen): het werd een kruispunt"; continue; }
                foreach (int j in new[] { p, q }) { Links.Remove(Key(c, j)); OneWay.Remove(DKey(c, j)); OneWay.Remove(DKey(j, c)); partners[c].Remove(j); partners[j].Remove(c); Touch(j); }
                LinkTunnel(p, q); if (DragOneWay) OneWay.Add(DKey(p, q));
                Under[c] = "weg"; Low[c] = target; Touch(c);
            }
            else if (diff >= LevelH - 0.1f)   // de bestaande gaat onder de nieuwe door
            {
                if (!road) { LastNote = "Een pad over een weg heen kan nog niet (loopbrug): het werd een kruising"; continue; }
                int pp = k > 0 ? line[k - 1] : -1, qq = k + 1 < line.Count ? line[k + 1] : -1;
                if (pp < 0 || qq < 0 || MidCell(pp, qq) != c) { LastNote = "Een brug moet recht zijn: sleep de nieuwe weg in een rechte lijn over de bestaande (het werd een kruispunt)"; continue; }
                if (!PassUnder(c, other)) continue;
                LevelTerr(c, target); Touch(c);
            }
            else LastNote = "Te weinig hoogteverschil met de weg die je kruist (minstens een heel niveau): het werd een kruispunt op de hoogte van die weg";
        }
        CarvePaths();
    }
    // de eigen hoogte van wat onder brugvak c door gaat: zijn buren buiten de geul (hun hoogte van voor de geul)
    static float UnderLevel(int c)
    {
        float s = 0f; int n = 0; var ends = Under.TryGetValue(c, out var u) && u == "weg" ? TunnelEnds(c) : null;
        if (ends == null) { ends = new List<int>(); foreach (int j in Partners(c)) if (IsPath(j)) ends.Add(j); }
        foreach (int j in ends) { s += preCarve.TryGetValue(j, out var pre) ? pre : G(j); n++; }
        return n > 0 ? s / n : (PadGround(c) ?? G(c));
    }
    // een bestaande weg die hier gekruist wordt (other: zijn twee buren) gaat onder vak c door: een tunnel van de ene buur naar de
    // andere, zoals ongelijkvloers maar andersom; c wordt het dek. Lukt alleen als hij recht kruist.
    // loopt de weg (of het pad) recht door vak c? Een brug kan niet in een bocht of op een kruispunt liggen.
    static bool StraightThrough(int c) { var w = new List<int>(); foreach (int j in Partners(c)) if (IsWalk(j)) w.Add(j); return w.Count == 2 && MidCell(w[0], w[1]) == c; }
    static bool PassUnder(int c, List<int> other)
    {
        if (Under.ContainsKey(c)) return false;
        if (other.Count != 2 || MidCell(other[0], other[1]) != c) { LastNote = "Hier kon de bestaande weg niet onder de nieuwe door (hij kruist niet recht, of het is een kruispunt): het werd een kruispunt"; return false; }
        int a = other[0], b = other[1], f = Flow(a, c);
        foreach (int j in other) { Links.Remove(Key(c, j)); OneWay.Remove(DKey(c, j)); OneWay.Remove(DKey(j, c)); partners[c].Remove(j); partners[j].Remove(c); Touch(j); }
        LinkTunnel(a, b); if (f > 0) OneWay.Add(DKey(a, b)); else if (f < 0) OneWay.Add(DKey(b, a));
        Under[c] = "weg"; Low[c] = G(c); return true;
    }
    // hoort vak i bij de weg óver een onderdoorgang (een buur van het brugvak op de hoogte van het dek)? Die blijft uit de geul.
    static bool UpperRoad(int i) { if (!IsRoad(i)) return false; foreach (int j in Partners(i)) if (Underpass(j) && Mathf.Abs(G(j) - G(i)) < 0.3f) return true; return false; }
    // padvakken in een geul op de hoogte van de geul zetten (alleen omlaag)
    public static void CarvePaths()
    {
        cuts.Clear(); deckSide.Clear(); CollectCuts(cuts, deckSide); if (cuts.Count == 0) return;
        foreach (var kv in new List<int>(Cells.Keys)) if (IsWalk(kv) && !Underpass(kv) && !UpperRoad(kv) && !preLevel.ContainsKey(kv)) { float v = Carve(Planet.Cells[kv].dir); if (v < G(kv) - 0.01f) { if (!preCarve.ContainsKey(kv)) preCarve[kv] = G(kv); SetTerr(kv, v); } }   // paden en de onderste weg
    }
    // bij het bijwerken: de geulen opnieuw bepalen; wat verandert, wordt opnieuw getekend
    static string cutSig = "";
    static void UpdateCuts0()
    {
        var orphan = new List<int>(); foreach (var kv in Under) if (kv.Value == "weg" && TunnelEnds(kv.Key).Count == 0) orphan.Add(kv.Key);   // brug zonder weg eronder: weg ermee
        foreach (int i in orphan) { if (Low.TryGetValue(i, out var lo0) && !IsRoad(i)) SetTerr(i, lo0); Under.Remove(i); Low.Remove(i); Touch(i); }
        cuts.Clear(); deckSide.Clear(); CollectCuts(cuts, deckSide); RestoreCarve();
        var now = new HashSet<int>(); var sb = new System.Text.StringBuilder(); var keys = new Dictionary<string, Vector3>();
        foreach (var q in cuts) { string ck = q.p.ToString("F2") + q.floor.ToString("F2") + q.ground.ToString("F2"); sb.Append(ck).Append('|'); keys[ck] = q.p; }
        foreach (int lc in levelled) now.Add(lc);   // een vak op een niveau: zijn buren rekenen hun hoekpunten zonder zijn hoogte (de geul of dijk komt uit de stroken)
        foreach (var kv in Cells) if (IsWalk(kv.Key) && !Underpass(kv.Key) && !UpperRoad(kv.Key) && cuts.Count > 0) { float v = Carve(Planet.Cells[kv.Key].dir); if (v < float.MaxValue && G(kv.Key) <= v + 0.02f && v < MaxGround(kv.Key) - 0.05f) now.Add(kv.Key); }
        string sig = sb.ToString(); if (sig == cutSig && now.SetEquals(carved)) return;
        cutSig = sig;
        // alleen opnieuw tekenen wat veranderde (vroeger de hele planeet, bij elke gumstreek): rond een geul die kwam of verdween, en
        // drie stappen rond een vak dat in of uit een geul of niveau ging
        var gone = new List<Vector3>(); foreach (var kv in cutKeys) if (!keys.ContainsKey(kv.Key)) gone.Add(kv.Value);
        foreach (var kv in keys) if (!cutKeys.ContainsKey(kv.Key)) gone.Add(kv.Value);
        cutKeys = keys;
        if (gone.Count > 0)
            for (int i = 0; i < Planet.Cells.Length; i++) { Vector3 w = Planet.Cells[i].dir * Planet.R; foreach (var gp in gone) if ((w - gp).sqrMagnitude < 75f * 75f) { Touch(i); break; } }
        var diff = new HashSet<int>(now); diff.SymmetricExceptWith(carved);
        var seen = new HashSet<int>(); var ring = new List<int>(diff);
        for (int r = 0; r < 4 && ring.Count > 0; r++) { var next = new List<int>(); foreach (int i in ring) if (seen.Add(i)) { Touch(i); foreach (int j in Planet.Cells[i].nb) next.Add(j); } ring = next; }
        carved.Clear(); carved.UnionWith(now);
    }
    static void UpdateCuts() { UpdateCuts0(); CollectBores(); }   // ook de tunnels door een berg (PlanetTunnels.cs)
    static Dictionary<string, Vector3> cutKeys = new Dictionary<string, Vector3>();
    static float MaxGround(int i) { float g = float.MinValue; foreach (var q in cuts) if (!q.fill) g = Mathf.Max(g, q.ground); return g; }
    public static float FloorH(int i) { float m = Mathf.Min(G(i), MinCorner(i)); return Low.TryGetValue(i, out var lo) ? Mathf.Min(m, lo) : m; }   // het laagste punt in vak i
    public static float MinCorner(int i) { float m = G(i); for (int k = 0; k < Planet.Cells[i].corners.Length; k++) m = Mathf.Min(m, CornerH(i, k)); return m; }
    // hoogte van het oppervlak van vak i (maaiveld, of de weg die er helt) onder een wereldpunt
    public static float SurfaceH(int i, Vector3 world) { if (IsPath(i)) return PathSurfaceH(i, world); var fr = new Frame(i); Vector3 d = world - fr.o; return G(i) + fr.H(Vector3.Dot(d, fr.e), Vector3.Dot(d, fr.n)); }
    // idem voor iemand die van vak 'from' komt of erheen gaat: op het pad onder een brug is dat de hoogte van het pad
    public static float SurfaceH(int i, int from, Vector3 world)
    {
        if (!UnderLink(i, from)) return SurfaceH(i, world);
        var fr = new Frame(i, false, true); Vector3 d = world - fr.o; return LevelFor(i, from) + fr.H(Vector3.Dot(d, fr.e), Vector3.Dot(d, fr.n));
    }
}
