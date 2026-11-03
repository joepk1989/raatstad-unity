using System.Collections.Generic;
using UnityEngine;

// Leven rond de kijker. Op een kleine stad (de startstad, de teststad) lopen en rijden mensen, auto's en fietsers over de hele
// stad, zoals altijd. Op een grote planeet (Hele planeet vol) alleen binnen een paar honderd meter van waar de camera naar kijkt
// (of van de wandelaar, L): wie te ver weg raakt verdwijnt buiten beeld en komt elders in de buurt terug, ook buiten beeld.
// Hoeveel er zijn volgt de bebouwing rond elk weg-, pad- of fietsvak (Busy): druk bij winkels, pleinen en kantoren, rustiger in
// een dorp, een enkeling op het platteland. Ver uitgezoomd of in de ruimte niemand.
public partial class PlanetLife
{
    const int SmallTown = 400;   // minder weg- en padvakken: een kleine stad, alles doet mee zoals vroeger
    const int MaxPeople = 150, MaxCars = 70, MaxRiders = 30;   // in de buurt, met Rocketbox-figuren nog soepel
    bool Big => walks.Count + roads.Count >= SmallTown;
    public int RiderCount => riders.Count;
    Camera viewCam; Vector3 viewAt = Vector3.up; float viewR = 1e6f, viewK = 1f, nearT;   // viewAt: richting op de bol; viewK 1 dichtbij, 0 ver weg
    Vector3 nearAt; bool hasNear;
    readonly List<int> nearWalk = new List<int>(), nearRoad = new List<int>(), nearBike = new List<int>();
    readonly List<float> wWalk = new List<float>(), wRoad = new List<float>(), wBike = new List<float>();
    float sumWalk, sumRoad, sumBike;
    readonly Dictionary<int, float> busy = new Dictionary<int, float>();
    int checkP, checkC, checkR;   // om de beurt nakijken wie te ver weg is

    // bebouwing op een vak: winkels en pleinen trekken het meest, dan kantoren, dan woningen (per laag)
    static float Score(int i)
    {
        if (!PlanetCity.Cells.TryGetValue(i, out var l)) return 0f;
        float s = 0f;
        foreach (var t in l)
            switch (t) { case "winkel": s += 5f; break; case "plein": s += 5f; break; case "kantoor": s += 2f; break; case "toren": s += 1.5f; break; case "woning": case "blok": s += 1f; break; case "park": s += 0.4f; break; case "molen": s += 0.3f; break; }
        return s;
    }
    float Busy(int i)
    {
        if (busy.TryGetValue(i, out float b)) return b;
        var c = Planet.Cells[i]; b = Score(i);
        foreach (int j in c.nb) b += Score(j);
        if (c.across != null) foreach (int j in c.across) b += Score(j) * 0.5f;
        busy[i] = b; return b;
    }
    // hoeveel er per vak lopen, rijden of fietsen: met het kwadraat van de drukte, zodat een stadscentrum veel drukker is dan een dorp
    float WalkDensity(int i) { float b = Busy(i); return 0.012f + Mathf.Min(1.4f, b * b * 0.009f); }
    float RoadDensity(int i) { float b = Busy(i); return 0.025f + Mathf.Min(0.45f, b * b * 0.0035f); }
    float BikeDensity(int i) { float b = Busy(i); return 0.04f + Mathf.Min(0.4f, b * b * 0.003f); }

    // waar de kijker is: de wandelaar, of het punt op de bol waar de camera naar kijkt; de straal groeit met de hoogte
    void UpdateView()
    {
        bool walking = walker != null && walker.gameObject.activeInHierarchy;
        viewCam = walking && walker.Cam != null ? walker.Cam : Camera.main;
        if (walking) { viewAt = walker.transform.position.normalized; viewR = 170f; viewK = 1f; return; }
        if (viewCam == null) return;
        Vector3 o = viewCam.transform.position, d = viewCam.transform.forward; float R = Planet.R;
        float alt = o.magnitude - R, b = Vector3.Dot(o, d), disc = b * b - (o.sqrMagnitude - R * R);
        float t = disc > 0f ? -b - Mathf.Sqrt(disc) : -1f;
        viewAt = (t > 0f ? o + d * t : o).normalized;
        viewR = Mathf.Clamp(alt * 1.1f, 140f, 320f);
        viewK = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(380f, 700f, alt));
    }

    // de vakken binnen de straal met hun dichtheid (twee keer per seconde, of als de kijker ver is verschoven)
    void UpdateNear(float dt)
    {
        nearT -= dt;
        if (nearT > 0f && hasNear && (nearAt - viewAt).magnitude * Planet.R < 25f) return;
        nearT = 0.5f; nearAt = viewAt; hasNear = true;
        float lim = viewR / Planet.R;
        void Fill(List<int> src, List<int> into, List<float> w, System.Func<int, float> dens, out float sum)
        {
            into.Clear(); w.Clear(); sum = 0f;
            foreach (int i in src) { if ((Planet.Cells[i].dir - viewAt).magnitude > lim) continue; float x = dens(i); into.Add(i); w.Add(x); sum += x; }
        }
        Fill(walks, nearWalk, wWalk, WalkDensity, out sumWalk); Fill(roads, nearRoad, wRoad, RoadDensity, out sumRoad); Fill(bikeCells, nearBike, wBike, BikeDensity, out sumBike);
    }

    bool Visible(Vector3 p)
    {
        if (viewCam == null) return false;
        Vector3 v = viewCam.WorldToViewportPoint(p);
        return v.z > 0f && v.z < 400f && v.x > -0.08f && v.x < 1.08f && v.y > -0.08f && v.y < 1.08f;
    }
    bool Far(Vector3 p) => (p.normalized - viewAt).magnitude * Planet.R > viewR * 1.25f;

    // een vak in de buurt, naar dichtheid gekozen; liefst buiten beeld (anyView: ook in beeld, bij het vullen)
    bool PickCell(List<int> cells, List<float> w, float sum, bool anyView, out int cell)
    {
        cell = -1; if (cells.Count == 0 || sum <= 0f) return false;
        for (int k = 0; k < 8; k++)
        {
            float r = Random.value * sum; int i = 0;
            for (; i < cells.Count - 1; i++) { r -= w[i]; if (r <= 0f) break; }
            int c = cells[i]; Vector3 p = Planet.Cells[c].dir * (Planet.R + PlanetCity.G(c) + 1f);
            if (anyView || !Visible(p)) { cell = c; return true; }
        }
        return false;
    }

    int TargetPeople() => !PeopleOn ? 0 : Big ? Mathf.RoundToInt(Mathf.Min(MaxPeople, sumWalk) * viewK) : Mathf.Clamp(walks.Count / 2, 0, 40);
    int TargetCars() => !CarsOn ? 0 : Big ? Mathf.RoundToInt(Mathf.Min(MaxCars, sumRoad) * viewK) + CarBoost : Mathf.Clamp(roads.Count / 3 + CarBoost, 0, 40 + CarBoost);   // klein: een auto per drie wegvakken, hooguit 40
    int TargetRiders() => !PeopleOn ? 0 : Big ? Mathf.RoundToInt(Mathf.Min(MaxRiders, sumBike) * viewK) : Mathf.Clamp(bikeCells.Count / 3, 0, 20);

    // een vak voor iemand die erbij komt: klein overal, groot in de buurt
    bool SpawnWalk(bool anyView, out int cell)
    {
        if (!Big) { cell = walks.Count > 0 ? walks[Random.Range(0, walks.Count)] : -1; return cell >= 0; }
        return PickCell(nearWalk, wWalk, sumWalk, anyView, out cell);
    }
    bool SpawnRoad(bool anyView, out int cell)
    {
        if (!Big) { cell = roads.Count > 0 ? roads[Random.Range(0, roads.Count)] : -1; return cell >= 0; }
        return PickCell(nearRoad, wRoad, sumRoad, anyView, out cell);
    }
    bool SpawnBike(bool anyView, out int cell)
    {
        if (!Big) { cell = bikeCells.Count > 0 ? bikeCells[Random.Range(0, bikeCells.Count)] : -1; return cell >= 0; }
        return PickCell(nearBike, wBike, sumBike, anyView, out cell);
    }

    // iemand weghalen of verplaatsen: een praatje met hem stopt
    void Unpartner(Person a)
    {
        var p = a.partner; a.partner = null; a.paused = false; a.chatT = 0f;
        if (p != null && p.partner == a) { p.partner = null; p.paused = false; p.chatT = 0f; }
    }
    void RemovePerson(int i) { var a = People[i]; Unpartner(a); Destroy(a.root.gameObject); People.RemoveAt(i); }
    void MovePerson(Person a, int cell)
    {
        Unpartner(a); a.downT = 0f; a.waveT = 0f;
        a.cellA = cell; a.prevCell = -1; a.cellB = NextRoad(cell, -1, true); a.ct = Random.value; a.hasAt = false;
        PlacePerson(a, 0f);
    }

    // Groot: wie te ver weg en buiten beeld is komt terug in de buurt (of verdwijnt als het er te veel zijn); per beeld een paar
    void Recycle(int targetP, int targetC, int targetR)
    {
        if (!Big) return;
        for (int k = 0; k < 12 && People.Count > 0; k++)
        {
            checkP = (checkP + 1) % People.Count; var a = People[checkP]; Vector3 p = a.root.position;
            if (!Far(p) || Visible(p)) continue;
            if (People.Count > targetP) { RemovePerson(checkP); continue; }
            if (SpawnWalk(false, out int cell)) MovePerson(a, cell);
        }
        for (int k = 0; k < 6 && cars.Count > 0; k++)
        {
            checkC = (checkC + 1) % cars.Count; var c = cars[checkC]; Vector3 p = c.root.position;
            if (Far(p) && !Visible(p)) { Destroy(c.root.gameObject); cars.RemoveAt(checkC); }   // een nieuwe komt via het aanvullen, buiten beeld
        }
        for (int k = 0; k < 4 && riders.Count > 0; k++)
        {
            checkR = (checkR + 1) % riders.Count; var r = riders[checkR]; Vector3 p = r.root.position;
            if (!Far(p) || Visible(p)) continue;
            if (riders.Count > targetR) { Destroy(r.root.gameObject); riders.RemoveAt(checkR); continue; }
            if (SpawnBike(false, out int cell)) { r.a = cell; r.prev = -1; r.b = NextBike(cell, -1); r.t = Random.value; PlaceRider(r, 0f); }
        }
        // te veel (ingezoomd naar minder drukte, of uitgezoomd): eerst wie buiten beeld is; uitgezoomd iedereen
        int over = People.Count - targetP;
        for (int i = People.Count - 1; i >= 0 && over > 0; i--) if (viewK <= 0f || !Visible(People[i].root.position)) { RemovePerson(i); over--; if (viewK > 0f) break; }
        over = cars.Count - targetC;
        for (int i = cars.Count - 1; i >= 0 && over > 0; i--) if (viewK <= 0f || !Visible(cars[i].root.position)) { Destroy(cars[i].root.gameObject); cars.RemoveAt(i); over--; if (viewK > 0f) break; }
        over = riders.Count - targetR;
        for (int i = riders.Count - 1; i >= 0 && over > 0; i--) if (viewK <= 0f || !Visible(riders[i].root.position)) { Destroy(riders[i].root.gameObject); riders.RemoveAt(i); over--; if (viewK > 0f) break; }
    }
}
