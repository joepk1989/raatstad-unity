using UnityEngine;

// Zelf rijden (J): instappen in een auto op de weg die het dichtst bij de muis (of het midden van het beeld) ligt, en weer uitstappen
// met J of Esc. Het rijden zelf, de camera en het geluid zitten in PlayerCar.cs; hier de toetsen, het in- en uitstappen en de
// snelheidsmeter rechtsonder.
public partial class PlanetBuilder
{
    bool driving; PlayerCar playerCar; float driveT;
    public bool Driving => driving;

    // true zolang er gereden wordt (dan doet de bouwmodus niets)
    bool DriveUpdate()
    {
        if (!driving)
        {
            if (Inp.Down(K.J) && !inSpace && !Modal && !cursorMode) StartDrive();
            return driving;
        }
        if (Inp.Down(K.Slash)) ToggleHelp();
        if (Inp.Down(K.Esc) && helpOpen) helpOpen = false;
        else if (Inp.Down(K.J) || Inp.Down(K.Esc)) { StopDrive(); return true; }
        WalkHide();
        return true;
    }

    void StartDrive()
    {
        var L = PlanetLife.I; if (L == null) { Say("Rijden kan hier niet"); return; }
        if (walking) ToggleWalk();
        Vector3 at = (hasHover ? Planet.Cells[hover].dir : focus).normalized;
        // de dichtstbijzijnde weg (binnen 400 m), in de richting die het meest op de kijkrichting lijkt
        int best = -1; float bd = 400f / Planet.R;
        foreach (var kv in PlanetCity.Cells)
        {
            if (!PlanetCity.IsRoad(kv.Key) || PlanetCity.IsRot(kv.Key)) continue;
            float d = Vector3.Distance(Planet.Cells[kv.Key].dir, at); if (d < bd) { bd = d; best = kv.Key; }
        }
        Vector3 pos, dir;
        Vector3 look = fwd;
        if (best >= 0)
        {
            Vector3 cd = Planet.Cells[best].dir; dir = Vector3.zero; float bs = -2f;
            foreach (int j in PlanetCity.Partners(best))
            {
                if (!PlanetCity.IsRoad(j)) continue; Vector3 t = Planet.Cells[j].dir - cd; t -= cd * Vector3.Dot(t, cd); t.Normalize();
                float s = Vector3.Dot(t, look); if (s > bs) { bs = s; dir = t; }
            }
            if (dir == Vector3.zero) { dir = look - cd * Vector3.Dot(look, cd); }
            dir.Normalize(); Vector3 right = Vector3.Cross(cd, dir);
            pos = cd * Planet.R + right * PlanetCity.LaneOffset(best, 1);   // rechts op de weg
            pos = pos.normalized * (Planet.R + PlanetCity.SurfaceH(best, pos) + 0.2f);
        }
        else
        {
            dir = look - at * Vector3.Dot(look, at); if (dir.sqrMagnitude < 1e-6f) dir = Planet.Cells[Planet.Nearest(at * Planet.R)].north; dir.Normalize();
            pos = at * (Planet.R + PlanetCity.TerrainAt(at) + 0.3f);
        }
        var root = L.AddPlayer(pos, dir);
        if (playerCar == null) playerCar = gameObject.AddComponent<PlayerCar>();
        playerCar.Enter(root, Cam);
        driving = true; driveT = Time.time; Sounds.Fx("whoosh");
        PlanetCity.ShowGrid(false); catOpen = todOpen = menuOpen = scoreOpen = helpOpen = welcome = wheelOpen = false; WalkHide();
        Say(best >= 0 ? "Instappen: rijden maar" : "Geen weg in de buurt: je staat op het terrein");
    }

    void StopDrive()
    {
        var L = PlanetLife.I;
        if (L != null && L.PlayerRoot != null)
        {
            Transform car = L.PlayerRoot; Vector3 up = car.position.normalized;
            focus = up; Vector3 f = car.forward - up * Vector3.Dot(car.forward, up); if (f.sqrMagnitude > 1e-6f) fwd = f.normalized;
        }
        if (playerCar != null) playerCar.Exit();
        if (L != null) L.RemovePlayer();
        driving = false; Sounds.Fx("whoosh"); PlanetCity.ShowGrid(PlanetCity.GridOn);
    }

    // rechtsonder de snelheid, onderin even de toetsen
    void DriveGui(float W, float H)
    {
        float kmh = playerCar != null ? playerCar.Kmh : 0f;
        var r = new Rect(W - G - 210f, H - G - 92f, 210f, 92f); Ui.Panel(r, false, 14f);
        Ui.Label(new Rect(r.x + 14f, r.y + 6f, 120f, 52f), Mathf.RoundToInt(kmh).ToString(), Ui.T.Title, Ui.Ink, TextAnchor.MiddleLeft, 44);
        Ui.Label(new Rect(r.x + 120f, r.y + 14f, 80f, 20f), "KM/U", Ui.T.Tiny, Ui.InkDim, TextAnchor.MiddleRight, 10);
        bool rev = playerCar != null && playerCar.Reverse;
        Ui.Label(new Rect(r.x + 120f, r.y + 32f, 80f, 24f), rev ? "R" : "D", Ui.T.Title, rev ? new Color(1f, 0.45f, 0.4f) : Ui.Signal, TextAnchor.MiddleRight, 20);
        // een rij zeshoekjes die oplicht met de snelheid (tot 180 km/u)
        int n = 12, lit = Mathf.Clamp(Mathf.CeilToInt(kmh / 180f * n), 0, n); float hx = 14f;
        for (int k = 0; k < n; k++)
        {
            Color on = k < 8 ? Ui.Signal : new Color(1f, 0.45f, 0.35f);
            Ui.Tex(new Rect(r.x + 14f + k * (hx + 1.5f), r.y + 66f, hx, hx * 1.1f), Ui.Hex(k < lit ? on : new Color(1f, 1f, 1f, 0.08f), k < lit ? on : Ui.Line, 1f, true));
        }
        float a = Mathf.Clamp01(1f - (Time.time - driveT - 7f)); if (a > 0f)
        {
            string t = "W S gas en rem · A D sturen · Spatie handrem · R rechtop · C of wiel: camera · muis rondkijken · J of Esc uitstappen"; float tw = Ui.Width(t, Ui.T.Dim) + 40f;
            var hr = new Rect((W - tw) * 0.5f, H - G - 40f, tw, 40f);
            var old = GUI.color; GUI.color = new Color(1f, 1f, 1f, a); Ui.Panel(hr, false, 20f); Ui.Label(hr, t, Ui.T.Dim, null, TextAnchor.MiddleCenter); GUI.color = old;
        }
        if (Time.time < msgT) Ui.Toast(msg, Mathf.Clamp01((msgT - Time.time) / 0.5f), G + 52f);
        if (helpOpen) Help(W, H);
    }
}
