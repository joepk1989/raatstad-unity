using UnityEngine;

// Schrikken als er iets gesloopt wordt (PlanetBuilder.Wreck.cs): mensen binnen screamR gillen of kreunen, binnen knockR duiken ze
// weg (zoals bij een inslag, maar zachter); dieren vluchten via LastIncident (PlanetAnimals).
public partial class PlanetLife
{
    public static void Startle(Vector3 at, float knockR, float screamR)
    {
        var L = I; if (L == null) return;
        int screams = 0;
        foreach (var a in L.People)
        {
            if (a.root == null || a.downT > 0f || Ragged(a)) continue;
            float d = Vector3.Distance(a.root.position, at); if (d > screamR) continue;
            Vector3 up = a.root.position.normalized;
            if (d < knockR) { L.KnockBlast(a, at, 0.15f * (1f - d / knockR), 2.5f); continue; }
            if (screams < 2 && Random.value < 0.6f) { screams++; Aanrijdgeluid.Scream(a.root.position + up * 1.5f, "horror", a); }
        }
        L.LastIncident = at; L.LastIncidentT = Time.time;
    }
}
