using UnityEngine;

// Een inslag (PlanetMeteor.cs): auto's in de buurt vliegen tollend weg en raken beschadigd (dicht bij de krater: brand),
// mensen dichtbij gaan tegen de grond, verder weg schrikken ze en blijven even liggen. Alleen deze ene openbare aanroep.
public partial class PlanetLife
{
    public static void Blast(Vector3 at, float radius)
    {
        var L = I; if (L == null) return;
        float reach = radius * 3f;
        foreach (var c in L.cars)
        {
            if (c.dead || c.root == null) continue;
            Vector3 p = c.root.position, up = p.normalized, d = Flat(p - at, up); float dist = d.magnitude;
            if (dist > reach) continue;
            float k = 1f - dist / reach; Vector3 away = dist > 0.1f ? d / dist : Random.onUnitSphere;
            L.GoFree(c, Flat(away, up).normalized * Mathf.Lerp(4f, 26f, k), Random.Range(-3f, 3f) * k, 3f);
            c.air = true; c.vy = Mathf.Lerp(2f, 16f, k); c.tumbleAxis = Random.onUnitSphere; c.tumbleW = Random.Range(120f, 480f) * k;
            c.dmgCool = 0f; L.Damage(c, k > 0.6f ? 200f : 60f * k);
        }
        foreach (var a in L.People)
        {
            if (a.root == null) continue;
            float dist = Vector3.Distance(a.root.position, at); if (dist > reach * 1.3f) continue;
            a.downT = Mathf.Lerp(3f, 9f, 1f - dist / (reach * 1.3f)); a.paused = false; a.partner = null;
        }
        L.LastIncident = at; L.LastIncidentT = Time.time;
    }
}
