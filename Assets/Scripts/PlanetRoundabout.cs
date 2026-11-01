using System.Collections.Generic;
using UnityEngine;

// Rotondes, met de maten van de webversie (07-driehoeken-vectorwegen.js, ROT): rijbaan op 11 m van het midden, binnenrand 8,25 m,
// buitenrand 13,75 m, eiland 6,75 m met een overrijdbare klinkerrand tot de binnenrand en een boom; daaromheen de schuine stoeprand
// en de stoep, open waar een weg aansluit (dwars op die weg gemeten, zodat de randen evenwijdig aan de stoeprand van de weg lopen).
// Een rotonde is een wegknoop (het middenvak, in Rot) met de zes vakken eromheen als ring: daarop komen geen gebouwen, alleen de
// wegen die op de rotonde uitkomen (verbonden met het midden). Auto's rijden tegen de klok in rond en geven voorrang aan wie er
// al rijdt (PlanetLife); er staan geen verkeerslichten.
public static partial class PlanetCity
{
    public static readonly HashSet<int> Rot = new HashSet<int>();
    public const float RotRc = 11f, RotRi = 8.25f, RotRo = 13.75f, RotIsl = 6.75f, RotWalk = 2.3f;
    public static bool IsRot(int i) => Rot.Contains(i) && IsRoad(i);
    // de rotonde waar vak i bij hoort (het midden zelf of een vak van de ring), of -1
    public static int RotOf(int i) { if (IsRot(i)) return i; foreach (int j in Planet.Cells[i].nb) if (IsRot(j)) return j; return -1; }
    static bool RotFree(int x) => !Under.ContainsKey(x) && (!Cells.TryGetValue(x, out var l) || l.Count == 0 || (l.Count == 1 && (l[0] == "weg" || l[0] == "pad" || l[0] == "wegdeel")));   // een half vak naast een schuine weg mag ook

    // een rotonde met midden c: het midden en de ring moeten leeg zijn (of weg of pad); wegen op de ring sluiten erop aan
    public static string PlaceRoundabout(int c)
    {
        if (RotOf(c) >= 0) return null;   // hoort al bij een rotonde (slepen over de rotonde)
        if (!(IsRoad(c) && RoadNb(c).Count >= 3))   // naast een kruispunt geklikt: de rotonde komt op dat kruispunt
        { int best = -1, most = 2; foreach (int j in Planet.Cells[c].nb) if (IsRoad(j) && RoadNb(j).Count > most) { most = RoadNb(j).Count; best = j; } if (best >= 0) { c = best; if (RotOf(c) >= 0) return null; } }
        foreach (int j in Planet.Cells[c].nb) foreach (int k in Planet.Cells[j].nb) if (k != c && IsRot(k)) return "Hier ligt al een rotonde vlakbij";
        if (!RotFree(c)) return "Hier staat iets in de weg: een rotonde is ongeveer 32 m breed (het vak en de zes eromheen)";
        foreach (int j in Planet.Cells[c].nb) if (!RotFree(j)) return "Hier staat iets in de weg: een rotonde is ongeveer 32 m breed (het vak en de zes eromheen)";
        Cells[c] = new List<string> { "weg" }; Bike.Remove(c); Rot.Add(c);
        var ring = Planet.Cells[c].nb;
        foreach (int j in ring) if (IsWalk(j)) Link(c, j);
        foreach (int j in ring) foreach (int k in ring)   // twee ringvakken die aan elkaar vastzitten: die weg loopt nu over de rotonde
            if (j < k && IsRoad(j) && IsRoad(k) && Linked(j, k)) { Links.Remove(Key(j, k)); partners[j].Remove(k); partners[k].Remove(j); OneWay.Remove(DKey(j, k)); OneWay.Remove(DKey(k, j)); }
        float h = G(c); foreach (int j in ring) LevelTerr(j, h);   // vlak, zoals een plein
        Touch(c); foreach (int j in ring) Touch(j);
        return null;
    }
    static void LinkRot(int i) { int ro = RotOf(i); if (ro >= 0 && ro != i) Link(i, ro); }   // weg of pad op de ring: aan het midden vast
    // een rotonde weghalen (het midden leegmaken): de ring komt terug zoals hij was
    static void ClearRoundabout(int c)
    {
        Rot.Remove(c); foreach (int j in Planet.Cells[c].nb) if (!IsWalk(j)) RestoreLevel(j);
    }

    // ---------- tekenen ----------
    // NodeTrim voor een arm van rotonde n: het lint van de weg houdt op waar de rand van zijn asfalt de buitenrand raakt
    static float RotTrim(int n, int j) { Dims(ArmStyle(n, j), out float a, out _); return Mathf.Sqrt(RotRo * RotRo - a * a); }
    static void RoundaboutMesh(Dictionary<string, MeshBuilder> mb, int n, List<int> arms)
    {
        System.Func<string, MeshBuilder> M = k => { if (!mb.TryGetValue(k, out var x)) { x = new MeshBuilder(); mb[k] = x; } return x; };
        var fr = new Frame(n, true).Flat(); skb = SkirtDepth(n);
        System.Func<float, float, Vector2> At = (an, r) => new Vector2(Mathf.Cos(an), Mathf.Sin(an)) * r;
        const int S = 72; float da = Mathf.PI * 2f / S;
        // rijbaan, klinkerrand en eiland
        for (int k = 0; k < S; k++)
        {
            float a0 = k * da, a1 = (k + 1) * da;
            foreach (var (r0, r1) in new[] { (RotRi, (RotRi + RotRo) * 0.5f), ((RotRi + RotRo) * 0.5f, RotRo) })
            { Vector2 p0 = At(a0, r0), p1 = At(a1, r0), q1 = At(a1, r1), q0 = At(a0, r1); M("asfalt").Quad(fr.P(p0.x, AY, p0.y), fr.P(p1.x, AY, p1.y), fr.P(q1.x, AY, q1.y), fr.P(q0.x, AY, q0.y), fr.u); }
            Vector2 i0 = At(a0, RotIsl), i1 = At(a1, RotIsl), o0 = At(a0, RotRi - 0.25f), o1 = At(a1, RotRi - 0.25f), e0 = At(a0, RotRi), e1 = At(a1, RotRi);
            M("klinker").Quad(fr.P(i0.x, 0.16f, i0.y), fr.P(i1.x, 0.16f, i1.y), fr.P(o1.x, 0.16f, o1.y), fr.P(o0.x, 0.16f, o0.y), fr.u);   // overrijdbare rand, 6 cm boven het asfalt
            M("klinker").Quad(fr.P(o0.x, 0.16f, o0.y), fr.P(o1.x, 0.16f, o1.y), fr.P(e1.x, AY, e1.y), fr.P(e0.x, AY, e0.y), fr.D(o0.x + o1.x, 0f, o0.y + o1.y));
            M("rand").Quad(fr.P(i0.x, 0.16f, i0.y), fr.P(i1.x, 0.16f, i1.y), fr.P(i1.x, 0.32f, i1.y), fr.P(i0.x, 0.32f, i0.y), fr.D(i0.x + i1.x, 0f, i0.y + i1.y));   // band om het eiland
            M("gras").Tri(fr.P(0f, 0.32f, 0f), fr.P(i0.x, 0.32f, i0.y), fr.P(i1.x, 0.32f, i1.y), fr.u);
        }
        Cone(M("stam"), fr, 0f, 0f, 0.25f, 0.3f, 2.6f, 0.18f); Cone(M("blad"), fr, 0f, 0f, 2.2f, 2.0f, 6.4f, 0f);
        // stoeprand en stoep rondom, open waar een arm aansluit: per arm de opening |opzij| < halve breedte van zijn asfalt
        var openA = new List<float>(); var openW = new List<float>();
        foreach (int j in arms)
        {
            Vector3 d3 = (Planet.Cells[j].dir - Planet.Cells[n].dir) * Planet.R; openA.Add(Mathf.Atan2(Vector3.Dot(d3, fr.n), Vector3.Dot(d3, fr.e)));
            Dims(ArmStyle(n, j), out float a, out _); openW.Add(a);
        }
        var ord = new List<int>(); for (int k = 0; k < openA.Count; k++) ord.Add(k); ord.Sort((x, y) => openA[x].CompareTo(openA[y]));
        float rK = RotRo + KW, rS = RotRo + RotWalk;
        System.Func<int, float, float> Half = (k, r) => Mathf.Asin(Mathf.Clamp01(openW[k] / r));
        int m = ord.Count;
        for (int g = 0; g < Mathf.Max(1, m); g++)
        {
            float s0, s1; int ka = m > 0 ? ord[g] : -1, kb = m > 0 ? ord[(g + 1) % m] : -1;
            System.Func<float, float> From = r => m == 0 ? 0f : openA[ka] + Half(ka, r), To = r => m == 0 ? Mathf.PI * 2f : (m == 1 ? openA[ka] + Mathf.PI * 2f : openA[kb] + (g + 1 == m ? Mathf.PI * 2f : 0f)) - Half(kb, r);
            s0 = From(RotRo); s1 = To(RotRo); if (s1 - s0 < 0.02f) continue;
            int ns = Mathf.Max(2, Mathf.CeilToInt((s1 - s0) / da));
            for (int k = 0; k < ns; k++)
            {
                float f0 = k / (float)ns, f1 = (k + 1) / (float)ns;
                System.Func<float, float, Vector2> Pt = (f, r) => At(Mathf.Lerp(From(r), To(r), f), r);
                Kerb(M("rand"), fr, Pt(f0, RotRo), Pt(f1, RotRo), Pt(f0, rK), Pt(f1, rK), AY);
                Walk(M("stoep"), fr, Pt(f0, rK), Pt(f1, rK), Pt(f0, rS), Pt(f1, rS));
            }
        }
        // haaientanden op de inrit: rechts van wie de rotonde op rijdt (die rijdt naar het midden)
        foreach (int k in ord)
        {
            Vector2 ax = At(openA[k], 1f), rt = new Vector2(-ax.y, ax.x); if (Vector3.Dot(Vector3.Cross(fr.u, -(fr.e * ax.x + fr.n * ax.y)), fr.e * rt.x + fr.n * rt.y) < 0f) rt = -rt;   // rechts van de rijrichting naar binnen
            float r0 = Mathf.Sqrt(RotRo * RotRo - openW[k] * openW[k]) + 0.2f;
            for (float l = 0.4f; l < openW[k] - 0.3f; l += 0.75f)
            {
                Vector2 b0 = ax * r0 + rt * l, b1 = ax * r0 + rt * (l + 0.5f), tip = ax * (r0 - 0.6f) + rt * (l + 0.25f);
                M("lijn").Tri(fr.P(b0.x, LY, b0.y), fr.P(b1.x, LY, b1.y), fr.P(tip.x, LY, tip.y), fr.u);
            }
        }
        // een zebrapad op elke arm, net buiten de ring: daar steken de voetgangers over die om de rotonde heen lopen
        foreach (int k in ord) { Vector2 ax = At(openA[k], 1f); Zebra(M, fr, ax * (RotRo + 1.6f), ax, openW[k], 2.4f); }
        // een lantaarn op de stoep tussen twee armen
        { float an = m > 0 ? openA[ord[0]] + 0.6f : 0.4f; Vector2 lp = At(an, rS - 0.6f); Cone(M("paal"), fr, lp.x, lp.y, 0.08f, SY, 5.2f, 0.06f); Cone(M("lamp"), fr, lp.x, lp.y, 0.28f, 4.9f, 5.35f, 0.2f); }
    }

    // ---------- voor de auto's ----------
    // De rijlijn over de rotonde van arm a naar arm c: van het punt waar de auto de cirkel op komt (rechts van de as van a) tegen de
    // klok in (het midden links van de auto) tot het punt waar hij er rechts van de as van c af gaat. lane: de afstand opzij.
    public static void RotPath(int a, int b, int c, float laneIn, float laneOut, out Vector3 C, out Vector3 e, out Vector3 n, out float f0, out float span, out float r0, out float r1)
    {
        var cell = Planet.Cells[b]; Vector3 up = cell.dir; C = up * (Planet.R + G(b)); e = cell.east; n = cell.north;
        Vector3 ain = Tan(cell.dir - Planet.Cells[a].dir, cell.dir), aout = Tan(Planet.Cells[c].dir - cell.dir, cell.dir);   // naar binnen over arm a, naar buiten over arm c
        Vector3 E = -ain * RotRc + Vector3.Cross(up, ain).normalized * laneIn, X = aout * RotRc + Vector3.Cross(up, aout).normalized * laneOut;
        f0 = Mathf.Atan2(Vector3.Dot(E, n), Vector3.Dot(E, e)); float f1 = Mathf.Atan2(Vector3.Dot(X, n), Vector3.Dot(X, e));
        r0 = E.magnitude; r1 = X.magnitude;
        // tegen de klok in: het midden links van de rijrichting (rechts = Cross(up, vooruit))
        Vector3 t = -e * Mathf.Sin(f0) + n * Mathf.Cos(f0); float sg = Vector3.Dot(Vector3.Cross(up, t), -(e * Mathf.Cos(f0) + n * Mathf.Sin(f0))) > 0f ? -1f : 1f;
        span = Mathf.Repeat((f1 - f0) * sg, Mathf.PI * 2f); if (span < 0.35f) span += Mathf.PI * 2f; span *= sg;   // keren: helemaal rond
    }
}
