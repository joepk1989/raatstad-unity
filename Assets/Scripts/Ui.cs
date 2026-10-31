using System.Collections.Generic;
using UnityEngine;

// De interface, naar de bediening (v100) van de webversie (style.css "Bediening v100"): donkere panelen met een dunne lichte
// rand (--panel, --line), amber als accent (--signal), Atkinson Hyperlegible voor tekst en Big Shoulders voor koppen.
// Alles met IMGUI (OnGUI). De interface schaalt mee met de schermhoogte (S); tekenen gebeurt in een virtueel scherm W × H.
// Elk getekend paneel en elke knop meldt zijn rechthoek (Hit), zodat een klik daarop nooit in de stad terechtkomt (MouseOverUI).
public static class Ui
{
    static Color C(int r, int g, int b, float a = 1f) => new Color(r / 255f, g / 255f, b / 255f, a);
    public static readonly Color PanelBg = C(20, 26, 33, 0.86f), PanelSolid = C(22, 28, 35), Ink = C(238, 241, 236), InkDim = C(168, 178, 171),
        Line = C(238, 241, 236, 0.14f), Signal = C(242, 178, 51), SignalInk = C(29, 23, 8), SlotFill = C(27, 35, 43), SlotHover = C(38, 48, 58), SlotOn = C(59, 48, 20);
    public static readonly Color Amber = Signal;
    public static float S = 1f, W = 1280f, H = 720f;
    // interfacegrootte uit Instellingen (90–130 %), bewaard tussen sessies
    static float size = -1f;
    public static float Size { get { if (size < 0f) size = PlayerPrefs.GetFloat("raatstad-ui-grootte", 1f); return size; } set { size = value; PlayerPrefs.SetFloat("raatstad-ui-grootte", value); } }
    public static Vector2 Mouse => new Vector2(Inp.Pos.x / S, (Screen.height - Inp.Pos.y) / S);

    // ---------- klikvlakken ----------
    static readonly List<Rect> hitsNow = new List<Rect>(), hitsLast = new List<Rect>();
    public static bool MouseOverUI { get { var m = Mouse; foreach (var r in hitsLast) if (r.Contains(m)) return true; return false; } }
    public static void Hit(Rect r) { if (Event.current != null && Event.current.type == EventType.Repaint) hitsNow.Add(r); }
    public static void Begin()
    {
        Init(); S = Mathf.Min(Mathf.Clamp(Screen.height / 900f, 0.85f, 1.7f) * Size, Screen.height / 790f); W = Screen.width / S; H = Screen.height / S;   // hooguit zo groot dat het menu nog past
        GUI.matrix = Matrix4x4.Scale(new Vector3(S, S, 1f));
        if (Event.current.type == EventType.Repaint) hitsNow.Clear();
        if (Event.current.type == EventType.MouseDown) pressed = false;   // een nieuwe klik: alleen de knop die hem nu krijgt, telt
    }
    public static void End()
    {
        if (Event.current.type == EventType.Repaint)
        {
            if (tipSet && Time.unscaledTime - tipT > 0.35f) DrawTip();   // als laatste: boven alles
            if (!tipSet) tipId = null; tipSet = false;
            hitsLast.Clear(); hitsLast.AddRange(hitsNow);
        }
        GUI.matrix = Matrix4x4.identity;
    }

    // ---------- uitleg bij aanwijzen: naam, wat het doet en de toets, na een korte pauze boven het element ----------
    static string tipId, tipTitle, tipSub, tipKey; static float tipT; static Rect tipR; static bool tipSet;
    public static void Tip(Rect over, string title, string sub = null, string key = null)
    {
        if (!over.Contains(Mouse)) return;
        string id = title + over.x + "," + over.y; if (id != tipId) { tipId = id; tipT = Time.unscaledTime; }
        tipSet = true; tipR = over; tipTitle = title; tipSub = sub; tipKey = key;
    }
    static void DrawTip()
    {
        float kw = tipKey != null ? Mathf.Max(20f, Width(tipKey, T.Kbd) + 12f) : 0f;
        float w = Mathf.Max(Width(tipTitle, T.Bold) + (kw > 0f ? kw + 10f : 0f), tipSub != null ? Width(tipSub, T.Dim) : 0f) + 24f, h = tipSub != null ? 48f : 30f;
        float x = Mathf.Clamp(tipR.center.x - w * 0.5f, 8f, W - w - 8f), y = tipR.y - 8f - h; if (y < 8f) y = tipR.yMax + 8f;
        var r = new Rect(x, y, w, h); Box(r, PanelSolid, Line, 10f);
        Label(new Rect(r.x + 12f, r.y + 6f, w - 24f, 18f), tipTitle, T.Bold);
        if (kw > 0f) Kbd(new Rect(r.xMax - 12f - kw, r.y + 7f, kw, 17f), tipKey);
        if (tipSub != null) Label(new Rect(r.x + 12f, r.y + 25f, w - 24f, 16f), tipSub, T.Dim);
    }

    // ---------- stijlen en texturen ----------
    static Font fontR, fontB, fontTitle;
    static GUIStyle sText, sBold, sDim, sTiny, sTitle, sBig, sBtn, sCenter, sKbd, sNum, sHelp, sToast;
    static readonly Dictionary<string, Texture2D> texCache = new Dictionary<string, Texture2D>();
    public static bool Ready => sText != null;
    public static void ResetStatics() { sText = null; texCache.Clear(); fontR = fontB = fontTitle = null; hitsNow.Clear(); hitsLast.Clear(); pressed = false; }

    public static void Init()
    {
        if (sText != null && texCache.Count > 0 && texCache.TryGetValue("chk", out var chk) && chk != null) return;   // na een hercompilatie tijdens Play zijn de texturen weg: opnieuw maken
        texCache.Clear(); texCache["chk"] = Texture2D.whiteTexture;
        // lettertypen zoals in de browser (Assets/Resources/Fonts, OFL-licentie); ontbreken ze, dan het standaardlettertype
        fontR = Resources.Load<Font>("Fonts/AtkinsonHyperlegible-Regular"); fontB = Resources.Load<Font>("Fonts/AtkinsonHyperlegible-Bold"); fontTitle = Resources.Load<Font>("Fonts/BigShouldersDisplay-ExtraBold");
        System.Func<Font, int, Color, TextAnchor, GUIStyle> st = (f, size, col, al) => { var s = new GUIStyle { font = f, fontSize = size, alignment = al, richText = true, wordWrap = false, clipping = TextClipping.Overflow }; s.normal.textColor = col; return s; };
        sText = st(fontR, 13, Ink, TextAnchor.MiddleLeft); sBold = st(fontB, 13, Ink, TextAnchor.MiddleLeft); sDim = st(fontR, 12, InkDim, TextAnchor.MiddleLeft);
        sTiny = st(fontB, 10, InkDim, TextAnchor.MiddleLeft); sTitle = st(fontTitle, 21, Ink, TextAnchor.MiddleLeft); sBig = st(fontTitle, 30, Ink, TextAnchor.MiddleLeft);
        sBtn = st(fontB, 13, Ink, TextAnchor.MiddleCenter); sCenter = st(fontR, 13, Ink, TextAnchor.MiddleCenter); sKbd = st(fontB, 10, InkDim, TextAnchor.MiddleCenter);
        sNum = st(fontB, 9, InkDim, TextAnchor.MiddleCenter); sHelp = st(fontR, 13, Ink, TextAnchor.UpperLeft); sHelp.wordWrap = true; sToast = st(fontB, 13, Ink, TextAnchor.MiddleCenter);
    }
    public enum T { Text, Bold, Dim, Tiny, Title, Big, Center, Kbd, Help }
    static GUIStyle Style(T t) { switch (t) { case T.Bold: return sBold; case T.Dim: return sDim; case T.Tiny: return sTiny; case T.Title: return sTitle; case T.Big: return sBig; case T.Center: return sCenter; case T.Kbd: return sKbd; case T.Help: return sHelp; default: return sText; } }
    public static void Label(Rect r, string s, T t = T.Text, Color? col = null, TextAnchor? al = null, int size = 0)
    {
        var st = Style(t); var oc = st.normal.textColor; var oa = st.alignment; int os = st.fontSize;
        if (col.HasValue) st.normal.textColor = col.Value; if (al.HasValue) st.alignment = al.Value; if (size > 0) st.fontSize = size;
        GUI.Label(r, s, st); st.normal.textColor = oc; st.alignment = oa; st.fontSize = os;
    }
    public static float Width(string s, T t = T.Text, int size = 0) { var st = Style(t); int os = st.fontSize; if (size > 0) st.fontSize = size; float w = st.CalcSize(new GUIContent(s)).x; st.fontSize = os; return w; }

    // afgeronde rechthoek met rand, als 9-slice
    static GUIStyle Round(Color fill, Color border, int radius)
    {
        string key = "r" + radius + fill + border; if (!texCache.TryGetValue(key, out var t) || t == null)
        {
            int size = radius * 2 + 4; t = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp }; var px = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, radius, size - radius), cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                float d = radius - Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));   // afstand tot de buitenrand (binnen positief)
                float a = Mathf.Clamp01(d + 0.5f), inner = Mathf.Clamp01(d - 0.5f);
                Color c = Color.Lerp(border, fill, inner); c.a = Mathf.Lerp(border.a, fill.a, inner) * a; px[y * size + x] = c;
            }
            t.SetPixels(px); t.Apply(); texCache[key] = t;
        }
        var s = new GUIStyle { border = new RectOffset { left = radius + 2, right = radius + 2, top = radius + 2, bottom = radius + 2 } }; s.normal.background = t; return s;
    }
    static void Box(Rect r, Color fill, Color border, float radius)
    {
        if (Event.current.type != EventType.Repaint) return;
        int rad = Mathf.Clamp(Mathf.RoundToInt(Mathf.Min(radius, Mathf.Min(r.width, r.height) * 0.5f)), 2, 40);
        Round(fill, border, rad).Draw(r, false, false, false, false);
    }
    // zeshoek (platte boven- en onderkant, zoals .hslot; puntig: zoals het logo) met een rand
    public static Texture2D Hex(Color fill, Color border, float bw, bool pointy = false)
    {
        string key = "h" + fill + border + bw + pointy; if (texCache.TryGetValue(key, out var t) && t != null) return t;
        int w = pointy ? 56 : 64, h = pointy ? 64 : 58; t = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        Vector2[] p = pointy ? new[] { new Vector2(0.5f, 0.01f), new Vector2(0.99f, 0.26f), new Vector2(0.99f, 0.74f), new Vector2(0.5f, 0.99f), new Vector2(0.01f, 0.74f), new Vector2(0.01f, 0.26f) }
                             : new[] { new Vector2(0.25f, 0.02f), new Vector2(0.75f, 0.02f), new Vector2(1f, 0.5f), new Vector2(0.75f, 0.98f), new Vector2(0.25f, 0.98f), new Vector2(0f, 0.5f) };
        for (int k = 0; k < 6; k++) p[k] = new Vector2(p[k].x * w, p[k].y * h);
        var px = new Color[w * h];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
        {
            Vector2 q = new Vector2(x + 0.5f, y + 0.5f); float d = 1e9f;
            for (int k = 0; k < 6; k++) { Vector2 a = p[k], b = p[(k + 1) % 6], n = new Vector2(b.y - a.y, a.x - b.x).normalized; d = Mathf.Min(d, -Vector2.Dot(q - a, n)); }   // afstand tot de rand, binnen positief
            float al = Mathf.Clamp01(d + 0.5f), inner = Mathf.Clamp01(d - bw + 0.5f);
            Color c = Color.Lerp(border, fill, inner); c.a = Mathf.Lerp(border.a, fill.a, inner) * al; px[(h - 1 - y) * w + x] = c;
        }
        t.SetPixels(px); t.Apply(); texCache[key] = t; return t;
    }
    // het blokje in de snelbalk en de catalogus: een zeshoek met het patroon van het blok (zoals .sw-… in style.css van de webversie)
    public static Texture2D Swatch(string id, Color col)
    {
        string key = "s:" + id; if (texCache.TryGetValue(key, out var t) && t != null) return t;
        const int w = 64, h = 58; t = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        Vector2[] p = { new Vector2(0.25f * w, 0.02f * h), new Vector2(0.75f * w, 0.02f * h), new Vector2(w, 0.5f * h), new Vector2(0.75f * w, 0.98f * h), new Vector2(0.25f * w, 0.98f * h), new Vector2(0f, 0.5f * h) };
        var px = new Color[w * h];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
        {
            Vector2 q = new Vector2(x + 0.5f, y + 0.5f); float d = 1e9f;
            for (int k = 0; k < 6; k++) { Vector2 a = p[k], b = p[(k + 1) % 6], n = new Vector2(b.y - a.y, a.x - b.x).normalized; d = Mathf.Min(d, -Vector2.Dot(q - a, n)); }
            Color c = SwatchPx(id, q.x / w, q.y / h, col); c.a *= Mathf.Clamp01(d + 0.5f); px[(h - 1 - y) * w + x] = c;   // y = 0 is boven
        }
        t.SetPixels(px); t.Apply(); texCache[key] = t; return t;
    }
    static Color X(string s) { ColorUtility.TryParseHtmlString(s, out var c); return c; }
    static bool Band(float u, float a, float b) => u >= a && u < b;
    // de kleur van het patroon op (u, v), beide 0..1, v van boven naar onder
    static Color SwatchPx(string id, float u, float v, Color col)
    {
        float ex = Mathf.Abs(u - 0.5f) * 2f, ey = Mathf.Abs(v - 0.55f) * 2f;
        switch (id)
        {
            case "weg": return Band(u, 0.48f, 0.52f) && Mathf.Repeat(v * 5f, 1f) < 0.5f ? X("#f1efe6") : Band(u, 0.22f, 0.78f) ? X("#3c4045") : X("#cbc8bd");
            case "weg:breed": return (Band(u, 0.44f, 0.47f) || Band(u, 0.53f, 0.56f)) ? X("#f1efe6") : Band(u, 0.14f, 0.86f) ? X("#3c4045") : X("#cbc8bd");
            case "weg:laan":
                return u < 0.1f || u >= 0.9f ? X("#cbc8bd") : u < 0.18f || u >= 0.82f ? X("#9b5a45") : Band(u, 0.42f, 0.58f) ? X("#6aa84a") : X("#3c4045");
            case "pad": return Band(u, 0.25f, 0.75f) ? (Mathf.Repeat(v * 9f, 1f) < 0.85f ? X("#d4c8b0") : X("#b3a68c")) : X("#6fa356");
            case "woning": return Mathf.Repeat(v * 7f, 1f) < 0.8f ? X("#9c4a33") : X("#c9b9a6");
            case "kantoor": return Mathf.Repeat(u * 5f, 1f) < 0.22f ? X("#2e3b45") : Color.Lerp(X("#9fc0d6"), X("#4f7088"), v);
            case "winkel": return v < 0.42f ? (Mathf.Repeat(u * 5f, 1f) < 0.5f ? X("#d9493f") : X("#f3efe6")) : v < 0.48f ? X("#3a2a22") : Color.Lerp(X("#b8d6e6"), X("#6f97ad"), (v - 0.48f) * 2f);
            case "dak": return Color.Lerp(X("#a5503b"), X("#6e2e21"), (u * 0.35f + v) / 1.35f);
            case "blok": return Color.Lerp(X("#e2d9c8"), X("#c2b69f"), v);
            case "toren": return Band(u, 0.3f, 0.7f) ? (Mathf.Repeat(v * 8f, 1f) < 0.35f && Mathf.Repeat(u * 10f, 1f) < 0.5f ? X("#cfe0ea") : X("#5b6370")) : X("#8eab69");
            case "molen":
            {
                float a = Mathf.Abs(u - 0.5f - (v - 0.5f)), b = Mathf.Abs(u - 0.5f + (v - 0.5f));
                return a < 0.07f || b < 0.07f ? X("#f6f3ea") : Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) < 0.09f ? X("#6b3a2d") : X("#8fbfe0");
            }
            case "park":
                return Vector2.Distance(new Vector2(u, v), new Vector2(0.36f, 0.4f)) < 0.17f ? X("#2f6b3f") : Vector2.Distance(new Vector2(u, v), new Vector2(0.66f, 0.62f)) < 0.14f ? X("#3f7d3a") : X("#6fa356");
            case "plein": return Mathf.Repeat(u * 6f, 1f) < 0.08f || Mathf.Repeat(v * 6f, 1f) < 0.08f ? X("#a48f7a") : X("#cdb9a2");
            case "water": return Mathf.Abs(Mathf.Repeat(v * 4f + Mathf.Sin(u * 12f) * 0.12f, 1f) - 0.5f) < 0.05f ? X("#9ccbe6") : Color.Lerp(X("#5fa3cf"), X("#2f6f9c"), v);
            case "meer": { float r = Mathf.Sqrt(ex * ex + ey * ey * 1.6f); return r < 0.62f ? X("#3f86b3") : r < 0.76f ? X("#d8c48f") : X("#6fa356"); }
            case "kuil": return u < 0.18f || u >= 0.82f ? X("#6fa356") : Color.Lerp(X("#7c8a91"), X("#5a666c"), v);
            default:
                if (id.StartsWith("t:")) return Color.Lerp(X("#6fa356"), X("#4f8a3c"), v);   // terrein: groen, met het pictogram erop
                return col;
        }
    }
    public static void Tex(Rect r, Texture2D t, Color? tint = null)
    {
        if (t == null || Event.current.type != EventType.Repaint) return;
        var old = GUI.color; if (tint.HasValue) GUI.color = tint.Value; GUI.DrawTexture(r, t, ScaleMode.StretchToFill, true); GUI.color = old;
    }

    // ---------- bouwstenen ----------
    public static void Panel(Rect r, bool solid = false, float radius = 14f) { Hit(r); Box(r, solid ? PanelSolid : PanelBg, Line, radius); }
    static bool Hover(Rect r) => r.Contains(Mouse);
    // Een klik: indrukken en loslaten op dezelfde knop. Op de rechthoek en niet op Unity's control-id, want die verschuift als er
    // tussen indrukken en loslaten ergens anders iets verschijnt of verdwijnt (een melding, een label); dan ging de klik verloren.
    static Rect pressRect; static bool pressed;
    static bool Clicked(Rect r)
    {
        Hit(r); var e = Event.current; if (e == null || e.button != 0) return false;
        // de positie die bij de klik zelf hoort (de muis van het Input System blijft in de editor op de laatste plek in het
        // spelbeeld staan, ook als je ergens anders klikt, zoals op de Play-knop)
        if (e.type == EventType.MouseDown && r.Contains(e.mousePosition)) { pressed = true; pressRect = r; e.Use(); return false; }
        if (e.type == EventType.MouseUp && pressed && SameRect(pressRect, r) && r.Contains(e.mousePosition)) { pressed = false; e.Use(); return true; }
        return false;
    }
    static bool SameRect(Rect a, Rect b) => Mathf.Abs(a.x - b.x) < 2f && Mathf.Abs(a.y - b.y) < 2f && Mathf.Abs(a.width - b.width) < 2f && Mathf.Abs(a.height - b.height) < 2f;
    // knop met tekst (en eventueel een pictogram links); on: amber
    public static bool Button(Rect r, string text, bool on = false, string icon = null, bool disabled = false, float radius = 10f, bool solid = false)
    {
        bool hv = Hover(r) && !disabled; Color fill = on ? Signal : solid ? PanelSolid : PanelBg; if (hv && !on) fill = Color.Lerp(fill, Color.white, 0.08f);
        Box(r, fill, on ? new Color(0, 0, 0, 0) : Line, radius);
        Color ink = on ? SignalInk : Ink; if (disabled) ink.a = 0.35f;
        float iw = icon != null ? 18f : 0f, tw = string.IsNullOrEmpty(text) ? 0f : Width(text, T.Bold), gap = icon != null && tw > 0f ? 7f : 0f, x = r.x + (r.width - iw - gap - tw) * 0.5f;
        if (icon != null) Tex(new Rect(x, r.y + (r.height - 18f) * 0.5f, 18f, 18f), Icon(icon), ink);
        if (tw > 0f) Label(new Rect(x + iw + gap, r.y, tw + 2f, r.height), text, T.Bold, ink);
        return Clicked(r) && !disabled;
    }
    // ronde knop met alleen een pictogram (zoals .rt)
    public static bool RoundButton(Rect r, string icon, bool on = false, bool disabled = false) => Button(r, null, on, icon, disabled, r.height * 0.5f);
    // een kaart in het menu: titel vet, regel eronder gedimd
    public static bool CardButton(Rect r, string title, string sub, bool on = false, string icon = null)
    {
        bool hv = Hover(r); Color fill = on ? Signal : new Color(1f, 1f, 1f, hv ? 0.07f : 0.03f);
        Box(r, fill, on ? new Color(0, 0, 0, 0) : Line, 10f);
        Color ink = on ? SignalInk : Ink, dim = on ? new Color(SignalInk.r, SignalInk.g, SignalInk.b, 0.75f) : InkDim;
        float x = r.x + 12f; if (icon != null) { Tex(new Rect(x, r.y + (r.height - 18f) * 0.5f, 18f, 18f), Icon(icon), ink); x += 28f; }
        if (string.IsNullOrEmpty(sub)) Label(new Rect(x, r.y, r.width - (x - r.x) - 8f, r.height), title, T.Bold, ink);
        else { Label(new Rect(x, r.y + r.height * 0.5f - 17f, r.width - (x - r.x) - 8f, 18f), title, T.Bold, ink); Label(new Rect(x, r.y + r.height * 0.5f + 1f, r.width - (x - r.x) - 8f, 16f), sub, T.Dim, dim, null, 11); }
        return Clicked(r);
    }
    // gesegmenteerde keuze (.seg): geeft de aangeklikte index, of -1
    public static int Seg(Rect r, string[] labels, int sel, bool icons = false)
    {
        Box(r, new Color(1f, 1f, 1f, 0.06f), new Color(0, 0, 0, 0), 10f); int n = labels.Length, hit = -1; float w = (r.width - 6f - (n - 1) * 2f) / n;
        for (int k = 0; k < n; k++)
        {
            var b = new Rect(r.x + 3f + k * (w + 2f), r.y + 3f, w, r.height - 6f); bool on = k == sel, hv = Hover(b);
            if (on || hv) Box(b, on ? Signal : new Color(1f, 1f, 1f, 0.08f), new Color(0, 0, 0, 0), 8f);
            Color ink = on ? SignalInk : Ink;
            if (icons) Tex(new Rect(b.center.x - 9f, b.center.y - 9f, 18f, 18f), Icon(labels[k]), ink); else Label(b, labels[k], T.Bold, ink, TextAnchor.MiddleCenter);
            if (Clicked(b)) hit = k;
        }
        return hit;
    }
    // knoppenrij met een icoon en de naam ernaast
    public static int SegIcon(Rect r, string[] labels, string[] icons, int sel)
    {
        Box(r, new Color(1f, 1f, 1f, 0.06f), new Color(0, 0, 0, 0), 10f); int n = labels.Length, hit = -1; float w = (r.width - 6f - (n - 1) * 2f) / n;
        for (int k = 0; k < n; k++)
        {
            var b = new Rect(r.x + 3f + k * (w + 2f), r.y + 3f, w, r.height - 6f); bool on = k == sel, hv = Hover(b);
            if (on || hv) Box(b, on ? Signal : new Color(1f, 1f, 1f, 0.08f), new Color(0, 0, 0, 0), 8f);
            Color ink = on ? SignalInk : Ink; float tw = Width(labels[k], T.Bold) + 22f, x0 = b.center.x - tw * 0.5f;
            Tex(new Rect(x0, b.center.y - 9f, 18f, 18f), Icon(icons[k]), ink); Label(new Rect(x0 + 22f, b.y, tw, b.height), labels[k], T.Bold, ink, TextAnchor.MiddleLeft);
            if (Clicked(b)) hit = k;
        }
        return hit;
    }
    // snelbalkvak: zeshoek met het blokje (patroon, eventueel een pictogram erop) en het cijfer eronder (.hslot)
    public static bool HexSlot(Rect r, Texture2D swatch, string icon, string num, bool on)
    {
        bool hv = Hover(r);
        Tex(r, Hex(on ? SlotOn : hv ? SlotHover : SlotFill, on ? Signal : new Color(Ink.r, Ink.g, Ink.b, 0.24f), 2f));
        var sr = new Rect(r.center.x - 16f, r.y + r.height * 0.5f - 20f, 32f, 29f); Tex(sr, swatch);
        if (icon != null) Tex(new Rect(sr.center.x - 9f, sr.center.y - 9f, 18f, 18f), Icon(icon), Color.white);
        var oc = sNum.normal.textColor; sNum.normal.textColor = on ? Signal : InkDim; GUI.Label(new Rect(r.x, r.yMax - 14f, r.width, 12f), num, sNum); sNum.normal.textColor = oc;
        return Clicked(r);
    }
    // schuifje (tijd): geeft de nieuwe waarde 0..1
    static int sliderId; static bool sliderDrag;
    public static float Slider(Rect r, float v)
    {
        int id = GUIUtility.GetControlID(FocusType.Passive); var e = Event.current; Hit(r);
        if (e.type == EventType.MouseDown && r.Contains(e.mousePosition)) { GUIUtility.hotControl = id; sliderId = id; sliderDrag = true; e.Use(); }
        if (sliderDrag && sliderId == id)
        {
            if (e.type == EventType.MouseDrag || e.type == EventType.MouseDown) { v = Mathf.Clamp01((e.mousePosition.x - r.x) / r.width); GUI.changed = true; if (e.type == EventType.MouseDrag) e.Use(); }
            if (e.type == EventType.MouseUp || e.rawType == EventType.MouseUp) { sliderDrag = false; if (GUIUtility.hotControl == id) GUIUtility.hotControl = 0; }
        }
        float cy = r.center.y; Box(new Rect(r.x, cy - 2f, r.width, 4f), new Color(1f, 1f, 1f, 0.14f), new Color(0, 0, 0, 0), 2f);
        Box(new Rect(r.x, cy - 2f, r.width * v, 4f), Signal, new Color(0, 0, 0, 0), 2f);
        Box(new Rect(r.x + r.width * v - 7f, cy - 7f, 14f, 14f), Signal, PanelSolid, 7f);
        return v;
    }
    // achter een venster: het scherm iets donkerder, en klikken komen niet in de stad
    public static void DimBack(float a = 0.35f) { var r = new Rect(0, 0, W, H); Hit(r); Tex(r, Texture2D.whiteTexture, new Color(0, 0, 0, a)); }
    // na het tekenen van het venster: een klik die nergens anders op viel (dus naast het venster)
    public static bool DimClick() => GUI.Button(new Rect(0, 0, W, H), GUIContent.none, GUIStyle.none);
    public static void Kbd(Rect r, string k) { Box(r, new Color(0, 0, 0, 0), new Color(Ink.r, Ink.g, Ink.b, 0.25f), 4f); GUI.Label(r, k, sKbd); }
    public static void Toast(string s, float alpha, float bottom)
    {
        float w = Mathf.Min(W - 40f, Mathf.Max(220f, Width(s, T.Bold) + 40f)); var r = new Rect((W - w) * 0.5f, H - bottom - 38f, w, 38f);
        Box(r, new Color(PanelSolid.r, PanelSolid.g, PanelSolid.b, 0.94f * alpha), new Color(Line.r, Line.g, Line.b, Line.a * alpha), 19f);
        var oc = sToast.normal.textColor; sToast.normal.textColor = new Color(Ink.r, Ink.g, Ink.b, alpha); GUI.Label(r, s, sToast); sToast.normal.textColor = oc;
    }

    // ---------- pictogrammen: eenvoudige lijntekeningen, wit op doorzichtig (64×64), met GUI.color te kleuren ----------
    public static Texture2D Icon(string name)
    {
        string key = "i:" + name; if (texCache.TryGetValue(key, out var t) && t != null) return t;
        var sh = new List<System.Func<Vector2, float>>(); var cut = new List<System.Func<Vector2, float>>();
        System.Func<float, float, float, float, float, System.Func<Vector2, float>> L = (x0, y0, x1, y1, ww) => p => { Vector2 a = new Vector2(x0, y0), b = new Vector2(x1, y1), ab = b - a; float f = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude)); return ww * 0.5f - Vector2.Distance(p, a + ab * f); };
        System.Func<float, float, float, System.Func<Vector2, float>> D = (cx, cy, r) => p => r - Vector2.Distance(p, new Vector2(cx, cy));
        System.Func<float, float, float, float, System.Func<Vector2, float>> O = (cx, cy, r, ww) => p => ww * 0.5f - Mathf.Abs(Vector2.Distance(p, new Vector2(cx, cy)) - r);
        System.Func<float, float, float, float, float, float, System.Func<Vector2, float>> A = (cx, cy, r, a0, a1, ww) => p => { float ang = Mathf.Atan2(p.y - cy, p.x - cx) * Mathf.Rad2Deg; float m = Mathf.Repeat(ang - a0, 360f); if (m > Mathf.Repeat(a1 - a0, 360f)) { Vector2 e0 = new Vector2(cx, cy) + new Vector2(Mathf.Cos(a0 * Mathf.Deg2Rad), Mathf.Sin(a0 * Mathf.Deg2Rad)) * r, e1 = new Vector2(cx, cy) + new Vector2(Mathf.Cos(a1 * Mathf.Deg2Rad), Mathf.Sin(a1 * Mathf.Deg2Rad)) * r; return ww * 0.5f - Mathf.Min(Vector2.Distance(p, e0), Vector2.Distance(p, e1)); } return ww * 0.5f - Mathf.Abs(Vector2.Distance(p, new Vector2(cx, cy)) - r); };
        System.Func<Vector2[], System.Func<Vector2, float>> P = pts => p => { float d = 1e9f; bool inside = false; for (int k = 0, j = pts.Length - 1; k < pts.Length; j = k++) { Vector2 a = pts[j], b = pts[k], ab = b - a; float f = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude)); d = Mathf.Min(d, Vector2.Distance(p, a + ab * f)); if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside; } return inside ? d : -d; };
        System.Action cloud = () => { sh.Add(D(0.36f, 0.56f, 0.15f)); sh.Add(D(0.54f, 0.46f, 0.19f)); sh.Add(D(0.70f, 0.58f, 0.13f)); sh.Add(L(0.34f, 0.65f, 0.72f, 0.65f, 0.12f)); };
        const float w = 0.085f;
        switch (name)
        {
            case "zon": sh.Add(D(0.5f, 0.5f, 0.16f)); for (int k = 0; k < 8; k++) { float a = k * Mathf.PI / 4f; sh.Add(L(0.5f + Mathf.Cos(a) * 0.27f, 0.5f + Mathf.Sin(a) * 0.27f, 0.5f + Mathf.Cos(a) * 0.39f, 0.5f + Mathf.Sin(a) * 0.39f, w)); } break;
            case "maan": sh.Add(D(0.5f, 0.5f, 0.3f)); cut.Add(D(0.64f, 0.38f, 0.26f)); break;
            case "bewolkt": cloud(); break;
            case "regen": cloud(); for (int k = 0; k < 3; k++) sh.Add(L(0.38f + k * 0.13f, 0.76f, 0.33f + k * 0.13f, 0.9f, w)); break;
            case "onweer": cloud(); sh.Add(P(new[] { new Vector2(0.52f, 0.66f), new Vector2(0.42f, 0.82f), new Vector2(0.52f, 0.81f), new Vector2(0.46f, 0.96f), new Vector2(0.62f, 0.76f), new Vector2(0.52f, 0.77f), new Vector2(0.58f, 0.66f) })); break;
            case "mist": for (int k = 0; k < 3; k++) sh.Add(L(0.22f + (k % 2) * 0.06f, 0.34f + k * 0.16f, 0.78f - (k % 2) * 0.06f, 0.34f + k * 0.16f, w)); break;
            case "sneeuw": for (int k = 0; k < 3; k++) { float a = k * Mathf.PI / 3f + Mathf.PI / 2f; sh.Add(L(0.5f - Mathf.Cos(a) * 0.32f, 0.5f - Mathf.Sin(a) * 0.32f, 0.5f + Mathf.Cos(a) * 0.32f, 0.5f + Mathf.Sin(a) * 0.32f, w)); } break;
            case "wissel": sh.Add(A(0.5f, 0.5f, 0.27f, 200f, 340f, w)); sh.Add(A(0.5f, 0.5f, 0.27f, 20f, 160f, w)); sh.Add(P(new[] { new Vector2(0.80f, 0.30f), new Vector2(0.84f, 0.50f), new Vector2(0.66f, 0.44f) })); sh.Add(P(new[] { new Vector2(0.20f, 0.70f), new Vector2(0.16f, 0.50f), new Vector2(0.34f, 0.56f) })); break;
            case "geluid": case "stil":
                sh.Add(P(new[] { new Vector2(0.16f, 0.40f), new Vector2(0.30f, 0.40f), new Vector2(0.48f, 0.24f), new Vector2(0.48f, 0.76f), new Vector2(0.30f, 0.60f), new Vector2(0.16f, 0.60f) }));
                if (name == "geluid") { sh.Add(A(0.5f, 0.5f, 0.14f, -50f, 50f, w)); sh.Add(A(0.5f, 0.5f, 0.27f, -50f, 50f, w)); } else { sh.Add(L(0.6f, 0.38f, 0.84f, 0.62f, w)); sh.Add(L(0.6f, 0.62f, 0.84f, 0.38f, w)); }
                break;
            case "ongedaan": sh.Add(A(0.52f, 0.56f, 0.24f, 200f, 90f, w)); sh.Add(P(new[] { new Vector2(0.16f, 0.36f), new Vector2(0.38f, 0.30f), new Vector2(0.30f, 0.52f) })); break;
            case "gum": sh.Add(P(new[] { new Vector2(0.20f, 0.62f), new Vector2(0.50f, 0.30f), new Vector2(0.78f, 0.56f), new Vector2(0.50f, 0.86f) })); cut.Add(P(new[] { new Vector2(0.29f, 0.62f), new Vector2(0.50f, 0.40f), new Vector2(0.69f, 0.56f), new Vector2(0.50f, 0.77f) })); sh.Add(L(0.36f, 0.46f, 0.64f, 0.72f, w)); sh.Add(L(0.48f, 0.86f, 0.82f, 0.86f, w)); break;
            case "raster": for (int k = 0; k < 6; k++) { float a0 = k * Mathf.PI / 3f, a1 = (k + 1) * Mathf.PI / 3f; sh.Add(L(0.5f + Mathf.Cos(a0) * 0.32f, 0.5f + Mathf.Sin(a0) * 0.32f, 0.5f + Mathf.Cos(a1) * 0.32f, 0.5f + Mathf.Sin(a1) * 0.32f, w)); } break;
            case "huis": sh.Add(L(0.18f, 0.48f, 0.5f, 0.2f, w)); sh.Add(L(0.5f, 0.2f, 0.82f, 0.48f, w)); sh.Add(L(0.28f, 0.4f, 0.28f, 0.8f, w)); sh.Add(L(0.72f, 0.4f, 0.72f, 0.8f, w)); sh.Add(L(0.28f, 0.8f, 0.72f, 0.8f, w)); sh.Add(L(0.44f, 0.8f, 0.44f, 0.6f, w)); sh.Add(L(0.56f, 0.8f, 0.56f, 0.6f, w)); sh.Add(L(0.44f, 0.6f, 0.56f, 0.6f, w)); break;
            case "lopen": sh.Add(D(0.56f, 0.17f, 0.085f)); sh.Add(L(0.52f, 0.32f, 0.46f, 0.58f, 0.11f)); sh.Add(L(0.46f, 0.58f, 0.6f, 0.72f, w)); sh.Add(L(0.6f, 0.72f, 0.58f, 0.88f, w)); sh.Add(L(0.46f, 0.58f, 0.36f, 0.88f, w)); sh.Add(L(0.5f, 0.38f, 0.66f, 0.5f, w)); sh.Add(L(0.5f, 0.38f, 0.34f, 0.48f, w)); break;
            case "ophogen": sh.Add(L(0.16f, 0.8f, 0.84f, 0.8f, w)); sh.Add(L(0.5f, 0.66f, 0.5f, 0.32f, w)); sh.Add(P(new[] { new Vector2(0.3f, 0.42f), new Vector2(0.5f, 0.16f), new Vector2(0.7f, 0.42f) })); break;
            case "afgraven": sh.Add(L(0.16f, 0.2f, 0.84f, 0.2f, w)); sh.Add(L(0.5f, 0.34f, 0.5f, 0.68f, w)); sh.Add(P(new[] { new Vector2(0.3f, 0.58f), new Vector2(0.5f, 0.84f), new Vector2(0.7f, 0.58f) })); break;
            case "egaliseren": sh.Add(L(0.16f, 0.4f, 0.84f, 0.4f, w)); sh.Add(L(0.16f, 0.62f, 0.84f, 0.62f, w)); break;
            case "gladstrijken": for (int k = 0; k < 8; k++) { float x0 = 0.14f + k * 0.09f, x1 = x0 + 0.09f; sh.Add(L(x0, 0.5f + Mathf.Sin(k * 0.9f) * 0.16f, x1, 0.5f + Mathf.Sin((k + 1) * 0.9f) * 0.16f, w)); } break;
            case "helling": sh.Add(P(new[] { new Vector2(0.16f, 0.8f), new Vector2(0.84f, 0.8f), new Vector2(0.84f, 0.24f) })); break;
            case "rand":
                for (int k = 0; k < 6; k++) { float a0 = k * Mathf.PI / 3f + Mathf.PI / 6f, a1 = (k + 1) * Mathf.PI / 3f + Mathf.PI / 6f; sh.Add(L(0.5f + Mathf.Cos(a0) * 0.34f, 0.5f + Mathf.Sin(a0) * 0.34f, 0.5f + Mathf.Cos(a1) * 0.34f, 0.5f + Mathf.Sin(a1) * 0.34f, k == 1 ? w * 1.9f : w * 0.6f)); }   // een zeshoek met één dikke rand
                break;
            case "pijl": sh.Add(P(new[] { new Vector2(0.18f, 0.2f), new Vector2(0.86f, 0.5f), new Vector2(0.18f, 0.8f), new Vector2(0.34f, 0.5f) })); break;
            case "zoek": sh.Add(O(0.44f, 0.44f, 0.2f, w)); sh.Add(L(0.59f, 0.59f, 0.8f, 0.8f, 0.11f)); break;
            case "sluit": sh.Add(L(0.28f, 0.28f, 0.72f, 0.72f, w)); sh.Add(L(0.28f, 0.72f, 0.72f, 0.28f, w)); break;
            case "menu": for (int k = 0; k < 3; k++) sh.Add(L(0.24f, 0.3f + k * 0.2f, 0.76f, 0.3f + k * 0.2f, w)); break;
            case "mensen": sh.Add(D(0.38f, 0.32f, 0.11f)); sh.Add(A(0.38f, 0.78f, 0.22f, 180f, 360f, w)); sh.Add(D(0.66f, 0.36f, 0.09f)); sh.Add(A(0.66f, 0.78f, 0.17f, 220f, 360f, w)); break;
            case "auto": sh.Add(P(new[] { new Vector2(0.12f, 0.66f), new Vector2(0.16f, 0.5f), new Vector2(0.3f, 0.48f), new Vector2(0.4f, 0.34f), new Vector2(0.64f, 0.34f), new Vector2(0.74f, 0.48f), new Vector2(0.88f, 0.52f), new Vector2(0.88f, 0.66f) })); cut.Add(D(0.3f, 0.68f, 0.11f)); cut.Add(D(0.7f, 0.68f, 0.11f)); sh.Add(D(0.3f, 0.68f, 0.07f)); sh.Add(D(0.7f, 0.68f, 0.07f)); break;
            case "licht": sh.Add(P(new[] { new Vector2(0.38f, 0.14f), new Vector2(0.62f, 0.14f), new Vector2(0.62f, 0.74f), new Vector2(0.38f, 0.74f) })); cut.Add(D(0.5f, 0.27f, 0.07f)); cut.Add(D(0.5f, 0.44f, 0.07f)); cut.Add(D(0.5f, 0.61f, 0.07f)); sh.Add(L(0.5f, 0.74f, 0.5f, 0.92f, w)); break;
            case "kompas": sh.Add(O(0.5f, 0.5f, 0.32f, w)); sh.Add(P(new[] { new Vector2(0.5f, 0.24f), new Vector2(0.58f, 0.5f), new Vector2(0.5f, 0.76f), new Vector2(0.42f, 0.5f) })); break;
            case "boek": sh.Add(L(0.5f, 0.28f, 0.5f, 0.8f, w)); sh.Add(L(0.18f, 0.26f, 0.5f, 0.3f, w)); sh.Add(L(0.82f, 0.26f, 0.5f, 0.3f, w)); sh.Add(L(0.18f, 0.26f, 0.18f, 0.76f, w)); sh.Add(L(0.82f, 0.26f, 0.82f, 0.76f, w)); sh.Add(L(0.18f, 0.76f, 0.5f, 0.8f, w)); sh.Add(L(0.82f, 0.76f, 0.5f, 0.8f, w)); break;
            case "leeg": for (int k = 0; k < 6; k++) { float a0 = k * Mathf.PI / 3f + Mathf.PI / 6f, a1 = a0 + Mathf.PI / 3f; sh.Add(L(0.5f + Mathf.Cos(a0) * 0.32f, 0.5f + Mathf.Sin(a0) * 0.32f, 0.5f + Mathf.Cos(a1) * 0.32f, 0.5f + Mathf.Sin(a1) * 0.32f, w)); } break;
            case "stad": sh.Add(L(0.2f, 0.82f, 0.8f, 0.82f, w)); sh.Add(L(0.26f, 0.82f, 0.26f, 0.46f, w)); sh.Add(L(0.26f, 0.46f, 0.44f, 0.46f, w)); sh.Add(L(0.44f, 0.82f, 0.44f, 0.22f, w)); sh.Add(L(0.44f, 0.22f, 0.66f, 0.22f, w)); sh.Add(L(0.66f, 0.22f, 0.66f, 0.82f, w)); sh.Add(L(0.66f, 0.52f, 0.76f, 0.52f, w)); sh.Add(L(0.76f, 0.52f, 0.76f, 0.82f, w)); break;
            default: sh.Add(D(0.5f, 0.5f, 0.2f)); break;
        }
        const int N = 64; t = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp }; var px = new Color[N * N];
        for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
        {
            Vector2 p = new Vector2((x + 0.5f) / N, (y + 0.5f) / N); float a = 0f;
            foreach (var f in sh) a = Mathf.Max(a, Mathf.Clamp01(f(p) * N + 0.5f));
            foreach (var f in cut) a *= 1f - Mathf.Clamp01(f(p) * N + 0.5f);
            px[(N - 1 - y) * N + x] = new Color(1f, 1f, 1f, a);
        }
        t.SetPixels(px); t.Apply(); texCache[key] = t; return t;
    }

    public static Color ToolColor(string t)
    {
        switch (t)
        {
            case "weg": return new Color(0.3f, 0.32f, 0.35f); case "woning": return new Color(0.75f, 0.35f, 0.25f); case "kantoor": return new Color(0.55f, 0.65f, 0.78f);
            case "park": return new Color(0.4f, 0.68f, 0.3f); case "water": return new Color(0.22f, 0.5f, 0.78f); case "meer": return new Color(0.3f, 0.62f, 0.8f);
            case "pad": return new Color(0.82f, 0.76f, 0.63f); case "dak": return new Color(0.6f, 0.3f, 0.22f); case "heuvel": case "terrein": return new Color(0.5f, 0.7f, 0.35f);
            case "kuil": return new Color(0.35f, 0.5f, 0.28f); case "blok": return new Color(0.6f, 0.58f, 0.56f);
            case "winkel": return new Color(0.85f, 0.3f, 0.28f); case "plein": return new Color(0.72f, 0.58f, 0.48f); case "toren": return new Color(0.45f, 0.48f, 0.55f); case "molen": return new Color(0.92f, 0.9f, 0.84f);
            case "gum": return new Color(0.9f, 0.5f, 0.6f);
            default: return Color.gray;
        }
    }
}
