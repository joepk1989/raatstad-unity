using System.Collections.Generic;
using UnityEngine;

// Modules in de stad (stap 1 van het modulesysteem): een zevende categorie naast de zes vaste (toets 7), met een lade vol modules
// uit de bibliotheek (voorbeeldplaatjes per module, filter per soort). Klikken zet de gekozen module neer, R draait 60° (Shift+R
// terug), F spiegelt; op een plat dak komt hij erbovenop (hooguit MV2.MAX_LV lagen). Rechts klikken haalt de bovenste module weg, de
// middelste knop neemt een module over (pipet, met draaiing en spiegeling). Alles via Push/Changed, dus undo en opslaan werken.
// Vanuit de lade: een nieuwe module tekenen of de gekozen bewerken in de Raatmaker (Raatmaker.cs).
public partial class PlanetBuilder
{
    bool modOn, modDrawer; string modId; int modRot; bool modMir; string modCat = "Alle"; int modRow;
    MeshFilter mgF, mgPF; MeshRenderer mgR, mgPR; string mgKey;
    // plaatsingswijze (T): terrein volgen (met PgUp/PgDn lagen hoger op palen) of vaste hoogte (stappen van 3 m; PgUp/PgDn kiest
    // de hoogte; na het neerzetten blijft die hoogte staan, zodat de volgende module erop aansluit; de knop Van het terrein: weer de hoogte van het vak)
    bool modFixed; int modLift; float? modFixedH;
    float ModHeightAt(int c) => modFixedH ?? PlanetCity.FixedSnap(PlanetCity.G(c));
    float? ModFixedArg(int c) => modFixed ? ModHeightAt(c) : (float?)null;
    string ModModeText => modFixed ? "Vaste hoogte " + (modFixedH.HasValue ? modFixedH.Value.ToString("0") + " m" : "(van het terrein)") : "Terrein volgen" + (modLift > 0 ? " · " + modLift + (modLift == 1 ? " laag" : " lagen") + " hoger op palen" : "");

    ModuleV2 ModCur => modId != null ? ModuleLib.Data(modId, modMir) : null;
    void ModEnter(string id = null)
    {
        modOn = true; catOpen = false; eraser = false; if (id != null) modId = id;
        if (ModuleLib.Get(modId) == null && ModuleLib.All.Count > 0) modId = ModuleLib.All[0].id;
        if (ModuleLib.Get(modId) == null) { modOn = false; Say("Geen modules gevonden"); return; }
        Say("Modules: " + ModuleLib.Get(modId).name + " · klik om neer te zetten, R draait, F spiegelt");
    }
    void ModExit() { modOn = false; modDrawer = false; ModGhost(-1); ShowPlan(null); }
    // na bewaren in de Raatmaker: die module in je hand, de stad bijgewerkt (alle exemplaren volgen)
    public void ModPick(string id) { PlanetCity.SyncModules(); mgKey = null; ModEnter(id); modDrawer = false; }
    // pipet op een module: die module (met draaiing en spiegeling) in je hand
    bool ModPipet(int i)
    {
        if (!PlanetCity.ModTop(i, out _, out var I) || ModuleLib.Get(I.id) == null) return false;
        ModEnter(I.id); modRot = I.rot; modMir = I.mir; Say("Pipet: " + ModuleLib.Get(I.id).name + (I.mir ? " (gespiegeld)" : "")); return true;
    }

    bool ModUpdate()
    {
        if (Inp.Down(K.D7)) { if (!modOn) { ModEnter(); modDrawer = modOn; } else modDrawer = !modDrawer; }
        if (!modOn) return false;
        ModuleStudio.Tick();
        // een ander gereedschap: terug naar de gewone bouwbalk
        K[] dk = { K.D1, K.D2, K.D3, K.D4, K.D5, K.D6 };
        foreach (var k in dk) if (Inp.Down(k)) { ModExit(); return false; }
        if (Inp.Down(K.Tab) || Inp.Down(K.Del) || Inp.Down(K.Back)) { ModExit(); return false; }
        if (Inp.Down(K.Esc)) { if (modDrawer) modDrawer = false; else ModExit(); return true; }
        if (Inp.Down(K.G)) ToggleGrid();
        if (Inp.Down(K.H)) GoCentre();
        if (Inp.Down(K.N)) GoPole();
        if (Inp.Down(K.K)) GoOpposite();
        if (Inp.Down(K.Z) && Inp.Held(K.Ctrl)) Undo();
        if (Inp.Down(K.R)) { modRot = (modRot + (Inp.Held(K.Shift) ? 5 : 1)) % 6; Say("Gedraaid: " + modRot * 60 + "° (R, Shift+R terug)"); }
        if (Inp.Down(K.F)) { modMir = !modMir; Say(modMir ? "Gespiegeld (F)" : "Niet gespiegeld (F)"); }
        if (Inp.Down(K.T)) { modFixed = !modFixed; modFixedH = null; mgKey = null; Say("Plaatsen: " + ModModeText + " (T wisselt, PgUp/PgDn " + (modFixed ? "hoogte" : "lagen") + ")"); }
        int dl = (Inp.Down(K.PgUp) ? 1 : 0) - (Inp.Down(K.PgDn) ? 1 : 0);
        if (dl != 0)
        {
            if (modFixed) { float h0 = modFixedH ?? (hasHover ? PlanetCity.FixedSnap(PlanetCity.G(hover)) : 0f); modFixedH = Mathf.Clamp(h0 + dl * PlanetCity.FixedStep, 0f, PlanetCity.TerrTop); }
            else modLift = Mathf.Clamp(modLift + dl, 0, MV2.MAX_LV - 1);
            mgKey = null; Say("Plaatsen: " + ModModeText);
        }

        bool leftDown = Inp.LeftDown && camDrag == 0;
        if (leftDown && !Ui.MouseOverUI && modDrawer) modDrawer = false;   // de lade gaat dicht en de klik bouwt meteen
        hasHover = false;
        if (!OverBar && Physics.Raycast(Cam.ScreenPointToRay(Inp.Pos), out RaycastHit hit, 5000f, Layers.Wijzen, QueryTriggerInteraction.Ignore))
        {
            hover = Planet.Nearest(hit.point - hit.normal * 0.3f); hasHover = true; hoverPoint = hit.point;
            if (Inp.RightDown) { rightAt = Inp.Pos; rightCell = hover; }
            if (Inp.MiddleDown) { midAt = Inp.Pos; midCell = hover; }
            if (Inp.RightUp && rightCell >= 0 && Vector2.Distance(Inp.Pos, rightAt) < 6f)
            {
                Push(); bool mod = PlanetCity.HasModule(hover);
                if (PlanetCity.Clear(hover)) { Changed(); Say(mod ? "Module weggehaald" : "Vak leeggemaakt"); } else undo.RemoveAt(undo.Count - 1);
                rightCell = -1;
            }
            if (leftDown && modId != null)
            {
                Push(); float? fh = ModFixedArg(hover); string err = PlanetCity.PlaceModule(modId, hover, modRot, modMir, modFixed ? 0 : modLift, fh);
                if (err != null) { undo.RemoveAt(undo.Count - 1); Say(err); Sounds.Fx("deny"); }
                else { if (modFixed && fh.HasValue) modFixedH = fh; Changed(); mgKey = null; Say(ModuleLib.Get(modId).name + " neergezet" + (modFixed ? " op " + fh.Value.ToString("0") + " m" : modLift > 0 ? " op palen" : "") + " (Ctrl+Z haalt hem weg)"); }
            }
        }
        if (midCell >= 0 && !Inp.MiddleHeld) { if (Vector2.Distance(Inp.Pos, midAt) < 6f && !ModPipet(midCell)) Pipet(midCell); midCell = -1; }
        outline.enabled = false; ShowBrush(-1); ShowVerts(-1); ShowGhost(-1);
        ModGhost(hasHover && !Ui.MouseOverUI ? hover : -1);
        return true;
    }

    // ---- voorbeeld onder de muis: de echte module, amber als hij past, rood als niet; de omtrek van zijn vakken ----
    void ModGhost(int c)
    {
        var M = ModCur;
        if (c < 0 || M == null) { if (mgR != null) mgR.enabled = false; if (mgPR != null) mgPR.enabled = false; if (mgKey != null) ShowPlan(null); mgKey = null; return; }
        float? fh = ModFixedArg(c); string err = PlanetCity.CanPlaceModule(M, c, modRot, out int lv, modFixed ? 0 : modLift, fh);
        bool ground = true; var area0 = PlanetCity.ModArea(c, M.k, modRot); if (area0 != null) foreach (int i in area0) if (PlanetCity.HasModule(i)) ground = false;
        float? baseG = fh.HasValue && ground ? fh : null;   // vaste hoogte op de grond: het voorbeeld staat al op die hoogte
        string key = c + "|" + modId + "|" + M.ver + "|" + modRot + "|" + modMir + "|" + lv + "|" + (err == null) + "|" + PlanetCity.G(c) + "|" + baseG;
        if (key == mgKey && mgR != null) { mgR.enabled = true; if (mgPR != null) mgPR.enabled = mgPF.sharedMesh != null; return; }
        mgKey = key;
        if (mgF == null)
        {
            var go = new GameObject("Modulevoorbeeld"); mgF = go.AddComponent<MeshFilter>(); mgR = go.AddComponent<MeshRenderer>();
            mgR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mgR.receiveShadows = false;
        }
        if (ghostOk == null) { ghostOk = Raatstad.MakeMat(new Color(0.949f, 0.698f, 0.2f, 0.42f), true); ghostBad = Raatstad.MakeMat(new Color(0.878f, 0.341f, 0.243f, 0.45f), true); }
        var b = ModuleMesh.Get(M, lv > 0, 0);
        var mats = new Material[b.mesh.subMeshCount]; for (int i = 0; i < mats.Length; i++) mats[i] = err == null ? ghostOk : ghostBad; mgR.sharedMaterials = mats;
        // vervormd naar de echte vakken, zoals het exemplaar straks (PlanetCity.WarpMesh), een paar cm hoger tegen flikkeren
        var oldG = mgF.sharedMesh; Vector3 gorg = Planet.Cells[c].dir * (Planet.R + (baseG ?? PlanetCity.G(c)) + lv * MV2.LV_H);
        mgF.sharedMesh = area0 != null ? PlanetCity.WarpMesh(b.mesh, c, area0, M.k, modRot, Planet.R + (baseG ?? PlanetCity.G(c)) + lv * MV2.LV_H + 0.03f, out gorg) : null;
        if (oldG != null && oldG != b.mesh) Destroy(oldG);
        mgF.transform.SetPositionAndRotation(gorg, Quaternion.identity); mgF.transform.localScale = Vector3.one;
        mgR.enabled = true;
        // palen eronder (terrein volgen, een laag of meer hoger)
        if (mgPF == null) { var pgo = new GameObject("Modulevoorbeeld palen"); mgPF = pgo.AddComponent<MeshFilter>(); mgPR = pgo.AddComponent<MeshRenderer>(); mgPR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
        var oldP = mgPF.sharedMesh; Vector3 porg = Vector3.zero; mgPF.sharedMesh = null; if (err == null && !baseG.HasValue) mgPF.sharedMesh = PlanetCity.PoleMesh(c, area0, lv, PlanetCity.G(c), out porg, out _); if (oldP != null) Destroy(oldP);
        if (mgPF.sharedMesh != null) { mgPF.transform.position = porg; mgPR.sharedMaterial = ghostOk; mgPR.enabled = true; } else mgPR.enabled = false;
        var area = PlanetCity.ModArea(c, M.k, modRot); ShowPlan(area != null ? new List<int>(area) : new List<int> { c });
        if (err != null) Say(err);
    }

    // ---------- interface ----------
    void ModGui(float W, float H)
    {
        if (walking || inSpace) return;
        float bw = Tabs.Length * CatW + (Tabs.Length - 1) * 3f + 12f; var r = new Rect((W + bw) * 0.5f + 8f, H - 12f - BarH, CatW + 12f, BarH); Ui.Panel(r, false, 14f);
        var sr = new Rect(r.x + 6f + (CatW - SlotW) * 0.5f, r.y + 4f, SlotW, SlotH);
        if (Ui.HexSlot(sr, Ui.Swatch("module", MV2.Hex("#c9a46a")), "huis", "7", modOn)) { if (modOn) modDrawer = !modDrawer; else { ModEnter(); modDrawer = modOn; } }
        Ui.Label(new Rect(r.x + 6f, sr.yMax + 1f, CatW, 15f), "Modules", Ui.T.Bold, modOn ? Ui.Signal : Ui.InkDim, TextAnchor.MiddleCenter, 10);
        if (!modDrawer) Ui.Tip(r, "Modules" + (ModuleLib.Get(modId) != null ? ": " + ModuleLib.Get(modId).name : ""), "Gebouwen, pleinen en parken uit de bibliotheek; R draait, F spiegelt, rechts klikken haalt weg", "7");
        if (modOn && !modDrawer) ModOptions(W, H);
        if (modDrawer) ModDrawer(W, H);
    }
    // boven de balk: de naam van de module en de knoppen draaien, spiegelen, bewerken
    void ModOptions(float W, float H)
    {
        var m = ModuleLib.Get(modId); if (m == null) return; var meta = MV2.MetaOf(m);
        string nm = m.name, info = (meta.bouw ? meta.fl + " lagen · " : "") + meta.n + (meta.n == 1 ? " vak" : " vakken") + (meta.stack ? " · plat dak" : "");
        float nw = Mathf.Max(Ui.Width(nm, Ui.T.Bold), Ui.Width(info, Ui.T.Dim, 10)) + 24f;
        string[] lab = { "−60°", "+60°", modMir ? "Gespiegeld" : "Spiegelen", "Bewerken", "Nieuw" }; float[] bwid = new float[lab.Length]; float tot = nw + 8f;
        for (int i = 0; i < lab.Length; i++) { bwid[i] = Ui.Width(lab[i], Ui.T.Bold) + 26f; tot += bwid[i] + 6f; }
        var r = new Rect((W - tot) * 0.5f, H - 12f - BarH - 8f - 42f, tot + 6f, 42f); Ui.Panel(r, true, 12f);
        Ui.Label(new Rect(r.x + 12f, r.y + 4f, nw, 18f), nm, Ui.T.Bold, Ui.Signal); Ui.Label(new Rect(r.x + 12f, r.y + 21f, nw, 16f), info, Ui.T.Dim, null, null, 10);
        float x = r.x + nw + 8f;
        for (int i = 0; i < lab.Length; i++)
        {
            var b = new Rect(x, r.y + 6f, bwid[i], 30f); x += bwid[i] + 6f;
            if (Ui.Button(b, lab[i], i == 2 && modMir))
            {
                if (i == 0) modRot = (modRot + 5) % 6; else if (i == 1) modRot = (modRot + 1) % 6; else if (i == 2) modMir = !modMir;
                else if (i == 3) Raatmaker.Show(m, this); else Raatmaker.Show(null, this);
            }
            Ui.Tip(b, i == 0 ? "Draai links" : i == 1 ? "Draai rechts" : i == 2 ? "Spiegelen" : i == 3 ? "Bewerken in de Raatmaker" : "Nieuwe module tekenen", i < 2 ? "60° per klik" : i == 2 ? "Het spiegelbeeld neerzetten" : i == 3 ? "Bewaren past alle exemplaren in de stad aan" : "Een lege module in de Raatmaker", i < 2 ? (i == 0 ? "Shift+R" : "R") : i == 2 ? "F" : null);
        }
        // daarboven: de plaatsingswijze en de hoogte
        string hv = modFixed ? (modFixedH.HasValue ? modFixedH.Value.ToString("0") + " m" : "van het terrein") : modLift == 0 ? "op het terrein" : "+" + modLift + (modLift == 1 ? " laag" : " lagen");
        float sw = 260f, hw = Mathf.Max(110f, Ui.Width(hv, Ui.T.Bold) + 20f), rw = 12f + sw + 8f + 32f + 4f + hw + 4f + 32f + 8f + 120f + 12f;
        var r2 = new Rect((W - rw) * 0.5f, r.y - 6f - 42f, rw, 42f); Ui.Panel(r2, true, 12f); float x2 = r2.x + 12f;
        int pick = Ui.Seg(new Rect(x2, r2.y + 5f, sw, 32f), new[] { "Terrein volgen", "Vaste hoogte" }, modFixed ? 1 : 0);
        if (pick >= 0 && (pick == 1) != modFixed) { modFixed = pick == 1; modFixedH = null; mgKey = null; }
        Ui.Tip(new Rect(x2, r2.y, sw, r2.height), "Plaatsingswijze", "Terrein volgen: op de hoogte van het vak, met PgUp/PgDn lagen hoger op palen. Vaste hoogte: stappen van 3 m, het terrein gaat erheen, zodat modules op dezelfde hoogte aansluiten", "T");
        x2 += sw + 8f; int dl = 0;
        if (Ui.Button(new Rect(x2, r2.y + 6f, 32f, 30f), "▼")) dl = -1; x2 += 36f;
        Ui.Label(new Rect(x2, r2.y, hw, r2.height), hv, Ui.T.Bold, Ui.Signal, TextAnchor.MiddleCenter); x2 += hw + 4f;
        if (Ui.Button(new Rect(x2, r2.y + 6f, 32f, 30f), "▲")) dl = 1; x2 += 40f;
        Ui.Tip(new Rect(x2 - 40f - hw - 40f, r2.y, hw + 80f, r2.height), modFixed ? "Hoogte" : "Lagen hoger", modFixed ? "In stappen van 3 m; na het neerzetten blijft deze hoogte staan" : "De module komt op palen boven wat eronder staat", "PgUp/PgDn");
        if (dl != 0)
        {
            if (modFixed) { float h0 = modFixedH ?? (hasHover ? PlanetCity.FixedSnap(PlanetCity.G(hover)) : 0f); modFixedH = Mathf.Clamp(h0 + dl * PlanetCity.FixedStep, 0f, PlanetCity.TerrTop); }
            else modLift = Mathf.Clamp(modLift + dl, 0, MV2.MAX_LV - 1);
            mgKey = null;
        }
        if (Ui.Button(new Rect(x2, r2.y + 6f, 120f, 30f), "Van het terrein", false, null, modFixed ? !modFixedH.HasValue : modLift == 0)) { modFixedH = null; modLift = 0; mgKey = null; }
        Ui.Tip(new Rect(x2, r2.y, 120f, r2.height), "Van het terrein", modFixed ? "De hoogte weer van het vak onder de muis (afgerond op 3 m)" : "Weer gewoon op het terrein, zonder palen");
    }
    // de lade: soorten links, de modules als kaartjes met een voorbeeldplaatje
    void ModDrawer(float W, float H)
    {
        var all = ModuleLib.All; float cw = Mathf.Min(980f, W - 24f), ch = Mathf.Min(430f, H - CatBottom - 70f); var r = new Rect((W - cw) * 0.5f, H - CatBottom - ch, cw, ch); Ui.Panel(r, true, 16f);
        Ui.Label(new Rect(r.x + 16f, r.y + 8f, 200f, 26f), "MODULES", Ui.T.Tiny, Ui.Signal, null, 11);
        var bNew = new Rect(r.xMax - 12f - 130f, r.y + 8f, 130f, 28f); if (Ui.Button(bNew, "Nieuwe module", false, null, false, 9f)) Raatmaker.Show(null, this);
        var bEd = new Rect(bNew.x - 8f - 100f, r.y + 8f, 100f, 28f); if (Ui.Button(bEd, "Bewerken", false, null, ModuleLib.Get(modId) == null, 9f)) Raatmaker.Show(ModuleLib.Get(modId), this);
        Ui.Tip(bEd, "Bewerken in de Raatmaker", "Bewaren past alle exemplaren in de stad aan");
        // soorten
        var cats = new List<string> { "Alle" }; bool eigen = false; foreach (var c in MV2.Cats) foreach (var m in all) if (m.cat == c) { cats.Add(c); break; }
        foreach (var m in all) if (m.user) { eigen = true; break; } if (eigen) cats.Add("Eigen");
        float y = r.y + 42f;
        foreach (var c in cats) { var b = new Rect(r.x + 10f, y, 140f, 24f); if (Ui.Button(b, c, c == modCat, null, false, 8f)) { modCat = c; modRow = 0; } y += 26f; if (y > r.yMax - 26f) break; }
        // kaartjes
        var list = new List<ModuleV2>(); foreach (var m in all) if (modCat == "Alle" || (modCat == "Eigen" ? m.user : m.cat == modCat)) list.Add(m);
        var g = new Rect(r.x + 160f, r.y + 42f, r.width - 170f, r.height - 50f); const float cardW = 108f, cardH = 124f;
        int per = Mathf.Max(1, Mathf.FloorToInt((g.width + 8f) / (cardW + 8f))), rows = Mathf.CeilToInt(list.Count / (float)per), vis = Mathf.Max(1, Mathf.FloorToInt((g.height + 8f) / (cardH + 8f)));
        if (g.Contains(Ui.Mouse) && Event.current.type == EventType.ScrollWheel) { modRow += Event.current.delta.y > 0f ? 1 : -1; Event.current.Use(); }
        modRow = Mathf.Clamp(modRow, 0, Mathf.Max(0, rows - vis));
        for (int k = modRow * per; k < list.Count && k < (modRow + vis) * per; k++)
        {
            var m = list[k]; int row = k / per - modRow, col = k % per; var c = new Rect(g.x + col * (cardW + 8f), g.y + row * (cardH + 8f), cardW, cardH);
            bool on = m.id == modId; if (Ui.Button(c, null, false, null, false, 10f, true)) { modId = m.id; modDrawer = false; ModEnter(m.id); }
            if (on) Ui.Box(c, Color.clear, Ui.Signal, 10f);
            var tex = ModuleStudio.Thumb(m); if (tex != null && Event.current.type == EventType.Repaint) GUI.DrawTexture(new Rect(c.x + 10f, c.y + 4f, 88f, 88f), tex, ScaleMode.ScaleToFit, true);
            string nm = m.name; int cut = nm.IndexOf(" · "); string n1 = cut > 0 ? nm.Substring(0, cut) : nm, n2 = cut > 0 ? nm.Substring(cut + 3) : "";
            Ui.Label(new Rect(c.x + 4f, c.y + 92f, c.width - 8f, 15f), n1, Ui.T.Bold, on ? Ui.Signal : Ui.Ink, TextAnchor.MiddleCenter, 10);
            if (n2 != "") Ui.Label(new Rect(c.x + 4f, c.y + 106f, c.width - 8f, 14f), n2, Ui.T.Dim, null, TextAnchor.MiddleCenter, 9);
            var meta = MV2.MetaOf(m); Ui.Tip(c, m.name, m.cat + " · " + (meta.bouw ? meta.fl + " lagen · " : "") + meta.n + (meta.n == 1 ? " vak" : " vakken") + (meta.stack ? " · plat dak, stapelbaar" : meta.top == "kap" ? " · schuin dak" : ""));
        }
        if (rows > vis) Ui.Label(new Rect(g.x, r.yMax - 22f, g.width, 16f), "scroll voor meer · " + (modRow + 1) + "–" + Mathf.Min(rows, modRow + vis) + " van " + rows + " rijen", Ui.T.Dim, null, TextAnchor.MiddleRight, 10);
    }
}
