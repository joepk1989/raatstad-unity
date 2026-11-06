using System.Collections.Generic;
using UnityEngine;

// Hoogtetest: wegen van startpunt naar startpunt (de middens van de 20 vlakken), elk startpunt naar zijn drie buren: 30 wegen over
// de landschappen heen. Ze worden gesleept zoals een speler sleept (PlaceLine, SetLevel, DipUnder, BoreLine), dus waar het terrein
// erom vraagt komen vanzelf tunnels, dalbruggen, dammen, taluds en geulen. Een rechte sleep gaat onder 30° of 60° (LinePlan); komt
// hij niet precies bij het andere startpunt uit, dan een tweede (en derde) sleep vanaf waar hij ophield.
public static partial class PlanetCity
{
    public static int HeightTestRoadCount;   // hoeveel wegen er gelegd zijn (voor de test)
    public static System.Collections.IEnumerator HeightTestRoads(System.Action<float, string> prog)
    {
        int S = Planet.Starts.Length; var pairs = new List<(int a, int b)>();
        for (int s = 0; s < S; s++)
        {
            var order = new List<int>(); for (int t = 0; t < S; t++) if (t != s) order.Add(t);
            Vector3 ds = Planet.Cells[Planet.Starts[s]].dir;
            order.Sort((p, q) => Vector3.Dot(Planet.Cells[Planet.Starts[q]].dir, ds).CompareTo(Vector3.Dot(Planet.Cells[Planet.Starts[p]].dir, ds)));
            for (int k = 0; k < 3; k++) { int t = order[k]; var pr = s < t ? (s, t) : (t, s); if (!pairs.Contains(pr)) pairs.Add(pr); }
        }
        HeightTestRoadCount = 0; DragLevel = 0; DragUnder = false; DragStyle = 0; DragBike = false;
        for (int q = 0; q < pairs.Count; q++)
        {
            int a = Planet.Starts[pairs[q].a], b = Planet.Starts[pairs[q].b], cur = a;
            for (int leg = 0; leg < 4 && cur != b; leg++)
            {
                var line = LinePlan(cur, b); if (line == null || line.Count < 2) break;
                if (PlaceLine(line, "weg") > 0) { SetLevel(line, 0, "weg"); DipUnder(line); BoreLine(line); UpdateCuts(); }
                int end = line[line.Count - 1]; if (end == cur) break; cur = end;
            }
            if (cur == b) HeightTestRoadCount++;
            prog?.Invoke((q + 1f) / pairs.Count, "Wegen tussen de startpunten"); yield return null;
        }
        TouchAll();
    }
}
