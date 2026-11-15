using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Opslaan en laden: drie slots (planeet1/2/3.json in persistentDataPath), Snapshot en Restore ook voor undo.
[Serializable] public class PCellSave { public int i; public string[] items; }
[Serializable] public class PSaveData { public int m; public float r; public List<PCellSave> cells = new List<PCellSave>(); public List<int> links = new List<int>(); public List<int> style = new List<int>(), under = new List<int>(), oneway = new List<int>(), terr = new List<int>(), low = new List<int>(), tunnel = new List<int>(), span = new List<int>(), lower = new List<int>(), stairs = new List<int>(), precarve = new List<int>(), prelevel = new List<int>(), corner = new List<int>(), center = new List<int>(), bike = new List<int>(), rot = new List<int>(), scorch = new List<int>(); public int startVer, startCell = -1, heath; }   // startVer: de versie van de startstad als de stad nog de ongewijzigde startstad is (anders 0)   // corner: per hoekpunt drie vakken en de verschuiving in cm   // paren: vak/stijl, vak/onder (1 meer, 2 water, 3 kuil, 4 pad, 5 weg), van/naar, vak/terreinhoogte in cm
public static partial class PlanetCity
{
    static string FilePath => Path.Combine(Application.persistentDataPath, "planeet" + Slot + Planet.Suffix + ".json");
    public static bool SlotExists(int n) => File.Exists(Path.Combine(Application.persistentDataPath, "planeet" + n + Planet.Suffix + ".json"));

    public static void Save()
    {
        try { File.WriteAllText(FilePath, Snapshot()); }
        catch (Exception e) { Debug.LogWarning("Opslaan mislukt: " + e.Message); }
    }
    // de hele stad als tekst (zoals het opslagbestand): ook verbindingen, bruggen en de terreinhoogtes van voor een geul of niveau,
    // zodat ongedaan maken alles terugzet (Restore)
    public static string Snapshot()
    {
        {
            var sd = new PSaveData { m = Planet.M, r = Planet.R };
            foreach (var kv in Cells) if (kv.Value.Count > 0) sd.cells.Add(new PCellSave { i = kv.Key, items = kv.Value.ToArray() });
            foreach (long k in Links) { sd.links.Add((int)(k >> 20)); sd.links.Add((int)(k & 0xFFFFF)); }
            foreach (var kv in Style) if (IsRoad(kv.Key)) { sd.style.Add(kv.Key); sd.style.Add(kv.Value); }
            foreach (var kv in Under) { sd.under.Add(kv.Key); sd.under.Add(kv.Value == "meer" ? 1 : kv.Value == "water" ? 2 : kv.Value == "pad" ? 4 : kv.Value == "weg" ? 5 : 3); }
            foreach (long k in TunnelLinks) { sd.tunnel.Add((int)(k >> 20)); sd.tunnel.Add((int)(k & 0xFFFFF)); }
            foreach (long k in SpanLinks) if (TunnelLinks.Contains(k)) { sd.span.Add((int)(k >> 20)); sd.span.Add((int)(k & 0xFFFFF)); }   // welke lange verbindingen een brug over een dal zijn
            foreach (long k in LowerLinks) { sd.lower.Add((int)(k >> 20)); sd.lower.Add((int)(k & 0xFFFFF)); }
            foreach (long k in Stairs) if (Links.Contains(k)) { sd.stairs.Add((int)(k >> 20)); sd.stairs.Add((int)(k & 0xFFFFF)); }   // trappen (MarkStairs)
            foreach (var kv in preCarve) { sd.precarve.Add(kv.Key); sd.precarve.Add(Mathf.RoundToInt(kv.Value * 100f)); }
            foreach (var kv in preLevel) { sd.prelevel.Add(kv.Key); sd.prelevel.Add(Mathf.RoundToInt(kv.Value * 100f)); }
            foreach (var kv in Low) { sd.low.Add(kv.Key); sd.low.Add(Mathf.RoundToInt(kv.Value * 100f)); }
            foreach (long k in OneWay) { sd.oneway.Add((int)(k >> 20)); sd.oneway.Add((int)(k & 0xFFFFF)); }
            foreach (var kv in Terr) { sd.terr.Add(kv.Key); sd.terr.Add(Mathf.RoundToInt(kv.Value * 100f)); }
            foreach (var kv in Scorch) { sd.scorch.Add(kv.Key); sd.scorch.Add(kv.Value); }   // een verse krater (PlanetCity.Scorch.cs)
            foreach (var kv in CornerOfs) { sd.corner.Add((int)(kv.Key >> 40)); sd.corner.Add((int)((kv.Key >> 20) & 0xFFFFF)); sd.corner.Add((int)(kv.Key & 0xFFFFF)); sd.corner.Add(Mathf.RoundToInt(kv.Value * 100f)); }
            foreach (var kv in CenterOfs) { sd.center.Add(kv.Key); sd.center.Add(Mathf.RoundToInt(kv.Value * 100f)); }
            foreach (int b in Bike) if (Cells.ContainsKey(b)) sd.bike.Add(b);
            foreach (int c in Rot) if (IsRoad(c)) sd.rot.Add(c);
            sd.startVer = StartVer; sd.startCell = StartCell; sd.heath = HeathVer;
            return JsonUtility.ToJson(sd);
        }
    }

    public static bool Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return false;
            if (!Restore(File.ReadAllText(FilePath))) return false;
            if (StartVer > 0 && StartVer < StartTownVersion) { int s = StartCell >= 0 && StartCell < Planet.Cells.Length ? StartCell : Planet.North; ClearAll(); StartTown(s); Save(); }   // de nieuwe startstad
            else if (UpgradeHeath()) Save();   // heidevelden erbij, alleen op lege vakken (PlanetHeath.cs)
            return true;
        }
        catch (Exception e) { Debug.LogWarning("Laden mislukt: " + e.Message); return false; }
    }
    public static bool Restore(string json)
    {
        try
        {
            var sd = JsonUtility.FromJson<PSaveData>(json); if (sd == null || sd.m != Planet.M) return false;
            StartVer = sd.startVer; StartCell = sd.startCell; startFresh = false; HeathVer = sd.heath;
            Cells = new Dictionary<int, List<string>>();
            foreach (var c in sd.cells) if (c.items != null && c.items.Length > 0 && c.i >= 0 && c.i < Planet.Cells.Length) Cells[c.i] = new List<string>(c.items);
            ClearLinks();
            if (sd.links != null && sd.links.Count > 0) { for (int k = 0; k + 1 < sd.links.Count; k += 2) Link(sd.links[k], sd.links[k + 1]); }
            else foreach (var kv in Cells) if (IsWalk(kv.Key)) foreach (int j in Planet.Cells[kv.Key].nb) if (IsWalk(j)) Link(kv.Key, j);   // oude opslag: alles naast elkaar verbonden
            if (sd.style != null) for (int k = 0; k + 1 < sd.style.Count; k += 2) Style[sd.style[k]] = (byte)sd.style[k + 1];
            if (sd.bike != null) foreach (int b in sd.bike) Bike.Add(b);
            if (sd.rot != null) foreach (int c in sd.rot) Rot.Add(c);
            if (sd.under != null) for (int k = 0; k + 1 < sd.under.Count; k += 2) Under[sd.under[k]] = sd.under[k + 1] == 1 ? "meer" : sd.under[k + 1] == 2 ? "water" : sd.under[k + 1] == 4 ? "pad" : sd.under[k + 1] == 5 ? "weg" : "kuil";
            if (sd.low != null) for (int k = 0; k + 1 < sd.low.Count; k += 2) Low[sd.low[k]] = sd.low[k + 1] / 100f;
            if (sd.tunnel != null) for (int k = 0; k + 1 < sd.tunnel.Count; k += 2) LinkTunnel(sd.tunnel[k], sd.tunnel[k + 1]);
            SpanLinks.Clear(); if (sd.span != null) for (int k = 0; k + 1 < sd.span.Count; k += 2) SpanLinks.Add(Key(sd.span[k], sd.span[k + 1]));
            if (sd.lower != null) for (int k = 0; k + 1 < sd.lower.Count; k += 2) LowerLinks.Add(Key(sd.lower[k], sd.lower[k + 1]));
            Stairs.Clear(); if (sd.stairs != null) for (int k = 0; k + 1 < sd.stairs.Count; k += 2) Stairs.Add(Key(sd.stairs[k], sd.stairs[k + 1]));
            if (sd.precarve != null) for (int k = 0; k + 1 < sd.precarve.Count; k += 2) preCarve[sd.precarve[k]] = sd.precarve[k + 1] / 100f;
            if (sd.prelevel != null) for (int k = 0; k + 1 < sd.prelevel.Count; k += 2) preLevel[sd.prelevel[k]] = sd.prelevel[k + 1] / 100f;
            if (sd.oneway != null) for (int k = 0; k + 1 < sd.oneway.Count; k += 2) OneWay.Add(DKey(sd.oneway[k], sd.oneway[k + 1]));
            Terr.Clear(); CornerOfs.Clear(); CenterOfs.Clear(); Scorch.Clear();
            if (sd.scorch != null) for (int k = 0; k + 1 < sd.scorch.Count; k += 2) if (sd.scorch[k] >= 0 && sd.scorch[k] < Planet.Cells.Length) Scorch[sd.scorch[k]] = (byte)sd.scorch[k + 1];
            if (sd.center != null) for (int k = 0; k + 1 < sd.center.Count; k += 2) CenterOfs[sd.center[k]] = sd.center[k + 1] / 100f;
            if (sd.corner != null) for (int k = 0; k + 3 < sd.corner.Count; k += 4) CornerOfs[((long)sd.corner[k] << 40) | ((long)sd.corner[k + 1] << 20) | (uint)sd.corner[k + 2]] = sd.corner[k + 3] / 100f;
            if (sd.terr != null) for (int k = 0; k + 1 < sd.terr.Count; k += 2) if (sd.terr[k] >= 0 && sd.terr[k] < Planet.Cells.Length && sd.terr[k + 1] != 0) Terr[sd.terr[k]] = Mathf.Clamp(sd.terr[k + 1] / 100f, TerrLow, TerrTop);
            // oude heuvelblokken onderaan een stapel worden terreinhoogte (3 m elk), net als terrMigrate in de webversie
            foreach (int k in new List<int>(Cells.Keys))
            {
                var l = Cells[k]; int n = 0; while (n < l.Count && l[n] == "heuvel") n++;
                if (n == 0) continue;
                Terr[k] = Mathf.Min(TerrTop, G(k) + n * 3f); l.RemoveRange(0, n); if (l.Count == 0) Cells.Remove(k);
            }
            FixCrossings();
            RoadH.Clear(); Reprofile();   // Terrein v2: het wegprofiel opnieuw (het wordt niet opgeslagen)
            TouchAll(); RebuildGrid(); return true;
        }
        catch (Exception e) { Debug.LogWarning("Laden mislukt: " + e.Message); return false; }
    }
}
