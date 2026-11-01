using System.Collections.Generic;
using UnityEngine;

// Rijden met fysica, zoals in GTA: de route (van vak naar vak, met de rijstrook) is alleen waar de bestuurder naartoe stuurt.
// De auto zelf heeft een snelheid, een neus die met de wielen meedraait en banden met een grens aan grip: te snel in een bocht
// en hij glijdt naar buiten; een duw van een andere auto zet hem uit zijn lijn en hij stuurt zelf terug. De bestuurder kijkt een
// stukje vooruit op zijn rijlijn (pure pursuit) en geeft gas of remt naar de snelheid die DriveCar wil.
// Daarnaast: rijstroken op een brede weg (rechts houden, links inhalen, voorsorteren) en voorrang op kruisingen.
public partial class PlanetLife
{
    const float LookMin = 2.5f, KappaMax = 0.34f, YawAcc = 5f;   // vooruitkijken (m), scherpste bocht (1/m, draaicirkel 3 m), hoe snel de neus bijdraait (rad/s²)

    // zijwaartse plek van de rijlijn op de boog door b (rechts van het midden), met de rijstrook en het uitwijken van de bestuurder
    static float SideAt(Car c, int a, int b, int cc, float s) => Mathf.Lerp(Lane(a, b, c.lane), Lane(b, cc, c.lane), s) + c.lat;

    // het punt op de rijlijn 'dist' meter voor het punt van de auto op zijn route (hooguit tot het eind van de volgende boog)
    static Vector3 Ahead(Car c, float dist)
    {
        float len = Mathf.Max(1f, ArcLen(c.a, c.b, c.c)), rest = (1f - c.s) * len; Vector3 pos;
        if (dist <= rest || c.c == c.b) { float s = Mathf.Min(1f, c.s + dist / len); Arc(c.a, c.b, c.c, s, SideAt(c, c.a, c.b, c.c, s), 0f, out pos, out _, out _); }
        else { float s = Mathf.Min(1f, (dist - rest) / Mathf.Max(1f, ArcLen(c.b, c.c, c.n))); Arc(c.b, c.c, c.n, s, SideAt(c, c.b, c.c, c.n, s), 0f, out pos, out _, out _); }
        return pos;
    }

    // voortgang langs de route: s schuift naar het punt van de boog dat het dichtst bij de auto ligt. Een keerlus begint en
    // eindigt op hetzelfde punt (projecteren loopt daar vast): daar schuift s mee met de snelheid.
    static void Track(Car c, float dt)
    {
        if (c.a == c.c)   // alleen een klein stukje vooruit zoeken, zodat hij niet naar het eind van de lus springt
        {
            float best = 1e9f, bs = c.s; Vector3 pp = c.root.position;
            for (int k = 0; k <= 8; k++)
            {
                float s = Mathf.Min(1f, c.s + k * 0.02f); Arc(c.a, c.b, c.c, s, SideAt(c, c.a, c.b, c.c, s), 0f, out Vector3 q, out _, out _);
                float dd = (Flat(pp - q, pp.normalized)).sqrMagnitude; if (dd < best) { best = dd; bs = s; }
            }
            c.s = bs; if (c.s > 0.97f) c.s = 1f; return;
        }
        Vector3 p = c.root.position;
        for (int k = 0; k < 2; k++)
        {
            Arc(c.a, c.b, c.c, Mathf.Clamp01(c.s), 0f, 0f, out Vector3 q, out Vector3 f, out _);
            c.s += Vector3.Dot(p - q, f) / Mathf.Max(1f, ArcLen(c.a, c.b, c.c));
        }
        if (c.s < 0f) c.s = 0f;
    }

    // één stap: gas of rem naar 'want', sturen naar de rijlijn, banden met 'grip' m/s² zijwaarts, dan op het wegdek zetten
    void Physics(Car c, float want, float acc, float brk, float grip, float dt)
    {
        Vector3 p = c.root.position, up = p.normalized, f = Flat(c.fwd, up).normalized, r = Vector3.Cross(up, f);
        Vector3 vel = Flat(c.vel, up); float vf = Vector3.Dot(vel, f), vl = Vector3.Dot(vel, r);
        if (c.persona == 0)   // ver van de rijlijn (een duw, een te ruime bocht): eerst rustig terug
        {
            Arc(c.a, c.b, c.c, Mathf.Clamp01(c.s), SideAt(c, c.a, c.b, c.c, Mathf.Clamp01(c.s)), 0f, out Vector3 lp, out _, out _);
            float off = Flat(p - lp, up).magnitude; if (off > 2f && want > 0f) want = Mathf.Min(want, Mathf.Max(3f, 13f - 2.5f * off));
        }
        bool speedUp = Mathf.Abs(want) > Mathf.Abs(vf) && want * vf >= 0f;
        vf = Mathf.MoveTowards(vf, want, (speedUp ? acc : brk) * dt);
        // sturen: de bocht door het punt een stukje vooruit op de rijlijn, hooguit zo scherp als de draaicirkel; achteruit recht
        Vector3 d = Flat(Ahead(c, Mathf.Max(LookMin, 1.5f + Mathf.Abs(vf) * 0.3f)) - p, up);
        float L = Mathf.Max(1f, d.magnitude), alpha = Vector3.SignedAngle(f, d, up) * Mathf.Deg2Rad;
        // plus een correctie op de dwarsafwijking van de rijlijn zelf (Stanley): alleen vooruitkijken snijdt bochten af, over de stoep
        Arc(c.a, c.b, c.c, Mathf.Clamp01(c.s), SideAt(c, c.a, c.b, c.c, Mathf.Clamp01(c.s)), 0f, out Vector3 lq, out Vector3 lf, out _);
        float e = Vector3.Dot(Flat(p - lq, up), Vector3.Cross(up, Flat(lf, up).normalized));   // rechts van de lijn > 0
        float kappa = vf < 0f ? 0f : Mathf.Clamp(2f * Mathf.Sin(alpha) / L - 0.6f * e / (2f + Mathf.Abs(vf)), -KappaMax, KappaMax);
        c.w = Mathf.MoveTowards(c.w, vf * kappa, YawAcc * dt);   // de neus draait mee met de wielen, met wat traagheid
        if (vf >= 0f) c.steerDeg = Mathf.Clamp(Mathf.Atan(2.9f * kappa) * Mathf.Rad2Deg, -38f, 38f);   // stuurhoek van de voorwielen (wielbasis 2,9 m)
        Vector3 v3 = f * vf + r * vl;
        f = (Quaternion.AngleAxis(c.w * Mathf.Rad2Deg * dt, up) * f).normalized; r = Vector3.Cross(up, f);
        vf = Vector3.Dot(v3, f); vl = Vector3.Dot(v3, r);
        vl = Mathf.MoveTowards(vl, 0f, grip * dt);   // de banden: daarboven glijdt hij (slippen, driften)
        vel = f * vf + r * vl;
        p += vel * dt; up = p.normalized; f = Flat(f, up).normalized;
        // op het wegdek, of naast de weg op het maaiveld (en terugstuiteren tegen gebouwen); de neus mee omhoog of omlaag
        Arc(c.a, c.b, c.c, Mathf.Clamp01(c.s), 0f, 0f, out Vector3 rp, out _, out _);
        bool onRoad = Flat(p - rp, up).magnitude < 7f;
        float h = onRoad ? PlanetCity.SurfaceH(c.b, p) + 0.1f : GroundAt(p), ha = onRoad ? PlanetCity.SurfaceH(c.b, p + f * 1.5f) + 0.1f : GroundAt(p + f * 1.5f);
        float sp = vel.magnitude;
        if (!onRoad && sp > 0.5f && Wall(p + vel / sp * 1.8f, h)) { vel *= -0.25f; if (sp >= 3f) Damage(c, sp * 1.2f); }
        p = up * (Planet.R + h);
        c.root.position = p; c.fwd = f; c.vel = Flat(vel, up); c.v = Vector3.Dot(c.vel, f);
        c.root.rotation = Geo.Look((f + up * ((ha - h) / 1.5f)).normalized, up);
        if (c.wheels != null) c.wheels.Drive(c.v * dt, c.steerDeg, dt);
    }

    // de auto precies op zijn rijlijn zetten (bij het verschijnen)
    void PlaceCar(Car c)
    {
        Arc(c.a, c.b, c.c, c.s, SideAt(c, c.a, c.b, c.c, c.s), 0.10f, out Vector3 pos, out Vector3 fwd, out Vector3 up);
        pos += up * PlanetCity.SurfaceH(c.b, pos);
        c.root.position = pos; c.fwd = fwd; c.root.rotation = Geo.Look(fwd, up); c.vel = fwd * c.v; c.w = 0f;
    }

    Car Nearest(Car c, float r) { Car best = null; float bd = r; foreach (var o in cars) { if (o == c || o.dead) continue; float d = Vector3.Distance(o.root.position, c.root.position); if (d < bd) { bd = d; best = o; } } return best; }
    bool Crowded(Car c) { foreach (var o in cars) if (Vector3.Distance(o.root.position, c.root.position) < 8f) return true; return false; }

    // voor het wegvoorbeeld: hoe ver auto's van hun rijlijn zitten en wat ze doen
    public string Worst = "", Stuck = "", CrashLog = ""; static bool diagOn; static string yieldWhy = "";
    public void Diag(out float maxOff, out int moving, out int waiting, out int stuck, out int free, out int lane0, out int wide)
    {
        maxOff = 0f; moving = waiting = stuck = free = lane0 = wide = 0; Worst = ""; Stuck = ""; diagOn = true;
        foreach (var c in cars)
        {
            if (c.dead) continue; if (c.free) { free++; continue; }
            Arc(c.a, c.b, c.c, Mathf.Clamp01(c.s), SideAt(c, c.a, c.b, c.c, Mathf.Clamp01(c.s)), 0f, out Vector3 q, out _, out _);
            Vector3 p = c.root.position; float off = Flat(p - q, p.normalized).magnitude;
            if (off > maxOff) { maxOff = off; Worst = $"a{c.a} b{c.b} c{c.c} n{c.n} s{c.s:0.00} v{c.v:0.0} w{c.w:0.00} lane{c.lane} keer{(c.a == c.c)} bend{Bend(c.a, c.b, c.c):0.00} ghost{c.ghostT:0.0}"; }
            if (Mathf.Abs(c.v) > 0.3f) moving++;
            else if (c.idleT < 12f) waiting++;
            else { stuck++; var l = c.leadCar; Stuck += $"[b{c.b} c{c.c} n{c.n} s{c.s:0.00} stil{c.idleT:0} {c.why}" + (l == null ? "" : $" | voorligger b{l.b} c{l.c} s{l.s:0.00} v{l.v:0.0} stil{l.idleT:0} free{l.free} dead{l.dead} zijnlead={(l.leadCar == c ? "ik" : l.leadCar == null ? "geen" : "ander")} {l.why}") + "] "; }
            if (PlanetCity.StyleOf(c.b) == 1) { wide++; if (c.lane == 0) lane0++; }
        }
        if (stuck > 0) foreach (var c in cars) if (!c.dead && !c.free && JunctionCell(c.b)) Stuck += $" inJ{{a{c.a} b{c.b} c{c.c} n{c.n} s{c.s:0.00} v{c.v:0.0} stil{c.idleT:0} {c.why}}}";
        if (stuck > 0) { var js = new HashSet<int>(); foreach (var c in cars) if (!c.dead && !c.free && JunctionCell(c.c) && c.idleT > 12f) js.Add(c.c); foreach (int J in js) { Stuck += $" J{J}:"; foreach (int j in PlanetCity.Partners(J)) if (PlanetCity.IsRoad(j)) Stuck += $" {j}/st{PlanetCity.StyleOf(j)}/pr{PlanetCity.RoadPrio(J, j)}"; } }
    }

    // >0 rechtsaf, <0 linksaf in vak b (van a naar cc)
    static float TurnSign(int a, int b, int cc) => Vector3.Dot(Vector3.Cross(D(b) - D(a), D(cc) - D(b)), D(b));
    static bool JunctionCell(int j)
    {
        if (!PlanetCity.IsRoad(j) || PlanetCity.IsRot(j)) return false;
        int n = 0; foreach (int p in PlanetCity.Partners(j)) if (PlanetCity.IsRoad(p)) n++; return n >= 3;
    }

    // is de andere rijstrook naast de auto vrij (geen auto ernaast, 8 m erachter tot 12 m ervoor)?
    bool LaneFree(Car c, int lane)
    {
        float dl = Lane(c.b, c.c, lane) - Lane(c.b, c.c, c.lane);
        Vector3 pos = c.root.position, up = pos.normalized, right = Vector3.Cross(up, c.fwd);
        foreach (var o in cars)
        {
            if (o == c || o.dead || Vector3.Dot(o.fwd, c.fwd) < 0.3f) continue; Vector3 d = o.root.position - pos;
            if (Mathf.Abs(Vector3.Dot(d, up)) > 2.5f) continue; float ah = Vector3.Dot(d, c.fwd), lt = Vector3.Dot(d, right);
            if (ah > -8f && ah < 12f && Mathf.Abs(lt - dl) < 1.4f) return false;
        }
        return true;
    }

    // Rijstroken op een brede weg: rechts houden, links inhalen als de auto voor je langzamer rijdt, voorsorteren voor een
    // afslag (linksaf links, rechtsaf rechts). Wisselen alleen als de andere strook vrij is; de fysica maakt er een vloeiende
    // strookwissel van.
    void ChooseLane(Car c, Car lead, float leadD, float leadV)
    {
        if (PlanetCity.StyleOf(c.b) != 1) return;
        int want = c.lane;
        if (PlanetCity.StyleOf(c.c) != 1 || c.c == c.b || c.n == c.b) want = 1;   // de brede weg houdt op (smaller, het eind, of zo keren): naar rechts
        else if (c.c != c.b && Bend(c.b, c.c, c.n) > 0.3f) want = TurnSign(c.b, c.c, c.n) < 0f ? 0 : 1;
        else if (c.lane == 1 && lead != null && leadD < 16f && leadV < c.speed - 1f && !lead.atLight) want = 0;
        else if (c.lane == 0 && (lead == null || leadD > 20f)) want = 1;
        if (want != c.lane && LaneFree(c, want)) c.lane = want;
    }

    // links op een brede weg die binnen 20 m smaller wordt, en rechts is niet vrij: terugzakken (ritsen)
    bool MustMerge(Car c, float len) => c.lane == 0 && PlanetCity.StyleOf(c.b) == 1 && (PlanetCity.StyleOf(c.c) != 1 || c.c == c.b || c.n == c.b) && (1f - c.s) * len < 20f && !LaneFree(c, 1);

    // Voorrang op een kruising: zonder lichten gaat de bredere weg voor, bij gelijke wegen verkeer van rechts. Linksaf wacht op
    // tegemoetkomend verkeer (ook bij groen) en niemand rijdt een kruising op waar een ander dwars overheen rijdt. Wie het
    // langst wacht mag eerst, zodat vier auto's die elk iemand van rechts zien niet eeuwig op elkaar wachten.
    bool MustYield(Car c, float len)
    {
        int J = c.c; if (J == c.b || c.n == J || !JunctionCell(J)) return false;
        float rest = (1f - c.s) * len; if (rest > 16f) return false;
        bool lights = Traffic.IsJunction(J);
        Vector3 cj = D(J), myDir = Flat(cj - D(c.b), cj).normalized;
        bool myLeft = TurnSign(c.b, J, c.n) < 0f && Bend(c.b, J, c.n) > 0.3f;
        int myPrio = PlanetCity.RoadPrio(J, c.b);
        foreach (var o in cars)
        {
            if (o == c || o.dead || o.free) continue;
            if (o.b == c.n && o.a == J && o.s < 0.45f && Mathf.Abs(o.v) < 0.5f && c.waitPri < 12f) { yieldWhy = "uitrit"; return true; }   // mijn uitrit staat vol: de kruising niet blokkeren (na 12 s toch, anders staat alles voor eeuwig)
            if (o.b == J && o.a != c.b && o.a != J && o.c != c.b && o.s < 0.75f && (c.waitPri < 6f || Mathf.Abs(o.v) > 0.5f)) { yieldWhy = "dwars"; return true; }   // dwars op de kruising (na 6 s wachten alleen nog voor wie daar rijdt, niet voor wie er stilstaat)
            if (o.c != J || o.b == c.b || o.n == o.c) continue;
            float oRest = (1f - o.s) * Mathf.Max(1f, ArcLen(o.a, o.b, o.c));
            if (oRest > Mathf.Max(16f, 5f * o.v)) continue;   // de ander is nog ver weg (meer dan 5 s rijden: linksaf over een kruising duurt lang)
            if (Mathf.Abs(o.v) < 0.5f && oRest > StopBack + 3f) continue;   // staat stil in een rij, niet vooraan: die komt voorlopig niet
            if (lights && o.atLight && o.v < 0.5f) continue;   // staat voor rood
            if (o.waitPri > 0f && Mathf.Abs(o.v) < 0.5f && (c.waitPri > o.waitPri + 0.5f || (Mathf.Abs(c.waitPri - o.waitPri) <= 0.5f && c.GetHashCode() > o.GetHashCode()))) continue;   // ik wacht al langer (bij gelijk: vaste volgorde, anders wachten vier auto's eeuwig op elkaar)
            Vector3 vo = Flat(D(o.b) - cj, cj).normalized;
            if (Vector3.Dot(vo, myDir) > 0.7f)   // tegemoetkomend
            {
                bool oLeft = TurnSign(o.b, J, o.n) < 0f && Bend(o.b, J, o.n) > 0.3f;
                if (myLeft && !oLeft) { yieldWhy = $"linksaf(b{o.b} v{o.v:0.0})"; return true; }
                continue;
            }
            if (lights) continue;   // de rest regelt het licht
            int oPrio = PlanetCity.RoadPrio(J, o.b);
            if (oPrio > myPrio || (oPrio == myPrio && Vector3.Dot(Vector3.Cross(myDir, vo), cj) > 0f)) { yieldWhy = (oPrio > myPrio ? "breder" : "rechts") + $"(ik{myPrio} b{o.b} {oPrio})"; return true; }   // bredere weg, of van rechts
        }
        return false;
    }
}
