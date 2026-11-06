using System.Collections.Generic;
using UnityEngine;

// Naambordjes boven de bakens (ontwikkelaarshulp, de bakens zelf tekent PlanetCity.Ground): "Pentagon 1–12" boven de vijfhoeken
// (Vijfhoeken en vervorming) en "Start 1–20" boven de startplekken (Startplekken), van dichtbij met een regel uitleg. Zwevend
// boven de punt, vervaagt met de afstand en verdwijnt achter de bol.
public partial class PlanetBuilder
{
    static int[] pentagons;
    void BeaconLabels(float W, float H)
    {
        if (Dev.Pentagons)
        {
            if (pentagons == null) { var l = new List<int>(); for (int i = 0; i < Planet.Cells.Length; i++) if (Planet.Cells[i].corners.Length == 5) l.Add(i); pentagons = l.ToArray(); }
            for (int k = 0; k < pentagons.Length; k++)
                Plate(W, H, pentagons[k], 33f, "Pentagon " + (k + 1), "vijfhoek · 5 buren, de zeshoeken eromheen vervormen");
        }
        if (Dev.Starts)
            for (int k = 0; k < Planet.Starts.Length; k++)
            {
                int i = Planet.Starts[k]; if (Planet.IsPole(i)) continue;   // op een pool staat de poolpaal
                Plate(W, H, i, 34f, "Start " + (k + 1), "midden van een icosaëdervlak · het netste raster");
            }
    }
    // één bordje: de naam groot, de uitleg klein eronder (alleen van dichtbij)
    void Plate(float W, float H, int i, float up, string name, string info)
    {
        var c = Planet.Cells[i]; Vector3 cam = Cam.transform.position, top = c.dir * (Planet.R + PlanetCity.G(i) + up);
        if (Vector3.Dot(c.dir, (cam - top).normalized) < -0.05f) return;   // achter de bol
        float d = Vector3.Distance(cam, top), a = Mathf.Clamp01((2200f - d) / 600f) * Mathf.Clamp01((d - 15f) / 25f); if (a <= 0.01f) return;
        Vector3 sp = Cam.WorldToScreenPoint(top); if (sp.z <= 0f) return;
        var p = new Vector2(sp.x / Ui.S, (Screen.height - sp.y) / Ui.S);
        if (p.x < -200f || p.x > W + 200f || p.y < -60f || p.y > H + 60f) return;
        bool near = d < 500f; int size = d < 300f ? 15 : 12;
        float nw = Ui.Width(name, Ui.T.Bold, size) + 14f, tw = Mathf.Max(nw + 18f, near ? Ui.Width(info, Ui.T.Dim, 10) + 22f : 0f), th = near ? 36f : 22f;
        var r = new Rect(p.x - tw * 0.5f, p.y - th - 4f, tw, th);
        var old = GUI.color; GUI.color = new Color(1f, 1f, 1f, a);
        Ui.Box(r, new Color(0.06f, 0.08f, 0.1f, 0.8f), new Color(Ui.Signal.r, Ui.Signal.g, Ui.Signal.b, 0.8f), 11f);
        float nx = r.center.x - nw * 0.5f;
        Ui.Tex(new Rect(nx, r.y + 6.5f, 10f, 9f), Ui.Hex(Ui.Signal, Ui.Signal, 0f));   // het amber zeshoekje van de huisstijl
        Ui.Label(new Rect(nx + 14f, r.y, nw, 22f), name, Ui.T.Bold, Ui.Ink, TextAnchor.MiddleLeft, size);
        if (near) Ui.Label(new Rect(r.x, r.y + 18f, r.width, 16f), info, Ui.T.Dim, null, TextAnchor.MiddleCenter, 10);
        GUI.color = old;
    }
}
