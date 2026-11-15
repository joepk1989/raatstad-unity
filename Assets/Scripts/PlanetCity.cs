using System;
using System.Collections.Generic;
using UnityEngine;

// De stad op de planeet: per vak een stapel blokken; een vak waarvan het onderste blok "weg" is, is een wegvak.
// Wegvakken sluiten vanzelf op elkaar aan (asfalt van hart naar hart, stoep eromheen). Alles wordt in het
// assenstelsel van het vak gebouwd, zodat gebouwen overal recht op de bol staan. De bodem is één mesh per
// deel (128 vakken); blokken en wegen worden per deel opnieuw gemaakt als er iets verandert.

public static partial class PlanetCity
{
    public static Dictionary<int, List<string>> Cells = new Dictionary<int, List<string>>();
    public const int ChunkN = 128;   // klein genoeg dat een klik maar een klein stuk opnieuw tekent
    static Transform root, groundRoot;
    static readonly Dictionary<int, Transform> chunks = new Dictionary<int, Transform>();
    static readonly HashSet<int> dirty = new HashSet<int>();

    public static void ResetAll() { Cells = new Dictionary<int, List<string>>(); Terr.Clear(); RoadH.Clear(); CornerOfs.Clear(); CenterOfs.Clear(); cnA = cnB = edN = null; Links.Clear(); partners.Clear(); Style.Clear(); Bike.Clear(); Rot.Clear(); DragBike = false; Under.Clear(); Low.Clear(); OneWay.Clear(); TunnelLinks.Clear(); LowerLinks.Clear(); DragUnder = false; DragLevel = 0; DragStyle = 0; DragOneWay = false; Lights = true; StartVer = 0; StartCell = -1; startFresh = false; chunks.Clear(); groundChunks.Clear(); dirty.Clear(); root = null; groundRoot = null; grid = null; equator = null; GridOn = true; Slot = 1; roadPieces.Clear(); roadRoot = null; }
    public static bool IsRoad(int i) => Cells.TryGetValue(i, out var l) && l.Count > 0 && l[0] == "weg";
    public static bool IsPath(int i) => Cells.TryGetValue(i, out var l) && l.Count == 1 && l[0] == "pad";
    public static bool IsWalk(int i) => IsRoad(i) || IsPath(i);

    // een heel vak leegmaken (rechtermuisknop)
    // De gum over een brug. Een gumstreek die langs de weg of het pad onder een brug loopt, haalt die onderdoorgang weg en laat
    // de brug staan als gewoon stuk van de bovenste weg (het terrein blijft). Daarvoor onthoudt de streek bij elk gewist vak welke
    // brug erbij hoort (gumSkip); komt de streek daarna op die brug, dan gaat alleen het eronder weg. Een losse klik op een brug
    // (from < 0) haalt de brug zelf weg (Clear).
    static readonly HashSet<int> gumSkip = new HashSet<int>();
    public static bool ClearFrom(int i, int from)
    {
        if (from < 0) gumSkip.Clear();
        if (gumSkip.Contains(i) && Under.TryGetValue(i, out var u) && (u == "weg" || u == "pad"))
        {
            if (u == "weg") { var ends = new List<long>(); foreach (long k in TunnelLinks) { int a = (int)(k >> 20), b = (int)(k & 0xFFFFF); if (MidCell(a, b) == i) ends.Add(k); } foreach (long k in ends) { int a = (int)(k >> 20), b = (int)(k & 0xFFFFF); Links.Remove(k); TunnelLinks.Remove(k); partners[a].Remove(b); partners[b].Remove(a); Touch(a); Touch(b); } }
            else foreach (int j in new List<int>(Partners(i))) if (IsPath(j)) { Links.Remove(Key(i, j)); partners[i].Remove(j); if (partners.TryGetValue(j, out var b)) b.Remove(i); Touch(j); }
            Under.Remove(i); Low.Remove(i); Touch(i); return true;
        }
        // dit vak hoort bij wat onder een brug door gaat: die brug straks overslaan
        foreach (long k in TunnelLinks) { int a = (int)(k >> 20), b = (int)(k & 0xFFFFF); if (a == i || b == i) { int m = MidCell(a, b); if (m >= 0) gumSkip.Add(m); } }
        if (IsPath(i)) foreach (int j in Partners(i)) if (PadUnder(j)) gumSkip.Add(j);
        return Clear(i);
    }
    public static bool Clear(int i)
    {
        if (!Cells.TryGetValue(i, out var l)) return false;
        if (HasModule(i)) return RemoveModuleAt(i);   // een module: het bovenste exemplaar met al zijn vakken (PlanetCity.Modules.cs)
        if (IsHalf(i)) { if (l.Count <= 1) return ClearOver(i); l.RemoveRange(1, l.Count - 1); Touch(i); return true; }   // eerst wat op de vrije helft staat, daarna de schuine weg erover
        if (Under.TryGetValue(i, out var under)) { RestoreUnder(i, under); return true; }   // een brug: het water of de kuil komt terug
        if (IsRot(i)) ClearRoundabout(i);
        bool walk = IsWalk(i); Cells.Remove(i); Style.Remove(i); Bike.Remove(i); Unlink(i); if (walk) RestoreLevel(i); Touch(i); return true;
    }
    public static float StackHeight(int i) { float h = 0f; if (Cells.TryGetValue(i, out var l)) foreach (var t in l) h += Blocks.Height(t); return h; }
    public static int RoadCount() { int n = 0; foreach (var kv in Cells) if (kv.Value.Count > 0 && kv.Value[0] == "weg") n++; return n; }

    public static Dictionary<int, List<string>> Clone(Dictionary<int, List<string>> src)
    {
        var d = new Dictionary<int, List<string>>(); foreach (var kv in src) d[kv.Key] = new List<string>(kv.Value); return d;
    }

    // plaatsen; geeft een melding terug als het niet kan (anders null)
    public static string Place(int i, string type, bool autoLink = true)
    {
        if (HasModule(i)) return "Hier staat een module: rechtermuisknop haalt hem weg";
        if (!Cells.TryGetValue(i, out var l)) { l = new List<string>(); Cells[i] = l; }
        if (TreesYield(l, type)) l.Clear();   // bomen op het terrein wijken voor alles wat je bouwt (een weg of pad door een bos, een huis in een bos)
        if (l.Count == 1 && IsWild(l[0]) && type != l[0]) l.Clear();   // ijs, woestijn, heide, palmen en rotsen wijken voor alles (PlanetWild.cs)
        if (type == "weg")
        {
            if (l.Count == 1 && l[0] == "wegdeel") l.Clear();
            if (l.Count == 1 && (l[0] == "meer" || l[0] == "water" || l[0] == "kuil")) { Under[i] = l[0]; l[0] = "weg"; }   // een brug over water of een uitgraving
            if (l.Count == 1 && l[0] == "pad")   // een weg over een pad: gelijkvloers een oversteek (het pad sluit aan op de stoep); onderdoor met ongelijkvloers, of als de weg op een ander niveau ligt dan het pad
            { if (DragUnder || DragLevel != WalkLevel(i)) MakeUnderpass(i, l); else { l[0] = "weg"; Bike.Remove(i); } }
            if (l.Count > 0 && !(l.Count == 1 && l[0] == "weg")) return "Eerst het vak leegmaken (rechtermuisknop) voor een weg";
            if (l.Count == 0) l.Add("weg");
            SetStyle(i); if (autoLink) { foreach (int j in ClickLinks(i)) Link(i, j); CarvePaths(); }
            LinkRot(i);   // een weg op de ring van een rotonde sluit erop aan
            Touch(i); return null;
        }
        if (type == "rotonde") { if (l.Count == 0) Cells.Remove(i); return PlaceRoundabout(i); }
        if (RotOf(i) >= 0 && !IsRot(i) && type != "pad") { if (l.Count == 0) Cells.Remove(i); return "Hier ligt een rotonde: op de ring kun je niet bouwen"; }
        if (type == "pad" && l.Count == 1 && l[0] == "wegdeel") l.Clear();   // een pad over een schuine weg: het halve wegvak wordt een padvak (de brug komt op de rand)
        if (type == "pad" && l.Count == 1 && l[0] == "weg" && (Underpass(i) || ((DragUnder || DragLevel < 0) && WalkLevel(i) >= 0 && DigUnder(i))))   // een pad onder een weg door: met ongelijkvloers of een lager niveau (niet onder een verdiepte weg: daar kruist het pad op de hoogte van de weg)
        { if (autoLink) { foreach (int j in Planet.Cells[i].nb) if (IsPath(j)) Link(i, j); DigTrench(i); } Touch(i); return null; }
        if (type == "pad" && l.Count == 1 && l[0] == "weg")   // gelijkvloers: het pad sluit aan op de weg (een oversteek)
        { if (autoLink) foreach (int j in ClickLinks(i)) if (IsPath(j)) Link(i, j); Touch(i); return null; }
        if (type == "pad" && l.Count == 0) { l.Add("pad"); LinkRot(i); if (DragBike) Bike.Add(i); if (autoLink) { foreach (int j in ClickLinks(i)) Link(i, j); DigCrossings(); } Touch(i); return null; }
        if (type == "pad" && l.Count == 1 && l[0] == "pad") { if (DragBike && Bike.Add(i)) Touch(i); if (autoLink) foreach (int j in ClickLinks(i)) Link(i, j); Touch(i); return null; }
        if (l.Count > 0 && l[0] == "weg") return "Op een weg kun je niet bouwen; haal de weg eerst weg (rechtermuisknop)";
        if (l.Count > 0 && l[0] == "wegdeel" && !Buildable(type)) return "Hier loopt een weg overheen: op de vrije helft passen alleen gebouwen en parken";
        if (l.Count >= MV2.MAX_LV) return "Hoger kan niet";
        if (l.Count > 0 && (l[l.Count - 1] == "toren" || l[l.Count - 1] == "molen") && type != "water" && type != "meer") return "Op een " + l[l.Count - 1] + " kun je niets meer zetten";
        if (l.Count > 0 && NoStackOn(l[l.Count - 1]) is string why && type != "water" && type != "meer") return why;
        if ((type == "water" || type == "meer") && l.Count > 0) l.Clear();
        if (l.Count > 0 && (l[l.Count - 1] == "water" || l[l.Count - 1] == "meer")) l.Clear();
        l.Add(type); Touch(i); return null;
    }

    // Een gesleepte lijn: de vakken worden weg (of pad) en alleen onderling verbonden, in volgorde
    public static int PlaceLine(List<int> line, string type)
    {
        int ok = 0; LastNote = null;
        // ongelijkvloers: een bestaand wegvak midden in de lijn wordt een brug en de nieuwe weg gaat eronderdoor, tenzij de lijn daar
        // over die weg heen loopt (het vak ervoor of erna hoort bij dezelfde weg): dan wordt het een kruispunt, met een melding
        var under = new List<int>();
        if (type == "weg" && DragUnder)
            for (int k = 1; k + 1 < line.Count; k++)
            {
                int c = line[k], p = line[k - 1], q = line[k + 1]; if (!IsRoad(c)) continue;
                if (DragLevel != 0 && G(c) - ((PadGround(c) ?? G(c)) + DragLevel * StepH) < LevelH - 0.1f) continue;   // de weg die je kruist ligt niet een niveau hoger: geen tunnel, SetLevel maakt er een kruispunt van
                if (TunnelLinks.Contains(Key(p, q)) && !under.Contains(p)) { under.Add(c); continue; }   // daar gaat deze weg al onderdoor (opnieuw of terug gesleept)
                if ((Linked(p, c) || !IsRoad(p)) && (Linked(c, q) || !IsRoad(q)) && (Linked(p, c) || Linked(c, q))) continue;   // dit stuk ligt er al precies zo: niets te doen, geen melding
                if (!Under.ContainsKey(c) && StraightThrough(c) && !Linked(p, c) && !Linked(q, c) && !under.Contains(p) && Adjacent2(p, c) && Adjacent2(c, q) && MidCell(p, q) == c) under.Add(c);
                else LastNote = "Hier kon de weg niet onderdoor (hij loopt daar langs of over een bestaande weg of brug): het werd een kruispunt";
            }
        for (int k = 0; k < line.Count; k++) { if (under.Contains(line[k])) { ok++; continue; } if (Place(line[k], type, false) == null) ok++; }
        var cross = new List<int>();
        for (int k = 1; k < line.Count; k++)
        {
            int a = line[k - 1], b = line[k]; if (!IsWalk(a) || !IsWalk(b) || under.Contains(a) || under.Contains(b)) continue;
            if (type == "weg" && !(DragUnder && (under.Contains(a) || under.Contains(b))))   // schuin kruisen met een andere weg: met ongelijkvloers eronderdoor, anders een kruispunt
            {
                var bt = Between(a, b);
                if (bt.Count == 2 && IsRoad(bt[0]) && IsRoad(bt[1]) && Linked(bt[0], bt[1]))
                {
                    float newH = (PadGround(a) ?? G(a)) + DragLevel * StepH, oldH = Mathf.Max(G(bt[0]), G(bt[1]));
                    if (DragUnder) LowerLinks.Add(Key(a, b));
                    else if (newH - oldH >= LevelH - 0.1f) LowerLinks.Add(Key(bt[0], bt[1]));   // de bestaande weg ligt een niveau lager: hij gaat eronderdoor (een brug), niet verleggen
                    else if (IsNeighbour(a, b))   // een schuine weg springt over de rand a|b: die weg gaat nu door vak a (het kruispunt)
                    {
                        int c1 = bt[0], c2 = bt[1], f = Flow(c1, c2); Links.Remove(Key(c1, c2)); OneWay.Remove(DKey(c1, c2)); OneWay.Remove(DKey(c2, c1)); partners[c1].Remove(c2); partners[c2].Remove(c1); LowerLinks.Remove(Key(c1, c2));
                        Link(c1, a); Link(a, c2); if (f > 0) { OneWay.Add(DKey(c1, a)); OneWay.Add(DKey(a, c2)); } else if (f < 0) { OneWay.Add(DKey(c2, a)); OneWay.Add(DKey(a, c1)); }
                        Touch(c1); Touch(c2);
                    }
                    else   // deze lijn springt zelf over de rand tussen twee wegvakken: door het eerste daarvan (het kruispunt)
                    {
                        // welk van de twee: het vak waar deze weg al doorheen gaat (opnieuw of terug gesleept), anders vast het kleinste nummer
                        int h = Linked(a, bt[0]) && Linked(bt[0], b) ? bt[0] : Linked(a, bt[1]) && Linked(bt[1], b) ? bt[1] : Mathf.Min(bt[0], bt[1]);
                        Link(a, h); Link(h, b); if (DragOneWay) { OneWay.Add(DKey(a, h)); OneWay.Add(DKey(h, b)); }
                        continue;
                    }
                }
            }
            // een schuin pad springt over de rand tussen twee verbonden wegvakken: gelijkvloers sluit het pad aan beide kanten aan op
            // het eerste wegvak (een oversteek); met ongelijkvloers of een lager niveau gaat het eronderdoor (EdgeCrossings)
            if (type == "pad" && !DragUnder && DragLevel >= 0 && !IsNeighbour(a, b))
            {
                var br = Between(a, b);
                if (br.Count == 2 && IsRoad(br[0]) && IsRoad(br[1]) && Linked(br[0], br[1]))
                {
                    int h = Linked(a, br[0]) && Linked(b, br[0]) ? br[0] : Linked(a, br[1]) && Linked(b, br[1]) ? br[1] : Mathf.Min(br[0], br[1]);   // net als bij een weg: steeds hetzelfde vak
                    Link(a, h); Link(b, h); continue;
                }
            }
            // een schuine weg springt over de rand tussen twee verbonden padvakken: gelijkvloers blijft de weg recht en sluit het pad aan
            // beide kanten aan op wegvak a (een oversteek). Met ongelijkvloers, of op een ander niveau, gaat het pad eronderdoor (EdgeCrossings).
            if (type == "weg" && !DragUnder && !IsNeighbour(a, b))
            {
                var bp = Between(a, b);
                if (bp.Count == 2 && IsPath(bp[0]) && IsPath(bp[1]) && Linked(bp[0], bp[1]) && WalkLevel(bp[0]) == DragLevel)
                {
                    int p0 = bp[0], p1 = bp[1]; Links.Remove(Key(p0, p1)); partners[p0].Remove(p1); partners[p1].Remove(p0);
                    Link(p0, a); Link(p1, a); cross.Add(p0); cross.Add(p1);
                }
            }
            Link(a, b);
            if (IsRoad(a) && IsRoad(b))   // eenrichting in de sleeprichting, of weer tweerichting
            {
                OneWay.Remove(DKey(a, b)); OneWay.Remove(DKey(b, a)); if (DragOneWay) OneWay.Add(DKey(a, b));
            }
            if (!IsNeighbour(a, b)) foreach (int h in Between(a, b)) MarkHalf(h);
        }
        foreach (int c in under)   // de nieuwe weg onder het kruisvak door: van het vak ervoor naar het vak erna
        {
            int k = line.IndexOf(c), p = line[k - 1], q = line[k + 1]; LinkTunnel(p, q);
            Under[c] = "weg"; Low[c] = Mathf.Min(G(p), Mathf.Min(G(q), G(c) - UnderClear)); Touch(p); Touch(q);
        }
        foreach (int i in line) Touch(i); foreach (int i in cross) Touch(i);
        CarvePaths();   // de geulen van de onderdoorgangen in de lijn en van schuine kruisingen, één keer voor de hele lijn (rekent over de hele planeet)
        if (line.Count >= 2) { JoinEnd(line[0], line[1], type); JoinEnd(line[line.Count - 1], line[line.Count - 2], type); }
        return ok;
    }
    // Begint of eindigt een gesleepte lijn precies in het verlengde van een doodlopende weg (of pad) van dezelfde soort, dan horen ze
    // bij elkaar: verbinden (anders liggen er twee losse eindes met een kier ertussen). Op een ander niveau wordt het daar een helling.
    static void JoinEnd(int e, int inner, string type)
    {
        if (!IsWalk(e)) return;
        foreach (int x in Planet.Cells[e].nb)
        {
            if (x == inner || MidCell(x, inner) != e || Linked(e, x) || Underpass(x)) continue;   // het vak dat de lijn zou vervolgen
            bool same = type == "weg" ? IsRoad(x) : IsPath(x); if (!same) continue;
            int n = 0; foreach (int j in Partners(x)) if (IsWalk(j)) n++;
            if (n <= 1) { Link(e, x); Touch(e); Touch(x); }   // een doodlopend eind (of los vak): aansluiten
        }
    }

    public static string LastNote;   // een melding over de laatst gelegde lijn (of null)

    public static bool RemoveTop(int i)
    {
        if (!Cells.TryGetValue(i, out var l) || l.Count == 0) return false;
        if (HasModule(i)) return RemoveModuleAt(i);
        if (IsHalf(i) && l.Count <= 1) return ClearOver(i);
        if (Under.TryGetValue(i, out var under)) { RestoreUnder(i, under); return true; }
        bool walk = IsWalk(i); l.RemoveAt(l.Count - 1); if (l.Count == 0) { Cells.Remove(i); Style.Remove(i); Bike.Remove(i); Unlink(i); if (walk) RestoreLevel(i); } Touch(i); return true;
    }

    public static void Touch(int i) { cutsFresh = false; dirty.Add(i / ChunkN); foreach (int j in Planet.Cells[i].nb) dirty.Add(j / ChunkN); if (boreNear.Count > 0 && boreNear.TryGetValue(i, out var bl)) foreach (int b in bl) dirty.Add(bores[b].a / ChunkN); }   // boven een tunnel: het betonblok volgt het terrein
    // Alles weg (lege bouwgrond): de gegevens én alle getekende stukken, zodat er nooit iets van de oude stad blijft staan
    public static void ClearAll()
    {
        Cells = new Dictionary<int, List<string>>(); Terr.Clear(); RoadH.Clear(); CornerOfs.Clear(); CenterOfs.Clear(); Scorch.Clear(); ClearLinks();
        if (root != null) foreach (Transform t in root) UnityEngine.Object.Destroy(t.gameObject);
        chunks.Clear();
        if (roadRoot != null) foreach (Transform t in roadRoot) UnityEngine.Object.Destroy(t.gameObject);
        roadPieces.Clear();
        TouchAll(); Rebuild(); RebuildGrid();
    }
    public static void TouchAll() { for (int c = 0; c * ChunkN < Planet.Cells.Length; c++) dirty.Add(c); }

    public static int Slot = 1;   // drie opslagslots: planeet1.json, planeet2.json, planeet3.json
    // Een stad die nog de ongewijzigde startstad is, wordt bij het laden opnieuw gebouwd als de startstad in de code veranderd is
    // (StartTownVersion omhoog). Zodra je zelf iets bouwt (UserEdit) blijft je stad zoals hij is.
    public const int StartTownVersion = 7;   // 3: de gebouwen van de startstad zijn modules (PlanetStartTown.cs); 5: laanhuizen aan de straat, de weg blijft vrij; 6: een groene rand met bos, heide en meertjes
    public static int StartVer, StartCell = -1; static bool startFresh;
    public static void UserEdit() { if (startFresh) startFresh = false; else StartVer = 0; }
    // Kan blok 'type' op vak i? null = ja, anders de reden. Dezelfde regels als Place (voor blokken, niet voor wegen), zonder iets te veranderen.
    public static string CanPlace(int i, string type)
    {
        if (HasModule(i)) return "Hier staat een module";
        if (type == "rotonde") return RotOf(i) >= 0 ? null : RotFree(i) ? null : "Hier staat iets in de weg";
        if (RotOf(i) >= 0 && !IsRot(i)) return "Hier ligt een rotonde: op de ring kun je niet bouwen";
        if (!Cells.TryGetValue(i, out var l) || l.Count == 0 || TreesYield(l, type)) return null;
        if (type == "pad" && l.Count == 1 && l[0] == "pad") return null;
        if (l[0] == "weg") return "Op een weg kun je niet bouwen; haal de weg eerst weg (rechtermuisknop)";
        if (l[0] == "wegdeel" && !Buildable(type)) return "Hier loopt een weg overheen: op de vrije helft passen alleen gebouwen en parken";
        if (type == "water" || type == "meer") return null;   // water vervangt wat er staat
        if (l[l.Count - 1] == "water" || l[l.Count - 1] == "meer") return null;
        if (l.Count >= MV2.MAX_LV) return "Hoger kan niet";
        if (l[l.Count - 1] == "toren" || l[l.Count - 1] == "molen") return "Op een " + l[l.Count - 1] + " kun je niets meer zetten";
        return NoStackOn(l[l.Count - 1]);
    }
    // alleen bomen op het terrein (een vak met alleen "park"): die maken plaats; een daktuin bovenop een gebouw niet
    static bool TreesYield(List<string> l, string type) => l.Count == 1 && l[0] == "park" && type != "park";
    // op wegen, paden, bomen (park) en daken kun je niets stapelen; null = wel
    static string NoStackOn(string top) => top == "park" ? "Op bomen kun je niets zetten" : top == "dak" ? "Op een dak kun je niets zetten" : top == "pad" ? "Op een pad kun je niet bouwen" : null;
}

// Draaiende molenwieken: harder bij storm, wat sneller bij regen, en een beetje onregelmatig zoals echte wind
public class Wieken : MonoBehaviour
{
    public float Speed = 40f; float gust;
    void Update()
    {
        gust = Mathf.Lerp(gust, Mathf.PerlinNoise(Time.time * 0.15f, Speed) - 0.5f, Time.deltaTime);
        float s = Speed * (1f + gust * 0.6f + Weather.Storm * 1.6f + Weather.Rain * 0.3f);
        transform.Rotate(0f, 0f, -s * Time.deltaTime, Space.Self);
    }
}
